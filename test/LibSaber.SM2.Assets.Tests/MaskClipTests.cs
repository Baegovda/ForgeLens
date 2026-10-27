using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public class MaskClipTests
{
  private const string Part = """
    emitters2   =   {
       drops   =   {
          isVisible   =   True
          typeParams   =   {
             default   =   {
                visuals   =   {
                   texture   =   {
                      nameTex   =   "part_splash_central_01"
                   }
                   alphaMask   =   {
                      isEnabled   =   True
                      texture   =   {
                         nameTex   =   "part_splash_central_01_am"
                      }
                      clip   =   {
                         power   =   {
                            ageSpline   =   {
                               ageSpline   =   [
                                  0,
                                  0,
                                  1,
                                  0.6
                               ]
                            }
                            __type   =   "local__ageSpline"
                         }
                         softClip   =   0.075
                      }
                   }
                }
             }
             __type   =   "local__default"
          }
          __type   =   "CPU_EMITTER"
       }
    }
    __type   =   "sfx_part"
    """;

  [Fact]
  public void MaskClipSplineIsParsed()
  {
    var def = EffectDefinition.Build("fx_m", new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_m"] = Part })).Resolve("fx_m")!);
    var e = Assert.Single(def.Emitters);
    Assert.Equal("part_splash_central_01_am", e.AlphaMask);
    Assert.NotNull(e.MaskClip);
    Assert.Equal(0f, e.MaskClip!.Sample(0f, 0f), 1e-3f);
    Assert.Equal(0.6f, e.MaskClip.Sample(1f, 0f), 1e-3f);
  }

  [Fact]
  public void MaskClipIsOptIn()
  {
    if (Environment.GetEnvironmentVariable("SM2_VFX_MASK_CLIP") is null) Assert.False(EmitterDef.MaskClipEnabled);
  }
}
