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

  // ch_hellbrute_armor_01.td "blue": color0 unset.
  private const string HellbruteBlue = """
    {
       shaders = {
          glt = {
             chameleon = {
                enabled = true
                color1 = [
                   255,
                   0,
                   255,
                   255
                ]
                offset1 = 0.500000
                offset2 = 0.900000
                fresnelPwr = 0.700000
             }
             __type_id = "glt_sh_templ"
          }
       }
    }
    """;

  [Fact]
  public void Chameleon_UnsetValuesAreZeroLikeTheEngineParser()
  {
    var cham = SpecLayers.Chameleon(HellbruteBlue)!;
    Assert.Equal(new[] { 0f, 0f, 0f, 0f }, cham.Colors[0]);
    Assert.Equal(new[] { 0f, 1f, 1f, 1f }, cham.Colors[1]);
    Assert.Equal(new[] { 0f, 0f, 0f, 0f }, cham.Colors[3]);
    Assert.Equal(0.7f, cham.FresnelPower, 1e-3f);
    var bare = SpecLayers.Chameleon("""
      {
         shaders = {
            glt = {
               chameleon = {
                  enabled = true
               }
            }
         }
      }
      """)!;
    Assert.Equal(0f, bare.Offset2);
    Assert.Equal(0f, bare.FresnelPower);
  }

  [Fact]
  public void ChameleonRamp_MatchesCpuPacker()
  {
    var (r14, r15) = ChameleonSpec.Ramp(0.5f, 0.9f, 0.7f);
    Assert.Equal(0f, r14[0]);
    Assert.Equal(2f, r14[1], 1e-3f);
    Assert.Equal(0.5f, r14[2]);
    Assert.Equal(2.5f, r14[3], 1e-3f);
    Assert.Equal(0.9f, r15[0]);
    Assert.Equal(10f, r15[1], 1e-3f);
    Assert.Equal(0.7f, r15[2]);
    // offset2 < offset1 -> offset1 + eps; both clamped to [0, 1].
    var (b14, b15) = ChameleonSpec.Ramp(1.5f, 0.2f, 1f);
    Assert.Equal(1f, b14[2]);
    Assert.Equal(1f, b15[0]);
    Assert.True(b15[1] > 1e5f);
  }
}
