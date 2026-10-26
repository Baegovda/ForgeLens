using Avalonia;
using System.Globalization;
using System.Numerics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2Browser.Effects;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   Effects on a model, from loose files:
///   <c>--hostvfxbench &lt;tplDir&gt; &lt;clip.sani|-&gt; &lt;sfxDir&gt; &lt;part_aliases.ps&gt; &lt;texDir&gt; &lt;out&gt; &lt;effect|-&gt; [attachBone|-] [seconds] [every]</c>.
///   <c>SM2_VFX_BLUEPRINTS</c>=dir of blueprint .sso turns on anim-event effects for the clip;
///   <c>SM2_HOST_CAM</c>="yaw:pitch:fit[:bone]" frames the view (fit = fraction of the model fit distance, bone = look-at).
/// </summary>
internal static class HostVfxBench
{
  public static int Run(string[] args)
  {
    var dir = args[1];
    var saniPath = args[2];
    var output = args[6];
    var effect = args[7] == "-" ? null : args[7];
    var attach = args.Length > 8 && args[8] != "-" ? args[8] : "";
    var seconds = args.Length > 9 ? float.Parse(args[9], CultureInfo.InvariantCulture) : 1f;
    var every = args.Length > 10 ? int.Parse(args[10]) : 6;
    const int Size = 720;
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string text)
    {
      log.AppendLine(text);
      File.WriteAllText(Path.Combine(output, "hostvfxbench.txt"), log.ToString());
      Console.WriteLine(text);
    }

    var stem = Path.GetFileName(dir.TrimEnd('/')).Replace(".tpl", "");
    var mesh = TemplateMeshReader.LoadRaw(File.ReadAllBytes(Path.Combine(dir, stem + ".tpl")),
      File.Exists(Path.Combine(dir, stem + ".tpl_data")) ? File.ReadAllBytes(Path.Combine(dir, stem + ".tpl_data")) : null, stem);
    // Loose tpls carry LOD1 + merged Object* proxies only (LOD0 lives in lods_base): SM2_HOST_LOD1=1 shows LOD1 instead.
    if (Environment.GetEnvironmentVariable("SM2_HOST_LOD1") == "1")
      foreach (var part in mesh.Parts)
        (part.Visible, part.HideReason) = part.HideReason == "LOD" ? (true, null) : (false, "proxy");
    AnimationClip? clip = saniPath == "-" ? null : SaniReader.FromBytes(File.ReadAllBytes(saniPath),
      File.Exists(saniPath + "_data") ? File.ReadAllBytes(saniPath + "_data") : null, Path.GetFileNameWithoutExtension(saniPath));
    var vfx = new HostVfx
    {
      Library = new EffectLibrary(new DirectoryEffectSource(args[3])),
      Aliases = PartAliases.Load(args[4]),
      Textures = EffectTextures.FromDirectory(args[5]),
      Meshes = EffectMeshes.FromDirectory(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[5]).TrimEnd('/')) ?? ".", "mesh"), args[5]),
    };
    if (Environment.GetEnvironmentVariable("SM2_VFX_BLUEPRINTS") is { Length: > 0 } bp)
      vfx.Map = AnimEventVfxMap.FromDirectory(bp);
    vfx.SourceFilter = Environment.GetEnvironmentVariable("SM2_VFX_BP_FILTER") ?? "";
    mesh.Clip = clip;
    Line($"hostvfxbench {DateTime.Now:yyyy-MM-dd HH:mm:ss} {stem}: bones {mesh.Bones.Count}; clip {clip?.Name ?? "-"} {clip?.Duration:0.00}s; tracks {clip?.EventTracks.Count ?? 0}");
    if (clip is not null)
      foreach (var t in clip.EventTracks)
        Line($"  track {t.Name}: " + string.Join("; ", t.Events.Select(e => $"{e.Start * clip.Duration:0.000}s+{e.Duration * clip.Duration:0.000} {string.Join(",", e.Tags)}")));
    if (effect is not null)
      Line("attach: " + vfx.SetEffect(mesh, effect, attach));
    if (vfx.Map.Files > 0 && clip is not null)
      Line("events: " + vfx.SetEvents(mesh, true));
    vfx.Textures.Prefetch(vfx.Single?.Textures ?? []);

    var fit = MeshPreview.FitDistance(mesh, 1f);
    var cam = (Yaw: 0.9f, Pitch: 0.15f, Fit: 1f, Bone: "");
    if (Environment.GetEnvironmentVariable("SM2_HOST_CAM") is { Length: > 0 } spec)
    {
      var c = spec.Split(':');
      cam = (F(c[0]), F(c[1]), F(c[2]), c.Length > 3 ? c[3] : "");
    }

    static float F(string text) => float.Parse(text, CultureInfo.InvariantCulture);
    var settings = new PreviewSettings { InPlace = true, Physics = false };
    var steps = (int)(seconds * 60);
    var exit = 0;
    var done = false;
    Window window = null!;
    GpuPreviewControl control = null!;
    Func<GlPreview, int, bool> hook = (gpu, fb) =>
    {
      if (done) return false;
      Line($"GL: {gpu.Info}");
      vfx.Textures.WaitAll(TimeSpan.FromSeconds(30));
      var host = new EffectHost(mesh.Bones.Select(b => b.Name).ToList());
      for (var i = 0; i <= steps; i++)
      {
        var t = i / 60f;
        if (i % every != 0) continue;
        var probe = MeshPreview.BuildScene(mesh, 64, 64, new OrbitCamera(), true, t, settings);
        host.SetPose(probe.BoneWorlds ?? MeshPreview.BoneWorlds(mesh, t));
        Vector3? target = cam.Bone.Length > 0 && host.World(cam.Bone) is { } w ? w.Translation : null;
        var camera = new OrbitCamera { Yaw = cam.Yaw, Pitch = cam.Pitch, Distance = fit * cam.Fit, Target = target };
        foreach (var with in i == 0 ? new[] { false, true } : new[] { true })
        {
          var scene = MeshPreview.BuildScene(mesh, Size, Size, camera, false, t, settings);
          gpu.Effect = with ? vfx.Frame(mesh, scene, t, t) : null;
          gpu.Render(mesh, scene, Size, Size, false, fb);
          for (var guard = 0; gpu.Pending && guard < 50; guard++)
          {
            scene = MeshPreview.BuildScene(mesh, Size, Size, camera, false, t, settings);
            gpu.Effect = with ? vfx.Frame(mesh, scene, t, t) : null;
            gpu.Render(mesh, scene, Size, Size, false, fb);
          }

          var name = with ? $"f{i:000}_{t:0.00}s" : "f000_model_only";
          PreviewBench.Png(Path.Combine(output, name + ".png"), gpu.ReadPixels(), gpu.Width, gpu.Height);
          var layer = gpu.Effect;
          Line($"{name}: quads {layer?.Quads ?? 0} meshes {layer?.MeshInstances ?? 0} lights {layer?.Lights.Count ?? 0}");
        }
      }

      done = true;
      Dispatcher.UIThread.Post(() => window.Close());
      return false;
    };

    App.WindowFactory = () =>
    {
      control = new GpuPreviewControl { BenchHook = hook };
      window = new Window { Width = 760, Height = 760, Title = "SM2 host vfx bench", Content = control, ShowActivated = false };
      DispatcherTimer.RunOnce(() =>
      {
        if (done) return;
        Line(control.HasFailed ? "GL FAILED: " + control.FailReason : "timeout");
        exit = 3;
        window.Close();
      }, TimeSpan.FromMinutes(10));
      control.Failed += reason =>
      {
        Line("GL FAILED: " + reason);
        exit = 3;
        window.Close();
      };
      window.Opened += (_, _) => control.Show(new GpuFrameRequest(mesh, default, false, false, 0, settings));
      return window;
    };
    Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    return exit;
  }
}
