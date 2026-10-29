using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Where .sfx text comes from: a loose directory tree or the game catalog.</summary>
public interface IEffectSource
{
  /// <summary>Every effect, keyed by lower-case basename (the engine references effects by basename).</summary>
  IReadOnlyDictionary<string, string> Paths { get; }
  string? ReadText(string path);
}

/// <summary>Loose <c>*.sfx</c> files under a directory (e.g. an extracted <c>ssl/sfx</c> tree).</summary>
public sealed class DirectoryEffectSource : IEffectSource
{
  private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

  public DirectoryEffectSource(string root)
  {
    foreach (var file in Directory.EnumerateFiles(root, "*.sfx", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
      _paths.TryAdd(Path.GetFileNameWithoutExtension(file), file);
  }

  public IReadOnlyDictionary<string, string> Paths => _paths;
  public string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
}

/// <summary>In-memory effects (tests).</summary>
public sealed class MemoryEffectSource(IReadOnlyDictionary<string, string> texts) : IEffectSource
{
  private readonly Dictionary<string, string> _paths = texts.Keys.ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);
  public IReadOnlyDictionary<string, string> Paths => _paths;
  public string? ReadText(string path) => texts.TryGetValue(path, out var t) ? t : null;
}

/// <summary>
///   Resolves .sfx composition into one self-contained tree per effect:
///   <list type="bullet">
///     <item><c>sfx_group.list = { name = { overrides } }</c>: an entry without its own <c>__type</c> instances the
///       effect file <c>name.sfx</c> (by basename) and deep-merges the overrides on top; an entry whose
///       <c>__type</c> is a node type (<c>sfx_part</c>, <c>sfx_light</c>, ...) is inline.</item>
///     <item>Prototype inheritance: a <c>__type</c> that names another effect file means "that file, with these fields
///       replaced" (objects merge recursively, arrays and scalars replace).</item>
///   </list>
///   Strings for the node types are in sm2_retail.exe (sfx_group @0x143646930, sfx_part @0x14363c148,
///   sfx_trail @0x14363c280, CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0).
/// </summary>
public sealed class EffectLibrary(IEffectSource source)
{
  private readonly Dictionary<string, JsonObject?> _raw = new(StringComparer.OrdinalIgnoreCase);

  public IEffectSource Source => source;
  public IEnumerable<string> Names => source.Paths.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
  public string? PathOf(string name) => source.Paths.TryGetValue(name, out var p) ? p : null;

  public JsonObject? Raw(string name)
  {
    if (_raw.TryGetValue(name, out var cached)) return cached;
    JsonObject? parsed = null;
    if (source.Paths.TryGetValue(name, out var path) && source.ReadText(path) is { } text)
    {
      try { parsed = SslText.Parse(text); }
      catch (Exception) { parsed = null; }
    }

    _raw[name] = parsed;
    return parsed;
  }

  /// <summary>Fully expanded tree for <paramref name="name"/> (inheritance applied, group entries instanced).</summary>
  public JsonObject? Resolve(string name) => Raw(name) is { } raw ? Expand(raw, 0, [name]) : null;

  /// <summary>mtl1-53: an override node (a class <c>sfx.list</c> entry: <c>__type</c> = effect file plus replaced fields), expanded.</summary>
  public JsonObject ResolveNode(JsonObject node) => Expand(node, 0, []);

  private JsonObject Expand(JsonObject node, int depth, HashSet<string> chain)
  {
    var result = node;
    var type = node.Str("__type");
    if (depth < 16 && type is not null && !IsNodeType(type) && !type.StartsWith("local__", StringComparison.Ordinal)
        && !chain.Contains(type) && Raw(type) is { } baseNode)
    {
      chain.Add(type);
      var expandedBase = Expand(baseNode, depth + 1, chain);
      chain.Remove(type);
      var copy = (JsonObject)expandedBase.DeepClone();
      var overrides = (JsonObject)node.DeepClone();
      overrides.Remove("__type");
      Merge(copy, overrides);
      result = copy;
    }
    else
      result = (JsonObject)node.DeepClone();

    if (result.Str("__type") == "sfx_group" && result["list"] is JsonObject list)
    {
      var expanded = new JsonObject();
      foreach (var (key, value) in list.ToList())
      {
        if (value is not JsonObject entry) continue;
        var baseName = key.Split('#')[0];
        JsonObject child;
        var entryType = entry.Str("__type");
        if (entryType is null && depth < 16 && !chain.Contains(baseName) && Raw(baseName) is { } referenced)
        {
          chain.Add(baseName);
          child = (JsonObject)Expand(referenced, depth + 1, chain).DeepClone();
          chain.Remove(baseName);
          Merge(child, (JsonObject)entry.DeepClone());
          child["__file"] = baseName;
        }
        else
        {
          // mtl1-41: an untyped entry with no effect file of that name whose key is itself a node type name
          // ("sfx_part" 360x, "sfx_trail" 54x in the retail corpus, payload emitters2 / trailParams) is an inline node
          // of that type: 342 effects (e.g. fx_krak_grenade_explosion, 59 CPU emitters) parsed 0 emitters before.
          // INFERRED from the data (every such payload matches the type), the exe list loader was not traced.
          if (entryType is null && IsNodeType(baseName))
          {
            entry = (JsonObject)entry.DeepClone();
            entry["__type"] = baseName;
          }

          chain.Add(baseName);
          child = Expand(entry, depth + 1, chain);
          chain.Remove(baseName);
        }

        expanded[key] = child;
      }

      result["list"] = expanded;
    }

    return result;
  }

  public static bool IsNodeType(string type) => type.StartsWith("sfx_", StringComparison.Ordinal) || type is "CPU_EMITTER" or "GPU_EMITTER";

  /// <summary>Recursive merge: objects merge key by key, everything else is replaced.</summary>
  public static void Merge(JsonObject target, JsonObject overrides)
  {
    foreach (var (key, value) in overrides.ToList())
    {
      overrides.Remove(key);
      if (value is JsonObject o && target[key] is JsonObject t)
        Merge(t, o);
      else
        target[key] = value;
    }
  }
}
