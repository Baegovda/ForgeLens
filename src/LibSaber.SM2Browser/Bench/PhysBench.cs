using System.Diagnostics;
using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   Physics capture from loose files (no game install needed):
///   <c>--physbench &lt;dir with X.tpl + X.tpl_data&gt; &lt;clip.sani&gt; &lt;cls root&gt; &lt;out&gt; [seconds] [every]</c>.
///   Plays the clip at 60 Hz twice (physics off, then on), saves every Nth frame from two cameras that follow the
///   pelvis, and logs physics ms per step and the mean pixel difference between the two runs.
/// </summary>
internal static class PhysBench
{
  public static int Run(string[] args)
  {
    var dir = args[1];
    var saniPath = args[2];
    var clsRoot = args[3];
    var output = args[4];
    var seconds = args.Length > 5 ? float.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 2f;
    var every = args.Length > 6 ? int.Parse(args[6]) : 10;
    const int Size = 640;
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string text)
    {
      log.AppendLine(text);
      File.WriteAllText(Path.Combine(output, "physbench.txt"), log.ToString());
      Console.WriteLine(text);
    }

    var stem = Path.GetFileName(dir.TrimEnd('/')).Replace(".tpl", "");
    var mesh = TemplateMeshReader.LoadRaw(File.ReadAllBytes(Path.Combine(dir, stem + ".tpl")),
      File.Exists(Path.Combine(dir, stem + ".tpl_data")) ? File.ReadAllBytes(Path.Combine(dir, stem + ".tpl_data")) : null, stem);
    var clip = SaniReader.FromBytes(File.ReadAllBytes(saniPath),
      File.Exists(saniPath + "_data") ? File.ReadAllBytes(saniPath + "_data") : null, Path.GetFileNameWithoutExtension(saniPath));
    mesh.Physics = PhysicsLocator.Find(new PhysicsLocator.DirectorySource(clsRoot), stem + ".tpl");
    Line($"physbench {DateTime.Now:yyyy-MM-dd HH:mm:ss} {stem}: verts {mesh.Positions.Count} bones {mesh.Bones.Count} sims {mesh.ClothSims.Count}; clip {clip.Name} {clip.Duration:0.00}s tracks {clip.Tracks.Count}");
    Line($"class {mesh.Physics?.SourceClass}: cloths {mesh.Physics?.Cloths.Count} jiggles {mesh.Physics?.Jiggles.Count}");
    var runtime = MeshPreview.PhysicsFor(mesh);
    if (runtime is not null)
      Line($"runtime: cloths {runtime.Cloths.Count} jiggles {runtime.JiggleCount} missing [{string.Join(", ", runtime.Missing)}] notes [{string.Join(" | ", runtime.BindNotes)}]");

    // Frame on the cloth shells (else the whole model) and follow the pelvis.
    var focus = Vector3.Zero;
    var count = 0;
    foreach (var sim in mesh.ClothSims)
      foreach (var p in sim.Positions)
      {
        focus += p;
        count++;
      }

    var fit = MeshPreview.FitDistance(mesh, 1f);
    focus = count > 0 ? focus / count : Vector3.Zero;
    var pelvis = mesh.Bones.FindIndex(b => b.Name.Contains("pelvis", StringComparison.OrdinalIgnoreCase));
    if (pelvis < 0)
      pelvis = mesh.Bones.FindIndex(b => b.Name.Contains("hip", StringComparison.OrdinalIgnoreCase));
    var steps = (int)(seconds * 60);
    var cameras = new (string Name, float Yaw, float Pitch, float Distance)[] { ("rear34", 0.7f, 0.1f, 0.9f), ("front34", 2.5f, 0.1f, 0.9f) };
    // SM2_PHYS_CAMS="name:yaw:pitch:distance,..." overrides the views (distance in model fits).
    if (Environment.GetEnvironmentVariable("SM2_PHYS_CAMS") is { Length: > 0 } spec)
      cameras = spec.Split(',').Select(c => c.Split(':')).Select(c => (c[0], F(c[1]), F(c[2]), F(c[3]))).ToArray();
    static float F(string text) => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    var settingsOff = new PreviewSettings { InPlace = true, Physics = false };
    var settingsOn = settingsOff with { Physics = true };
    // SM2_PHYS_BAKE=1 adds a third run: a second copy of the model with the exporter's bake (cloth helper bones +
    // baked jiggle/cloth curves, PhysicsBake) played as plain skinning with physics off, i.e. what Unreal plays.
    ExportedMesh? baked = null;
    if (Environment.GetEnvironmentVariable("SM2_PHYS_BAKE") == "1" && mesh.Physics is { IsEmpty: false } setup)
    {
      baked = TemplateMeshReader.LoadRaw(File.ReadAllBytes(Path.Combine(dir, stem + ".tpl")),
        File.Exists(Path.Combine(dir, stem + ".tpl_data")) ? File.ReadAllBytes(Path.Combine(dir, stem + ".tpl_data")) : null, stem);
      var bakeWatch = Stopwatch.StartNew();
      var maxBones = int.TryParse(Environment.GetEnvironmentVariable("SM2_PHYS_MAXBONES"), out var mb) ? mb : PhysicsBake.MaxBonesPerCloth;
      var perBone = int.TryParse(Environment.GetEnvironmentVariable("SM2_PHYS_PPB"), out var pb) ? pb : 1;
      var rig = PhysicsBake.AddClothBones(baked, setup, perBone, maxBones);
      var bakeClip = SaniReader.FromBytes(File.ReadAllBytes(saniPath),
        File.Exists(saniPath + "_data") ? File.ReadAllBytes(saniPath + "_data") : null, Path.GetFileNameWithoutExtension(saniPath));
      var preroll = float.TryParse(Environment.GetEnvironmentVariable("SM2_PHYS_PREROLL"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pr) ? pr : 0f;
      var rep = PhysicsBake.Bake(baked, bakeClip, setup, rig, preroll);
      baked.Clip = bakeClip;
      Line($"bake: {rig.Clusters.Count} cloth helper bones, {rig.SkinnedVertices} re-skinned vertices, {rep.JiggleBones} jiggle bones, {rep.Frames} frames in {bakeWatch.Elapsed.TotalSeconds:0.00}s, max cluster fit error {rep.MaxClusterFitError * 100:0.0} cm, max jiggle swing {rep.MaxJiggleDegrees:0.0} deg [{string.Join(" | ", rig.Notes)}]");
      File.WriteAllText(Path.Combine(output, stem + ".physics.json"), PhysicsSidecar.Serialize(baked, setup, rig, new Dictionary<string, PhysicsBakeReport> { [clip.Name] = rep }));
    }
    var diffs = new Dictionary<string, double>();
    var frames = new Dictionary<string, byte[]>();
    var exit = 0;
    var done = false;
    Window window = null!;
    GpuPreviewControl control = null!;

    Func<GlPreview, int, bool> hook = (gpu, fb) =>
    {
      if (done)
        return false;
      Line($"GL: {gpu.Info}");
      mesh.Clip = clip;
      var runs = new List<(string, PreviewSettings, ExportedMesh)> { ("off", settingsOff, mesh), ("on", settingsOn, mesh) };
      if (baked is not null)
        runs.Add(("baked", settingsOff, baked));
      foreach (var (label, settings, target) in runs)
      {
        runtime?.Reset();
        var physMs = new List<double>();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i <= steps; i++)
        {
          var t = i / 60f;
          var probe = MeshPreview.BuildScene(target, 64, 64, new OrbitCamera { Yaw = 0.7f }, false, t, settings);
          if (settings.Physics)
            physMs.Add(probe.PhysicsMs);
          if (i % every != 0)
            continue;
          var follow = pelvis >= 0 && probe.SkinMatrices is { } skin && pelvis < skin.Length ? Vector3.Transform(focus, skin[pelvis]) : focus;
          foreach (var cam in cameras)
          {
            var camera = new OrbitCamera { Yaw = cam.Yaw, Pitch = cam.Pitch, Distance = fit * cam.Distance, Target = follow };
            var scene = MeshPreview.BuildScene(target, Size, Size, camera, false, t, settings);
            gpu.Render(target, scene, Size, Size, false, fb);
            while (gpu.Pending)
              gpu.Render(target, MeshPreview.BuildScene(target, Size, Size, camera, false, t, settings), Size, Size, false, fb);
            var pixels = gpu.ReadPixels();
            var name = $"{cam.Name}_f{i:000}_{label}";
            PreviewBench.Png(Path.Combine(output, name + ".png"), pixels, gpu.Width, gpu.Height);
            if (label != "baked")
              frames[$"{cam.Name}_f{i:000}_{label}"] = pixels;
            if (label != "off" && frames.TryGetValue($"{cam.Name}_f{i:000}_" + (label == "on" ? "off" : "on"), out var before))
            {
              long sum = 0, changed = 0;
              for (var k = 0; k < Math.Min(before.Length, pixels.Length); k += 4)
              {
                var d = Math.Abs(before[k] - pixels[k]) + Math.Abs(before[k + 1] - pixels[k + 1]) + Math.Abs(before[k + 2] - pixels[k + 2]);
                sum += d;
                if (d > 24)
                  changed++;
              }

              diffs[$"{cam.Name}_f{i:000} {(label == "on" ? "on vs off" : "baked vs on")}"] = 100.0 * changed / (pixels.Length / 4);
            }
          }
        }

        var sorted = physMs.Skip(1).OrderBy(x => x).ToList();
        Line($"run {label}: {steps + 1} steps in {watch.Elapsed.TotalSeconds:0.0}s" +
             (sorted.Count > 0 ? $"; physics ms/step mean {sorted.Average():0.000} p50 {sorted[sorted.Count / 2]:0.000} p95 {sorted[(int)(sorted.Count * 0.95)]:0.000} max {sorted[^1]:0.000} (first {physMs[0]:0.0})" : ""));
      }

      foreach (var (key, pct) in diffs)
        Line($"diff {key}: {pct:0.00}% pixels differ");
      mesh.Clip = null;
      done = true;
      Dispatcher.UIThread.Post(() => window.Close());
      return false;
    };

    App.WindowFactory = () =>
    {
      control = new GpuPreviewControl { BenchHook = hook };
      window = new Window { Width = 700, Height = 700, Title = "SM2 physics bench", Content = control, ShowActivated = false };
      DispatcherTimer.RunOnce(() =>
      {
        if (done)
          return;
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
      window.Opened += (_, _) => control.Show(new GpuFrameRequest(mesh, default, false, false, 0, settingsOff));
      return window;
    };
    Program.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    return exit;
  }
}
