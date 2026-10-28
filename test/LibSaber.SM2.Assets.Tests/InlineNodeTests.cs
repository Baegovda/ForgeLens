using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-41: inline untyped list nodes keyed by their type, typed GPU_EMITTER keys, and the isAlphaKill blend flag rule.</summary>
public class InlineNodeTests
{
  private static string Cpu(string name, bool alphaKill) => $$"""
         {{name}}   =   {
            __node   =   {
               x   =   0
            }
            emission   =   {
               type   =   {
                  explosion   =   {
                     numPartsToEmit   =   [
                        4,
                        4
                     ]
                  }
                  __type   =   "local__explosion"
               }
            }
            typeParams   =   {
               default   =   {
                  isAlphaKill   =   {{(alphaKill ? "True" : "False")}}
                  visuals   =   {
                     blendType   =   "add"
                  }
               }
               __type   =   "local__default"
            }
            particleParams   =   {
               timeLife   =   [
                  1,
                  1
               ]
            }
         }
""";

  private static readonly string Group = $$"""
    list   =   {
       sfx_part   =   {
          timeLife   =   2
          emitters2   =   {
{{Cpu("sparks_05", true)}}
{{Cpu("flash_01", false)}}
             GPU_EMITTER   =   {
                emission   =   {
                   type   =   {
                      constantFlow   =   {
                         rate   =   10
                      }
                      __type   =   "local__constantFlow"
                   }
                }
                visuals   =   {
                   blendType   =   "add"
                }
             }
          }
       }
    }
    __type   =   "sfx_group"
""";

  private static EffectDefinition Build()
  {
    var lib = new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_inline"] = Group }));
    return EffectDefinition.Build("fx_inline", lib.Resolve("fx_inline")!);
  }

  [Fact]
  public void UntypedListEntryKeyedByNodeTypeIsInlineNode()
  {
    var def = Build();
    Assert.Equal(3, def.Emitters.Count);
    Assert.Equal(1, def.Emitters.Count(e => e.IsGpu));
  }

  [Fact]
  public void AlphaKillDefaultAddDropsFlag20()
  {
    var def = Build();
    var sparks = def.Emitters.Single(e => e.Name.EndsWith("sparks_05"));
    var flash = def.Emitters.Single(e => e.Name.EndsWith("flash_01"));
    Assert.True(sparks.AlphaKill);
    Assert.False(flash.AlphaKill);
    Assert.Equal(0x400u, EffectBlendRea.Flags(sparks));      // capture plasma slot 56: 0x2500
    Assert.Equal(0x420u, EffectBlendRea.Flags(flash));
    Assert.Equal(0x420u, EffectBlendRea.Flags(def.Emitters.Single(e => e.IsGpu)));
  }

  [Fact]
  public void ScreenSpaceOrNonDefaultTypeKeepsAdditive()
  {
    static EmitterDef E(EffectBlend b, bool ak, bool ss = false, string type = "default", bool mul = true)
      => new() { Name = "x", Path = "x", Blend = b, AlphaKill = ak, ScreenSpace = ss, ParticleType = type, BlendAlphaMultiply = mul };
    Assert.Equal(0x400u, EffectBlendRea.Flags(E(EffectBlend.Add, true)));
    Assert.Equal(0x420u, EffectBlendRea.Flags(E(EffectBlend.Add, true, ss: true)));
    Assert.Equal(0x420u, EffectBlendRea.Flags(E(EffectBlend.Add, true, type: "lwiGeom")));
    Assert.Equal(0x400u, EffectBlendRea.Flags(E(EffectBlend.Blend, true, mul: false)));
    Assert.Equal(0x000u, EffectBlendRea.Flags(E(EffectBlend.Blend, true)));
  }
}
