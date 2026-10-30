// R54: ForgeLens --levelbench <game> <level> <out> [ue 0|1]
// <level> = level name (e.g. hub_battle_barge), a cd_list file name, or "list" (prints every *.cd_list in the game).
// Loads the level from the user's own game files through Level/LevelScene (same path as the viewer), renders
// top/persp/close with textures + level point lights, and with ue=1 writes templates/*.obj (UE axes) + instances_ue.tsv
// for tools/ue/ue_r54_level.py. Outputs are local test files (keep them under Exports; never commit or ship).
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2Browser.Level;

namespace LibSaber.SM2Browser.Bench;

internal static class LevelBench
{
  public static int Run(string[] args)
  {
    var game = args[1]; var level = args[2]; var output = args[3];
    var ue = args.Length > 4 && args[4] == "1";
    Directory.CreateDirectory(output);
    var log = new StringBuilder();
    void Line(string t) { log.AppendLine(t); File.WriteAllText(Path.Combine(output, "levelbench.txt"), log.ToString()); }
    float Env(string k, float d) => float.TryParse(Environment.GetEnvironmentVariable(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;
    var sw = Stopwatch.StartNew();
    var catalog = GameCatalog.Load(game);
    using var store = new ArchiveStore(catalog);
    Line($"catalog {catalog.FileCount:N0} files {sw.Elapsed.TotalSeconds:0.0}s");
    if (level == "list")
    {
      for (var i = 0; i < catalog.FileCount; i++)
        if (LevelScene.IsLevelFile(catalog.GetFile(i).Path)) Line($"{catalog.GetFile(i).Path}\t{catalog.GetFile(i).Size}");
      return 0;
    }
    var name = level.EndsWith(".cd_list", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(level) : level + ".cd_list";
    var hits = catalog.FindByFileName(name);
    if (hits.Count == 0) { Line("level not found: " + name); return 2; }
    var scene = LevelScene.Load(catalog, store, hits[0], new LevelScene.Options(LightGain: Env("SM2_LEVEL_LIGHT_GAIN", 1f), UseCache: Environment.GetEnvironmentVariable("SM2_LEVEL_NOCACHE") != "1"), Line);
    Line("missing: " + string.Join(" ", scene.Missing.Take(30)));
    Line("skipped: " + string.Join(" ", scene.Skipped.Take(30)));
    Line("untextured (largest): " + string.Join(" ", scene.Untextured));
    Line($"open {sw.Elapsed.TotalSeconds:0.0}s from_disk_cache={scene.FromDiskCache} coverage instances {scene.Placed.Count}/{scene.WorkCount} lwi {scene.LwiPlaced}/{scene.LwiCount} tris {scene.Triangles:N0} hidden_parts_dropped {scene.HiddenParts}");
    Line($"cameras persp yaw {scene.Persp.Yaw:0.00} d {scene.Persp.Distance:0} close yaw {scene.Close.Yaw:0.00} d {scene.Close.Distance:0}; timing {scene.Timing}");
    Line($"camera centre {scene.Centre} spread(10-90%) {scene.Spread:0.0} m");
    var w = 1600; var h = 1000;
    var settings = new PreviewSettings
    {
      MaxEdge = 2048, Albedo = scene.Albedo, Normals = scene.Normals, Specular = scene.Specular, PointLights = scene.Lights,
      Ambient = Env("SM2_LEVEL_AMBIENT", 0.22f), HeadLight = Env("SM2_LEVEL_HEAD", 0.6f), Exposure = Env("SM2_LEVEL_EXPOSURE", 1.3f),
    };
    foreach (var (view, cam) in Views(scene))
    {
      var t = Stopwatch.StartNew();
      var img = MeshPreview.Render(scene.Mesh, w, h, cam, false, 0, out var note, settings);
      PreviewBench.Png(Path.Combine(output, $"level_{view}.png"), img, w, h);
      Line($"render {view} {t.Elapsed.TotalMilliseconds:0} ms {note}");
    }
    if (ue) WriteUe(scene, output, Line);
    Line($"total {sw.Elapsed.TotalSeconds:0.0}s");
    return 0;
  }

  public static (string, OrbitCamera)[] Views(LevelScene s) => new[] { ("top", s.Top), ("persp", s.Persp), ("close", s.Close) };

  static void WriteUe(LevelScene s, string output, Action<string> line)
  {
    var dir = Path.Combine(output, "templates"); Directory.CreateDirectory(dir);
    using var inst = new StreamWriter(Path.Combine(output, "instances_ue.tsv"));
    inst.WriteLine("template\tidx\tue_matrix16 (row-major, cm, UE Z-up = game (x,z,y)*100)");
    var written = new HashSet<string>();
    foreach (var (tpl, idx, world) in s.Placed)
    {
      if (written.Add(tpl)) WriteUeObj(s, tpl, Path.Combine(dir, tpl + ".obj"));
      inst.WriteLine($"{tpl}\t{idx}\t{string.Join(' ', Flat(ToUe(world)).Select(v => v.ToString("0.######", CultureInfo.InvariantCulture)))}");
    }
    line($"ue: {written.Count} template obj, {s.Placed.Count} instances");
  }

  static readonly Matrix4x4 Swap = new(1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);

  /// <summary>Game (metres, Y-up) world matrix -> UE (cm, Z-up) via the repo convention (x,z,y)*100 (ue_vfx_frame.py).</summary>
  public static Matrix4x4 ToUe(Matrix4x4 m) { var r = Swap * m * Swap; r.M41 *= 100; r.M42 *= 100; r.M43 *= 100; return r; }

  public static float[] Flat(Matrix4x4 m) => new[] { m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44 };

  /// <summary>Re-reads the template's model-space geometry from the merged mesh is not possible, so it reloads via the parts:
  /// the first placed copy is un-transformed with the inverse of its world matrix.</summary>
  static void WriteUeObj(LevelScene s, string tpl, string path)
  {
    var first = s.Placed.First(p => p.Template == tpl);
    Matrix4x4.Invert(first.World, out var inv);
    var parts = s.Mesh.Parts.Where(p => p.Name.StartsWith(tpl + "/", StringComparison.Ordinal) || p.Name == tpl).ToList();
    // the first copy's parts are the first parts with this prefix (Append adds them contiguously)
    var firstParts = new List<MeshPart>(); var seen = new HashSet<string>();
    foreach (var p in parts) { if (!seen.Add(p.Name)) break; firstParts.Add(p); }
    using var w = new StreamWriter(path); var ci = CultureInfo.InvariantCulture;
    var map = new Dictionary<int, int>(); var n = 0; var faces = new StringBuilder();
    foreach (var part in firstParts)
      for (var i = part.IndexStart; i < part.IndexStart + part.IndexCount; i += 3)
      {
        var f = new int[3];
        for (var k = 0; k < 3; k++)
        {
          var vi = s.Mesh.Indices[i + k];
          if (!map.TryGetValue(vi, out var o))
          {
            o = map[vi] = ++n;
            var p = Vector3.Transform(s.Mesh.Positions[vi], inv); var nn = Vector3.Normalize(Vector3.TransformNormal(s.Mesh.Normals[vi], inv)); var uv = s.Mesh.Uvs[vi];
            // UE OBJ import maps file (x,y,z) -> UE (x,-y,z) (measured R54); wanted UE = (x,z,y)*100 -> file (x,-z,y)*100.
            w.WriteLine(string.Format(ci, "v {0:0.###} {1:0.###} {2:0.###}", p.X * 100, -p.Z * 100, p.Y * 100));
            w.WriteLine(string.Format(ci, "vn {0:0.###} {1:0.###} {2:0.###}", nn.X, -nn.Z, nn.Y));
            w.WriteLine(string.Format(ci, "vt {0:0.####} {1:0.####}", uv.X, 1 - uv.Y));
          }
          f[k] = o;
        }
        if (first.World.GetDeterminant() < 0) (f[1], f[2]) = (f[2], f[1]);
        faces.AppendLine($"f {f[0]}/{f[0]}/{f[0]} {f[1]}/{f[1]}/{f[1]} {f[2]}/{f[2]}/{f[2]}");
      }
    w.Write(faces);
  }
}
