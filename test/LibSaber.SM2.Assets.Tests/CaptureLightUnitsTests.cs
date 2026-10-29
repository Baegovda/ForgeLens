using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-36: VFX light units calibrated against CB_COMMON_LIGHTNG in the plasma capture (s4462).</summary>
public class CaptureLightUnitsTests
{
  [Fact]
  public void AttenuationIsSat1MinusD4OverR4Squared()
  {
    Assert.Equal(1f, CaptureLightUnits.Attenuation(0f, 5f), 1e-6f);
    Assert.Equal(0f, CaptureLightUnits.Attenuation(5f, 5f), 1e-6f);
    Assert.Equal(0f, CaptureLightUnits.Attenuation(9f, 5f), 1e-6f);
    var a = 1f - MathF.Pow(0.5f, 4f);
    Assert.Equal(a * a, CaptureLightUnits.Attenuation(2.5f, 5f), 1e-6f);
  }

  [Fact]
  public void SurfaceFalloffAddsSoftenedInverseSquare()
  {
    // rea-batch 2: s29889/s4462/s5611 multiply the window by P0.w / (d^2 + P0.w); P0.w = 1 on captured sfx lights.
    Assert.Equal(1f, CaptureLightUnits.SurfaceAttenuation(0f, 5f), 1e-6f);
    var a = 1f - MathF.Pow(0.5f, 4f);
    Assert.Equal(a * a / (2.5f * 2.5f + 1f), CaptureLightUnits.SurfaceAttenuation(2.5f, 5f), 1e-6f);
    Assert.Equal(300f / (4f + 300f), CaptureLightUnits.InverseSquare(2f, 300f), 1e-6f);
  }

  [Fact]
  public void RadiusFromInvR4RoundTrips()
  {
    Assert.Equal(24f, CaptureLightUnits.RadiusFromInvR4(1f / MathF.Pow(24f, 4f)), 1e-3f);
    Assert.Equal(0f, CaptureLightUnits.RadiusFromInvR4(0f));
  }

  [Fact]
  public void PlasmaVentingFitsCaptureWithinSixPercent()
  {
    // fx_plasma_exterminator_shot_venting: ColorCtrl (94,120,152), intensity 200, adaptive; capture avgLum 4.150339, CB blue 274.0 / 269.0.
    var c = CaptureLightUnits.Capture(new Vector3(94, 120, 152) / 255f, 200f, true, 4.150339f);
    Assert.InRange(c.Z, 274f * 0.94f, 274f * 1.06f);
    Assert.InRange(c.Z, 269f * 0.94f, 269f * 1.06f);
    var rel = CaptureLightUnits.Relative(new Vector3(94, 120, 152) / 255f, 200f, true, 4.150339f);
    Assert.Equal(c.Z / 4.150339f, rel.Z, 1e-3f);
  }

  [Fact]
  public void NonAdaptiveRelativeDividesByAverageLuminance()
  {
    var r = CaptureLightUnits.Relative(Vector3.One, 8f, false, 4f);
    Assert.Equal(2f, r.X, 1e-5f);
    Assert.Equal(8f, CaptureLightUnits.Capture(Vector3.One, 8f, false, 4f).X, 1e-5f);
    Assert.Equal(0.5f, CaptureLightUnits.SrgbToLinear(CaptureLightUnits.SrgbToLinear(1f) * 0f + 0.7354f), 2e-3f);
  }

  [Fact]
  public void ExportedLightColorIsRelative()
  {
    var def = new LightDef { Name = "l", Path = "fx/l", Adaptive = false };
    var a = VfxFrameExport.Lights([new LightSample(def, Vector3.Zero, Vector3.One, 3f, 10f)], 5f);
    Assert.Equal(2f, (float)a[0]!["color"]![0]!, 1e-4f);
    Assert.False((bool)a[0]!["adaptive"]!);
  }
}
