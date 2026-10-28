namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   mtl1-23: rules measured in RenderDoc captures of the running game (nightly 1.47; box /workspace/rdc/NOTES.md, per-scene
///   draws.json / cbuffers.json / dis). Frames: hellbrute 68141, smoke 88302, plasma 101917, armory 128197, outdoor 137266.
///   Each constant cites the draw (eid) or shader (PS id) it was read from. These replace the earlier REA-only heuristics.
/// </summary>
public enum VfxDrawKind
{
  /// <summary>CPU sprite particles (smoke/explosion): CB_PARTICLES, PS 101818.</summary>
  CpuParticle,
  /// <summary>Trails: CB_PASS_TRAIL.</summary>
  Trail,
  /// <summary>Spark particles (eid 2086 (NOTES 12:10 blend list), 228 instances): no blending, writes VELOCITY.</summary>
  Spark,
  /// <summary>SFX meshes (plasma CB_PASS_SCREEN_EFFECT_STATIC / CB_DYNAMIC_SFX).</summary>
  SfxMesh
}

/// <summary>A D3D12 blend state (factor names as RenderDoc prints them).</summary>
public readonly record struct VfxBlendState(bool Enabled, string Src, string Dst, string AlphaSrc, string AlphaDst)
{
  public override string ToString() => Enabled ? $"{Src}/{Dst} (alpha {AlphaSrc}/{AlphaDst})" : "opaque (blending disabled)";
}

public static class VfxBlendRules
{
  /// <summary>Capture blend state per draw kind (draws.json blend[0] of the cited draws).</summary>
  public static VfxBlendState For(VfxDrawKind kind) => kind switch
  {
    // smoke 88302 / outdoor 137266, PS 101818: premultiplied over.
    VfxDrawKind.CpuParticle => new(true, "One", "InvSrcAlpha", "One", "InvSrcAlpha"),
    // CB_PASS_TRAIL draws: straight alpha, destination alpha kept.
    VfxDrawKind.Trail => new(true, "SrcAlpha", "InvSrcAlpha", "Zero", "One"),
    // eid 2086 (NOTES 12:10 blend list): blending disabled (opaque / alpha-tested), writes the VELOCITY target.
    VfxDrawKind.Spark => new(false, "One", "Zero", "One", "Zero"),
    // plasma 101917 SFX meshes: additive, destination alpha kept.
    VfxDrawKind.SfxMesh => new(true, "One", "One", "Zero", "One"),
    _ => throw new ArgumentOutOfRangeException(nameof(kind))
  };
}

/// <summary>
///   mtl1-28: draw kind of an emitter's sprite batch. Trails are <see cref="VfxDrawKind.Trail"/> by construction
///   (EffectTrailSprites). Sparks: the capture draw (smoke 88302 eid 2086, CB_PARTICLES, 228 instances, blending off,
///   writes VELOCITY) has no sfx name, and the exe names a SPARKS pass ("SPARKS" @0x1436dceac, "SPARKS_BEGIN" @0x1436e16a0,
///   data-table refs only). The sfx data has no opaque blendType (add 10841, blend 7150, blend_add 635), so the mapping
///   to data is HEURISTIC: a GPU_EMITTER with <c>transforms.streaks.isEnabled</c> (velocity-stretched GPU points, the
///   only GPU-only render feature, 1163 of 2000 GPU emitters) is a spark. <c>SM2_VFX_KIND_BLEND=0</c> draws everything
///   with the particle blend as before.
/// </summary>
public static class VfxDrawKinds
{
  public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_KIND_BLEND") != "0";

  public static VfxDrawKind ForEmitter(EmitterDef emitter) => Classify(emitter.IsGpu, emitter.StreakRatio);

  public static VfxDrawKind Classify(bool isGpu, float streakRatio) =>
    isGpu && streakRatio > 0f ? VfxDrawKind.Spark : VfxDrawKind.CpuParticle;

  /// <summary>Spark alpha test (blending is off): texture coverage below this is discarded. HEURISTIC 0.5.</summary>
  public const float SparkAlphaTest = 0.5f;
}

/// <summary>
///   SFX mesh emissive exposure compensation, plasma PS s33060 (disasm lines 514-517):
///   intensity *= lerp(1, COMMON_HDR_AVERAGE_LUMINANCE_CLAMPED, sfx[560].w). With the frame exposure = key / avgLum
///   (CaptureTonemap), k = 1 makes the emissive term scene-exposure independent. Which td field fills sfx[560].w
///   (CB_DYNAMIC_SFX byte 560, .w) is not traced: k is HEURISTIC (default 1).
/// </summary>
public static class SfxExposure
{
  public static float Scale(float averageLuminanceClamped, float k) => 1f + k * (averageLuminanceClamped - 1f);

  /// <summary>
  ///   Exposure for an SFX emissive given the frame exposure and the exposure at which the frame's average luminance
  ///   would be 1 (the bench default): exposure * lerp(1, avgLum, k) with avgLum = reference / exposure.
  /// </summary>
  public static float Exposure(float frameExposure, float referenceExposure, float k)
    => frameExposure * Scale(referenceExposure / MathF.Max(frameExposure, 1e-12f), k);
}

/// <summary>
///   Screen distortion, plasma 101917: particle PS 4996 writes DUDV (R10G10B10A2, One/InvSrcAlpha) =
///   mask * (0.5 + 2*offset), mask = sat(10*|offset|) * depthFade, offset = normal.xy * TEXCOORD0.w (per-particle strength
///   x fade); post PS 5160: uv += DUDV.xy * 0.04 - 0.02 (hardcoded), gated by DUDV.w. Over the neutral 0.5 buffer
///   the screen shift is 0.08 * mask * offset, at most +-0.02 of the screen.
/// </summary>
public static class DistortionRules
{
  /// <summary>Screen-UV shift per unit offset (2 x 0.04).</summary>
  public const float UvPerOffset = 0.08f;
  /// <summary>Largest shift the 0..1 DUDV encoding can produce (0.04 x 0.5).</summary>
  public const float MaxShift = 0.02f;

  public static float Mask(float offsetX, float offsetY, float depthFade = 1f)
    => Math.Clamp(10f * (MathF.Abs(offsetX) + MathF.Abs(offsetY)), 0f, 1f) * depthFade;

  /// <summary>Encoded DUDV channel for one particle (before blending), PS 4996.</summary>
  public static float Encode(float offset, float mask) => mask * (0.5f + 2f * offset);

  /// <summary>Post-process uv shift from a DUDV channel value, PS 5160.</summary>
  public static float PostShift(float dudv) => dudv * 0.04f - 0.02f;

  /// <summary>Screen shift of one particle over the neutral (0.5) buffer: PostShift(Encode + (1-mask)*0.5), clamped.</summary>
  public static float Shift(float offset, float mask)
    => Math.Clamp(PostShift(Encode(offset, mask) + (1f - mask) * 0.5f), -MaxShift, MaxShift);
}

/// <summary>
///   CB_PASS_HDR tonemap (identical in all five captures), LUT bake CS s1996: x = linear HDR * exposure;
///   F(x) = (x(Ax+CB)+DE)/(x(Ax+B)+DF) - E/F (Uncharted 2 / Hable) with A..D = HDR_TONEMAP_CURVE_PARAMS_0 [0.12,0.05,0.45,0.2],
///   E,F = _1 [0.02,0.08]; out = sat(F(x) * WHITEPOINT_SCALE 1.375385 (outdoor 1.343645)), then sRGB and the grading LUT
///   (LUT_RANGE_LOG 14, GREY_POINT 0.43, LUT input x = 0.18*2^((i/31 - grey)*rangeLog) - 0.18*2^(-grey*rangeLog)).
///   Exposure: CB_COMMON EXPOSURE_DIV_AVG_LUM x AVG_LUM = 0.112..0.117 in every scene, so exposure ~ Key / avgLum
///   (Key 0.11 is a fit of five scenes: HEURISTIC; the histogram auto-exposure was not traced).
/// </summary>
public static class CaptureTonemap
{
  public const float A = 0.12f, B = 0.05f, C = 0.45f, D = 0.2f, E = 0.02f, F = 0.08f;
  public const float WhitePointScale = 1.375385f;
  public const float LutRangeLog = 14f, LutGreyPoint = 0.43f;
  public const float Key = 0.11f;

  public static float Curve(float x)
  {
    x = MathF.Max(x, 0f);
    return (x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F) - E / F;
  }

  /// <summary>Display value 0..1 (linear, before sRGB encoding) for an exposed HDR value.</summary>
  public static float Apply(float exposed) => Math.Clamp(Curve(exposed) * WhitePointScale, 0f, 1f);

  /// <summary>Game exposure for a scene average luminance (EXPOSURE_DIV_AVG_LUM).</summary>
  public static float Exposure(float averageLuminance) => Key / MathF.Max(averageLuminance, 1e-6f);
}

/// <summary>GLSL for <see cref="CaptureTonemap"/> (shared by the browser shaders so the constants live in one place).</summary>
public static class CaptureTonemapGlsl
{
  private static string F(float v) => v.ToString("0.0#######", System.Globalization.CultureInfo.InvariantCulture);

  /// <summary>
  ///   Declares <c>uniform int uTonemap</c> (1 = capture curve, 0 = legacy 1-exp) and <c>vec3 sm2Tonemap(vec3 exposed)</c>
  ///   / <c>float sm2TonemapLum(float exposed)</c> returning linear 0..1.
  /// </summary>
  public static string Source =>
    "uniform int uTonemap;\n" +
    $"float sm2Hable(float x) {{ x = max(x, 0.0); return (x * ({F(CaptureTonemap.A)} * x + {F(CaptureTonemap.C * CaptureTonemap.B)}) + {F(CaptureTonemap.D * CaptureTonemap.E)}) / (x * ({F(CaptureTonemap.A)} * x + {F(CaptureTonemap.B)}) + {F(CaptureTonemap.D * CaptureTonemap.F)}) - {F(CaptureTonemap.E / CaptureTonemap.F)}; }}\n" +
    $"float sm2TonemapLum(float x) {{ return uTonemap == 1 ? clamp(sm2Hable(x) * {F(CaptureTonemap.WhitePointScale)}, 0.0, 1.0) : 1.0 - exp(-x); }}\n" +
    "vec3 sm2Tonemap(vec3 x) { return vec3(sm2TonemapLum(x.r), sm2TonemapLum(x.g), sm2TonemapLum(x.b)); }\n";
}
