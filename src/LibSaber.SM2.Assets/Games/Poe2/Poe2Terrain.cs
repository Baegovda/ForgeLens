using System.Numerics;
using System.Text;
using System.Text.Json;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Games.Poe2;

/// <summary>
///   PoE2 M3: terrain tiles (.tgm) and a room layout (.arm) assembled into one static <see cref="ExportedMesh"/>.
///   .tgm layout (PROVEN on all 24 cosmic tiles: every index &lt; vertex count, shape ranges sum to the index count):
///   u8 version (4) | 6 x f32 bbox (minX maxX minY maxY minZ maxZ) | u8 shapeCount | u8 | u32 vertexCount | u32 triangleCount |
///   12 bytes (unknown) | shapeCount x { u16 id, 6 x f32 bbox, u32 indexStart, u32 indexCount } |
///   vertexCount x 24 B { f32 x y z, u8x4 normal (biased by 128), u8x4 tangent, f16 u, f16 v } | u16 indices (u32 when > 65535 verts) |
///   edge tiles end with 142 unknown bytes (not used).
///   Placement (INFERRED from the space hideout, no in-game check): one .arm grid cell = one tile = 250 units (tgm bbox);
///   doodad x/y are in 23 sub-units per tile (waypoint at 219,219 = centre of 19x19); K cells take the .tst tile, S cells the
///   .gft "default" fill, and an outer ring of .tsi OuterBufferSize cells the .gft section named by OuterGroundType's .gt name.
///   Tile rotation is not applied (the space tiles are flat squares that all use the same mesh).
/// </summary>
public sealed partial class Poe2Reader
{
  public const float TileUnits = 250f, SubUnits = 23f;

  public sealed class Tgm
  {
    public int Version;
    public float[] Bbox = new float[6];
    public List<(int Id, int Start, int Count)> Shapes = new();
    public List<Vector3> Pos = new();
    public List<Vector3> Nrm = new();
    public List<Vector2> Uv = new();
    public List<int> Idx = new();
  }

  public static Tgm ReadTgm(byte[] b)
  {
    var t = new Tgm { Version = b[0] };
    for (var i = 0; i < 6; i++) t.Bbox[i] = BitConverter.ToSingle(b, 1 + i * 4);
    int n = b[25];
    var nv = BitConverter.ToInt32(b, 27); var nt = BitConverter.ToInt32(b, 31);
    var o = 47;
    for (var i = 0; i < n; i++) { t.Shapes.Add((BitConverter.ToUInt16(b, o), BitConverter.ToInt32(b, o + 26), BitConverter.ToInt32(b, o + 30))); o += 34; }
    for (var i = 0; i < nv; i++)
    {
      t.Pos.Add(new Vector3(BitConverter.ToSingle(b, o), BitConverter.ToSingle(b, o + 4), BitConverter.ToSingle(b, o + 8)));
      t.Nrm.Add(new Vector3((b[o + 12] - 128) / 127f, (b[o + 13] - 128) / 127f, (b[o + 14] - 128) / 127f));
      t.Uv.Add(new Vector2((float)BitConverter.ToHalf(b, o + 20), (float)BitConverter.ToHalf(b, o + 22)));
      o += 24;
    }
    var wide = nv > 65535;
    for (var i = 0; i < nt * 3; i++) { t.Idx.Add(wide ? BitConverter.ToInt32(b, o) : BitConverter.ToUInt16(b, o)); o += wide ? 4 : 2; }
    if (t.Idx.Count > 0 && t.Idx.Max() >= nv) throw new InvalidDataException($"tgm index {t.Idx.Max()} >= {nv} vertices");
    return t;
  }

  /// <summary>First UTF-16 string of a .tdt = its .tgt (PROVEN on the space and cosmic tdt files).</summary>
  public static string TdtTile(byte[] b)
  {
    var len = BitConverter.ToInt32(b, 4);
    var s = Encoding.Unicode.GetString(b, 8, Math.Min(len * 2, b.Length - 8));
    return s.Split('\0')[0];
  }

  private sealed class TileDef
  {
    public Tgm Mesh = null!;
    public List<string?> ShapeMats = new();
    public bool Projective;
  }

  private readonly Dictionary<string, TileDef?> _tiles = new(StringComparer.OrdinalIgnoreCase);

  private TileDef? Tile(string tdt)
  {
    if (_tiles.TryGetValue(tdt, out var have)) return have;
    TileDef? def = null;
    try
    {
      var tgt = TdtTile(File.ReadAllBytes(RawPath(tdt)));
      using var doc = Json(tgt);
      var v = doc.RootElement.GetProperty("section").EnumerateObject().First().Value;
      var tgm = v.GetProperty("tile_mesh").GetString()!;
      def = new TileDef { Mesh = ReadTgm(File.ReadAllBytes(RawPath(tgm))) };
      foreach (var m in v.GetProperty("normal_materials").EnumerateArray())
      {
        var k = m.TryGetProperty("uint", out var u) && u.ValueKind == JsonValueKind.Number ? Math.Max(1, u.GetInt32()) : 1;
        def.ShapeMats.AddRange(Enumerable.Repeat(m.GetProperty("mat_file").GetString(), k));
      }
      foreach (var mf in def.ShapeMats.Distinct())
        if (mf != null && File.Exists(Path.Combine(JsonRoot, mf.ToLowerInvariant().Replace('/', Path.DirectorySeparatorChar) + ".json"))
            && File.ReadAllText(Path.Combine(JsonRoot, mf.ToLowerInvariant().Replace('/', Path.DirectorySeparatorChar) + ".json")).Contains("ProjectiveUV", StringComparison.Ordinal))
          def.Projective = true;
      Log.Add($"tile {tdt} -> {tgt} -> {tgm}: {def.Mesh.Pos.Count} verts, {def.Mesh.Idx.Count / 3} tris, {def.Mesh.Shapes.Count} shapes, mats {string.Join(",", def.ShapeMats)}");
    }
    catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidDataException or InvalidOperationException)
    {
      Log.Add($"tile {tdt}: {e.Message}");
    }
    _tiles[tdt] = def;
    return def;
  }

  /// <summary>Every game file the level loader needs besides the .arm (tsi/tst/gft next to it). Used by Poe2Source.PrepareLevel.</summary>
  public static string SetDir(string arm) => Path.GetDirectoryName(Path.GetDirectoryName(arm.Replace('\\', '/'))!)!.Replace('\\', '/');

  /// <summary>Build the room in <paramref name="arm"/> (prepared folder must hold the arm/tsi/tst/gft JSON, the .tdt/.tgm raw files and the .tgt JSON).</summary>
  public ExportedMesh LoadLevel(string arm, bool doodads = true)
  {
    arm = arm.ToLowerInvariant();
    var set = SetDir(arm);
    using var armDoc = Json(arm);
    var root = armDoc.RootElement;
    var grid = root.GetProperty("grid").EnumerateArray().Select(r => r.EnumerateArray().ToList()).ToList();
    int h = grid.Count, w = grid.Max(r => r.Count);
    string? tst = null, fill = null, outer = null; var buffer = 0;
    try { using var d = Json(set + "/tiles.tst"); tst = d.RootElement.GetProperty("tdt_files").EnumerateArray().First().GetProperty("tdt_file").GetString(); } catch (Exception e) { Log.Add("tst: " + e.Message); }
    string? outerGround = null;
    try
    {
      using var d = Json(set + "/master.tsi");
      buffer = int.TryParse(d.RootElement.GetProperty("OuterBufferSize").GetString(), out var bs) ? bs : 0;
      outerGround = d.RootElement.GetProperty("OuterGroundType").GetString();
    }
    catch (Exception e) { Log.Add("tsi: " + e.Message); }
    string? outerName = null;
    if (outerGround != null) try { using var d = Json(outerGround); outerName = d.RootElement.GetProperty("name").GetString(); } catch (Exception e) { Log.Add("gt: " + e.Message); }
    try
    {
      using var d = Json(set + "/random_fill_tiles.gft");
      foreach (var s in d.RootElement.GetProperty("sections").EnumerateArray())
      {
        var name = s.GetProperty("name").GetString();
        var f = s.GetProperty("files").EnumerateArray().First().GetProperty("path").GetString();
        if (name == "default") fill = f;
        if (name == outerName) outer = f;
      }
    }
    catch (Exception e) { Log.Add("gft: " + e.Message); }
    Log.Add($"arm {arm}: grid {w}x{h}, tst {tst}, fill {fill}, outer {outer} x{buffer}");

    var mesh = new ExportedMesh { Name = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(Path.GetDirectoryName(arm))!) + "_" + Path.GetFileNameWithoutExtension(arm) };
    var byMat = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
    var placed = 0;
    void Place(string? tdt, int cx, int cy)
    {
      if (tdt == null || Tile(tdt.ToLowerInvariant()) is not { } t) return;
      var b = mesh.Positions.Count;
      foreach (var (p, i) in t.Mesh.Pos.Select((p, i) => (p, i)))
      {
        float wx = p.X + cx * TileUnits, wy = p.Y + cy * TileUnits;
        mesh.Positions.Add(P(wx, wy, p.Z));
        mesh.Normals.Add(Vector3.Normalize(D(t.Mesh.Nrm[i].X, t.Mesh.Nrm[i].Y, t.Mesh.Nrm[i].Z) + new Vector3(1e-6f)));
        // ProjectiveUV_Accurate params [4, 2048, 2048] (void_hideoutc.mat): INFERRED as 4 repeats per 2048 world units.
        var uv = t.Mesh.Uv[i];
        mesh.Uvs.Add(t.Projective ? new Vector2(wx / 512f, wy / 512f) : new Vector2(uv.X < 0 ? uv.X + 1 : uv.X, uv.Y < 0 ? uv.Y + 1 : uv.Y));
      }
      for (var s = 0; s < t.Mesh.Shapes.Count; s++)
      {
        var (_, start, count) = t.Mesh.Shapes[s];
        var mat = (s < t.ShapeMats.Count ? t.ShapeMats[s] : t.ShapeMats.LastOrDefault()) ?? "none";
        if (!byMat.TryGetValue(mat, out var list)) byMat[mat] = list = new List<int>();
        for (var k = start; k + 2 < start + count; k += 3) { list.Add(b + t.Mesh.Idx[k]); list.Add(b + t.Mesh.Idx[k + 2]); list.Add(b + t.Mesh.Idx[k + 1]); }
      }
      placed++;
    }
    for (var y = -buffer; y < h + buffer; y++)
      for (var x = -buffer; x < w + buffer; x++)
      {
        if (y < 0 || x < 0 || y >= h || x >= grid[y].Count) { Place(outer, x, y); continue; }
        var kind = grid[y][x].GetProperty("kind").GetString();
        Place(kind == "K" ? tst ?? fill : fill, x, y);
      }
    foreach (var (mat, list) in byMat)
    {
      var part = new MeshPart { Name = "terrain_" + Path.GetFileNameWithoutExtension(mat), IndexStart = mesh.Indices.Count, IndexCount = list.Count };
      mesh.Indices.AddRange(list);
      ApplyMaterial(part, mat == "none" ? null : mat);
      part.Texture ??= part.Maps.FirstOrDefault(m => m.Role.Contains("color", StringComparison.OrdinalIgnoreCase))?.Name;
      // CelestialMaterial (void_hideoutc.mat): the look is the scrolling "01. Space Texture"; kept as the emissive map (INFERRED approximation, no scroll/parallax).
      part.Emissive ??= part.Maps.FirstOrDefault(m => m.Role.Contains("Space Texture", StringComparison.OrdinalIgnoreCase))?.Name;
      mesh.Parts.Add(part);
    }
    Log.Add($"tiles placed {placed} ({w + 2 * buffer}x{h + 2 * buffer}), {mesh.Positions.Count} verts, {mesh.Indices.Count / 3} tris");

    if (doodads && root.TryGetProperty("doodads", out var dd))
      foreach (var d in dd.EnumerateArray())
      {
        var ao = d.GetProperty("ao_file").GetString()!.ToLowerInvariant();
        try
        {
          using var aoDoc = Json(ao);
          var sm = aoDoc.RootElement.GetProperty("client_structs").EnumerateArray().Where(s => s.GetProperty("name").GetString() == "SkinMesh")
            .SelectMany(s => s.GetProperty("entries").EnumerateArray()).Select(e => e.GetProperty("value").GetString()).FirstOrDefault(v => v != null && v.EndsWith(".sm", StringComparison.OrdinalIgnoreCase));
          if (sm == null) { Log.Add($"doodad {ao}: no SkinMesh"); continue; }
          var m = Load(sm.ToLowerInvariant());
          float wx = d.GetProperty("x").GetSingle() * TileUnits / SubUnits, wy = d.GetProperty("y").GetSingle() * TileUnits / SubUnits;
          var rot = d.TryGetProperty("radians1", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetSingle() : 0f;
          var sc = d.TryGetProperty("scale", out var scv) && scv.ValueKind == JsonValueKind.Number ? scv.GetSingle() : 1f;
          // PoE rotation about its Z axis; ForgeLens Y-up, and the axis map is a reflection, so the angle flips (INFERRED).
          var xf = Matrix4x4.CreateScale(sc) * Matrix4x4.CreateRotationY(-rot) * Matrix4x4.CreateTranslation(P(wx, wy, 0));
          var b = mesh.Positions.Count;
          mesh.Positions.AddRange(m.Positions.Select(p => Vector3.Transform(p, xf)));
          mesh.Normals.AddRange(m.Normals.Select(n => Vector3.Normalize(Vector3.TransformNormal(n, xf))));
          mesh.Uvs.AddRange(m.Uvs);
          foreach (var p in m.Parts)
          {
            var np = new MeshPart
            {
              Name = Path.GetFileNameWithoutExtension(ao) + "_" + p.Name, IndexStart = mesh.Indices.Count, IndexCount = p.IndexCount,
              Variant = p.Variant, Texture = p.Texture, Normal = p.Normal, Emissive = p.Emissive, Blend = p.Blend, AlbedoTint = p.AlbedoTint, Visible = p.Visible,
            };
            np.Maps.AddRange(p.Maps);
            foreach (var kv in p.TdScalars) np.TdScalars[kv.Key] = kv.Value;
            mesh.Indices.AddRange(m.Indices.Skip(p.IndexStart).Take(p.IndexCount).Select(i => b + i));
            mesh.Parts.Add(np);
          }
          Log.Add($"doodad {ao} -> {sm} at ({wx:0},{wy:0}) rot {rot:0.###} scale {sc}: {m.Positions.Count} verts");
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or NotSupportedException)
        {
          Log.Add($"doodad {ao}: {e.GetType().Name} {e.Message}");
        }
      }
    Log.Add($"{arm}: {mesh.Positions.Count} verts, {mesh.Indices.Count / 3} tris, {mesh.Parts.Count} parts");
    return mesh;
  }
}
