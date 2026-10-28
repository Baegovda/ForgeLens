using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2.Assets.Tests;

public class ClothSimSectionTests
{
  // mtl1-28: mtl15 cc_sorcerer (exported without --include-cloth-sim) converted 0 cloths because the _SIM part was hidden.
  [Fact]
  public void SimSectionExportedFollowsHideClothSim()
  {
    var mesh = new ExportedMesh { Name = "m" };
    mesh.Parts.Add(new MeshPart { Name = "skirt_SIM", IndexStart = 0, IndexCount = 3 });
    mesh.Parts.Add(new MeshPart { Name = "skirt_REND", IndexStart = 3, IndexCount = 3 });
    Assert.True(PhysicsSidecar.SimSectionExported(mesh, "skirt_sim"));
    AssetExporter.HideClothSim(mesh);
    Assert.False(PhysicsSidecar.SimSectionExported(mesh, "skirt_SIM"));
    Assert.False(PhysicsSidecar.SimSectionExported(mesh, null));
  }
}
