using System.Numerics;
using System.Text.Json;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>phys52: prop_motion_damper_controller parse, authority fallback, sidecar.</summary>
public class SpringDamperTests
{
  // Shape of pc_marine_pve_story_titus_assault_authority.cls (box damper), trimmed.
  private const string Authority = """
    properties   =   {
       prop_motion_damper_controller   =   {
          springDampers   =   [
             {
                timeScale   =   5
                joints   =   [
                   {
                      name   =   "box_jiggle_02"
                      stiffness   =   7.21
                      dampingRation   =   2.18
                      mass   =   1
                      jointAngle   =   30
                      gravity   =   0
                   },
                   {
                      name   =   "box_jiggle_end"
                      stiffness   =   6.81
                      dampingRation   =   2.21
                      isParent   =   False
                      jointAngle   =   45
                      gravity   =   0.5
                   }
                ]
                constrains   =   [
                   {
                      upLocatorName   =   "cloth_cdt_2_a"
                      downLocatorName   =   "cloth_cdt_2_b"
                      radius   =   0.26
                   }
                ]
             }
          ]
       }
    }
    __type   =   "base_authority"
    """;

  private const string Client = """
    properties   =   {
       prop_phys_jiggle   =   {
          physJiggle   =   {
             objectsSettings   =   {
                belt   =   [
                   {
                      name   =   "Belt_jiggle"
                      stiffness   =   40
                      mass   =   0.2
                      damping   =   20
                      gravity   =   -1
                      offsetDirection   =   [
                         -1,
                         0,
                         0
                      ]
                      offsetDistance   =   1
                      constraintMin   =   [
                         -0.01,
                         -0.4,
                         -0.05
                      ]
                      constraintMax   =   [
                         0.01,
                         -0.35,
                         0.05
                      ]
                   }
                ]
             }
          }
       }
    }
    __type   =   "base_client"
    """;

  private static string Geom(string tpl) => $"geom   =   {{ nameTpl   =   \"{tpl}\" }}\n";

  [Fact]
  public void ParsesSpringDamperWithDefaults()
  {
    Assert.True(PhysicsParser.Declares(Authority));
    var s = PhysicsParser.Parse(Authority, "a.cls");
    Assert.False(s.IsEmpty);
    var d = Assert.Single(s.SpringDampers);
    Assert.Equal(5f, d.TimeScale);
    Assert.Equal(2, d.Joints.Count);
    Assert.Equal("box_jiggle_02", d.Joints[0].Name);
    Assert.Equal((double)(2.18f), (double)(d.Joints[0].DampingRation), 3);
    Assert.Equal(1f, d.Joints[1].Mass); // absent -> HEURISTIC default 1
    Assert.Contains("mass", d.Joints[1].Defaulted);
    Assert.False(d.Joints[1].IsParent);
    var c = Assert.Single(d.Constrains);
    Assert.Equal("cloth_cdt_2_a", c.UpBone);
    Assert.Equal((double)(0.26f), (double)(c.Radius), 3);
    Assert.Equal("a.cls", s.SpringDamperSource);
  }

  [Fact]
  public void LocatorMergesDamperFromAuthorityCounterpart()
  {
    var dir = Directory.CreateTempSubdirectory("sm2damper");
    try
    {
      File.WriteAllText(Path.Combine(dir.FullName, "x_titus_client.cls"), Geom("cc_x") + Client);
      File.WriteAllText(Path.Combine(dir.FullName, "x_titus_authority.cls"), Geom("cc_other") + Authority);
      File.WriteAllText(Path.Combine(dir.FullName, "base_client.cls"), "properties = { }\n");
      var setup = PhysicsLocator.Find(new PhysicsLocator.DirectorySource(dir.FullName), "cc_x.tpl");
      Assert.NotNull(setup);
      Assert.Single(setup!.Jiggles);
      Assert.Single(setup.SpringDampers);
      Assert.EndsWith("x_titus_client.cls", setup.SourceClass);
      Assert.EndsWith("x_titus_authority.cls", setup.SpringDamperSource);
      Assert.Contains(setup.Notes, n => n.Contains("prop_motion_damper_controller"));
    }
    finally
    {
      dir.Delete(true);
    }
  }

  [Fact]
  public void SidecarEmitsDamperAndJiggleRigidBody()
  {
    var mesh = new ExportedMesh { Name = "m" };
    mesh.Bones.Add(new Bone { Name = "CENTRE", Parent = -1 });
    mesh.Bones.Add(new Bone { Name = "Belt_jiggle", Parent = 0 });
    mesh.Bones.Add(new Bone { Name = "box_jiggle_01", Parent = 1 });
    mesh.Bones.Add(new Bone { Name = "box_jiggle_02", Parent = 2 });
    mesh.Bones.Add(new Bone { Name = "box_jiggle_end", Parent = 3 });
    mesh.Bones.Add(new Bone { Name = "cloth_cdt_2_a", Parent = 0 });
    mesh.Bones.Add(new Bone { Name = "cloth_cdt_2_b", Parent = 5 });
    var setup = PhysicsParser.Parse(Client, "c.cls");
    setup.SpringDampers.AddRange(PhysicsParser.Parse(Authority, "a.cls").SpringDampers);
    using var doc = JsonDocument.Parse(PhysicsSidecar.Serialize(mesh, setup, null, new Dictionary<string, PhysicsBakeReport>()));
    var root = doc.RootElement;
    var jig = root.GetProperty("jiggles")[0];
    Assert.Equal("Belt_jiggle", jig.GetProperty("Name").GetString());
    Assert.Equal("Belt_jiggle", jig.GetProperty("bone").GetString());
    var rb = jig.GetProperty("ue").GetProperty("rigidBody");
    Assert.Equal((double)(200f), (double)(rb.GetProperty("driveStiffness").GetSingle()), 2); // k/m
    // rest direction leans onto the clamp box (y in [-0.4,-0.35] off the offset (-1,0,0) -> source rest (-1,-0.35,0) normalised)
    var restSrc = Vector3.Normalize(new Vector3(-1, -0.35f, 0));
    var restUe = AxisConvert.Direction(restSrc, false);
    var got = rb.GetProperty("restDirUe");
    Assert.Equal((double)(restUe.X), (double)(got[0].GetSingle()), 3);
    Assert.Equal((double)(restUe.Y), (double)(got[1].GetSingle()), 3);
    Assert.Equal((double)(restUe.Z), (double)(got[2].GetSingle()), 3);
    Assert.True(rb.GetProperty("gravityEnabled").GetBoolean());
    Assert.Equal((double)(196f), (double)(rb.GetProperty("comDistanceCm").GetSingle()), 1); // 980 * 1 / (1/0.2)

    var j0 = root.GetProperty("springDampers")[0].GetProperty("joints")[0];
    Assert.Equal("box_jiggle_01", j0.GetProperty("parent").GetString());
    Assert.Equal("box_jiggle_end", j0.GetProperty("segmentTo").GetString());
    var w0 = MathF.Sqrt(7.21f) * 5f;
    Assert.Equal((double)(w0 * w0), (double)(j0.GetProperty("ue").GetProperty("driveStiffness").GetSingle()), 2);
    Assert.Equal((double)(2f * 2.18f * w0), (double)(j0.GetProperty("ue").GetProperty("driveDamping").GetSingle()), 2);
    var j1 = root.GetProperty("springDampers")[0].GetProperty("joints")[1];
    Assert.Equal("", j1.GetProperty("segmentTo").GetString()); // leaf
    var c = root.GetProperty("springDampers")[0].GetProperty("constrains")[0];
    Assert.True(c.GetProperty("upFound").GetBoolean());
    Assert.Equal((double)(26f), (double)(c.GetProperty("radiusCm").GetSingle()), 2);
  }
}
