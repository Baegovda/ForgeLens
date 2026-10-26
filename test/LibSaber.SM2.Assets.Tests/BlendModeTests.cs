using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class BlendModeTests
{
  // ch_undersuit_v1.td materials.default, trimmed: glt.blendMode "none", a nested "blend", tint layers.
  private const string Undersuit = """
    {
       preset = "default"
       shaders = {
          glt = {
             reflectance = 0.500000
             blendMode = "none"
             transpWriteDepth = false
             detail = {
                blendMode = "blend"
             }
             colorization = {
                layers = {
                   l0 = {
                      tintBlendMode = "multiply"
                      blendMode = "tint_only"
                   }
                }
             }
             __type_id = "glt_sh_templ"
          }
       }
       __type_id = "material_templ"
    }
    """;

  // ch_soldier_helmet_01.td materials.default, trimmed: no glt.blendMode, only layer "tex_blend".
  private const string Helmet = """
    {
       shaders = {
          glt = {
             layers = {
                l0 = {
                   tintBlendMode = "linear_blend"
                   blendMode = "tex_blend"
                }
             }
             __type_id = "glt_sh_templ"
          }
       }
    }
    """;

  private const string Glass = """
    {
       shaders = {
          glt = {
             blendMode = "blend"
             __type_id = "glt_sh_templ"
          }
       }
    }
    """;

  [Fact]
  public void OnlyTheDirectGltBlendModeCounts()
  {
    Assert.Equal("none", MaterialMaps.BlendMode(Undersuit));
    Assert.Null(MaterialMaps.BlendMode(Helmet));
    Assert.Equal("blend", MaterialMaps.BlendMode(Glass));
  }

  [Fact]
  public void NoneAndOpaqueDrawOpaque()
  {
    var diffuse = new MaterialMap { Role = "diffuse", Channels = "Diff(rgb)" };
    Assert.Equal("opaque", MaterialMaps.BlendOf(diffuse, "none"));
    Assert.Equal("opaque", MaterialMaps.BlendOf(diffuse, "opaque"));
    Assert.Equal("opaque", MaterialMaps.BlendOf(diffuse, null));
    Assert.Equal("translucent", MaterialMaps.BlendOf(diffuse, "blend"));
    Assert.Equal("translucent", MaterialMaps.BlendOf(diffuse, "add"));
    var akill = new MaterialMap { Role = "diffuse", Channels = "Diff(rgb)+AKill(a)" };
    Assert.Equal("masked", MaterialMaps.BlendOf(akill, "none"));
  }
}
