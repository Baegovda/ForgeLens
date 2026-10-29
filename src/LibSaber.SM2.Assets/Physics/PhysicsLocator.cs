using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   Finds the character class that drives a template's cloth and jiggle bones. Candidates are the classes whose
///   <c>geom.nameTpl</c> is the template stem; the client class of the playable or NPC character wins over
///   librarium dummies, autogen and server/authority copies. A class without the properties falls back to its
///   parent chain (<c>__type</c>).
/// </summary>
public static class PhysicsLocator
{
  public interface IClassSource
  {
    IEnumerable<string> ClassesForTemplate(string stem);
    string? Read(string path);
    string? PathOf(string className);
    string? ParentOf(string path);
  }

  public static int Score(string path) => Score(path, null);

  public static int Score(string path, string? token)
  {
    var p = path.Replace('\\', '/').ToLowerInvariant();
    var score = 0;
    if (token is { Length: >= 3 } && Path.GetFileName(p).Contains(token, StringComparison.Ordinal)) score += 30;
    if (p.EndsWith("_client.cls", StringComparison.Ordinal)) score += 100;
    else if (p.EndsWith("_authority.cls", StringComparison.Ordinal)) score += 40;
    if (p.Contains("librarium") || p.Contains("models_viewer")) score -= 200;
    if (p.Contains("autogen")) score -= 100;
    if (p.Contains("/characters/")) score += 20;
    if (p.Contains("_damaged") || p.Contains("_deathwatch") || p.Contains("_undamaged")) score -= 5;
    return score;
  }

  public static PhysicsSetup? Find(IClassSource source, string modelPath)
  {
    var stem = Path.GetFileNameWithoutExtension(modelPath);
    var candidates = source.ClassesForTemplate(stem)
      .OrderByDescending(p => Score(p, CharacterAnimations.ModelToken(modelPath)))
      .ThenBy(p => p.Length)
      .ThenBy(p => p, StringComparer.Ordinal)
      .ToList();
    PhysicsSetup? setup = null;
    string? from = null;
    foreach (var candidate in candidates)
    {
      foreach (var path in Chain(source, candidate))
      {
        var text = source.Read(path);
        if (text is null || !PhysicsParser.Declares(text))
          continue;
        var parsed = PhysicsParser.Parse(text, path);
        if (parsed.Cloths.Count == 0 && parsed.Jiggles.Count == 0)
          continue; // phys52: a damper-only class does not stop the cloth/jiggle search (dampers are merged below)
        if (!string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase))
          parsed.Notes.Add($"inherited from {path} (via {candidate})");
        setup = parsed;
        from = candidate;
        break;
      }
      if (setup is not null)
        break;
    }

    // phys52: prop_motion_damper_controller is often declared only on the authority (server) class (Titus: the client class
    // has cloth + jiggles, pc_marine_pve_story_titus_authority.cls has the box damper). Look in the chosen class chain, then
    // its *_authority counterpart (+ chain), then every other candidate in score order.
    if (setup is null || setup.SpringDampers.Count == 0)
    {
      var order = new List<string>();
      var first = from ?? candidates.FirstOrDefault();
      if (first is not null)
      {
        order.Add(first);
        if (Counterpart(source, first) is { } auth)
          order.Add(auth);
      }
      order.AddRange(candidates);
      var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var candidate in order)
      {
        string? hit = null;
        foreach (var path in Chain(source, candidate))
        {
          if (!visited.Add(path))
            continue;
          var text = source.Read(path);
          if (text is null || !PhysicsParser.DeclaresDamper(text))
            continue;
          var parsed = PhysicsParser.Parse(text, path);
          if (parsed.SpringDampers.Count == 0)
            continue;
          setup ??= new PhysicsSetup { SourceClass = path };
          setup.SpringDampers.AddRange(parsed.SpringDampers);
          setup.SpringDamperSource = path;
          if (!string.Equals(path, setup.SourceClass, StringComparison.OrdinalIgnoreCase))
            setup.Notes.Add($"phys52: prop_motion_damper_controller ({parsed.SpringDampers.Count} springDampers) read from {path}; {Path.GetFileName(setup.SourceClass)} has none");
          hit = path;
          break;
        }
        if (hit is not null)
          break;
      }
    }

    return setup is { IsEmpty: false } ? setup : null;
  }

  /// <summary>The class itself, then its <c>__type</c> parents.</summary>
  private static IEnumerable<string> Chain(IClassSource source, string start)
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var path = start; path is not null && seen.Add(path); path = source.ParentOf(path) is { } parent ? source.PathOf(parent) : null)
      yield return path;
  }

  /// <summary>phys52: <c>x_client.cls</c> -> <c>x_authority.cls</c> (by class name), or null.</summary>
  public static string? Counterpart(IClassSource source, string path)
  {
    var name = Path.GetFileNameWithoutExtension(path);
    if (!name.EndsWith("_client", StringComparison.OrdinalIgnoreCase))
      return null;
    return source.PathOf(name[..^"_client".Length] + "_authority");
  }

  public static PhysicsSetup? Find(GameCatalog catalog, ArchiveStore store, string modelPath)
    => Find(new CatalogSource(catalog, store), modelPath);

  private sealed class CatalogSource : IClassSource
  {
    private readonly GameCatalog _catalog;
    private readonly ArchiveStore _store;

    public CatalogSource(GameCatalog catalog, ArchiveStore store)
    {
      _catalog = catalog;
      _store = store;
    }

    public IEnumerable<string> ClassesForTemplate(string stem)
      => CharacterAnimations.Resolve(_catalog, _store, stem + ".tpl").Classes;

    public string? Read(string path)
    {
      foreach (var index in _catalog.FindByPath(path))
      {
        try
        {
          return Encoding.UTF8.GetString(_store.Read(index));
        }
        catch (Exception)
        {
        }
      }

      return null;
    }

    public string? PathOf(string className) => CharacterAnimations.FindClass(_catalog, _store, className)?.Path;

    public string? ParentOf(string path)
      => CharacterAnimations.FindClass(_catalog, _store, Path.GetFileNameWithoutExtension(path))?.Parent;
  }

  /// <summary>Loose <c>ssl/**/*.cls</c> tree on disk (an extractor dump).</summary>
  public sealed class DirectorySource : IClassSource
  {
    private readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string? Tpl, string? Parent)> _info = new(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex Tpl = new(@"\bgeom\s*=\s*\{\s*nameTpl\s*=\s*""([^""]+)""");
    private static readonly System.Text.RegularExpressions.Regex Parent = new(@"__type\s*=\s*""([^""]+)""\s*$");

    public DirectorySource(string root)
    {
      foreach (var file in Directory.EnumerateFiles(root, "*.cls", SearchOption.AllDirectories))
      {
        var text = File.ReadAllText(file);
        var head = text.Length > 4096 ? text[..4096] : text;
        var tail = (text.Length > 512 ? text[^512..] : text).TrimEnd();
        var tpl = Tpl.Match(head);
        var parent = Parent.Match(tail);
        _byName.TryAdd(Path.GetFileNameWithoutExtension(file), file);
        _info[file] = (tpl.Success ? tpl.Groups[1].Value : null, parent.Success ? parent.Groups[1].Value : null);
      }
    }

    public IEnumerable<string> ClassesForTemplate(string stem)
      => _info.Where(kv => string.Equals(kv.Value.Tpl, stem, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key);

    public string? Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
    public string? PathOf(string className) => _byName.GetValueOrDefault(className);
    public string? ParentOf(string path) => _info.TryGetValue(path, out var i) ? i.Parent : null;
  }
}
