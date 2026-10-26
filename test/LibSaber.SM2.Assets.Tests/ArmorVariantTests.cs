using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class ArmorVariantTests
{
  // Shape of ssl/body/customization/character_customization_library.sso (text SSL).
  private const string Library = """
appearancesByTeams   =   {
   LOYALIST   =   {
      customizationPresets   =   {
         PVE_PVP_TANK   =   {
            masteryTypes   =   [
               "PVP_TANK",
               "PVE_TANK"
            ]
            armorPresets   =   {
               PREDEFINED_PRESET_1   =   {
                  bodySectionRegionUids   =   {
                     HELMET   =   {
                        __value   =   "helmet_tank_no_helmet"
                     }
                     CHEST   =   {
                        __value   =   "chest_tank_common_01"
                     }
                  }
               }
               PREDEFINED_PRESET_2   =   {
                  bodySectionRegionUids   =   {
                     HELMET   =   {
                        __value   =   "helmet_tank_common_01"
                     }
                     CHEST   =   {
                        __value   =   "chest_tank_master_01"
                     }
                  }
               }
            }
            bodySections   =   {
               HELMET   =   {
                  regions   =   {
                     helmet_tank_no_helmet   =   {
                        meshes   =   [
                           "tank_head_01"
                        ]
                     }
                     helmet_tank_common_01   =   {
                        meshes   =   [
                           "tank_helmet_01_grp"
                        ]
                        meshesToHide   =   [
                           "tank_neck_01"
                        ]
                        isHidden   =   False
                     }
                  }
               }
               CHEST   =   {
                  regions   =   {
                     chest_tank_common_01   =   {
                        meshes   =   [
                           "tank_cuirasse_01",
                           "tank_neck_01"
                        ]
                     }
                     chest_tank_master_01   =   {
                        meshes   =   [
                           "tank_cuirasse_02"
                        ]
                     }
                  }
               }
            }
         }
      }
      sharedOutfitParts   =   {
         outfitParts   =   {
            CLOTH   =   {
               regions   =   {
                  vanity_cloth_shared_01   =   {
                     meshes   =   [
                        "tabard_shared_01"
                     ]
                     isForAllCharacters   =   True
                  }
               }
            }
         }
      }
   }
}
""";

  private static ExportedMesh Tank()
  {
    var mesh = new ExportedMesh { Name = "cc_sm_constructor_tank" };
    void Add(string name, int parent, bool part = true)
    {
      mesh.Bones.Add(new Bone { Name = name, SourceName = name, Parent = parent });
      if (part)
        mesh.Parts.Add(new MeshPart { Name = name, Node = mesh.Bones.Count - 1, IndexCount = 3 });
    }

    Add("Object0265", -1, part: false);            // 0
    Add("skinned_geometry", 0, part: false);       // 1
    Add("tank_head_01", 1);                        // 2
    Add("tank_helmet_01_grp", 1, part: false);     // 3
    Add("tank_helmet_01", 3);                      // 4  child of the _grp
    Add("tank_helmet_01_LOD1", 4);                 // 5  LOD child
    Add("tank_neck_01", 1);                        // 6
    Add("tank_cuirasse_01", 1);                    // 7
    Add("tank_cuirasse_02", 1);                    // 8
    Add("tabard_shared_01", 1);                    // 9
    Add("powerfist_01", 1);                        // 10
    Add("tank_belt_base", 1);                      // 11 in no region: untouched
    return mesh;
  }

  private static HashSet<string> Visible(ExportedMesh mesh)
    => mesh.Parts.Where(part => part.Visible).Select(part => part.Name).ToHashSet();

  [Fact]
  public void Ssl_text_parses_objects_arrays_and_scalars()
  {
    var root = SslText.Parse("a = { b = [ \"x\", \"y\" ] c = True d = 1.5 e = name } bare");
    var a = SslText.Obj(root, "a")!;
    Assert.Equal(new[] { "x", "y" }, SslText.Strings(a["b"]));
    Assert.Equal(true, a["c"]);
    Assert.Equal(1.5, a["d"]);
    Assert.Equal("name", a["e"]);
    Assert.True(root.ContainsKey("bare"));
    Assert.Null(root["bare"]);
  }

  [Fact]
  public void Default_preset_hides_other_regions_and_powerfists()
  {
    var library = CustomizationLibrary.FromText(Library);
    var mesh = Tank();
    var result = ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", VariantRequest.Default);

    Assert.Equal("PREDEFINED_PRESET_1", result.Preset);
    Assert.Equal(new[] { "tank_belt_base", "tank_cuirasse_01", "tank_head_01", "tank_neck_01" }, Visible(mesh).OrderBy(x => x));
    Assert.All(mesh.Parts.Where(part => !part.Visible), part => Assert.Equal(ArmorVariant.HideReason, part.HideReason));
    Assert.Equal(2, result.Presets.Count);
    Assert.Empty(result.Notes);
  }

  [Fact]
  public void Preset_two_shows_grp_subtree_and_applies_meshes_to_hide()
  {
    var library = CustomizationLibrary.FromText(Library);
    var mesh = Tank();
    ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", VariantRequest.Parse("preset=2"));

    // helmet _grp shows its child and LOD child; the helmet region's meshesToHide drops the neck.
    Assert.Equal(new[] { "tank_belt_base", "tank_cuirasse_02", "tank_helmet_01", "tank_helmet_01_LOD1" }, Visible(mesh).OrderBy(x => x));
  }

  [Fact]
  public void Section_overrides_shared_regions_and_forced_objects()
  {
    var library = CustomizationLibrary.FromText(Library);
    var mesh = Tank();
    var result = ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK",
      VariantRequest.Parse("CLOTH=vanity_cloth_shared_01; +powerfist_01, -tank_belt_base"));

    Assert.Equal("vanity_cloth_shared_01", result.Regions["CLOTH"]);
    Assert.Equal(new[] { "powerfist_01", "tabard_shared_01", "tank_cuirasse_01", "tank_head_01", "tank_neck_01" }, Visible(mesh).OrderBy(x => x));
  }

  [Fact]
  public void Unknown_region_keeps_the_preset_region_and_notes_it()
  {
    var library = CustomizationLibrary.FromText(Library);
    var mesh = Tank();
    var result = ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", VariantRequest.Parse("HELMET=nope"));
    Assert.Equal("helmet_tank_no_helmet", result.Regions["HELMET"]);
    Assert.Contains(result.Notes, note => note.Contains("HELMET=nope"));
    Assert.Contains("tank_head_01", Visible(mesh));
  }

  [Fact]
  public void Variant_never_unhides_lod_or_volume_parts()
  {
    var library = CustomizationLibrary.FromText(Library);
    var mesh = Tank();
    var lod = mesh.Parts.Single(part => part.Name == "tank_helmet_01_LOD1");
    lod.Visible = false;
    lod.HideReason = "LOD";
    ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", VariantRequest.Parse("preset=2"));
    Assert.False(lod.Visible);
    Assert.Equal("LOD", lod.HideReason);
  }

  [Fact]
  public void Request_parsing()
  {
    Assert.True(VariantRequest.Parse("all").All);
    Assert.False(VariantRequest.Parse(null).All);
    Assert.Equal(3, VariantRequest.Parse("preset=PREDEFINED_PRESET_3").Preset);
    var r = VariantRequest.Parse("appearance=chaos/PVE_PVP_SOLDIER,helmet=helmet_x");
    Assert.Equal("CHAOS", r.Team);
    Assert.Equal("PVE_PVP_SOLDIER", r.Appearance);
    Assert.Equal("helmet_x", r.Regions["HELMET"]);
    Assert.Throws<ArgumentException>(() => VariantRequest.Parse("preset=0"));
    Assert.Throws<ArgumentException>(() => VariantRequest.Parse("bogus"));
  }

  [Fact]
  public void Appearance_from_dummy_class_and_name()
  {
    var library = CustomizationLibrary.FromText(Library);
    var cls = SslText.Parse("""
__type = "cls"
geom = {
   nameTpl   =   "cc_sm_constructor_tank"
}
prop_geom_customizer_character = {
   characterInfo   =   {
      team   =   "LOYALIST"
      masteryType   =   "PVE_TANK"
   }
}
""");
    Assert.Equal(("LOYALIST", "PVE_TANK"), CustomizationLibrary.FindCharacter(cls, "cc_sm_constructor_tank"));
    Assert.Null(CustomizationLibrary.FindCharacter(cls, "cc_sm_constructor_sniper"));
    Assert.Equal("PVE_PVP_TANK", library.AppearanceForMastery("LOYALIST", "PVE_TANK"));
    Assert.Equal(("CHAOS", "PVE_SOLDIER"), CustomizationLibrary.CharacterFromName("cc_ch_constructor_soldier"));
    Assert.True(CustomizationLibrary.IsConstructorTemplate("cc_sm_constructor_tank"));
    Assert.False(CustomizationLibrary.IsConstructorTemplate("cc_titus_assault"));
  }

  [Fact]
  public void Part_copy_keeps_node()
  {
    var part = new MeshPart { Name = "a", Node = 7 };
    Assert.Equal(7, part.Copy(0, 3).Node);
  }
}
