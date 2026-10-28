using System.Text;
using LibSaber.SM2.Assets.Customization;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-49: generic firearm uid resolution (bolt rifle vs auto bolt rifle artificer) and colour schemes.</summary>
public sealed class FirearmResolveTests
{
  private const string Skins = """
skinsGroups   =   [
   {
      firearmUid   =   "arifle_auto_bolt_rifle"
      skins   =   {
         "01"   =   { geomCustomizerRegions   =   [ "base_01" ] }
         "01_red"   =   { material   =   "red" overridedActorName   =   "arifle_auto_bolt_rifle_artificer_01" }
      }
   }
   {
      firearmUid   =   "arifle_bolt_rifle"
      skins   =   {
         "01"   =   { geomCustomizerRegions   =   [ "base_01" ] }
         "02"   =   { geomCustomizerRegions   =   [ "base_01" ] }
         "01_red"   =   { material   =   "red" overridedActorName   =   "arifle_bolt_rifle_artificer_01" }
      }
   }
]
""";

  private static WeaponCustomization Lib()
  {
    var lib = WeaponCustomization.FromText(Skins, "regions = { }");
    lib.AddActor("ssl/weapons/common/firearm/mgs/arifle_auto_bolt_rifle/arifle_auto_bolt_rifle_client.cls", "wpn_bolt_rifle_01");
    lib.AddActor("ssl/weapons/common/firearm/mgs/arifle_bolt_rifle/arifle_bolt_rifle_client.cls", "wpn_bolt_rifle_01");
    lib.AddActor("ssl/weapons/common/firearm/firearm_versions/arifle_auto_bolt_rifle/arifle_auto_bolt_rifle_artificer_01_client.cls", "wpn_bolt_rifle_01_artificer_01");
    lib.AddActor("ssl/weapons/common/firearm/mgs/arifle_bolt_rifle_artificer_01/arifle_bolt_rifle_artificer_01_client.cls", "wpn_bolt_rifle_01_artificer_01");
    lib.AddActor("ssl/weapons/common/firearm/mgs/arifle_bolt_rifle_relic_01/arifle_bolt_rifle_relic_01_client.cls", "wpn_bolt_rifle_01_relic_01");
    lib.SortUids();
    return lib;
  }

  [Fact]
  public void Actor_is_the_file_stem_not_the_folder()
  {
    var lib = Lib();
    Assert.Equal("wpn_bolt_rifle_01_artificer_01", lib.ActorTpl["arifle_auto_bolt_rifle_artificer_01"]);
    Assert.False(lib.UidsByTpl.ContainsKey("wpn_bolt_rifle_01_artificer_01"));
    Assert.Equal("arifle_bolt_rifle", lib.UidFor("wpn_bolt_rifle_01_artificer_01"));
  }

  [Fact]
  public void Shared_override_prefers_the_mgs_actor_and_honours_uid()
  {
    var lib = Lib();
    var r = lib.Resolve("wpn_bolt_rifle_01_artificer_01", "01_red");
    Assert.Equal("arifle_bolt_rifle", r.Uid);
    Assert.True(r.Override);
    Assert.Equal(new[] { "arifle_bolt_rifle", "arifle_auto_bolt_rifle" }, r.Candidates);
    var e = lib.Resolve("wpn_bolt_rifle_01_artificer_01", "01_red", "arifle_auto_bolt_rifle");
    Assert.Equal("arifle_auto_bolt_rifle", e.Uid);
    Assert.True(e.Override);
    Assert.Equal("arifle_auto_bolt_rifle", VariantRequest.Parse("skin=01_red,uid=arifle_auto_bolt_rifle").Uid);
  }

  [Fact]
  public void Base_and_relic_tpls_resolve_by_skin_count_and_prefix_owner()
  {
    var lib = Lib();
    Assert.Equal("arifle_bolt_rifle", lib.UidFor("wpn_bolt_rifle_01"));
    Assert.Equal("arifle_bolt_rifle", lib.UidFor("wpn_bolt_rifle_01_relic_01"));
    Assert.Equal("arifle_bolt_rifle", lib.OwnerUid("arifle_bolt_rifle_relic_01"));
    Assert.Equal("arifle_auto_bolt_rifle", lib.OwnerUid("arifle_auto_bolt_rifle_artificer_01"));
  }

  [Fact]
  public void Colour_schemes_use_game_names_and_palette()
  {
    var str = "UI_MELEE_CHAINSWORD_NAME\t\t\t\t\"Chainsword\"\r\nUI_MELEE_CHAINSWORD_HM_RED_NAME\t\t\t\t\"Mephiston Red Chainsword\"\r\n"
      + "UI_MELEE_POWER_SWORD_HM_BLUE_NAME\t\t\t\t\"Gold Power Sword\"\r\n"
      + "UI_HERALDRY_COLOR_MEPHISTON_RED_NAME\t\t\t\t\"Mephiston Red\"\r\nUI_HERALDRY_COLOR_GOLD_NAME\t\t\t\t\"Gold\"\r\n";
    var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(str)).ToArray();
    var strings = ColourSchemes.ParseStrings(bytes);
    Assert.Equal("Mephiston Red Chainsword", strings["UI_MELEE_CHAINSWORD_HM_RED_NAME"]);
    var palette = ColourSchemes.ParsePalette("""
primaryShaderLayers = {
   Mephiston_Red = {
      tint = { value = { a = 255 r = 150 g = 20 b = 10 __type = "Color" } }
      spec_metalnessTint = { value = { a = 255 r = 1 g = 2 b = 3 __type = "Color" } }
   }
}
""");
    Assert.Equal((byte)150, palette["Mephiston_Red"].Tint!.Value.R);

    var cs = ColourSchemes.Describe(new[] { "red", "violet" }, "wpn_chainsword_01_artificer_01", null, false, strings, null, palette, _ => (9, 9, 9));
    Assert.Equal("Mephiston Red", cs[0].Name);
    Assert.Equal("#96140A", cs[0].Hex);
    Assert.Equal("UI_MELEE_CHAINSWORD_HM_RED_NAME", cs[0].NameKey);
    Assert.StartsWith("HEURISTIC", cs[1].NameKey);
    Assert.Contains("texture average", cs[1].SwatchSource);

    var halberd = ColourSchemes.Describe(new[] { "red" }, "wpn_omnissian_power_axe_01", "power_halberd", false,
      new Dictionary<string, string> { ["UI_MELEE_POWER_HALBERT_HM_RED_NAME"] = "Mephiston Red Omnissian Axe", ["UI_HERALDRY_COLOR_MEPHISTON_RED_NAME"] = "Mephiston Red" }, null, palette, _ => null);
    Assert.Equal("Mephiston Red", halberd[0].Name);
    Assert.StartsWith("HEURISTIC fuzzy", halberd[0].NameKey);

    var gold = ColourSchemes.Describe(new[] { "blue" }, "wpn_power_sword_01", null, false, strings, null, palette, _ => (200, 160, 60));
    Assert.Equal("Gold", gold[0].Name);
  }

  [Fact]
  public void Td_materials_lists_variants_without_default()
  {
    var td = "materials = { default = { preset = \"x\" } red = { shaders = { glt = { tex = { diffOverride = \"a_red\" } } } } blue = { preset = \"y\" } }";
    Assert.Equal(new[] { "red", "blue" }, ColourSchemes.TdMaterials(td));
  }
}
