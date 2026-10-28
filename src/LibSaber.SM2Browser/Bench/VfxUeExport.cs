using System.Globalization;
using System.Numerics;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SM2Browser.Effects;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   mtl1-34: headless (no GL) frozen-frame export for Unreal, see <see cref="VfxFrameExport"/>.
///   <c>--vfxue &lt;sfx root&gt; &lt;part_aliases.ps&gt; &lt;texture root&gt; &lt;out&gt; name@seconds[@distance] ...</c> writes
///   <c>&lt;out&gt;/&lt;name&gt;_&lt;ms&gt;.vfxframe.json</c> (camera = the vfxbench Fit camera) and
///   <c>&lt;out&gt;/textures/&lt;texture&gt;.png</c>; <c>--vfxue-rules &lt;out&gt;</c> writes the rule check scene
///   (<see cref="VfxRuleScene"/>) and its synthetic textures.
/// </summary>
internal static class VfxUeExport
{

  public static int Rules(string[] args)
  {
    var output = args[1];
    var textures = Path.Combine(output, "textures");
    Directory.CreateDirectory(textures);
    foreach (var (name, (w, h, bgra)) in VfxRuleScene.Textures())
      ImageWriters.WritePng(Path.Combine(textures, name + ".png"), bgra, w, h);
    File.WriteAllText(Path.Combine(output, "sm2_vfx_rules.vfxframe.json"), VfxRuleScene.Build().ToJsonString());
    Console.WriteLine($"vfxue-rules: wrote {output}");
    return 0;
  }

  public static int Run(string[] args)
  {
    var library = new EffectLibrary(new DirectoryEffectSource(args[1]));
    var aliases = PartAliases.Load(args[2]);
    var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var f in Directory.EnumerateFiles(args[3], "*.pct.resource", SearchOption.AllDirectories))
      files.TryAdd(Path.GetFileName(f)[..^".pct.resource".Length], f);
    var output = args[4];
    var texDir = Path.Combine(output, "textures");
    Directory.CreateDirectory(texDir);
    var exposure = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_EXPOSURE"), CultureInfo.InvariantCulture, out var ex) ? ex : 1f / 250f;
    const int size = 512;
    var exit = 0;
    foreach (var spec in args.Skip(5))
    {
      var parts = spec.Split('@');
      var name = parts[0];
      var tree = library.Resolve(name);
      if (tree is null) { Console.WriteLine($"{name}: not found"); exit = 1; continue; }
      var def = EffectDefinition.Build(name, tree);
      var seconds = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : Math.Min(def.Duration, 3f) * 0.5f;
      var instance = EffectInstance.Standalone(def, 1);
      // Same framing as the vfxbench sheet (Fit over the effect's capture window, min(duration, 3 s)).
      var camera = EffectCamera.Fit(instance, Math.Min(def.Duration, 3f));
      if (parts.Length > 2 && float.TryParse(parts[2], CultureInfo.InvariantCulture, out var distance)) camera = camera with { Distance = distance };
      var (view, _, eye) = camera.Matrices(size, size);
      instance.Seek(seconds);
      var batches = EffectSprites.Build(instance, aliases, view);
      var written = new List<System.Text.Json.Nodes.JsonObject>();
      var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var b in batches)
      {
        var j = VfxFrameExport.Batch(b);
        written.Add(j);
        foreach (var t in new[] { (string?)j["texture"], (string?)j["alphaMask"] })
        {
          if (string.IsNullOrEmpty(t)) continue;
          var png = Path.Combine(texDir, t.ToLowerInvariant() + ".png");
          if (File.Exists(png)) continue;
          try
          {
            if (!files.TryGetValue(t, out var path)) { missing.Add(t); continue; }
            var doc = TextureLoader.LoadLoose(path);
            var bgra = doc.Decode(0, 0, out var w, out var h, out _);
            ImageWriters.WritePng(png, bgra, w, h);
          }
          catch (Exception e) { missing.Add($"{t} ({e.Message})"); }
        }
      }

      var frame = VfxFrameExport.Frame(name, seconds, VfxFrameExport.Camera(eye, camera.Center, camera.Fov), exposure, written,
        "browser bench exposure (SM2_VFX_EXPOSURE, default 1/250; HEURISTIC). Game exposure = key/avgLum (CaptureTonemap.Exposure).");
      frame["pixels"] = size;
      frame["missingTextures"] = new System.Text.Json.Nodes.JsonArray(missing.Select(m => (System.Text.Json.Nodes.JsonNode)m).ToArray());
      var file = Path.Combine(output, $"{name}_{(int)MathF.Round(seconds * 1000)}ms.vfxframe.json");
      File.WriteAllText(file, frame.ToJsonString());
      Console.WriteLine($"{name}@{seconds:0.000}: batches {batches.Count} quads {batches.Sum(b => b.QuadCount)} kinds [{string.Join(", ", batches.GroupBy(b => (string)VfxFrameExport.Batch(b)["ueBlend"]!).Select(g => $"{g.Key} {g.Count()}"))}] missing [{string.Join(", ", missing)}] -> {Path.GetFileName(file)}");
    }

    return exit;
  }
}
