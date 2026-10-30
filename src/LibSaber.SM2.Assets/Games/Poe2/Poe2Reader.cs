using System.Numerics;
using System.Text.Json;
using BCnEncoder.Decoder;
using BCnEncoder.Shared.ImageFiles;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Games.Poe2;

/// <summary>
///   PoE2 M1 reader: builds the same <see cref="ExportedMesh"/> / <see cref="AnimationClip"/> the SM2 pipeline uses, so the
///   viewer, glTF and FBX writers are shared. Input is a "prepared folder": the poe_data_tools <c>translate</c> JSON of
///   .sm/.smd/.fmt/.ast/.mat (<c>json/</c>), the raw files for .dds (<c>raw/</c>), and for .ast version 8+ the decompressed
///   animation bundle written by <c>ast_bundle.exe</c> next to the raw .ast as <c>&lt;file&gt;.ast.anim.bin</c>.
///   Space: PoE stores centimetres with -Z up (BasicSkeleton1 bbox z -195.6..0.18). ForgeLens internal space is metres, Y-up:
///   (x, y, z) -> (x, -z, -y) * 0.01. M2 orientation fix (INFERRED from bone names, no in-game check): BasicSkeleton toes point
///   to PoE -Y (forward) and L_* bones sit at +X, which is a left-handed frame (D3D style); this map turns PoE -Y into ForgeLens +Z
///   front and keeps L_* on the character's left, so it is a reflection (det -1) and every triangle's winding is flipped.
/// </summary>
public sealed partial class Poe2Reader
{
  public string JsonRoot { get; }
  public string RawRoot { get; }
  public List<string> Log { get; } = new();

  public Poe2Reader(string prepared)
  {
    JsonRoot = Path.Combine(prepared, "json");
    RawRoot = Path.Combine(prepared, "raw");
  }

  private static readonly Matrix4x4 C = new(1, 0, 0, 0, 0, 0, -1, 0, 0, -1, 0, 0, 0, 0, 0, 1);
  private static readonly Matrix4x4 CInv = Matrix4x4.Transpose(C);
  public const float Scale = 0.01f;

  public static Vector3 P(float x, float y, float z) => new Vector3(x, -z, -y) * Scale;
  public static Vector3 D(float x, float y, float z) => new(x, -z, -y);

  public static Matrix4x4 M(Matrix4x4 m)
  {
    var r = CInv * m * C;
    r.Translation *= Scale;
    return r;
  }

  public static Quaternion Q(float x, float y, float z, float w)
  {
    var m = CInv * Matrix4x4.CreateFromQuaternion(new Quaternion(x, y, z, w)) * C;
    return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
  }

  internal JsonDocument Json(string gamePath)
  {
    var p = Path.Combine(JsonRoot, gamePath.ToLowerInvariant().Replace('/', Path.DirectorySeparatorChar) + ".json");
    return JsonDocument.Parse(File.ReadAllBytes(p));
  }

  public string RawPath(string gamePath) => Path.Combine(RawRoot, gamePath.ToLowerInvariant().Replace('/', Path.DirectorySeparatorChar));

  /// <summary>Open a .sm (skinned, smd geometry) or .fmt (fixed mesh). <paramref name="ast"/> adds the skeleton and, with <paramref name="clip"/>, one animation.</summary>
  public ExportedMesh Load(string meshPath, string? ast = null, string? clip = null)
  {
    var ext = Path.GetExtension(meshPath).ToLowerInvariant();
    var mesh = new ExportedMesh { Name = Path.GetFileNameWithoutExtension(meshPath) };
    if (ext == ".fmt")
    {
      using var doc = Json(meshPath);
      var root = doc.RootElement;
      var section = root.GetProperty("section").EnumerateObject().First().Value;
      var lods = section.GetProperty("lods");
      var lod = lods[0];
      ReadVertices(mesh, lod, false);
      var tris = ReadIndices(mesh, lod);
      for (var l = 1; l < lods.GetArrayLength(); l++)
        mesh.Levels.Add(ReadLod(lods[l], l, new List<string> { "lod" }, new List<string?>(), false));
      var shapes = root.GetProperty("shapes").EnumerateArray().ToList();
      for (var i = 0; i < shapes.Count; i++)
      {
        var start = shapes[i].GetProperty("triangle_start").GetInt32() * 3;
        var end = i + 1 < shapes.Count ? shapes[i + 1].GetProperty("triangle_start").GetInt32() * 3 : tris;
        var part = new MeshPart { Name = shapes[i].GetProperty("name").GetString() ?? "shape", IndexStart = start, IndexCount = end - start };
        ApplyMaterial(part, shapes[i].GetProperty("material").GetString());
        mesh.Parts.Add(part);
      }
    }
    else if (ext == ".sm")
    {
      using var sm = Json(meshPath);
      var smd = sm.RootElement.GetProperty("smd_file").GetString()!;
      var mats = sm.RootElement.GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("mat_file").GetString()).ToList();
      using var smdDoc = Json(smd);
      var v = smdDoc.RootElement.GetProperty("section").EnumerateObject().First().Value;
      var dolm = v.GetProperty("dolm");
      var names = v.TryGetProperty("shape_names", out var sn) ? sn.EnumerateArray().Select(x => x.GetString() ?? "shape").ToList() : new List<string>();
      var lods = dolm.GetProperty("lods");
      var lod = lods[0];
      ReadVertices(mesh, lod, true);
      ReadIndices(mesh, lod);
      for (var l = 1; l < lods.GetArrayLength(); l++)
        mesh.Levels.Add(ReadLod(lods[l], l, names, mats, true));
      var i = 0;
      foreach (var ext2 in lod.GetProperty("shape_extents").EnumerateArray())
      {
        var part = new MeshPart
        {
          Name = i < names.Count ? names[i] : $"shape{i}",
          IndexStart = ext2.GetProperty("start_index").GetInt32(),
          IndexCount = ext2.GetProperty("count_index").GetInt32(),
        };
        ApplyMaterial(part, i < mats.Count ? mats[i] : mats.LastOrDefault());
        mesh.Parts.Add(part);
        i++;
      }
    }
    else
      throw new NotSupportedException($"PoE2 mesh type {ext}");

    if (ast != null)
      ReadSkeleton(mesh, ast, clip);
    Log.Add($"{meshPath}: {mesh.Positions.Count} verts, {mesh.Indices.Count / 3} tris, {mesh.Parts.Count} parts, LODs {1 + mesh.Levels.Count} ({string.Join("/", new[] { mesh.Indices.Count / 3 }.Concat(mesh.Levels.Select(l => l.Indices.Count / 3)))} tris), {mesh.Bones.Count} bones, clip {mesh.Clip?.Name ?? "-"} {mesh.Clip?.Duration:0.00}s");
    return mesh;
  }

  private static void ReadVertices(ExportedMesh mesh, JsonElement lod, bool skinned)
  {
    foreach (var vx in lod.GetProperty("vertices").EnumerateArray())
    {
      var p = vx.GetProperty("pos");
      mesh.Positions.Add(P(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()));
      var n = vx.GetProperty("normal");
      mesh.Normals.Add(Vector3.Normalize(D(n[0].GetSingle() / 127f, n[1].GetSingle() / 127f, n[2].GetSingle() / 127f) + new Vector3(1e-6f)));
      var uv = vx.GetProperty("tex_coord0");
      var u = uv[0].GetSingle(); var w = uv[1].GetSingle();
      // smd stores UVs in -1..0 (PROVEN range on BasicSkeleton1); +1 is the same texel under wrap sampling.
      if (u < 0) u += 1; if (w < 0) w += 1;
      mesh.Uvs.Add(new Vector2(u, w));
      if (skinned && vx.TryGetProperty("skin_bones", out var sb) && sb.ValueKind == JsonValueKind.Array)
      {
        var sw = vx.GetProperty("skin_weights");
        mesh.Skin.Add(new SkinWeights
        {
          J0 = (ushort)sb[0].GetInt32(), J1 = (ushort)sb[1].GetInt32(), J2 = (ushort)sb[2].GetInt32(), J3 = (ushort)sb[3].GetInt32(),
          W0 = sw[0].GetInt32() / 255f, W1 = sw[1].GetInt32() / 255f, W2 = sw[2].GetInt32() / 255f, W3 = sw[3].GetInt32() / 255f,
        });
      }
    }
  }

  /// <summary>Lower LOD (lods[1..]) into a <see cref="MeshLod"/>; parts from its shape_extents when present, else one part.</summary>
  private MeshLod ReadLod(JsonElement lodEl, int level, List<string> names, List<string?> mats, bool skinned)
  {
    var tmp = new ExportedMesh();
    ReadVertices(tmp, lodEl, skinned);
    ReadIndices(tmp, lodEl);
    var lod = new MeshLod { Name = $"LOD{level}" };
    lod.Positions.AddRange(tmp.Positions); lod.Normals.AddRange(tmp.Normals); lod.Uvs.AddRange(tmp.Uvs);
    lod.Indices.AddRange(tmp.Indices); lod.Skin.AddRange(tmp.Skin);
    var i = 0;
    if (lodEl.TryGetProperty("shape_extents", out var se) && se.ValueKind == JsonValueKind.Array)
      foreach (var e in se.EnumerateArray())
      {
        var part = new MeshPart { Name = (i < names.Count ? names[i] : $"shape{i}") + $"_LOD{level}", IndexStart = e.GetProperty("start_index").GetInt32(), IndexCount = e.GetProperty("count_index").GetInt32(), Visible = false, HideReason = "LOD" };
        ApplyMaterial(part, i < mats.Count ? mats[i] : mats.LastOrDefault());
        lod.Parts.Add(part); i++;
      }
    if (lod.Parts.Count == 0)
      lod.Parts.Add(new MeshPart { Name = $"LOD{level}", IndexStart = 0, IndexCount = lod.Indices.Count, Visible = false, HideReason = "LOD" });
    return lod;
  }

  private static int ReadIndices(ExportedMesh mesh, JsonElement lod)
  {
    var idx = lod.GetProperty("indices").EnumerateObject().First().Value;
    var list = idx.EnumerateArray().Select(i => i.GetInt32()).ToList();
    for (var t = 0; t + 2 < list.Count; t += 3)
    {
      // Winding flip: the space map is a reflection.
      mesh.Indices.Add(list[t]); mesh.Indices.Add(list[t + 2]); mesh.Indices.Add(list[t + 1]);
    }
    return mesh.Indices.Count;
  }

  private void ApplyMaterial(MeshPart part, string? mat)
  {
    if (string.IsNullOrEmpty(mat))
      return;
    part.Variant = mat;
    try
    {
      using var doc = Json(mat);
      foreach (var gi in doc.RootElement.GetProperty("graphinstances").EnumerateArray())
      {
        if (!gi.TryGetProperty("custom_parameters", out var cps)) continue;
        foreach (var cp in cps.EnumerateArray())
        {
          var name = cp.GetProperty("name").GetString() ?? "";
          var path = cp.GetProperty("parameters").EnumerateArray()
            .Select(p => p.TryGetProperty("path", out var pp) ? pp.GetString() : null).FirstOrDefault(s => s != null);
          if (path == null) continue;
          if (part.Texture == null && (name.Contains("Albedo", StringComparison.OrdinalIgnoreCase) || name.Contains("Colour", StringComparison.OrdinalIgnoreCase) || name.Contains("Diffuse", StringComparison.OrdinalIgnoreCase)))
            part.Texture = path;
          else if (part.Normal == null && name.Contains("Normal", StringComparison.OrdinalIgnoreCase))
            part.Normal = path;
          else if (part.Emissive == null && (name.Contains("Glow", StringComparison.OrdinalIgnoreCase) || name.Contains("Emissive", StringComparison.OrdinalIgnoreCase)))
            part.Emissive = path;
          part.Maps.Add(new MaterialMap { Role = name, Name = path });
        }
      }
    }
    catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException)
    {
      Log.Add($"mat {mat}: {e.Message}");
    }
  }

  internal void ReadSkeleton(ExportedMesh mesh, string ast, string? clipName)
  {
    using var doc = Json(ast);
    var root = doc.RootElement;
    var bones = root.GetProperty("bones").EnumerateArray().ToList();
    var parents = new int[bones.Count];
    Array.Fill(parents, -1);
    void Walk(int? i, int p)
    {
      var guard = 0;
      while (i is int k && guard++ < 1024)
      {
        parents[k] = p;
        var c = bones[k].GetProperty("child");
        if (c.ValueKind == JsonValueKind.Number) Walk(c.GetInt32(), k);
        var s = bones[k].GetProperty("sibling");
        i = s.ValueKind == JsonValueKind.Number ? s.GetInt32() : null;
      }
    }
    Walk(0, -1);
    var worlds = new Matrix4x4[bones.Count];
    for (var i = 0; i < bones.Count; i++)
    {
      var t = bones[i].GetProperty("transform");
      var m = new Matrix4x4(
        t[0][0].GetSingle(), t[0][1].GetSingle(), t[0][2].GetSingle(), t[0][3].GetSingle(),
        t[1][0].GetSingle(), t[1][1].GetSingle(), t[1][2].GetSingle(), t[1][3].GetSingle(),
        t[2][0].GetSingle(), t[2][1].GetSingle(), t[2][2].GetSingle(), t[2][3].GetSingle(),
        t[3][0].GetSingle(), t[3][1].GetSingle(), t[3][2].GetSingle(), t[3][3].GetSingle());
      var local = M(m);
      mesh.Bones.Add(new Bone { Name = bones[i].GetProperty("name").GetString() ?? $"bone{i}", Parent = parents[i], Local = local, FileBind = true, Influences = true });
    }
    for (var i = 0; i < bones.Count; i++)
    {
      var p = parents[i];
      worlds[i] = p < 0 ? mesh.Bones[i].Local : mesh.Bones[i].Local * worlds[p];
      Matrix4x4.Invert(worlds[i], out var inv);
      mesh.Bones[i].InverseBind = inv;
    }

    var anims = root.GetProperty("animations").EnumerateArray().ToList();
    Log.Add($"{ast}: {bones.Count} bones, {anims.Count} animations");
    if (clipName == null || anims.Count == 0)
      return;
    if (clipName == "?")
      clipName = anims.Select(a => a.GetProperty("name").GetString() ?? "").FirstOrDefault(n => n.Contains("idle", StringComparison.OrdinalIgnoreCase))
        ?? anims.Select(a => a.GetProperty("name").GetString() ?? "").FirstOrDefault(n => n.Contains("run", StringComparison.OrdinalIgnoreCase))
        ?? anims[0].GetProperty("name").GetString();
    var anim = anims.FirstOrDefault(a => string.Equals(a.GetProperty("name").GetString(), clipName, StringComparison.OrdinalIgnoreCase));
    if (anim.ValueKind == JsonValueKind.Undefined) anim = anims[0];
    var bin = RawPath(ast) + ".anim.bin";
    if (!anim.TryGetProperty("data_location", out var loc) || loc.ValueKind != JsonValueKind.Object || !File.Exists(bin))
    {
      Log.Add($"clip {clipName}: no data_location or {bin} missing (run ast_bundle.exe)");
      return;
    }
    var all = File.ReadAllBytes(bin);
    var data = all.AsSpan(loc.GetProperty("offset").GetInt32(), loc.GetProperty("length").GetInt32());
    var version = root.GetProperty("header").GetProperty("version").GetInt32();
    var fps = Math.Max(1, anim.GetProperty("framerate").GetInt32());
    var clip = DecodeTracks(data, anim.GetProperty("num_tracks").GetInt32(), version, fps, mesh.Bones);
    clip.Name = anim.GetProperty("name").GetString() ?? "clip";
    mesh.Clip = clip;
  }

  /// <summary>Track layout from poe_data_tools ast parser (v10+ header has unk5); keys are (frame, value...), rotation x,y,z,w.</summary>
  public static AnimationClip DecodeTracks(ReadOnlySpan<byte> b, int tracks, int version, int fps, IReadOnlyList<Bone> bones)
  {
    var clip = new AnimationClip { SampleRate = fps, RootMotion = false };
    var p = 0;
    float F(ReadOnlySpan<byte> s, int o) => BitConverter.ToSingle(s.Slice(o, 4));
    uint U(ReadOnlySpan<byte> s, int o) => BitConverter.ToUInt32(s.Slice(o, 4));
    float last = 0;
    for (var t = 0; t < tracks; t++)
    {
      p += 1;
      var index = (int)U(b, p); p += 4;
      var n = new int[6];
      for (var k = 0; k < 6; k++) { n[k] = (int)U(b, p); p += 4; }
      if (version >= 10) p += 4;
      var track = new BoneTrack { Bone = index < bones.Count ? index : -1, BoneName = index < bones.Count ? bones[index].Name : "" };
      for (var k = 0; k < n[0]; k++, p += 16)
        track.Scale.Add(new VectorKey { Time = F(b, p) / fps, Value = new Vector3(F(b, p + 4), F(b, p + 12), F(b, p + 8)) });
      for (var k = 0; k < n[1]; k++, p += 20)
        track.Rotation.Add(new QuatKey { Time = F(b, p) / fps, Value = Q(F(b, p + 4), F(b, p + 8), F(b, p + 12), F(b, p + 16)) });
      for (var k = 0; k < n[2]; k++, p += 16)
        track.Translation.Add(new VectorKey { Time = F(b, p) / fps, Value = P(F(b, p + 4), F(b, p + 8), F(b, p + 12)) });
      p += n[3] * 16 + n[4] * 20 + n[5] * 16;
      foreach (var k in track.Rotation) last = Math.Max(last, k.Time);
      foreach (var k in track.Translation) last = Math.Max(last, k.Time);
      clip.Tracks.Add(track);
      clip.Bones.Add(track.BoneName);
    }
    clip.Duration = last;
    if (p != b.Length) clip.Notes.Add($"decoded {p} of {b.Length} bytes");
    return clip;
  }

  /// <summary>Decode a raw .dds (DX9 DXT1/5 or DX10 BC1..7) to BGRA mip 0.</summary>
  public PreviewImage? Texture(string gamePath)
  {
    var path = RawPath(gamePath);
    if (!File.Exists(path)) { Log.Add($"tex missing {gamePath}"); return null; }
    try
    {
      using var s = File.OpenRead(path);
      var dds = DdsFile.Load(s);
      var px = new BcDecoder().Decode(dds);
      int w = (int)dds.header.dwWidth, h = (int)dds.header.dwHeight;
      var bgra = new byte[w * h * 4];
      for (var i = 0; i < w * h; i++) { bgra[i * 4] = px[i].b; bgra[i * 4 + 1] = px[i].g; bgra[i * 4 + 2] = px[i].r; bgra[i * 4 + 3] = px[i].a; }
      return new PreviewImage { Width = w, Height = h, Bgra = bgra };
    }
    catch (Exception e)
    {
      Log.Add($"tex {gamePath}: {e.GetType().Name} {e.Message}");
      return null;
    }
  }

  /// <summary>List animation names of an .ast (for the viewer clip list).</summary>
  public List<string> Animations(string ast)
  {
    using var doc = Json(ast);
    return doc.RootElement.GetProperty("animations").EnumerateArray().Select(a => a.GetProperty("name").GetString() ?? "").ToList();
  }
}
