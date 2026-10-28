using System.Numerics;
using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class WeaponAttachTests
{
  private const string Library = @"locators = [ ""purity_seal_1"" ]
actors = {
  eagle_collision = {
    type = {
      properties = { geom = { nameTpl = ""wpn_purity_seal_eagle_01"" } }
      __type = ""purity_seal_01""
    }
    __type = ""IactorDesc""
  }
  big = {
    type = {
      properties = { geom = { nameTpl = ""wpn_big"" scale = 2 } }
      __type = ""purity_seal_01""
    }
    __type = ""IactorDesc""
  }
}";

  private const string BaseClass = @"properties = { geom = { nameTpl = ""wpn_purity_seal_eagle_01"" scale = 0.7 } }";

  [Fact]
  public void AttachingActorsInheritClassScale()
  {
    var c = new WeaponCustomization();
    c.LoadAttachingActors(Library, cls => cls == "purity_seal_01" ? BaseClass : null);
    Assert.Equal(("wpn_purity_seal_eagle_01", 0.7f, "purity_seal_01"), c.AttachingActors["eagle_collision"]);
    Assert.Equal(2f, c.AttachingActors["big"].Scale);
  }

  [Fact]
  public void SkinAttachmentsReadLocatorAndActor()
  {
    var skin = SslText.Parse(@"attachingActors = { purity_seal_3 = { locator = ""purity_seal_3"" actorName = ""demoskull_collision"" __type = ""AttachingActorDescription"" } }");
    var list = WeaponCustomization.SkinAttachments(skin);
    Assert.Single(list);
    Assert.Equal(("purity_seal_3", "purity_seal_3", "demoskull_collision"), list[0]);
  }

  private static ExportedMesh Tri(string name, int bones, Matrix4x4 rootLocal)
  {
    var m = new ExportedMesh { Name = name };
    for (var i = 0; i < bones; i++)
      m.Bones.Add(new Bone { Name = name + "_b" + i, Parent = i - 1, Local = i == 0 ? rootLocal : Matrix4x4.CreateTranslation(0, 1, 0) });
    var worlds = MeshAttach.Worlds(m.Bones);
    for (var i = 0; i < bones; i++)
    {
      Matrix4x4.Invert(worlds[i], out var ib);
      m.Bones[i].InverseBind = ib;
    }

    m.Positions.AddRange(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) });
    m.Normals.AddRange(Enumerable.Repeat(Vector3.UnitY, 3));
    m.Uvs.AddRange(Enumerable.Repeat(Vector2.Zero, 3));
    m.Indices.AddRange(new[] { 0, 1, 2 });
    for (var i = 0; i < 3; i++)
      m.Skin.Add(new SkinWeights { J0 = (ushort)(bones - 1), W0 = 1 });
    m.Parts.Add(new MeshPart { Name = name + "_part", IndexStart = 0, IndexCount = 3, Node = bones - 1 });
    return m;
  }

  private static Vector3 Skinned(ExportedMesh m, int v)
  {
    var worlds = MeshAttach.Worlds(m.Bones);
    var s = m.Skin[v];
    return Vector3.Transform(m.Positions[v], m.Bones[s.J0].InverseBind * worlds[s.J0]);
  }

  [Fact]
  public void MergePlacesAttachmentUnderLocatorWithScale()
  {
    var host = Tri("gun", 2, Matrix4x4.CreateRotationZ(0.5f) * Matrix4x4.CreateTranslation(3, 0, 0));
    var seal = Tri("seal", 2, Matrix4x4.CreateTranslation(0, 0, 2));
    var locator = MeshAttach.FindNode(host, "gun_b1");
    var merged = MeshAttach.Merge(host, seal, locator, 0.5f, "purity_seal_1");

    Assert.Equal(1, merged);
    Assert.Equal(4, host.Bones.Count);
    Assert.Equal(locator, host.Bones[2].Parent);
    Assert.Equal(2, host.Bones[3].Parent);
    Assert.Equal("purity_seal_1_seal_b0", host.Bones[2].Name);
    Assert.Equal(6, host.Positions.Count);
    Assert.Equal(new[] { 3, 4, 5 }, host.Indices.Skip(3));
    Assert.Equal(3, host.Parts[1].IndexStart);
    Assert.Equal(3, host.Parts[1].Node);
    Assert.Equal(3, host.Skin[3].J0);

    // Position = seal model point * scale * locator world, and the bind pose is preserved by the rebased inverse binds.
    var place = Matrix4x4.CreateScale(0.5f) * MeshAttach.Worlds(host.Bones)[locator];
    var expected = Vector3.Transform(new Vector3(1, 0, 0), place);
    Assert.True(Vector3.Distance(expected, host.Positions[4]) < 1e-5f);
    for (var v = 0; v < host.Positions.Count; v++)
      Assert.True(Vector3.Distance(host.Positions[v], Skinned(host, v)) < 1e-4f, $"vertex {v} moved in bind pose");
  }

  [Fact]
  public void MergeSkipsMissingLocator()
  {
    var host = Tri("gun", 1, Matrix4x4.Identity);
    Assert.Equal(0, MeshAttach.Merge(host, Tri("seal", 1, Matrix4x4.Identity), MeshAttach.FindNode(host, "purity_seal_9"), 0.7f, "x"));
    Assert.Equal(3, host.Positions.Count);
  }
}
