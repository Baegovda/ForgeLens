using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2Browser.ContentBrowser;

namespace LibSaber.SM2Browser.Bench;

/// <summary>R75: <c>--thumbbench &lt;game&gt; &lt;path regex&gt; &lt;out dir&gt; [max]</c>: grid thumbnail before (clay) / after (textured), PNGs + ms.</summary>
internal static class ThumbBench
{
  public static int Run(string[] args)
  {
    var catalog = GameCatalog.Load(args[1]);
    using var store = new ArchiveStore(catalog);
    var rx = new Regex(args[2], RegexOptions.IgnoreCase);
    var dir = args[3]; Directory.CreateDirectory(dir);
    var max = args.Length > 4 && int.TryParse(args[4], out var m) ? m : 8;
    var log = new StringBuilder();
    var n = 0;
    for (var i = 0; i < catalog.FileCount && n < max; i++)
    {
      var f = catalog.GetFile(i);
      if (f.Kind is not AssetKind.Model || !rx.IsMatch(f.Path)) continue;
      n++;
      var name = Path.GetFileNameWithoutExtension(f.Name);
      foreach (var tex in new[] { false, true })
      {
        var sw = Stopwatch.StartNew();
        var px = ThumbnailService.RenderModel(catalog, store, i, tex);
        var ms = sw.Elapsed.TotalMilliseconds;
        if (px is not null) PreviewBench.Png(Path.Combine(dir, $"{name}_{(tex ? "after" : "before")}.png"), px, ThumbnailService.StillSize, ThumbnailService.StillSize);
        log.Append($"{f.Path}\t{(tex ? "textured" : "clay")}\t{ms:0}ms\n");
      }
    }
    File.WriteAllText(Path.Combine(dir, "thumbbench.txt"), log.ToString());
    return 0;
  }
}
