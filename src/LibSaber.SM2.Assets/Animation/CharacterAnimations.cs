using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>Which of a character's clips are its own: <see cref="Main"/> ★, <see cref="Cinematic"/> ☆, or neither.</summary>
public enum ClipRank
{
  Main = 0,
  Cinematic = 1,
  Other = 2,
}

/// <summary>
///   Finds the animations that belong to a character template, from game data first.
///   <list type="number">
///     <item>Character classes (<c>ssl/**/*.cls</c>, text) name their template in <c>geom = { nameTpl = "cc_titus" }</c>.
///       Their parent class is the file's last <c>__type</c>; the chain ends in a base class whose
///       <c>morphemeController.nameBundle</c> is the Morpheme network (for example pc_marine_base_client → "primaris").
///       <c>animSet</c> (for example "titus") names the character's set inside that network.</item>
///     <item><c>morpheme/&lt;bundle&gt;/&lt;bundle&gt;.mr.resource</c> (JSON <c>res_desc_morpheme</c>) lists every clip the
///       network links in <c>linksAnimation</c>. Those clips are the character's gameplay set (★).</item>
///     <item>Cinematic clips (☆) come from cinematic/story/shot/scene sources and carry the character's name token
///       (the template stem's longest token, plus the class animSet), as in <c>..._char_titus_cinematic_titus.sani</c>.</item>
///   </list>
///   Without a class link, the fallback is name tokens only: ★ = token-named non-cinematic clips.
/// </summary>
public static class CharacterAnimations
{
  public sealed record Result(
    IReadOnlySet<string> Main,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<string> Bundles,
    IReadOnlyList<string> Classes,
    string Source)
  {
    public bool FromData => Main.Count > 0;

    public ClipRank Rank(string clipPath)
    {
      var path = clipPath.Replace('\\', '/');
      if (Main.Contains(path))
        return ClipRank.Main;
      var named = NamesCharacter(path, Tokens);
      if (named && IsCinematic(path))
        return ClipRank.Cinematic;
      if (!FromData && named && !IsLipsync(path))
        return ClipRank.Main;
      return ClipRank.Other;
    }

    public bool Named(string clipPath)
      => NamesCharacter(clipPath.Replace('\\', '/'), Tokens);
  }

  private sealed record ClassInfo(string Name, string? Tpl, string? Bundle, string? AnimSet, string? Parent, string Path);

  private static readonly ConditionalWeakTable<GameCatalog, Dictionary<string, ClassInfo>> ClassCache = new();
  private static readonly Regex TplRegex = new(@"\bgeom\s*=\s*\{\s*nameTpl\s*=\s*""([^""]+)""", RegexOptions.Compiled);
  private static readonly Regex BundleRegex = new(@"\bnameBundle\s*=\s*""([^""]+)""", RegexOptions.Compiled);
  private static readonly Regex AnimSetRegex = new(@"\banimSet\s*=\s*""([^""]+)""", RegexOptions.Compiled);
  private static readonly Regex ParentRegex = new(@"__type\s*=\s*""([^""]+)""\s*$", RegexOptions.Compiled);

  private static readonly string[] CinematicMarks =
    { "/cinematics/", "cinematic", "/scenes/", "xscene", "_shot_", "/shot_", "/story_", "story_" };

  public static bool IsCinematic(string path)
  {
    var lower = path.ToLowerInvariant();
    // Interplay (in-game interactions) and lipsync are not cut-scenes even when a story name appears in them.
    if (lower.Contains("/interplay/") || IsLipsync(lower))
      return false;
    return CinematicMarks.Any(lower.Contains);
  }

  public static bool IsLipsync(string path)
    => path.Contains("/lipsync/", StringComparison.OrdinalIgnoreCase);

  /// <summary>The token appears as its own word in the file name: '_titus_', not 'titusvane'.</summary>
  public static bool NamesCharacter(string path, IReadOnlyList<string> tokens)
  {
    var name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
    foreach (var token in tokens)
    {
      var at = 0;
      while ((at = name.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
      {
        var before = at == 0 || !char.IsLetter(name[at - 1]);
        var end = at + token.Length;
        var after = end >= name.Length || !char.IsLetter(name[end]);
        if (before && after)
          return true;
        at = end;
      }
    }

    return false;
  }

  /// <summary>Longest token of the template stem (cc_titus → titus), as the browser always used.</summary>
  public static string ModelToken(string modelPath)
  {
    var stem = System.IO.Path.GetFileNameWithoutExtension(modelPath);
    var tokens = stem.Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries)
      .Where(token => token.Length >= 3 && !token.All(char.IsDigit))
      .ToArray();
    return (tokens.Length == 0 ? stem : tokens.OrderByDescending(token => token.Length).First()).ToLowerInvariant();
  }

  public static Result Resolve(GameCatalog catalog, ArchiveStore store, string modelPath)
  {
    var stem = System.IO.Path.GetFileNameWithoutExtension(modelPath);
    var classes = Classes(catalog, store);
    var tokens = new List<string> { ModelToken(modelPath) };
    var bundles = new List<string>();
    var names = new List<string>();
    foreach (var info in classes.Values.Where(c => string.Equals(c.Tpl, stem, StringComparison.OrdinalIgnoreCase)).OrderBy(c => c.Path, StringComparer.Ordinal))
    {
      names.Add(info.Path);
      string? bundle = null, set = null;
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (var node = info; node is not null && seen.Add(node.Name); node = node.Parent is null ? null : classes.GetValueOrDefault(node.Parent))
      {
        bundle ??= node.Bundle;
        set ??= node.AnimSet;
      }

      if (bundle is not null && !bundles.Contains(bundle, StringComparer.OrdinalIgnoreCase))
        bundles.Add(bundle);
      if (set is { Length: >= 3 } && !tokens.Contains(set.ToLowerInvariant()))
        tokens.Add(set.ToLowerInvariant());
    }

    var main = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var bundle in bundles)
    {
      foreach (var index in catalog.FindByPath($"morpheme/{bundle}/{bundle}.mr.resource"))
      {
        try
        {
          using var json = JsonDocument.Parse(store.Read(index));
          if (!json.RootElement.TryGetProperty("linksAnimation", out var links))
            continue;
          foreach (var link in links.EnumerateArray())
          {
            var text = link.GetString() ?? "";
            if (text.StartsWith("res://", StringComparison.Ordinal))
              text = text[6..];
            if (text.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
              text = text[..^9];
            main.Add(text);
          }
        }
        catch (Exception)
        {
          // A broken resource only loses the data link; the name fallback still works.
        }

        break;
      }
    }

    var source = main.Count > 0
      ? $"{names.Count} class(es) with nameTpl \"{stem}\" → Morpheme bundle {string.Join(", ", bundles)} ({main.Count:N0} linked clips)"
      : names.Count > 0
        ? $"{names.Count} class(es) with nameTpl \"{stem}\" but no Morpheme bundle list; name tokens only"
        : $"no class names template \"{stem}\"; name tokens only";
    return new Result(main, tokens, bundles, names, source);
  }

  /// <summary>Archive path and parent class (<c>__type</c>) of a class by name, from the cached class scan.</summary>
  public static (string Path, string? Parent)? FindClass(GameCatalog catalog, ArchiveStore store, string name)
    => Classes(catalog, store).TryGetValue(name, out var info) ? (info.Path, info.Parent) : null;

  private static Dictionary<string, ClassInfo> Classes(GameCatalog catalog, ArchiveStore store)
  {
    lock (ClassCache)
    {
      if (ClassCache.TryGetValue(catalog, out var cached))
        return cached;
    }

    var result = new Dictionary<string, ClassInfo>(StringComparer.OrdinalIgnoreCase);
    var query = catalog.Query(null, ".cls", AssetFilter.All);
    var lockObject = new object();
    var indices = Enumerable.Range(0, query.Length).Select(query.IndexAt).ToArray();
    Parallel.ForEach(indices, index =>
    {
      var file = catalog.GetFile(index);
      if (!file.Path.EndsWith(".cls", StringComparison.OrdinalIgnoreCase))
        return;
      string text;
      try
      {
        text = Encoding.UTF8.GetString(store.Read(index));
      }
      catch (Exception)
      {
        return;
      }

      var head = text.Length > 4096 ? text[..4096] : text;
      var tail = text.Length > 512 ? text[^512..] : text;
      var info = new ClassInfo(
        System.IO.Path.GetFileNameWithoutExtension(file.Path),
        Group(TplRegex.Match(head)),
        Group(BundleRegex.Match(text)),
        Group(AnimSetRegex.Match(text)),
        Group(ParentRegex.Match(tail.TrimEnd())),
        file.Path);
      lock (lockObject)
        result.TryAdd(info.Name, info);
    });
    lock (ClassCache)
    {
      ClassCache.AddOrUpdate(catalog, result);
    }

    return result;
  }

  private static string? Group(Match match)
    => match.Success ? match.Groups[1].Value : null;
}
