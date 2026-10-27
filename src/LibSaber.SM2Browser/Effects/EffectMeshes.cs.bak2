using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Effects;

/// <summary>Material look of a mesh-particle texture, read from its .td <c>materials.default</c> block.</summary>
internal sealed record EffectMeshMaterial(string Texture, bool Translucent, Vector4 Tint, float Clip, LibSaber.SM2.Assets.Effects.VatParams? Vat = null)
{
  /// <summary><c>softFreshnel.power</c> when enabled (0 = off). HEURISTIC use: alpha *= |n·v|^power (soft silhouettes).</summary>
  public float SoftFresnel { get; init; }
}

/// <summary>A geometry template flattened for the effect pass: pos(3) normal(3) uv(2) uv1(2) per vertex; sub-objects by node order.</summary>
internal sealed class EffectMesh
{
  public static readonly EffectMesh Missing = new() { Name = "", Vertices = [], Indices = [] };
  public required string Name { get; init; }
  public required float[] Vertices { get; init; }
  public required uint[] Indices { get; init; }
  /// <summary>typeInst index -> index ranges with their material.</summary>
  public List<List<(int Start, int Count, EffectMeshMaterial Material)>> Variants { get; } = [];
  public bool IsMissing => Indices.Length == 0;
}

/// <summary>Loads <c>nameGeomTpl</c> templates (and their .td materials) on worker threads, like <see cref="EffectTextures"/>.</summary>
internal sealed class EffectMeshes
{
  private readonly ConcurrentDictionary<string, Task<EffectMesh>> _cache = new(StringComparer.OrdinalIgnoreCase);
  private readonly Func<string, ExportedMesh?> _load;
  private readonly Func<string, string?> _td;
  public Action? Loaded { get; set; }

  public EffectMeshes(Func<string, ExportedMesh?> load, Func<string, string?> td)
  {
    _load = load;
    _td = td;
  }

  public static readonly EffectMeshes None = new(_ => null, _ => null);

  /// <summary>Loose layout: <c>meshDir/tpl/NAME.tpl/NAME.tpl(+_data)</c>; materials at <c>texDir/td/NAME.td</c>.</summary>
  public static EffectMeshes FromDirectory(string meshDir, string texDir)
  {
    var tpls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (Directory.Exists(meshDir))
      foreach (var f in Directory.EnumerateFiles(meshDir, "*.tpl", SearchOption.AllDirectories))
        tpls.TryAdd(Path.GetFileNameWithoutExtension(f), f);
    var tds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (Directory.Exists(texDir))
      foreach (var f in Directory.EnumerateFiles(texDir, "*.td", SearchOption.AllDirectories))
        tds.TryAdd(Path.GetFileNameWithoutExtension(f), f);
    return new EffectMeshes(
      name => tpls.TryGetValue(name, out var p)
        ? TemplateMeshReader.LoadRaw(File.ReadAllBytes(p), File.Exists(p + "_data") ? File.ReadAllBytes(p + "_data") : null, name)
        : null,
      name => tds.TryGetValue(name, out var p) ? Encoding.UTF8.GetString(File.ReadAllBytes(p)) : null);
  }

  /// <summary>Null while loading; <see cref="EffectMesh.Missing"/> when the template is not available.</summary>
  public EffectMesh? Get(string name)
  {
    var task = _cache.GetOrAdd(name, n => Task.Run(() =>
    {
      EffectMesh mesh;
      try { mesh = Build(n); }
      catch { mesh = EffectMesh.Missing; }
      Loaded?.Invoke();
      return mesh;
    }));
    return task.IsCompleted ? task.Result : null;
  }

  public void Prefetch(IEnumerable<string?> names)
  {
    foreach (var n in names)
      if (!string.IsNullOrEmpty(n)) Get(n);
  }

  public void WaitAll(TimeSpan timeout) => Task.WaitAll(_cache.Values.ToArray(), timeout);

  private EffectMesh Build(string name)
  {
    if (_load(name) is not { HasTriangles: true } m) return EffectMesh.Missing;
    var v = new float[m.Positions.Count * 10];
    for (var i = 0; i < m.Positions.Count; i++)
    {
      var p = m.Positions[i];
      var n = i < m.Normals.Count ? m.Normals[i] : Vector3.UnitY;
      var uv = i < m.Uvs.Count ? m.Uvs[i] : Vector2.Zero;
      var uv1 = i < m.Uvs1.Count ? m.Uvs1[i] : Vector2.Zero;
      var o = i * 10;
      v[o] = p.X; v[o + 1] = p.Y; v[o + 2] = p.Z;
      v[o + 3] = n.X; v[o + 4] = n.Y; v[o + 5] = n.Z;
      v[o + 6] = uv.X; v[o + 7] = uv.Y;
      v[o + 8] = uv1.X; v[o + 9] = uv1.Y;
    }

    var mesh = new EffectMesh { Name = name, Vertices = v, Indices = m.Indices.Select(i => (uint)i).ToArray() };
    var materials = new Dictionary<string, EffectMeshMaterial>(StringComparer.OrdinalIgnoreCase);
    foreach (var node in m.Parts.GroupBy(p => p.Node).OrderBy(g => g.Key))
    {
      var list = new List<(int, int, EffectMeshMaterial)>();
      foreach (var part in node)
      {
        var tex = part.Texture ?? "";
        if (tex.StartsWith("__default", StringComparison.Ordinal) || part.IndexCount < 3) continue;
        var key = (LibSaber.SM2.Assets.Effects.VatDecode.Enabled ? tex + "|" + part.Name : tex) + "|" + part.Variant;
        if (!materials.TryGetValue(key, out var mat)) materials[key] = mat = Material(tex, _td(tex), LibSaber.SM2.Assets.Effects.VatDecode.Enabled ? part.Name : null, part.Variant);
        list.Add((part.IndexStart, part.IndexCount, mat));
      }

      mesh.Variants.Add(list);
    }

    return mesh;
  }

  /// <summary><c>preset = "sfx"</c> materials are translucent (premultiplied) with <c>tint [A,R,G,B]</c> and <c>clipping.level</c>;
  /// everything else (combine_material, ...) draws opaque and lit.</summary>
  /// <summary>SM2_VFX_MESH_MTL=0: ignore the tpl's <c>shadingMtl_Mtl</c> (vfx3w) and always use <c>materials.default</c>.</summary>
  public static bool NamedMaterials { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_MESH_MTL") != "0";

  public static EffectMeshMaterial Material(string texture, string? td, string? partName = null, string? variant = null)
  {
    if (td is null) return new EffectMeshMaterial(texture, false, Vector4.One, 0f);
    var start = td.IndexOf("materials", StringComparison.Ordinal);
    var body = start >= 0 ? td[start..] : td;
    var named = false;
    // vfx3w: the tpl names its material (shadingMtl_Mtl, e.g. sfx_geom_muzzle -> "mzl_geom" inside water_drops_01.td);
    // that block, not materials.default, holds the shader (preset sfx, tint, emissive, its own tex = "...").
    if (NamedMaterials && variant is { Length: > 0 } vn && vn != "default" &&
        Regex.Match(body, @"(?m)^\s*" + Regex.Escape(vn) + @"\s*=\s*\{") is { Success: true } nm)
    { body = body[nm.Index..]; named = true; }
    if (LibSaber.SM2.Assets.Effects.VatDecode.MaterialBlockFor(td, partName) is { } own) body = own;
    var next = body.IndexOf("__type_id = \"material_templ\"", StringComparison.Ordinal);
    if (next > 0) body = body[..next];
    var sfx = Regex.IsMatch(body, "preset\\s*=\\s*\"sfx\"");
    if (named && sfx && Regex.Match(body, "(?m)^\\s*tex\\s*=\\s*\"([^\"]+)\"") is { Success: true } own2) texture = own2.Groups[1].Value;
    // Shader-driven materials whose texture is only a placeholder (solid_blood decodes to flat blue, flesh_material is a
    // flat normal-map default): the blood-splash shader colours the mesh from the sfxMtlOverrides colour, so draw white
    // and let the override tint it; "flesh" game materials get an assumed dark flesh red (not REA-confirmed).
    if (Regex.IsMatch(body, "shaderType\\s*=\\s*\"shader_blood_splash\""))
      return new EffectMeshMaterial("", false, Vector4.One, 0f, LibSaber.SM2.Assets.Effects.VatDecode.Parse(body));
    if (!sfx && !Regex.IsMatch(body, "preset\\s*=") && Regex.IsMatch(body, "game_material\\s*=\\s*\"flesh\""))
      return new EffectMeshMaterial("", false, new Vector4(0.42f, 0.09f, 0.08f, 1f), 0f);
    var tint = Vector4.One;
    var t = Regex.Match(body, "tint\\s*=\\s*\\[\\s*([\\d.]+)\\s*,\\s*([\\d.]+)\\s*,\\s*([\\d.]+)\\s*,\\s*([\\d.]+)");
    if (t.Success)
    {
      float F(int i) => float.Parse(t.Groups[i].Value, CultureInfo.InvariantCulture) / 255f;
      tint = new Vector4(F(2), F(3), F(4), F(1));
    }

    var clip = Regex.Match(body, "clipping\\s*=\\s*\\{\\s*level\\s*=\\s*([\\d.]+)");
    var fres = Regex.Match(body, @"softFreshnel\s*=\s*\{\s*enabled\s*=\s*true\s*power\s*=\s*([\d.]+)");
    return new EffectMeshMaterial(texture, sfx, tint, clip.Success ? float.Parse(clip.Groups[1].Value, CultureInfo.InvariantCulture) : 0f)
    { SoftFresnel = named && fres.Success ? float.Parse(fres.Groups[1].Value, CultureInfo.InvariantCulture) : 0f };
  }
}
