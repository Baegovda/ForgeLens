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
    Assert.Equal(0.2f, second.MetalnessBias, 4);
    Assert.Equal(0f, second.RoughnessScale);
    Assert.Equal(0.49f, second.RoughnessBias, 4);
  }

  [Fact]
  public void Aniso_TopLevelFieldsDoNotReadExtra()
  {
    var aniso = SpecLayers.Aniso(Hormagaunt);
    Assert.NotNull(aniso);
    Assert.Equal(90f, aniso!.Angle);
    Assert.Equal(0.4f, aniso.Anisotropy, 4);
    Assert.Equal(3f, aniso.Intensity);
    Assert.Equal(4f, aniso.ExtraIntensity);
    Assert.Equal(0.5f, aniso.ExtraAnisotropy);
    Assert.Equal(0.2f, aniso.ExtraShift, 4);
    Assert.Equal("", aniso.DirMap);
  }

  [Fact]
  public void DisabledOrBelowCutoff_IsNull()
  {
    Assert.Null(SpecLayers.Second("{\n second_spec = {\n  metallicTint = [\n 0,\n 255,\n 255,\n 255\n ]\n }\n}"));
    Assert.Null(SpecLayers.Aniso("{\n anisotropic_spec = {\n  enabled = true\n  anisotropy = 0.005\n }\n}"));
    Assert.Null(SpecLayers.Aniso("{\n anisotropic_spec = {\n  enabled = false\n }\n}"));
  }
}
