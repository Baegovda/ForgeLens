using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class SkinMaterialTests
{
  [Theory]
  [InlineData("wpn_chainsword_01", "wpn_chainsword_01_red")]
  [InlineData("wpn_chainsword_01_nm", "wpn_chainsword_01_red_nm")]
  [InlineData("wpn_chainsword_01_spec", "wpn_chainsword_01_red_spec")]
  [InlineData("wpn_chainsword_01_em", "wpn_chainsword_01_red_em")]
  public void Candidate_inserts_material_before_the_role_suffix(string name, string expected)
    => Assert.Equal(expected, SkinMaterial.Candidate(name, "red"));

  [Fact]
  public void Apply_swaps_only_existing_siblings()
  {
    var mesh = new ExportedMesh();
    var part = new MeshPart { Texture = "a", Normal = "a_nm", Specular = "a_spec", Emissive = null };
    part.Maps.Add(new MaterialMap { Role = "diffuse", Name = "a" });
    mesh.Parts.Add(part);
    var existing = new HashSet<string> { "a_blue", "a_blue_spec" };
    var swaps = SkinMaterial.Apply(mesh, "blue", existing.Contains);

    Assert.Equal(3, swaps);
    Assert.Equal("a_blue", part.Texture);
    Assert.Equal("a_nm", part.Normal);
    Assert.Equal("a_blue_spec", part.Specular);
    Assert.Equal("a_blue", part.Maps[0].Name);
  }

  [Fact]
  public void Apply_skips_excluded_objects_and_their_children()
  {
    var mesh = new ExportedMesh();
    mesh.Bones.Add(new Bone { Name = "body", SourceName = "body", Parent = -1 });
    mesh.Bones.Add(new Bone { Name = "body_child", SourceName = "body_child", Parent = 0 });
    mesh.Bones.Add(new Bone { Name = "blade", SourceName = "blade", Parent = -1 });
    for (var i = 0; i < 3; i++)
      mesh.Parts.Add(new MeshPart { Name = mesh.Bones[i].Name, Node = i, Texture = "a" });
    var swaps = SkinMaterial.Apply(mesh, "red", n => n == "a_red", new[] { "body" });

    Assert.Equal(1, swaps);
    Assert.Equal(new[] { "a", "a", "a_red" }, mesh.Parts.Select(p => p.Texture));
  }

  [Fact]
  public void Override_skin_applies_its_material_to_the_target_tpl()
  {
    const string skins = """
meleeActors   =   {
   chainsword   =   {
      skins   =   {
         "01"   =   { geomCustomizerRegions   =   [ "cs_1" ] }
         "01_red"   =   { material   =   "red" meleeActorOverride   =   "chainsword_art" }
      }
   }
   chainsword_art   =   {
      excludeObjectsToApplyMaterial   =   [ "vfx_glow" ]
      skins   =   { "01"   =   { } }
   }
}
""";
    var lib = MeleeCustomization.FromText(skins, "regions = { cs_1 = { meshes = [ \"blade\" ] } }", null);
    lib.ActorTpl["chainsword"] = "wpn_cs";
    lib.ActorTpl["chainsword_art"] = "wpn_cs_art";
    var mesh = new ExportedMesh { Name = "wpn_cs_art" };
    var r = MeleeVariant.Apply(mesh, lib, "wpn_cs_art", VariantRequest.Parse("skin=01_red"));

    Assert.Equal("red", r.Material);
    Assert.Equal("chainsword/01_red", r.Preset);
    Assert.Contains("vfx_glow", r.MaterialExclude);
  }

  [Fact]
  public void Power_fist_hand_section_hides_its_meshes_except_kept()
  {
    var library = CustomizationLibrary.FromText(ArmorVariantTests.Library);
    var mesh = ArmorVariantTests.Tank();
    var request = VariantRequest.Parse("+powerfist_01");
    request.HideHandSection = "CHEST";
    request.HandKeep.Add("tank_neck_01");
    var result = ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", request);

    Assert.Equal(new[] { "powerfist_01", "tank_belt_base", "tank_head_01", "tank_neck_01" }, ArmorVariantTests.Visible(mesh).OrderBy(x => x));
    Assert.Contains(result.Notes, n => n.StartsWith("power fist hides the CHEST=chest_tank_common_01"));
  }

  [Fact]
  public void Hand_is_kept_when_the_tpl_has_no_fist_object()
  {
    var library = CustomizationLibrary.FromText(ArmorVariantTests.Library);
    var mesh = ArmorVariantTests.Tank();
    var request = VariantRequest.Parse("");
    request.HideHandSection = "CHEST";
    request.HandFist.Add("powerfist_99_grp");
    var result = ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", request);

    Assert.Contains("tank_cuirasse_01", ArmorVariantTests.Visible(mesh));
    Assert.Contains(result.Notes, n => n.Contains("hand kept"));
  }

  [Fact]
  public void Firearm_override_skin_resolves_the_artificer_tpl()
  {
    const string skins = """
skinsGroups   =   [
   {
      firearmUid   =   "arifle_bolt_rifle"
      skins   =   {
         "01"   =   { geomCustomizerRegions   =   [ "base_01" ] }
         "01_red"   =   { material   =   "red" geomCustomizerRegions   =   [ "base_01" ] overridedActorName   =   "arifle_bolt_rifle_artificer_01" }
      }
   }
]
""";
    var lib = WeaponCustomization.FromText(skins, "regions = { }");
    Assert.Contains("arifle_bolt_rifle_artificer_01", lib.OverrideTargets);
    lib.ActorTpl["arifle_bolt_rifle_artificer_01"] = "wpn_bolt_rifle_01_artificer_01";
    Assert.Equal("arifle_bolt_rifle", lib.OverrideUidFor("wpn_bolt_rifle_01_artificer_01", "01_red"));
    Assert.Null(lib.OverrideUidFor("wpn_bolt_rifle_01_artificer_01", "01"));
  }
}
