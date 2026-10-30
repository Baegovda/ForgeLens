using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   R71: <c>--meshparts &lt;game folder&gt; &lt;path regex&gt; &lt;out.tsv&gt; [max]</c>: loads every matching model
///   (.tpl) like the viewer does and writes one row per part: path, lod, part, hideReason, visible, tris, nonRender
///   (<see cref="MeshToggle.IsNonRender"/>), so the Meshes-tab grouping can be inventoried without a window.
/// </summary>
internal static class MeshPartsBench
{
  public static int Run(string[] args)
  {
    var game = args[1];
    var rx = new Regex(args.Length > 2 ? args[2] : @"\.tpl$", RegexOptions.IgnoreCase);
    var output = args.Length > 3 ? args[3] : "meshparts.tsv";
    var max = args.Length > 4 && int.TryParse(args[4], out var m) ? m : int.MaxValue;
    var catalog = GameCatalog.Load(game);
    using var store = new ArchiveStore(catalog);
    var sb = new StringBuilder("path\tlod\tpart\thideReason\tvisible\ttris\tnonRender\n");
    int done = 0, failed = 0;
    for (var i = 0; i < catalog.FileCount && done < max; i++)
    {
      var file = catalog.GetFile(i);
      if (file.Kind is not AssetKind.Model || !rx.IsMatch(file.Path))
        continue;
      done++;
      try
      {
        var mesh = TemplateMeshReader.Load(catalog, store, i);
        void Dump(string lod, IEnumerable<MeshPart> parts)
        {
          foreach (var p in parts)
            sb.Append(file.Path).Append('\t').Append(lod).Append('\t').Append(p.Name).Append('\t').Append(p.HideReason ?? "").Append('\t')
              .Append(p.Visible ? 1 : 0).Append('\t').Append(p.IndexCount / 3).Append('\t').Append(MeshToggle.IsNonRender(p) ? 1 : 0).Append('\n');
        }
        Dump("0", mesh.Parts);
        foreach (var level in mesh.Levels)
          Dump(level.Name, level.Parts);
      }
      catch (Exception e)
      {
        failed++;
        sb.Append(file.Path).Append("\t-\tLOADFAIL\t").Append(e.GetType().Name).Append("\t0\t0\t0\n");
      }
    }
    File.WriteAllText(output, sb.ToString());
    Console.WriteLine($"models {done} failed {failed} -> {output}");
    return 0;
  }
}
