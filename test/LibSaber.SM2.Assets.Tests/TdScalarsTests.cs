using LibSaber.SM2.Assets.Meshes;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

public class TdScalarsTests
{
  [Fact]
  public void R50_reads_nmScale_and_emissive()
  {
    const string block = "{\n preset = \"default\"\n shaders = {\n  glt = {\n   emissive = {\n    intensity = 30.000000\n    adaptiveIntensity = true\n    bloomIntensity = 200.000000\n    animation = {\n     gradient = {\n      speed = 0.100000\n     }\n    }\n   }\n   nmScale = 0.500000\n   detail = {\n    scale = 2.0\n   }\n  }\n }\n}";
    var d = SpecLayers.TdScalars(block);
    Assert.Equal(0.5f, d["nmScale"]);
    Assert.Equal(30f, d["emissiveIntensity"]);
    Assert.Equal(200f, d["emissiveBloomIntensity"]);
    Assert.Equal(1f, d["emissiveAdaptive"]);
    Assert.Equal(4, d.Count);
  }

  [Fact]
  public void R50_empty_when_absent() => Assert.Empty(SpecLayers.TdScalars("{ shaders = { glt = { } } }"));
}
