using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Meshes;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-52: character colour schemes (heraldry chapters) and the customization-layer bake.</summary>
public sealed class ArmorColourSchemeTests
{
  private static string Col(string name, int r, int g, int b, bool metallic = false, string extra = "") => $$"""
   {{name}}   =   {
      isMetallic   =   {{(metallic ? "True" : "False")}}
      {{extra}}
      tint   =   { value   =   { a   =   255 r   =   {{r}} g   =   {{g}} b   =   {{b}} } }
      spec_metalnessTint   =   { value   =   { a   =   255 r   =   {{r}} g   =   {{g}} b   =   {{b}} } }
   }
""";

  private static readonly string Palette =
    "primaryShaderLayers   =   {\n" + Col("Macragge_Blue", 67, 84, 123) + Col("Mephiston_Red", 150, 20, 20) + "}\n" +
    "secondaryShaderLayers   =   {\n" + Col("Macragge_Blue", 67, 84, 123) + Col("Mephiston_Red", 150, 20, 20) + "}\n" +
    "tertiaryShaderLayers   =   {\n" + Col("Liberator_Gold", 236, 210, 154) + Col("Corvus_Black", 20, 20, 20) + Col("Mephiston_Red", 160, 30, 30, true, "spec_metalness_bias   =   { value   =   1 }") + "}\n" +
    "lensesShaderLayers   =   {\n" + Col("Caliban_Green", 0, 200, 0) + "}\n";

  private const string Schemes = """
defaultColoringLink   =   "Ultramarines"
characterColoringInfoCollection   =   {
   Blood_Angels   =   {
      mainColoring   =   { colors   =   { primary   =   "Mephiston_Red" secondary   =   "Mephiston_Red" tertiary   =   "Mephiston_Red" } }
      bodySectionsColoring   =   {
         HELMET_LENSES   =   { colors   =   { primary   =   "Caliban_Green" } }
         CHEST   =   { colors   =   { primary   =   "Mephiston_Red" secondary   =   "Mephiston_Red" tertiary   =   "Corvus_Black" } }
      }
   }
   Ultramarines   =   {
      decals   =   [ { } ]
   }
   Some_Champion   =   {
   }
}
""";

  private const string Ui = """
chapters   =   {
   Blood_Angels   =   { chapterName   =   "UI_HERALDRY_LEGION_BLOODANGELS_NAME" patternName   =   "UI_HERALDRY_LEGION_BLOODANGELS_NAME" }
   Ultramarines   =   { patternName   =   "UI_HERALDRY_LEGION_ULTRAMARINES_NAME" }
}
colors   =   {
   Mephiston_Red   =   { __value   =   "UI_HERALDRY_COLOR_MEPHISTON_RED_NAME" }
}
""";

  private static readonly Dictionary<string, string> Strings = new(StringComparer.OrdinalIgnoreCase)
  {
    ["UI_HERALDRY_LEGION_BLOODANGELS_NAME"] = "BLOOD ANGELS",
    ["UI_HERALDRY_LEGION_ULTRAMARINES_NAME"] = "ULTRAMARINES",
    ["UI_HERALDRY_COLOR_MEPHISTON_RED_NAME"] = "Mephiston Red",
  };

  [Fact]
  public void SchemesResolveSlotsDefaultsAndNames()
  {
    var list = ArmorColourSchemes.Parse(Schemes, Palette, Ui, null, Strings, null);
    Assert.Equal("Ultramarines", list[0].Id); // defaultColoringLink first
    var um = list[0];
    Assert.Equal("Ultramarines", um.Name);
    Assert.Equal(new[] { "Macragge_Blue", "Macragge_Blue", "Liberator_Gold" }, um.Main.Select(l => l.Colour));
    var ba = list.Single(s => s.Id == "Blood_Angels");
    Assert.Equal("Blood Angels", ba.Name);
    Assert.Equal("Mephiston Red", ba.Main[0].Name);
    // tertiary resolves in tertiaryShaderLayers (metallic variant), not the primary entry
    Assert.True(ba.Main[2].Metallic);
    Assert.Equal(160, ba.Main[2].TintR);
    Assert.Equal("Corvus_Black", ba.Sections["CHEST"][2].Colour);
    Assert.Equal("Caliban_Green", ba.Lenses?.Colour);
    Assert.StartsWith("HEURISTIC", list.Single(s => s.Id == "Some_Champion").NameKey);
    Assert.Same(ba.Sections["CHEST"], ArmorColourSchemes.LayersFor(ba, "chest_plate", "ch_titus_chest"));
    Assert.Same(ba.Main, ArmorColourSchemes.LayersFor(ba, "legs", "ch_titus_legs"));
  }

  [Fact]
  public void CustomizationSlotFromTd()
  {
    const string td = """
materials = {
   default = { preset = "default" }
   "default CC" = {
      shaders = { glt = {
         tex = { diffOverride = "ch_titus_body_a" }
         customization = { mask = "ch_titus_body_cc" texArrayDiffuse = "ch_custom_01"
            layer0 = { tintBlendMode = "linear_blend" } layer1 = { tintBlendMode = "linear_blend" }
            layer2 = { tintBlendMode = "linear_blend" } layer3 = { tintBlendMode = "linear_blend" enabled = false } }
      } }
   }
}
""";
    var slot = CustomizationSlot.FromTd(td, "default");
    Assert.NotNull(slot);
    Assert.Equal("ch_titus_body_cc", slot!.Mask);
    Assert.Equal("ch_titus_body_a", slot.DiffOverride);
    Assert.Equal(new[] { true, true, true, false }, slot.Enabled);
    Assert.Null(CustomizationSlot.FromTd("materials = { default = { preset = \"default\" } }", "default"));
  }

  [Fact]
  public void BakeTintsByMaskChannel()
  {
    // 2x1: pixel 0 mask red (layer0), pixel 1 mask green (layer1); grey base
    var baseImg = new PreviewImage { Width = 2, Height = 1, Bgra = new byte[] { 200, 200, 200, 255, 200, 200, 200, 255 } };
    var mask = new PreviewImage { Width = 2, Height = 1, Bgra = new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 } };
    var list = ArmorColourSchemes.Parse(Schemes, Palette, Ui, null, Strings, null);
    var um = list[0];
    var red = list.Single(s => s.Id == "Blood_Angels").Main;
    var layers = new ArmorLayer?[] { red[0], um.Main[0], null, null };
    var outImg = ArmorSchemeBake.Albedo(baseImg, mask, null, layers, new[] { true, true, true, false });
    Assert.True(outImg.Bgra[2] > outImg.Bgra[0] + 60); // pixel 0 red
    Assert.True(outImg.Bgra[4] > outImg.Bgra[6]); // pixel 1 blue
    var json = ArmorSchemeBake.UeParams(um, new[] { ("body", (string?)"ch_titus_body", CustomizationSlot.FromTd("materials = { \"default CC\" = { shaders = { glt = { customization = { mask = \"m_cc\" layer0 = { } } } } } }", "default")!) });
    Assert.Contains("\"Layer0Tint\"", json);
    Assert.Contains("\"mask\": \"m_cc\"", json);
  }

  [Fact]
  public void TintBlendModeLinearReplacesMultiplyScalesBase()
  {
    // grey base (sRGB 128), full layer0 mask; tint white-ish from the scheme -> linear_blend gives the tint, multiply tint x base
    var baseImg = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 128, 128, 128, 255 } };
    var mask = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 0, 0, 255, 255 } };
    var list = ArmorColourSchemes.Parse(Schemes, Palette, Ui, null, Strings, null);
    var red = list.Single(s => s.Id == "Blood_Angels").Main;
    var layers = new ArmorLayer?[] { red[0] with { TintA = 255 }, null, null, null };
    var on = new[] { true, false, false, false };
    var lin = ArmorSchemeBake.Albedo(baseImg, mask, null, layers, on, new[] { "linear_blend", null, null, null });
    var mul = ArmorSchemeBake.Albedo(baseImg, mask, null, layers, on, new[] { "multiply", null, null, null });
    Assert.Equal(red[0].TintR, lin.Bgra[2]); // replace: albedo = tint (metal 0)
    Assert.True(mul.Bgra[2] < lin.Bgra[2]); // multiply by the 0.5 base is darker
    Assert.True(ArmorSchemeBake.MultipliesBase("multiply"));
    Assert.False(ArmorSchemeBake.MultipliesBase("linear_blend"));
  }

  [Fact]
  public void SlotReadsPatternBlendMode()
  {
    var slot = CustomizationSlot.FromTd("materials = { \"default CC\" = { shaders = { glt = { customization = { mask = \"m_cc\" layer0 = { tintBlendMode = \"multiply\" blendMode = \"tint_only\" } layer1 = { tintBlendMode = \"linear_blend\" blendMode = \"tex_blend\" } } } } } }", "default")!;
    Assert.Equal("multiply", slot.BlendModes[0]);
    Assert.Equal("tint_only", slot.PatternModes[0]);
    Assert.Equal("tex_blend", slot.PatternModes[1]);
    Assert.True(ArmorSchemeBake.IsTintOnly(slot.PatternModes[0]));
  }

  [Fact]
  public void SlotReadsTexArraysDensityAndCustomUv()
  {
    var slot = CustomizationSlot.FromTd("materials = { \"default CC\" = { shaders = { glt = { customization = { mask = \"m_cc\" texArrayDiffuse = \"ch_custom_01\" texArraySpec = \"ch_custom_01_spec\" texArrayEmissiveMask = \"ch_custom_flat_em\" layer0 = { density = 2.000000 useCustomUVSet = false blendMode = \"tex_blend\" } layer1 = { useCustomUVSet = true } } } } } }", "default")!;
    Assert.Equal("ch_custom_01", slot.TexArrayDiffuse);
    Assert.Equal("ch_custom_01_spec", slot.TexArraySpec);
    Assert.Equal("ch_custom_flat_em", slot.TexArrayEmissiveMask);
    Assert.Equal(2f, slot.LayerDensity[0]);
    Assert.Equal(1f, slot.LayerDensity[1]);
    Assert.False(slot.UseCustomUVSet[0]);
    Assert.True(slot.UseCustomUVSet[1]);
  }

  [Fact]
  public void LayerReadsPatternAndEmissiveParams()
  {
    const string pal = """
      lensesShaderLayers = {
        Evil_Sunz_Scarlet = {
          textureIndex = { value = 0 }
          maps_emissive = { value = 0 }
          emissive_tint = { value = { a = 255 r = 194 g = 25 b = 31 } }
          emissive_intensity = { value = 35 }
          emissive_bloomIntensity = { value = 10 }
        }
      }
      """;
    var l = ArmorColourSchemes.ParseLayers(pal)["lensesShaderLayers"]["Evil_Sunz_Scarlet"];
    Assert.Equal(0, l.TextureIndex);
    Assert.Equal(0, l.MapEmissive);
    Assert.Equal(-1, l.MapSpec);
    Assert.Equal(1f, l.Density);
    Assert.Equal("#C2191F", l.EmissiveHex);
    Assert.Equal(35f, l.EmissiveIntensity);
    Assert.Equal(10f, l.EmissiveBloom);
  }

  [Fact]
  public void TexBlendPatternFollowsSliceAlpha()
  {
    // glt PS tex_blend: slice alpha 1 -> lerp(slice, patternTint, patternTint.a); slice alpha 0 -> the tint
    var baseImg = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 128, 128, 128, 255 } };
    var mask = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 0, 0, 255, 255 } };
    var opaque = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 109, 109, 109, 255 } };
    var clear = new PreviewImage { Width = 1, Height = 1, Bgra = new byte[] { 109, 109, 109, 0 } };
    var layer = new ArmorLayer("primary", "X", "X", 200, 10, 10, 255, 200, 10, 10, 10, 200, 10, 255, false, 0, 0, 1, 0, 0, "test");
    var on = new[] { true, false, false, false };
    var modes = new[] { "linear_blend", null, null, null };
    var pm = new[] { "tex_blend", null, null, null };
    ArmorSchemeBake.PatternArrays A(PreviewImage s, bool customUv = false) => new(new[] { s }, null, new[] { 1f, 1f, 1f, 1f }, new[] { customUv, false, false, false });
    var pat = ArmorSchemeBake.Albedo(baseImg, mask, null, new ArmorLayer?[] { layer, null, null, null }, on, modes, pm, A(opaque));
    Assert.Equal(new byte[] { 10, 200, 10 }, pat.Bgra[..3]); // BGR of patternTint (10,200,10)
    var grey = ArmorSchemeBake.Albedo(baseImg, mask, null, new ArmorLayer?[] { layer with { PatternA = 0 }, null, null, null }, on, modes, pm, A(opaque));
    Assert.Equal(109, grey.Bgra[2]); // the slice itself
    var tint = ArmorSchemeBake.Albedo(baseImg, mask, null, new ArmorLayer?[] { layer, null, null, null }, on, modes, pm, A(clear));
    Assert.Equal(new byte[] { 10, 10, 200 }, tint.Bgra[..3]); // tint (200,10,10)
    var uv2 = ArmorSchemeBake.Albedo(baseImg, mask, null, new ArmorLayer?[] { layer, null, null, null }, on, modes, pm, A(opaque, customUv: true));
    Assert.Equal(new byte[] { 10, 10, 200 }, uv2.Bgra[..3]); // useCustomUVSet: tint path
    var none = ArmorSchemeBake.Albedo(baseImg, mask, null, new ArmorLayer?[] { layer with { TextureIndex = -1 }, null, null, null }, on, modes, pm, A(opaque));
    Assert.Equal(new byte[] { 10, 10, 200 }, none.Bgra[..3]);
  }

  [Theory]
  [InlineData("berserk_shoulder_L_01", "LEFT_PAULDRON")]
  [InlineData("assault_shoulder_r_02", "RIGHT_PAULDRON")]
  [InlineData("tac_hand_L_01", "LEFT_GAUNTLET")]
  [InlineData("tac_knee_R_01", "RIGHT_GREAVE")]
  [InlineData("chest_plate", "CHEST")]
  [InlineData("assault_jumppack_01", "VANITY_2")]
  [InlineData("legs", null)]
  [InlineData("shoulders_01", null)]
  public void R48_SectionOf_side(string part, string? expected) => Assert.Equal(expected, ArmorColourSchemes.SectionOf(part, null));
}
