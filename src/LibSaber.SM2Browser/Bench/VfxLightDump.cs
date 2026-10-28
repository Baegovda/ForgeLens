using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   mtl1-36: <c>--vfxlights &lt;sfx root&gt; &lt;out.json&gt;</c> lists every sfx_light (raw definition, local; nested
///   effects are not expanded): radius, intensity and colour (0..1 as authored) sampled at 9 points over its
///   animationTime. Used to match capture CB_COMMON_LIGHTNG lights to sfx lights (light unit calibration).
/// </summary>
internal static class VfxLightDump
{
  public static int Run(string[] args)
  {
    var library = new EffectLibrary(new DirectoryEffectSource(args[1]));
    var all = new JsonArray();
    foreach (var name in library.Names)
    {
      EffectDefinition def;
      try { if (library.Resolve(name) is not { } tree) continue; def = EffectDefinition.Build(name, tree); }
      catch { continue; }
      foreach (var l in def.Lights)
      {
        var s = new JsonArray();
        for (var i = 0; i <= 8; i++)
        {
          var n = i / 8f; var c = l.Color.Evaluate(n);
          s.Add(new JsonArray(n, l.Radius.Evaluate(n), l.Intensity.Evaluate(n), c.X, c.Y, c.Z));
        }
        all.Add(new JsonObject { ["effect"] = name, ["light"] = l.Name, ["path"] = l.Path, ["duration"] = l.Duration, ["samples"] = s });
      }
    }
    File.WriteAllText(args[2], all.ToJsonString());
    Console.WriteLine($"vfxlights: {all.Count} lights -> {args[2]}");
    return 0;
  }
}
