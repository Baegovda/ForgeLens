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
