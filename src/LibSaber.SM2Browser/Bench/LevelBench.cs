// R54: place level templates (placement.tsv from tools/level/placement.py) as real meshes.
// ForgeLens --levelbench <game> <placement.tsv> <out> [maxObjects] [kinds=geom]
// Each row's templates (';' = multi-part bundle, all parts share the bundle matrix) are loaded once
// (TemplateMeshReader LOD0, parts already in model space), transformed by the row's 4x4 world matrix
// (row-major, translation m[12..14], System.Numerics row-vector convention, game metres, Y-up) and merged.
// Output: level_top.png, level_persp.png (MeshPreview CPU render), level.obj (static merged mesh for UE), levelbench.txt.
// New mode only: nothing else changes.
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Bench;

internal static class LevelBench
{
  public static int Run(string[] args)
  {
    var game = args[1]; var tsv = args[2]; var output = args[3];
    var max = args.Length > 4 && int.TryParse(args[4], out var m) ? m : int.MaxValue;
    var kinds = (args.Length > 5 ? args[5] : "geom").Split(',');
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string t) { log.AppendLine(t); File.WriteAllText(Path.Combine(output, "levelbench.txt"), log.ToString()); }
    var sw = Stopwatch.StartNew();
    var catalog = GameCatalog.Load(game);
    using var store = new ArchiveStore(catalog);
    Line($"catalog {catalog.FileCount:N0} files {sw.Elapsed.TotalSeconds:0.0}s");
    var cache = new Dictionary<string, ExportedMesh?>();
    var merged = new ExportedMesh { Name = "level" };
    int rows = 0, placed = 0, missing = 0, failed = 0;
    var missingNames = new SortedSet<string>();
    foreach (var line in File.ReadLines(tsv).Skip(1))
    {
      var c = line.Split('\t');
      if (c.Length < 10 || !kinds.Contains(c[3])) continue;
      if (++rows > max) break;
      var f = c[9].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
      if (f.Length != 16) continue;
      var world = ParseMatrix(f);
      foreach (var tpl in c[4].Split(';', StringSplitOptions.RemoveEmptyEntries))
      {
        if (!cache.TryGetValue(tpl, out var mesh))
        {
          mesh = null;
          var hits = catalog.FindByFileName(tpl + ".tpl");
          if (hits.Count == 0) { missing++; missingNames.Add(tpl); }
          else
          {
            try { mesh = TemplateMeshReader.Load(catalog, store, hits[0]); if (!mesh.HasTriangles) mesh = null; }
            catch (Exception e) { failed++; Line($"fail {tpl}: {e.GetType().Name} {e.Message}"); }
          }
          cache[tpl] = mesh;
        }
        if (mesh is null) continue;
        Append(merged, mesh, world, tpl);
        placed++;
      }
    }
    Line($"rows {rows} placed {placed} templates {cache.Count} loaded {cache.Values.Count(v => v != null)} missing {missing} failed {failed} verts {merged.Positions.Count:N0} tris {merged.Indices.Count / 3:N0} build {sw.Elapsed.TotalSeconds:0.0}s");
    Line("missing: " + string.Join(" ", missingNames.Take(40)));
    WriteObj(merged, Path.Combine(output, "level.obj"));
    Line($"obj written {sw.Elapsed.TotalSeconds:0.0}s");
    var w = 1600; var h = 1000;
    foreach (var (name, cam) in new[] { ("top", new OrbitCamera { Yaw = 0f, Pitch = 1.55f }), ("persp", new OrbitCamera { Yaw = 0.7f, Pitch = 0.45f }) })
    {
      var t = Stopwatch.StartNew();
      var img = MeshPreview.Render(merged, w, h, cam, false, 0, out var note, new PreviewSettings { MaxEdge = 2048 });
      PreviewBench.Png(Path.Combine(output, $"level_{name}.png"), img, w, h);
      Line($"render {name} {t.Elapsed.TotalMilliseconds:0} ms {note}");
    }
    Line($"total {sw.Elapsed.TotalSeconds:0.0}s");
    return 0;
  }

  /// <summary>16 floats as stored (row-major, translation in [12..14]).</summary>
  public static Matrix4x4 ParseMatrix(float[] f) => new(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]);

  public static void Append(ExportedMesh dst, ExportedMesh src, Matrix4x4 world, string name)
  {
    var baseV = dst.Positions.Count; var baseI = dst.Indices.Count;
    foreach (var p in src.Positions) dst.Positions.Add(Vector3.Transform(p, world));
    for (var i = 0; i < src.Positions.Count; i++)
      dst.Normals.Add(i < src.Normals.Count ? Vector3.Normalize(Vector3.TransformNormal(src.Normals[i], world)) : Vector3.UnitY);
    for (var i = 0; i < src.Positions.Count; i++) dst.Uvs.Add(i < src.Uvs.Count ? src.Uvs[i] : Vector2.Zero);
    var flip = world.GetDeterminant() < 0; // mirrored instance: keep front faces
    for (var i = 0; i + 2 < src.Indices.Count; i += 3)
    {
      dst.Indices.Add(baseV + src.Indices[i]);
      dst.Indices.Add(baseV + src.Indices[i + (flip ? 2 : 1)]);
      dst.Indices.Add(baseV + src.Indices[i + (flip ? 1 : 2)]);
    }
    dst.Parts.Add(new MeshPart { Name = name, IndexStart = baseI, IndexCount = dst.Indices.Count - baseI });
  }

  static void WriteObj(ExportedMesh m, string path)
  {
    using var w = new StreamWriter(path);
    var ci = CultureInfo.InvariantCulture;
    foreach (var p in m.Positions) w.WriteLine(string.Format(ci, "v {0:0.####} {1:0.####} {2:0.####}", p.X, p.Y, p.Z));
    foreach (var n in m.Normals) w.WriteLine(string.Format(ci, "vn {0:0.###} {1:0.###} {2:0.###}", n.X, n.Y, n.Z));
    foreach (var part in m.Parts)
    {
      w.WriteLine("g " + part.Name);
      for (var i = part.IndexStart; i < part.IndexStart + part.IndexCount; i += 3)
      {
        int a = m.Indices[i] + 1, b = m.Indices[i + 1] + 1, c = m.Indices[i + 2] + 1;
        w.WriteLine($"f {a}//{a} {b}//{b} {c}//{c}");
      }
    }
  }
}
