using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>vfx3m: FUN_1417e1e50 blend flag bits and particles.sdc 00000_0 output.</summary>
public class BlendReaTests
{
  [Theory]
  [InlineData(EffectBlend.Blend, false, 0x400u)]
  [InlineData(EffectBlend.Blend, true, 0x000u)]
  [InlineData(EffectBlend.BlendAdd, false, 0x400u)]
  [InlineData(EffectBlend.BlendAdd, true, 0x000u)]
  [InlineData(EffectBlend.Add, false, 0x420u)]
  [InlineData(EffectBlend.Add, true, 0x420u)]
  public void FlagsMatchExe(EffectBlend b, bool mul, uint expected) => Assert.Equal(expected, EffectBlendRea.Flags(b, mul));

  [Fact]
  public void AddWritesZeroAlpha()
  {
    var (rgb, a) = EffectBlendRea.Output(EffectBlendRea.Flags(EffectBlend.Add, false), 0.5f, 0.25f);
    Assert.Equal(0.5f, rgb);
    Assert.Equal(0f, a);
  }

  [Fact]
  public void AlphaMultiplyScalesColourByTextureAlpha()
  {
    var (rgb, a) = EffectBlendRea.Output(EffectBlendRea.Flags(EffectBlend.Blend, true), 0.5f, 0.25f);
    Assert.Equal(0.125f, rgb);
    Assert.Equal(0.125f, a);
    var (rgb2, a2) = EffectBlendRea.Output(EffectBlendRea.Flags(EffectBlend.Blend, false), 0.5f, 0.25f);
    Assert.Equal(0.5f, rgb2);
    Assert.Equal(0.125f, a2);
  }
}
