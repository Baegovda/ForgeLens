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
  public void DistortionEncodeAndPost()
  {
    Assert.Equal(0f, DistortionRules.Shift(0f, DistortionRules.Mask(0f, 0f)), 1e-6f);
    // mask 1 (|off| >= 0.1): shift = 0.08 * off.
    Assert.Equal(0.008f, DistortionRules.Shift(0.1f, DistortionRules.Mask(0.1f, 0f)), 1e-6f);
    Assert.Equal(-0.016f, DistortionRules.Shift(-0.2f, 1f), 1e-6f);
    // encoding range 0..1 caps the screen shift at +-0.02.
    Assert.Equal(0.02f, DistortionRules.Shift(5f, 1f), 1e-6f);
    Assert.Equal(-0.02f, DistortionRules.PostShift(0f), 1e-6f);
    Assert.Equal(0f, DistortionRules.PostShift(0.5f), 1e-6f);
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
}
