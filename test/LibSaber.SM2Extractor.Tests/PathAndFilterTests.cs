namespace LibSaber.SM2Extractor.Tests;

public class PathAndFilterTests
{

  [Theory]
  [InlineData("characters/marine/body.tpl", "*.tpl", true)]
  [InlineData("characters/marine/body.tpl", ".tpl", true)]
  [InlineData("characters/marine/body.TPL", "*.tpl", true)]
  [InlineData("characters/marine/body.pct.resource", "*.pct", false)]
  [InlineData("characters/marine/body.pct.resource", ".pct.resource", true)]
  [InlineData("characters/marine/body.tpl", "characters/**/*.tpl", true)]
  [InlineData("characters/body.tpl", "characters/**/*.tpl", true)]
  [InlineData("other/body.tpl", "characters/**/*.tpl", false)]
  [InlineData("characters/marine/body.lg", "body.lg", true)]
  [InlineData("characters/marine/body.tpl", "body.tp?", true)]
  [InlineData(@"characters\marine\body.tpl", "*.tpl", true)]
  public void Filter_matches_globs_and_extensions(string path, string filter, bool expected)
  {
    Assert.Equal(expected, ArchivePathFilter.Matches(path, new[] { filter }));
  }

  [Fact]
  public void Filter_with_no_patterns_matches_everything()
  {
    Assert.True(ArchivePathFilter.Matches("a/b.pct", Array.Empty<string>()));
  }

  [Fact]
  public void Filter_ors_multiple_patterns()
  {
    var filters = new[] { "*.pct", "*.tpl" };
    Assert.True(ArchivePathFilter.Matches("a/b.tpl", filters));
    Assert.False(ArchivePathFilter.Matches("a/b.lg", filters));
  }

  [Fact]
  public void File_type_keeps_compound_resource_suffix()
  {
    Assert.Equal(".pct.resource", ArchiveOutputPath.FileType("textures/marine.pct.resource"));
    Assert.Equal(".tpl", ArchiveOutputPath.FileType("characters/body.tpl"));
    Assert.Equal("(none)", ArchiveOutputPath.FileType("readme"));
    Assert.Equal("directory", ArchiveOutputPath.FileType("characters/"));
  }

  [Fact]
  public void Output_mapping_preserves_directories_and_rejects_traversal()
  {
    using var temp = new TempDirectory();

    Assert.True(ArchiveOutputPath.TryMap(temp.Path, @"characters\marine\body.tpl", out var output, out _));
    Assert.Equal(
      Path.GetFullPath(Path.Combine(temp.Path, "characters", "marine", "body.tpl")),
      output);

    Assert.False(ArchiveOutputPath.TryMap(temp.Path, "../outside.txt", out _, out var error));
    Assert.Contains("escapes", error, StringComparison.OrdinalIgnoreCase);

    Assert.False(ArchiveOutputPath.TryMap(temp.Path, "characters/../../outside.txt", out _, out _));
    Assert.False(ArchiveOutputPath.TryMap(temp.Path, "bad:name.tpl", out _, out var invalid));
    Assert.Contains("invalid", invalid, StringComparison.OrdinalIgnoreCase);
  }

}
