namespace LibSaber.SM2Extractor.Tests;

public class VariantOptionTests
{
  private static readonly string[] Export = { "export", "game", "--asset", "tpl/cc_sm_constructor_tank.tpl", "--format", "fbx-unreal", "-o", "out" };

  [Fact]
  public void Defaults_keep_all_texture_formats_and_default_variant()
  {
    Assert.True(ExtractorOptions.TryParse(Export, out var options, out var error), error);
    Assert.Null(options.Variant);
    Assert.True(options.WriteDds && options.WritePng && options.WriteTga);
  }

  [Fact]
  public void Variant_and_tex_formats_parse()
  {
    Assert.True(ExtractorOptions.TryParse(Export.Concat(new[] { "--variant", "preset=2,HELMET=helmet_tank_common_01", "--tex-formats", "png" }).ToArray(), out var options, out var error), error);
    Assert.Equal("preset=2,HELMET=helmet_tank_common_01", options.Variant);
    Assert.True(options.WritePng);
    Assert.False(options.WriteDds);
    Assert.False(options.WriteTga);

    Assert.True(ExtractorOptions.TryParse(Export.Concat(new[] { "--variant", "all", "--tex-formats", "dds,.tga" }).ToArray(), out options, out error), error);
    Assert.Equal("all", options.Variant);
    Assert.True(options.WriteDds && options.WriteTga && !options.WritePng);
  }

  [Theory]
  [InlineData("--tex-formats", "jpg", "jpg")]
  [InlineData("--tex-formats", ",", "at least one")]
  [InlineData("--variant", "preset=x", "preset")]
  [InlineData("--variant", "bogus", "bogus")]
  public void Bad_values_are_rejected(string option, string value, string message)
  {
    Assert.False(ExtractorOptions.TryParse(Export.Concat(new[] { option, value }).ToArray(), out _, out var error));
    Assert.Contains(message, error);
  }

  [Fact]
  public void Export_only()
  {
    Assert.False(ExtractorOptions.TryParse(new[] { "list", "game", "--tex-formats", "png" }, out _, out var error));
    Assert.Contains("--tex-formats", error);
    Assert.False(ExtractorOptions.TryParse(new[] { "list", "game", "--variant", "all" }, out _, out error));
    Assert.Contains("--variant", error);
  }
}
