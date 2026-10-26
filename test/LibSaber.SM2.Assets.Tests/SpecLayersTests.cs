using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class SpecLayersTests
{
  // ch_hormagaunt_torso.td, materials.default (delta-encoded: only non-default fields).
  private const string Hormagaunt = """
    {
       shaders = {
          glt = {
             albedo_tint = [
                255,
                224,
                224,
                224
             ]
             second_spec = {
                enabled = true
                metallicTint = [
                   255,
                   252,
                   204,
                   133
                ]
                metalness = {
                   scale = 0.000000
                   bias = 0.200000
                }
                roughness = {
                   bias = 0.490000
                }
             }
             anisotropic_spec = {
                enabled = true
                anisotropy = 0.400000
                intensity = 3.000000
                extra = {
                   intensity = 4.000000
                }
             }
             __type_id = "glt_sh_templ"
          }
       }
    }
    """;

  [Fact]
  public void SecondSpec_ReadsArgbTintAndKeepsDefaults()
  {
    var second = SpecLayers.Second(Hormagaunt);
    Assert.NotNull(second);
    Assert.Equal(new[] { 252 / 255f, 204 / 255f, 133 / 255f, 1f }, second!.MetallicTint);
    Assert.Equal(0f, second.MetalnessScale);
    Assert.Equal((double)0.2f, (double)second.MetalnessBias, 4);
    Assert.Equal(0f, second.RoughnessScale);
    Assert.Equal((double)0.49f, (double)second.RoughnessBias, 4);
  }

  [Fact]
  public void Aniso_TopLevelFieldsDoNotReadExtra()
  {
    var aniso = SpecLayers.Aniso(Hormagaunt);
    Assert.NotNull(aniso);
    Assert.Equal(90f, aniso!.Angle);
    Assert.Equal((double)0.4f, (double)aniso.Anisotropy, 4);
    Assert.Equal(3f, aniso.Intensity);
    Assert.Equal(4f, aniso.ExtraIntensity);
    Assert.Equal(0.5f, aniso.ExtraAnisotropy);
    Assert.Equal((double)0.2f, (double)aniso.ExtraShift, 4);
    Assert.Equal("", aniso.DirMap);
  }

  [Fact]
  public void DisabledOrBelowCutoff_IsNull()
  {
    Assert.Null(SpecLayers.Second("{\n second_spec = {\n  metallicTint = [\n 0,\n 255,\n 255,\n 255\n ]\n }\n}"));
    Assert.Null(SpecLayers.Aniso("{\n anisotropic_spec = {\n  enabled = true\n  anisotropy = 0.005\n }\n}"));
    Assert.Null(SpecLayers.Aniso("{\n anisotropic_spec = {\n  enabled = false\n }\n}"));
  }

  // ch_calgar_head_hair_01.td, materials.hard (glt-level metalness/roughness bias, warm metallicTint).
  private const string CalgarHair = """
    {
       shaders = {
          glt = {
             metallicTint = [
                0,
                255,
                244,
                229
             ]
             metalness = {
                bias = 0.110000
             }
             second_spec = {
                enabled = true
                metallicTint = [
                   0,
                   173,
                   216,
                   255
                ]
                metalness = {
                   bias = 0.160000
                }
                roughness = {
                   bias = 0.650000
                }
             }
             roughness = {
                bias = 0.200000
             }
             __type_id = "glt_sh_templ"
          }
       }
    }
    """;

  [Fact]
  public void BaseLayer_ReadsGltLevelOnly()
  {
    var layer = SpecLayers.Base(CalgarHair);
    Assert.Equal(1f, layer.MetalnessScale);
    Assert.Equal((double)0.11f, (double)layer.MetalnessBias, 4);
    Assert.Equal(1f, layer.RoughnessScale);
    Assert.Equal((double)0.2f, (double)layer.RoughnessBias, 4);
    Assert.Equal(0.5f, layer.Reflectance);
    Assert.Equal(new[] { 1f, 244 / 255f, 229 / 255f, 0f }, layer.MetallicTint);
    var defaults = SpecLayers.Base(Hormagaunt);
    Assert.Equal(0f, defaults.MetalnessBias);
    Assert.Equal(1f, defaults.RoughnessScale);
    Assert.Equal(new[] { 1f, 1f, 1f, 0f }, defaults.MetallicTint);
  }
}
