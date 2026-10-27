using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public class DistortionTests
{
  private const string Part = """
    timeLife   =   1
    emitters2   =   {
       wave   =   {
          isVisible   =   True
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
          particleParams   =   {
             timeLife   =   [
                1,
                1
             ]
          }
          typeParams   =   {
             distortion   =   {
                visuals   =   {
                   texture   =   {
                      nameTex   =   "part_distort_nm"
                   }
                   power   =   {
                      ageSpline   =   [
                         0,
                         0,
                         0.5,
                         0.5,
                         1,
                         0
                      ]
                      __type   =   "local__ageSpline"
                   }
                }
             }
             __type   =   "local__distortion"
          }
          __type   =   "CPU_EMITTER"
       }
       flame   =   {
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
                      nameTex   =   "part_flame"
                   }
                   sdr   =   {
                      isEnabled   =   True
                      texture   =   {
                         nameTex   =   "alga_distort_02_nm"
                         animIn   =   {
                            channel   =   "DISTORTION"
                            __type   =   "local__channel"
                         }
                      }
                      power   =   {
                         ageSpline   =   {
                            ageSpline   =   [
                               0,
                               0.2,
                               1,
                               0.2
                            ]
                         }
                         __type   =   "local__ageSpline"
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

  private static EffectDefinition Load() =>
    EffectDefinition.Build("fx_d", new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_d"] = Part })).Resolve("fx_d")!);

  [Fact]
  public void ParsesDistortionTypeAndSdrChannel()
  {
    var def = Load();
    var wave = def.Emitters.Single(e => e.Name == "wave");
    Assert.True(wave.IsSprite);
    Assert.Equal(DrawTarget.Distortion, wave.Target);
    Assert.Equal("part_distort_nm", wave.DistortTexture);
    Assert.Equal(0.5f, wave.DistortPower.Sample(0.5f, 0.5f), 0.001f);
    var flame = def.Emitters.Single(e => e.Name == "flame");
    Assert.Equal(DrawTarget.Color, flame.Target);
    Assert.Equal("alga_distort_02_nm", flame.DistortTexture);
    Assert.Equal(0.2f, flame.DistortPower.Sample(0.5f, 0.5f), 0.001f); // nested { ageSpline = { ageSpline = [..] } }
  }

  [Fact]
  public void BuildsColourAndRefractionBatches()
  {
    var instance = new EffectInstance(Load(), 3) { Ground = null };
    instance.Seek(0.5f);
    var batches = EffectSprites.Build(instance, PartAliases.Empty, Matrix4x4.CreateLookAt(new Vector3(0, 1, 5), Vector3.Zero, Vector3.UnitY));
    Assert.Equal(4, batches.Where(b => b.Distortion && b.Emitter.Name == "wave").Sum(b => b.QuadCount));
    Assert.Equal(2, batches.Where(b => b.Distortion && b.Emitter.Name == "flame").Sum(b => b.QuadCount));
    Assert.Equal(2, batches.Where(b => !b.Distortion).Sum(b => b.QuadCount)); // the refraction-only wave draws no colour
    var power = batches.First(b => b.Distortion && b.Emitter.Name == "wave").Vertices[5];
    Assert.Equal(0.5f, power, 0.02f);
  }

  [Fact]
  public void DistanceFadeNearAndFar()
  {
    var near = new DistanceFade(1.8f, 0.9f, Alpha: 0.1f);
    Assert.Equal(0f, near.Amount(3f));
    Assert.Equal(1f, near.Amount(0.5f));
    Assert.Equal(0.5f, near.Amount(1.35f), 0.001f);
    var far = new DistanceFade(40f, 60f, Intens: 0f);
    Assert.Equal(0f, far.Amount(10f));
    Assert.Equal(0.25f, far.Amount(45f), 0.001f);
    Assert.Equal(1f, far.Amount(80f));
  }
  [Fact]
  public void DistanceFadeKSharedBySpritesAndMeshes()
  {
    var def = new EmitterDef { Name = "m", Path = "m", DegFar = new DistanceFade(40f, 60f, Size: 0.5f, Intens: 0f) };
    var (s0, i0, a0, _) = EffectSprites.DistanceFadeK(def, 10f);
    Assert.Equal((1f, 1f, 1f), (s0, i0, a0));
    var (s1, i1, _, _) = EffectSprites.DistanceFadeK(def, 50f);
    Assert.Equal(0.75f, s1, 0.001f);
    Assert.Equal(0.5f, i1, 0.001f);
  }
}
