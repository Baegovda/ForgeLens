// BMW M1: read a glTF binary (.glb) exported by tools/bmw (CUE4Parse) into the shared ExportedMesh, so Black Myth: Wukong
// meshes use the same preview as SM2 / PoE2. Material textures come from the CUE4Parse material .json next to the export
// (PM_Diffuse / PM_Normals keys). Texture paths are returned as absolute .png paths; the caller decodes them.
using System.Numerics;
using System.Text.Json;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Games.Bmw;

public static class BmwGlb
{
  /// <summary>Loads <paramref name="glb"/>. <paramref name="exportRoot"/> is the export folder (searched for material .json and .png).</summary>
  public static ExportedMesh Load(string glb, string exportRoot)
  {
    var b = File.ReadAllBytes(glb);
    if (BitConverter.ToUInt32(b, 0) != 0x46546C67) throw new InvalidDataException("not a .glb");
    var jsonLen = BitConverter.ToInt32(b, 12);
    using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(b, 20, jsonLen));
    var j = doc.RootElement;
    var binStart = 20 + jsonLen + 8;
    var views = j.GetProperty("bufferViews").EnumerateArray().ToList();
    var accs = j.GetProperty("accessors").EnumerateArray().ToList();

    int Comp(string t) => t switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => 1 };
    float[] Read(int ai)
    {
      var a = accs[ai];
      var v = views[a.GetProperty("bufferView").GetInt32()];
      var off = binStart + (v.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
      var n = a.GetProperty("count").GetInt32();
      var c = Comp(a.GetProperty("type").GetString()!);
      var ct = a.GetProperty("componentType").GetInt32();
      var size = ct switch { 5126 or 5125 => 4, 5123 or 5122 => 2, _ => 1 };
      var stride = v.TryGetProperty("byteStride", out var st) ? st.GetInt32() : size * c;
      var norm = a.TryGetProperty("normalized", out var nm) && nm.GetBoolean();
      var r = new float[n * c];
      for (var i = 0; i < n; i++)
        for (var k = 0; k < c; k++)
        {
          var p = off + i * stride + k * size;
          r[i * c + k] = ct switch
          {
            5126 => BitConverter.ToSingle(b, p),
            5125 => BitConverter.ToUInt32(b, p),
            5123 => norm ? BitConverter.ToUInt16(b, p) / 65535f : BitConverter.ToUInt16(b, p),
            5122 => BitConverter.ToInt16(b, p),
            5121 => norm ? b[p] / 255f : b[p],
            _ => (sbyte)b[p],
          };
        }
      return r;
    }

    var mesh = new ExportedMesh { Name = Path.GetFileNameWithoutExtension(glb) };
    var matNames = j.TryGetProperty("materials", out var mats) ? mats.EnumerateArray().Select(m => m.TryGetProperty("name", out var nn) ? nn.GetString() ?? "" : "").ToList() : new List<string>();
    var jsonIndex = Directory.Exists(exportRoot)
      ? Directory.EnumerateFiles(exportRoot, "*.json", SearchOption.AllDirectories).GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase)
      : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    string? Tex(string mat, string key)
    {
      if (!jsonIndex.TryGetValue(mat, out var jf)) return null;
      using var md = JsonDocument.Parse(File.ReadAllText(jf));
      if (!md.RootElement.TryGetProperty("Textures", out var t) || !t.TryGetProperty(key, out var p)) return null;
      var obj = p.GetString() ?? "";
      var rel = obj.Contains('.') ? obj[..obj.LastIndexOf('.')] : obj;
      var png = Path.Combine(exportRoot, rel.Replace('/', Path.DirectorySeparatorChar) + ".png");
      return File.Exists(png) ? png : null;
    }

    var nodes = j.GetProperty("nodes").EnumerateArray().ToList();
    var meshNode = 0;
    for (var i = 0; i < nodes.Count; i++) if (nodes[i].TryGetProperty("mesh", out _)) { meshNode = i; break; }
    var m0 = j.GetProperty("meshes")[nodes[meshNode].GetProperty("mesh").GetInt32()];
    foreach (var prim in m0.GetProperty("primitives").EnumerateArray())
    {
      var at = prim.GetProperty("attributes");
      var baseV = mesh.Positions.Count;
      var pos = Read(at.GetProperty("POSITION").GetInt32());
      var nor = at.TryGetProperty("NORMAL", out var na) ? Read(na.GetInt32()) : null;
      var uv = at.TryGetProperty("TEXCOORD_0", out var ua) ? Read(ua.GetInt32()) : null;
      var jo = at.TryGetProperty("JOINTS_0", out var ja) ? Read(ja.GetInt32()) : null;
      var we = at.TryGetProperty("WEIGHTS_0", out var wa) ? Read(wa.GetInt32()) : null;
      var n = pos.Length / 3;
      for (var i = 0; i < n; i++)
      {
        mesh.Positions.Add(new Vector3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]));
        mesh.Normals.Add(nor != null ? new Vector3(nor[i * 3], nor[i * 3 + 1], nor[i * 3 + 2]) : Vector3.UnitY);
        mesh.Uvs.Add(uv != null ? new Vector2(uv[i * 2], uv[i * 2 + 1]) : Vector2.Zero);
        if (jo != null && we != null)
          mesh.Skin.Add(new SkinWeights { J0 = (ushort)jo[i * 4], J1 = (ushort)jo[i * 4 + 1], J2 = (ushort)jo[i * 4 + 2], J3 = (ushort)jo[i * 4 + 3], W0 = we[i * 4], W1 = we[i * 4 + 1], W2 = we[i * 4 + 2], W3 = we[i * 4 + 3] });
      }
      var start = mesh.Indices.Count;
      if (prim.TryGetProperty("indices", out var ia)) foreach (var x in Read(ia.GetInt32())) mesh.Indices.Add(baseV + (int)x);
      else for (var i = 0; i < n; i++) mesh.Indices.Add(baseV + i);
      var mi = prim.TryGetProperty("material", out var me) ? me.GetInt32() : -1;
      var mn = mi >= 0 && mi < matNames.Count ? matNames[mi] : "part";
      mesh.Parts.Add(new MeshPart { Name = mn, IndexStart = start, IndexCount = mesh.Indices.Count - start, Texture = Tex(mn, "PM_Diffuse"), Normal = Tex(mn, "PM_Normals") });
    }

    if (j.TryGetProperty("skins", out var skins) && skins.GetArrayLength() > 0)
    {
      var skin = skins[0];
      var joints = skin.GetProperty("joints").EnumerateArray().Select(x => x.GetInt32()).ToList();
      var ibm = skin.TryGetProperty("inverseBindMatrices", out var ib) ? Read(ib.GetInt32()) : null;
      var parentOf = new Dictionary<int, int>();
      for (var i = 0; i < nodes.Count; i++)
        if (nodes[i].TryGetProperty("children", out var ch)) foreach (var c in ch.EnumerateArray()) parentOf[c.GetInt32()] = i;
      var boneOf = joints.Select((nd, i) => (nd, i)).ToDictionary(t => t.nd, t => t.i);
      for (var i = 0; i < joints.Count; i++)
      {
        var nd = nodes[joints[i]];
        var t = nd.TryGetProperty("translation", out var tr) ? new Vector3(tr[0].GetSingle(), tr[1].GetSingle(), tr[2].GetSingle()) : Vector3.Zero;
        var r = nd.TryGetProperty("rotation", out var ro) ? new Quaternion(ro[0].GetSingle(), ro[1].GetSingle(), ro[2].GetSingle(), ro[3].GetSingle()) : Quaternion.Identity;
        var s = nd.TryGetProperty("scale", out var sc) ? new Vector3(sc[0].GetSingle(), sc[1].GetSingle(), sc[2].GetSingle()) : Vector3.One;
        var parent = -1;
        var p = joints[i];
        while (parentOf.TryGetValue(p, out var pp)) { if (boneOf.TryGetValue(pp, out var pb)) { parent = pb; break; } p = pp; }
        var bone = new Bone { Name = nd.TryGetProperty("name", out var bn) ? bn.GetString() ?? "bone" : "bone", Parent = parent, Local = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t), FileBind = true, Influences = true };
        if (ibm != null)
        {
          var o = i * 16; // glTF column-major == System.Numerics row-vector layout read in order
          bone.InverseBind = new Matrix4x4(ibm[o], ibm[o + 1], ibm[o + 2], ibm[o + 3], ibm[o + 4], ibm[o + 5], ibm[o + 6], ibm[o + 7], ibm[o + 8], ibm[o + 9], ibm[o + 10], ibm[o + 11], ibm[o + 12], ibm[o + 13], ibm[o + 14], ibm[o + 15]);
        }
        mesh.Bones.Add(bone);
      }
    }
    var tex = mesh.Parts.Count(p => p.Texture != null);
    mesh.TexturesNamed = mesh.Parts.Count;
    mesh.TexturesResolved = tex;
    mesh.Summary = $"Black Myth: Wukong {mesh.Name}: {mesh.Positions.Count} vertices, {mesh.Indices.Count / 3} triangles, {mesh.Parts.Count} parts ({tex} with albedo), {mesh.Bones.Count} bones";
    mesh.Message = mesh.Summary;
    return mesh;
  }
}
