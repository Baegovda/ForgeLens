using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>lwiGeom mesh particles and sfx_trail ribbons, on trimmed retail-shaped saves.</summary>
public class EffectGeomTrailTests
{
  // part_mag_ee_bolter_flesh (trimmed): a splash emitter drawing sub-object 2 of splashes_vtxa_lwi.
  private const string Geom = """
    timeLife   =   1
    emitters2   =   {
       splash   =   {
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
             lwiGeom   =   {
                nameGeomTpl   =   "splashes_vtxa_lwi"
                typeInst   =   {
                   enumeration   =   {
                      typeInstEnum   =   "2"
                   }
                   __type   =   "local__enumeration"
                }
                sfxMtlOverrides   =   {
                   emissiveIntensity   =   {
                      splineOverride   =   {
                         spline   =   [
                            0,
                            10,
                            1,
                            0
                         ]
                      }
                      __type   =   "local__splineOverride"
                   }
                   color   =   {
                      color   =   {
                         constExpr   =   [
                            255,
                            44,
                            3,
                            155
                         ]
                         __type   =   "local__constExpr"
                      }
                      __type   =   "local__color"
                   }
                }
                combineMtlAnimControl   =   {
                   animStage   =   {
                      splineOverride   =   {
                         spline   =   [
                            0,
                            0,
                            0.25,
                            1,
                            1,
                            1
                         ]
                      }
                      __type   =   "local__splineOverride"
                   }
                }
                transforms   =   {
                   initRotAxises   =   {
                      random   =   {
                         x   =   True
                         y   =   False
                         z   =   True
                      }
                      __type   =   "local__random"
                   }
                }
             }
             __type   =   "local__lwiGeom"
          }
          particleParams   =   {
             timeLife   =   [
                0.7,
                0.7
             ]
          }
          __type   =   "CPU_EMITTER"
       }
    }
    __type   =   "sfx_part"
    """;

  // trail_bolter_smoke (trimmed): cylinder trail, 0.5 m segments living 1 s.
  private const string Trail = """
    nameParent   =   "RotCenter"
    visuals   =   {
       color   =   [
          0,
          255,
          100,
          100,
          100,
          1,
          0,
          0,
          0,
          0
       ]
       colorIntensityMultiplier   =   2
       blendType   =   "add"
       texture   =   {
          nameTex   =   "trail_smoke_hard"
       }
    }
    trailParams   =   {
       maxTrailLength   =   -1
       maxSegLength   =   0.5
       timeLifeSeg   =   1
       typeParams   =   {
          cylinder   =   {
             subdivs   =   4
          }
          __type   =   "local__cylinder"
       }
    }
    __type   =   "sfx_trail"
    """;

  private static EffectDefinition Load(string name) =>
    EffectDefinition.Build(name, new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["geom"] = Geom, ["trail"] = Trail })).Resolve(name)!);

  [Fact]
  public void ParsesLwiGeomEmitter()
  {
    var def = Load("geom");
    var e = Assert.Single(def.Emitters);
    var g = Assert.IsType<MeshParticleDef>(e.Geom);
    Assert.Equal("splashes_vtxa_lwi", g.Template);
    Assert.Equal((2, 2), (g.InstanceMin, g.InstanceMax));
    Assert.Equal(new Vector3(1, 0, 1), g.RandomAxes);
    // constExpr material colour is R,G,B,A: blood red, not blue.
    var c = g.ColorAt(0);
    Assert.Equal(1f, c.X, 0.001f);
    Assert.True(c.X > c.Y && c.Y > c.Z);
    Assert.Equal(10f, g.Emissive!.Evaluate(0), 0.001f);
    Assert.Equal(5f, g.Emissive.Evaluate(0.5f), 0.001f);
  }

  [Fact]
  public void AnimStageCurveDrivesVatStage()
  {
    var g = Assert.IsType<MeshParticleDef>(Assert.Single(Load("geom").Emitters).Geom);
    Assert.NotNull(g.AnimStage);
    Assert.Equal(0.5f, g.StageAt(0.125f), 0.001f);
    Assert.Equal(1f, g.StageAt(0.6f), 0.001f);
    Assert.Equal(0.3f, new MeshParticleDef { Template = "x" }.StageAt(0.3f), 0.001f);
  }

  [Fact]
  public void MeshParticlesSpawnAndAge()
  {
    var instance = new EffectInstance(Load("geom"), 3) { Ground = null };
    instance.Seek(0.1f);
    var batch = Assert.Single(EffectMeshParticles.Build(instance));
    var inst = Assert.Single(batch.Instances);
    Assert.Equal(2, inst.Variant);
    instance.Seek(0.9f); // timeLife 0.7
    Assert.Empty(EffectMeshParticles.Build(instance).SelectMany(b => b.Instances));
  }

  [Fact]
  public void TrailFollowsMovingRootAndExpires()
  {
    var def = Load("trail");
    var trail = Assert.Single(def.Trails);
    Assert.Equal(TrailShape.Cylinder, trail.Shape);
    Assert.Equal((0.5f, 1f), (trail.MaxSegLength, trail.TimeLifeSeg));
    Assert.Equal("trail_smoke_hard", trail.Render.Texture);
    Assert.True(def.Loops);

    var instance = new EffectInstance(def, 1) { Ground = null, RootMotion = t => Matrix4x4.CreateTranslation(t * 4f, 0, 0) };
    instance.Seek(0.5f); // 2 m travelled; one point per step, never more than maxSegLength apart
    var state = Assert.Single(instance.Trails);
    Assert.True(state.Points.Count >= 5);
    for (var i = 1; i < state.Points.Count; i++)
      Assert.True(Vector3.Distance(state.Points[i - 1].Position, state.Points[i].Position) <= 0.5f + 1e-4f);
    var batches = EffectTrailSprites.Build(instance, new Vector3(0, 2, 5)).ToList();
    Assert.Single(batches);
    Assert.True(batches[0].QuadCount > 0);

    // Moves for 0.3 s then parks at x = 1.2: after timeLifeSeg the moving segment has died out.
    var parked = new EffectInstance(def, 1) { Ground = null, RootMotion = t => Matrix4x4.CreateTranslation(MathF.Min(t, 0.3f) * 4f, 0, 0) };
    parked.Seek(1.5f);
    Assert.All(parked.Trails[0].Points, p => Assert.Equal(1.2f, p.Position.X, 0.001f));
  }
}
