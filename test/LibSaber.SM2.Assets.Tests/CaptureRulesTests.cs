using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-23: RenderDoc capture rules (blend states, SFX exposure, distortion, CB_PASS_HDR tonemap).</summary>
public class CaptureRulesTests
{
  [Fact]
  public void BlendStatesMatchCapture()
  {
    Assert.Equal(new VfxBlendState(true, "One", "InvSrcAlpha", "One", "InvSrcAlpha"), VfxBlendRules.For(VfxDrawKind.CpuParticle));
    Assert.Equal(new VfxBlendState(true, "SrcAlpha", "InvSrcAlpha", "Zero", "One"), VfxBlendRules.For(VfxDrawKind.Trail));
    Assert.False(VfxBlendRules.For(VfxDrawKind.Spark).Enabled);
    Assert.Equal(new VfxBlendState(true, "One", "One", "Zero", "One"), VfxBlendRules.For(VfxDrawKind.SfxMesh));
  }

  [Fact]
  public void SfxExposureCompensation()
  {
    Assert.Equal(1f, SfxExposure.Scale(3.66f, 0f), 1e-5f);
    Assert.Equal(3.66f, SfxExposure.Scale(3.66f, 1f), 1e-5f);
    // k = 1: emissive exposure independent of the frame exposure.
    Assert.Equal(1f / 250f, SfxExposure.Exposure(1f / 1000f, 1f / 250f, 1f), 1e-6f);
    Assert.Equal(1f / 250f, SfxExposure.Exposure(1f / 30f, 1f / 250f, 1f), 1e-6f);
    Assert.Equal(1f / 1000f, SfxExposure.Exposure(1f / 1000f, 1f / 250f, 0f), 1e-6f);
  }

  [Fact]
  public void SfxAdaptiveIntensityIsK()
  {
    // mtl1-50b PROVEN: sfx[560].w = emissive.adaptiveIntensity ? 1 : 0 (FUN_1424540e0); preset default false.
    const string on = "preset = \"sfx\"\nshaders = { sfx = { emissive = { intensity = 55.000000 adaptiveIntensity = true bloomIntensity = 2.0 } tint = [1,1,1,1] } }";
    Assert.True(SfxExposure.AdaptiveFromTd(on));
    Assert.False(SfxExposure.AdaptiveFromTd("shaders = { sfx = { emissive = { intensity = 3.0 bloomIntensity = 1.0 } } }"));
    Assert.False(SfxExposure.AdaptiveFromTd("shaders = { sfx = { emissive = { adaptiveIntensity = false } } }"));
    Assert.True(SfxExposure.AdaptiveFromTd("shaders = { sfx = { emissive.adaptiveIntensity = true } }"));
    // only the sfx block's own top-level emissive counts
    Assert.False(SfxExposure.AdaptiveFromTd("shaders = { sfx = { layers = { l0 = { adaptiveIntensity = true } } } }"));
    Assert.False(SfxExposure.AdaptiveFromTd("lights = { adaptiveIntensity = true }"));
    Assert.False(SfxExposure.AdaptiveFromTd(null));
    if (SfxExposure.ForcedK is null)
    {
      Assert.Equal(1f, SfxExposure.K(true));
      Assert.Equal(0f, SfxExposure.K(false));
    }
  }

  [Fact]
  public void DistortionEncodeAndPost()
  {
    Assert.Equal(0f, DistortionRules.Shift(0f, DistortionRules.Mask(0f, 0f)), 1e-6f);
    // mask 1 (|off| >= 0.1): shift = 0.08 * off.
    Assert.Equal(0.008f, DistortionRules.Shift(0.1f, DistortionRules.Mask(0.1f, 0f)), 1e-6f);
    Assert.Equal(-0.016f, DistortionRules.Shift(-0.2f, 1f), 1e-6f);
    // encoding range 0..1 caps the screen shift at +-0.02.
    Assert.Equal(0.02f, DistortionRules.Shift(5f, 1f), 1e-6f);
    Assert.Equal(-0.0200391f, DistortionRules.PostShift(0f), 1e-7f);
    Assert.Equal(0f, DistortionRules.PostShift(DistortionRules.Neutral), 1e-7f);
    Assert.Equal(0.02f, DistortionRules.PostShift(1f), 1e-7f);
    Assert.Equal(0.5f, DistortionRules.Mask(0.03f, 0.02f), 1e-5f);
    Assert.Equal(0.25f, DistortionRules.Mask(0.5f, 0f, 0.25f), 1e-5f);
  }

  [Fact]
  public void HableCurveFromCapture()
  {
    Assert.Equal(0f, CaptureTonemap.Apply(0f), 1e-6f);
    Assert.Equal(1f, CaptureTonemap.Apply(1000f), 1e-6f);
    // shoulder: F(inf) * W = 0.75 * 1.375385 > 1, white point near x = 11.2 (classic Hable W).
    Assert.InRange(CaptureTonemap.Curve(11.2f) * CaptureTonemap.WhitePointScale, 0.995f, 1.005f);
    // toe slope F'(0) * W ~ 0.86.
    Assert.InRange(CaptureTonemap.Apply(1e-3f) / 1e-3f, 0.84f, 0.88f);
    Assert.Equal(0.11f / 3.661f, CaptureTonemap.Exposure(3.661f), 1e-6f);
  }

  [Fact]
  public void GlslUsesSameConstants()
  {
    var src = CaptureTonemapGlsl.Source;
    Assert.Contains("0.12 * x + 0.0225", src);
    Assert.Contains("0.12 * x + 0.05", src);
    Assert.Contains("1.375385", src);
    Assert.Contains("- 0.25", src);
  }

  [Fact]
  public void CaptureSceneKeysFromAverageLuminance()
  {
    Assert.Equal(0.1124f, CaptureScenes.Hellbrute.Key, 1e-3f);
    Assert.Equal(0.1146f, CaptureScenes.Smoke.Key, 1e-3f);
    Assert.Equal(0.1134f, CaptureScenes.Plasma.Key, 1e-3f);
    Assert.Equal(0.0737f, CaptureScenes.Outdoor.Key, 1e-3f);
    Assert.Equal(0.0245f, CaptureScenes.Armory.Key, 1e-3f);
    Assert.Equal(1.343645f, CaptureScenes.Outdoor.WhitePoint, 1e-6f);
    Assert.Same(CaptureScenes.Plasma, CaptureScenes.Find("PLASMA"));
    Assert.Null(CaptureScenes.Find("bench"));
  }

  [Fact]
  public void FrameExposureKeepsReferenceAndScalesByKey()
  {
    Assert.Equal(1f / 250f, CaptureExposure.Frame(CaptureScenes.Smoke), 1e-7f);
    Assert.Equal(CaptureScenes.Outdoor.Key / CaptureScenes.Smoke.Key,
      CaptureExposure.Frame(CaptureScenes.Outdoor) / CaptureExposure.Frame(CaptureScenes.Smoke), 1e-5f);
    Assert.InRange(CaptureExposure.Frame(CaptureScenes.Armory) * 250f, 0.21f, 0.22f);
    // sfx k = 1: emissive scales by avgLumClamped / avgLum = 1 relative to particles; k = 0: 1 / avgLum.
    Assert.Equal(0.004f, CaptureExposure.Sfx(0.004f, CaptureScenes.Smoke, 1f), 1e-7f);
    Assert.Equal(0.004f / 4.818526f, CaptureExposure.Sfx(0.004f, CaptureScenes.Smoke, 0f), 1e-7f);
  }

  [Fact]
  public void DistortionGateNeedsEveryBilinearTap()
  {
    Assert.Equal(0.0400391f, DistortionRules.PostScale, 1e-7f);
    Assert.Equal(-0.0200391f, DistortionRules.PostBias, 1e-7f);
    Assert.True(DistortionRules.BilinearResample);
    bool Drawn(int x, int y) => x >= 4 && x < 8 && y >= 0 && y < 8;
    Assert.True(DistortionRules.Covered(Drawn, 6.5f, 3.5f));   // texel centre: one tap
    Assert.True(DistortionRules.Covered(Drawn, 7.0f, 3.5f));   // between texels 6 and 7
    Assert.False(DistortionRules.Covered(Drawn, 7.75f, 3.5f)); // taps 7 and 8: 8 not drawn
    Assert.True(DistortionRules.Covered(Drawn, 4.5f, 3.5f));
    Assert.False(DistortionRules.Covered(Drawn, 4.25f, 3.5f)); // taps 3 and 4
  }
}
