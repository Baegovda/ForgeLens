using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;

namespace LibSaber.SM2.Assets.Tests;

public class UseSpecMapPolicyTests
{
  [Theory]
  [InlineData(true, false, true)]
  [InlineData(true, true, false)]
  [InlineData(false, false, false)]
  [InlineData(false, true, false)]
  public void Decide_MatchesHeuristic(bool hasSpec, bool chameleon, bool expected)
    => Assert.Equal(expected, UseSpecMapPolicy.Decide(hasSpec, chameleon));

  [Fact]
  public void MeshPart_UsesSpecularNameAndChameleon()
  {
    var withSpec = new MeshPart { Specular = "armor_spec" };
    Assert.True(UseSpecMapPolicy.Decide(withSpec));
    withSpec.Chameleon = new ChameleonSpec();
    Assert.False(UseSpecMapPolicy.Decide(withSpec));
    Assert.False(UseSpecMapPolicy.Decide(new MeshPart()));
  }

  [Fact]
  public void EvidenceMentionsHeuristicAndOpenWriter()
  {
    Assert.Contains("HEURISTIC", UseSpecMapPolicy.Evidence);
    Assert.Contains("OBJ_MTLBLEND_LAYER_CONST[15].x", UseSpecMapPolicy.Evidence);
  }
}

public class RestFitPolicyTests
{
  [Fact]
  public void DefaultsMatchR76R91()
  {
    Assert.Equal(0.9f, RestFitPolicy.Floor);
    Assert.Equal(0.5f, RestFitPolicy.DeepFrac);
    Assert.Equal(1.0f, RestFitPolicy.ShallowFracDefault);
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
    // lighting__sun_light [255,165,207,226] → R,G,B = 165,207,226
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
    Assert.Equal(300f, p.Y); // z*100
    Assert.Equal(200f, p.Z); // y*100
  }

  [Fact]
  public void UeCandela_UsesFitK()
  {
    Assert.Equal(80f, LevelLightUe.UeCandela(8f));
    Assert.Equal(40f, LevelLightUe.UeCandela(8f, 5f));
    Assert.Contains("HEURISTIC", LevelLightUe.IntensityEvidence);
    Assert.Contains("PROVEN", LevelLightUe.ColorOrderEvidence);
  }

  [Fact]
  public void AttenuationRadius_FallbackTenMetres()
  {
    Assert.Equal(1000f, LevelLightUe.AttenuationRadiusCm(0f));
    Assert.Equal(1500f, LevelLightUe.AttenuationRadiusCm(15f));
  }
}
