using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public class CoveringTintTests
{
  private const string Part = """
    timeLife   =   1
    emitters2   =   {
       em_covering_blood   =   {
          isVisible   =   True
          physics   =   {
             cdt   =   {
                coveringInteraction   =   {
                   radiusScale   =   0.4
                }
             }
          }
          __type   =   "CPU_EMITTER"
       }
       smoke   =   {
          isVisible   =   True
          emission   =   {
             type   =   {
                explosion   =   {
                   numPartsToEmit   =   [
                      2,
                      2
                   ]
                }
                __type   =   "local__explosion"
             }
          }
          particleParams   =   {
             timeLife   =   [
                1,
                1
             ]
          }
          typeParams   =   {
             default   =   {
                visuals   =   {
                   texture   =   {
                      nameTex   =   "seq_fin_expl_g"
                   }
                   textureTintControl   =   1
                   isBlendAlphaMultiply   =   False
                }
             }
             __type   =   "local__default"
          }
          __type   =   "CPU_EMITTER"
       }
       spark   =   {
          isVisible   =   True
          emission   =   {
             type   =   {
                explosion   =   {
                   numPartsToEmit   =   [
                      1,
                      1
                   ]
                }
                __type   =   "local__explosion"
             }
          }
          typeParams   =   {
             default   =   {
                visuals   =   {
                   texture   =   {
                      nameTex   =   "spark_01"
                   }
                }
             }
             __type   =   "local__default"
          }
          __type   =   "CPU_EMITTER"
       }
       gpu_default   =   {
          isVisible   =   True
          visuals   =   {
             texture   =   {
                nameTex   =   "spark_01"
             }
          }
          __type   =   "GPU_EMITTER"
       }
       gpu_ratio   =   {
          isVisible   =   True
          visuals   =   {
             transforms   =   {
                streaks   =   {
                   ratio   =   0.5
                }
             }
          }
          __type   =   "GPU_EMITTER"
       }
       gpu_off   =   {
          isVisible   =   True
          visuals   =   {
             transforms   =   {
                streaks   =   {
                   isEnabled   =   False
                   ratio   =   0.5
                }
             }
          }
          __type   =   "GPU_EMITTER"
       }
    }
    __type   =   "sfx_part"
    """;

  private static EffectDefinition Def() =>
    EffectDefinition.Build("fx_c", new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_c"] = Part })).Resolve("fx_c")!);

  [Fact]
  public void CoveringStubIsReportedNotUnsupported()
  {
    var def = Def();
    var cov = Assert.Single(def.Coverings);
    Assert.Equal("blood", cov.Kind);
    Assert.Equal(0.4f, cov.RadiusScale, 0.001f);
    Assert.True(cov.Visible);
    Assert.DoesNotContain(def.Emitters, e => e.Name == "em_covering_blood");
  }

  [Fact]
  public void VisualsDefaultsMatchEngineConstructor()
  {
    // FUN_14178cbf0: textureTintControl 0, isBlendAlphaMultiply true when the key is absent.
    var def = Def();
    var smoke = def.Emitters.Single(e => e.Name == "smoke");
    var spark = def.Emitters.Single(e => e.Name == "spark");
    Assert.Equal(1f, smoke.TextureTint);
    Assert.False(smoke.BlendAlphaMultiply);
    Assert.Equal(0f, spark.TextureTint);
    Assert.True(spark.BlendAlphaMultiply);
    Assert.Equal(0f, spark.StreakRatio);
  }

  [Fact]
  public void GpuStreaksDefaultToEnabledRatio03()
  {
    // FUN_1417c6290: streaks.isEnabled true, ratio 0.3 when the keys are absent.
    var def = Def();
    Assert.Equal(0.3f, def.Emitters.Single(e => e.Name == "gpu_default").StreakRatio, 0.0001f);
    Assert.Equal(0.5f, def.Emitters.Single(e => e.Name == "gpu_ratio").StreakRatio, 0.0001f);
    Assert.Equal(0f, def.Emitters.Single(e => e.Name == "gpu_off").StreakRatio);
  }
}
