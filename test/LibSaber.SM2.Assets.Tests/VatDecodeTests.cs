using System.Numerics;
using LibSaber.SM2.Assets.Effects;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

public class VatDecodeTests
{
  [Fact]
  public void RowsStepOneFramePerBandFraction()
  {
    var (v0, v1, t) = VatDecode.Rows(0.5f, 3.25f, 119, 8);
    Assert.Equal(0.5f - 3f / 119 / 8, v0, 1e-5f);
    Assert.Equal(0.5f - 4f / 119 / 8, v1, 1e-5f);
    Assert.Equal(0.25f, t, 1e-5f);
    Assert.Equal(-3f / 60, VatDecode.RowV(0.5f, 3, 60, 1), 1e-5f);
  }

  [Fact]
  public void DecodesPositionAndNormal()
  {
    var p = VatDecode.Position(new Vector3(1, 0, 1), new Vector3(-0.5f, -0.5f, -0.0624f), new Vector3(1 / 255f, 1 / 255f, 0.001397f));
    Assert.Equal(0.5f, p.X, 1e-4f); Assert.Equal(-0.5f, p.Y, 1e-4f); Assert.Equal(-0.0624f + 255 * 0.001397f, p.Z, 1e-4f);
    Assert.Equal(1f, VatDecode.Normal(new Vector3(1, 0, 0)).X, 1e-4f);
    Assert.Equal(0f, VatDecode.Normal(new Vector3(128 / 255f)).X, 1e-3f);
    Assert.Equal(118f * 0.5f, VatDecode.StageFrame(0.5f, 119), 1e-4f);
  }
}

public class VatParseTests
{
  [Fact]
  public void ParsesBloodSplashMaterial()
  {
    const string td = """
      shaderType = "shader_blood_splash"
      floatParam0 = -0.5
      floatParam1 = -0.5
      floatParam2 = -0.0624
      floatParam3 = 0.00392157
      floatParam4 = 0.00392157
      floatParam5 = 0.001397
      floatParam6 = 120
      floatParam7 = 119
      tex1 = { name = "splash_5_v2_anim_data" }
      addInfo = "splash_5_v2_anim_data_n"
      tex2 = { addInfo = "splash_5_v2_anim_data_d" }
      """;
    var p = VatDecode.Parse(td);
    Assert.NotNull(p);
    Assert.Equal("splash_5_v2_anim_data", p!.PositionTexture);
    Assert.Equal("splash_5_v2_anim_data_n", p.NormalTexture);
    Assert.Equal(119f, p.Frames);
    Assert.Equal(8, p.Bands);
    Assert.Equal(-0.0624f, p.Min.Z, 1e-6f);
    Assert.Null(VatDecode.Parse("shaderType = \"combine_material\""));
  }
}

public class VatMaterialBlockTests
{
  private const string Td = "materials = {\n   splash_05 = {\n      shaderType = \"shader_blood_splash\"\n      floatParam7 = 60.000000\n      albedo = \"splash_5_v2_anim_data\"\n      __type_id = \"material_templ\"\n   }\n   splash_07 = {\n      shaderType = \"shader_blood_splash\"\n      floatParam7 = 60.000000\n      albedo = \"splash7_v2_anim_data\"\n      __type_id = \"material_templ\"\n   }\n}";

  [Fact]
  public void PartPicksItsOwnMaterial()
  {
    var b = LibSaber.SM2.Assets.Effects.VatDecode.MaterialBlockFor(Td, "splashe07");
    Assert.NotNull(b);
    Assert.Equal("splash7_v2_anim_data", LibSaber.SM2.Assets.Effects.VatDecode.Parse(b)!.PositionTexture);
    Assert.Equal("splash_5_v2_anim_data", LibSaber.SM2.Assets.Effects.VatDecode.Parse(LibSaber.SM2.Assets.Effects.VatDecode.MaterialBlockFor(Td, "splashe05"))!.PositionTexture);
  }

  [Fact]
  public void UnknownPartFallsBack()
  {
    Assert.Null(LibSaber.SM2.Assets.Effects.VatDecode.MaterialBlockFor(Td, "splashe99"));
    Assert.Null(LibSaber.SM2.Assets.Effects.VatDecode.MaterialBlockFor(Td, null));
    Assert.Null(LibSaber.SM2.Assets.Effects.VatDecode.MaterialBlockFor(Td, "debris"));
  }
}
