using LibSaber.SM2.Assets.Customization;

namespace LibSaber.SM2.Assets.Tests;

public class KitbashTests
{
  private const string Extra = """
appearancesByTeams   =   {
   LOYALIST   =   {
      customizationPresets   =   {
         PVE_PVP_TANK   =   {
            masteryTypes   =   [ "PVP_TANK", "PVE_TANK" ]
            armorPresets   =   {
               PREDEFINED_PRESET_1   =   {
                  bodySectionRegionUids   =   {
                     HELMET   =   { __value   =   "helmet_tank_no_helmet" }
                  }
               }
               PREDEFINED_PRESET_8   =   {
                  isChampion   =   True
                  dlcLock   =   { dlcUid   =   "SEASON_2_DARK_ANGELS" }
                  skinOverrides   =   {
                     PVP_TANK   =   { __value   =   "PvpSkinChampion01" }
                     PVE_TANK   =   { __value   =   "PveSkinChampion01" }
                  }
               }
            }
            bodySections   =   {
               HELMET   =   { regions   =   { helmet_tank_no_helmet   =   { meshes   =   [ "tank_head_01" ] } } }
            }
         }
         PVE_PVP_SNIPER   =   { masteryTypes   =   [ "PVE_SNIPER" ] }
      }
      sharedOutfitParts   =   {
         outfitParts   =   {
            HELMET   =   {
               regions   =   {
                  helmet_custom_a   =   { meshes   =   [ "helmet_a" ] isForAllCharacters   =   False appearanceUids   =   [ "PVE_PVP_SNIPER" ] }
                  helmet_custom_b   =   { meshes   =   [ "helmet_b" ] appearanceUids   =   [ "PVP_TANK" ] }
                  helmet_custom_c   =   { meshes   =   [ "helmet_c" ] }
               }
            }
         }
      }
   }
}
""";

  [Fact]
  public void Library_reads_teams_classes_presets_and_shared_parts()
  {
    var kit = KitbashLibrary.From(CustomizationLibrary.FromText(Extra));
    Assert.Equal(new[] { "LOYALIST" }, kit.Teams);
    var tank = kit.Class("LOYALIST", "PVE_PVP_TANK")!;
    Assert.Equal(2, tank.Presets.Count);
    Assert.Equal("helmet_tank_no_helmet", tank.Presets[0].Regions["HELMET"]);
    var champion = tank.Presets[1];
    Assert.True(champion.IsChampion);
    Assert.Equal("PveSkinChampion01", champion.ChampionSkin);
    Assert.Equal(8, champion.Number);

    // Class parts first, then shared parts that fit the class (b names PVP_TANK, c names nobody); a is sniper-only.
    Assert.Equal(new[] { "helmet_tank_no_helmet", "helmet_custom_b", "helmet_custom_c" }, kit.Options(tank, "HELMET", true).Select(p => p.Uid));
    Assert.Equal(4, kit.Options(tank, "HELMET", false).Count);
    Assert.Equal(new[] { "HELMET" }, kit.Slots(tank, true));
    Assert.True(kit.Options(tank, "HELMET", true)[1].Shared);
  }

  [Fact]
  public void Template_candidates_and_spec()
  {
    var kit = KitbashLibrary.From(CustomizationLibrary.FromText(Extra));
    var tank = kit.Class("LOYALIST", "PVE_PVP_TANK")!;
    Assert.Equal(new[] { "cc_sm_constructor_tank" }, KitbashLibrary.TemplateCandidates("LOYALIST", "PVE_PVP_TANK", tank.Presets[0]));
    Assert.Equal(new[] { "cc_sm_champion_tank_01", "cc_sm_constructor_tank" }, KitbashLibrary.TemplateCandidates("LOYALIST", "PVE_PVP_TANK", tank.Presets[1]));
    Assert.Equal(new[] { "cc_ch_constructor_soldier" }, KitbashLibrary.TemplateCandidates("CHAOS", "PVE_PVP_SOLDIER", null));
    Assert.Equal(new[] { "cc_sm_constructor_tank", "cc_sm_tank_story" }, KitbashLibrary.TemplateCandidates("LOYALIST", "STORY_TANK", null));

    var spec = KitbashLibrary.Spec("LOYALIST", "PVE_PVP_TANK", tank.Presets[0], new Dictionary<string, string?> { ["HELMET"] = "helmet_custom_b", ["CHEST"] = null });
    Assert.Equal("appearance=LOYALIST/PVE_PVP_TANK,preset=1,CHEST=none,HELMET=helmet_custom_b", spec);
    var request = VariantRequest.Parse(spec);
    Assert.Equal("PVE_PVP_TANK", request.Appearance);
    Assert.Equal("none", request.Regions["CHEST"]);
  }

  [Fact]
  public void None_slot_hides_every_region_of_that_slot()
  {
    var library = CustomizationLibrary.FromText(ArmorVariantTests.Library);
    var mesh = ArmorVariantTests.Tank();
    var result = ArmorVariant.Apply(mesh, library, "LOYALIST", "PVE_PVP_TANK", VariantRequest.Parse("HELMET=none"));
    Assert.False(result.Regions.ContainsKey("HELMET"));
    Assert.Equal(new[] { "tank_belt_base", "tank_cuirasse_01", "tank_neck_01" }, ArmorVariantTests.Visible(mesh).OrderBy(x => x));
  }

  [Fact]
  public void Real_library_when_present()
  {
    const string path = "/workspace/ui43/ssl/body/customization/character_customization_library.sso";
    if (!File.Exists(path))
      return;
    var kit = KitbashLibrary.From(CustomizationLibrary.FromText(File.ReadAllText(path)));
    Assert.Contains("CHAOS", kit.Teams);
    var tank = kit.Class("LOYALIST", "PVE_PVP_TANK")!;
    Assert.Equal(10, tank.Presets.Count);
    Assert.Contains("VANITY", kit.Slots(tank, true));
    Assert.True(kit.Options(tank, "HELMET", true).Count > 7);
    Assert.True(kit.Options(tank, "HELMET", false).Count >= kit.Options(tank, "HELMET", true).Count);
  }
}
