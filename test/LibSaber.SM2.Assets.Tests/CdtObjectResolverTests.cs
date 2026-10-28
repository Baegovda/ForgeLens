using System.Numerics;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2.Assets.Tests;

public class CdtObjectResolverTests
{
  // Shape of cc_calgar.tpl_markup (trimmed): a locator entity hanging off a template object.
  private const string Markup = """
    objectInfos   =   [
       {
          objectId   =   {
             name   =   "ROOT"
          }
          animationRole   =   "RIG_INCLUDE"
       },
       {
          entities   =   [
             {
                name   =   "Backpack_CDT_L"
                localTransform   =   {
                   position   =   [
                      0.2031592,
                      0,
                      -0.5929316
                   ]
                   rotation   =   [
                      0,
                      90,
                      0
                   ]
                }
                __type   =   "tpl_markup_locator"
             }
          ]
          objectId   =   {
             name   =   "powerplant_backpack"
          }
          animationRole   =   "RIG_INCLUDE"
       }
    ]
    """;

  private static List<Bone> Bones() => new()
  {
    new Bone { Name = "ROOT", Parent = -1 },
    new Bone { Name = "powerplant_backpack", Parent = 0 },
  };

  [Fact]
  public void Locators_ParseParentAndTransform()
  {
    var locs = TplMarkup.Locators(Markup);
    var l = Assert.Single(locs);
    Assert.Equal("Backpack_CDT_L", l.Name);
    Assert.Equal("powerplant_backpack", l.Parent);
    Assert.Equal((double)(0.2031592f), (double)(l.Position.X), 5);
    Assert.Equal((double)(-0.5929316f), (double)(l.Position.Z), 5);
    Assert.Equal((double)(90f), (double)(l.RotationDegrees.Y), 3);
  }

  [Fact]
  public void Apply_MovesBoxOntoParentWithFoldedOffsetAndRotation_Idempotent()
  {
    var setup = new PhysicsSetup();
    var entry = new ClothEntry { SimMeshName = "cloak_SIM" };
    var box = new ClothBox { Bone = "Backpack_CDT_L", BoneOffset = Vector3.Zero };
    entry.Config.Boxes.Add(box);
    var other = new ClothEntry { SimMeshName = "belt_SIM" };
    other.Config.Boxes.Add(box); // shared shape object
    setup.Cloths.Add(entry);
    setup.Cloths.Add(other);
    var locs = TplMarkup.Locators(Markup);

    Assert.Equal(1, CdtObjectResolver.Apply(setup, Bones(), locs));
    Assert.Equal("powerplant_backpack", box.Bone);
    Assert.Equal("Bone=Backpack_CDT_L", box.ResolvedFrom);
    Assert.Equal((double)(0.2031592f), (double)(box.BoneOffset.X), 4);
    Assert.Equal((double)(-0.5929316f), (double)(box.BoneOffset.Z), 4);
    Assert.True(Math.Abs(Math.Abs(box.Rotation.Y) - MathF.Sqrt(0.5f)) < 1e-4f);
    Assert.Contains(setup.Notes, n => n.Contains("mtl1-37") && n.Contains("Backpack_CDT_L"));

    Assert.Equal(0, CdtObjectResolver.Apply(setup, Bones(), locs));
    Assert.Equal("powerplant_backpack", box.Bone);
  }

  [Fact]
  public void Apply_LeavesKnownBonesAndUnknownNames()
  {
    var setup = new PhysicsSetup();
    var entry = new ClothEntry();
    entry.Config.Capsules.Add(new ClothCapsule { BoneFirst = "ROOT", BoneSecond = "Nope_CDT" });
    setup.Cloths.Add(entry);
    Assert.Equal(0, CdtObjectResolver.Apply(setup, Bones(), TplMarkup.Locators(Markup)));
    Assert.Equal("Nope_CDT", entry.Config.Capsules[0].BoneSecond);
    Assert.Equal("", entry.Config.Capsules[0].ResolvedFrom);
  }

  [Fact]
  public void RemapDefaults_FromRetailCtor()
  {
    var e = new ClothEntry();
    Assert.Equal((double)(0.15f), (double)(e.MaxInfluenceDist), 5);
    Assert.Equal((double)(5f), (double)(e.DistanceRemapPower), 5);
    Assert.False(e.UseTopologicalDistance);
    Assert.Null(e.MaxInfluenceVert);
  }
}
