using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   mtl1-51 survey: <c>--fxparts &lt;game folder&gt; &lt;tpl path regex&gt; &lt;out.txt&gt;</c>. Loads every matching tpl and lists
///   the parts <see cref="MaterialMaps.HideSfxParts"/> classifies (sfx shader cards, sfx sources) plus every other part with the
///   placeholder material or an sfx-looking name, with its default visibility, so the hide rule can be checked across models.
/// </summary>
internal static class FxPartsBench
{
  public static int Run(string[] args)
  {
    var game = args[1];
    var filter = new Regex(args.Length > 2 ? args[2] : @"/wpn_", RegexOptions.IgnoreCase);
    var output = args.Length > 3 ? args[3] : "fxparts.txt";
    var log = new StringBuilder();
    var watch = Stopwatch.StartNew();
    var catalog = GameCatalog.Load(game);
    var store = new ArchiveStore(catalog);
    int models = 0, failed = 0;
    var counts = new Dictionary<string, int>();
    for (var i = 0; i < catalog.FileCount; i++)
    {
      var path = catalog.GetFile(i).Path;
      if (!path.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase) || !filter.IsMatch(path))
        continue;
      ExportedMesh mesh;
      try
      {
        mesh = TemplateMeshReader.Load(catalog, store, i);
      }
      catch (Exception ex)
      {
        failed++;
        log.AppendLine($"FAIL\t{path}\t{ex.GetType().Name}: {ex.Message}");
        continue;
      }

      models++;
      var lod = 0;
      foreach (var parts in new[] { mesh.Parts }.Concat(mesh.Levels.Select(l => l.Parts)))
      {
        foreach (var part in parts)
        {
          var placeholder = MaterialMaps.IsPlaceholderMaterial(part.Texture);
          var named = Regex.IsMatch(part.Name ?? "", @"(^|_)(sfx|fx|exaust|exhaust|vfx)", RegexOptions.IgnoreCase);
          if (!part.Sfx && !placeholder && !named && part.HideReason is not ("sfx" or "sfxSource"))
            continue;
          var kind = part.Sfx ? "SFX" : placeholder ? "PLACEHOLDER" : "NAME";
          var state = part.Visible ? "visible" : "hidden:" + part.HideReason;
          counts[kind + " " + state] = counts.GetValueOrDefault(kind + " " + state) + 1;
          log.AppendLine($"{kind}\t{state}\t{path}\tL{lod}\t{part.Name}\t{part.Texture}\t{part.Variant}\t{part.SfxBlend}\ttris={part.IndexCount / 3}");
        }

        lod++;
      }
    }

    log.Insert(0, $"fxparts {DateTime.Now:yyyy-MM-dd HH:mm:ss} filter={filter} models={models} failed={failed} {watch.Elapsed.TotalSeconds:0}s\n"
      + string.Join("\n", counts.OrderBy(c => c.Key).Select(c => $"# {c.Key}: {c.Value}")) + "\n");
    File.WriteAllText(output, log.ToString());
    Console.WriteLine($"fxparts: {models} models, {failed} failed -> {output}");
    return 0;
  }
}
