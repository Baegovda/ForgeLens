using System.Numerics;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class UnrealRootTests
{
  private static ExportedMesh Sm2Shape()
  {
    var mesh = new ExportedMesh { Name = "m" };
    mesh.Bones.Add(new Bone { Name = "Object00C9", Parent = -1, Local = Matrix4x4.CreateTranslation(1, 2, 3) });
    mesh.Bones.Add(new Bone { Name = "ROOT", Parent = 0, Local = Matrix4x4.CreateRotationY(0.5f) * Matrix4x4.CreateTranslation(0, 1, 0) });
    mesh.Bones.Add(new Bone { Name = "CENTRE", Parent = 1, Local = Matrix4x4.CreateTranslation(0, 1, 0) });
    mesh.Bones.Add(new Bone { Name = "skinned_geometry", Parent = 0, Local = Matrix4x4.CreateTranslation(0, 0, 2) });
    return mesh;
  }

  private static Matrix4x4 World(IReadOnlyList<Bone> bones, int i)
    => bones[i].Parent < 0 ? bones[i].Local : bones[i].Local * World(bones, bones[i].Parent);

  [Fact]
  public void Promote_MakesRootTheOnlyRoot_AndKeepsWorldBinds()
  {
    var mesh = Sm2Shape();
    var promoted = UnrealRoot.Promote(mesh);
    Assert.NotSame(mesh, promoted);
    Assert.Equal(new[] { 1 }, promoted.Bones.Select((b, i) => (b, i)).Where(x => x.b.Parent < 0).Select(x => x.i));
    Assert.Equal(1, promoted.Bones[0].Parent);
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var a = World(mesh.Bones, i);
      var b = World(promoted.Bones, i);
      for (var k = 0; k < 16; k++)
        Assert.True(Math.Abs(a[k / 4, k % 4] - b[k / 4, k % 4]) < 1e-5f, $"bone {i} m{k}");
    }

    Assert.Equal(0, mesh.Bones[1].Parent); // source mesh untouched
  }

  [Fact]
  public void Promote_LeavesOtherShapesAlone()
  {
    var mesh = new ExportedMesh();
    mesh.Bones.Add(new Bone { Name = "a", Parent = -1 });
    mesh.Bones.Add(new Bone { Name = "b", Parent = -1 });
    Assert.Same(mesh, UnrealRoot.Promote(mesh));
    var single = new ExportedMesh();
    single.Bones.Add(new Bone { Name = "ROOT", Parent = -1 });
    Assert.Same(single, UnrealRoot.Promote(single));
  }

  [Fact]
  public void HasSingleRoot_CountsParentlessBones()
  {
    var tyranid = new List<Bone>
    {
      new() { Name = "cc_carnifex", Parent = -1 },
      new() { Name = "Object0080", Parent = 0 },
      new() { Name = "CENTRE", Parent = 1 },
    };
    Assert.True(UnrealRoot.HasSingleRoot(tyranid));
    Assert.Equal(-1, UnrealRoot.FindPromotable(tyranid, out _));
    Assert.False(UnrealRoot.HasSingleRoot(new List<Bone> { new() { Name = "a", Parent = -1 }, new() { Name = "b", Parent = -1 } }));
    Assert.False(UnrealRoot.HasSingleRoot(new List<Bone>()));
  }

  [Theory]
  [InlineData(1, false)] // single tpl root without ROOT (carnifex shape): no armature Null, the root is bone 0
  [InlineData(2, true)] // several roots: keep the Null so Unreal gets one skeleton
  public void FbxUnreal_ArmatureNullOnlyForSeveralRoots(int roots, bool expectNull)
  {
    var mesh = new ExportedMesh { Name = "beast" };
    mesh.Positions.Add(Vector3.Zero); mesh.Positions.Add(Vector3.UnitX); mesh.Positions.Add(Vector3.UnitY);
    mesh.Indices.Add(0); mesh.Indices.Add(1); mesh.Indices.Add(2);
    mesh.Bones.Add(new Bone { Name = "beast_root", Parent = -1, Local = Matrix4x4.Identity, InverseBind = Matrix4x4.Identity });
    mesh.Bones.Add(new Bone { Name = "CENTRE", Parent = roots == 1 ? 0 : -1, Local = Matrix4x4.CreateTranslation(0, 1, 0), InverseBind = Matrix4x4.Identity });
    var dir = Path.Combine(Path.GetTempPath(), "ur_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
      var fbx = Path.Combine(dir, "beast.fbx");
      FbxWriter.Write(fbx, mesh, ExportSpace.Unreal);
      var hasNull = File.ReadAllBytes(fbx).AsSpan().IndexOf("beast_Skeleton"u8) >= 0;
      Assert.Equal(expectNull, hasNull);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }
}
