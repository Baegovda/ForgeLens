using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-51: sfx-shader cards and placeholder sfx locators embedded in models start hidden.</summary>
public class SfxPartsTests
{
  // chainsword_blade_01.td sfx_blade_01 (trimmed): preset sfx, no blendMode (= add), tint [A,R,G,B].
  private const string BladeVariant = """
    {
          preset = "sfx"
          shaders = {
             sfx = {
                tint = [
                   255,
                   193,
                   84,
                   17
                ]
                dynamicAlphaMask = {
                   alphaVal_max = 1.000000
                }
             }
          }
       }
    """;

  [Fact]
  public void SfxPresetVariantIsSfxWithTintAndAdd()
  {
    var (sfx, blend, tint) = MaterialMaps.SfxShading(BladeVariant);
    Assert.True(sfx);
    Assert.Equal(SfxBlendMode.Unspecified, blend);
    Assert.True(SfxBlend.IsAdditive(blend));
    Assert.NotNull(tint);
    Assert.Equal(193 / 255f, tint![0], 0.001f);
    Assert.Equal(84 / 255f, tint[1], 0.001f);
    Assert.Equal(17 / 255f, tint[2], 0.001f);
    Assert.Equal(1f, tint[3], 0.001f);
  }

  [Fact]
  public void GltVariantIsNotSfx()
    => Assert.False(MaterialMaps.SfxShading("{ shaders = { glt = { blendMode = \"opaque\" } } }").Sfx);

  [Fact]
  public void HidesSfxCardsAndLocatorsButKeepsGeometry()
  {
    var mesh = new ExportedMesh();
    var body = new MeshPart { Name = "base_01", Texture = "wpn_chainsword_01", IndexCount = 300 };
    var card = new MeshPart { Name = "sfx_chainsword_attack_01", Texture = "chainsword_blade_01", IndexCount = 36, Sfx = true };
    var trail = new MeshPart { Name = "sfx_trail", Texture = "__default_material__", IndexCount = 18 };
    var exhaust = new MeshPart { Name = "exaust_source", Texture = "__default_material__", IndexCount = 24 };
    var backpack = new MeshPart { Name = "backpack", Texture = "__default_material__", IndexCount = 3000 };
    mesh.Parts.AddRange(new[] { body, card, trail, exhaust, backpack });
    MaterialMaps.HideSfxParts(mesh);
    Assert.True(body.Visible);
    Assert.True(backpack.Visible);
    Assert.False(card.Visible);
    Assert.Equal("sfx", card.HideReason);
    Assert.Equal("sfxSource", trail.HideReason);
    Assert.Equal("sfxSource", exhaust.HideReason);
  }

  [Fact]
  public void EffectOnlyModelKeepsItsCards()
  {
    var mesh = new ExportedMesh();
    var card = new MeshPart { Name = "glow", Texture = "white_simple_01", IndexCount = 36, Sfx = true };
    mesh.Parts.Add(card);
    MaterialMaps.HideSfxParts(mesh);
    Assert.True(card.Visible);
  }
}
