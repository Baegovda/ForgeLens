using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2.Assets.Tests;

public class TextureUsageTests
{
  [Theory]
  [InlineData("MSCRGHAO", 1u << 21)]
  [InlineData("MSCG", 1u << 18)]
  [InlineData("MSCG+MRGH", (1u << 18) | (1u << 3))]
  [InlineData("", 0u)]
  public void ParseMask_MatchesExeTable(string usage, uint expected)
    => Assert.Equal(expected, TextureUsage.ParseMask(usage));

  [Fact]
  public void FromTd_ReadsTopLevelUsage()
  {
    var td = "rendering = { linear_rgb = false }\nusage = \"MSCRGHAO\"\nconvert_settings = { format_name = \"MSCG+MRGH\" }\n";
    Assert.Equal("MSCRGHAO", TextureUsage.FromTd(td));
  }
}

public class UseSpecMapPolicyTests
{
  [Theory]
  [InlineData("MSCRGHAO", true)]
  [InlineData("MSCG+MRGH", true)]
  [InlineData("MSCG", true)]
  [InlineData("MRGH", false)]
  [InlineData("MAO", false)]
  [InlineData("", false)]
  public void Decide_MatchesProvenUsageBits(string usage, bool expected)
    => Assert.Equal(expected, UseSpecMapPolicy.Decide(usage));

  [Fact]
  public void MeshPart_UsesSpecularUsageNotChameleonHeuristic()
  {
    var withMscr = new MeshPart { Specular = "armor_spec", SpecularUsage = "MSCRGHAO" };
    Assert.True(UseSpecMapPolicy.Decide(withMscr));
    // Chameleon alone no longer forces off when MSCRGHAO is present (engine has no chameleon branch).
    withMscr.Chameleon = new ChameleonSpec();
    Assert.True(UseSpecMapPolicy.Decide(withMscr));

    var chameleonNoSpec = new MeshPart { Chameleon = new ChameleonSpec() };
    Assert.False(UseSpecMapPolicy.Decide(chameleonNoSpec));

    var mscg = new MeshPart { Specular = "hb_spec", SpecularUsage = "MSCG+MRGH" };
    Assert.True(UseSpecMapPolicy.Decide(mscg));
  }

  [Fact]
  public void EvidenceMentionsProvenWriter()
  {
    Assert.Contains("PROVEN", UseSpecMapPolicy.Evidence);
    Assert.Contains("FUN_14217da70", UseSpecMapPolicy.Evidence);
    Assert.Contains("MSCRGHAO", UseSpecMapPolicy.Evidence);
    Assert.DoesNotContain("HEURISTIC", UseSpecMapPolicy.Evidence);
  }
}

public class RestFitPolicyTests
{
  [Fact]
  public void DefaultsMatchR76R91()
  {
    Assert.False(RestFitPolicy.Enabled);
    Assert.Equal(0.9f, RestFitPolicy.Floor);
    Assert.Equal(0.5f, RestFitPolicy.DeepFrac);
    Assert.Equal(1.0f, RestFitPolicy.ShallowFracDefault);
    Assert.Contains("HEURISTIC", RestFitPolicy.Evidence);
    Assert.Contains("collisionThickness", RestFitPolicy.Evidence);
  }

  [Theory]
  [InlineData("coat_SIM_01", 0.04f)]
  [InlineData("straps_SIM_01", 0.04f)]
  [InlineData("COAT_SIM_01", 0.04f)]
  [InlineData("cloak_SIM_01", 1.0f)]
  [InlineData(null, 1.0f)]
  public void ShallowFracFor_CommissarOverrides(string? name, float expected)
    => Assert.Equal(expected, RestFitPolicy.ShallowFracFor(name));

  [Fact]
  public void CVarString_ListsCommissarCloths()
  {
    var s = RestFitPolicy.ShallowOverrideCVar();
    Assert.Contains("coat_SIM_01=0.04", s);
    Assert.Contains("straps_SIM_01=0.04", s);
  }
}

public class LevelLightUeTests
{
  [Fact]
  public void ArgbOrder_MatchesProvenColorCtrl()
  {
    var c = LevelLightUe.ParseColorCtrlToLinearRgb("[255,165,207,226]");
    Assert.Equal(MathF.Pow(165 / 255f, 2.2f), c.X, 1e-5f);
    Assert.Equal(MathF.Pow(207 / 255f, 2.2f), c.Y, 1e-5f);
    Assert.Equal(MathF.Pow(226 / 255f, 2.2f), c.Z, 1e-5f);
  }

  [Fact]
  public void MetresToUeCm_SwapsYZ()
  {
    var p = LevelLightUe.MetresToUeCm(1f, 2f, 3f);
    Assert.Equal(100f, p.X);
    Assert.Equal(300f, p.Y);
    Assert.Equal(200f, p.Z);
  }

  [Fact]
  public void UeIntensity_UsesEngineColorScale_NoFitK()
  {
    Assert.Equal(8f, LevelLightUe.UeIntensity(8f));
    Assert.Equal(800f, LevelLightUe.UeIntensity(2f, useInverseSquaredFalloff: true));
    Assert.Contains("PROVEN", LevelLightUe.IntensityEvidence);
    Assert.DoesNotContain("HEURISTIC FIT K=10", LevelLightUe.IntensityEvidence);
    Assert.Contains("PROVEN", LevelLightUe.ColorOrderEvidence);
  }

  [Fact]
  public void P0w_MatchesFun14224f010()
  {
    Assert.Equal(1f, LevelLightUe.P0w(0f), 1e-6f);
    Assert.Equal(300f, LevelLightUe.P0w(1f), 1e-4f);
    Assert.Equal(0.0025f, LevelLightUe.P0w(1f, useInverseSquaredFalloff: true), 1e-7f);
    Assert.Equal(8e22f, LevelLightUe.P0w(1f, isConstAtten: true));
  }

  [Fact]
  public void SpotRow_KeepsSpotAndCones()
  {
    var p = LevelLightUe.FromRow(
      "spot", 1, 2, 3, 8f, "[255,165,207,226]", 10f,
      attenCurveParam: 1f, spotAngle: 110f, spotInnerAngleFactor: 0.04f);
    Assert.NotNull(p);
    Assert.Equal("spot", p!.LightType);
    Assert.Equal(8f, p.Intensity, 1e-5f);
    Assert.Equal(110f, p.OuterConeDegrees, 1e-4f);
    Assert.Equal(4.4f, p.InnerConeDegrees, 1e-4f);
    Assert.Equal(300f, p.P0w, 1e-3f);
  }

  [Fact]
  public void AttenuationRadius_FallbackTenMetres()
  {
    Assert.Equal(1000f, LevelLightUe.AttenuationRadiusCm(0f));
    Assert.Equal(1500f, LevelLightUe.AttenuationRadiusCm(15f));
  }
}
