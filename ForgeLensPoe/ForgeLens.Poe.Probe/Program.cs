// ForgeLens.Poe.Probe - read-only probe of a PoE2 Content.ggpk (uses the private LibGGPK3 copy, AGPL-3.0).
using System.Diagnostics;
using System.Text.RegularExpressions;
using LibBundledGGPK3;
using LibBundle3;
using G = LibGGPK3.Records;

if (args.Length < 2) { Console.WriteLine("usage: list <ggpk> <outdir> | find <ggpk> <regex> [max] | extract <ggpk> <outdir> <path>..."); return 1; }
string mode = args[0], ggpkPath = args[1];
var sw = Stopwatch.StartNew();
// READ-ONLY: FileAccess.Read; FileShare.Read (other readers ok, no writers)
using var fs = new FileStream(ggpkPath, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read, Options = FileOptions.RandomAccess });
var hb = new byte[12]; fs.ReadExactly(hb); fs.Position = 0; uint ggVer = BitConverter.ToUInt32(hb, 8);
using var gg = new BundledGGPK(fs, false, false);
var tOpen = sw.Elapsed.TotalSeconds;
gg.Index.ParsePaths();
var tPaths = sw.Elapsed.TotalSeconds;
Console.WriteLine($"OPEN ggpk+index {tOpen:F3}s, paths {tPaths - tOpen:F3}s, total {tPaths:F3}s");
Console.WriteLine($"GGPKVersion {ggVer}  ggpkLength {fs.Length}");
var files = gg.Index.Files.Values;
if (mode == "list") {
    string od = args[2]; Directory.CreateDirectory(od);
    int ggFiles = 0; var ggTop = new SortedDictionary<string,int>();
    foreach (var (f, p) in G.TreeNode.RecurseFiles(gg.Root)) { ggFiles++; var k = string.Join('/', p.Split('/', StringSplitOptions.RemoveEmptyEntries).Take(2)); ggTop[k] = ggTop.GetValueOrDefault(k) + 1; }
    Console.WriteLine($"GGPK raw files {ggFiles}"); foreach (var kv in ggTop.Take(40)) Console.WriteLine($"  ggpk {kv.Key} {kv.Value}");
    Console.WriteLine($"INDEX bundles {gg.Index.Bundles.Length} files {gg.Index.Files.Count}");
    var top = new SortedDictionary<string,(int n,long b)>(); var ext = new SortedDictionary<string,(int n,long b)>();
    using (var w = new StreamWriter(Path.Combine(od, "poe2_filelist.txt"))) {
        foreach (var f in files.OrderBy(f => f.Path, StringComparer.Ordinal)) {
            var p = f.Path ?? $"<null:{f.PathHash:X16}>"; w.WriteLine($"{f.Size}\t{p}");
            var parts = p.Split('/'); 
            void add(SortedDictionary<string,(int,long)> d, string k) { var o = d.GetValueOrDefault(k); d[k] = (o.Item1 + 1, o.Item2 + f.Size); }
            add(top, parts[0]); if (parts.Length > 2) add(top, parts[0] + "/" + parts[1]);
            var e = Path.GetExtension(p).ToLowerInvariant(); add(ext, e == "" ? "(none)" : e);
        }
    }
    using (var w = new StreamWriter(Path.Combine(od, "poe2_tree2.txt"))) foreach (var kv in top) w.WriteLine($"{kv.Key}\t{kv.Value.n}\t{kv.Value.b}");
    using (var w = new StreamWriter(Path.Combine(od, "poe2_ext.txt"))) foreach (var kv in ext.OrderByDescending(k => k.Value.n)) w.WriteLine($"{kv.Key}\t{kv.Value.n}\t{kv.Value.b}");
    Console.WriteLine($"wrote lists, elapsed {sw.Elapsed.TotalSeconds:F3}s");
} else if (mode == "find") {
    var re = new Regex(args[2], RegexOptions.IgnoreCase); int max = args.Length > 3 ? int.Parse(args[3]) : 50;
    foreach (var f in files.Where(f => f.Path != null && re.IsMatch(f.Path)).OrderBy(f => f.Size).Take(max)) Console.WriteLine($"{f.Size}\t{f.Path}");
} else if (mode == "extract") {
    string od = args[2];
    foreach (var p in args.Skip(3)) {
        if (!gg.Index.TryGetFile(p, out var f)) { Console.WriteLine($"MISSING {p}"); continue; }
        var t0 = sw.Elapsed.TotalSeconds; var data = f.Read();
        var dst = Path.Combine(od, p.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.WriteAllBytes(dst, data.ToArray()); Console.WriteLine($"OK {data.Length}\t{p}\t{sw.Elapsed.TotalSeconds - t0:F3}s");
    }
}
Console.WriteLine($"DONE {sw.Elapsed.TotalSeconds:F3}s");
return 0;

