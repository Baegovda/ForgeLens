using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public class EffectTests
{
  // Shape of a retail sfx_part (trimmed): one explosion emitter that spawns a child on death, one constant flow.
  private const string Part = """
    timeLife   =   2
    scale   =   1
    emitters2   =   {
       burst   =   {
          isVisible   =   True
          emission   =   {
             speedNorm   =   {
                constExpr   =   [
                   2,
                   4
                ]
                __type   =   "local__constExpr"
             }
             type   =   {
                explosion   =   {
                   numPartsToEmit   =   [
                      5,
                      5
                   ]
                }
                __type   =   "local__explosion"
             }
          }
          particleParams   =   {
             timeLife   =   [
                0.5,
                0.5
             ]
          }
          typeParams   =   {
             default   =   {
                visuals   =   {
                   texture   =   {
                      nameTex   =   "part_spark_01"
                   }
                   blendType   =   "add"
                   alphaMask   =   {
                      mode   =   "Enabled"
                      texture   =   {
                         nameTex   =   "q_sparks_mask_thin"
                         tcIn   =   {
                            transform   =   {
                               rotation   =   {
                                  x   =   -30
                                  y   =   30
                               }
                            }
                            __type   =   "local__transform"
                         }
                      }
                   }
                }
             }
             __type   =   "local__default"
          }
          attached   =   {
             genOnDie   =   {
                k   =   {
                   name   =   "debris"
                   numParts   =   3
                }
             }
          }
          __type   =   "CPU_EMITTER"
       }
       debris   =   {
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
          particleParams   =   {
             timeLife   =   [
                5,
                5
             ]
          }
          __type   =   "CPU_EMITTER"
       }
       smoke   =   {
          isVisible   =   True
          emission   =   {
             type   =   {
                constantFlow   =   {
                   x   =   30
                   y   =   30
                }
                __type   =   "local__constantFlow"
             }
          }
          particleParams   =   {
             timeLife   =   [
                10,
                10
             ]
          }
          typeParams   =   {
             default   =   {
                drawInSimulationTex   =   {
                   drawInSimulationEnabled   =   {
                   }
                   __type   =   "local__drawInSimulationEnabled"
                }
             }
             __type   =   "local__default"
          }
          __type   =   "CPU_EMITTER"
       }
       orphan   =   {
          isVisible   =   True
          emission   =   {
          }
       }
    }
    __type   =   "sfx_part"
    """;

  private const string Group = """
    list   =   {
       fx_part   =   {
          scale   =   2
       }
    }
    __type   =   "sfx_group"
    """;

  private static EffectDefinition Load(string name = "fx_part")
  {
    var library = new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_part"] = Part, ["fx_group"] = Group }));
    return EffectDefinition.Build(name, library.Resolve(name)!);
  }

  [Fact]
  public void ParsesEmittersAttachedAndMasks()
  {
    var def = Load();
    Assert.Equal(["burst", "debris", "smoke"], def.Emitters.Select(e => e.Name));
    Assert.Contains(def.Unsupported, u => u.Type == "orphan");
    var burst = def.Emitters[0];
    Assert.Equal(EmissionKind.Explosion, burst.Emission);
    Assert.Equal(EffectBlend.Add, burst.Blend);
    Assert.Equal("q_sparks_mask_thin", burst.AlphaMask);
    Assert.Equal(new Vector2(-30, 30), burst.AlphaMaskRotation);
    var attached = Assert.Single(burst.Attached);
    Assert.Equal((AttachEvent.Die, 1, 3f), (attached.Event, attached.Index, attached.NumParts));
    Assert.True(def.Emitters[1].IsAttachedChild);
    Assert.Equal(DrawTarget.Simulation, def.Emitters[2].Target);
    Assert.Equal(EmissionKind.ConstantFlow, def.Emitters[2].Emission);
  }

  [Fact]
  public void GroupInstancesFileWithOverrides()
  {
    var def = Load("fx_group");
    Assert.Equal(3, def.Emitters.Count);
    Assert.Equal(2f, def.Emitters[0].Scale);
  }

  [Fact]
  public void ExplosionConstantFlowAndGenOnDie()
  {
    var instance = new EffectInstance(Load(), 7) { Ground = null };
    instance.Seek(0.25f);
    Assert.Equal(5, instance.Emitters[0].Particles.Count);
    Assert.Empty(instance.Emitters[1].Particles); // attached child does not emit on its own
    Assert.InRange(instance.Emitters[2].Particles.Count, 6, 9); // 30/s
    instance.Seek(1f);
    Assert.Empty(instance.Emitters[0].Particles);
    Assert.Equal(15, instance.Emitters[1].Particles.Count); // 5 parents x numParts 3
    Assert.InRange(instance.Emitters[2].Particles.Count, 28, 32);
  }

  [Fact]
  public void HiddenSourceStillSpawnsAttachedChildren()
  {
    // isVisible only gates draw submission in the exe (FUN_1417f71b0 / FUN_1417ee5c0), so a hidden source still emits.
    // Brittle whitespace-sensitive Replace used to fail when the fixture indent drifted; flip the first burst isVisible only.
    var burstAt = Part.IndexOf("burst", StringComparison.Ordinal);
    Assert.True(burstAt >= 0);
    var visAt = Part.IndexOf("isVisible", burstAt, StringComparison.Ordinal);
    Assert.True(visAt > burstAt);
    var trueAt = Part.IndexOf("True", visAt, StringComparison.Ordinal);
    Assert.True(trueAt > visAt && trueAt - visAt < 40);
    var hidden = Part.Substring(0, trueAt) + "False" + Part.Substring(trueAt + 4);
    Assert.NotEqual(Part, hidden);
    var library = new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_part"] = hidden }));
    var def = EffectDefinition.Build("fx_part", library.Resolve("fx_part")!);
    Assert.False(def.Emitters[0].Visible);
    var instance = new EffectInstance(def, 7) { Ground = null };
    instance.Seek(1f);
    Assert.Equal(15, instance.Emitters[1].Particles.Count); // 5 hidden parents x numParts 3
    var sprites = EffectSprites.Build(instance, PartAliases.Empty, Matrix4x4.CreateLookAt(new Vector3(0, 1, 8), Vector3.Zero, Vector3.UnitY));
    Assert.DoesNotContain(sprites, b => b.Emitter == def.Emitters[0]); // hidden source itself never draws
  }

  [Fact]
  public void SimulationIsDeterministicAndSeekable()
  {
    var def = Load();
    var a = new EffectInstance(def, 42);
    var b = new EffectInstance(def, 42);
    a.Seek(1.5f);
    b.Seek(0.7f);
    b.Seek(1.5f);
    var pa = a.Emitters.SelectMany(e => e.Particles).Select(p => p.Position).ToList();
    var pb = b.Emitters.SelectMany(e => e.Particles).Select(p => p.Position).ToList();
    Assert.Equal(pa, pb);
    a.Seek(0.3f); // backward seek replays from the start
    b.Reset();
    b.Seek(0.3f);
    Assert.Equal(a.Emitters.SelectMany(e => e.Particles).Select(p => p.Position), b.Emitters.SelectMany(e => e.Particles).Select(p => p.Position));
  }

  [Fact]
  public void PartAliasesFlipbookGrid()
  {
    var aliases = PartAliases.Parse("""
      fire_seq   =   {
         __type_id   =   "textureDataAdd"
         nFrameX   =   8
         nFrameY   =   4
         nFrameMax   =   30
      }
      plain   =   {
         __type_id   =   "textureDataAdd"
      }
      """);
    Assert.Equal((8, 4, 30), (aliases["fire_seq"].FramesX, aliases["fire_seq"].FramesY, aliases["fire_seq"].FrameMax));
    Assert.Equal(1, aliases["plain"].FrameMax);
    Assert.Same(TextureFrames.Single, aliases["missing"]);
  }

  [Fact]
  public void CurvesInterpolate()
  {
    var c = new Curve1([0f, 0f, 1f, 10f]);
    Assert.Equal(5f, c.Evaluate(0.5f), 1e-3f);
    Assert.Equal(10f, c.Evaluate(2f), 1e-3f);
    var color = new ColorCurve([0f, 255f, 255f, 0f, 0f]);
    Assert.Equal(new Vector4(1, 0, 0, 1), color.Evaluate(0.3f));
  }
}
