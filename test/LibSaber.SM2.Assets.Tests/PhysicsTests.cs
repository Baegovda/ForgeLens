using System.Numerics;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2.Assets.Tests;

public class PhysicsTests
{
  // Shape of a retail character class (pc_marine_pve_story_titus_client.cls), trimmed.
  private const string Cls = """
    properties   =   {
       prop_cloth_sim   =   {
          clothes   =   [
             {
                simMeshName   =   "seal_SIM"
                renderMeshes   =   [
                   {
                      name   =   "seal_REND"
                   }
                ]
                simRemapConfig   =   {
                   maxInfluenceDist   =   0.03
                   useTopologicalDistance   =   True
                }
                config   =   {
                   vertical   =   {
                      stiffness   =   0.8
                   }
                   tether   =   {
                      stiffness   =   0.5
                   }
                   gravity   =   [
                      0,
                      -12,
                      0
                   ]
                   damping   =   0.08
                   solverFrequency   =   200
                   cdtCapsules   =   [
                      {
                         name   =   "thigh"
                         boneFirst   =   "Thigh_R"
                         radiusFirst   =   0.1
                         boneSecond   =   "Calf_R"
                         radiusSecond   =   0.08
                      }
                   ]
                }
             }
          ]
       }
       prop_phys_jiggle   =   {
          physJiggle   =   {
             objectsSettings   =   {
                Plates   =   {
                   __value   =   [
                      {
                         name   =   "Hip_plate_R_jiggle"
                         stiffness   =   80
                         mass   =   0.2
                         damping   =   10
                         gravity   =   -1
                         offsetDirection   =   {
                            x   =   0
                            y   =   -1
                            z   =   0
                            __type   =   "Vector3d"
                         }
                         offsetDistance   =   1
                         constraintMin   =   {
                            x   =   -0.4
                            y   =   -0.1
                            z   =   -0.1
                            __type   =   "Vector3d"
                         }
                         constraintMax   =   {
                            x   =   -0.35
                            y   =   0.1
                            z   =   0.1
                            __type   =   "Vector3d"
                         }
                         jumpsConstraintMin   =   -0.1
                         jumpsConstraintMax   =   0.1
                      }
                   ]
                }
             }
          }
       }
    }
    __type   =   "pc_marine_story_client"
    """;

  [Fact]
  public void ParsesClothAndJiggle()
  {
    Assert.True(PhysicsParser.Declares(Cls));
    var setup = PhysicsParser.Parse(Cls, "test.cls");
    var cloth = Assert.Single(setup.Cloths);
    Assert.Equal("seal_SIM", cloth.SimMeshName);
    Assert.Equal(new[] { "seal_REND" }, cloth.RenderMeshes);
    Assert.Equal((double)(0.03f), (double)(cloth.MaxInfluenceDist), 5);
    Assert.True(cloth.UseTopologicalDistance);
    Assert.Equal((double)(0.8f), (double)(cloth.Config.Vertical.Stiffness), 5);
    Assert.Equal(new Vector3(0, -12, 0), cloth.Config.Gravity);
    Assert.Equal(200f, cloth.Config.SolverFrequency);
    var capsule = Assert.Single(cloth.Config.Capsules);
    Assert.Equal("Thigh_R", capsule.BoneFirst);
    Assert.Equal("Calf_R", capsule.BoneSecond);
    // Unset fields keep the engine defaults (constructor ending 0x14108f119).
    Assert.Equal(1f, cloth.Config.StiffnessMultiplier);

    var jiggle = Assert.Single(setup.Jiggles);
    Assert.Equal("Plates", jiggle.Group);
    Assert.Equal("Hip_plate_R_jiggle", jiggle.Name);
    Assert.Equal(80f, jiggle.Stiffness);
    Assert.Equal((double)(0.2f), (double)(jiggle.Mass), 5);
    Assert.Equal(new Vector3(0, -1, 0), jiggle.OffsetDirection);
    Assert.Equal(new Vector3(-0.4f, -0.1f, -0.1f), jiggle.ConstraintMin);
    Assert.Equal((double)(-0.1f), (double)(jiggle.JumpsConstraintMin), 5);
  }

  [Fact]
  public void ClassWithoutPhysicsIsEmpty()
  {
    const string text = "x = { properties = { prop_other = { a = 1 } } }";
    Assert.False(PhysicsParser.Declares(text));
    Assert.True(PhysicsParser.Parse(text).IsEmpty);
  }

  [Theory]
  [InlineData(-2f, -1f)] // below min: pushed to min
  [InlineData(0.5f, 0.5f)] // inside: untouched
  [InlineData(3f, 1f)] // above max: pulled to max
  public void ClampAlongAxis(float s, float expected)
  {
    var p = new Vector3(s, 5, 0);
    JiggleObject.Clamp(Vector3.Zero, Vector3.UnitX, -1, 1, ref p);
    Assert.Equal((double)(expected), (double)(p.X), 5);
    Assert.Equal((double)(5f), (double)(p.Y), 5);
  }

  [Fact]
  public void JiggleStartsAtTargetAndRestsUnderSpring()
  {
    var settings = new JiggleSettings { Name = "j", Stiffness = 100, Mass = 1, Damping = 8, OffsetDirection = -Vector3.UnitY, OffsetDistance = 0.5f, ConstraintMin = new Vector3(-1), ConstraintMax = new Vector3(1) };
    var bindLocal = Matrix4x4.CreateTranslation(0, -0.2f, 0);
    var obj = new JiggleObject(settings, 1, 0, bindLocal);
    Assert.Equal(JiggleObject.Unset, obj.Position.X);
    var parent = Matrix4x4.CreateTranslation(1, 2, 3);
    var world = obj.Step(parent, 1f / 60);
    // First step seeds the spring at its target: the bone stays at bindLocal * parent.
    Assert.True(Vector3.Distance(world.Translation, (bindLocal * parent).Translation) < 1e-4f);
    // A sideways parent jump swings the spring point, which then settles back under damping.
    var moved = Matrix4x4.CreateTranslation(1.3f, 2, 3);
    var swing = 0f;
    for (var i = 0; i < 600; i++)
    {
      obj.Step(moved, 1f / 120);
      swing = MathF.Max(swing, MathF.Abs(obj.Position.X - 1.3f));
    }

    Assert.True(swing > 0.05f, $"swing {swing}");
    Assert.True(MathF.Abs(obj.Position.X - 1.3f) < 1e-3f);
    Assert.All(new[] { obj.Position.X, obj.Position.Y, obj.Position.Z }, v => Assert.True(float.IsFinite(v)));
  }

  /// <summary>A 5x5 strip hanging from its pinned top row on one bone.</summary>
  private static (ExportedMesh Mesh, PhysicsSetup Setup) Strip(float windX = 0, int n = 5)
  {
    var mesh = new ExportedMesh { Name = "strip" };
    mesh.Bones.Add(new Bone { Name = "Root", Parent = -1 });
    mesh.Bones.Add(new Bone { Name = "Thigh_R", Parent = 0 });
    var shell = new ClothSimShell { Name = "seal_SIM" };
    for (var r = 0; r < n; r++)
      for (var c = 0; c < n; c++)
      {
        shell.Positions.Add(new Vector3(c * 0.025f, 1 - r * 0.05f, 0));
        shell.Normals.Add(Vector3.UnitZ);
        shell.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
        shell.Colors.Add(new Vector4(r * 0.05f, 0, 0, 1));
      }

    for (var r = 0; r + 1 < n; r++)
      for (var c = 0; c + 1 < n; c++)
      {
        var a = r * n + c;
        shell.Indices.AddRange(new[] { a, a + n, a + 1, a + 1, a + n, a + n + 1 });
      }

    mesh.ClothSims.Add(shell);
    var setup = new PhysicsSetup();
    var entry = new ClothEntry { SimMeshName = "seal_SIM" };
    entry.Config.Environment.LocalWindVelocity = new Vector3(windX, 0, 0);
    setup.Cloths.Add(entry);
    return (mesh, setup);
  }

  [Fact]
  public void ClothFollowsPinsAndStaysBounded()
  {
    var (mesh, setup) = Strip(windX: 6);
    var runtime = new PhysicsRuntime(mesh, setup);
    var cloth = Assert.Single(runtime.Cloths);
    Assert.Equal(5, cloth.PinnedCount);
    for (var i = 0; i <= 240; i++)
    {
      var t = i / 120f;
      var worlds = new[] { Matrix4x4.Identity, Matrix4x4.CreateTranslation(0.3f * MathF.Sin(t * 6), 0, 0) };
      runtime.Step(worlds, t, 0);
    }

    var pin = Matrix4x4.CreateTranslation(0.3f * MathF.Sin(2 * 6), 0, 0);
    for (var c = 0; c < 5; c++)
      Assert.True(Vector3.Distance(cloth.Positions[c], Vector3.Transform(cloth.BindPositions[c], pin)) < 1e-4f);
    Assert.All(cloth.Positions, p => Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)));
    // Motion constraints (COLOR0 = max distance, 0.2 m at the free end) keep every particle near its skinned spot.
    Assert.True(cloth.MaxDeviation <= 0.2f + 1e-3f, $"deviation {cloth.MaxDeviation}");
  }

  [Fact]
  public void ClothResetsWhenTimeJumpsBack()
  {
    var (mesh, setup) = Strip();
    var runtime = new PhysicsRuntime(mesh, setup);
    var worlds = new[] { Matrix4x4.Identity, Matrix4x4.Identity };
    runtime.Step(worlds, 0, 10);
    runtime.Step(worlds, 0.1f, 10);
    var before = runtime.ResetCount;
    runtime.Step(worlds, 0.05f, 10); // scrub back
    Assert.Equal(before + 1, runtime.ResetCount);
  }

  [Fact]
  public void ClosestPointOnTriangle()
  {
    var a = Vector3.Zero;
    var b = Vector3.UnitX;
    var c = Vector3.UnitY;
    var point = PhysicsRuntime.ClosestOnTriangle(new Vector3(0.25f, 0.25f, 1), a, b, c, out var bary);
    Assert.True(Vector3.Distance(point, new Vector3(0.25f, 0.25f, 0)) < 1e-5f);
    Assert.Equal((double)(1f), (double)(bary.X + bary.Y + bary.Z), 4);
    point = PhysicsRuntime.ClosestOnTriangle(new Vector3(2, -1, 0), a, b, c, out _);
    Assert.True(Vector3.Distance(point, b) < 1e-5f);
  }

  [Fact]
  public void LargeClothGetsOneBonePerFreeParticleByDefault()
  {
    // mtl1-42: 36x36 grid, top row pinned = 1260 free particles (> the old 1024 cap, like calgar's 1133-particle cloak).
    static (ExportedMesh, PhysicsSetup) Big()
    {
      var (m, st) = Strip(n: 36);
      var shell = m.ClothSims[0];
      st.Cloths[0].RenderMeshes.Add("seal_REND");
      foreach (var p in shell.Positions)
      {
        m.Positions.Add(p + new Vector3(0, 0, 0.002f));
        m.Normals.Add(Vector3.UnitZ);
        m.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
      }
      m.Indices.AddRange(shell.Indices);
      m.Parts.Add(new MeshPart { Name = "seal_REND", IndexStart = 0, IndexCount = shell.Indices.Count });
      return (m, st);
    }
    var (mesh, setup) = Big();
    var rig = PhysicsBake.AddClothBones(mesh, setup);
    Assert.Equal(36 * 35, rig.Clusters.Count);
    Assert.All(rig.Clusters, c => Assert.Single(c.Particles));
    var (m2, s2) = Big();
    var capped = PhysicsBake.AddClothBones(m2, s2, maxBonesPerCloth: 1024);
    Assert.Equal(1024, capped.Clusters.Count);
  }

  [Fact]
  public void ClusterFitRecoversRigidMotion()
  {
    var bind = new[] { new Vector3(0, 0, 0), new Vector3(0.1f, 0, 0), new Vector3(0, 0.1f, 0), new Vector3(0, 0, 0.1f), new Vector3(0.05f, 0.05f, 0.02f) };
    var q = Quaternion.CreateFromYawPitchRoll(0.4f, -0.3f, 0.2f);
    var move = new Vector3(0.3f, -0.2f, 1f);
    var current = bind.Select(b => Vector3.Transform(b, q) + move).ToArray();
    var particles = Enumerable.Range(0, bind.Length).ToArray();
    var center = bind.Aggregate(Vector3.Zero, (a, b) => a + b) / bind.Length;
    PhysicsBake.Fit(bind, current, particles, center, Quaternion.Identity, out _, out var error);
    Assert.True(error < 1e-4f, $"fit error {error}");
  }

  [Theory]
  [InlineData(1, 0.01f)]
  [InlineData(4, 0.06f)]
  public void BakedClipReproducesTheSimulation(int particlesPerBone, float tolerance)
  {
    var (mesh, setup) = Strip();
    var shell = mesh.ClothSims[0];
    setup.Cloths[0].RenderMeshes.Add("seal_REND");
    foreach (var p in shell.Positions)
    {
      mesh.Positions.Add(p + new Vector3(0, 0, 0.002f));
      mesh.Normals.Add(Vector3.UnitZ);
      mesh.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
    }

    mesh.Indices.AddRange(shell.Indices);
    mesh.Parts.Add(new MeshPart { Name = "seal_REND", IndexStart = 0, IndexCount = shell.Indices.Count });
    mesh.Bones.Add(new Bone { Name = "Jig", Parent = 1, Local = Matrix4x4.CreateTranslation(0, 0.9f, 0) });
    setup.Jiggles.Add(new JiggleSettings { Name = "Jig", Stiffness = 60, Mass = 1, Damping = 4, OffsetDirection = -Vector3.UnitY, OffsetDistance = 0.2f, ConstraintMin = new Vector3(-1), ConstraintMax = new Vector3(1) });
    var clip = new LibSaber.SM2.Assets.Animation.AnimationClip { Name = "swing", Duration = 1f, SampleRate = 60 };
    var track = new LibSaber.SM2.Assets.Animation.BoneTrack { Bone = 1, BoneName = "Thigh_R" };
    for (var f = 0; f <= 60; f++)
      track.Translation.Add(new LibSaber.SM2.Assets.Animation.VectorKey { Time = f / 60f, Value = new Vector3(0.3f * MathF.Sin(f / 60f * 6), 0, 0) });
    clip.Tracks.Add(track);

    var rig = PhysicsBake.AddClothBones(mesh, setup, particlesPerBone);
    Assert.NotEmpty(rig.Clusters);
    Assert.True(rig.SkinnedVertices >= 15, $"skinned {rig.SkinnedVertices}");
    var report = PhysicsBake.Bake(mesh, clip, setup, rig, preroll: 0);
    Assert.Equal(1, report.JiggleBones);
    Assert.NotNull(clip.Baked);
    Assert.True(report.MaxJiggleDegrees > 1f, $"jiggle swing {report.MaxJiggleDegrees}");

    // Replay the live simulation and compare with plain skinning of the baked skeleton at the last frame.
    var live = new PhysicsRuntime(mesh, setup);
    mesh.Clip = clip;
    var baked = clip.Baked!;
    clip.Baked = null;
    Matrix4x4[] worlds = null!;
    for (var f = 0; f <= 60; f++)
    {
      worlds = MeshPreview.BoneWorlds(mesh, f / 60f);
      live.Step(worlds, f / 60f, 0);
    }

    clip.Baked = baked;
    var bakedWorlds = MeshPreview.BoneWorlds(mesh, 1f);
    var jig = mesh.Bones.FindIndex(b => b.Name == "Jig");
    Assert.True(Vector3.Distance(worlds[jig].Translation, bakedWorlds[jig].Translation) < 1e-3f);
    Assert.True(Vector3.Distance(Vector3.TransformNormal(Vector3.UnitY, worlds[jig]), Vector3.TransformNormal(Vector3.UnitY, bakedWorlds[jig])) < 1e-3f);
    var cloth = live.Cloths[0];
    var worst = 0f;
    for (var v = 0; v < mesh.Positions.Count; v++)
    {
      var s = mesh.Skin[v];
      var p = Vector3.Zero;
      void Add(ushort j, float w) { if (w > 0) p += Vector3.Transform(mesh.Positions[v], mesh.Bones[j].InverseBind * bakedWorlds[j]) * w; }
      Add(s.J0, s.W0); Add(s.J1, s.W1); Add(s.J2, s.W2); Add(s.J3, s.W3);
      worst = MathF.Max(worst, Vector3.Distance(p - new Vector3(0, 0, 0.002f), cloth.Positions[v]));
    }

    Assert.True(worst < tolerance, $"baked skinning is {worst} m from the live sim");
    mesh.Clip = null;
  }

  [Fact]
  public void DrivenPartsFallBackToLodSuffix()
  {
    var entry = new ClothEntry();
    entry.RenderMeshes.Add("cloak_REND_01");
    var lodOnly = new[] { "cloak_REND_01_LOD1", "cloak_REND_011", "body" };
    Assert.Equal(new[] { "cloak_REND_01_LOD1" }, entry.DrivenParts(lodOnly, n => n));
    var both = new[] { "cloak_REND_01", "cloak_REND_01_LOD1" };
    Assert.Equal(new[] { "cloak_REND_01" }, entry.DrivenParts(both, n => n));
  }

  [Fact]
  public void ClothBonesReskinLevelsAndBakedLocalsFollowBoneNames()
  {
    var (mesh, setup) = Strip();
    var shell = mesh.ClothSims[0];
    setup.Cloths[0].RenderMeshes.Add("seal_REND");
    // The drawn copy only exists in a LOD level, named with the _LOD suffix (cc_calgar.tpl).
    var level = new MeshLod { Name = "LOD1" };
    foreach (var p in shell.Positions)
    {
      level.Positions.Add(p);
      level.Normals.Add(Vector3.UnitZ);
      level.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });
    }

    level.Indices.AddRange(shell.Indices);
    level.Parts.Add(new MeshPart { Name = "seal_REND_LOD1", IndexStart = 0, IndexCount = shell.Indices.Count });
    mesh.Levels.Add(level);
    mesh.Positions.Add(Vector3.Zero);
    mesh.Normals.Add(Vector3.UnitZ);
    mesh.Skin.Add(new SkinWeights { J0 = 1, W0 = 1 });

    var rig = PhysicsBake.AddClothBones(mesh, setup);
    Assert.True(rig.SkinnedVertices >= 15, $"skinned {rig.SkinnedVertices}");
    Assert.Contains(level.Skin, w => w.J0 >= rig.Clusters[0].Bone || w.J1 >= rig.Clusters[0].Bone);

    var clip = new LibSaber.SM2.Assets.Animation.AnimationClip { Name = "swing", Duration = 0.5f, SampleRate = 60 };
    var track = new LibSaber.SM2.Assets.Animation.BoneTrack { Bone = 1, BoneName = "Thigh_R" };
    for (var f = 0; f <= 30; f++)
      track.Translation.Add(new LibSaber.SM2.Assets.Animation.VectorKey { Time = f / 60f, Value = new Vector3(0.3f * MathF.Sin(f / 10f), 0, 0) });
    clip.Tracks.Add(track);
    PhysicsBake.Bake(mesh, clip, setup, rig, preroll: 0);

    // A writer copy of the skeleton with an extra root in front (UnrealRoot.Promote) still gets the baked locals.
    var shifted = new List<Bone> { new() { Name = "EXTRA_ROOT", Parent = -1, Local = Matrix4x4.Identity } };
    shifted.AddRange(mesh.Bones.Select(b => new Bone { Name = b.Name, Parent = b.Parent < 0 ? 0 : b.Parent + 1, Local = b.Local, InverseBind = b.InverseBind }));
    var original = mesh.Bones.Select(b => b.Local).ToArray();
    var moved = shifted.Select(b => b.Local).ToArray();
    LibSaber.SM2.Assets.Meshes.ClipPose.Apply(mesh.Bones, clip, 0.4f, original);
    LibSaber.SM2.Assets.Meshes.ClipPose.Apply(shifted, clip, 0.4f, moved);
    var helper = rig.Clusters[^1].Bone;
    Assert.True(clip.Baked!.TrySample(mesh.Bones[helper].Name, 0.4f, out var expected));
    Assert.True(Vector3.Distance(expected.Translation, original[helper].Translation) < 1e-6f);
    Assert.True(Vector3.Distance(expected.Translation, moved[helper + 1].Translation) < 1e-6f);
    Assert.True(Vector3.Distance(expected.Translation, mesh.Bones[helper].Local.Translation) > 1e-4f, "helper bone did not move");
  }

  [Fact]
  public void SharedCdtShapesApplyToEveryCloth()
  {
    // Shape of npc_lesser_sorcerer_client.cls (trimmed): the cloths have no cdt lists, only prop-level shared shapes.
    const string text = """
      properties   =   {
         prop_cloth_sim   =   {
            sharedCDTCapsules   =   [
               {
                  name   =   "Hip_L"
                  boneFirst   =   "HIP_L"
                  radiusFirst   =   0.23
                  boneSecond   =   "LEG2L"
                  radiusSecond   =   0.2
               }
            ]
            sharedCDTSpheres   =   [
               {
                  name   =   "Pelvis"
                  bone   =   "CENTRE"
                  radius   =   0.3
               },
               {
                  bone   =   "powerplant_backpack"
               }
            ]
            clothes   =   [
               {
                  simMeshName   =   "skirt_SIM"
                  config   =   {
                     damping   =   0.1
                  }
               },
               {
                  simMeshName   =   "own_SIM"
                  config   =   {
                     cdtSpheres   =   [
                        {
                           name   =   "Pelvis"
                        }
                     ]
                  }
               }
            ]
         }
      }
      """;
    var setup = PhysicsParser.Parse(text, "sorcerer.cls");
    var skirt = setup.Cloths[0].Config;
    Assert.True(skirt.CollisionFromShared);
    Assert.Single(skirt.Capsules);
    Assert.Equal("HIP_L", skirt.Capsules[0].BoneFirst);
    Assert.Single(skirt.Spheres); // the radius-less backpack sphere is skipped
    Assert.Equal("CENTRE", skirt.Spheres[0].Bone);
    var own = setup.Cloths[1].Config;
    Assert.False(own.CollisionFromShared); // own list present: shared shapes are added on top (mtl1-31)
    Assert.Single(own.Capsules);
    Assert.Single(own.Spheres); // the own "Pelvis" reference resolves to the shared one, not added twice
    Assert.Equal(1, own.SharedShapesAdded);
  }
}
