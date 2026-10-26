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
                   textureTintControl   =   0
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
  public void TextureTintControlDefaultsToOne()
  {
    var def = Def();
    Assert.Equal(0f, def.Emitters.Single(e => e.Name == "smoke").TextureTint);
    Assert.Equal(1f, def.Emitters.Single(e => e.Name == "spark").TextureTint);
  }
}
