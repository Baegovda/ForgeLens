using System.Diagnostics;
using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   R73 headless cloth penetration bench from the game install (same loader, physics lookup and in-place scene step as
///   the viewer): <c>--clothpen &lt;game&gt; &lt;models,comma&gt; &lt;clip keywords,comma&gt; &lt;out&gt; [clipsPerKeyword]</c>.
///   Per model and clip it plays at 60 Hz physics off (animation-skinned cloth) and on, and per frame measures
///   (a) free cloth particles behind the posed body surface (nearest non-cloth body vertex within 6 cm, depth along its
///   normal &gt; 1 cm) and (b) particles inside the class collision shapes (&gt; 0.5 cm). Writes clothpen.tsv.
/// </summary>
internal static class ClothPenBench
{
  public static int Run(string[] args)
  {
    var game = args[1];
    var models = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
    var keys = args[3].Split(',', StringSplitOptions.RemoveEmptyEntries);
    var output = args[4];
    var perKey = args.Length > 5 ? int.Parse(args[5]) : 1;
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string t) { log.AppendLine(t); Console.WriteLine(t); File.WriteAllText(Path.Combine(output, "clothpen.txt"), log.ToString()); }
    var tsv = new StringBuilder("model\tclip\tdur\trun\tframes\tfree\tbody_pen_mean\tbody_pen_max_count\tbody_pen_depth_max_cm\tbody_pen_p95frame\tcoll_in_mean\tcoll_in_max_count\tcoll_depth_max_cm\tphys_ms_mean\tphys_ms_p95\n");
    var catalog = GameCatalog.Load(game);
    using var store = new ArchiveStore(catalog);
    var headers = SaniIndex.ReadHeaders(catalog, store);
    foreach (var model in models)
    {
      var hits = catalog.FindByFileName(model.EndsWith(".tpl") ? model : model + ".tpl");
      if (hits.Count == 0) { Line($"{model}: not found"); continue; }
      ExportedMesh mesh;
      try
      {
        mesh = TemplateMeshReader.Load(catalog, store, hits[0]);
        var path = catalog.GetFile(hits[0]).Path;
        mesh.Physics = PhysicsLocator.Find(catalog, store, path) ?? new PhysicsSetup();
        if (!mesh.Physics.IsEmpty)
          CdtObjectResolver.Apply(mesh.Physics, mesh.Bones, TplMarkup.Find(catalog, store, hits[0]));
      }
      catch (Exception e) { Line($"{model}: load failed {e.Message}"); continue; }

      var runtime = MeshPreview.PhysicsFor(mesh);
      Line($"{model}: verts {mesh.Positions.Count} bones {mesh.Bones.Count} cloths {runtime?.Cloths.Count ?? 0} colliders [{string.Join(",", runtime?.Cloths.Select(c => c.ColliderCount) ?? Array.Empty<int>())}] free {runtime?.Cloths.Sum(c => c.FreeCount) ?? 0}");
      if (runtime is null || runtime.Cloths.Count == 0) continue;
      foreach (var c in runtime.Cloths)
      {
        var col = c.Shell.Colors;
        string R(Func<Vector4, float> f) => col.Count == 0 ? "-" : $"{col.Min(f):0.###}..{col.Max(f):0.###}";
        var cf = c.Entry.Config;
        Line($"  cloth {c.Name}: n {c.ParticleCount} free {c.FreeCount} backstop {c.BackstopParticles} normals {c.Shell.Normals.Count} sep {cf.Separation.Enable}/{cf.Separation.RadiusScale}/{cf.Separation.OffsetScale} self {cf.SelfCollision.Enable}/{cf.SelfCollision.Distance} thick {cf.CollisionThickness} inertia {cf.LinearInertia}/{cf.AngularInertia}/{cf.CentrifugalInertia} friction {cf.Friction} massScale {cf.CollisionMassScale} solverHz {cf.SolverFrequency} motion {cf.Motion.Enable}/{cf.Motion.Stiffness}/{cf.Motion.DistanceScale} colX {R(v => v.X)} Y {R(v => v.Y)} Z {R(v => v.Z)} W {R(v => v.W)}");
      }
      var body = BodyVertices(mesh);
      var compatible = SaniIndex.Compatible(headers, mesh.Bones.Select(b => b.Name).ToArray());
      var exact = Environment.GetEnvironmentVariable("SM2_PEN_CLIPS")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
      CharacterAnimations.Result? owner = null;
      try { owner = CharacterAnimations.Resolve(catalog, store, catalog.GetFile(hits[0]).Path); } catch { }
      int RankOf(SaniIndex.Header h) => owner is null ? 9 : owner.Rank(h.Path) == ClipRank.Main ? 0 : owner.Named(h.Path) ? 1 : 2;
      var chosen = new List<SaniIndex.Header>();
      foreach (var key in keys)
        chosen.AddRange(compatible.Where(h => Path.GetFileName(h.Path).Contains(key, StringComparison.OrdinalIgnoreCase) && h.Duration > 0.4f && h.Duration < 12f
            && (exact.Length == 0 || exact.Any(x => Path.GetFileName(h.Path).Contains(x, StringComparison.OrdinalIgnoreCase))))
          .Where(h => !CharacterAnimations.IsCinematic(h.Path)).OrderBy(RankOf).ThenBy(h => h.Path.Length).Take(perKey));
      foreach (var h in chosen.DistinctBy(h => h.Index))
      {
        var clip = SaniReader.Load(catalog, store, h.Index);
        Line($"  clip {h.Path} rank {RankOf(h)}");
        mesh.Clip = clip;
        foreach (var on in new[] { false, true })
        {
          runtime = MeshPreview.PhysicsFor(mesh)!;
          runtime.Reset();
          var settings = new PreviewSettings { InPlace = true, Physics = true };
          var loops = int.TryParse(Environment.GetEnvironmentVariable("SM2_PEN_LOOPS"), out var lp) ? Math.Max(1, lp) : 1;
          var steps = (int)(clip.Duration * 60) * loops;
          var pen = new List<int>(); var coll = new List<int>(); var ms = new List<double>();
          float depthMax = 0, collMax = 0;
          var perCloth = new Dictionary<string, int>(); var perFar = new Dictionary<string, int>();
          for (var i = 0; i <= steps; i++)
          {
            var t = loops > 1 ? (i / 60f) % clip.Duration : i / 60f;
            var scene = MeshPreview.BuildScene(mesh, 64, 64, new OrbitCamera(), false, t, settings);
            ms.Add(scene.PhysicsMs);
            if (scene.SkinMatrices is not { } skin || scene.BoneWorlds is not { } worlds) continue;
            var grid = BodyGrid(mesh, body, skin);
            int pc = 0, cc = 0;
            foreach (var cloth in runtime.Cloths)
            {
              var pos = on ? cloth.Positions : cloth.SkinnedPositions;
              var (n, d) = Penetration(grid, pos, cloth, on ? cloth.SkinnedPositions : null);
              if (on)
              {
                ClothInstance.AllIndices = true;
                var gap = cloth.ColliderGap(worlds, pos);
                ClothInstance.AllIndices = false;
                var far = 0;
                for (var q = 0; q < pos.Length; q++)
                  if (!float.IsNaN(gap[q]) && gap[q] > 0.03f && Depth(grid, pos[q]) > 0.01f && Depth(grid, cloth.SkinnedPositions[q]) <= 0.01f) far++;
                perCloth[cloth.Name] = perCloth.GetValueOrDefault(cloth.Name) + n;
                perFar[cloth.Name] = perFar.GetValueOrDefault(cloth.Name) + far;
              }
              pc += n; depthMax = MathF.Max(depthMax, d);
              var (cn, cd) = cloth.ColliderDepth(worlds, pos);
              cc += cn; collMax = MathF.Max(collMax, cd);
            }
            pen.Add(pc); coll.Add(cc);
            if (!on && i == 1 && Environment.GetEnvironmentVariable("SM2_PEN_SHAPES") == "1")
              foreach (var cloth in runtime.Cloths)
                foreach (var r in cloth.ShapeReport(worlds, cloth.SkinnedPositions))
                  Line($"  shape {cloth.Name} {r}");
            if (on && i == 1)
              foreach (var cloth in runtime.Cloths)
              {
                var g = cloth.ColliderGap(worlds, cloth.SkinnedPositions).OrderBy(x => x).ToArray();
                if (g.Length > 0)
                  Line($"  gap {cloth.Name}: skinned particle to nearest capsule surface cm p10 {g[g.Length / 10] * 100:0.0} p50 {g[g.Length / 2] * 100:0.0} p90 {g[g.Length * 9 / 10] * 100:0.0} max {g[^1] * 100:0.0}; >5cm {g.Count(x => x > 0.05f)} of {g.Length}");
              }
          }
          var free = runtime.Cloths.Sum(c => c.FreeCount);
          var sms = ms.Skip(1).OrderBy(x => x).ToList();
          var sp = pen.OrderBy(x => x).ToList();
          var row = $"{model}\t{Path.GetFileName(h.Path)}\t{clip.Duration:0.00}\t{(on ? "on" : "off")}\t{pen.Count}\t{free}\t{pen.Average():0.0}\t{pen.Max()}\t{depthMax * 100:0.0}\t{sp[(int)(sp.Count * 0.95)]}\t{coll.Average():0.0}\t{coll.Max()}\t{collMax * 100:0.0}\t{(on ? sms.Average() : 0):0.000}\t{(on ? sms[(int)(sms.Count * 0.95)] : 0):0.000}";
          tsv.AppendLine(row);
          if (on)
            Line($"  wrap resets {runtime.WrapResets}");
          if (on)
            Line("  per cloth sim-only pen mean (of which >3 cm outside every capsule): " + string.Join(", ", perCloth.Select(kv => $"{kv.Key} {kv.Value / (double)pen.Count:0.0} ({perFar[kv.Key] / (double)pen.Count:0.0})")));
          File.WriteAllText(Path.Combine(output, "clothpen.tsv"), tsv.ToString());
          Line(row);
          if (Environment.GetEnvironmentVariable("SM2_PEN_FRAMES") == "1")
            File.WriteAllText(Path.Combine(output, $"{model}_{Path.GetFileNameWithoutExtension(h.Path)}_{(on ? "on" : "off")}_frames.txt"), string.Join("\n", pen.Select((v, k) => $"{k}\t{v}\t{coll[k]}")));
        }
      }
      mesh.Clip = null;
    }
    return 0;
  }

  /// <summary>Body = mesh vertices farther than 2 cm from every cloth shell bind vertex (the cloth's own render mesh is excluded).</summary>
  private static int[] BodyVertices(ExportedMesh mesh)
  {
    var cloth = new HashSet<(int, int, int)>();
    const float cell = 0.02f;
    (int, int, int) K(Vector3 p) => ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Y / cell), (int)MathF.Floor(p.Z / cell));
    foreach (var s in mesh.ClothSims) foreach (var p in s.Positions) cloth.Add(K(p));
    var list = new List<int>();
    for (var i = 0; i < mesh.Positions.Count && i < mesh.Normals.Count && i < mesh.Skin.Count; i++)
    {
      var (x, y, z) = K(mesh.Positions[i]);
      var near = false;
      for (var a = -1; a <= 1 && !near; a++) for (var b = -1; b <= 1 && !near; b++) for (var c = -1; c <= 1 && !near; c++) near = cloth.Contains((x + a, y + b, z + c));
      if (!near) list.Add(i);
    }
    return list.ToArray();
  }

  private static float Depth(Grid g, Vector3 p)
  {
    var (x, y, z) = Key(p);
    var best = float.MaxValue; Vector3 bp = default, bn = default;
    for (var a = -1; a <= 1; a++) for (var b = -1; b <= 1; b++) for (var c = -1; c <= 1; c++)
      if (g.Cells.TryGetValue((x + a, y + b, z + c), out var l))
        foreach (var (vp, vn) in l) { var d = (vp - p).LengthSquared(); if (d < best) { best = d; bp = vp; bn = vn; } }
    return best > Cell * Cell ? 0 : Vector3.Dot(bp - p, bn);
  }

  private const float Cell = 0.06f;
  private sealed class Grid { public Dictionary<(int, int, int), List<(Vector3 P, Vector3 N)>> Cells = new(); }
  private static (int, int, int) Key(Vector3 p) => ((int)MathF.Floor(p.X / Cell), (int)MathF.Floor(p.Y / Cell), (int)MathF.Floor(p.Z / Cell));

  private static Grid BodyGrid(ExportedMesh mesh, int[] body, Matrix4x4[] skin)
  {
    var g = new Grid();
    foreach (var i in body)
    {
      var w = mesh.Skin[i];
      var p = mesh.Positions[i]; var n = mesh.Normals[i];
      Vector3 sp = default, sn = default; var total = 0f;
      void Add(ushort j, float wt) { if (wt <= 0 || j >= skin.Length) return; sp += Vector3.Transform(p, skin[j]) * wt; sn += Vector3.TransformNormal(n, skin[j]) * wt; total += wt; }
      Add(w.J0, w.W0); Add(w.J1, w.W1); Add(w.J2, w.W2); Add(w.J3, w.W3);
      if (total <= 1e-6f) continue;
      sp /= total;
      if (sn.LengthSquared() < 1e-12f) continue;
      var k = Key(sp);
      if (!g.Cells.TryGetValue(k, out var l)) g.Cells[k] = l = new();
      l.Add((sp, Vector3.Normalize(sn)));
    }
    return g;
  }

  /// <summary>With <paramref name="reference"/> (the animation-skinned particles) only penetrations the sim added are counted.</summary>
  private static (int, float) Penetration(Grid g, Vector3[] pos, ClothInstance cloth, Vector3[]? reference)
  {
    var count = 0; var max = 0f;
    var bind = cloth.Shell.Colors;
    for (var i = 0; i < pos.Length; i++)
    {
      if (i < bind.Count && bind[i].X <= 1e-4f) continue; // pinned
      var p = pos[i];
      var (x, y, z) = Key(p);
      var best = float.MaxValue; Vector3 bp = default, bn = default;
      for (var a = -1; a <= 1; a++) for (var b = -1; b <= 1; b++) for (var c = -1; c <= 1; c++)
        if (g.Cells.TryGetValue((x + a, y + b, z + c), out var l))
          foreach (var (vp, vn) in l) { var d = (vp - p).LengthSquared(); if (d < best) { best = d; bp = vp; bn = vn; } }
      if (best > Cell * Cell) continue;
      var depth = Vector3.Dot(bp - p, bn);
      if (depth > 0.01f && (reference is null || Depth(g, reference[i]) <= 0.01f)) { count++; max = MathF.Max(max, depth); }
    }
    return (count, max);
  }
}
