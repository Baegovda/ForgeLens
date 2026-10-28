using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-35: TONEMAP_LUT bake (CS s1996), content LUT layout, strip layout, lights / mesh-batch export.</summary>
public class CaptureGradingTests
{
  [Fact]
  public void IdentityBakeIsSrgbHableAtTheNodes()
  {
    var lut = CaptureLut.Bake(GradingLut.Identity());
    for (var i = 0; i < CaptureLut.Size; i += 5)
    {
      var x = CaptureLut.Input(i / 31f);
      var want = VfxFrameExport.Srgb(Math.Clamp(CaptureTonemap.Curve(x) * CaptureTonemap.WhitePointScale, 0f, 1f));
      var o = ((i * 32 + i) * 32 + i) * 3;
      Assert.Equal(want, lut.Rgb[o], 2e-3f);
      Assert.Equal(want, lut.Rgb[o + 1], 2e-3f);
    }
  }

  [Fact]
  public void CoordinateInvertsInput()
  {
    foreach (var u in new[] { 0f, 0.1f, 0.43f, 0.7f, 1f })
      Assert.Equal(u, CaptureLut.Coordinate(CaptureLut.Input(u)), 1e-4f);
    Assert.Equal(0f, CaptureLut.Input(0f), 1e-7f);
  }

  [Fact]
  public void ApplyThroughIdentityBakeTracksExactTonemap()
  {
    var lut = CaptureLut.Bake();
    foreach (var x in new[] { 0.02f, 0.18f, 0.5f, 1f, 3f })
    {
      var want = VfxFrameExport.Srgb(Math.Clamp(CaptureTonemap.Curve(x) * CaptureTonemap.WhitePointScale, 0f, 1f));
      Assert.Equal(want, CaptureLut.Apply(lut, x, x, x).R, 0.01f);
    }
  }

  [Fact]
  public void BgraLayoutIsRFastestThenGThenB()
  {
    // neutral_lut layout: texel (r, g, b) at ((b * 32 + g) * 32 + r) * 4, BGRA.
    var px = new byte[32 * 32 * 32 * 4];
    for (var b = 0; b < 32; b++)
    for (var g = 0; g < 32; g++)
    for (var r = 0; r < 32; r++)
    {
      var o = ((b * 32 + g) * 32 + r) * 4;
      px[o] = (byte)(b * 8); px[o + 1] = (byte)(g * 8); px[o + 2] = (byte)(r * 8); px[o + 3] = 255;
    }
    var lut = GradingLut.FromBgra(px);
    var (cr, cg, cb) = lut.Sample(3.5f / 32, 7.5f / 32, 11.5f / 32);
    Assert.Equal(24 / 255f, cr, 1e-5f); Assert.Equal(56 / 255f, cg, 1e-5f); Assert.Equal(88 / 255f, cb, 1e-5f);
  }

  [Fact]
  public void StripLayoutIsRPlus32BAcrossGDown()
  {
    var strip = CaptureLut.Strip(GradingLut.Identity());
    Assert.Equal(1024 * 32 * 4, strip.Length);
    int r = 5, g = 9, b = 20;
    var d = (g * 1024 + b * 32 + r) * 4;
    Assert.Equal((byte)MathF.Round(b / 31f * 255), strip[d]);
    Assert.Equal((byte)MathF.Round(g / 31f * 255), strip[d + 1]);
    Assert.Equal((byte)MathF.Round(r / 31f * 255), strip[d + 2]);
  }

  [Fact]
  public void LightsAndTriangleBatchJson()
  {
    var def = new LightDef { Name = "glow", Path = "fx/glow" };
    var a = VfxFrameExport.Lights([new LightSample(def, new Vector3(1, 2, 3), new Vector3(1, 0.5f, 0), 4f, 2f), new LightSample(def, Vector3.Zero, Vector3.One, 0f, 1f)]);
    Assert.Single(a);
    Assert.Equal(2f, (float)a[0]!["color"]![0]!, 1e-5f);
    Assert.Equal(4f, (float)a[0]!["radius"]!, 1e-5f);

    var v = new float[3 * SpriteBatch.Stride];
    var solid = VfxFrameExport.TriangleBatch("m", "fx/m", false, "t", 0f, v, [0, 1, 2], "n");
    Assert.Equal("Solid", (string)solid["ueBlend"]!);
    Assert.Equal(1, (int)solid["triangles"]!);
    var add = VfxFrameExport.TriangleBatch("m", "fx/m", true, "t", 0f, v, [0, 1, 2], "n");
    Assert.Equal("Additive", (string)add["ueBlend"]!);
  }

  [Fact]
  public void RuleSceneSoftDepthFades()
  {
    var e = VfxRuleScene.Build()["expect"]!.AsArray().ToDictionary(x => (string)x!["id"]!, x => (float)x!["value"]!);
    Assert.True(e["soft0"] < e["soft1"] && e["soft1"] < e["soft2"]);
  }

  [Theory]
  [InlineData(0f)]
  [InlineData(0.01f)]
  [InlineData(0.18f)]
  [InlineData(1f)]
  [InlineData(37f)]
  [InlineData(5000f)]
  public void CoordinateMatchesHdrPs44Uvw(float c)
  {
    // mtl1-38 PROVEN hdr PS 44/47: uvw = sat(0.43 + log2((c + 0.18*2^(-0.43*14))/0.18)/14) * 31/32 + 1/64.
    var ps = Math.Clamp(0.43 + Math.Log2((c + 0.18 * Math.Pow(2, -0.43 * 14)) / 0.18) / 14, 0, 1) * 31.0 / 32 + 1.0 / 64;
    var ours = CaptureLut.Coordinate(c) * (CaptureLut.Size - 1f) / CaptureLut.Size + 0.5f / CaptureLut.Size;
    Assert.Equal(ps, (double)ours, 1e-5);
  }
}
