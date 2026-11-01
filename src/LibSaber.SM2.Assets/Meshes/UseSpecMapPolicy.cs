namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Decide whether a material slot samples the packed specular map (UE <c>UseSpecMap</c> / viewer <c>uHasSpec</c>).
///   <para>
///   <b>PROVEN</b> (REA / headless Ghidra on sm2_retail.exe): CPU writer <c>FUN_14217da70</c> (siblings
///   <c>FUN_14217e330</c>, <c>FUN_14217ebf0</c>) stores the float bitfield at <c>param_1[0x3c]</c> =
///   <c>OBJ_MTLBLEND_LAYER_CONST[15].x</c> (CB_OBJ_MATERIAL_DATA byte 336). Flag <c>0x2000</c> is set iff the
///   texture at material+0xb0 has usage-mask bit 21 (<c>MSCRGHAO</c>, table <c>0x144ccee10</c>); flag <c>0x40</c>
///   is set iff the same texture has bit 18 (<c>MSCG</c>). Chameleon slots with no MSCRGHAO/MSCG at +0xb0 get 0
///   naturally (no separate chameleon branch). Evidence: docs/handoff/RE_CACHE/notes/REA_FINDINGS.md;
///   decomp evidence FUN_14217da70.c.
///   </para>
///   <para>
///   Importer / viewer UseSpecMap follows those two LAYER bits: sample packed metal/rough when either is set.
///   </para>
/// </summary>
public static class UseSpecMapPolicy
{
  public const int MscgBit = TextureUsage.MscgBit;
  public const int MscrGhaoBit = TextureUsage.MscrGhaoBit;
  public const uint LayerSpecSampleMask = (1u << MscgBit) | (1u << MscrGhaoBit);

  public const string Evidence =
    "PROVEN FUN_14217da70 param_1[0x3c]=OBJ_MTLBLEND_LAYER_CONST[15].x: 0x2000 iff usage bit 21 MSCRGHAO; " +
    "0x40 iff bit 18 MSCG (siblings FUN_14217e330/FUN_14217ebf0; table 0x144ccee10). " +
    "UseSpecMap = (usage & (MSCRGHAO|MSCG)) != 0; chameleon naturally 0 without those bits.";

  /// <summary>True when the specular/packed texture usage mask carries MSCRGHAO and/or MSCG.</summary>
  public static bool Decide(uint specularUsageMask)
    => (specularUsageMask & LayerSpecSampleMask) != 0;

  /// <summary>
  ///   Parse a .td <c>usage</c> string (e.g. <c>MSCRGHAO</c>, <c>MSCG+MRGH</c>) then decide.
  /// </summary>
  public static bool Decide(string? specularUsage)
    => Decide(TextureUsage.ParseMask(specularUsage));

  /// <summary>Convenience for a <see cref="MeshPart"/> (specular map usage, else part.SpecularUsage).</summary>
  public static bool Decide(MeshPart part)
  {
    var map = part.Maps.Find(m => string.Equals(m.Role, "specular", StringComparison.OrdinalIgnoreCase));
    var usage = map?.Usage;
    if (string.IsNullOrEmpty(usage))
      usage = part.SpecularUsage;
    if (!string.IsNullOrEmpty(usage))
      return Decide(usage);

    // No .td usage available: INFERRED parity with DATA (bound *_spec with Metal/Rough channels).
    var hasSpec = !string.IsNullOrEmpty(part.Specular)
      || part.Maps.Exists(m => string.Equals(m.Role, "specular", StringComparison.OrdinalIgnoreCase) && !m.Missing);
    if (!hasSpec)
      return false;
    var channels = map?.Channels ?? part.SpecularChannels ?? "";
    return channels.Contains("Metal", StringComparison.OrdinalIgnoreCase)
      || channels.Contains("MetGray", StringComparison.OrdinalIgnoreCase)
      || channels.Contains("Rough", StringComparison.OrdinalIgnoreCase)
      || channels.Contains("Spec", StringComparison.OrdinalIgnoreCase);
  }
}
