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
    // plasma 101917 SFX meshes: additive, destination alpha kept = SFX_BLEND_ADD_NO_ALPHA PSO (PROVEN state, mtl1-38 psos);
    // material -> define selection not traced (HEURISTIC: every sfx mesh).
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
///   Screen distortion, plasma 101917: particle PS 4996 writes DUDV (R10G10B10A2, One/InvSrcAlpha, alpha Zero/Zero) =
///   mask * (0.5 + 2*offset), mask = sat(10*|offset|) * depthFade, offset = normal.xy * TEXCOORD0.w (per-particle strength
///   x fade); post PS 5160 (capture disasm 5160_ps.txt l.30-64): uv += DUDV.xy * 0.0400391 - 0.0200391, applied only when
///   the source DUDV is non-neutral (|DUDV.xy * 2.00196 - 1.00196|^2 > 0) AND the DUDV alpha sampled at the shifted uv is 0.
///   The DUDV target is cleared to alpha 1 and every distortion particle writes alpha 0 (blend alpha Zero/Zero; capture
///   dudv.png: 21726 alpha-0 pixels, all 4231 shifted pixels inside them), so distortion only pulls colour from inside the
///   distortion footprint (<see cref="Covered"/>). Over the neutral 0.5 buffer the screen shift is 0.08 * mask * offset,
///   at most +-0.02 of the screen. Both PS 5160 samples (DUDV and DISTORT_FRAMEBUF) use PS_SAMPLERS[4]; the same slot is
///   used with textureGrad (particles PS 281 l.260/264) and for the per-particle light atlas, so it filters: the resample
///   is bilinear (INFERRED from static usage; the sampler descriptor table itself was not read).
/// </summary>
public static class DistortionRules
{
  /// <summary>Screen-UV shift per unit offset (2 x 0.04).</summary>
  public const float UvPerOffset = 0.08f;
  /// <summary>Largest shift the 0..1 DUDV encoding can produce (0.04 x 0.5).</summary>
  public const float MaxShift = 0.02f;
  /// <summary>PS 5160 post constants (exact 10-bit UNORM rescale of 0.04 / -0.02).</summary>
  public const float PostScale = 0.0400391f, PostBias = -0.0200391f;
  /// <summary>Sampler of the post resample (PS_SAMPLERS[4]): bilinear, INFERRED.</summary>
  public const bool BilinearResample = true;

  public static float Mask(float offsetX, float offsetY, float depthFade = 1f)
    => Math.Clamp(10f * (MathF.Abs(offsetX) + MathF.Abs(offsetY)), 0f, 1f) * depthFade;

  /// <summary>Encoded DUDV channel for one particle (before blending), PS 4996.</summary>
  public static float Encode(float offset, float mask) => mask * (0.5f + 2f * offset);

  /// <summary>Post-process uv shift from a DUDV channel value, PS 5160.</summary>
  public static float PostShift(float dudv) => dudv * PostScale + PostBias;

  /// <summary>DUDV value with zero shift: the 10-bit UNORM code 512 (PostShift(512/1023) = 0; the gate tests the same point).</summary>
  public const float Neutral = 512f / 1023f;

  /// <summary>
  ///   Screen shift of one particle over the neutral buffer: nominal decode 0.04 * (Encode + (1-mask)*0.5) - 0.02, clamped
  ///   (= 0.08 * mask * offset; the exact constants add the 10-bit rescale, +0.1 %, and move the neutral to 512/1023).
  /// </summary>
  public static float Shift(float offset, float mask)
    => Math.Clamp(0.04f * (Encode(offset, mask) + (1f - mask) * 0.5f) - 0.02f, -MaxShift, MaxShift);

  /// <summary>
  ///   PS 5160 gate: the shift is applied only if the target texel lies inside the distortion footprint (bilinear DUDV
  ///   alpha == 0, i.e. every filter tap with non-zero weight was drawn by a distortion particle).
  /// </summary>
  public static bool Covered(Func<int, int, bool> drawn, float x, float y)
  {
    var fx = x - 0.5f; var fy = y - 0.5f; var x0 = (int)MathF.Floor(fx); var y0 = (int)MathF.Floor(fy);
    float wx = fx - x0, wy = fy - y0;
    for (var j = 0; j < 2; j++)
      for (var i = 0; i < 2; i++)
        if ((i == 0 ? 1 - wx : wx) * (j == 0 ? 1 - wy : wy) > 0 && !drawn(x0 + i, y0 + j)) return false;
    return true;
  }
}

/// <summary>
///   mtl1-40: per-capture HDR state (CB_COMMON, identical in every draw of a capture; /workspace/rdc/&lt;scene&gt;/cbuffers):
///   COMMON_HDR_AVERAGE_LUMINANCE(_CLAMPED) and COMMON_HDR_EXPOSURE_DIV_AVG_LUM (= final tonemap HDR_TONEMAP_EXPOSURE,
///   smoke eid 3500 0.023784, LUT bake eid 3226 0.023783), whitepoint from CB_PASS_HDR.
///   Particle VS 390 / 248 multiply unlit colour and the emissive term by COMMON_HDR_AVERAGE_LUMINANCE (VS 390 GLSL l.490-492,
///   788-790: COLOR * TEXCOORD_1 * CB_COMMON[22].y) and the final tonemap multiplies by EXPOSURE_DIV_AVG_LUM, so an unlit /
///   emissive particle displays colour * intensity * Key with Key = exposure * avgLum (PROVEN). Key is NOT constant:
///   0.112-0.115 in the three combat captures, outdoor 0.074, armory 0.025 (the old "0.11 in every scene" was wrong).
///   The histogram auto-exposure that picks EXPOSURE_DIV_AVG_LUM (hdr CS 36: 64 log2 bins of max(rgb); CS 20 merge; debug
///   PS 104 corr = (2 - 4/(AUTO_K*log10(SCALE_X*L+1)+2))*SCALE_Y) runs its final mapping on the CPU: not traced.
/// </summary>
public sealed record CaptureScene(string Name, int Frame, float AverageLuminance, float AverageLuminanceClamped, float ExposureDivAvgLum, float WhitePoint)
{
  /// <summary>Display key: exposure x average luminance (what an unlit particle of HDR 1/avgLum shows).</summary>
  public float Key => ExposureDivAvgLum * AverageLuminance;
}

public static class CaptureScenes
{
  public static readonly CaptureScene Hellbrute = new("hellbrute", 68141, 3.66142f, 3.66142f, 0.030691f, 1.375385f);
  public static readonly CaptureScene Smoke = new("smoke", 88302, 4.818526f, 4.818526f, 0.023783f, 1.375385f);
  public static readonly CaptureScene Plasma = new("plasma", 101917, 4.150339f, 4.150339f, 0.027324f, 1.375385f);
  public static readonly CaptureScene Armory = new("armory", 128197, 0.108577f, 0.108577f, 0.225831f, 1.375385f);
  public static readonly CaptureScene Outdoor = new("outdoor", 137266, 2.287158f, 2.287158f, 0.03224f, 1.343645f);
  public static IReadOnlyList<CaptureScene> All { get; } = [Hellbrute, Smoke, Plasma, Armory, Outdoor];

  /// <summary>Reference scene of the bench look: the grenade smoke capture (the 1/250 bench exposure was tuned on grenade shots).</summary>
  public static CaptureScene Reference => Smoke;

  public static CaptureScene? Find(string? name) => All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
///   mtl1-40: frame exposure from a capture's average luminance. Bench / UE frame HDR is in particle-relative units
///   (game HDR / avgLum: unlit sprite = colour * intensity, lights relative to the ambient = avgLum, CaptureLightUnits), so
///   its exposure is the scene Key times one bench scale: exposure = Key(scene) * BenchScale, BenchScale = (1/250) /
///   Key(smoke). The CPU colour -> vertex scale (what TEXCOORD_1.w holds) is untraced, so BenchScale is HEURISTIC (it
///   keeps the tuned 1/250 look for the reference scene); the per-scene ratio Key(scene)/Key(smoke) is PROVEN capture data.
/// </summary>
public static class CaptureExposure
{
  public const float ReferenceExposure = 1f / 250f;
  public static float BenchScale => ReferenceExposure / CaptureScenes.Reference.Key;

  /// <summary>Particle-relative frame exposure for a scene.</summary>
  public static float Frame(CaptureScene scene) => scene.Key * BenchScale;

  /// <summary>
  ///   SFX emissive exposure in particle-relative units: the emissive is not multiplied by avgLum like particles, but by
  ///   lerp(1, avgLumClamped, k) (SfxExposure), so relative to particles it scales by lerp(1, avgLumClamped, k) / avgLum.
  /// </summary>
  public static float Sfx(float frameExposure, CaptureScene scene, float k)
    => frameExposure * SfxExposure.Scale(scene.AverageLuminanceClamped, k) / MathF.Max(scene.AverageLuminance, 1e-6f);
}

/// <summary>
///   CB_PASS_HDR tonemap (identical in all five captures), LUT bake CS s1996: x = linear HDR * exposure;
///   F(x) = (x(Ax+CB)+DE)/(x(Ax+B)+DF) - E/F (Uncharted 2 / Hable) with A..D = HDR_TONEMAP_CURVE_PARAMS_0 [0.12,0.05,0.45,0.2],
///   E,F = _1 [0.02,0.08]; out = sat(F(x) * WHITEPOINT_SCALE 1.375385 (outdoor 1.343645)), then sRGB and the grading LUT
///   (LUT_RANGE_LOG 14, GREY_POINT 0.43, LUT input x = 0.18*2^((i/31 - grey)*rangeLog) - 0.18*2^(-grey*rangeLog)).
///   Exposure: CB_COMMON EXPOSURE_DIV_AVG_LUM; EXPOSURE_DIV_AVG_LUM x AVG_LUM (the key) is 0.112-0.115 in the three combat
///   captures but 0.074 outdoor and 0.025 in the armory (mtl1-40 correction, <see cref="CaptureScenes"/>). Key 0.11 is the
///   combat-scene value: HEURISTIC for other scenes; the histogram auto-exposure CPU mapping was not traced.
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

  /// <summary>Game exposure for a combat-scene average luminance (EXPOSURE_DIV_AVG_LUM ~ 0.11 / avgLum; see CaptureScenes for measured keys).</summary>
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

/// <summary>
///   mtl1-36: sfx_light units calibrated against capture CB_COMMON_LIGHTNG (plasma 101917, GPU particle CS s4462).
///   Per light i, COMMON_DYN_LIGHT_PARAMS[4i..4i+3] = (pos, mult), (colour HDR, 1/r^4), (spot dir, offset), (spot scale,
///   shadow idx, ..); particle light = colour * mult * sat(1 - d^4 / r^4)^2 (no 1/pi), unlit particles use
///   COMMON_HDR_AVERAGE_LUMINANCE instead (s4462 _label167), so "ambient" = avgLum in the same HDR units.
///   Capture match: fx_plasma_exterminator_shot_venting (r 24 exact, colour (94,120,152) sRGB-decoded ratio within 1.3%):
///   CB colour 274.0 / 269.0 vs srgbToLinear(152/255) * 200 (Intensity at t=0) * avgLum 4.150 = 260.5 (-5% / -3%);
///   fx_plasma_charging_side (r 2 exact, ratio within 1.5%): 1145 = lin * 569 * avgLum, 569 inside its 150..4448 spline.
///   So capture HDR = srgbToLinear(ColorCtrl) * Intensity * (isAdaptiveIntensity ? avgLum : 1). The adaptive scale is
///   inferred from one light at one avgLum (HEURISTIC for the factor itself; non-adaptive lights were not in a capture).
///   With exposure = key / avgLum an adaptive light's exposed value is lin * I * key, independent of scene brightness.
/// </summary>
public static class CaptureLightUnits
{
  /// <summary>Bench sprite ambient HDR (EffectRenderer mesh/sprite "200") that stands for capture avgLum.</summary>
  public const float BenchAmbient = 200f;
  /// <summary>Diffuse albedo used to light the bench ground/cover relative to its ambient (HEURISTIC 0.18, grey card).</summary>
  public const float SurfaceAlbedo = 0.18f;
  /// <summary>Ground/cover light per unit relative light: albedo / pi (Lambert; the surface BRDF itself is HEURISTIC).</summary>
  public const float SurfaceScale = SurfaceAlbedo / MathF.PI;
  /// <summary>Default for lights without isAdaptiveIntensity (233 of 270 authored flags are True; HEURISTIC).</summary>
  public const bool DefaultAdaptive = true;

  public static float SrgbToLinear(float c)
  {
    c = Math.Clamp(c, 0f, 1f);
    return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
  }

  public static System.Numerics.Vector3 Linear(System.Numerics.Vector3 srgb) => new(SrgbToLinear(srgb.X), SrgbToLinear(srgb.Y), SrgbToLinear(srgb.Z));

  /// <summary>Capture HDR colour (CB_COMMON_LIGHTNG PARAMS[4i+1].rgb) for an authored colour/intensity.</summary>
  public static System.Numerics.Vector3 Capture(System.Numerics.Vector3 srgb, float intensity, bool adaptive, float averageLuminance)
    => Linear(srgb) * intensity * (adaptive ? averageLuminance : 1f);

  /// <summary>Light relative to the particle ambient (capture HDR / avgLum): what the bench and UE export use.</summary>
  public static System.Numerics.Vector3 Relative(System.Numerics.Vector3 srgb, float intensity, bool adaptive, float averageLuminance)
    => Linear(srgb) * intensity * (adaptive ? 1f : 1f / MathF.Max(averageLuminance, 1e-6f));

  /// <summary>s4462 distance attenuation: sat(1 - d^4 / r^4)^2.</summary>
  public static float Attenuation(float distance, float radius)
  {
    var q = distance / MathF.Max(radius, 1e-4f); q *= q;
    var a = Math.Clamp(1f - q * q, 0f, 1f);
    return a * a;
  }

  /// <summary>CB P1.w = 1/r^4 back to the radius.</summary>
  public static float RadiusFromInvR4(float invR4) => invR4 > 0 ? MathF.Pow(invR4, -0.25f) : 0f;

  /// <summary>Bench average luminance for a bench exposure (ReferenceExposure = avgLum 1, as SfxExposure).</summary>
  public static float BenchAverageLuminance(float exposure, float referenceExposure) => referenceExposure / MathF.Max(exposure, 1e-12f);
}
