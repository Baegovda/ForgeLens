using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
/// Blood-splash VAT decode, transcribed from the combine-material VS (pc_dx12_combine_material.sdc records 00000/00001,
/// e.g. 00000_0; exe permutation string COMBINE_MATERIAL_SHADER0_BLOOD_SPLASH). Material cb (binding 3) rows 6..8 hold
/// floatParam0..: [6].xyz = boundsMin (fp0-2), [6].w,[7].x,[7].y = scale (fp3-5), [7].z = rate (fp6), [7].w = frames (fp7),
/// [8].z = bands (fp10). HEURISTIC: the row-6 = floatParam0 alignment is inferred from the values matching solid_blood.td.
/// Textures: [18].x = anim_data (position, RGB8), [19].y = addInfo _n (normal). The _d texture is NOT read by this VS.
/// </summary>
public static class VatDecode
{
  /// <summary>Free-running frame: frames*frac(seed/frames) + rate*time (VS fallback path when no anim control).</summary>
  public static float FreeFrame(float seed, float frames, float rate, float time)
  {
    var q = seed / frames;
    var fr = q - MathF.Truncate(q);
    return frames * fr + rate * time;
  }

  /// <summary>Anim-control path (flags&amp;25==25): frame = animStage * (frames - 1).</summary>
  public static float StageFrame(float animStage, float frames) => animStage * (frames - 1);

  /// <summary>Texture V for integer frame f: bands&lt;2 -> -f/frames (wrap); else uv1.y - frac(f/frames)/bands.</summary>
  public static float RowV(float uv1Y, float frame, float frames, int bands)
  {
    var q = frame / frames;
    if (bands < 2) return -q;
    var fr = MathF.Abs(q) - MathF.Floor(MathF.Abs(q));
    return uv1Y - (q >= 0 ? fr : -fr) / bands;
  }

  /// <summary>Returns (v0, v1, t): the two rows sampled and the lerp weight.</summary>
  public static (float V0, float V1, float T) Rows(float uv1Y, float frame, float frames, int bands)
  {
    var f0 = MathF.Floor(frame);
    return (RowV(uv1Y, f0, frames, bands), RowV(uv1Y, f0 + 1, frames, bands), frame - f0);
  }

  /// <summary>Local position = boundsMin + texel.rgb(0..1) * 255 * scale.</summary>
  public static Vector3 Position(Vector3 texel, Vector3 boundsMin, Vector3 scale) => boundsMin + texel * 255f * scale;

  /// <summary>Normal = texel * 2.007874 - 1.007874 (= (b*255 - 128)/127), normalised after the instance transform.</summary>
  public static Vector3 Normal(Vector3 texel) => texel * 2.007874f - new Vector3(1.007874f);

  /// <summary>Whether the browser animates blood-splash meshes (SM2_VFX_VAT=1; parameter mapping is partly heuristic).</summary>
  public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_VAT") == "1";

  /// <summary>
  /// Reads VAT parameters from a <c>shader_blood_splash</c> .td body. HEURISTIC: floatParamN keys and texture names are
  /// matched loosely (position = first quoted name containing "anim_data" without an _n/_d suffix, normal = the "_n" one);
  /// bands default to 8 (the band count seen in splashe05 UV1) when floatParam10 is absent.
  /// </summary>
  /// <summary>
  ///   The td material block whose key names this mesh part (solid_blood.td: part <c>splashe07</c> -> material
  ///   <c>splash_07</c>, each with its own VAT bounds/frames/texture). Match: same trailing number and the key's letters
  ///   are a prefix of the part's. HEURISTIC (vfx3g): inferred from the key/part naming, not from exe code.
  /// </summary>
  public static string? MaterialBlockFor(string td, string? partName)
  {
    if (string.IsNullOrEmpty(partName)) return null;
    var pm = Regex.Match(partName, @"^([A-Za-z_]*?)_?0*(\d+)$");
    if (!pm.Success) return null;
    foreach (Match m in Regex.Matches(td, @"(?m)^\s*([A-Za-z_]+?)_?0*(\d+)\s*=\s*\{"))
    {
      if (m.Groups[2].Value != pm.Groups[2].Value) continue;
      var kl = m.Groups[1].Value.Replace("_", "");
      if (!pm.Groups[1].Value.Replace("_", "").StartsWith(kl, StringComparison.OrdinalIgnoreCase) || kl.Length < 3) continue;
      var body = td[m.Index..];
      var end = body.IndexOf("__type_id = \"material_templ\"", StringComparison.Ordinal);
      return end > 0 ? body[..end] : body;
    }
    return null;
  }


  public static VatParams? Parse(string? td)
  {
    if (td is null || !Regex.IsMatch(td, "shaderType\\s*=\\s*\"shader_blood_splash\"")) return null;
    var fp = new Dictionary<int, float>();
    foreach (Match m in Regex.Matches(td, @"floatParam(\d+)\s*=\s*(-?[\d.]+(?:[eE][-+]?\d+)?)"))
      fp.TryAdd(int.Parse(m.Groups[1].Value), float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    string? pos = null, nrm = null;
    foreach (Match m in Regex.Matches(td, "\"([^\"]*anim_data[^\"]*)\""))
    {
      var n = m.Groups[1].Value;
      if (n.EndsWith("_n", StringComparison.OrdinalIgnoreCase)) nrm ??= n;
      else if (!n.EndsWith("_d", StringComparison.OrdinalIgnoreCase)) pos ??= n;
    }
    if (pos is null || !fp.TryGetValue(7, out var frames) || frames < 1) return null;
    float F(int k, float d) => fp.TryGetValue(k, out var v) ? v : d;
    return new VatParams(new Vector3(F(0, 0), F(1, 0), F(2, 0)), new Vector3(F(3, 1 / 255f), F(4, 1 / 255f), F(5, 1 / 255f)),
      F(6, 0), frames, (int)MathF.Round(F(10, 8)), pos, nrm);
  }
}

/// <summary>Blood-splash VAT material constants (cb3 rows 6..8) and its two textures.</summary>
public sealed record VatParams(Vector3 Min, Vector3 Scale, float Rate, float Frames, int Bands, string PositionTexture, string? NormalTexture);
