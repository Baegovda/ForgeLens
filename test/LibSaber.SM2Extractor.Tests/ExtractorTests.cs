using System.Text;
using LibSaber.SpaceMarine2.Files;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SM2Extractor.Tests;

public class ExtractorTests
{

  [Fact]
  public void Stored_and_deflate_round_trip_including_unaligned_and_empty()
  {
    using var temp = new TempDirectory();
    var stored = Encoding.ASCII.GetBytes("stored-payload");
    var deflated = Encoding.ASCII.GetBytes("deflated-payload");
    var archive = SyntheticPak.Create(
      temp.Path,
      "sample.pck",
      new[]
      {
        new SyntheticPak.Entry(@"characters\marine\body.tpl", stored),
        new SyntheticPak.Entry("textures/body.pct", deflated, fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE),
        new SyntheticPak.Entry("empty.bin", Array.Empty<byte>()),
      },
      payloadPrefix: 1);

    using var device = new SM2PckFileDevice(archive);
    Assert.True(device.Initialize());

    var files = device.EnumerateFiles().OfType<SM2FileSystemNode>().ToArray();
    Assert.Equal(3, files.Length);
    Assert.Equal("characters/marine/body.tpl", files[0].RelativePath);
    Assert.Equal(stored, ReadAll(files[0]));
    Assert.Equal(deflated, ReadAll(files[1]));
    Assert.Empty(ReadAll(files[2]));
  }

  [Fact]
  public void Version7_cache_round_trips()
  {
    using var temp = new TempDirectory();
    var payload = new byte[] { 1, 2, 3, 4, 5 };
    var archive = SyntheticPak.Create(
      temp.Path,
      "update.pak",
      new[] { new SyntheticPak.Entry("maps/level.lg", payload) },
      SyntheticPak.Format.Version7);

    using var device = new SM2PckFileDevice(archive);
    Assert.True(device.Initialize());
    var file = Assert.Single(device.EnumerateFiles().OfType<SM2FileSystemNode>());
    Assert.Equal(payload, ReadAll(file));
  }

  [Fact]
  public void Cache_trailer_and_duplicate_names_do_not_fail_the_archive()
  {
    using var temp = new TempDirectory();
    var archive = SyntheticPak.Create(
      temp.Path,
      "dup.pak",
      new[]
      {
        new SyntheticPak.Entry("same.txt", Encoding.ASCII.GetBytes("first")),
        new SyntheticPak.Entry("same.txt", Encoding.ASCII.GetBytes("second")),
      },
      cacheTrailer: new byte[40]);

    using var device = new SM2PckFileDevice(archive);
    Assert.True(device.Initialize());
    var file = Assert.Single(device.EnumerateFiles().OfType<SM2FileSystemNode>());
    Assert.Equal("first", Encoding.ASCII.GetString(ReadAll(file)));
  }

  [Fact]
  public void Empty_archive_opens()
  {
    using var temp = new TempDirectory();
    var archive = SyntheticPak.Create(temp.Path, "empty.pak", Array.Empty<SyntheticPak.Entry>());

    using var device = new SM2PckFileDevice(archive);
    Assert.True(device.Initialize());
    Assert.Empty(device.EnumerateFiles().OfType<SM2FileSystemNode>());
  }

  [Fact]
  public void List_prints_sizes_types_and_paths()
  {
    using var temp = new TempDirectory();
    var payload = Encoding.ASCII.GetBytes("picture");
    var archive = SyntheticPak.Create(
      temp.Path,
      "Sample.PAK",
      new[]
      {
        new SyntheticPak.Entry("textures/marine.pct.resource", payload),
        new SyntheticPak.Entry("characters/", Array.Empty<byte>()),
      });

    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var code = ExtractorHost.Run(
      new[] { "list", archive, "--filter", ".pct.resource" },
      stdout,
      stderr);

    Assert.Equal(0, code);
    Assert.Equal("", stderr.ToString());
    var text = stdout.ToString();
    Assert.Contains("Size\tCompressed\tMethod\tType\tArchive\tPath", text);
    Assert.Contains($"{payload.Length}\t{payload.Length}\tStore\t.pct.resource\tSample.PAK\ttextures/marine.pct.resource", text);
    Assert.DoesNotContain("characters/", text);
    Assert.Contains("archivesOpened=1", text);
    Assert.Contains("filesListed=1", text);
  }

  [Fact]
  public void Extract_preserves_directories_and_applies_filters()
  {
    using var temp = new TempDirectory();
    var source = Path.Combine(temp.Path, "src");
    var output = Path.Combine(temp.Path, "out");
    var tpl = Encoding.ASCII.GetBytes("template");
    var pct = Encoding.ASCII.GetBytes("texture-bytes");
    SyntheticPak.Create(
      source,
      "game.pak",
      new[]
      {
        new SyntheticPak.Entry(@"characters\marine\body.tpl", tpl),
        new SyntheticPak.Entry("textures/body.pct", pct, fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE),
        new SyntheticPak.Entry("skip.lg", Encoding.ASCII.GetBytes("level")),
      });

    var stdout = new StringWriter();
    var code = ExtractorHost.Run(
      new[] { "extract", source, "--output", output, "-f", "*.tpl", "-f", ".pct" },
      stdout,
      new StringWriter());

    Assert.Equal(0, code);
    Assert.Equal(tpl, File.ReadAllBytes(Path.Combine(output, "characters", "marine", "body.tpl")));
    Assert.Equal(pct, File.ReadAllBytes(Path.Combine(output, "textures", "body.pct")));
    Assert.False(File.Exists(Path.Combine(output, "skip.lg")));
    Assert.Contains("bytesWritten=" + (tpl.Length + pct.Length), stdout.ToString());
  }

  [Fact]
  public void Extract_skips_bad_entries_and_archives_then_reports_a_summary()
  {
    using var temp = new TempDirectory();
    var source = Path.Combine(temp.Path, "client_pc");
    var output = Path.Combine(temp.Path, "out");
    var good = Encoding.ASCII.GetBytes("ok");
    SyntheticPak.Create(
      source,
      "good.pak",
      new[]
      {
        new SyntheticPak.Entry("keep.txt", good),
        new SyntheticPak.Entry(
          "broken.bin",
          Encoding.ASCII.GetBytes("nope"),
          fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE,
          PayloadOverride: new byte[] { 1, 2, 3, 4, 5 }),
        new SyntheticPak.Entry("../outside.txt", Encoding.ASCII.GetBytes("secret")),
        new SyntheticPak.Entry("wide.bin", Encoding.ASCII.GetBytes("x"), fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE64),
      });
    File.WriteAllBytes(Path.Combine(source, "missing-cache.pck"), new byte[] { 1, 2, 3 });

    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var code = ExtractorHost.Run(
      new[] { "extract", source, "-o", output },
      stdout,
      stderr);

    Assert.Equal(2, code);
    Assert.Equal(good, File.ReadAllBytes(Path.Combine(output, "keep.txt")));
    Assert.False(File.Exists(Path.Combine(temp.Path, "outside.txt")));
    Assert.False(File.Exists(Path.Combine(output, "outside.txt")));
    var errors = stderr.ToString();
    Assert.Contains("missing-cache.pck", errors);
    Assert.Contains("broken.bin", errors);
    Assert.Contains("DEFLATE64", errors);
    Assert.Contains("escapes", errors, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("filesExtracted=1", stdout.ToString());
    Assert.Contains("filesSkipped=3", stdout.ToString());
    Assert.Contains("archivesSkipped=1", stdout.ToString());
  }

  [Fact]
  public void Extract_can_prefix_archives_and_skip_existing_files()
  {
    using var temp = new TempDirectory();
    var source = Path.Combine(temp.Path, "client_pc", "nested");
    var output = Path.Combine(temp.Path, "out");
    SyntheticPak.Create(source, "one.pak", new[] { new SyntheticPak.Entry("a.txt", new byte[] { 1 }) });
    Directory.CreateDirectory(output);
    var prefixed = Path.Combine(output, "nested_one", "a.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(prefixed)!);
    File.WriteAllBytes(prefixed, new byte[] { 9 });

    var stdout = new StringWriter();
    var code = ExtractorHost.Run(
      new[]
      {
        "extract",
        Path.Combine(temp.Path, "client_pc"),
        "--output",
        output,
        "--prefix-archive",
        "--no-overwrite",
        "--quiet"
      },
      stdout,
      new StringWriter());

    Assert.Equal(0, code);
    Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(prefixed));
    var text = stdout.ToString().Trim();
    Assert.StartsWith("Summary:", text);
    Assert.Contains("filesExisting=1", text);
    Assert.DoesNotContain("Wrote", text);
  }

  [Fact]
  public void Later_archive_overwrites_the_same_internal_path()
  {
    using var temp = new TempDirectory();
    var source = Path.Combine(temp.Path, "src");
    var output = Path.Combine(temp.Path, "out");
    SyntheticPak.Create(source, "a.pak", new[] { new SyntheticPak.Entry("same.txt", new byte[] { 1 }) });
    SyntheticPak.Create(source, "b.pak", new[] { new SyntheticPak.Entry("same.txt", new byte[] { 2 }) });

    var code = ExtractorHost.Run(
      new[] { "extract", source, "-o", output, "-q" },
      new StringWriter(),
      new StringWriter());

    Assert.Equal(0, code);
    Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(Path.Combine(output, "same.txt")));
  }

  [Fact]
  public void No_recursive_ignores_nested_archives()
  {
    using var temp = new TempDirectory();
    var nested = Path.Combine(temp.Path, "nested");
    SyntheticPak.Create(nested, "inner.pak", new[] { new SyntheticPak.Entry("a.txt", new byte[] { 1 }) });

    var code = ExtractorHost.Run(
      new[] { "list", temp.Path, "--no-recursive" },
      new StringWriter(),
      new StringWriter());

    Assert.Equal(1, code);
  }

  public static IEnumerable<object[]> UsageCases()
  {
    yield return new object[] { Array.Empty<string>(), 0 };
    yield return new object[] { new[] { "--help" }, 0 };
    yield return new object[] { new[] { "explode", "x" }, 1 };
    yield return new object[] { new[] { "extract", "x" }, 1 };
    yield return new object[] { new[] { "list" }, 1 };
  }

  [Theory]
  [MemberData(nameof(UsageCases))]
  public void Usage_errors_return_the_expected_code(string[] args, int expected)
  {
    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var code = ExtractorHost.Run(args, stdout, stderr);
    Assert.Equal(expected, code);
    var combined = stdout.ToString() + stderr.ToString();
    Assert.Contains("list", combined);
    Assert.Contains("extract", combined);
  }

  [Fact]
  public void Missing_path_and_plain_files_fail_without_throwing()
  {
    using var temp = new TempDirectory();
    var textFile = Path.Combine(temp.Path, "notes.txt");
    File.WriteAllText(textFile, "hello");

    Assert.Equal(1, ExtractorHost.Run(new[] { "list", textFile }, new StringWriter(), new StringWriter()));
    Assert.Equal(1, ExtractorHost.Run(
      new[] { "list", Path.Combine(temp.Path, "missing") },
      new StringWriter(),
      new StringWriter()));
  }

  [Fact]
  public void Export_accepts_anim_and_rejects_it_on_list()
  {
    Assert.True(ExtractorOptions.TryParse(
      new[] { "export", "game", "--asset", "tpl/hero.tpl", "--format", "fbx-unreal", "--anim", "idle", "-o", "out" },
      out var options,
      out var error));
    Assert.Equal("", error);
    Assert.Equal("idle", options.Animation);
    Assert.Equal("fbx-unreal", options.Format);

    Assert.True(ExtractorOptions.TryParse(
      new[] { "export", "game", "--asset", "tpl/hero.tpl", "--format", "both", "--anim", "all", "-o", "out" },
      out options,
      out error));
    Assert.Equal("all", options.Animation);

    Assert.False(ExtractorOptions.TryParse(
      new[] { "list", "game", "--anim", "idle" },
      out _,
      out error));
    Assert.Contains("--anim", error);

    Assert.True(ExtractorOptions.TryParse(
      new[] { "export", "game", "--asset", "tpl/hero.tpl", "--format", "fbx-unreal", "--anim", "run", "--no-root-motion", "-o", "out" },
      out options,
      out error));
    Assert.Equal("", error);
    Assert.False(options.RootMotion);

    Assert.False(ExtractorOptions.TryParse(
      new[] { "list", "game", "--no-root-motion" },
      out _,
      out error));
    Assert.Contains("--no-root-motion", error);
  }

  [Fact]
  public void Export_requires_output_asset_and_format()
  {
    var stderr = new StringWriter();
    var code = ExtractorHost.Run(new[] { "export", "game" }, new StringWriter(), stderr);
    Assert.Equal(1, code);
    Assert.Contains("--output", stderr.ToString());
  }

  [Fact]
  public void Export_resolves_the_nested_model_path_the_browser_lists()
  {
    using var temp = new TempDirectory();
    var game = Path.Combine(temp.Path, "game");
    var output = Path.Combine(temp.Path, "out");
    SyntheticPak.Create(
      game,
      "characters.pak",
      new[] { new SyntheticPak.Entry("tpl/hero.tpl/hero.tpl", Encoding.ASCII.GetBytes("not-a-model")) });

    var stdout = new StringWriter();
    var stderr = new StringWriter();
    var code = ExtractorHost.Run(
      new[] { "export", game, "--asset", "tpl/hero.tpl", "--format", "gltf", "--lod", "0", "-o", output },
      stdout,
      stderr);

    Assert.Equal(1, code);
    var text = stdout.ToString();
    Assert.Contains("Exporting tpl/hero.tpl/hero.tpl as gltf lod 0", text);
    Assert.Contains("filesSkipped=1", text);
    Assert.DoesNotContain("No model named", stderr.ToString());
    Assert.Equal(1, ExtractorHost.Run(
      new[] { "export", game, "--asset", "missing.tpl", "--format", "both", "-o", output },
      new StringWriter(),
      stderr));
    Assert.Contains("No model named 'missing.tpl'", stderr.ToString());
  }

  [Fact]
  public void Program_main_prints_help()
  {
    var previousOut = Console.Out;
    var previousError = Console.Error;
    var stdout = new StringWriter();
    try
    {
      Console.SetOut(stdout);
      Console.SetError(new StringWriter());
      Assert.Equal(0, Program.Main(new[] { "--help" }));
    }
    finally
    {
      Console.SetOut(previousOut);
      Console.SetError(previousError);
    }

    Assert.Contains("Space Marine 2", stdout.ToString());
  }

  private static byte[] ReadAll(SM2FileSystemNode node)
  {
    using var stream = node.Open();
    using var memory = new MemoryStream();
    stream.CopyTo(memory);
    return memory.ToArray();
  }


  [Fact]
  public void Export_anim_root_mode()
  {
    Assert.True(ExtractorOptions.TryParse(new[] { "export", "game", "--asset", "tpl/hero.tpl", "--format", "fbx-unreal", "--anim", "run", "-o", "out" }, out var options, out _));
    Assert.False(options.InPlace); // default: root motion (unchanged)
    Assert.True(ExtractorOptions.TryParse(new[] { "export", "game", "--asset", "tpl/hero.tpl", "--format", "fbx-unreal", "--anim", "run", "--anim-root", "in-place", "-o", "out" }, out options, out _));
    Assert.True(options.InPlace);
    Assert.False(ExtractorOptions.TryParse(new[] { "export", "game", "--asset", "tpl/hero.tpl", "--format", "fbx-unreal", "--anim-root", "sideways", "-o", "out" }, out _, out var error));
    Assert.Contains("--anim-root", error);
    Assert.False(ExtractorOptions.TryParse(new[] { "list", "game", "--anim-root", "in-place" }, out _, out error));
  }
}
