using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>
///   level-lights: CB_COMMON_LIGHTNG P0.w from attenCurveParam / useInverseSquaredFalloff / isConstAtten (packer
///   FUN_14224ecf0, P0.w = FUN_14224f010), checked against the armory capture 128197 eid 9799 lights paired with
///   hub_battle_barge character_customization (cd_list positions, class_list light descs).
/// </summary>
public class LevelLightSoftTests
{
  // (attenCurveParam, Intensity, ColorCtrl rgb, radius, captured P0.w, captured P1)
  public static IEnumerable<object[]> Armory => new List<object[]>
  {
    new object[] { 1f, 8f, 254f, 197f, 113f, 15f, 300f, 7.931144f, 4.534479f, 1.334978f, 2e-05f },    // spot_light_bot_set1
    new object[] { 1f, 35f, 165f, 255f, 247f, 8.4f, 300f, 13.432114f, 35.0f, 32.629692f, 0.000201f }, // spot_light_bot_3
    new object[] { 0f, 180f, 213f, 250f, 253f, 6f, 1f, 121.148857f, 172.326538f, 176.908752f, 0.000772f }, // spot_light_kick_2 (default)
    new object[] { 1f, 400f, 167f, 213f, 252f, 6.66f, 300f, 157.633255f, 269.219696f, 389.720123f, 0.000508f }, // spot_light_bot_5
  };

  [Theory]
  [MemberData(nameof(Armory))]
  public void ArmoryCaptureLightsMatchPacker(float acp, float intensity, float r, float g, float b, float radius, float p0w, float cr, float cg, float cb, float p1w)
  {
    Assert.Equal(p0w, CaptureLightUnits.InverseSquareSoft(acp), 1e-3f);
    var c = CaptureLightUnits.Capture(new Vector3(r, g, b) / 255f, intensity, false, 0.1f);
    Assert.Equal(cr, c.X, cr * 2e-4f);
    Assert.Equal(cg, c.Y, cg * 2e-4f);
    Assert.Equal(cb, c.Z, cb * 2e-4f);
    Assert.Equal(p1w, 1f / MathF.Pow(radius, 4f), 5e-7f);
  }

  [Fact]
  public void SoftFollowsFun14224f010()
  {
    Assert.Equal(1f, CaptureLightUnits.InverseSquareSoft(0f), 1e-6f);
    Assert.Equal(300f, CaptureLightUnits.InverseSquareSoft(1f), 1e-4f);
    Assert.Equal(299f / 16f + 1f, CaptureLightUnits.InverseSquareSoft(0.5f), 1e-4f);
    Assert.Equal(1f / 256f, CaptureLightUnits.InverseSquareSoft(-1f), 1e-7f);
    Assert.Equal(1f / 16f, CaptureLightUnits.InverseSquareSoft(-0.5f), 1e-6f);
    Assert.Equal(0.0025f, CaptureLightUnits.InverseSquareSoft(1f, useInverseSquaredFalloff: true), 1e-7f);
    Assert.Equal(0.0025f, CaptureLightUnits.InverseSquareSoft(1f, true, true), 1e-7f); // bit 1 wins over bit 2
    Assert.Equal(8e22f, CaptureLightUnits.InverseSquareSoft(1f, isConstAtten: true));
    Assert.Equal(0f, CaptureLightUnits.InverseSquareSoft(1f, directional: true));
    Assert.Equal(400f, CaptureLightUnits.LightColorScale(true));
    Assert.Equal(1f, CaptureLightUnits.LightColorScale(false));
  }

  [Fact]
  public void ConstAttenIsAHardSphere()
  {
    var s = CaptureLightUnits.InverseSquareSoft(0f, isConstAtten: true);
    Assert.Equal(1f, CaptureLightUnits.SurfaceAttenuation(4.9f, 5f, s));
    Assert.Equal(0f, CaptureLightUnits.SurfaceAttenuation(5.1f, 5f, s));
  }

  [Fact]
  public void InverseSquaredLightIsPhysicalAtOneMetre()
  {
    // colour x400, P0.w 1/400: 400 * 0.0025 / (1 + 0.0025) ~ 1 at 1 m, ~ 1/d^2 beyond (window aside)
    var s = CaptureLightUnits.InverseSquareSoft(0f, useInverseSquaredFalloff: true);
    Assert.Equal(1f / 1.0025f, CaptureLightUnits.LightColorScale(true) * CaptureLightUnits.InverseSquare(1f, s), 1e-5f);
    Assert.Equal(0.25f, CaptureLightUnits.LightColorScale(true) * CaptureLightUnits.InverseSquare(2f, s), 1e-3f);
  }

  [Fact]
  public void LightDefCarriesDescFlags()
  {
    var d = new LightDef { Name = "l", Path = "fx/l", AttenCurveParam = 1f };
    Assert.Equal(300f, d.InverseSquareSoft, 1e-4f);
    var sample = new LightSample(d, Vector3.Zero, Vector3.One, 10f, 1f);
    Assert.Equal(CaptureLightUnits.Attenuation(2f, 10f) * 300f / 304f, sample.Falloff(2f), 1e-6f);
    var inv = new LightDef { Name = "l", Path = "fx/l", UseInverseSquaredFalloff = true, Adaptive = false };
    Assert.Equal(400f, new LightSample(inv, Vector3.Zero, Vector3.One, 3f, 1f).Relative(1f).X, 1e-3f);
    var a = VfxFrameExport.Lights([new LightSample(d, Vector3.Zero, Vector3.One, 3f, 10f)], 1f);
    Assert.Equal(300f, (float)a[0]!["inverseSquareSoft"]!, 1e-3f);
    var k = VfxFrameExport.Lights([new LightSample(new LightDef { Name = "c", Path = "fx/c", IsConstAtten = true }, Vector3.Zero, Vector3.One, 3f, 10f)], 1f);
    Assert.Equal(-1f, (float)k[0]!["inverseSquareSoft"]!);
  }
}
