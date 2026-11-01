namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Retail texture-usage channel table at <c>0x144ccee10</c> (27 names).
///   Parser <c>FUN_14267baf0</c> turns a <c>+</c>-joined usage string into a bit mask (<c>1 &lt;&lt; index</c>).
///   PROVEN (mtl1-39 / REA_FINDINGS).
/// </summary>
public static class TextureUsage
{
  /// <summary>Order matches exe table 0x144ccee10 (index 0 = MD … 21 = MSCRGHAO … 26 = MTRAO).</summary>
  public static readonly string[] Names =
  {
    "MD", "MSC", "MSP", "MRGH", "MNM", "MDT", "MDTM", "MT", "MAK", "MH",
    "MBR", "MBL", "MR", "MF", "MLM", "MPM", "MRC", "HDR", "MSCG", "MEM",
    "MAO", "MSCRGHAO", "MD16U", "MRGHAOD", "MHTERR", "MMRAOT", "MTRAO",
  };

  public const int MscgBit = 18;
  public const int MscrGhaoBit = 21;

  /// <summary>Parse <c>usage = "MSCG+MRGH"</c> / <c>MSCRGHAO</c> into the exe bit mask.</summary>
  public static uint ParseMask(string? usage)
  {
    if (string.IsNullOrWhiteSpace(usage))
      return 0;
    uint mask = 0;
    foreach (var raw in usage.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      var token = raw.Trim();
      if (token.Length == 0)
        continue;
      for (var i = 0; i < Names.Length; i++)
      {
        if (Names[i].Equals(token, StringComparison.OrdinalIgnoreCase))
        {
          mask |= 1u << i;
          break;
        }
      }
    }
    return mask;
  }

  /// <summary>Extract the top-level <c>usage = "…"</c> assignment from a .td body.</summary>
  public static string? FromTd(string? tdText)
  {
    if (string.IsNullOrEmpty(tdText))
      return null;
    // Prefer a line that starts with usage = so nested convert_settings.format_name is ignored.
    foreach (var line in tdText.Split('\n'))
    {
      var t = line.Trim();
      if (!t.StartsWith("usage", StringComparison.OrdinalIgnoreCase))
        continue;
      var eq = t.IndexOf('=');
      if (eq < 0)
        continue;
      var v = t[(eq + 1)..].Trim().TrimEnd(',').Trim().Trim('"');
      if (v.Length > 0)
        return v;
    }
    return null;
  }

  public static bool HasBit(uint mask, int bit) => ((mask >> bit) & 1u) != 0;
}
