using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Export;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>oneclick: Direct-to-Unreal path rules, manifest, locator and MCP parsing.</summary>
public class UnrealDirectTests
{
  [Theory]
  [InlineData("/Game/SM2_Work/Tests/oneclick/wpn_bolt_rifle_01", true)]
  [InlineData("/Game/SM2_Work/Props/wpn_bolt_rifle_01", true)]
  [InlineData("/Game/SM2_Work/Original/cc_titus", false)]
  [InlineData("/game/sm2_work/original", false)]
  [InlineData("/Game/Work/foo", false)]
  [InlineData("/Game/Work", false)]
  [InlineData("/Game/SM2_Work/Props", false)]
  [InlineData("/Game/SM2_Work/Tests/", false)]
  [InlineData("/Game", false)]
  [InlineData("/Game/Foo", false)]
  [InlineData("/Engine/SM2/x", false)]
  [InlineData("/Game/SM2_Work/Props/has space", false)]
  [InlineData("/Game/SM2_Work/Props/../Original", false)]
  [InlineData(@"\Game\SM2_Work\Props\x\", true)]
  [InlineData("/Game/Workshop/a", true)]
  [InlineData("/Game/ForgeLens/SM2/Characters/cc_titus", true)]
  [InlineData("/Game/ForgeLens/POE/Props/x", true)]
  [InlineData("/Game/ForgeLens", false)]
  [InlineData("/Game/ForgeLens/SM2", false)]
  [InlineData("/Game/ForgeLens/SM2/Characters", false)]
  public void Validate(string path, bool ok) => Assert.Equal(ok, UnrealContentPath.Validate(path) is null);

  [Fact]
  public void BackslashesNormalize() => Assert.Equal("/Game/SM2_Work/Props/x", UnrealContentPath.Normalize(@"/Game\SM2_Work//Props\x\"));

  [Theory]
  [InlineData("cc_titus_assault", false, "/Game/ForgeLens/SM2/Characters/cc_titus_assault")]
  [InlineData("wpn_bolt_rifle_01", false, "/Game/ForgeLens/SM2/Props/wpn_bolt_rifle_01")]
  [InlineData("cc_titus_assault", true, "/Game/ForgeLens/SM2/Animations/cc_titus_assault")]
  [InlineData("a b.c", false, "/Game/ForgeLens/SM2/Props/a_b_c")]
  public void Default(string asset, bool animOnly, string want) => Assert.Equal(want, UnrealContentPath.Default(asset, animOnly));

  [Fact]
  public void ForModel()
  {
    Assert.Equal("/Game/SM2_Work/Tests/oneclick/x", UnrealContentPath.ForModel("/Game/SM2_Work/Tests/oneclick/x", false, "a", 1, false));
    Assert.Equal("/Game/SM2_Work/Tests/oneclick/a", UnrealContentPath.ForModel("/Game/SM2_Work/Tests/oneclick", false, "a", 2, false));
    Assert.Equal("/Game/ForgeLens/SM2/Characters/cc_b", UnrealContentPath.ForModel("/Game/ForgeLens/SM2/Props/a", true, "cc_b", 2, false));
  }

  [Fact]
  public void ManifestCarriesEveryFile()
  {
    var m = new ExportedModelFiles { Name = "cc_x", Source = "tpl/cc_x.tpl", Skeletal = true, Fbx = @"C:\e\cc_x.fbx", Materials = @"C:\e\cc_x.materials.json", Physics = @"C:\e\cc_x.physics.json", ColourScheme = @"C:\e\s.json" };
    m.Clips.Add(@"C:\e\cc_x__idle.fbx");
    m.Manny.Add(@"C:\e\cc_x__idle_Manny.fbx");
    var paths = new UnrealDirectManifest.Paths("C:/e/_forge_direct", "C:/e/_forge_direct/m.json", "C:/e/_forge_direct/r.py", "C:/e/_forge_direct/l.log", "C:/e/_forge_direct/res.json");
    var j = UnrealDirectManifest.Build(new[] { m }, new UnrealDirectChoice { Project = @"K:\P\P.uproject", ContentPath = "/Game/SM2_Work/Tests/oneclick/cc_x" }, false, paths);
    var o = (JsonObject)j["models"]![0]!;
    Assert.Equal("/Game/SM2_Work/Tests/oneclick/cc_x", o["folder"]!.ToString());
    Assert.Equal("C:/e/cc_x.fbx", o["fbx"]!.ToString());
    Assert.True(o["skeletal"]!.GetValue<bool>());
    Assert.Equal("C:/e/cc_x__idle.fbx", o["clips"]![0]!.ToString());
    Assert.Equal("C:/e/cc_x__idle_Manny.fbx", o["manny"]![0]!.ToString());
    Assert.Equal("C:/e/cc_x.physics.json", o["physics"]!.ToString());
    Assert.Equal("C:/e/s.json", o["colourScheme"]!.ToString());
    Assert.Equal("C:/e/_forge_direct/l.log", j["log"]!.ToString());
  }

  [Fact]
  public void WriteDropsScriptsAndRunner()
  {
    var dir = Directory.CreateTempSubdirectory("oneclick").FullName;
    try
    {
      var m = new ExportedModelFiles { Name = "p", Source = "tpl/p.tpl", Fbx = Path.Combine(dir, "p.fbx") };
      var paths = UnrealDirectManifest.Write(dir, new[] { m }, new UnrealDirectChoice { Project = "x.uproject", ContentPath = "/Game/SM2_Work/Tests/oneclick/p" }, false);
      foreach (var s in UnrealDirectManifest.Scripts)
        Assert.True(File.Exists(Path.Combine(paths.Folder, s)), s);
      Assert.Contains("def dest_error", File.ReadAllText(Path.Combine(paths.Folder, "ue_forge_direct.py")));
      Assert.Contains("# --- sm2_anim_import", File.ReadAllText(Path.Combine(paths.Folder, "ue_import_anim.py")));
      var runner = File.ReadAllText(paths.Runner);
      Assert.Contains("runpy.run_path(", runner);
      Assert.Contains(paths.Manifest.Replace('\\', '/'), runner);
      Assert.True(JsonNode.Parse(File.ReadAllText(paths.Manifest)) is JsonObject);
    }
    finally
    {
      Directory.Delete(dir, true);
    }
  }

  [Fact]
  public void RunnerEscapesQuotes()
  {
    var r = UnrealDirectManifest.RunnerSource(@"C:\a ""b""\s.py", @"C:\m.json");
    Assert.Contains("\"C:/a \\u0022b\\u0022/s.py\"", r);
  }

  [Fact]
  public void LauncherList()
  {
    const string json = """{"InstallationList":[{"InstallLocation":"D:\\UE\\UE_5.6","AppName":"UE_5.6"},{"InstallLocation":"E:\\Epic\\UE_5.7","AppName":"UE_5.7"}]}""";
    Assert.Equal(@"E:\Epic\UE_5.7", UnrealEditorLocator.FromLauncherList(json, "5.7"));
    Assert.Null(UnrealEditorLocator.FromLauncherList(json, "5.4"));
    Assert.Null(UnrealEditorLocator.FromLauncherList("not json", "5.7"));
  }

  [Fact]
  public void HeadlessArgs()
  {
    var a = UnrealEditorLocator.HeadlessArguments(@"K:\Unreal Projects\P\P.uproject", @"C:\e x\_forge_direct\run.py");
    Assert.StartsWith("\"K:\\Unreal Projects\\P\\P.uproject\" -run=pythonscript -script=\"C:/e x/_forge_direct/run.py\"", a);
    Assert.Contains("-AutoDeclinePackageRecovery", a);
    Assert.Contains("-unattended", a);
  }

  [Fact]
  public void McpJsonAndSse()
  {
    Assert.NotNull(McpResponse.Find("""{"jsonrpc":"2.0","id":2,"result":{"x":1}}""", 2));
    Assert.Null(McpResponse.Find("""{"jsonrpc":"2.0","id":3,"result":{}}""", 2));
    const string sse = "event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\"}\n\nevent: message\ndata: {\"jsonrpc\":\"2.0\",\"id\":5,\"result\":{\"structuredContent\":{\"output\":\"ok\"}}}\n\n";
    var r = McpResponse.Find(sse, 5, "text/event-stream");
    Assert.Equal("ok", r!["result"]!["structuredContent"]!["output"]!.ToString());
  }

  [Fact]
  public void SummaryReadsResult()
  {
    var j = (JsonObject)JsonNode.Parse("""{"ok":true,"models":[{"name":"p","folder":"/Game/SM2_Work/Tests/oneclick/p","assets":7,"clips":["a"],"materialInstances":3,"backup":"/Game/SM2_Work/Tests/oneclick/p_bak1","errors":[]}]}""")!;
    var s = UnrealDirectImporter.Summarize(j, null);
    Assert.StartsWith("Imported.", s);
    Assert.Contains("p_bak1", s);
    Assert.StartsWith("The import did not finish", UnrealDirectImporter.Summarize(null, "Error: boom"));
    var crash = UnrealDirectImporter.Summarize(null, "LogWindows: Error: appError called: Assertion failed: CurrentApplication.IsValid()\nLogWindows: Error: [Callstack] 0x1 Core.dll\nLogWindows: Error: ");
    Assert.Contains("Unreal crashed", crash);
    Assert.DoesNotContain("Callstack", crash);
  }

  [Fact]
  public void ForcedBridgeFromEnvironment()
  {
    var old = Environment.GetEnvironmentVariable("SM2_DIRECT_BRIDGE");
    try
    {
      Environment.SetEnvironmentVariable("SM2_DIRECT_BRIDGE", " Headless ");
      Assert.Equal("headless", UnrealDirectImporter.ForcedBridge());
      Environment.SetEnvironmentVariable("SM2_DIRECT_BRIDGE", "auto");
      Assert.Null(UnrealDirectImporter.ForcedBridge());
      Environment.SetEnvironmentVariable("SM2_DIRECT_BRIDGE", null);
      Assert.Null(UnrealDirectImporter.ForcedBridge());
    }
    finally
    {
      Environment.SetEnvironmentVariable("SM2_DIRECT_BRIDGE", old);
    }
  }
}
