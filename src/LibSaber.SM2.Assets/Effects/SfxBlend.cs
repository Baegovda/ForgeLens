using System.Text.RegularExpressions;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>td <c>shaders.sfx.blendMode</c> (enum order of the exe table 0x144cccd38 and modtools sfx_sdr_desc.ps).</summary>
public enum SfxBlendMode
{
  /// <summary>Not written in the extracted .td (value comes from the "sfx" shader preset, which is not on disk).</summary>
  Unspecified = -1,
  Blend = 0,
  Add = 1,
  PremulA = 2,
}

/// <summary>
///   mtl1-44 PROVEN (static RE of sm2_retail.exe + shader cache psos): SFX material blendMode → shader define → PSO blend.
///   <list type="bullet">
///     <item>Parser FUN_1425fd580 (td <c>shaders.sfx</c>): stricmp against table 0x144cccd38 {"blend","add","premul_a"},
///       index stored as a byte at desc+0 (reflection FUN_142605c60: blendMode @0, isUseForPortals @1, softZ @0x110,
///       writeDepth @0x460, particle @0x463).</item>
///     <item>Key builder FUN_142451840 (sfx shader-type vtable 0x1437f0670 slot 1, define table FUN_142450650): in the colour
///       pass (<c>*pass == 0</c>) desc[0]==0 sets key bit 0 = SFX_ALPHA_BLEND, desc[0]==1 sets bit 1 = SFX_BLEND_ADD_NO_ALPHA;
///       premul_a sets neither. Bit order cross-checked with bit 5 SFX_SOFT_Z ← softZ.enabled, bit 16 SFX_SOFT_FRESNEL ← softFreshnel.</item>
///     <item>PSO table (shaders psos_sfx, non-DUDV colour pass): ADD_NO_ALPHA One/One (alpha Zero/One, 7200/7354);
///       ALPHA_BLEND SrcAlpha/InvSrcAlpha (alpha Zero/One, 2143/2335); neither One/InvSrcAlpha (premultiplied; alpha One/Zero
///       34/42). Matched PS triples 173/201/256 and 588/631/743 differ only in these defines; PS 201 (neither) outputs
///       rgb premultiplied by alpha, PS 256 (ALPHA_BLEND) straight rgb, PS 173 (ADD) rgb·alpha.</item>
///   </list>
///   HEURISTIC: <see cref="SfxBlendMode.Unspecified"/> (no blendMode in the .td; the loader requires the key, so it comes from the
///   unextracted "sfx" preset) keeps the capture-based additive state (plasma/outdoor SFX draws One/One).
///   <c>SM2_VFX_SFX_BLEND=0</c> restores "every sfx mesh additive".
/// </summary>
public static class SfxBlend
{
  public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_SFX_BLEND") != "0";

  public static SfxBlendMode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
  {
    "blend" => SfxBlendMode.Blend,
    "add" => SfxBlendMode.Add,
    "premul_a" => SfxBlendMode.PremulA,
    _ => SfxBlendMode.Unspecified,
  };

  /// <summary>Top-level <c>blendMode</c> of the first <c>sfx = { ... }</c> block in <paramref name="body"/> (nested layer/edge
  ///   blendModes are ignored), or Unspecified.</summary>
  public static SfxBlendMode FromTd(string? body)
  {
    if (string.IsNullOrEmpty(body)) return SfxBlendMode.Unspecified;
    var m = Regex.Match(body, @"\bsfx\s*=\s*\{");
    if (!m.Success) return SfxBlendMode.Unspecified;
    var depth = 1;
    for (var i = m.Index + m.Length; i < body.Length && depth > 0; i++)
    {
      var ch = body[i];
      if (ch == '{') depth++;
      else if (ch == '}') depth--;
      else if (depth == 1 && ch == 'b' && string.CompareOrdinal(body, i, "blendMode", 0, 9) == 0 && (i == 0 || !char.IsLetterOrDigit(body[i - 1])))
      {
        var v = Regex.Match(body[i..Math.Min(body.Length, i + 64)], "^blendMode\\s*=\\s*\"([A-Za-z_]+)\"");
        if (v.Success) return Parse(v.Groups[1].Value);
      }
    }
    return SfxBlendMode.Unspecified;
  }

  /// <summary>Effective mode: Unspecified (and everything when disabled) → Add.</summary>
  public static SfxBlendMode Effective(SfxBlendMode mode) => !Enabled || mode == SfxBlendMode.Unspecified ? SfxBlendMode.Add : mode;

  public static bool IsAdditive(SfxBlendMode mode) => Effective(mode) == SfxBlendMode.Add;

  /// <summary>Shader define the key builder sets (null = none).</summary>
  public static string? Define(SfxBlendMode mode) => mode switch
  {
    SfxBlendMode.Blend => "SFX_ALPHA_BLEND",
    SfxBlendMode.Add => "SFX_BLEND_ADD_NO_ALPHA",
    _ => null,
  };

  /// <summary>Colour-pass PSO blend of the effective mode.</summary>
  public static VfxBlendState State(SfxBlendMode mode) => Effective(mode) switch
  {
    SfxBlendMode.Blend => new(true, "SrcAlpha", "InvSrcAlpha", "Zero", "One"),
    SfxBlendMode.PremulA => new(true, "One", "InvSrcAlpha", "One", "Zero"),
    _ => new(true, "One", "One", "Zero", "One"),
  };

  /// <summary>UE master: additive → Additive; blend / premul_a → Translucent (straight over; premultiplied One/InvSrcAlpha of
  ///   c·a gives the same composite c·a + dst·(1-a)).</summary>
  public static string UeBlend(SfxBlendMode mode) => IsAdditive(mode) ? "Additive" : "Translucent";

  /// <summary>Composite of straight colour <paramref name="c"/> with coverage <paramref name="a"/> over <paramref name="dst"/>.</summary>
  public static float Composite(SfxBlendMode mode, float c, float a, float dst) => IsAdditive(mode) ? c * a + dst : c * a + dst * (1f - a);
}
