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
using LibSaber.SM2.Assets.Textures;

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
  // R54b: parallel template + texture loading, hidden/volume parts dropped (IndexMeshVisibility), LOD chosen per
  // instance by projected size, budget ranked by size/distance from the focus, disk cache of the built level
  // (template geometry + placements + cameras) and a shared texture cache, all under %LOCALAPPDATA%\ForgeLens.
  public sealed record Options(float MaxSize = 1000f, bool Textured = true, int ShrinkEdge = 512, long MaxTriangles = 20_000_000, float LightGain = 1f, bool UseCache = true);
  public const int CacheVersion = 8;

  public OrbitCamera Persp, Close, Top;
  public bool FromDiskCache;
  public string Timing = "";
  public int HiddenParts, WorkCount, DarkTranslucentDropped;
  public long Triangles;
  public List<string> Untextured = new();
  /// <summary>texture name -> catalog file index (pack stores refs into the shared texture cache, not pixels).</summary>
  public Dictionary<string, int> TexIndex = new(StringComparer.OrdinalIgnoreCase);

  sealed class Geo
  {
    public string Tpl = ""; public int Hint = -1;
    public ExportedMesh?[] V = new ExportedMesh?[3]; // 0 = LOD0 visible parts, 1 = first LOD level, 2 = coarsest LOD level
    public Vector3 Min, Max;
    public float Size => MathF.Max(Max.X - Min.X, MathF.Max(Max.Y - Min.Y, Max.Z - Min.Z));
    public ExportedMesh? Pick(int want) { for (var k = want; k >= 0; k--) if (V[k] is not null) return V[k]; return null; }
    public int PickLevel(int want) { for (var k = want; k >= 0; k--) if (V[k] is not null) return k; return -1; }
  }

  static readonly HashSet<string> HideKeep = new(StringComparer.OrdinalIgnoreCase) { "LOD" };

  /// <summary>Copy of the triangles of the kept parts with only the vertices they use.</summary>
  static ExportedMesh? Compact(string name, List<Vector3> pos, List<Vector3> nrm, List<Vector2> uv, List<int> idx, List<MeshPart> parts, Func<MeshPart, bool> keep, ref int dropped)
  {
    var e = new ExportedMesh { Name = name };
    var map = new int[pos.Count]; Array.Fill(map, -1);
    void Tri(int i0, int i1)
    {
      for (var i = i0; i < i1 && i < idx.Count; i++)
      {
        var v = idx[i]; if (v < 0 || v >= pos.Count) { e.Indices.Add(0); continue; }
        if (map[v] < 0)
        {
          map[v] = e.Positions.Count; e.Positions.Add(pos[v]);
          e.Normals.Add(v < nrm.Count ? nrm[v] : Vector3.UnitY); e.Uvs.Add(v < uv.Count ? uv[v] : Vector2.Zero);
        }
        e.Indices.Add(map[v]);
      }
    }
    if (parts.Count == 0) Tri(0, idx.Count - idx.Count % 3);
    foreach (var p in parts)
    {
      if (p.IndexCount < 3) continue;
      if (!keep(p)) { dropped++; continue; }
      var s = e.Indices.Count; Tri(p.IndexStart, p.IndexStart + p.IndexCount - p.IndexCount % 3);
      e.Parts.Add(p.Copy(s, e.Indices.Count - s));
    }
    return e.Indices.Count >= 3 ? e : null;
  }

  static Geo? MakeGeo(string tpl, int hint, ExportedMesh m, ref int dropped)
  {
    var g = new Geo { Tpl = tpl, Hint = hint };
    // sfx cards (light cones, flares) are hidden in the model viewer too (MaterialMaps.IsSfxPart); drawn opaque they were the
    // black shards (hub: 3.36M tris "translucent/opaque/sfx" measured before this rule).
    g.V[0] = Compact(tpl, m.Positions, m.Normals, m.Uvs, m.Indices, m.Parts, p => p.Visible && !MaterialMaps.IsSfxPart(p), ref dropped);
    if (m.Levels.Count > 0)
    {
      // Lower levels: Index unchecks them as "LOD"; keep those, drop volume / sfx / cloth-sim shells.
      bool Keep(MeshPart p) => !MaterialMaps.IsSfxPart(p) && (p.Visible || (p.HideReason is { } r && HideKeep.Contains(r)));
      var a = m.Levels[0]; var z = m.Levels[^1]; var d2 = 0;
      g.V[1] = Compact(tpl, a.Positions, a.Normals, a.Uvs, a.Indices, a.Parts, Keep, ref d2);
      if (m.Levels.Count > 1) g.V[2] = Compact(tpl, z.Positions, z.Normals, z.Uvs, z.Indices, z.Parts, Keep, ref d2);
      if (g.V[0] is not null)
        for (var k = 1; k < 3; k++) if (g.V[k] is { } l && l.Indices.Count >= g.V[0]!.Indices.Count) g.V[k] = null;
    }
    if (g.V[0] is null) { if (g.V[1] is null) return null; g.V[0] = g.V[1]; g.V[1] = null; }
    g.Min = new Vector3(float.MaxValue); g.Max = new Vector3(float.MinValue);
    foreach (var p in g.V[0]!.Positions) { g.Min = Vector3.Min(g.Min, p); g.Max = Vector3.Max(g.Max, p); }
    return g;
  }

  static float WorldSize(Geo g, Matrix4x4 w)
  {
    var mn = new Vector3(float.MaxValue); var mx = new Vector3(float.MinValue);
    for (var c = 0; c < 8; c++)
    {
      var p = Vector3.Transform(new Vector3((c & 1) == 0 ? g.Min.X : g.Max.X, (c & 2) == 0 ? g.Min.Y : g.Max.Y, (c & 4) == 0 ? g.Min.Z : g.Max.Z), w);
      mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
    }
    var d = mx - mn; return MathF.Max(d.X, MathF.Max(d.Y, d.Z));
  }

  public static int FindClassList(GameCatalog catalog, int cdIndex)
  {
    var path = catalog.GetFile(cdIndex).Path;
    return catalog.ResolveReference(Path.ChangeExtension(Path.GetFileName(path), ".class_list"), cdIndex);
  }

  static string Stamp(GameCatalog catalog, int index)
  {
    var f = catalog.GetFile(index);
    long t = 0; try { t = File.GetLastWriteTimeUtc(catalog.Archives[f.ArchiveId].PakPath).Ticks; } catch (Exception) { }
    // catalog-independent (the viewer and levelbench may index the game differently: file indices / counts differ)
    // ArchiveName is relative to the opened folder (game root vs client_pc differ), so use the pak file name.
    return $"{f.Path}-{f.Size}-{Path.GetFileName(catalog.Archives[f.ArchiveId].PakPath)}-{t}";
  }

  public static LevelScene Load(GameCatalog catalog, ArchiveStore store, int cdIndex, Options? o = null, Action<string>? log = null)
  {
    var path = catalog.GetFile(cdIndex).Path;
    lock (Cache) if (Cache.TryGetValue(path, out var hit)) { log?.Invoke("memory cache hit " + path); return hit; }
    o ??= new Options();
    var sw = Stopwatch.StartNew(); var times = new StringBuilder();
    void Mark(string what) { times.Append($"{what} {sw.Elapsed.TotalSeconds:0.0}s; "); }
    var cl = FindClassList(catalog, cdIndex);
    if (cl < 0) throw new FileNotFoundException("class_list next to " + path);
    var scene = new LevelScene { Name = Path.GetFileNameWithoutExtension(path) };
    var cdBytes = store.Read(cdIndex); var clBytes = store.Read(cl);
    scene.Objects = ParseObjects(cdBytes, clBytes);
    var lwiIndex = catalog.ResolveReference(Path.ChangeExtension(Path.GetFileName(path), ".lwi_container"), cdIndex);
    var hasLwi = lwiIndex >= 0 && Path.GetDirectoryName(catalog.GetFile(lwiIndex).Path) == Path.GetDirectoryName(path);
    var lwiBytes = hasLwi ? store.Read(lwiIndex) : Array.Empty<byte>();
    // content hashes: the viewer (game root) and levelbench (client_pc) can resolve the same path to different paks
    static string H(byte[] b) => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(b))[..16];
    var key = $"v{CacheVersion}|{H(cdBytes)}|{H(clBytes)}|{H(lwiBytes)}|{o}";
    var pack = Path.Combine(CacheDir, scene.Name + ".flpack");
    Mark("parse cd_list");
    foreach (var obj in scene.Objects)
      if (obj.Kind == "light" && obj.Radius > 0 && obj.LightType != "directional")
      {
        var w = (0.5f + 0.3f * MathF.Log10(1 + MathF.Max(obj.Intensity, 0))) * o.LightGain; // HEURISTIC preview weight (units differ per light)
        scene.Lights.Add(new PreviewPointLight(obj.World.Translation, obj.Colour * w, Math.Clamp(obj.Radius, 1f, 40f)));
      }
    if (o.UseCache && TryReadPack(scene, pack, key, catalog, store, o, log))
    {
      Mark("disk cache"); scene.FromDiskCache = true; scene.Timing = times.ToString();
      scene.Stats += $" | opened from disk cache in {sw.Elapsed.TotalSeconds:0.0}s";
      log?.Invoke(scene.Stats); log?.Invoke("timing " + scene.Timing);
      lock (Cache) Cache[path] = scene;
      return scene;
    }
    var lwi = new List<(string Template, Matrix4x4 World)>();
    if (hasLwi)
    {
      try { lwi = ParseLwi(lwiBytes); }
      catch (Exception e) { log?.Invoke("lwi parse failed: " + e.Message); }
    }
    scene.LwiCount = lwi.Count;
    WriteCacheTsv(scene);
    Mark("parse lwi");

    var work = new List<(string Tpl, int Index, Matrix4x4 World, bool Lwi)>();
    foreach (var obj in scene.Objects)
      if (obj.Kind == "geom") foreach (var tpl in obj.Templates) work.Add((tpl, obj.Index, obj.World, false));
    for (var j = 0; j < lwi.Count; j++) work.Add((lwi[j].Template, -1 - j, lwi[j].World, true));
    scene.WorkCount = work.Count;

    // 1) every distinct template in parallel (archive reads are locked per read, decode is per thread)
    var names = work.Select(x => x.Tpl).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var geos = new System.Collections.Concurrent.ConcurrentDictionary<string, Geo?>(StringComparer.OrdinalIgnoreCase);
    var missing = new System.Collections.Concurrent.ConcurrentBag<string>(); var failed = 0; var hidden = 0;
    Parallel.ForEach(names, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, tpl =>
    {
      var hits = catalog.FindByFileName(tpl + ".tpl");
      if (hits.Count == 0) { missing.Add(tpl); geos[tpl] = null; return; }
      try
      {
        var mesh = TemplateMeshReader.Load(catalog, store, hits[0]);
        var d = 0; var g = mesh.HasTriangles ? MakeGeo(tpl, hits[0], mesh, ref d) : null;
        Interlocked.Add(ref hidden, d); geos[tpl] = g;
      }
      catch (Exception e) { Interlocked.Increment(ref failed); geos[tpl] = null; log?.Invoke($"fail {tpl}: {e.GetType().Name} {e.Message}"); }
    });
    foreach (var m in missing) scene.Missing.Add(m);
    scene.HiddenParts = hidden;
    Mark($"templates x{names.Count}");

    // 2) focus = densest 40 m voxel of all placements, then parts within 200 m (median centre, 10..90 % spread)
    Focus(scene, work.Where(x => geos.TryGetValue(x.Tpl, out var g) && g is not null).Select(x => x.World.Translation).ToList());

    // 3) rank: projected size (world size / distance to focus) so big near things win and far clutter goes first;
    //    LOD per instance: >1 LOD0, >0.2 first LOD, else coarsest.
    var cand = new List<(int Work, Geo G, float Score, int Level)>(work.Count);
    for (var i = 0; i < work.Count; i++)
    {
      var (tpl, _, world, _) = work[i];
      if (!geos.TryGetValue(tpl, out var g) || g is null) continue;
      var size = WorldSize(g, world);
      if (size > o.MaxSize) { scene.Skipped.Add(tpl); continue; }
      var dist = MathF.Max(5f, Vector3.Distance(world.Translation, scene.Centre) - size * 0.5f);
      var score = size / dist;
      cand.Add((i, g, score, g.PickLevel(score > 1f ? 0 : score > 0.2f ? 1 : 2)));
    }
    cand.Sort((a, b) => b.Score.CompareTo(a.Score));
    var chosen = new List<(int Work, Geo G, int Level)>(); long tris = 0;
    foreach (var c in cand)
    {
      var t = c.G.V[c.Level]!.Indices.Count / 3;
      if (tris + t > o.MaxTriangles)
      {
        // try the coarsest variant before giving up on this instance
        var lv = c.G.PickLevel(2); var t2 = c.G.V[lv]!.Indices.Count / 3;
        if (tris + t2 > o.MaxTriangles) { scene.BudgetSkipped++; continue; }
        chosen.Add((c.Work, c.G, lv)); tris += t2; continue;
      }
      chosen.Add((c.Work, c.G, c.Level)); tris += t;
    }
    scene.Triangles = tris;
    Mark("rank");

    // 4) textures of the chosen variants, decoded in parallel at the first mip <= ShrinkEdge, shared disk cache
    if (o.Textured) LoadTextures(scene, catalog, store, chosen.Select(c => (c.G, c.G.V[c.Level]!)).ToList(), o.ShrinkEdge, log);
    Mark("textures");
    DropDarkTranslucent(scene, chosen.Select(c => (c.G, c.Level)).Distinct().ToList());

    // 5) merged mesh
    foreach (var (wi, g, lv) in chosen)
    {
      var (tpl, idx, world, isLwi) = work[wi];
      Append(scene.Mesh, g.V[lv]!, world, tpl);
      scene.Placed.Add((tpl, idx, world));
      if (isLwi) scene.LwiPlaced++;
    }
    Mark("merge");
    Untex(scene);
    PickCameras(scene);
    Mark("cameras");
    var k = scene.Objects.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count());
    scene.Stats = $"{scene.Name}: objects {scene.Objects.Count} (geom {k.GetValueOrDefault("geom")}, light {k.GetValueOrDefault("light")}, camera {k.GetValueOrDefault("camera")}, other {k.GetValueOrDefault("other")}); " +
      $"placed {scene.Placed.Count}/{work.Count} instances from {geos.Count(x => x.Value != null)}/{geos.Count} templates, missing {scene.Missing.Count}, failed {failed}, hidden parts dropped {hidden}, dark translucent dropped {scene.DarkTranslucentDropped}, skipped >{o.MaxSize:0} m {scene.Skipped.Count}, over budget {scene.BudgetSkipped}; lwi {scene.LwiPlaced}/{scene.LwiCount}; " +
      $"{tris:N0} tris; lights {scene.Lights.Count}; spread {scene.Spread:0} m; textures {scene.Albedo.Count}/{scene.Normals.Count}/{scene.Specular.Count}; built in {sw.Elapsed.TotalSeconds:0.0}s";
    if (o.UseCache) WritePack(catalog, scene, pack, key, o.ShrinkEdge, geos.Values.Where(g => g is not null).Cast<Geo>().ToList(), chosen, work, log);
    Mark("write cache");
    scene.Timing = times.ToString();
    log?.Invoke(scene.Stats); log?.Invoke("timing " + scene.Timing);
    lock (Cache) Cache[path] = scene;
    return scene;
  }

  /// <summary>
  ///   Translucent parts whose albedo averages near-black (luminance &lt; 20 of 255) are additive-looking holograms / glow
  ///   cards in game; the CPU preview draws them opaque, which gave the big black spheres (hub hologram_planet,
  ///   glob_sphere3). HEURISTIC rule: drop them. Opaque black (e.g. sc_black_01) stays.
  /// </summary>
  static void DropDarkTranslucent(LevelScene s, List<(Geo G, int Level)> used)
  {
    var lum = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    bool Dark(MeshPart p)
    {
      if (p.Blend != "translucent" || p.Texture is null || !s.Albedo.TryGetValue(p.Texture, out var im)) return false;
      if (!lum.TryGetValue(p.Texture, out var v))
      {
        double sum = 0; for (var i = 0; i + 3 < im.Bgra.Length; i += 4) sum += 0.11 * im.Bgra[i] + 0.59 * im.Bgra[i + 1] + 0.3 * im.Bgra[i + 2];
        lum[p.Texture] = v = sum / Math.Max(1, im.Bgra.Length / 4);
      }
      return v < 20;
    }
    foreach (var (g, lv) in used)
    {
      var m = g.V[lv]!;
      if (!m.Parts.Any(Dark)) continue;
      var d = 0;
      g.V[lv] = Compact(m.Name, m.Positions, m.Normals, m.Uvs, m.Indices, m.Parts, p => !Dark(p), ref d) ?? new ExportedMesh { Name = m.Name };
      s.DarkTranslucentDropped += d;
    }
  }

  static void Focus(LevelScene scene, List<Vector3> origins)
  {
    var cell = 40f;
    var best = origins.GroupBy(v => ((int)MathF.Floor(v.X / cell), (int)MathF.Floor(v.Y / cell), (int)MathF.Floor(v.Z / cell)))
      .OrderByDescending(g => g.Count()).FirstOrDefault();
    var seed = best is null ? Vector3.Zero : new Vector3(best.Average(v => v.X), best.Average(v => v.Y), best.Average(v => v.Z));
    var near = origins.Where(v => Vector3.Distance(v, seed) < 200f).ToList();
    if (near.Count == 0) near = origins;
    float Q(Func<Vector3, float> f, float q) { var a = near.Select(f).OrderBy(v => v).ToList(); return a.Count == 0 ? 0 : a[(int)((a.Count - 1) * q)]; }
    scene.Centre = new Vector3(Q(v => v.X, .5f), Q(v => v.Y, .5f), Q(v => v.Z, .5f));
    scene.Spread = MathF.Max(10f, MathF.Max(Q(v => v.X, .9f) - Q(v => v.X, .1f), Q(v => v.Z, .9f) - Q(v => v.Z, .1f)));
  }

  /// <summary>Largest placed templates whose parts got no albedo image (diagnostic for "black" meshes).</summary>
  static void Untex(LevelScene s)
  {
    var seen = new Dictionary<string, int>();
    foreach (var p in s.Mesh.Parts)
    {
      if (p.Texture is { } t && s.Albedo.ContainsKey(t)) continue;
      var tpl = p.Name.Split('/')[0];
      seen[tpl] = seen.GetValueOrDefault(tpl) + p.IndexCount / 3;
    }
    s.Untextured = seen.OrderByDescending(x => x.Value).Take(25).Select(x => $"{x.Key}({x.Value} tris)").ToList();
  }

  /// <summary>
  ///   Cameras that are not inside geometry: 12 yaws x 2 distances rendered at 160x100; the brightest mean wins
  ///   (a camera inside a wall renders the wall's back faces / nothing lit = near-black). Top is fixed.
  /// </summary>
  public static void PickCameras(LevelScene s)
  {
    var settings = new PreviewSettings { MaxEdge = 256, Albedo = s.Albedo, PointLights = s.Lights, Ambient = 0.22f, HeadLight = 0.6f, Exposure = 1.3f };
    OrbitCamera Best(float pitch, float[] dists)
    {
      var best = new OrbitCamera { Yaw = 0.7f, Pitch = pitch, Target = s.Centre, Distance = s.Spread * dists[0] }; var bestScore = -1.0;
      for (var y = 0; y < 12; y++)
        foreach (var d in dists)
        {
          var cam = new OrbitCamera { Yaw = y * MathF.PI / 6, Pitch = pitch, Target = s.Centre, Distance = s.Spread * d };
          byte[] px;
          try { px = MeshPreview.Render(s.Mesh, 160, 100, cam, false, 0, out _, settings); } catch (Exception) { continue; }
          // well exposed (20..235) share x luminance std-dev: a wall filling the view (flat, over/under exposed) or a
          // camera inside geometry (near-black) scores low; open views with structure score high.
          double sum = 0, sq = 0; var ok = 0; var n = px.Length / 4;
          for (var i = 0; i + 3 < px.Length; i += 4) { var l = 0.11 * px[i] + 0.59 * px[i + 1] + 0.3 * px[i + 2]; sum += l; sq += l * l; if (l > 20 && l < 235) ok++; }
          var mean = sum / n; var sd = Math.Sqrt(Math.Max(0, sq / n - mean * mean));
          var score = sd * ok / n;
          if (score > bestScore) { bestScore = score; best = cam; }
        }
      return best;
    }
    s.Top = new OrbitCamera { Yaw = 0f, Pitch = 1.2f, Target = s.Centre, Distance = s.Spread * 1.4f };
    if (s.Mesh.Indices.Count < 3) { s.Persp = s.Close = s.Top; return; }
    s.Persp = Best(0.45f, new[] { 1.1f, 0.8f });
    s.Close = Best(0.3f, new[] { 0.35f, 0.5f });
  }

  // ---------- textures ----------
  static string TexDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForgeLens", "textures");

  static void LoadTextures(LevelScene scene, GameCatalog catalog, ArchiveStore store, List<(Geo G, ExportedMesh M)> used, int edge, Action<string>? log)
  {
    var wanted = new Dictionary<string, (string Channels, int Hint)>(StringComparer.OrdinalIgnoreCase);
    void Take(string? name, string ch, int hint) { if (!string.IsNullOrWhiteSpace(name) && !wanted.ContainsKey(name)) wanted[name] = (ch, hint); }
    foreach (var (g, m) in used)
      foreach (var p in m.Parts)
      {
        if (MaterialMaps.TrySolid(p.Texture ?? "", out var r, out var gg, out var b, out var a) && !string.IsNullOrWhiteSpace(p.Texture))
        { scene.Albedo.TryAdd(p.Texture!, new PreviewImage { Width = 1, Height = 1, Bgra = new[] { b, gg, r, a } }); }
        else Take(p.Texture, "", g.Hint);
        Take(p.Normal, "Normalmap(rgb)", g.Hint); Take(p.Specular, p.SpecularChannels, g.Hint);
      }
    var res = new System.Collections.Concurrent.ConcurrentDictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    var stamp = "v2"; var hits = 0;
    Parallel.ForEach(wanted, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, kv =>
    {
      var im = LoadTexture(catalog, store, kv.Value.Hint, kv.Key, kv.Value.Channels, edge, stamp, ref hits, out var ti);
      if (im is not null) { res[kv.Key] = im; lock (scene.TexIndex) scene.TexIndex[kv.Key] = ti; }
    });
    foreach (var (name, im) in res)
    {
      var ch = wanted[name].Channels;
      if (ch.Contains("Normal", StringComparison.OrdinalIgnoreCase)) scene.Normals[name] = im;
      else if (MainWindow.IsSpecularChannels(ch)) scene.Specular[name] = im;
      else scene.Albedo.TryAdd(name, im);
    }
    log?.Invoke($"textures: {wanted.Count} wanted, {res.Count} decoded, {hits} from disk cache");
  }

  static PreviewImage? LoadTexture(GameCatalog catalog, ArchiveStore store, int hint, string name, string channels, int edge, string stamp, ref int hits, out int index, int known = -1)
  {
    index = known >= 0 ? known : TemplateMeshReader.ResolveTexture(catalog, hint, name);
    if (index < 0) return null;
    var f = catalog.GetFile(index);
    var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes($"{f.Path}|{f.Size}|{Path.GetFileName(catalog.Archives[f.ArchiveId].PakPath)}")))[..20];
    var file = Path.Combine(TexDir, stamp, $"{hash}_{edge}.tex");
    try
    {
      if (File.Exists(file))
      {
        using var r = new BinaryReader(File.OpenRead(file));
        var w = r.ReadInt32(); var h = r.ReadInt32(); var n = r.ReadInt32();
        var px = r.ReadBytes(n);
        if (px.Length == n && n == w * h * 4) { Interlocked.Increment(ref hits); return new PreviewImage { Width = w, Height = h, Bgra = px, Channels = channels }; }
      }
    }
    catch (Exception) { }
    try
    {
      var doc = TextureLoader.Load(catalog, store, index);
      if (!doc.CanDecode) return null;
      var mip = 0;
      while (mip < doc.MipCount - 1 && Math.Max(doc.Width, doc.Height) >> mip > edge) mip++;
      var pixels = doc.Decode(mip, 0, out var width, out var height, out _);
      var im = Shrink(new PreviewImage { Width = width, Height = height, Bgra = pixels, Channels = channels }, edge);
      try
      {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tmp = file + "." + Environment.CurrentManagedThreadId + ".tmp";
        using (var w = new BinaryWriter(File.Create(tmp))) { w.Write(im.Width); w.Write(im.Height); w.Write(im.Bgra.Length); w.Write(im.Bgra); }
        File.Move(tmp, file, true);
      }
      catch (Exception) { }
      return im;
    }
    catch (Exception) { return null; }
  }

  // ---------- level pack (disk cache of the built level; local user cache, never shipped) ----------
  static readonly System.Text.Json.JsonSerializerOptions PartJson = new()
  {
    PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate,
    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
  };

  static void WriteList<T>(BinaryWriter w, List<T> l) where T : struct
  {
    w.Write(l.Count); w.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(l)));
  }
  static void ReadList<T>(BinaryReader r, List<T> l) where T : struct
  {
    var n = r.ReadInt32(); var a = new T[n];
    var span = System.Runtime.InteropServices.MemoryMarshal.AsBytes(a.AsSpan());
    if (r.Read(span) != span.Length) throw new EndOfStreamException();
    l.AddRange(a);
  }
  static void WriteMat(BinaryWriter w, Matrix4x4 m) { foreach (var v in LevelBenchFlat(m)) w.Write(v); }
  static Matrix4x4 ReadMat(BinaryReader r) { var f = new float[16]; for (var i = 0; i < 16; i++) f[i] = r.ReadSingle(); return new Matrix4x4(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]); }
  static float[] LevelBenchFlat(Matrix4x4 m) => new[] { m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44 };
  static void WriteCam(BinaryWriter w, OrbitCamera c) { w.Write(c.Yaw); w.Write(c.Pitch); w.Write(c.Distance); }
  static OrbitCamera ReadCam(BinaryReader r, Vector3 t) => new() { Yaw = r.ReadSingle(), Pitch = r.ReadSingle(), Distance = r.ReadSingle(), Target = t };
  static void WriteStrings(BinaryWriter w, IEnumerable<string> s) { var l = s.ToList(); w.Write(l.Count); foreach (var x in l) w.Write(x); }
  static List<string> ReadStrings(BinaryReader r) { var n = r.ReadInt32(); var l = new List<string>(n); for (var i = 0; i < n; i++) l.Add(r.ReadString()); return l; }

  static void WritePack(GameCatalog catalog, LevelScene s, string file, string key, int edge, List<Geo> all, List<(int Work, Geo G, int Level)> chosen, List<(string Tpl, int Index, Matrix4x4 World, bool Lwi)> work, Action<string>? log)
  {
    try
    {
      Directory.CreateDirectory(CacheDir);
      var tmp = file + ".tmp";
      // only the variants actually drawn
      var used = chosen.Select(c => (c.G, c.Level)).Distinct().ToList();
      var ids = new Dictionary<(Geo, int), int>(); for (var i = 0; i < used.Count; i++) ids[used[i]] = i;
      using (var w = new BinaryWriter(new BufferedStream(File.Create(tmp), 1 << 20)))
      {
        w.Write("FLLVL"); w.Write(key);
        w.Write(s.Stats); w.Write(s.Centre.X); w.Write(s.Centre.Y); w.Write(s.Centre.Z); w.Write(s.Spread);
        w.Write(s.LwiCount); w.Write(s.LwiPlaced); w.Write(s.BudgetSkipped); w.Write(s.HiddenParts); w.Write(s.WorkCount); w.Write(s.Triangles);
        WriteStrings(w, s.Missing); WriteStrings(w, s.Skipped); WriteStrings(w, s.Untextured);
        WriteCam(w, s.Top); WriteCam(w, s.Persp); WriteCam(w, s.Close);
        w.Write(used.Count);
        foreach (var (g, lv) in used)
        {
          var m = g.V[lv]!;
          w.Write(g.Tpl); w.Write(g.Hint);
          WriteList(w, m.Positions); WriteList(w, m.Normals); WriteList(w, m.Uvs); WriteList(w, m.Indices);
          w.Write(System.Text.Json.JsonSerializer.Serialize(m.Parts, PartJson));
        }
        w.Write(chosen.Count);
        foreach (var c in chosen) { var x = work[c.Work]; w.Write(ids[(c.G, c.Level)]); w.Write(x.Index); w.Write(x.Lwi); WriteMat(w, x.World); }
        WriteStrings(w, s.Albedo.Keys); WriteStrings(w, s.Normals.Keys); WriteStrings(w, s.Specular.Keys);
        w.Write(edge);
        foreach (var d in new[] { s.Albedo, s.Normals, s.Specular })
          foreach (var (name, im) in d)
            if (s.TexIndex.TryGetValue(name, out var ti)) { w.Write(1); w.Write(catalog.GetFile(ti).Path); w.Write(im.Channels ?? ""); }
            else { w.Write(-1); w.Write(im.Width); w.Write(im.Height); w.Write(im.Channels ?? ""); w.Write(im.Bgra.Length); w.Write(im.Bgra); }
        w.Write("END");
      }
      File.Move(tmp, file, true);
      log?.Invoke($"cache written {file} {new FileInfo(file).Length:N0} B");
    }
    catch (Exception e) { log?.Invoke("cache write failed: " + e.GetType().Name + " " + e.Message); }
  }

  static bool TryReadPack(LevelScene s, string file, string key, GameCatalog catalog, ArchiveStore store, Options o, Action<string>? log)
  {
    if (!File.Exists(file)) return false;
    try
    {
      using var r = new BinaryReader(new BufferedStream(File.OpenRead(file), 1 << 20));
      if (r.ReadString() != "FLLVL" || r.ReadString() != key) { log?.Invoke("cache stale: " + file); return false; }
      s.Stats = r.ReadString(); s.Centre = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); s.Spread = r.ReadSingle();
      s.LwiCount = r.ReadInt32(); s.LwiPlaced = r.ReadInt32(); s.BudgetSkipped = r.ReadInt32(); s.HiddenParts = r.ReadInt32(); s.WorkCount = r.ReadInt32(); s.Triangles = r.ReadInt64();
      foreach (var x in ReadStrings(r)) s.Missing.Add(x);
      foreach (var x in ReadStrings(r)) s.Skipped.Add(x);
      s.Untextured = ReadStrings(r);
      s.Top = ReadCam(r, s.Centre); s.Persp = ReadCam(r, s.Centre); s.Close = ReadCam(r, s.Centre);
      var n = r.ReadInt32(); var meshes = new ExportedMesh[n];
      for (var i = 0; i < n; i++)
      {
        var m = new ExportedMesh { Name = r.ReadString() }; r.ReadInt32();
        ReadList(r, m.Positions); ReadList(r, m.Normals); ReadList(r, m.Uvs); ReadList(r, m.Indices);
        m.Parts.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<MeshPart>>(r.ReadString(), PartJson) ?? new());
        meshes[i] = m;
      }
      var c = r.ReadInt32();
      for (var i = 0; i < c; i++)
      {
        var id = r.ReadInt32(); var idx = r.ReadInt32(); r.ReadBoolean(); var world = ReadMat(r);
        Append(s.Mesh, meshes[id], world, meshes[id].Name);
        s.Placed.Add((meshes[id].Name, idx, world));
      }
      var names = new[] { ReadStrings(r), ReadStrings(r), ReadStrings(r) };
      var edge = r.ReadInt32(); var refs = new List<(int D, string Name, int Ti, string Ch)>();
      var dicts = new[] { s.Albedo, s.Normals, s.Specular };
      for (var d = 0; d < 3; d++)
        foreach (var name in names[d])
        {
          var ti = r.ReadInt32();
          if (ti >= 0) { var tp = r.ReadString(); var ch0 = r.ReadString(); if (catalog.TryGetByPath(tp, out var tix)) refs.Add((d, name, tix, ch0)); continue; }
          var w = r.ReadInt32(); var h = r.ReadInt32(); var ch = r.ReadString(); var len = r.ReadInt32();
          dicts[d][name] = new PreviewImage { Width = w, Height = h, Channels = ch, Bgra = r.ReadBytes(len) };
        }
      if (r.ReadString() != "END") throw new InvalidDataException("pack truncated");
      // texture refs: shared texture cache (decoded from the game files again if the cache file is gone)
      var stamp = "v2"; var hits = 0;
      var got = new System.Collections.Concurrent.ConcurrentBag<(int D, string Name, int Ti, PreviewImage Im)>();
      Parallel.ForEach(refs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, x =>
      {
        var im = LoadTexture(catalog, store, -1, x.Name, x.Ch, edge, stamp, ref hits, out _, x.Ti);
        if (im is not null) got.Add((x.D, x.Name, x.Ti, im));
      });
      foreach (var x in got) { dicts[x.D][x.Name] = x.Im; s.TexIndex[x.Name] = x.Ti; }
      log?.Invoke($"pack textures: {refs.Count} refs, {hits} from texture cache");
      return true;
    }
    catch (Exception e)
    {
      log?.Invoke("cache read failed, rebuilding: " + e.GetType().Name + " " + e.Message);
      s.Mesh = new ExportedMesh { Name = "level" }; s.Placed.Clear(); s.Albedo.Clear(); s.Normals.Clear(); s.Specular.Clear(); s.Missing.Clear(); s.Skipped.Clear();
      return false;
    }
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
