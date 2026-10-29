using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Context;

/// <summary>
///   mtl1-49 context playback: the class graph that links an effect to the character, held weapon and blueprint that
///   start it. Built from every <c>.cls</c> (text) and its <c>.cls.resource</c> (the cooker's dependency list):
///   <list type="bullet">
///     <item><c>linksDescVisual</c>: the <c>.sfx</c> files a class can spawn (its <c>sfx.list</c> after inheritance).</item>
///     <item><c>linksDesc</c>: the blueprint <c>.sso</c> graphs it runs (<c>prop_blueprints</c>).</item>
///     <item><c>parents</c> / the trailing <c>__type</c>: inheritance.</item>
///     <item><c>__type = "X"</c> inside a class body naming another class: that class creates / owns an X
///       (inventory <c>localCreatedActorsDescs</c> weapons, spawned sfx entities, child actors).</item>
///   </list>
///   Details (sfx.list keys, weapons, hand holds) are parsed lazily from the class text.
/// </summary>
public sealed class ContextIndex
{
  private static readonly Regex TypeRegex = new(@"__type\s*=\s*""([A-Za-z0-9_]+)""", RegexOptions.Compiled);
  private static readonly Regex FxWordRegex = new(@"\b((?:fx|vfx|sfx)_[a-z0-9_]+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
  private static readonly Regex TplRegex = new(@"\bgeom\s*=\s*\{\s*nameTpl\s*=\s*""([^""]+)""", RegexOptions.Compiled);
  private static readonly Regex BundleRegex = new(@"\bnameBundle\s*=\s*""([^""]+)""", RegexOptions.Compiled);

  private readonly Dictionary<string, ContextClass> _classes = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, HashSet<string>> _effectOwners = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, HashSet<string>> _effectMentions = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, HashSet<string>> _users = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, HashSet<string>> _children = new(StringComparer.OrdinalIgnoreCase);
  private readonly Func<string, string?> _read;

  public IReadOnlyDictionary<string, ContextClass> Classes => _classes;

  private ContextIndex(Func<string, string?> read) => _read = read;

  /// <param name="paths">Archive paths; <c>.cls</c> and <c>.cls.resource</c> are used.</param>
  /// <param name="read">Text of a path (null when unreadable / binary).</param>
  public static ContextIndex Build(IEnumerable<string> paths, Func<string, string?> read)
  {
    var index = new ContextIndex(read);
    var list = paths.Where(p => p.EndsWith(".cls", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var rows = new (ContextClass Cls, List<string> Types, List<string> Fx)?[list.Count];
    Parallel.For(0, list.Count, i =>
    {
      var path = list[i];
      string? text;
      try { text = read(path); }
      catch { text = null; }
      if (text is null) return;
      var name = Path.GetFileNameWithoutExtension(path);
      var head = text.Length > 4096 ? text[..4096] : text;
      var tail = text.Length > 512 ? text[^512..] : text;
      var types = TypeRegex.Matches(text).Select(m => m.Groups[1].Value).ToList();
      var parentMatch = Regex.Match(tail.TrimEnd(), @"__type\s*=\s*""([^""]+)""\s*$");
      var cls = new ContextClass
      {
        Name = name,
        Path = path,
        Tpl = Group(TplRegex.Match(head)),
        Bundle = Group(BundleRegex.Match(text)),
        Parent = parentMatch.Success ? parentMatch.Groups[1].Value : null,
        IsWeaponPath = ("/" + path.Replace('\\', '/')).Contains("/weapons/", StringComparison.OrdinalIgnoreCase) && !path.Contains("weapon_dummy", StringComparison.OrdinalIgnoreCase),
        HasEquipment = text.Contains("prop_equipment", StringComparison.Ordinal) && text.Contains("loadoutItemCategoryUid", StringComparison.Ordinal),
      };
      string? resource = null;
      try { resource = read(path + ".resource"); }
      catch { }
      if (resource is not null) ReadResource(cls, resource);
      var fx = FxWordRegex.Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
      rows[i] = (cls, types, fx);
    });

    foreach (var row in rows)
      if (row is { } r) index._classes.TryAdd(r.Cls.Name, r.Cls);
    foreach (var row in rows)
    {
      if (row is not { } r) continue;
      var cls = r.Cls;
      foreach (var e in cls.Effects) Add(index._effectOwners, e, cls.Name);
      foreach (var e in r.Fx) Add(index._effectMentions, e, cls.Name);
      if (cls.Parent is { } parent && index._classes.ContainsKey(parent)) Add(index._children, parent, cls.Name);
      foreach (var p in cls.ResourceParents)
        if (index._classes.ContainsKey(p)) Add(index._children, p, cls.Name);
      foreach (var t in r.Types.Distinct(StringComparer.OrdinalIgnoreCase))
        if (!t.Equals(cls.Parent, StringComparison.OrdinalIgnoreCase) && !t.Equals(cls.Name, StringComparison.OrdinalIgnoreCase) && index._classes.ContainsKey(t))
          Add(index._users, t, cls.Name);
    }

    return index;
  }

  private static void ReadResource(ContextClass cls, string text)
  {
    string? key = null;
    foreach (var raw in text.Split('\n'))
    {
      var line = raw.TrimEnd('\r');
      if (line.Length == 0) continue;
      if (!line.StartsWith('-') && !line.StartsWith(' ') && line.Contains(':'))
      {
        key = line[..line.IndexOf(':')];
        continue;
      }

      var item = line.TrimStart(' ', '-').Trim();
      if (item.StartsWith("res://", StringComparison.Ordinal)) item = item[6..];
      if (item.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) item = item[..^9];
      var stem = Path.GetFileNameWithoutExtension(item);
      switch (key)
      {
        case "linksDescVisual" or "linksDesc" or "linksDescWeak" when item.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase):
          cls.Effects.Add(stem);
          break;
        case "linksDesc" or "linksDescVisual" when item.EndsWith(".sso", StringComparison.OrdinalIgnoreCase):
          cls.Blueprints.Add(stem);
          break;
        case "parents" when item.EndsWith(".cls", StringComparison.OrdinalIgnoreCase):
          cls.ResourceParents.Add(stem);
          break;
      }
    }
  }

  private static void Add(Dictionary<string, HashSet<string>> map, string key, string value)
  {
    if (!map.TryGetValue(key, out var set)) map[key] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    set.Add(value);
  }

  private static string? Group(Match m) => m.Success ? m.Groups[1].Value : null;

  public ContextClass? Get(string name) => _classes.GetValueOrDefault(name);

  /// <summary>Classes whose cooked dependencies list <paramref name="effect"/> (proof), else whose text names it.</summary>
  public (IReadOnlyCollection<string> Classes, bool FromResource) OwnersOf(string effect)
  {
    if (_effectOwners.TryGetValue(effect, out var owners) && owners.Count > 0) return (owners, true);
    return (_effectMentions.TryGetValue(effect, out var m) ? m : (IReadOnlyCollection<string>)Array.Empty<string>(), false);
  }

  public IReadOnlyCollection<string> UsersOf(string cls) => _users.TryGetValue(cls, out var u) ? u : Array.Empty<string>();
  public IReadOnlyCollection<string> ChildrenOf(string cls) => _children.TryGetValue(cls, out var c) ? c : Array.Empty<string>();

  /// <summary>The class and its ancestors (trailing <c>__type</c> and resource parents), nearest first.</summary>
  public List<ContextClass> Chain(string name)
  {
    var chain = new List<ContextClass>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var queue = new Queue<string>();
    queue.Enqueue(name);
    while (queue.Count > 0 && chain.Count < 16)
    {
      var n = queue.Dequeue();
      if (!seen.Add(n) || !_classes.TryGetValue(n, out var c)) continue;
      chain.Add(c);
      if (c.Parent is { } p) queue.Enqueue(p);
      foreach (var rp in c.ResourceParents) queue.Enqueue(rp);
    }

    return chain;
  }

  public string? TplOf(string name) => Chain(name).Select(c => c.Tpl).FirstOrDefault(t => t is { Length: > 0 });
  public string? BundleOf(string name) => Chain(name).Select(c => c.Bundle).FirstOrDefault(t => t is { Length: > 0 });
  public bool IsWeapon(string name) => Chain(name) is { Count: > 0 } chain && (chain[0].IsWeaponPath || chain.Any(c => c.HasEquipment)) && BundleOf(name) is null;
  public bool IsCharacter(string name) => BundleOf(name) is { Length: > 0 } && TplOf(name) is { Length: > 0 };

  /// <summary>Lazily parsed class body (sfx.list, inventory, holds, inline blueprints).</summary>
  public ClassDetail Detail(string name)
  {
    if (!_classes.TryGetValue(name, out var cls)) return ClassDetail.Empty;
    lock (cls)
    {
      if (cls.DetailCache is { } d) return d;
      JsonObject? root = null;
      try
      {
        if (_read(cls.Path) is { } text) root = SslText.Parse(text);
      }
      catch { root = null; }

      return cls.DetailCache = root is null ? ClassDetail.Empty : ClassDetail.From(root);
    }
  }

  /// <summary>sfx.list key → effect for the class and its ancestors (nearest wins). Keys without <c>__type</c> are the effect name.</summary>
  public Dictionary<string, string> SfxKeys(string name)
  {
    var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var c in Chain(name))
      foreach (var (k, v) in Detail(c.Name).Sfx)
        keys.TryAdd(k, v);
    return keys;
  }

  /// <summary>mtl1-53: the raw <c>sfx.list</c> entry for <paramref name="key"/> on the class chain (nearest wins), or null.</summary>
  public System.Text.Json.Nodes.JsonObject? SfxEntry(string name, string key)
  {
    foreach (var c in Chain(name))
      if (Detail(c.Name).SfxEntries.TryGetValue(key, out var e)) return e;
    return null;
  }

  /// <summary>
  ///   propanim: the class's <c>anim.layerList</c> (nearest class in the chain that has one): which embedded sequences
  ///   play on which object subtrees (<c>MainMultiStitch</c> AnimList + RootObjectNameList). DATA.
  /// </summary>
  public IReadOnlyList<LibSaber.SM2.Assets.Animation.PropAnimLayer> AnimLayersOf(string name)
  {
    foreach (var c in Chain(name))
      if (Detail(c.Name).AnimLayers is { Count: > 0 } layers)
        return layers;
    return [];
  }

  /// <summary>propanim: <c>shootAnimation</c> set on the class or an ancestor (firearm animation property), else null.</summary>
  public string? ShootAnimationOf(string name) => Chain(name).Select(c => Detail(c.Name).ShootAnimation).FirstOrDefault(s => s is { Length: > 0 });

  /// <summary>Blueprint names the class (and ancestors) run: cooked <c>linksDesc</c> .sso plus inline <c>prop_blueprints</c> (as <c>cls:Class/Name</c>).</summary>
  public HashSet<string> BlueprintsOf(string name)
  {
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var c in Chain(name))
    {
      set.UnionWith(c.Blueprints);
      foreach (var (bp, _) in Detail(c.Name).InlineBlueprints) set.Add($"cls:{c.Name}/{bp}");
    }

    return set;
  }

  /// <summary>Registers the inline blueprints of the class chain on <paramref name="map"/> (once per class).</summary>
  public void AddInlineBlueprints(string name, AnimEventVfxMap map)
  {
    foreach (var c in Chain(name))
      foreach (var (bp, nodes) in Detail(c.Name).InlineBlueprints)
      {
        var source = $"cls:{c.Name}/{bp}";
        lock (map)
        {
          if (map.HasSource(source)) continue;
          map.AddNodes(source, nodes);
        }
      }
  }
}

public sealed class ContextClass
{
  public required string Name { get; init; }
  public required string Path { get; init; }
  public string? Tpl { get; init; }
  public string? Bundle { get; init; }
  public string? Parent { get; init; }
  public bool IsWeaponPath { get; init; }
  public bool HasEquipment { get; init; }
  public List<string> ResourceParents { get; } = [];
  public HashSet<string> Blueprints { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> Effects { get; } = new(StringComparer.OrdinalIgnoreCase);
  internal ClassDetail? DetailCache { get; set; }
  public override string ToString() => Name;
}

/// <summary>Parsed parts of a class body that context playback needs.</summary>
public sealed class ClassDetail
{
  public static readonly ClassDetail Empty = new();
  /// <summary>sfx.list key → effect name (<c>__type</c>, else the key).</summary>
  public Dictionary<string, string> Sfx { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>mtl1-53: the <c>sfx.list</c> entries themselves (overrides such as <c>trailParams.timeLifeSeg</c>, <c>visuals.colorIntensity</c>).</summary>
  public Dictionary<string, JsonObject> SfxEntries { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary><c>prop_inventory*.localCreatedActorsDescs.*.type.__type</c>: weapon classes the character creates.</summary>
  public List<string> WeaponTypes { get; } = [];
  /// <summary>Weapon uids: <c>inventoryUids</c>, <c>prop_weapon_dummy_user.weaponUid.*WeaponUid</c>, inventory desc keys.</summary>
  public List<string> WeaponUids { get; } = [];
  /// <summary><c>prop_model_weapon_holds</c> / <c>prop_weapon_dummy_user.locatorName</c>.</summary>
  public string? RightHold { get; set; }
  public string? LeftHold { get; set; }
  public string? DummyLocator { get; set; }
  /// <summary><c>prop_weapon_dummy_drop.animationEvents</c>: drop event name → slot (<c>right_hand</c>/<c>left_hand</c>).</summary>
  public Dictionary<string, string> DropSlots { get; } = new(StringComparer.OrdinalIgnoreCase);
  public string? EquipCategory { get; set; }
  public List<(string Name, JsonObject Root)> InlineBlueprints { get; } = [];
  /// <summary>propanim: <c>anim.layerList</c> entries.</summary>
  public List<LibSaber.SM2.Assets.Animation.PropAnimLayer> AnimLayers { get; } = [];
  /// <summary>propanim: a <c>shootAnimation</c> string in a property (firearm animation controller override).</summary>
  public string? ShootAnimation { get; set; }

  public static ClassDetail From(JsonObject root)
  {
    var d = new ClassDetail();
    var props = root.Obj("properties") ?? root;
    if (props.Obj("sfx")?.Obj("list") is { } list)
      foreach (var (key, value) in list)
      {
        d.Sfx[key] = value.Str("__type") is { Length: > 0 } t ? t : key;
        if (value is JsonObject entry) d.SfxEntries[key] = entry;
      }
    foreach (var (key, value) in props)
    {
      if (value is not JsonObject prop) continue;
      if (key.StartsWith("prop_inventory", StringComparison.OrdinalIgnoreCase) && prop.Obj("localCreatedActorsDescs") is { } descs)
        foreach (var (uid, desc) in descs)
        {
          d.WeaponUids.Add(uid);
          if (desc.Obj("type")?.Str("__type") is { Length: > 0 } t) d.WeaponTypes.Add(t);
        }
    }

    if (props.Obj("prop_equipment_controllers")?.Obj("controllers") is { } ctrls)
      foreach (var (_, c) in ctrls)
        if (c?["inventoryUids"] is JsonArray uids)
          foreach (var u in uids)
            if (u is JsonValue v && v.TryGetValue<string>(out var s)) d.WeaponUids.Add(s);
    if (props.Obj("prop_model_weapon_holds") is { } holds)
    {
      d.RightHold = holds.Str("rightHandHold");
      d.LeftHold = holds.Str("leftHandHold");
    }

    if (props.Obj("prop_weapon_dummy_user") is { } dummy)
    {
      d.DummyLocator = dummy.Str("locatorName");
      if (dummy.Obj("weaponUid") is { } wu)
        foreach (var (k, v) in wu)
          if (k.EndsWith("WeaponUid", StringComparison.OrdinalIgnoreCase) && v is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Length > 0)
            d.WeaponUids.Add(s);
    }

    if (props.Obj("prop_weapon_dummy_drop")?.Obj("weaponDummyDropController")?["animationEvents"] is JsonArray events)
      foreach (var e in events)
        if (e.Obj("mrEvent")?.Str("name") is { Length: > 0 } ev && e.Str("slot") is { Length: > 0 } slot)
          d.DropSlots[ev] = slot;
    d.EquipCategory = props.Obj("prop_equipment")?.Str("loadoutItemCategoryUid");
    if (props.Obj("anim")?.Obj("layerList") is { } layerList)
      foreach (var (layerName, layer) in layerList)
        if (layer is JsonObject lo)
          d.AnimLayers.Add(new LibSaber.SM2.Assets.Animation.PropAnimLayer(layerName, lo.Str("type") ?? "",
            ArrayStrings(lo["AnimList"]), ArrayStrings(lo["RootObjectNameList"])));
    foreach (var (_, value) in props)
      if (value is JsonObject prop && FindString(prop, "shootAnimation", 3) is { Length: > 0 } shoot)
      {
        d.ShootAnimation = shoot;
        break;
      }

    if (props.Obj("prop_blueprints")?.Obj("blueprints") is { } bps)
      foreach (var (name, bp) in bps)
        if (bp is JsonObject o && o.Obj("nodes") is not null)
          d.InlineBlueprints.Add((name, o));
    return d;
  }

  private static List<string> ArrayStrings(JsonNode? node)
  {
    var list = new List<string>();
    if (node is JsonArray a)
      foreach (var v in a)
        if (v is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Length > 0) list.Add(s);
    return list;
  }

  private static string? FindString(JsonObject obj, string key, int depth)
  {
    if (obj.Str(key) is { Length: > 0 } s) return s;
    if (depth <= 0) return null;
    foreach (var (_, v) in obj)
      if (v is JsonObject child && FindString(child, key, depth - 1) is { } found)
        return found;
    return null;
  }
}
