using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   <c>glt.second_spec</c>: a second specular lobe (gold or colored sheen on Tyranid chitin, Titus armor trim).
///   Field layout from sm2_retail.exe FUN_1425c1d00 (sub-struct at glt+0x94, fields from FUN_142576200):
///   enabled +0, metallicTint +4 (ARGB, see FUN_142621620 packing), metalness.scale/bias +0x14/+0x18,
///   roughness.scale/bias +0x1c/+0x20. The permutation define GLT_USE_SECOND_SPEC (0x1437f89c0) is set by
///   FUN_1423b7640 at 0x1423b819a when enabled is true and no layer blend is active.
///   Defaults are the engine's full-dump values (373 disabled blocks in the retail .td set).
///   Shading (PS 03840_0 / 00004_1, per-layer cb6 rows 18/19): m2 = saturate(row18.w * packedMetal + row7.w),
///   F0_2 = lerp(0.04, tint.rgb * albedo, m2) (tint alpha unused), r2 = max(saturate(row19.x * packedRough + row19.y), 0.02).
///   The lobe is added to the base specular at weight 1 (direct lights only, no IBL); there is no blend weight.
/// </summary>
public sealed class SecondSpec
{
  /// <summary>R, G, B, A in 0..1 (the .td stores A, R, G, B bytes). Default A=0, white.</summary>
  public float[] MetallicTint { get; set; } = { 1f, 1f, 1f, 0f };
  public float MetalnessScale { get; set; } = 1f;
  public float MetalnessBias { get; set; }
  public float RoughnessScale { get; set; }
  public float RoughnessBias { get; set; } = 0.7f;
}

/// <summary>
///   <c>glt.anisotropic_spec</c>: anisotropic (hair or brushed) highlight. Layout from FUN_142579ff0 (sub-struct at
///   glt+0x108): enabled +0, angle +4 (degrees), dirMap +8 (texture), anisotropy +0x18, intensity +0x1c,
///   extra { anisotropy, intensity, shift } +0x20. FUN_1423b7640 at 0x1423b801f enables the aniso permutation when
///   enabled and anisotropy &gt; 0.01 (constant 0x143c843ac), and the dir-map permutation when dirMap is also bound
///   (glt+0x110 non-null). Defaults are the engine's full-dump values (362 disabled blocks).
///   CB_STATIC_GLT packer FUN_1423ba150: row5 = (cos, sin, -sin, cos) of clamp(angle, 0, 180) in radians;
///   row6 = (intensity, extra.intensity, 1 - clamp(anisotropy, 0, 0.95), 1 - clamp(extra.anisotropy, 0, 0.95))
///   at 0x1423ba35c..0x1423ba3ec; row4.y = extra.shift (0x1423ba3f4). In the shader alphaT = (r * k)^2, alphaB = r^2,
///   D scaled by intensity. extra{} is the shifted second lobe (tangent normalize(T + shift * N)) and only exists
///   when GLT_USE_SECOND_SPEC is also set.
/// </summary>
public sealed class AnisoSpec
{
  public float Angle { get; set; } = 90f;
  public float Anisotropy { get; set; } = 0.5f;
  public float Intensity { get; set; } = 1f;
  public string DirMap { get; set; } = "";
  public float ExtraAnisotropy { get; set; } = 0.5f;
  public float ExtraIntensity { get; set; } = 1f;
  public float ExtraShift { get; set; } = 0.2f;
}

/// <summary>
///   Glt-level base lobe inputs: <c>metallicTint</c> (glt+0x40), <c>reflectance</c> (glt+8), <c>metalness</c> {scale, bias}
///   (glt+0x8c) and <c>roughness</c> {scale, bias} (glt+0x138); offsets from the FUN_1425c1d00 reader. In the glt pixel
///   shaders (PS 00004_1 lines 405-423, 3646) metal = saturate(row8.x * packedMetal + row8.y),
///   roughness = saturate(row9.x * packedRough + row9.y), albedo *= lerp(albedo_tint, metallicTint, metal) and the dielectric
///   F0 is 0.16 * reflectance^2 (0.04 at the default 0.5). Defaults are the full-dump values (709 metalness {1, 0},
///   670 roughness {1, 0}, 377 metallicTint [0, 255, 255, 255], 236 reflectance 0.5). The CPU packer that fills
///   rows 8/9 was not located; the mapping rests on the field names, defaults, and the scale/bias form shared with
///   second_spec (rows 18.w/7.w, 19.x/19.y).
/// </summary>
public sealed class BaseLayer
{
  /// <summary>R, G, B, A in 0..1 (the .td stores A, R, G, B bytes). Default white.</summary>
  public float[] MetallicTint { get; set; } = { 1f, 1f, 1f, 0f };
  public float Reflectance { get; set; } = 0.5f;
  public float MetalnessScale { get; set; } = 1f;
  public float MetalnessBias { get; set; }
  public float RoughnessScale { get; set; } = 1f;
  public float RoughnessBias { get; set; }
}

/// <summary>
///   <c>glt.chameleon</c>: view-dependent albedo colour ramp. Field layout from the parser FUN_142576f30 / descriptor
///   FUN_142578190 (sm2 retail): enabled +0, color0..3 +4/+0x14/+0x24/+0x34 (bytes * 1/255), offset1 +0x44, offset2 +0x48,
///   fresnelPwr +0x4c. Shader (pc_dx12_glt.sdc, cb CB_STATIC_GLT rows 10..15 = PS_REG_GLT_CHAMELEON_COLORS[4] /
///   PS_REG_GLT_CHAMELEON_OFFSETS[2], e.g. PS record 03558_0): t = pow(saturate(dot(N, V)), row15.z);
///   c = lerp(C0, C1, sat((t - r14.x) * r14.y)); c = lerp(c, C2, sat((t - r14.z) * r14.w)); c = lerp(c, C3, sat((t - r15.x) * r15.y));
///   albedo *= lerp(c, 1, metalness). Defaults (parser FUN_142576f30 passes 0 as the default to every read): unset colours are
///   (0,0,0,0) black, unset offset1/offset2/fresnelPwr are 0. Text colours are [a,r,g,b] (FUN_142820d20 stores 2nd,3rd,4th,1st).
///   CPU packer FUN_1423ba150 (glt desc +0xb8 = chameleon): o1 = clamp01(offset1); o2 = offset2 &lt; offset1 ? offset1 + 1e-6 : offset2,
///   clamp01; r14 = (0, 1/(o1+1e-6), o1, 1/(o2-o1+1e-6)); r15 = (o2, 1/(1-o2+1e-6), fresnelPwr, -); colours rows 10..13 in order.
///   See <see cref="ChameleonSpec.Ramp"/>.
/// </summary>
/// <summary>
///   <c>glt.detail</c> { density, scale }: tiling and strength of the <c>tex.det</c> detail normal. The retail .td set
///   (18690 files) has 1535 such blocks; density is mostly 5 or 1 (up to 20), scale is often absent. HEURISTIC: density is
///   used as the detail UV repeat count and scale as the detail blend strength; the CPU packer and shader use were not
///   located. Defaults (1, 1) when a key is absent are also unproven.
/// </summary>
public sealed class DetailSpec
{
  public float Density { get; set; } = 1f;
  public float Scale { get; set; } = 1f;
}

public sealed class ChameleonSpec
{
  public float[][] Colors { get; set; } = { new float[4], new float[4], new float[4], new float[4] };
  public float Offset1 { get; set; }
  public float Offset2 { get; set; }
  public float FresnelPower { get; set; }

  /// <summary>CB_STATIC_GLT rows 14/15 exactly as the CPU packer FUN_1423ba150 writes them (eps [0x143c8435c] = 1e-6).</summary>
  public static (float[] R14, float[] R15) Ramp(float offset1, float offset2, float fresnelPower)
  {
    const float Eps = 1e-6f;
    var o1 = offset1;
    var o2 = offset2 < o1 ? o1 + Eps : offset2;
    o1 = Math.Clamp(o1, 0f, 1f);
    o2 = Math.Clamp(o2, 0f, 1f);
    return (new[] { 0f, 1f / (o1 + Eps), o1, 1f / (o2 - o1 + Eps) }, new[] { o2, 1f / (1f - o2 + Eps), fresnelPower, 0f });
  }
}

public static class SpecLayers
{
  private const float AnisoThreshold = 0.01f; // [0x143c843ac], compared at 0x1423b8031

  private static readonly Regex Color = new(
    @"\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]",
    RegexOptions.Compiled);

  /// <summary>Null unless <c>second_spec.enabled = true</c> (the engine default is false).</summary>
  public static SecondSpec? Second(string variantBlock)
  {
    var block = Child(Glt(variantBlock), "second_spec");
    if (block is null || !Flag(block, "enabled"))
      return null;
    var result = new SecondSpec();
    var tint = TintOf(block, "metallicTint");
    if (tint is not null)
      result.MetallicTint = tint;
    var metal = Child(block, "metalness");
    if (metal is not null)
    {
      result.MetalnessScale = Number(metal, "scale") ?? result.MetalnessScale;
      result.MetalnessBias = Number(metal, "bias") ?? result.MetalnessBias;
    }

    var rough = Child(block, "roughness");
    if (rough is not null)
    {
      result.RoughnessScale = Number(rough, "scale") ?? result.RoughnessScale;
      result.RoughnessBias = Number(rough, "bias") ?? result.RoughnessBias;
    }

    return result;
  }

  /// <summary>Glt-level metallicTint, reflectance, metalness and roughness scale/bias (defaults when unset).</summary>
  public static BaseLayer Base(string variantBlock)
  {
    var glt = Glt(variantBlock);
    var result = new BaseLayer();
    var tint = TintOf(WithoutBlocks(glt), "metallicTint");
    if (tint is not null)
      result.MetallicTint = tint;
    result.Reflectance = Number(glt, "reflectance") ?? result.Reflectance;
    var metal = Child(glt, "metalness");
    if (metal is not null)
    {
      result.MetalnessScale = Number(metal, "scale") ?? result.MetalnessScale;
      result.MetalnessBias = Number(metal, "bias") ?? result.MetalnessBias;
    }

    var rough = Child(glt, "roughness");
    if (rough is not null)
    {
      result.RoughnessScale = Number(rough, "scale") ?? result.RoughnessScale;
      result.RoughnessBias = Number(rough, "bias") ?? result.RoughnessBias;
    }

    return result;
  }

  /// <summary><c>glt.detail</c> of the variant, or null when the variant has no detail block.</summary>
  public static DetailSpec? Detail(string variantBlock)
  {
    var block = Child(Glt(variantBlock), "detail");
    if (block is null)
      return null;
    var result = new DetailSpec();
    result.Density = Number(block, "density") ?? result.Density;
    result.Scale = Number(block, "scale") ?? result.Scale;
    return result;
  }

  /// <summary>Null unless <c>glt.chameleon.enabled = true</c>. See <see cref="ChameleonSpec"/>.</summary>
  public static ChameleonSpec? Chameleon(string variantBlock)
  {
    var block = Child(Glt(variantBlock), "chameleon");
    if (block is null || !Flag(block, "enabled"))
      return null;
    var result = new ChameleonSpec();
    for (var i = 0; i < 4; i++)
      if (TintOf(block, "color" + i) is { } color)
        result.Colors[i] = color;
    result.Offset1 = Number(block, "offset1") ?? result.Offset1;
    result.Offset2 = Number(block, "offset2") ?? result.Offset2;
    result.FresnelPower = Number(block, "fresnelPwr") ?? result.FresnelPower;
    return result;
  }

  /// <summary>Null unless enabled and anisotropy is above the engine's 0.01 cutoff.</summary>
  public static AnisoSpec? Aniso(string variantBlock)
  {
    var block = Child(Glt(variantBlock), "anisotropic_spec");
    if (block is null || !Flag(block, "enabled"))
      return null;
    var result = new AnisoSpec();
    result.Angle = Number(block, "angle") ?? result.Angle;
    result.Anisotropy = Number(block, "anisotropy") ?? result.Anisotropy;
    result.Intensity = Number(block, "intensity") ?? result.Intensity;
    var dir = Regex.Match(Shallow(block), "(^|\\n)\\s*dirMap\\s*=\\s*\"([^\"]*)\"");
    if (dir.Success)
      result.DirMap = dir.Groups[2].Value.Trim();
    var extra = Child(block, "extra");
    if (extra is not null)
    {
      result.ExtraAnisotropy = Number(extra, "anisotropy") ?? result.ExtraAnisotropy;
      result.ExtraIntensity = Number(extra, "intensity") ?? result.ExtraIntensity;
      result.ExtraShift = Number(extra, "shift") ?? result.ExtraShift;
    }

    return result.Anisotropy > AnisoThreshold ? result : null;
  }

  /// <summary>Glt-level <c>shaders.glt</c> block of a variant, or the variant itself when there is none.</summary>
  private static string Glt(string variantBlock)
    => Child(variantBlock, "shaders") is { } shaders && Child(shaders, "glt") is { } glt ? glt : variantBlock;

  /// <summary>A direct child block (<c>key = { ... }</c>) of <paramref name="block"/>, not a deeper one.</summary>
  private static string? Child(string block, string key)
  {
    var depth = 0;
    var inner = block.StartsWith('{') ? 1 : 0;
    var pattern = new Regex("\\G\\s*" + Regex.Escape(key) + "\\s*=\\s*\\{");
    for (var i = 0; i < block.Length; i++)
    {
      var c = block[i];
      if (c == '{')
        depth++;
      else if (c == '}')
        depth--;
      else if (depth == inner && (i == 0 || block[i - 1] == '\n'))
      {
        var match = pattern.Match(block, i);
        if (!match.Success)
          continue;
        var open = match.Index + match.Length - 1;
        var level = 0;
        for (var j = open; j < block.Length; j++)
        {
          if (block[j] == '{')
            level++;
          else if (block[j] == '}' && --level == 0)
            return block[open..(j + 1)];
        }

        return null;
      }
    }

    return null;
  }

  /// <summary>The block with nested <c>{ }</c> and <c>[ ]</c> bodies removed, so a key only matches at this level.</summary>
  private static string Shallow(string block)
  {
    var text = new StringBuilder(block.Length);
    var depth = 0;
    var inner = block.StartsWith('{') ? 1 : 0;
    foreach (var c in block)
    {
      if (c is '{' or '[')
      {
        depth++;
        if (depth <= inner)
          text.Append(c);
        continue;
      }

      if (c is '}' or ']')
      {
        depth--;
        if (depth < inner)
          text.Append(c);
        continue;
      }

      if (depth <= inner)
        text.Append(c);
    }

    return text.ToString();
  }

  /// <summary>The block with nested <c>{ }</c> bodies removed (arrays kept), so <c>second_spec.metallicTint</c> does not match.</summary>
  private static string WithoutBlocks(string block)
  {
    var text = new StringBuilder(block.Length);
    var depth = 0;
    var inner = block.StartsWith('{') ? 1 : 0;
    foreach (var c in block)
    {
      if (c == '{')
        depth++;
      if (depth <= inner)
        text.Append(c);
      if (c == '}')
        depth--;
    }

    return text.ToString();
  }

  private static bool Flag(string block, string key)
    => Regex.IsMatch(Shallow(block), "(^|\\n)\\s*" + Regex.Escape(key) + "\\s*=\\s*true\\b");

  private static float? Number(string block, string key)
  {
    var match = Regex.Match(Shallow(block), "(^|\\n)\\s*" + Regex.Escape(key) + "\\s*=\\s*(-?[0-9.]+(?:[eE][-+]?[0-9]+)?)");
    if (!match.Success)
      return null;
    return float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
  }

  private static float[]? TintOf(string block, string key)
  {
    var at = Regex.Match(block, "(^|\\n)\\s*" + Regex.Escape(key) + "\\s*=\\s*(?=\\[)");
    if (!at.Success)
      return null;
    var match = Color.Match(block, at.Index + at.Length);
    if (!match.Success || match.Index != at.Index + at.Length)
      return null;
    float Byte(int group) => Math.Clamp(int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture), 0, 255) / 255f;
    return new[] { Byte(2), Byte(3), Byte(4), Byte(1) };
  }
}
