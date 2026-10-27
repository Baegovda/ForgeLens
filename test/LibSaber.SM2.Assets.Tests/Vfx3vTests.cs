using LibSaber.SM2.Assets.Effects;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

public class Vfx3vTests
{
  [Fact]
  public void StepDefaultsTo60Hz()
  {
    if (Environment.GetEnvironmentVariable("SM2_VFX_STEP_HZ") is null) Assert.Equal(1f / 60f, EffectInstance.Step, 1e-6f);
  }

  [Fact]
  public void ForceVisibleIsOptIn()
  {
    if (Environment.GetEnvironmentVariable("SM2_VFX_FORCE_VISIBLE") is null) Assert.False(EffectSprites.ForceVisible);
  }
}
