using System.Numerics;
using LibSaber.SM2.Assets.Effects;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

public class VatDecodeTests
{
  [Fact]
  public void RowsStepOneFramePerBandFraction()
  {
    var (v0, v1, t) = VatDecode.Rows(0.5f, 3.25f, 119, 8);
    Assert.Equal(0.5f - 3f / 119 / 8, v0, 1e-5f);
    Assert.Equal(0.5f - 4f / 119 / 8, v1, 1e-5f);
    Assert.Equal(0.25f, t, 1e-5f);
    Assert.Equal(-3f / 60, VatDecode.RowV(0.5f, 3, 60, 1), 1e-5f);
  }

  [Fact]
  public void DecodesPositionAndNormal()
  {
    var p = VatDecode.Position(new Vector3(1, 0, 1), new Vector3(-0.5f, -0.5f, -0.0624f), new Vector3(1 / 255f, 1 / 255f, 0.001397f));
    Assert.Equal(0.5f, p.X, 1e-4f); Assert.Equal(-0.5f, p.Y, 1e-4f); Assert.Equal(-0.0624f + 255 * 0.001397f, p.Z, 1e-4f);
    Assert.Equal(1f, VatDecode.Normal(new Vector3(1, 0, 0)).X, 1e-4f);
    Assert.Equal(0f, VatDecode.Normal(new Vector3(128 / 255f)).X, 1e-3f);
    Assert.Equal(118f * 0.5f, VatDecode.StageFrame(0.5f, 119), 1e-4f);
  }
}
