using System.Diagnostics;
using System.Text;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SM2.Assets.Export;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   R76: headless stage timing of a model open (the PreviewFileAsync Model branch without the window):
///   <c>--openbench &lt;game folder&gt; &lt;out.tsv&gt; &lt;model.tpl&gt;...</c>. Per model: mesh read+decode, texture
///   list, per-texture archive read vs pixel decode (serial = pre-R76 path), then <see cref="MainWindow.ResolvePreview"/>
///   (current path) and a byte-for-byte compare of every decoded image between the two.
/// </summary>
internal static class OpenBench
{
  public static int Run(string[] args)
  {
    var game = args[1]; var outFile = args[2];
    var sb = new StringBuilder();
    void Line(string s) { sb.AppendLine(s); File.WriteAllText(outFile, sb.ToString()); }
    var sw = Stopwatch.StartNew();
    var catalog = GameCatalog.Load(game);
    using var store = new ArchiveStore(catalog);
    Line($"# cores {Environment.ProcessorCount} catalog {sw.ElapsedMilliseconds} ms");
    Line("model\trun\tmesh_ms\tverts\tbones\ttex_n\ttex_mpix\tserial_read_ms\tserial_decode_ms\tserial_total_ms\tresolve_preview_ms\tidentical");
    for (var run = 1; run <= 2; run++)
      foreach (var model in args.Skip(3))
      {
        var hits = catalog.FindByFileName(model);
        if (hits.Count == 0) { Line($"{model}\tnot found"); continue; }
        var index = hits[0];
        sw.Restart();
        var mesh = TemplateMeshReader.Load(catalog, store, index);
        var meshMs = sw.ElapsedMilliseconds;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in mesh.Parts.Concat(mesh.Levels.SelectMany(l => l.Parts)))
          foreach (var n in new[] { p.Texture, p.Normal, p.Specular })
            if (!string.IsNullOrWhiteSpace(n) && !MaterialMaps.TrySolid(n, out _, out _, out _, out _)) names.Add(n);
        long readMs = 0, decMs = 0, px = 0;
        var serial = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var t0 = Stopwatch.StartNew();
        foreach (var n in names)
        {
          var s1 = Stopwatch.StartNew();
          var ti = TemplateMeshReader.ResolveTexture(catalog, index, n);
          if (ti < 0) continue;
          TextureDocument doc;
          try { doc = TextureLoader.Load(catalog, store, ti); } catch { continue; }
          readMs += s1.ElapsedMilliseconds; s1.Restart();
          if (!doc.CanDecode) continue;
          try { var b = doc.Decode(0, 0, out var w, out var h, out _); px += (long)w * h; serial[n] = b; } catch { }
          decMs += s1.ElapsedMilliseconds;
        }
        var serialMs = t0.ElapsedMilliseconds;
        sw.Restart();
        var tex = MainWindow.ResolvePreview(catalog, store, mesh, index);
        var rpMs = sw.ElapsedMilliseconds;
        var all = tex.Albedo.Concat(tex.Normals).Concat(tex.Specular).Where(kv => serial.ContainsKey(kv.Key)).ToList();
        var same = all.Count == serial.Count && all.All(kv => kv.Value.Bgra.AsSpan().SequenceEqual(serial[kv.Key]));
        Line($"{model}\t{run}\t{meshMs}\t{mesh.Positions.Count}\t{mesh.Bones.Count}\t{serial.Count}\t{px / 1e6:0.0}\t{readMs}\t{decMs}\t{serialMs}\t{rpMs}\t{same}({all.Count}/{serial.Count})");
      }
    Line("# done");
    return 0;
  }
}
