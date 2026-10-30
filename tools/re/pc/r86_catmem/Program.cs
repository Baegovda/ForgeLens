// R86: managed heap / private bytes of GameCatalog.Load alone (startup catalog memory A/B) + lookup self-check.
using System.Diagnostics;
using LibSaber.SM2.Assets.Catalog;
var dir = args.Length > 0 ? args[0] : @"D:\Steam\steamapps\common\Space Marine 2";
long Heap() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); return GC.GetTotalMemory(true); }
var p = Process.GetCurrentProcess();
var h0 = Heap(); p.Refresh(); var pr0 = p.PrivateMemorySize64;
var sw = Stopwatch.StartNew();
var cat = GameCatalog.Load(dir);
var t = sw.Elapsed.TotalSeconds;
var h1 = Heap(); p.Refresh();
Console.WriteLine($"r86catmem files {cat.FileCount} archives {cat.Archives.Count} load_s {t:0.00} heap_mb {(h1 - h0) / 1048576.0:0.0} private_mb {(p.PrivateMemorySize64 - pr0) / 1048576.0:0.0}");
// self-check: brute force over every file vs the lookups, 3000 random files
var all = Enumerable.Range(0, cat.FileCount).Select(i => cat.GetFile(i)).ToArray();
var byName = all.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(f => f.Index).OrderBy(x => x).ToArray(), StringComparer.OrdinalIgnoreCase);
var rnd = new Random(86); int bad = 0, checkedN = 0;
sw.Restart();
for (var k = 0; k < 3000; k++)
{
  var f = all[rnd.Next(all.Length)]; checkedN++;
  if (!cat.TryGetByPath(f.Path.ToUpperInvariant(), out var i) || i != f.Index) bad++;
  if (!cat.FindByPath(f.Path).SequenceEqual(new[] { f.Index })) bad++;
  if (!cat.FindByFileName(f.Name).SequenceEqual(byName[f.Name])) bad++;
  if (cat.ResolveReference(f.Path, -1) != f.Index) bad++;
}
var lk = sw.Elapsed.TotalMilliseconds;
if (cat.TryGetByPath("no/such/file.xyz", out _) || cat.FindByFileName("no_such_file.xyz").Count != 0) bad++;
Console.WriteLine($"r86catcheck checked {checkedN} bad {bad} lookup_ms_12000 {lk:0} calgar {cat.TryGetByPath("tpl/cc_calgar.tpl/cc_calgar.tpl", out var ci)} {ci} name_hits {cat.FindByFileName("cc_calgar.tpl").Count}");
GC.KeepAlive(cat);
