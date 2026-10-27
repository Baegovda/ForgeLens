using System.Diagnostics;
using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   GPU preview timing: <c>--glbench &lt;game folder|synthetic&gt; &lt;model&gt; &lt;out folder&gt; [frames]</c>.
///   Opens one window with the GL control, then (1) uncapped: renders N frames back to back per resolution
///   (static orbit and clip playback) and divides by a final glFinish, i.e. vsync off; (2) presented: renders
///   on every compositor frame for 3 s (vsync on) and counts frames; saves PNG frames; writes glbench.txt; exits.
/// </summary>
internal static class GpuBench
{
  public static int Run(string[] args)
  {
    var game = args.Length > 1 ? args[1] : "synthetic";
    var model = args.Length > 2 ? args[2] : "cc_titus.tpl";
    var output = args.Length > 3 ? args[3] : Path.Combine(Environment.CurrentDirectory, "glbench");
    var frames = args.Length > 4 && int.TryParse(args[4], out var f) ? f : 120;
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string text)
    {
      log.AppendLine(text);
      File.WriteAllText(Path.Combine(output, "glbench.txt"), log.ToString());
    }

    ExportedMesh mesh;
    PreviewSettings settings;
    AnimationClip? clip = null;
    Vector3? pauldron = null;
    try
    {
      Line($"glbench {DateTime.Now:yyyy-MM-dd HH:mm:ss} cores={Environment.ProcessorCount} source={game} model={model}");
      if (game == "synthetic")
      {
        mesh = Synthetic(out var albedo, out var normal);
        settings = new PreviewSettings
        {
          Albedo = new Dictionary<string, PreviewImage> { ["albedo"] = albedo },
          Normals = new Dictionary<string, PreviewImage> { ["normal"] = normal },
          InPlace = true,
        };
      }
      else
      {
        var watch = Stopwatch.StartNew();
        var catalog = GameCatalog.Load(game);
        var store = new ArchiveStore(catalog);
        var index = catalog.FindByFileName(model)[0];
        mesh = TemplateMeshReader.Load(catalog, store, index);
        var textures = MainWindow.ResolvePreview(catalog, store, mesh, index);
        Line($"{catalog.GetFile(index).Path}: LOD0 {mesh.Positions.Count:N0} verts, {mesh.Parts.Sum(p => p.IndexCount / 3):N0} tris, bones {mesh.Bones.Count}, albedo {textures.Albedo.Count}, normal {textures.Normals.Count}, spec {textures.Specular.Count}; load {watch.Elapsed.TotalSeconds:0.0}s");
        settings = new PreviewSettings { Albedo = textures.Albedo, Normals = textures.Normals, Specular = textures.Specular, InPlace = true };
        foreach (var part in mesh.Parts.Where(p => p.Visible))
        {
          var diffuse = part.Maps.FirstOrDefault(m => m.Role == "diffuse");
          var alpha = "";
          if (part.Texture is { } tex && textures.Albedo.TryGetValue(tex, out var image))
          {
            long sum = 0, low = 0;
            var n = image.Bgra.Length / 4;
            for (var i = 3; i < image.Bgra.Length; i += 4)
            {
              sum += image.Bgra[i];
              if (image.Bgra[i] < 128)
                low++;
            }

            alpha = $" alpha mean {sum / Math.Max(1, n)} <128 {100.0 * low / Math.Max(1, n):0}%";
          }

          Line($"part {part.Name} tris {part.IndexCount / 3} blend={part.Blend} blendMode={part.BlendMode} variant={part.Variant} tex={part.Texture} diffuse.ch={diffuse?.Channels} spec={part.SpecularChannels} aniso={(part.AnisoSpec is null ? "-" : part.AnisoSpec.DirMap + "/" + part.AnisoSpec.Anisotropy)} second={(part.SecondSpec is null ? "-" : "on")} maps={string.Join(",", part.Maps.Select(m => m.Role + ":" + System.IO.Path.GetFileName(m.Name)))}{alpha}");
        }

        if (Environment.GetEnvironmentVariable("SM2_PARTS_ONLY") == "1")
          return 0;
        if (PreviewBench.PartCenter(mesh, out var c, out _))
          pauldron = c;
        if (mesh.Bones.Count > 0)
        {
          var headers = SaniIndex.ReadHeaders(catalog, store);
          var compatible = SaniIndex.Compatible(headers, mesh.Bones.Select(b => b.Name).ToArray());
          var pick = compatible.FirstOrDefault(h => h.Path.Contains("titus", StringComparison.OrdinalIgnoreCase) && h.Duration > 1);
          if (pick.Path is null && compatible.Count > 0)
            pick = compatible[0];
          if (pick.Path is not null)
          {
            clip = SaniReader.Load(catalog, store, pick.Index);
            Line($"clip {pick.Path} {pick.Duration:0.00}s");
          }
        }
      }
    }
    catch (Exception exception)
    {
      Line(exception.ToString());
      return 1;
    }

    var exit = 0;
    GpuPreviewControl control = null!;
    Window window = null!;
    var fit = MeshPreview.FitDistance(mesh, 1100 / 1000f);
    var phase = 0;
    var presentedStart = 0.0;
    var presentedFrames = 0;
    var clock = Stopwatch.StartNew();
    IDisposable? timeout = null;
    void Shot(GlPreview gpu, int fb, string name, int w, int h, OrbitCamera camera, float time, bool wire, bool skeleton)
    {
      var scene = MeshPreview.BuildScene(mesh, w, h, camera, skeleton, time, settings);
      gpu.Render(mesh, scene, w, h, wire, fb);
      while (gpu.Pending)
      {
        scene = MeshPreview.BuildScene(mesh, w, h, camera, skeleton, time, settings);
        gpu.Render(mesh, scene, w, h, wire, fb);
      }

      PreviewBench.Png(Path.Combine(output, name), gpu.ReadPixels(), gpu.Width, gpu.Height);
    }

    double Uncapped(GlPreview gpu, int fb, int w, int h, bool play)
    {
      mesh.Clip = play ? clip : null;
      for (var i = 0; i < 5; i++)
        gpu.Render(mesh, MeshPreview.BuildScene(mesh, w, h, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, false, i * 0.016f, settings), w, h, false, fb);
      gpu.Finish();
      var sw = Stopwatch.StartNew();
      double cpu = 0;
      for (var i = 0; i < frames; i++)
      {
        var start = sw.Elapsed.TotalMilliseconds;
        var camera = new OrbitCamera { Yaw = 0.7f + (play ? 0 : i * 0.02f), Pitch = 0.18f };
        var scene = MeshPreview.BuildScene(mesh, w, h, camera, false, i * 0.016f, settings);
        gpu.Render(mesh, scene, w, h, false, fb);
        cpu += sw.Elapsed.TotalMilliseconds - start;
      }

      gpu.Finish();
      var ms = sw.Elapsed.TotalMilliseconds / frames;
      Line($"uncapped {(play ? "play  " : "static")} {w}x{h} MSAA {gpu.Samples}x: {ms,6:0.00} ms/frame ({1000 / ms,6:0} fps)  cpu-submit {cpu / frames:0.00} ms");
      mesh.Clip = null;
      return ms;
    }

    Func<GlPreview, int, bool> hook = (gpu, fb) =>
    {
      if (phase == 0)
      {
        Line($"GL: {gpu.Info}");
        var watch = Stopwatch.StartNew();
        Shot(gpu, fb, "gpu_default_1100x1000.png", 1100, 1000, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, 0, false, false);
        Line($"first frame incl. upload {watch.Elapsed.TotalMilliseconds:0} ms");
        if (pauldron is { } target)
          Shot(gpu, fb, "gpu_pauldron_1100x1000.png", 1100, 1000, new OrbitCamera { Yaw = 1.45f, Pitch = 0.12f, Distance = fit * 0.30f, Target = target }, 0, false, false);
        if (HeadCenter(mesh) is { } head)
        {
          // Face close-ups: hide the helmet as the browser part list would.
          var helmet = mesh.Parts.Where(p => p.Visible && p.Name.Contains("helmet", StringComparison.OrdinalIgnoreCase)).ToList();
          foreach (var part in helmet)
            part.Visible = false;
          foreach (var (name, yaw) in new[] { ("front", 0f), ("left", 0.7f), ("right", -0.7f), ("back", 3.14159f) })
          {
            var headCam = new OrbitCamera { Yaw = yaw, Pitch = 0.05f, Distance = fit * 0.12f, Target = head };
            gpu.LegacyOpaque = true;
            Shot(gpu, fb, $"head_{name}_before.png", 1000, 1000, headCam, 0, false, false);
            gpu.LegacyOpaque = false;
            Shot(gpu, fb, $"head_{name}_after.png", 1000, 1000, headCam, 0, false, false);
          }

          foreach (var part in helmet)
            part.Visible = true;
        }

        Shot(gpu, fb, "gpu_wire_skeleton_1100x1000.png", 1100, 1000, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, 0, true, true);
        if (game == "synthetic")
        {
          // Skinning check: every vertex is bound to bone 0; a translated palette must move the sphere.
          var scene = MeshPreview.BuildScene(mesh, 400, 400, new OrbitCamera { Yaw = 0, Pitch = 0, Distance = 6 }, false, 0, settings);
          var moved = new PreviewScene
          {
            Batches = scene.Batches, View = scene.View, Projection = scene.Projection, Eye = scene.Eye, Center = scene.Center,
            KeyLight = scene.KeyLight, FillLight = scene.FillLight,
            SkinMatrices = new[] { Matrix4x4.CreateRotationZ(0.6f) * Matrix4x4.CreateTranslation(1.2f, 0, 0) },
          };
          gpu.Render(mesh, moved, 400, 400, false, fb);
          PreviewBench.Png(Path.Combine(output, "gpu_synthetic_skinned_shift.png"), gpu.ReadPixels(), 400, 400);
          gpu.Render(mesh, scene, 400, 400, false, fb);
          PreviewBench.Png(Path.Combine(output, "gpu_synthetic_bind.png"), gpu.ReadPixels(), 400, 400);
          // glt.chameleon check: test ramp on the sphere (rim t~0 = C0 teal ... facing centre t~1 = C3 magenta).
          mesh.Parts[0].Chameleon = new ChameleonSpec
          {
            Colors = new[] { new[] { 0.1f, 0.8f, 0.7f, 1f }, new[] { 0.2f, 0.5f, 0.9f, 1f }, new[] { 0.6f, 0.3f, 0.9f, 1f }, new[] { 0.9f, 0.2f, 0.6f, 1f } },
            Offset1 = 0.35f, Offset2 = 0.7f, FresnelPower = 1f,
          };
          var chamScene = MeshPreview.BuildScene(mesh, 400, 400, new OrbitCamera { Yaw = 0, Pitch = 0, Distance = 6 }, false, 0, settings);
          gpu.Render(mesh, chamScene, 400, 400, false, fb);
          PreviewBench.Png(Path.Combine(output, "gpu_synthetic_chameleon.png"), gpu.ReadPixels(), 400, 400);
          mesh.Parts[0].Chameleon = null;
          var cpu = MeshPreview.Render(mesh, 400, 400, new OrbitCamera { Yaw = 0, Pitch = 0, Distance = 6 }, false, 0, out _, settings with { Supersample = 2 });
          PreviewBench.Png(Path.Combine(output, "cpu_synthetic_bind.png"), cpu, 400, 400);
        }

        if (clip is not null)
        {
          mesh.Clip = clip;
          Shot(gpu, fb, "gpu_play_inplace_t0.5_1100x1000.png", 1100, 1000, new OrbitCamera { Yaw = 0.7f, Pitch = 0.18f }, 0.5f, false, true);
          mesh.Clip = null;
        }

        Line("");
        Line("-- vsync off (back-to-back frames, glFinish at the end) --");
        foreach (var (w, h) in new[] { (1100, 1000), (1600, 1200), (1920, 1080), (2560, 1440) })
        {
          Uncapped(gpu, fb, w, h, false);
          if (clip is not null || game == "synthetic")
            Uncapped(gpu, fb, w, h, game != "synthetic");
        }

        phase = 1;
        presentedStart = clock.Elapsed.TotalSeconds;
        presentedFrames = 0;
        mesh.Clip = null;
        return true;
      }

      // Presented frames at the window's own size: vsync on, as the app runs.
      var scale = TopLevel.GetTopLevel(control)?.RenderScaling ?? 1.0;
      var cw = Math.Max(1, (int)(control.Bounds.Width * scale));
      var ch = Math.Max(1, (int)(control.Bounds.Height * scale));
      var t = (float)(clock.Elapsed.TotalSeconds - presentedStart);
      var cam = new OrbitCamera { Yaw = 0.7f + (phase == 1 ? t * 0.5f : 0), Pitch = 0.18f };
      gpu.Render(mesh, MeshPreview.BuildScene(mesh, cw, ch, cam, false, t, settings), cw, ch, false, fb);
      presentedFrames++;
      if (t >= 3)
      {
        Line($"vsync on  {(phase == 1 ? "static orbit" : "playback   ")} {cw}x{ch}: {presentedFrames / t:0.0} presented fps");
        if (phase == 1 && clip is not null)
        {
          phase = 2;
          mesh.Clip = clip;
          presentedStart = clock.Elapsed.TotalSeconds;
          presentedFrames = 0;
          return true;
        }

        mesh.Clip = null;
        Line("done");
        timeout?.Dispose();
        Dispatcher.UIThread.Post(() => window.Close());
        return false;
      }

      return true;
    };

    App.WindowFactory = () =>
    {
      control = new GpuPreviewControl { BenchHook = hook };
      // ShowActivated = false: the bench must not take focus from whatever the user is doing.
      window = new Window { Width = 1280, Height = 800, Title = "SM2 GPU bench", Content = control, ShowActivated = false };
      timeout = DispatcherTimer.RunOnce(() =>
      {
        Line(control.HasFailed ? "GL FAILED: " + control.FailReason : $"timeout (init attempted {control.InitAttempted}, frames {control.FramesRendered})");
        exit = 3;
        window.Close();
      }, TimeSpan.FromSeconds(120));
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

  /// <summary>Centre of the eye parts (else the head part), to frame the face.</summary>
  private static Vector3? HeadCenter(ExportedMesh mesh)
  {
    var parts = mesh.Parts.Where(p => p.Visible && (p.Texture ?? "").Contains("_eye", StringComparison.OrdinalIgnoreCase) && !(p.Texture ?? "").Contains("shadow", StringComparison.OrdinalIgnoreCase)).ToList();
    if (parts.Count == 0)
      parts = mesh.Parts.Where(p => p.Visible && p.Name.Contains("head", StringComparison.OrdinalIgnoreCase)).ToList();
    var sum = Vector3.Zero;
    var n = 0;
    foreach (var part in parts)
    {
      for (var i = part.IndexStart; i < part.IndexStart + part.IndexCount && i < mesh.Indices.Count; i++)
      {
        var index = mesh.Indices[i];
        if ((uint)index < (uint)mesh.Positions.Count)
        {
          sum += mesh.Positions[index];
          n++;
        }
      }
    }

    return n == 0 ? null : sum / n;
  }

  private static ExportedMesh Synthetic(out PreviewImage albedo, out PreviewImage normal)
  {
    const int rings = 24, segments = 32;
    var mesh = new ExportedMesh { Name = "sphere" };
    for (var r = 0; r <= rings; r++)
    {
      var phi = MathF.PI * r / rings;
      for (var s = 0; s <= segments; s++)
      {
        var theta = 2 * MathF.PI * s / segments;
        var n = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
        mesh.Positions.Add(n);
        mesh.Normals.Add(n);
        mesh.Uvs.Add(new Vector2(s / (float)segments * 4, r / (float)rings * 2));
        mesh.Skin.Add(new SkinWeights { J0 = 0, W0 = 1 });
      }
    }

    for (var r = 0; r < rings; r++)
    {
      for (var s = 0; s < segments; s++)
      {
        var a = r * (segments + 1) + s;
        var b = a + segments + 1;
        mesh.Indices.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
      }
    }

    mesh.Parts.Add(new MeshPart { Name = "sphere", IndexStart = 0, IndexCount = mesh.Indices.Count, Texture = "albedo", Normal = "normal" });
    const int size = 256;
    var a8 = new byte[size * size * 4];
    var n8 = new byte[size * size * 4];
    for (var y = 0; y < size; y++)
    {
      for (var x = 0; x < size; x++)
      {
        var i = (y * size + x) * 4;
        var check = ((x / 16) + (y / 16)) % 2 == 0;
        a8[i] = (byte)(check ? 60 : 200); a8[i + 1] = (byte)(check ? 90 : 200); a8[i + 2] = (byte)(check ? 200 : 210); a8[i + 3] = 255;
        // Bumps: sine ridges along u.
        var nx = MathF.Sin(x / 8f) * 0.5f;
        var v = Vector3.Normalize(new Vector3(nx, 0, 1));
        n8[i] = (byte)((v.Z * 0.5f + 0.5f) * 255); n8[i + 1] = (byte)((v.Y * 0.5f + 0.5f) * 255); n8[i + 2] = (byte)((v.X * 0.5f + 0.5f) * 255); n8[i + 3] = 255;
      }
    }

    albedo = new PreviewImage { Width = size, Height = size, Bgra = a8 };
    normal = new PreviewImage { Width = size, Height = size, Bgra = n8, Channels = "Normalmap(rgb)" };
    return mesh;
  }
}
