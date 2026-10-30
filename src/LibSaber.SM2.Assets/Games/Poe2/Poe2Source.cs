using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LibSaber.SM2.Assets.Games.Poe2;

/// <summary>
///   M2 viewer source for PoE2: wraps the poe_data_tools CLI (read-only on the GGPK) to list files and to build a per-asset
///   prepared cache (<c>raw/</c>, <c>json/</c>, <c>tables/</c>, <c>.ast.anim.bin</c>) that <see cref="Poe2Reader"/> reads.
///   References are followed generically: every art/... or metadata/... path with a known extension found in the translated JSON
///   is extracted and translated too (up to 4 rounds), so an .sm pulls its .smd/.mat/.dds and an .ao its .ast/.pet/.trl/.fmt/.mat/.dds.
/// </summary>
public sealed class Poe2Source
{
  public string Ggpk { get; }
  public string Cache { get; }
  public string Tool { get; }
  public string AstTool { get; }
  public List<string> Log { get; } = new();

  public Poe2Source(string ggpk, string cache, string toolDir)
  {
    Ggpk = ggpk; Cache = cache;
    Tool = Path.Combine(toolDir, "poe_data_tools.exe");
    AstTool = Path.Combine(toolDir, "ast_bundle.exe");
    Directory.CreateDirectory(cache);
  }

  public static readonly string[] Browsable = { ".sm", ".fmt", ".ao" };

  private string Run(string exe, IEnumerable<string> args, int timeoutMs = 600_000)
  {
    var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    var err = p.StandardError.ReadToEndAsync();
    var output = p.StandardOutput.ReadToEnd();
    p.WaitForExit(timeoutMs);
    _ = err.Result;
    return output;
  }

  private IEnumerable<string> Base() => new[] { "-p", "2", "--ggpk", Ggpk };

  /// <summary>Browsable file paths (.sm/.fmt/.ao), cached in <c>files.txt</c> after the first full listing.</summary>
  public List<string> Files()
  {
    var f = Path.Combine(Cache, "files.txt");
    if (!File.Exists(f))
    {
      var all = Run(Tool, Base().Concat(new[] { "list" }), 1_800_000);
      var keep = all.Split((char)10).Select(s => s.Trim()).Where(s => Browsable.Append(".ast").Any(e => s.EndsWith(e, StringComparison.Ordinal))).ToList();
      File.WriteAllLines(f, keep);
    }
    return File.ReadAllLines(f).ToList();
  }

  private static readonly Regex Ref = new(@"((?:art|metadata)/[^""\\\s]+?\.(?:ast|smd|sm|fmt|mat|pet|trl|dds|ao))(?=[""\\\s])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly string[] Translatable = { ".ast", ".smd", ".sm", ".fmt", ".mat", ".pet", ".trl", ".ao" };

  private string JsonPath(string p) => Path.Combine(Cache, "json", p.Replace('/', Path.DirectorySeparatorChar) + ".json");
  private string RawPath(string p) => Path.Combine(Cache, "raw", p.Replace('/', Path.DirectorySeparatorChar));

  /// <summary>Make <paramref name="path"/> (and what it references) readable by <see cref="Poe2Reader"/> with prepared folder = <see cref="Cache"/>.</summary>
  public void Prepare(string path, string? extra = null)
  {
    var todo = new List<string> { path.ToLowerInvariant() };
    if (extra != null) todo.Add(extra.ToLowerInvariant());
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var round = 0; round < 4 && todo.Count > 0; round++)
    {
      var batch = todo.Where(seen.Add).Where(p => !File.Exists(RawPath(p))).ToList();
      var trans = batch.Where(p => Translatable.Contains(Path.GetExtension(p))).ToList();
      if (batch.Count > 0) Run(Tool, Base().Concat(new[] { "extract", Path.Combine(Cache, "raw") }).Concat(batch));
      if (trans.Count > 0) Run(Tool, Base().Concat(new[] { "translate", Path.Combine(Cache, "json") }).Concat(trans));
      Log.Add($"round {round}: extract {batch.Count}, translate {trans.Count}");
      var next = new List<string>();
      foreach (var p in seen.Where(p => Translatable.Contains(Path.GetExtension(p))))
      {
        var j = JsonPath(p);
        if (!File.Exists(j)) continue;
        var text = File.ReadAllText(j);
        foreach (Match m in Ref.Matches(text))
          next.Add(m.Groups[1].Value.ToLowerInvariant());
        // .sm text references are UTF-16 in raw, already in json; .ast animation bundle
      }
      todo = next.Where(n => !seen.Contains(n)).Distinct().ToList();
    }
    foreach (var ast in seen.Where(p => p.EndsWith(".ast")))
    {
      var raw = RawPath(ast);
      if (File.Exists(raw) && !File.Exists(raw + ".anim.bin"))
        Run(AstTool, new[] { raw, raw + ".anim.bin" });
    }
  }

  /// <summary>Skill tables for <see cref="Poe2Skills"/> (dump-tables JSON, once).</summary>
  public string Tables()
  {
    var dir = Path.Combine(Cache, "tables", "data", "balance");
    var need = new[] { "activeskills", "grantedeffects", "skillartvariations", "projectilesartvariations", "projectiles", "miscanimatedartvariations", "miscanimated" };
    var missing = need.Where(n => !File.Exists(Path.Combine(dir, n + ".datc64.json"))).Select(n => $"data/balance/{n}.datc64").ToList();
    if (missing.Count > 0)
      Run(Tool, Base().Concat(new[] { "dump-tables", "--mode", "json", Path.Combine(Cache, "tables") }).Concat(missing));
    return dir;
  }

  /// <summary>Skeleton guess for a mesh: same folder rig.ast, then &lt;name&gt;.ast, then the parent folder's rig.ast.</summary>
  public static string? GuessSkeleton(string mesh, IReadOnlyCollection<string>? known = null)
  {
    var dir = mesh[..mesh.LastIndexOf('/')];
    var name = Path.GetFileNameWithoutExtension(mesh);
    var cands = new List<string> { dir + "/rig.ast", dir + "/" + name + ".ast" };
    var up = dir.LastIndexOf('/');
    if (up > 0) cands.Add(dir[..up] + "/rig.ast");
    return known == null ? cands[0] : cands.FirstOrDefault(known.Contains);
  }
}
