using LibSaber.SM2.Assets.Meshes;
using LibSaber.SpaceMarine2.Structures.Materials;

namespace LibSaber.SM2.Assets.Tests;

public class MaterialLayersTests
{
  [Fact]
  public void LayerInfosKeepsNonEmptyLayersWithBlending()
  {
    var material = new SaberMaterial
    {
      Layer0 = new MaterialLayer { TextureName = "base_cloth", Tint = new[] { 1f, 1f, 1f, 1f }, TilingU = 1, TilingV = 1, UvSetIndex = 0 },
      Layer1 = new MaterialLayer(),
      Layer2 = new MaterialLayer
      {
        TextureName = "stripe",
        MaterialName = "gold_trim",
        TilingU = 4,
        TilingV = 2,
        UvSetIndex = 1,
        VcSet = 0,
        Blending = new MaterialBlending
        {
          Method = "dirt_no_spec",
          UseLayerAlpha = true,
          WeightMultiplier = 2,
          TexChannelBlendMask = 3,
          Weights = new MaterialWeights { ColorSetIndex = 0 },
          HeightMap = new MaterialHeightMap { ColorSetIndex = 1, Invert = true },
        },
      },
    };

    var layers = TemplateMeshReader.LayerInfos(material).ToList();

    Assert.Equal(2, layers.Count);
    Assert.Equal(0, layers[0].Index);
    Assert.Null(layers[0].BlendMethod);
    var top = layers[1];
    Assert.Equal(2, top.Index);
    Assert.Equal("gold_trim", top.SubMaterial);
    Assert.Equal(4f, top.TilingU);
    Assert.Equal(1, top.UvSet);
    Assert.Equal("dirt_no_spec", top.BlendMethod);
    Assert.True(top.UseLayerAlpha);
    Assert.Equal(3, top.TexChannelBlendMask);
    Assert.Equal(0, top.WeightsColorSet);
    Assert.True(top.HeightMapInvert);
  }

  [Fact]
  public void CopyKeepsLayers()
  {
    var part = new MeshPart();
    part.Layers.Add(new MaterialLayerInfo { Index = 1, Texture = "x" });
    Assert.Single(part.Copy(0, 3).Layers);
  }
}
