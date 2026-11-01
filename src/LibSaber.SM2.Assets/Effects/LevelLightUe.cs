using System.Globalization;
using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   Shared hub / level-light → Unreal placement rules (viewer + UE import).
///   Engine packer <c>FUN_14224ecf0</c> + P0.w <c>FUN_14224f010</c> + desc→render <c>FUN_141384090</c> are PROVEN
///   (docs/handoff/RE_CACHE/notes/REA_FINDINGS.md). UE candela/exponent bridging remains INFERRED.
/// </summary>
public static class LevelLightUe
{
  public const string ColorOrderEvidence = "PROVEN R97y: ColorCtrl = [A,R,G,B]";

  public const string IntensityEvidence =
    "PROVEN FUN_14224ecf0/FUN_14224f010: colour=(c/255)^2.2*I [*400 if useInverseSquaredFalloff]; " +
    "P0.w directional=0 / constAtten=8e22 / invSq=0.0025 / else 299*a^4+1 or 256^a. " +
    "INFERRED UE: Intensity = I * colorScale (no FIT K=10); attenuation radius = RadiusCtrl m → cm; " +
    "soft/curve lights use non-inverse-square with falloff exponent from attenCurveParam (approx).";

  public const string SpotEvidence =
    "PROVEN FUN_14224ecf0 type 1 spot: cos(angle*pi/360) cone scale/offset in P3/P2; " +
    "FUN_141384090 copies spotAngle (+0x60) and spotInnerAngleFactor (+0x64). " +
    "INFERRED UE outer=spotAngle deg, inner=spotAngle*spotInnerAngleFactor; " +
    "direction not in lights TSV → default Spot forward (needs cd_list matrix for parity).";

  /// <summary>SM2 metres (x,y,z) → UE cm (x,z,y)*100 (PROVEN prior level import).</summary>
  public static Vector3 MetresToUeCm(float x, float y, float z) => new(x * 100f, z * 100f, y * 100f);

  /// <summary>Decode ColorCtrl [A,R,G,B] bytes (0..255) to linear RGB via pure 2.2 power (PROVEN).</summary>
  public static Vector3 ArgbBytesToLinearRgb(float a, float r, float g, float b)
    => CaptureLightUnits.Linear(new Vector3(r, g, b) / 255f);

  public static Vector3 ParseColorCtrlToLinearRgb(string colorCtrl)
  {
    var parts = colorCtrl.Trim().Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length < 4)
      throw new FormatException($"ColorCtrl needs 4 ARGB components, got '{colorCtrl}'");
    return ArgbBytesToLinearRgb(float.Parse(parts[0], CultureInfo.InvariantCulture),
      float.Parse(parts[1], CultureInfo.InvariantCulture),
      float.Parse(parts[2], CultureInfo.InvariantCulture),
      float.Parse(parts[3], CultureInfo.InvariantCulture));
  }

  /// <summary>PROVEN FUN_14224f010 P0.w.</summary>
  public static float P0w(float attenCurveParam, bool useInverseSquaredFalloff = false, bool isConstAtten = false, bool directional = false)
    => CaptureLightUnits.InverseSquareSoft(attenCurveParam, useInverseSquaredFalloff, isConstAtten, directional);

  /// <summary>PROVEN FUN_14224ecf0 colour ×400 when invSq.</summary>
  public static float ColorScale(bool useInverseSquaredFalloff)
    => CaptureLightUnits.LightColorScale(useInverseSquaredFalloff);

  /// <summary>
  ///   UE light Intensity scalar (INFERRED unit bridge).
  ///   Engine HDR RGB = linearColor * I * ColorScale(invSq) [* adaptive]. UE multiplies LightColor × Intensity,
  ///   so Intensity = I * ColorScale with LightColor = linear RGB (no legacy FIT K=10).
  /// </summary>
  public static float UeIntensity(float authoredIntensity, bool useInverseSquaredFalloff = false)
    => authoredIntensity * ColorScale(useInverseSquaredFalloff);

  /// <summary>Attenuation radius cm from RadiusCtrl metres (fallback 10 m → 1000 cm).</summary>
  public static float AttenuationRadiusCm(float radiusCtrlMetres)
    => (radiusCtrlMetres > 0 ? radiusCtrlMetres : 10f) * 100f;

  /// <summary>
  ///   INFERRED UE outer/inner cone degrees from authored spotAngle + spotInnerAngleFactor
  ///   (engine uses cos(angle*π/360) for half-angle in degrees).
  /// </summary>
  public static (float OuterDegrees, float InnerDegrees) SpotConeDegrees(float spotAngle, float spotInnerAngleFactor)
  {
    var outer = spotAngle > 0 ? spotAngle : 45f;
    var factor = Math.Clamp(spotInnerAngleFactor, 0f, 1f);
    var inner = outer * factor;
    if (inner > outer)
      inner = outer;
    return (outer, inner);
  }

  /// <summary>
  ///   INFERRED UE falloff exponent for non-inverse-square lights from attenCurveParam.
  ///   Engine soft is 299*a^4+1 (a&gt;0) or 256^a (a≤0); UE exponent is a different curve — keep a thin mapping.
  /// </summary>
  public static float UeFalloffExponent(float attenCurveParam, bool useInverseSquaredFalloff, bool isConstAtten)
  {
    if (useInverseSquaredFalloff)
      return 2f;
    if (isConstAtten)
      return 0f;
    // Map a in typical [-1,1] toward UE's 0..16-ish soft range; identity-ish for a=0 → soft 1.
    var a = attenCurveParam;
    if (a <= 0f)
      return Math.Clamp(2f + a * 2f, 0.1f, 16f);
    return Math.Clamp(2f + a * 6f, 0.1f, 16f);
  }

  /// <summary>Map one lights-TSV style row to UE spawn parameters.</summary>
  public static LevelLightUeParams? FromRow(
    string lightType,
    float x, float y, float z,
    float intensity,
    string colorCtrl,
    float radiusCtrlMetres,
    float attenCurveParam = 0f,
    bool useInverseSquaredFalloff = false,
    bool isConstAtten = false,
    float spotAngle = 0f,
    float spotInnerAngleFactor = 0f)
  {
    if (intensity <= 0f)
      return null;
    var lt = (lightType ?? "point").Trim().ToLowerInvariant();
    if (lt is not ("point" or "spot" or "directional"))
      return null;

    var rgb = ParseColorCtrlToLinearRgb(colorCtrl);
    var invSq = useInverseSquaredFalloff;
    var directional = lt == "directional";
    var (outer, inner) = SpotConeDegrees(spotAngle, spotInnerAngleFactor);

    return new LevelLightUeParams
    {
      LightType = lt == "spot" ? "spot" : (directional ? "directional" : "point"),
      AuthoredType = lt,
      Location = MetresToUeCm(x, y, z),
      ColorLinear = rgb,
      Intensity = UeIntensity(intensity, invSq),
      AttenuationRadius = AttenuationRadiusCm(radiusCtrlMetres),
      UseInverseSquaredFalloff = invSq,
      FalloffExponent = UeFalloffExponent(attenCurveParam, invSq, isConstAtten),
      P0w = P0w(attenCurveParam, invSq, isConstAtten, directional),
      OuterConeDegrees = outer,
      InnerConeDegrees = inner,
      CastShadows = false,
      Evidence = IntensityEvidence,
      SpotEvidence = SpotEvidence,
    };
  }
}

/// <summary>UE spawn bag produced by <see cref="LevelLightUe.FromRow"/>.</summary>
public sealed class LevelLightUeParams
{
  public string LightType { get; init; } = "point";
  public string AuthoredType { get; init; } = "point";
  public Vector3 Location { get; init; }
  public Vector3 ColorLinear { get; init; }
  public float Intensity { get; init; }
  public float AttenuationRadius { get; init; }
  public bool UseInverseSquaredFalloff { get; init; }
  public float FalloffExponent { get; init; }
  public float P0w { get; init; }
  public float OuterConeDegrees { get; init; }
  public float InnerConeDegrees { get; init; }
  public bool CastShadows { get; init; }
  public string Evidence { get; init; } = "";
  public string SpotEvidence { get; init; } = "";
}
