using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   Shared hub / level-light → Unreal placement rules (viewer + UE import).
///   Structural root of R97/R98 scene-script drift: these numbers lived only in one-off <c>ue_r98_hublights.py</c>.
/// </summary>
public static class LevelLightUe
{
  /// <summary>ColorCtrl order [A,R,G,B] (PROVEN R97y).</summary>
  public const string ColorOrderEvidence = "PROVEN R97y: ColorCtrl = [A,R,G,B]";

  /// <summary>
  ///   HEURISTIC FIT: UE candela = K * authored Intensity (R98b used K=10). Not from RE.
  ///   Closing evidence = match armory/hub capture luminance after UE exposure/tonemap (owner visual OK + measured EV).
  /// </summary>
  public const float IntensityFitK = 10f;
  public const string IntensityEvidence = "HEURISTIC FIT K=10 (R98b); ARGB colour order PROVEN; spot→point when direction missing INFERRED from TSV schema";

  /// <summary>SM2 metres (x,y,z) → UE cm (x,z,y)*100 (same as ue_vfx_frame / ue_r66_level). PROVEN by prior level import.</summary>
  public static Vector3 MetresToUeCm(float x, float y, float z) => new(x * 100f, z * 100f, y * 100f);

  /// <summary>Decode ColorCtrl [A,R,G,B] bytes (0..255) to linear RGB via pure 2.2 power (PROVEN level-lights DATA).</summary>
  public static Vector3 ArgbBytesToLinearRgb(float a, float r, float g, float b)
    => CaptureLightUnits.Linear(new Vector3(r, g, b) / 255f);

  /// <summary>Parse a ColorCtrl string like <c>[255,165,207,226]</c> (A,R,G,B) to linear RGB.</summary>
  public static Vector3 ParseColorCtrlToLinearRgb(string colorCtrl)
  {
    var parts = colorCtrl.Trim().Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length < 4)
      throw new FormatException($"ColorCtrl needs 4 ARGB components, got '{colorCtrl}'");
    return ArgbBytesToLinearRgb(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
  }

  /// <summary>UE intensity (candela) from authored Intensity. HEURISTIC FIT.</summary>
  public static float UeCandela(float intensity, float k = IntensityFitK) => k * intensity;

  /// <summary>Attenuation radius cm from RadiusCtrl metres (fallback 10 m → 1000 cm).</summary>
  public static float AttenuationRadiusCm(float radiusCtrlMetres)
    => (radiusCtrlMetres > 0 ? radiusCtrlMetres : 10f) * 100f;
}
