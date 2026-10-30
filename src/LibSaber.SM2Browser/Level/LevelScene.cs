// R54: generic level loader. A level is scenes/<name>.scn/<name>.cd_list (+ sibling <name>.class_list) in the user's own
// game files. Read at runtime from the catalog (nothing bundled); placements cached in memory and as a TSV under
// %LOCALAPPDATA%\ForgeLens\levels (user cache, never shipped).
// cd_list: per object name, world matrix (16 floats, row-major, translation [12..14], game metres, Y-up), props text.
// class_list: actor class -> desc text. Template = props "nameTpl" (object override) else the class desc's.
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Level;

public sealed record LevelObject(int Index, string Name, string Class, string Kind, string[] Templates, Matrix4x4 World,
  float Radius, Vector3 Colour, float Intensity, string LightType);

public sealed class LevelScene
{
  public string Name = "";
  public List<LevelObject> Objects = new();
  public ExportedMesh Mesh = new() { Name = "level" };
  public Dictionary<string, PreviewImage> Albedo = new(StringComparer.OrdinalIgnoreCase), Normals = new(StringComparer.OrdinalIgnoreCase), Specular = new(StringComparer.OrdinalIgnoreCase);
  public List<PreviewPointLight> Lights = new();
  public List<(string Template, int Index, Matrix4x4 World)> Placed = new();
  public Vector3 Centre; public float Spread;
  public int LwiCount, LwiPlaced, BudgetSkipped;
  public string Stats = "";
  public SortedSet<string> Missing = new(), Skipped = new();

  public static bool IsLevelFile(string path) => path.EndsWith(".cd_list", StringComparison.OrdinalIgnoreCase);

  static readonly Dictionary<string, LevelScene> Cache = new(StringComparer.OrdinalIgnoreCase);

  public static string CacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForgeLens", "levels");

  // ---------- parsing (port of tools/level/cdlist.py, classlist.py, placement.py, lights_tsv2.py) ----------
  sealed class Reader
  {
    public byte[] D = Array.Empty<byte>(); public int P;
    public int I32() { var v = BitConverter.ToInt32(D, P); P += 4; return v; }
    public string S32() { var n = I32(); var s = Encoding.UTF8.GetString(D, P, n); P += n; return s; }
  }

  public static List<(string Name, Matrix4x4 M, string Props)> ParseCdList(byte[] d)
  {
    var r = new Reader { D = d, P = "1SERcd_list".Length + 0x35 };
    var cnt = r.I32(); r.I32();
    var names = new string[cnt]; var mats = new Matrix4x4[cnt]; var props = new string[cnt];
    Array.Fill(names, ""); Array.Fill(props, "");
    if (d[r.P++] != 0) for (var i = 0; i < cnt; i++) names[i] = r.S32();
    if (d[r.P++] != 0) throw new InvalidDataException("cd_list: unexpected section");
    if (d[r.P++] != 0)
      for (var i = 0; i < cnt; i++)
      {
        var f = new float[16]; for (var k = 0; k < 16; k++) f[k] = BitConverter.ToSingle(d, r.P + k * 4); r.P += 64;
        mats[i] = new Matrix4x4(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]);
      }
    if (d[r.P++] != 0) for (var i = 0; i < cnt; i++) r.S32();
    if (d[r.P++] != 0)
    {
      var nb = (cnt + 7) / 8; var bits = d.AsSpan(r.P, nb).ToArray(); r.P += nb;
      for (var i = 0; i < cnt; i++)
      {
        if (((bits[i >> 3] >> (i & 7)) & 1) == 0) continue;
        while (true)
        {
          var sid = BitConverter.ToUInt16(d, r.P); r.P += 6;
          if (sid == 0xFFFF) break;
          if (sid == 0) props[i] = r.S32(); else throw new InvalidDataException($"cd_list: sentinel {sid:x} at {r.P}");
        }
      }
    }
    var list = new List<(string, Matrix4x4, string)>(cnt);
    for (var i = 0; i < cnt; i++) list.Add((names[i], mats[i], props[i]));
    return list;
  }

  public static Dictionary<string, string> ParseClassList(byte[] d)
  {
    var r = new Reader { D = d, P = 0x40 };
    var cnt = r.I32(); var pc = r.I32();
    var names = new string[cnt]; var ps = new string[cnt]; Array.Fill(names, ""); Array.Fill(ps, "");
    if (pc > 0 && d[r.P++] != 0) for (var i = 0; i < cnt; i++) names[i] = r.S32();
    if (pc > 1 && d[r.P++] != 0) for (var i = 0; i < cnt; i++) ps[i] = r.S32();
    var map = new Dictionary<string, string>();
    for (var i = 0; i < cnt; i++) map[names[i]] = ps[i];
    return map;
  }

  /// <summary>
  ///   &lt;level&gt;.lwi_container ("light-weight instances": the bulk static architecture/decor, R54 format notes):
  ///   "stat_scene" u32 typeCount; per type an s32 key ("name" or "name/hash") followed by s32 "lwi_type_base" + type fields;
  ///   then u32 groupCount and per group u32 typeIndex, u32 hash, u32 count, count x 55-byte records:
  ///   12 floats (position xyz, 3x3 rotation*scale rows), u32 k, k x (u16 + 4x4 matrix), 3 flag bytes. The whole file is consumed exactly (validated).
  /// </summary>
  public static List<(string Template, Matrix4x4 World)> ParseLwi(byte[] c)
  {
    var key = Encoding.ASCII.GetBytes("\r\0\0\0lwi_type_base");
    var stat = Encoding.ASCII.GetBytes("stat_scene");
    var i = c.AsSpan().IndexOf(stat);
    if (i < 0) return new();
    var n = BitConverter.ToInt32(c, i + stat.Length);
    var types = new List<string>(); var last = 0; var from = 0;
    while (true)
    {
      var k = c.AsSpan(from).IndexOf(key); if (k < 0) break; k += from;
      var name = "";
      for (var L = 1; L < 300 && k - 4 - L >= 0; L++)
        if (BitConverter.ToInt32(c, k - 4 - L) == L) { name = Encoding.UTF8.GetString(c, k - L, L); break; }
      types.Add(name.Split('/')[0]); last = k + key.Length; from = last;
    }
    if (types.Count != n) throw new InvalidDataException($"lwi: {types.Count} type keys, header says {n}");
    var p = last;
    while (p >= 0 && p + 4 <= c.Length)
    {
      p = IndexOfInt(c, n, p); if (p < 0) break;
      var q = p + 4; var list = new List<(string, Matrix4x4)>(); var ok = true;
      for (var g = 0; g < n && ok; g++)
      {
        if (q + 12 > c.Length) { ok = false; break; }
        var idx = BitConverter.ToInt32(c, q); var cnt = BitConverter.ToInt32(c, q + 8); q += 12;
        if (idx < 0 || idx >= n || cnt < 0 || q + (long)cnt * 55 > c.Length) { ok = false; break; }
        for (var r = 0; r < cnt && ok; r++)
        {
          if (q + 55 > c.Length) { ok = false; break; }
          var at = q;
          float F(int j) => BitConverter.ToSingle(c, at + j * 4);
          list.Add((types[idx], new Matrix4x4(F(3), F(4), F(5), 0, F(6), F(7), F(8), 0, F(9), F(10), F(11), 0, F(0), F(1), F(2), 1)));
          // u32 k sub-entries of (u16 id + 4x4 matrix) follow (attached parts, seen in story_intro), then 3 flag bytes.
          var sub = BitConverter.ToInt32(c, q + 48);
          if (sub < 0 || sub > 1000) { ok = false; break; }
          q += 52 + sub * 66 + 3;
        }
      }
      if (ok && q == c.Length) return list;
      p += 1;
    }
    throw new InvalidDataException("lwi: instance table did not parse to the end of the file");
  }

  static int IndexOfInt(byte[] c, int v, int from)
  {
    var b = BitConverter.GetBytes(v);
    var k = c.AsSpan(from).IndexOf(b); return k < 0 ? -1 : k + from;
  }

  static readonly Regex Tpl = new(@"\bnameTpl = ""([^""]*)""", RegexOptions.Compiled);
  static readonly Regex ActorType = new(@"__type = ""([^""]*__actor)""", RegexOptions.Compiled);
  static string? Ctrl(string ps, string k) { var m = Regex.Match(ps, @"\b" + k + @" = \{value = ([^;]*);"); return m.Success ? m.Groups[1].Value.Trim('"') : null; }
  static string? Key(string ps, string k) { var m = Regex.Match(ps, @"\b" + k + @" = (""?[^;{]*""?);"); return m.Success ? m.Groups[1].Value.Trim('"') : null; }

  public static List<LevelObject> ParseObjects(byte[] cdList, byte[] classList)
  {
    var ents = ParseCdList(cdList); var cls = ParseClassList(classList); var ci = CultureInfo.InvariantCulture;
    var list = new List<LevelObject>(ents.Count);
    for (var i = 0; i < ents.Count; i++)
    {
      var (name, m, ps) = ents[i];
      var types = ActorType.Matches(ps); var cl = types.Count > 0 ? types[^1].Groups[1].Value : "";
      var cps = cls.TryGetValue(cl, out var c) ? c : "";
      var all = ps + cps;
      var kind = all.Contains("sfx_light") ? "light" : all.Contains("prop_camera") ? "camera" : all.Contains("geom") ? "geom" : "other";
      var t = Tpl.Matches(ps).Select(x => x.Groups[1].Value).ToArray();
      if (t.Length == 0) t = Tpl.Matches(cps).Select(x => x.Groups[1].Value).ToArray();
      float radius = 0, inten = 0; var col = Vector3.One; var ltype = "";
      if (kind == "light" && ps.Split("sfx_light").Length <= 2)
      {
        string? Get(string k, bool ctrl) => (ctrl ? Ctrl(ps, k) : Key(ps, k)) ?? (ctrl ? Ctrl(cps, k) : Key(cps, k));
        radius = float.TryParse(Get("RadiusCtrl", true), NumberStyles.Float, ci, out var rv) ? rv : 5f;
        inten = float.TryParse(Get("Intensity", true), NumberStyles.Float, ci, out var iv) ? iv : 1f;
        var cc = (Get("ColorCtrl", true) ?? "").Trim('[', ']').Split(',');
        // [A,R,G,B]: first value is 255 in 1,821 of 1,865 hub_battle_barge lights (INFERRED alpha-first).
        if (cc.Length == 4 && float.TryParse(cc[1], NumberStyles.Float, ci, out var R) && float.TryParse(cc[2], NumberStyles.Float, ci, out var G) && float.TryParse(cc[3], NumberStyles.Float, ci, out var B))
          col = new Vector3(R, G, B) / 255f;
        ltype = Get("lightType", false) ?? (cl.Contains("spot_light") ? "spot" : "point");
      }
      list.Add(new LevelObject(i, name.StartsWith("|x|") ? name.Substring(3) : name, cl, kind, t.Distinct().ToArray(), m, radius, col, inten, ltype));
    }
    return list;
  }

  // ---------- build ----------
  public sealed record Options(float MaxSize = 1000f, bool Textured = true, int ShrinkEdge = 512, long MaxTriangles = 20_000_000, float LightGain = 1f);

  public static int FindClassList(GameCatalog catalog, int cdIndex)
  {
    var path = catalog.GetFile(cdIndex).Path;
    return catalog.ResolveReference(Path.ChangeExtension(Path.GetFileName(path), ".class_list"), cdIndex);
  }

  public static LevelScene Load(GameCatalog catalog, ArchiveStore store, int cdIndex, Options? o = null, Action<string>? log = null)
  {
    var path = catalog.GetFile(cdIndex).Path;
    lock (Cache) if (Cache.TryGetValue(path, out var hit)) { log?.Invoke("cache hit " + path); return hit; }
    o ??= new Options();
    var sw = Stopwatch.StartNew();
    var cl = FindClassList(catalog, cdIndex);
    if (cl < 0) throw new FileNotFoundException("class_list next to " + path);
    var scene = new LevelScene { Name = Path.GetFileNameWithoutExtension(path) };
    scene.Objects = ParseObjects(store.Read(cdIndex), store.Read(cl));
    var lwiIndex = catalog.ResolveReference(Path.ChangeExtension(Path.GetFileName(path), ".lwi_container"), cdIndex);
    var lwi = new List<(string Template, Matrix4x4 World)>();
    if (lwiIndex >= 0 && Path.GetDirectoryName(catalog.GetFile(lwiIndex).Path) == Path.GetDirectoryName(path))
    {
      try { lwi = ParseLwi(store.Read(lwiIndex)); }
      catch (Exception e) { log?.Invoke("lwi parse failed: " + e.Message); }
    }
    scene.LwiCount = lwi.Count;
    var tParse = sw.Elapsed.TotalSeconds;
    WriteCacheTsv(scene);
    var meshes = new Dictionary<string, ExportedMesh?>(StringComparer.OrdinalIgnoreCase);
    var origins = new List<Vector3>();
    int placed = 0, failed = 0; long tris = 0;
    var textured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var tplIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    void Textures(string tpl, ExportedMesh mesh)
    {
      if (!o.Textured || !textured.Add(tpl) || !tplIndex.TryGetValue(tpl, out var hit)) return;
      var tex = MainWindow.ResolvePreview(catalog, store, mesh, hit);
      foreach (var kv in tex.Albedo) scene.Albedo.TryAdd(kv.Key, Shrink(kv.Value, o.ShrinkEdge));
      foreach (var kv in tex.Normals) scene.Normals.TryAdd(kv.Key, Shrink(kv.Value, o.ShrinkEdge));
      foreach (var kv in tex.Specular) scene.Specular.TryAdd(kv.Key, Shrink(kv.Value, o.ShrinkEdge));
    }
    var lods = new Dictionary<string, ExportedMesh?>(StringComparer.OrdinalIgnoreCase);
    // lwi instances draw a reduced LOD (LOD1 when the template has one) so far more of them fit the triangle budget.
    ExportedMesh? Lod(string tpl)
    {
      if (lods.TryGetValue(tpl, out var l)) return l;
      var m = Get(tpl); l = m;
      if (m is not null && m.Levels.Count > 0)
      {
        var lv = m.Levels[0];
        var e = new ExportedMesh { Name = tpl };
        e.Positions.AddRange(lv.Positions); e.Normals.AddRange(lv.Normals); e.Uvs.AddRange(lv.Uvs); e.Indices.AddRange(lv.Indices); e.Parts.AddRange(lv.Parts);
        if (e.HasTriangles && e.Indices.Count < m.Indices.Count) l = e;
      }
      lods[tpl] = l; return l;
    }
    ExportedMesh? Get(string tpl)
    {
      if (meshes.TryGetValue(tpl, out var mesh)) return mesh;
      mesh = null;
      var hits = catalog.FindByFileName(tpl + ".tpl");
      if (hits.Count == 0) scene.Missing.Add(tpl);
      else
      {
        try
        {
          mesh = TemplateMeshReader.Load(catalog, store, hits[0]);
          tplIndex[tpl] = hits[0];
          if (!mesh.HasTriangles) mesh = null;
        }
        catch (Exception e) { failed++; log?.Invoke($"fail {tpl}: {e.GetType().Name} {e.Message}"); }
      }
      meshes[tpl] = mesh;
      return mesh;
    }

    var work = new List<(string Tpl, int Index, Matrix4x4 World, bool Lwi)>();
    foreach (var obj in scene.Objects)
    {
      if (obj.Kind == "light" && obj.Radius > 0 && obj.LightType != "directional")
      {
        var w = (0.5f + 0.3f * MathF.Log10(1 + MathF.Max(obj.Intensity, 0))) * o.LightGain; // HEURISTIC preview weight (units differ per light)
        scene.Lights.Add(new PreviewPointLight(obj.World.Translation, obj.Colour * w, Math.Clamp(obj.Radius, 1f, 40f)));
      }
      if (obj.Kind == "geom") foreach (var tpl in obj.Templates) work.Add((tpl, obj.Index, obj.World, false));
    }
    var lwiStart = work.Count;
    for (var j = 0; j < lwi.Count; j++) work.Add((lwi[j].Template, -1 - j, lwi[j].World, true));
    // Triangle budget: cd_list objects first, then lwi instances largest-template-first so a cut drops small clutter (bolts, candles), not walls.
    float Size(string tpl) { var m = Get(tpl); return m is null ? 0 : Extent(m, Matrix4x4.Identity); }
    var ordered = work.Take(lwiStart).Concat(work.Skip(lwiStart).OrderByDescending(x => Size(x.Tpl))).ToList();
    foreach (var (tpl, idx, world, isLwi) in ordered)
    {
      var full = Get(tpl);
      var mesh = isLwi ? Lod(tpl) : full;
      if (mesh is null || full is null) continue;
      if (Extent(mesh, world) > o.MaxSize) { scene.Skipped.Add(tpl); continue; }
      var t = mesh.Indices.Count / 3;
      if (tris + t > o.MaxTriangles) { scene.BudgetSkipped++; continue; }
      tris += t;
      Textures(tpl, full);
      Append(scene.Mesh, mesh, world, tpl);
      scene.Placed.Add((tpl, idx, world));
      origins.Add(world.Translation); placed++;
      if (isLwi) scene.LwiPlaced++;
    }
    // Frame the densest cluster (levels are often separate islands far apart, so the global median can be empty space):
    // 40 m voxel with most placed parts, then the parts within 200 m of it; centre = their median, spread = their 10..90 % range.
    var cell = 40f;
    var best = origins.GroupBy(v => ((int)MathF.Floor(v.X / cell), (int)MathF.Floor(v.Y / cell), (int)MathF.Floor(v.Z / cell)))
      .OrderByDescending(g => g.Count()).FirstOrDefault();
    var seed = best is null ? Vector3.Zero : new Vector3(best.Average(v => v.X), best.Average(v => v.Y), best.Average(v => v.Z));
    var near = origins.Where(v => Vector3.Distance(v, seed) < 200f).ToList();
    if (near.Count == 0) near = origins;
    float Q(Func<Vector3, float> f, float q) { var a = near.Select(f).OrderBy(v => v).ToList(); return a.Count == 0 ? 0 : a[(int)((a.Count - 1) * q)]; }
    scene.Centre = new Vector3(Q(v => v.X, .5f), Q(v => v.Y, .5f), Q(v => v.Z, .5f));
    scene.Spread = MathF.Max(10f, MathF.Max(Q(v => v.X, .9f) - Q(v => v.X, .1f), Q(v => v.Z, .9f) - Q(v => v.Z, .1f)));
    var k = scene.Objects.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count());
    scene.Stats = $"{scene.Name}: objects {scene.Objects.Count} (geom {k.GetValueOrDefault("geom")}, light {k.GetValueOrDefault("light")}, camera {k.GetValueOrDefault("camera")}, other {k.GetValueOrDefault("other")}); " +
      $"placed {placed} parts from {meshes.Count(x => x.Value != null)}/{meshes.Count} templates, missing {scene.Missing.Count}, failed {failed}, skipped {scene.Skipped.Count}, over budget {scene.BudgetSkipped}; lwi {scene.LwiPlaced}/{scene.LwiCount}; " +
      $"{tris:N0} tris; lights {scene.Lights.Count}; focus {near.Count} parts, spread {scene.Spread:0} m; textures {scene.Albedo.Count}/{scene.Normals.Count}/{scene.Specular.Count}; parse {tParse:0.0}s total {sw.Elapsed.TotalSeconds:0.0}s";
    log?.Invoke(scene.Stats);
    lock (Cache) Cache[path] = scene;
    return scene;
  }

  static void WriteCacheTsv(LevelScene s)
  {
    try
    {
      Directory.CreateDirectory(CacheDir);
      using var w = new StreamWriter(Path.Combine(CacheDir, s.Name + ".placement.tsv"));
      w.WriteLine("idx\tname\tclass\tkind\ttemplates\tx\ty\tz\tmatrix16");
      foreach (var o in s.Objects)
      {
        var m = o.World;
        w.WriteLine(string.Join('\t', o.Index, o.Name, o.Class, o.Kind, string.Join(';', o.Templates),
          m.M41.ToString(CultureInfo.InvariantCulture), m.M42.ToString(CultureInfo.InvariantCulture), m.M43.ToString(CultureInfo.InvariantCulture),
          string.Join(' ', new[] { m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44 }.Select(v => v.ToString("G6", CultureInfo.InvariantCulture)))));
      }
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  public static PreviewImage Shrink(PreviewImage im, int edge)
  {
    var f = 1; while (im.Width / f > edge || im.Height / f > edge) f *= 2;
    if (f == 1) return im;
    int w = Math.Max(1, im.Width / f), h = Math.Max(1, im.Height / f); var o = new byte[w * h * 4];
    for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) for (var c = 0; c < 4; c++)
    {
      var sum = 0; for (var j = 0; j < f; j++) for (var i = 0; i < f; i++) sum += im.Bgra[(((y * f + j) * im.Width) + x * f + i) * 4 + c];
      o[(y * w + x) * 4 + c] = (byte)(sum / (f * f));
    }
    return new PreviewImage { Width = w, Height = h, Bgra = o, Channels = im.Channels };
  }

  public static float Extent(ExportedMesh mesh, Matrix4x4 world)
  {
    var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
    foreach (var p in mesh.Positions) { var q = Vector3.Transform(p, world); min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
    var d = max - min; return MathF.Max(d.X, MathF.Max(d.Y, d.Z));
  }

  public static void Append(ExportedMesh dst, ExportedMesh src, Matrix4x4 world, string name)
  {
    var baseV = dst.Positions.Count; var baseI = dst.Indices.Count;
    foreach (var p in src.Positions) dst.Positions.Add(Vector3.Transform(p, world));
    for (var i = 0; i < src.Positions.Count; i++)
      dst.Normals.Add(i < src.Normals.Count ? Vector3.Normalize(Vector3.TransformNormal(src.Normals[i], world)) : Vector3.UnitY);
    for (var i = 0; i < src.Positions.Count; i++) dst.Uvs.Add(i < src.Uvs.Count ? src.Uvs[i] : Vector2.Zero);
    var flip = world.GetDeterminant() < 0;
    for (var i = 0; i + 2 < src.Indices.Count; i += 3)
    {
      dst.Indices.Add(baseV + src.Indices[i]);
      dst.Indices.Add(baseV + src.Indices[i + (flip ? 2 : 1)]);
      dst.Indices.Add(baseV + src.Indices[i + (flip ? 1 : 2)]);
    }
    if (src.Parts.Count == 0) dst.Parts.Add(new MeshPart { Name = name, IndexStart = baseI, IndexCount = dst.Indices.Count - baseI });
    foreach (var part in src.Parts) { var cp = part.Copy(baseI + part.IndexStart, part.IndexCount); cp.Name = name + "/" + part.Name; dst.Parts.Add(cp); }
  }
}
