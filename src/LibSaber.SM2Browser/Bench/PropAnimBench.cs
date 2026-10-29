using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   propanim QA, headless: <c>--propbench &lt;game folder | loose&gt; &lt;model.tpl | loose tpl dir&gt; &lt;out&gt; &lt;spec;spec...&gt; [frames] [zoom]</c>.
///   A spec is an embedded sequence name (played once over its duration), <c>all</c>, or <c>auto:&lt;character clip.sani&gt;</c>
///   (game folder only: the Auto plan for that clip, on the prop's own skeleton). Writes one frame strip PNG per spec
///   (<c>strip_&lt;spec&gt;.png</c>, frames left to right, camera on the objects the clip moves) and <c>propbench.txt</c>.
/// </summary>
internal static class PropAnimBench
{
  public static int Run(string[] args)
  {
    if (args.Length < 5)
    {
      Console.Error.WriteLine("--propbench <game-folder|loose> <model.tpl|tplDir> <out> <spec;spec...> [frames] [zoom]");
      return 2;
    }

    var output = args[3];
    Directory.CreateDirectory(output);
    var frames = args.Length > 5 && int.TryParse(args[5], out var f) ? Math.Clamp(f, 2, 24) : 8;
    var zoom = args.Length > 6 && float.TryParse(args[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z) ? z : 0.45f;
    var log = new StringBuilder();
    void Line(string text)
    {
      log.AppendLine(text);
      File.WriteAllText(Path.Combine(output, "propbench.txt"), log.ToString());
    }

    try
    {
      ExportedMesh mesh;
      GameCatalog? catalog = null;
      ArchiveStore? store = null;
      PreviewSettings settings = new() { MaxEdge = 2048, Supersample = 2 };
      if (args[1] == "loose")
      {
        var dir = args[2].TrimEnd('/', '\\');
        var stem = Path.GetFileName(dir).Replace(".tpl", "");
        mesh = TemplateMeshReader.LoadRaw(File.ReadAllBytes(Path.Combine(dir, stem + ".tpl")),
          File.Exists(Path.Combine(dir, stem + ".tpl_data")) ? File.ReadAllBytes(Path.Combine(dir, stem + ".tpl_data")) : null, stem);
      }
      else
      {
        catalog = GameCatalog.Load(args[1]);
        store = new ArchiveStore(catalog);
        var hits = catalog.FindByFileName(args[2]);
        if (hits.Count == 0)
        {
          Line("model not found: " + args[2]);
          return 2;
        }

        mesh = TemplateMeshReader.Load(catalog, store, hits[0]);
        var tex = MainWindow.ResolvePreview(catalog, store, mesh, hits[0]);
        settings = settings with { Albedo = tex.Albedo, Normals = tex.Normals, Specular = tex.Specular };
      }

      if (mesh.TplAnim is not { } anim)
      {
        Line("no embedded track");
        return 3;
      }

      Line($"{mesh.Name}: {anim.Sequences.Count} sequence(s), {anim.Objects.Count} animated object(s), bones {mesh.Bones.Count}");
      foreach (var s in anim.Sequences)
        Line($"  {TplAnimation.Describe(s)}  {PropAnimStates.Label(s.Name)}  moves {anim.Objects.Count(o => anim.Moves(o, s))}/{anim.Objects.Count}");
      foreach (var n in anim.Notes) Line("  note: " + n);

      var specs = args[4].Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
      if (specs.Count == 1 && specs[0] == "all") specs = anim.Sequences.Select(s => s.Name).ToList();
      AnimEventVfxMap? map = null;
      foreach (var spec in specs)
      {
        AnimationClip clip;
        if (spec.StartsWith("auto:", StringComparison.OrdinalIgnoreCase))
        {
          if (catalog is null || store is null)
          {
            Line(spec + ": auto needs a game folder");
            continue;
          }

          var hits = catalog.FindByFileName(spec[5..]);
          if (hits.Count == 0)
          {
            Line(spec + ": clip not found");
            continue;
          }

          var character = SaniReader.Load(catalog, store, hits[0]);
          map ??= LoadMap(catalog, store);
          var triggers = map.Anims.GroupBy(a => a.Source, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.All(a => anim.Find(a.Anim) is not null) && g.Any(a => !a.Stop)).OrderByDescending(g => g.Count()).Take(1).SelectMany(g => g).ToList();
          var plan = PropAnimStates.AutoPlays(anim, character, triggers);
          clip = anim.Compose(plan.Plays, character.Duration, mesh.Bones, 0, spec);
          Line($"{spec}: character {character.Name} {character.Duration:0.###}s, tracks [{string.Join(", ", character.EventTracks.Select(t => $"{t.Name}:{t.Events.Count}"))}]");
          foreach (var p in plan.Plays) Line($"  {p.Start:0.000}s {p.Seq.Name}{(p.Loop ? " loop" : "")}: {p.Why}");
          foreach (var n in plan.Notes) Line("  " + n);
        }
        else if (anim.Find(spec) is { } seq)
          clip = anim.ToClip(seq, mesh.Bones);
        else
        {
          Line(spec + ": no such sequence");
          continue;
        }

        Strip(mesh, clip, Path.Combine(output, $"strip_{Safe(spec)}.png"), frames, zoom, settings, Line);
      }

      store?.Dispose();
      Line("done");
      return 0;
    }
    catch (Exception ex)
    {
      Line(ex.ToString());
      return 1;
    }
  }

  private static AnimEventVfxMap LoadMap(GameCatalog catalog, ArchiveStore store)
  {
    var map = new AnimEventVfxMap();
    for (var i = 0; i < catalog.FileCount; i++)
    {
      var path = catalog.GetFile(i).Path;
      if (!path.EndsWith(".sso", StringComparison.OrdinalIgnoreCase)) continue;
      byte[] bytes;
      try { bytes = store.Read(i); }
      catch { continue; }
      if (!SslText.LooksText(bytes)) continue;
      var text = Encoding.UTF8.GetString(bytes);
      if (text.Contains("BlueprintAnimationLibraryStartanim", StringComparison.Ordinal)) map.AddBlueprint(Path.GetFileNameWithoutExtension(path), text);
    }

    return map;
  }

  private static string Safe(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_'));

  /// <summary>Frames evenly over the clip, left to right, camera centred on the bones the clip moves most.</summary>
  private static void Strip(ExportedMesh mesh, AnimationClip clip, string path, int frames, float zoom, PreviewSettings settings, Action<string> line)
  {
    const int w = 420, h = 360;
    mesh.Clip = clip;
    try
    {
      var times = Enumerable.Range(0, frames).Select(i => clip.Duration * i / (frames - 1)).ToArray();
      var poses = times.Select(t => MeshPreview.BoneWorlds(mesh, t)).ToArray();
      var moved = new float[mesh.Bones.Count];
      for (var b = 0; b < moved.Length; b++)
        foreach (var p in poses)
        {
          moved[b] = Math.Max(moved[b], Vector3.Distance(p[b].Translation, poses[0][b].Translation));
          var r0 = Quaternion.CreateFromRotationMatrix(Normalize(poses[0][b]));
          var r1 = Quaternion.CreateFromRotationMatrix(Normalize(p[b]));
          moved[b] = Math.Max(moved[b], (1 - Math.Abs(Quaternion.Dot(r0, r1))) * 10f);
        }

      var top = Enumerable.Range(0, moved.Length).Where(b => moved[b] > 1e-4f).OrderByDescending(b => moved[b]).Take(8).ToList();
      Vector3? target = top.Count == 0 ? null : top.Select(b => poses[0][b].Translation).Aggregate(Vector3.Zero, (a, v) => a + v) / top.Count;
      var fit = MeshPreview.FitDistance(mesh, w / (float)h);
      var camera = new OrbitCamera { Yaw = 0.9f, Pitch = 0.25f, Distance = target is null ? 0 : fit * zoom, Target = target };
      line($"  {Path.GetFileName(path)}: {clip.Name} {clip.Duration:0.###}s, {clip.Tracks.Count} track(s); moving bones {string.Join(", ", top.Select(b => $"{mesh.Bones[b].Name} {moved[b]:0.###}"))}");
      var strip = new byte[w * frames * h * 4];
      for (var i = 0; i < frames; i++)
      {
        var img = MeshPreview.Render(mesh, w, h, camera, false, times[i], out _, settings);
        for (var y = 0; y < h; y++)
          Buffer.BlockCopy(img, y * w * 4, strip, (y * w * frames + i * w) * 4, w * 4);
      }

      PreviewBench.Png(path, strip, w * frames, h);
    }
    finally
    {
      mesh.Clip = null;
    }
  }

  private static Matrix4x4 Normalize(Matrix4x4 m)
  {
    Matrix4x4.Decompose(m, out _, out var r, out _);
    return Matrix4x4.CreateFromQuaternion(r);
  }
}
