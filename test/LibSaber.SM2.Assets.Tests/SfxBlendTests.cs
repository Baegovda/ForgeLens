using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-44: sfx blendMode selector (FUN_1425fd580 / FUN_142451840, PSO table psos_sfx).</summary>
public class SfxBlendTests
{
  [Theory]
  [InlineData("blend", SfxBlendMode.Blend)]
  [InlineData("ADD", SfxBlendMode.Add)]
  [InlineData("premul_a", SfxBlendMode.PremulA)]
  [InlineData("zzz", SfxBlendMode.Unspecified)]
  public void Parse_MatchesExeEnumOrder(string v, SfxBlendMode m)
  {
    Assert.Equal(m, SfxBlend.Parse(v));
    if (m != SfxBlendMode.Unspecified) Assert.Equal(new[] { "blend", "add", "premul_a" }[(int)m], v.ToLowerInvariant());
  }

  [Fact]
  public void FromTd_ReadsTopLevelOnly()
  {
    const string td = "shaders = { sfx = { layers = { l0 = { blendMode = \"add\" } } blendMode = \"premul_a\" isUseForPortals = false } }";
    Assert.Equal(SfxBlendMode.PremulA, SfxBlend.FromTd(td));
    Assert.Equal(SfxBlendMode.Unspecified, SfxBlend.FromTd("shaders = { sfx = { layers = { l0 = { blendMode = \"blend\" } } } }"));
    Assert.Equal(SfxBlendMode.Unspecified, SfxBlend.FromTd("preset = \"sfx\""));
  }

  [Fact]
  public void States_MatchPsoTable()
  {
    var keep = SfxBlend.Enabled;
    try
    {
      SfxBlend.Enabled = true;
      Assert.Equal(new VfxBlendState(true, "One", "One", "Zero", "One"), SfxBlend.State(SfxBlendMode.Add));
      Assert.Equal(new VfxBlendState(true, "SrcAlpha", "InvSrcAlpha", "Zero", "One"), SfxBlend.State(SfxBlendMode.Blend));
      Assert.Equal(new VfxBlendState(true, "One", "InvSrcAlpha", "One", "Zero"), SfxBlend.State(SfxBlendMode.PremulA));
      Assert.Equal(SfxBlend.State(SfxBlendMode.Add), SfxBlend.State(SfxBlendMode.Unspecified)); // HEURISTIC default
      Assert.Equal("SFX_ALPHA_BLEND", SfxBlend.Define(SfxBlendMode.Blend));
      Assert.Null(SfxBlend.Define(SfxBlendMode.PremulA));
      Assert.Equal("Translucent", SfxBlend.UeBlend(SfxBlendMode.PremulA));
      Assert.Equal("Additive", SfxBlend.UeBlend(SfxBlendMode.Unspecified));
      Assert.Equal(0.5f * 0.5f + 0.25f, SfxBlend.Composite(SfxBlendMode.Blend, 0.5f, 0.5f, 0.5f), 1e-4f);
      Assert.Equal(0.25f + 0.5f, SfxBlend.Composite(SfxBlendMode.Add, 0.5f, 0.5f, 0.5f), 1e-4f);
      var v = new float[39];
      var b = VfxFrameExport.TriangleBatch("m", "fx/m", true, "t", 0f, v, [0, 1, 2], "n", SfxBlendMode.Blend);
      Assert.Equal("Translucent", (string?)b["ueBlend"]);
      Assert.Equal("SFX_ALPHA_BLEND", (string?)b["sfxDefine"]);
      SfxBlend.Enabled = false;
      Assert.Equal("Additive", SfxBlend.UeBlend(SfxBlendMode.Blend));
    }
    finally { SfxBlend.Enabled = keep; }
  }
}
