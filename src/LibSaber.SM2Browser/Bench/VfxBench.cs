using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2Browser.Effects;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   Effect capture from loose files: <c>--vfxbench &lt;sfx root&gt; &lt;part_aliases.ps&gt; &lt;texture root&gt; &lt;out&gt; name[@seconds] ...</c>.
///   Per effect: plays the fixed-step simulation (seed 1), renders 8 frames spread over the run into a contact sheet
///   (<c>&lt;name&gt;_sheet.png</c>), checks determinism (a second run from scratch must match bit-for-bit), and logs
///   particle counts and CPU/GPU milliseconds per frame.
/// </summary>
internal static class VfxBench
{
  /// <summary>Debug: SM2_VFX_ONLY / SM2_VFX_SKIP = comma-separated emitter-name substrings.</summary>
  private static readonly string[] Only = (Environment.GetEnvironmentVariable("SM2_VFX_ONLY") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
  private static readonly string[] Skip = (Environment.GetEnvironmentVariable("SM2_VFX_SKIP") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
  private static bool Filter(EmitterDef e) =>
    (Only.Length == 0 || Only.Any(o => e.Name.Contains(o, StringComparison.OrdinalIgnoreCase))) &&
    !Skip.Any(o => e.Name.Contains(o, StringComparison.OrdinalIgnoreCase));

  static VfxBench()
  {
    if (float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_LIT"), System.Globalization.CultureInfo.InvariantCulture, out var lit)) EffectSprites.LitScale = lit;
  }

  public static int Run(string[] args)
  {
    var library = new EffectLibrary(new DirectoryEffectSource(args[1]));
    var aliases = PartAliases.Load(args[2]);
    var textures = EffectTextures.FromDirectory(args[3]);
    var meshes = EffectMeshes.FromDirectory(Environment.GetEnvironmentVariable("SM2_VFX_MESHES") is { Length: > 0 } md ? md : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[3]).TrimEnd('/')) ?? ".", "mesh"), args[3]);
    var output = args[4];
    var names = args.Skip(5).ToList();
    Directory.CreateDirectory(output);
    const int Size = 512;
    const int Cols = 4, Rows = 2;
    var log = new StringBuilder();
    void Line(string text)
    {
      log.AppendLine(text);
      File.WriteAllText(Path.Combine(output, "vfxbench.txt"), log.ToString());
      Console.WriteLine(text);
    }

    Line($"vfxbench {DateTime.Now:yyyy-MM-dd HH:mm:ss}: effects {library.Names.Count()} aliases {aliases.Count}");
    var exit = 0;
    var done = false;
    Window window = null!;
    Func<GlPreview, int, bool> hook = (gpu, fb) =>
    {
      if (done) return false;
      Line($"GL: {gpu.Info}");
      foreach (var spec in names)
      {
        var parts = spec.Split('@');
        var name = parts[0];
        var tree = library.Resolve(name);
        if (tree is null)
        {
          Line($"{name}: not found");
          continue;
        }

        var def = EffectDefinition.Build(name, tree);
        var seconds = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : Math.Min(def.Duration, 3f);
        textures.Prefetch(def.Textures);
        meshes.Prefetch(def.Emitters.Select(e => e.Geom?.Template));
        meshes.WaitAll(TimeSpan.FromMinutes(2));
        textures.Prefetch(def.Emitters.Where(e => e.Geom is not null).SelectMany(e => meshes.Get(e.Geom!.Template)?.Variants.SelectMany(v => v.Select(x => x.Material.Texture)) ?? []));
        textures.WaitAll(TimeSpan.FromMinutes(2));
        var missingMeshes = def.Emitters.Where(e => e.Geom is not null && meshes.Get(e.Geom.Template) is { IsMissing: true }).Select(e => e.Geom!.Template).Distinct().ToList();
        var missing = def.Drawables.Where(e => e.Texture is { Length: > 0 } t && textures.IsMissing(t)).Select(e => e.Texture).Distinct().ToList();
        var sprites = def.Emitters.Count(e => e.IsSprite);
        var meshCount = def.Emitters.Count(e => e.Geom is not null);
        Line($"{name}: emitters {def.Emitters.Count} (sprite {sprites}, mesh {meshCount}, other {def.Emitters.Count - sprites - meshCount}) lights {def.Lights.Count} trails {def.Trails.Count} other {def.Unsupported.Count} [{string.Join(", ", def.Unsupported.Select(u => u.Type).Distinct())}] duration {def.Duration:0.00}s capture {seconds:0.00}s missing textures [{string.Join(", ", missing)}] missing meshes [{string.Join(", ", missingMeshes)}]");
        var instance = EffectInstance.Standalone(def, 1);
        var camera = EffectCamera.Fit(instance, seconds);
        if (parts.Length > 2 && float.TryParse(parts[2], CultureInfo.InvariantCulture, out var distance))
          camera = camera with { Distance = distance };
        var (view, projection, eye) = camera.Matrices(Size, Size);
        var sheet = new byte[Cols * Size * Rows * Size * 4];
        var simMs = new List<double>(); var buildMs = new List<double>(); var gpuMs = new List<double>();
        var peak = 0;
        var frames = Cols * Rows;
        var fingerprint = new StringBuilder();
        for (var f = 0; f < frames; f++)
        {
          var t = seconds * (f + 0.5f) / frames;
          var w = Stopwatch.StartNew();
          instance.Seek(t);
          simMs.Add(w.Elapsed.TotalMilliseconds);
          peak = Math.Max(peak, instance.ParticleCount);
          w.Restart();
          var layer = new EffectLayer
          {
            Exposure = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_EXPOSURE"), System.Globalization.CultureInfo.InvariantCulture, out var ex) ? ex : 1f / 250f,
            Batches = EffectSprites.Build(instance, aliases, view, Filter),
            Lights = instance.Lights().ToList(),
            Texture = textures.Get,
            Meshes = EffectMeshParticles.Build(instance),
            Mesh = meshes.Get,
          };
          buildMs.Add(w.Elapsed.TotalMilliseconds);
          fingerprint.Append(Hash(layer)).Append(';');
          w.Restart();
          gpu.RenderEffect(layer, view, projection, eye, Size, Size, fb);
          gpu.Finish();
          gpuMs.Add(w.Elapsed.TotalMilliseconds);
          var pixels = gpu.ReadPixels();
          var ox = f % Cols * Size;
          var oy = f / Cols * Size;
          for (var y = 0; y < Size; y++)
            Buffer.BlockCopy(pixels, y * Size * 4, sheet, ((oy + y) * Cols * Size + ox) * 4, Size * 4);
          if (Environment.GetEnvironmentVariable("SM2_VFX_FRAMES") == "1")
            PreviewBench.Png(Path.Combine(output, $"{name}_f{f}_{t:0.000}s.png"), pixels, Size, Size);
          Line($"  t={t:0.000}s particles {instance.ParticleCount} quads {layer.Quads} meshes {layer.MeshInstances} batches {layer.Batches.Count} lights {layer.Lights.Count} distortion {gpu.DistortionQuads}");
        }

        PreviewBench.Png(Path.Combine(output, name + "_sheet.png"), sheet, Cols * Size, Rows * Size);
        // Determinism: replay from zero with the same seed (scrub backwards) and compare every frame's quads.
        var replay = new StringBuilder();
        var again = EffectInstance.Standalone(def, 1);
        for (var f = frames - 1; f >= 0; f--)
          again.Seek(seconds * (f + 0.5f) / frames);
        for (var f = 0; f < frames; f++)
        {
          again.Seek(seconds * (f + 0.5f) / frames);
          replay.Append(Hash(new EffectLayer { Batches = EffectSprites.Build(again, aliases, view), Lights = [], Texture = textures.Get })).Append(';');
        }

        var sw = Stopwatch.StartNew();
        var full = EffectInstance.Standalone(def, 1);
        full.Seek(seconds);
        var stepMs = sw.Elapsed.TotalMilliseconds / Math.Max(1, full.StepCount);
        Line($"  peak particles {peak}; sim {stepMs:0.000} ms/step (60 Hz); sprite build mean {buildMs.Average():0.00} ms; GPU draw+finish mean {gpuMs.Average():0.00} ms max {gpuMs.Max():0.00} ms; deterministic {(fingerprint.ToString() == replay.ToString() ? "yes" : "NO")}");
      }

      done = true;
      Dispatcher.UIThread.Post(() => window.Close());
      return false;
    };

    App.WindowFactory = () =>
    {
      var control = new GpuPreviewControl { BenchHook = hook };
      window = new Window { Width = 560, Height = 560, Title = "SM2 vfx bench", Content = control, ShowActivated = false };
      DispatcherTimer.RunOnce(() =>
      {
        if (done) return;
        Line(control.HasFailed ? "GL FAILED: " + control.FailReason : "timeout");
        exit = 3;
        window.Close();
      }, TimeSpan.FromMinutes(20));
      control.Failed += reason =>
      {
        Line("GL FAILED: " + reason);
        exit = 3;
        window.Close();
      };
      window.Opened += (_, _) => control.RequestNextFrameRendering();
      return window;
    };
    Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    return exit;
  }

  private static string Hash(EffectLayer layer)
  {
    var h = new HashCode();
    foreach (var b in layer.Batches)
    {
      h.Add(b.QuadCount);
      for (var i = 0; i < b.QuadCount * 4 * SpriteBatch.Stride; i++)
        h.Add(BitConverter.SingleToInt32Bits(b.Vertices[i]));
    }

    return h.ToHashCode().ToString("x8");
  }
}
