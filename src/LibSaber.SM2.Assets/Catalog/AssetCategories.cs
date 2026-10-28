using System.Runtime.CompilerServices;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Catalog;

/// <summary>A node of the Category view (ui-43). Files are catalog indices; the UI lists the whole subtree.</summary>
public sealed class CategoryNode
{
  private readonly List<int> _direct = new();
  private int[]? _all;

  public CategoryNode(string name, string key, bool heuristic = false, string? note = null)
  {
    Name = name;
    Key = key;
    Heuristic = heuristic;
    Note = note;
  }

  /// <summary>Display name ("Tyranids").</summary>
  public string Name { get; }

  /// <summary>Stable path-like key ("Models/Tyranids") used to persist the selection.</summary>
  public string Key { get; }

  /// <summary>True when some membership comes from file/class NAMES (keywords, prefixes, suffixes) instead of data links.</summary>
  public bool Heuristic { get; internal set; }

  /// <summary>Where the grouping comes from (shown as a tooltip).</summary>
  public string? Note { get; set; }

  public List<CategoryNode> Children { get; } = new();

  /// <summary>Files placed directly in this node.</summary>
  public IReadOnlyList<int> Files => _direct;

  /// <summary>Distinct files of the whole subtree, sorted by catalog index.</summary>
  public int[] AllFiles => _all ??= Collect();

  public int TotalCount => AllFiles.Length;

  internal void Add(int index) => _direct.Add(index);

  internal CategoryNode Child(string name, bool heuristic = false, string? note = null)
  {
    foreach (var child in Children)
      if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
      {
        child.Heuristic |= heuristic;
        if (note is not null && child.Note is null)
          child.Note = note;
        return child;
      }
    var node = new CategoryNode(name, Key.Length == 0 ? name : Key + "/" + name, heuristic, note);
    Children.Add(node);
    return node;
  }

  public CategoryNode? Find(string key)
  {
    if (string.Equals(Key, key, StringComparison.OrdinalIgnoreCase))
      return this;
    foreach (var child in Children)
      if (key.StartsWith(child.Key, StringComparison.OrdinalIgnoreCase) && child.Find(key) is { } hit)
        return hit;
    return null;
  }

  public IEnumerable<CategoryNode> Descendants()
  {
    foreach (var child in Children)
    {
      yield return child;
      foreach (var d in child.Descendants())
        yield return d;
    }
  }

  private int[] Collect()
  {
    var set = new HashSet<int>(_direct);
    foreach (var child in Children)
      set.UnionWith(child.AllFiles);
    var result = set.ToArray();
    Array.Sort(result);
    return result;
  }

  /// <summary>Drops empty nodes and orders children (fixed order first, then by name).</summary>
  internal void Prune(IReadOnlyList<string>? order = null)
  {
    Children.RemoveAll(c => c.TotalCount == 0);
    Children.Sort((a, b) =>
    {
      // "Other ..." buckets always last.
      var oa = a.Name.StartsWith("Other", StringComparison.Ordinal);
      var ob = b.Name.StartsWith("Other", StringComparison.Ordinal);
      if (oa != ob)
        return oa ? 1 : -1;
      var ia = order is null ? -1 : IndexOf(order, a.Name);
      var ib = order is null ? -1 : IndexOf(order, b.Name);
      if (ia != ib)
        return (ia < 0 ? int.MaxValue : ia).CompareTo(ib < 0 ? int.MaxValue : ib);
      return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    });
    foreach (var child in Children)
      child.Prune(order);
  }

  private static int IndexOf(IReadOnlyList<string> list, string name)
  {
    for (var i = 0; i < list.Count; i++)
      if (list[i] == name)
        return i;
    return -1;
  }
}

/// <summary>The Category tree: <see cref="All"/>, kind groups, and <see cref="Other"/>; plus why each file sits where it does.</summary>
public sealed class CategoryTree
{
  internal CategoryTree(CategoryNode root, CategoryNode all, Dictionary<int, string> reasons, Dictionary<int, int>? owners = null)
  {
    Root = root;
    All = all;
    _reasons = reasons;
    _owners = owners ?? new Dictionary<int, int>();
  }

  private readonly Dictionary<int, string> _reasons;
  private readonly Dictionary<int, int> _owners;

  /// <summary>
  ///   A model (.tpl file index) that can play <paramref name="clipIndex"/>, for thumbnails: the best-ranked template whose
  ///   class chain names a Morpheme bundle that links the clip (data); -1 when no bundle links it.
  /// </summary>
  public int OwnerModel(int clipIndex) => _owners.TryGetValue(clipIndex, out var m) ? m : -1;

  /// <summary>Invisible root; its children are the top-level nodes (All, Models, Animations, Effects, Textures, Other).</summary>
  public CategoryNode Root { get; }

  /// <summary>Every visualizable file (the default node).</summary>
  public CategoryNode All { get; }

  /// <summary>Why <paramref name="index"/> got its category ("class npc_hormagaunt_client → npc_client", "HEURISTIC: …").</summary>
  public string? Reason(int index) => _reasons.GetValueOrDefault(index);

  public CategoryNode? Find(string? key)
    => string.IsNullOrEmpty(key) ? null : Root.Find(key);
}

/// <summary>
///   ui-43: the Content-Browser "Categories" view. Groups every visualizable file (<see cref="AssetClassification.IsPreviewable"/>)
///   from game data where the data has a link, and by name otherwise (HEURISTIC, flagged on the node).
///   <list type="bullet">
///     <item><b>Models</b>: a template is classified by the <c>ssl/**/*.cls</c> classes whose <c>geom.nameTpl</c> names it (data),
///       walking the parent chain (<c>__type</c>) for the base family (npc_client, player_character_*, firearm_*,
///       wpn_melee_actor, pjl_*, scene_actor_client, ...) plus the class folder. The faction of an NPC (Tyranids, Chaos,
///       Humanoid, Space Marine) is a keyword match on that chain/path (HEURISTIC). Templates no class names fall back to
///       name prefixes (refrt_, dstr_, wpn_, cc_, ...; HEURISTIC).</item>
///     <item><b>Animations</b>: Morpheme bundle membership (<c>linksAnimation</c>, data), synced kills
///       (<c>animation/group_animations/&lt;a_vs_b&gt;</c>, folder), cinematics/interplay/lipsync (path rules already used by
///       the clip ranks), the rest by folder; plus "By action" from clip-name tokens (HEURISTIC).</item>
///     <item><b>Effects</b>: <c>ssl/sfx/&lt;group&gt;/&lt;sub&gt;</c> folders. <b>Textures</b>: UI / scenes by path, maps by
///       name suffix (_nm, _spec, _em, ...; HEURISTIC).</item>
///   </list>
///   RULE: no visualizable file is ever unreachable. <see cref="CategoryTree.All"/> lists everything, and each kind keeps
///   an "Other" bucket for files no rule placed (checked by the tests).
/// </summary>
public static class AssetCategories
{
  public readonly record struct FileEntry(int Index, string Path, string Extension);

  private static readonly ConditionalWeakTable<GameCatalog, CategoryTree> Cache = new();

  public static readonly IReadOnlyList<string> TopOrder = new[]
  {
    "All", "Models", "Animations", "Effects", "Textures", "Other",
    // model groups
    "Space Marine", "Humanoid", "Tyranids", "Chaos", "Other enemies", "Weapons", "Props & environment",
    // animation groups
    "By character (Morpheme bundle)", "Synced kills", "Cinematics", "Interplay", "Lipsync", "By folder", "By action (heuristic)",
    // texture groups
    "Albedo / colour", "Normal maps", "Specular", "Emissive", "Masks & detail", "UI", "Scenes",
  };

  /// <summary>Builds (once per catalog) from the archives: scans classes and Morpheme bundles.</summary>
  public static CategoryTree Build(GameCatalog catalog, ArchiveStore store)
  {
    lock (Cache)
      if (Cache.TryGetValue(catalog, out var cached))
        return cached;

    var files = new List<FileEntry>();
    for (var i = 0; i < catalog.FileCount; i++)
    {
      var f = catalog.GetFile(i);
      if (AssetClassification.IsPreviewable(f.Extension))
        files.Add(new FileEntry(i, f.Path, f.Extension));
    }

    var classes = CharacterAnimations.AllClasses(catalog, store).Values.ToList();
    var bundles = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
    var morpheme = catalog.Root.Children.FirstOrDefault(c => string.Equals(c.Name, "morpheme", StringComparison.OrdinalIgnoreCase));
    var bundleNames = new HashSet<string>(classes.Select(c => c.Bundle).Where(b => b is not null)!, StringComparer.OrdinalIgnoreCase);
    if (morpheme is not null)
      foreach (var child in morpheme.Children)
        bundleNames.Add(child.Name);
    foreach (var bundle in bundleNames)
    {
      var clips = CharacterAnimations.BundleClips(catalog, store, bundle);
      if (clips.Count > 0)
        bundles[bundle] = clips;
    }

    var tree = Build(files, classes, bundles);
    lock (Cache)
      Cache.AddOrUpdate(catalog, tree);
    return tree;
  }

  /// <summary>Pure builder (tests): <paramref name="files"/> are the visualizable files to place.</summary>
  public static CategoryTree Build(
    IReadOnlyList<FileEntry> files,
    IReadOnlyCollection<CharacterAnimations.ClassInfo> classes,
    IReadOnlyDictionary<string, IReadOnlySet<string>> bundleClips)
  {
    var root = new CategoryNode("", "");
    var all = root.Child("All", note: "Every visualizable file (textures, models, clips, effects).");
    var models = root.Child("Models", note: "Templates (.tpl). Data: ssl/**/*.cls geom.nameTpl + parent chain; faction keywords and name prefixes are HEURISTIC.");
    var anims = root.Child("Animations", note: "Clips (.sani). Data: Morpheme bundle linksAnimation; folders; 'By action' is HEURISTIC.");
    var effects = root.Child("Effects", note: "Effects (.sfx) by ssl/sfx folder.");
    var textures = root.Child("Textures", note: "Textures (.pct). UI/Scenes by path; map types by name suffix (HEURISTIC).");
    var other = root.Child("Other", note: "Visualizable files no rule placed.");
    var reasons = new Dictionary<int, string>();

    var byName = new Dictionary<string, CharacterAnimations.ClassInfo>(StringComparer.OrdinalIgnoreCase);
    foreach (var c in classes)
      byName.TryAdd(c.Name, c);
    var tplClasses = new Dictionary<string, List<CharacterAnimations.ClassInfo>>(StringComparer.OrdinalIgnoreCase);
    foreach (var c in classes)
      if (c.Tpl is { Length: > 0 } tpl)
      {
        if (!tplClasses.TryGetValue(tpl, out var list))
          tplClasses[tpl] = list = new List<CharacterAnimations.ClassInfo>();
        list.Add(c);
      }

    // Bundle → faction (from the models whose classes use the bundle) for nicer labels.
    var bundleFaction = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var bundleOwner = new Dictionary<string, (int Rank, int Length, int Index)>(StringComparer.OrdinalIgnoreCase);

    foreach (var file in files)
    {
      all.Add(file.Index);
      var placed = file.Extension switch
      {
        ".tpl" => PlaceModel(file, models, tplClasses, byName, reasons, bundleFaction, bundleOwner),
        ".sani" => false, // second pass (needs bundle factions)
        ".sfx" => PlaceEffect(file, effects, reasons),
        ".pct" or ".pct.resource" => PlaceTexture(file, textures, reasons),
        _ => false,
      };
      if (!placed && file.Extension != ".sani")
      {
        other.Add(file.Index);
        reasons.TryAdd(file.Index, "no rule matched");
      }
    }

    var clipBundles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    foreach (var (bundle, clips) in bundleClips)
      foreach (var clip in clips)
      {
        if (!clipBundles.TryGetValue(clip, out var list))
          clipBundles[clip] = list = new List<string>();
        list.Add(bundle);
      }

    var bundleSizes = bundleClips.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.OrdinalIgnoreCase);
    var owners = new Dictionary<int, int>();
    foreach (var file in files)
    {
      if (file.Extension != ".sani")
        continue;
      if (clipBundles.TryGetValue(file.Path.Replace('\\', '/'), out var cb))
      {
        (int Rank, int Length, int Index)? pick = null;
        foreach (var b in cb)
          if (bundleOwner.TryGetValue(b, out var o) && (pick is null || o.CompareTo(pick.Value) < 0))
            pick = o;
        if (pick is { } p)
          owners[file.Index] = p.Index;
      }
      if (!PlaceAnimation(file, anims, clipBundles, bundleFaction, reasons, bundleSizes))
      {
        anims.Child("Other").Add(file.Index);
        reasons.TryAdd(file.Index, "no rule matched");
      }
    }

    // RULE: everything visualizable is reachable from its kind node or Other (All has everything anyway).
    var reachable = new HashSet<int>();
    foreach (var top in new[] { models, anims, effects, textures, other })
      reachable.UnionWith(top.AllFiles);
    foreach (var file in files)
      if (!reachable.Contains(file.Index))
        other.Add(file.Index);

    root.Prune(TopOrder);
    if (!root.Children.Contains(all))
      root.Children.Insert(0, all);
    return new CategoryTree(root, all, reasons, owners);
  }

  // ---------------------------------------------------------------- models

  private static readonly string[] TyranidWords =
  {
    "tyranid", "hormagaunt", "termagant", "gaunt", "lictor", "ravener", "zoanthrope", "neurothrope", "carnifex", "biovore",
    "hive_tyrant", "hivetyrant", "hierophant", "trygon", "spore", "acid_mine", "ripper", "tyranid_warrior", "warrior_", "_prime",
    "gargoyle", "sporocyst", "biotitan", "harpy", "genestealer", "mawloc", "exocrine", "tyrannofex", "pyrovore", "venomthrope",
  };

  private static readonly string[] HumanoidWords =
  {
    "guardsman", "guard_", "commissar", "sarkanna", "cultist", "tech_priest", "techpriest", "servitor", "astropath", "crew",
    "cherub", "cadian", "trooper", "human", "militia", "civilian", "governor", "adept", "inquisitor", "leuz", "morias", "skitarii",
  };

  private static readonly string[] ChaosWords =
  {
    "chaos", "sorcerer", "rubric", "tzaangor", "lord_of_change", "lordofchange", "helbrute", "hellbrute", "occult", "scarab",
    "nereph", "spawn", "vortex_beast", "heldrake", "daemon", "demon", "pink_horror", "horror", "flamer", "screamer", "traitor",
    "thousand_sons", "imurah", "warp",
  };

  private static readonly string[] MarineWords =
  {
    "marine", "titus", "calgar", "chaplain", "acheran", "varellus", "chairon", "gadriel", "deathwatch", "primaris", "astartes",
    "intercessor", "assault", "tacticus", "dreadnought", "terminator", "librarian", "apothecary", "ultramarine", "blood_angel",
    "dark_angel", "space_wolf", "pc_marine", "companion", "_sm_", "cc_sm", "champion", "constructor",
  };

  private static bool Has(string text, string[] words)
  {
    foreach (var w in words)
      if (text.Contains(w, StringComparison.Ordinal))
        return true;
    return false;
  }

  private static List<string> Chain(CharacterAnimations.ClassInfo start, IReadOnlyDictionary<string, CharacterAnimations.ClassInfo> byName)
  {
    var chain = new List<string>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var name = start.Name;
    var node = start;
    while (seen.Add(name))
    {
      chain.Add(name.ToLowerInvariant());
      var parent = node?.Parent;
      if (parent is null)
        break;
      name = parent;
      node = byName.GetValueOrDefault(parent);
    }

    return chain;
  }

  private static (string[] Path, bool Heuristic, string Why)? ClassifyClass(
    CharacterAnimations.ClassInfo info, IReadOnlyDictionary<string, CharacterAnimations.ClassInfo> byName, string stem)
  {
    var chain = Chain(info, byName);
    var folder = info.Path.Replace('\\', '/').ToLowerInvariant();
    var text = string.Join("|", chain) + "|" + folder + "|" + stem.ToLowerInvariant();
    var why = $"class {info.Name}" + (chain.Count > 1 ? " → " + string.Join(" → ", chain.Skip(1)) : "");
    bool InChain(Func<string, bool> pred) => chain.Any(pred);

    // Weapons: firearm_base/firearm_client family, melee actors, projectiles, grenades, shields, or the weapons class folder.
    if (InChain(c => c.StartsWith("firearm", StringComparison.Ordinal)))
    {
      // Family = class-name prefix below firearm_client (arifle, hgun, brifle, smg, hwpn, pwpn, shotgun, aux, mounted, ...).
      var family = chain.Select(c => c.Split('_')[0]).FirstOrDefault(t => t is not ("firearm" or "")) ?? "firearm";
      return (new[] { "Weapons", "Firearms", family }, false, why);
    }

    if (InChain(c => c.StartsWith("wpn_melee", StringComparison.Ordinal) || c.Contains("melee_actor")))
      return (new[] { "Weapons", "Melee" }, false, why);
    if (InChain(c => c.StartsWith("pjl_", StringComparison.Ordinal)))
      return (new[] { "Weapons", "Projectiles" }, false, why);
    if (InChain(c => c.Contains("grenade")))
      return (new[] { "Weapons", "Grenades" }, false, why);
    if (InChain(c => c.Contains("combat_shield") || c.Contains("shield")))
      return (new[] { "Weapons", "Shields" }, false, why);

    // Space Marines driven by players / AI squad mates (class chain, data).
    if (InChain(c => c.StartsWith("player_character", StringComparison.Ordinal) || c.StartsWith("pc_marine", StringComparison.Ordinal)))
      return Has(text, ChaosWords) && !Has(text, new[] { "pc_marine" })
        ? (new[] { "Chaos", "Player" }, true, why + " (chaos keyword HEURISTIC)")
        : (new[] { "Space Marine", "Player" }, false, why);
    if (InChain(c => c.Contains("bot_marine")))
      return (new[] { "Space Marine", "AI squad" }, false, why);

    if (InChain(c => c.StartsWith("scene_actor", StringComparison.Ordinal) || c.Contains("door_") || c.Contains("lootable") || c.Contains("trap_")))
    {
      if (Has(text, ChaosWords) && InChain(c => c.Contains("heldrake")))
        return (new[] { "Chaos", "Scene actors" }, true, why + " (keyword HEURISTIC)");
      return (new[] { "Props & environment", "Scene actors" }, false, why);
    }

    var npc = InChain(c => c.StartsWith("npc", StringComparison.Ordinal) || c.Contains("interplay_npc"));
    if (InChain(c => c.Contains("librarium_character_dummy")))
      return (new[] { "Props & environment", "Librarium models" }, false, why);
    var doll = InChain(c => c.Contains("character_doll"));
    var corpse = InChain(c => c.Contains("ragdoll_corpse"));
    var s = stem.ToLowerInvariant();
    var characterName = s.StartsWith("cc_", StringComparison.Ordinal) || s.StartsWith("cine_", StringComparison.Ordinal);
    if (!npc && !doll && !corpse && !characterName && !InChain(c => c.StartsWith("cinematic_character", StringComparison.Ordinal)))
      return null; // an object/actor class: props (or a name prefix) decide
    var sub = InChain(c => c.StartsWith("npc", StringComparison.Ordinal)) ? "NPC"
      : doll ? "Menu & dolls"
      : s.StartsWith("cine_", StringComparison.Ordinal) || folder.Contains("cinematic") || InChain(c => c.StartsWith("cinematic", StringComparison.Ordinal)) ? "Cinematic"
      : npc ? "Interplay actors"
      : corpse ? "Corpses & gore"
      : "Other";
    if (Has(text, TyranidWords) || folder.Contains("/tyranid"))
      return (new[] { "Tyranids", sub }, true, why + " (faction keyword HEURISTIC)");
    if (Has(text, HumanoidWords))
      return (new[] { "Humanoid", sub }, true, why + " (faction keyword HEURISTIC)");
    if (Has(text, ChaosWords) || folder.Contains("/chaos/"))
      return (new[] { "Chaos", sub }, true, why + " (faction keyword HEURISTIC)");
    if (Has(text, MarineWords))
      return (new[] { "Space Marine", sub }, true, why + " (faction keyword HEURISTIC)");
    if (folder.Contains("/imperium/"))
      return (new[] { "Humanoid", sub }, true, why + " (class folder imperium)");
    if (sub == "NPC")
      return (new[] { "Other enemies" }, false, why);
    return (new[] { "Other", "Characters (unclassed)" }, false, why);
  }

  /// <summary>Lower = better owner class for a template (gameplay character over menu doll over cinematic over props).</summary>
  private static int Rank((string[] Path, bool Heuristic, string Why)? r)
  {
    if (r is null)
      return 9;
    var p = r.Value.Path;
    var sub = p.Length > 1 ? p[1] : "";
    if (p[0] == "Weapons")
      return 1;
    if (p[0] == "Props & environment")
      return 6;
    if (p[0] == "Other")
      return 8;
    return sub switch
    {
      "Player" or "AI squad" or "NPC" => 0,
      "Menu & dolls" => 2,
      "Interplay actors" => 3,
      "Cinematic" => 4,
      _ => 5,
    };
  }

  private static bool PlaceModel(
    FileEntry file, CategoryNode models,
    Dictionary<string, List<CharacterAnimations.ClassInfo>> tplClasses,
    IReadOnlyDictionary<string, CharacterAnimations.ClassInfo> byName,
    Dictionary<int, string> reasons,
    Dictionary<string, string> bundleFaction,
    Dictionary<string, (int Rank, int Length, int Index)> bundleOwner)
  {
    var stem = AssetClassification.BaseName(file.Path);
    (string[] Path, bool Heuristic, string Why)? best = null;
    if (tplClasses.TryGetValue(stem, out var owners))
    {
      foreach (var info in owners.OrderBy(o => o.Path, StringComparer.Ordinal))
      {
        var r = ClassifyClass(info, byName, stem);
        if (r is null)
          continue;
        // Prefer the most "character" owner, then a data (non-heuristic) result, then the first.
        if (best is null || Rank(r) < Rank(best) || (Rank(r) == Rank(best) && best.Value.Heuristic && !r.Value.Heuristic))
          best = r;
        // Remember bundle factions for the animation labels.
        for (var node = info; node is not null; node = node.Parent is null ? null : byName.GetValueOrDefault(node.Parent))
        {
          if (node.Bundle is { } b)
          {
            bundleFaction.TryAdd(b, r.Value.Path[0]);
            var cand = (Rank(r), stem.Length, file.Index);
            if (!bundleOwner.TryGetValue(b, out var have) || cand.CompareTo(have) < 0)
              bundleOwner[b] = cand;
            break;
          }

          if (node.Parent is not null && string.Equals(node.Parent, node.Name, StringComparison.OrdinalIgnoreCase))
            break;
        }
      }

      if (best is null)
      {
        var named = ClassifyByName(file.Path, stem);
        best = named.Path[^1] == "Other (no class)"
          ? (new[] { "Props & environment", "Classed actors" }, false, $"class {owners[0].Name} (actor class, no character/weapon family)")
          : (named.Path, true, $"class {owners[0].Name}; " + named.Why);
      }
    }

    best ??= ClassifyByName(file.Path, stem);
    var target = models;
    var heur = best.Value.Heuristic;
    foreach (var part in best.Value.Path)
      target = target.Child(part, heur, heur ? "Contains HEURISTIC (name/keyword) placements" : null);
    target.Add(file.Index);
    reasons[file.Index] = best.Value.Why;
    return true;
  }

  private static (string[] Path, bool Heuristic, string Why) ClassifyByName(string path, string stem)
  {
    var s = stem.ToLowerInvariant();
    var p = path.Replace('\\', '/').ToLowerInvariant();
    const string H = "HEURISTIC: no class names this template; ";
    if (p.StartsWith("scenes/", StringComparison.Ordinal))
    {
      var scn = p.Split('/')[1];
      return (new[] { "Props & environment", "Scenes", scn.Replace(".scn", "") }, false, "no class names this template; scene folder " + scn);
    }

    if (s.StartsWith("refrt_", StringComparison.Ordinal))
      return (new[] { "Props & environment", "Level geometry (refrt_)" }, true, H + "prefix refrt_");
    if (s.StartsWith("dstr_", StringComparison.Ordinal))
      return (new[] { "Props & environment", "Destructibles (dstr_)" }, true, H + "prefix dstr_");
    if (s.StartsWith("phys_", StringComparison.Ordinal))
      return (new[] { "Props & environment", "Physics (phys_)" }, true, H + "prefix phys_");
    if (s.StartsWith("fx_", StringComparison.Ordinal) || s.StartsWith("sfx_", StringComparison.Ordinal))
      return (new[] { "Props & environment", "FX meshes" }, true, H + "prefix fx_/sfx_");
    if (s.StartsWith("wpn_", StringComparison.Ordinal))
      return (new[] { "Weapons", "Unclassed (wpn_)" }, true, H + "prefix wpn_");
    if (s.StartsWith("cc_", StringComparison.Ordinal) || s.StartsWith("cine_", StringComparison.Ordinal) || s.StartsWith("ch_", StringComparison.Ordinal))
    {
      var sub = s.StartsWith("cine_", StringComparison.Ordinal) ? "Cinematic" : "Unclassed";
      if (Has(s, TyranidWords)) return (new[] { "Tyranids", sub }, true, H + "keyword");
      if (Has(s, HumanoidWords)) return (new[] { "Humanoid", sub }, true, H + "keyword");
      if (Has(s, ChaosWords)) return (new[] { "Chaos", sub }, true, H + "keyword");
      if (Has(s, MarineWords)) return (new[] { "Space Marine", sub }, true, H + "keyword");
      return (new[] { "Other", "Characters (unclassed)" }, true, H + "prefix cc_/cine_");
    }

    return (new[] { "Props & environment", "Other (no class)" }, true, H + "default props bucket");
  }

  // ---------------------------------------------------------------- animations

  private static readonly (string Group, string[] Words)[] ActionRules =
  {
    ("Synced kills & finishers", new[] { "_vs_", "finisher", "execution", "execute", "gore", "brutal" }),
    ("Deaths", new[] { "death", "die_", "_die", "dead", "dying" }),
    ("Hit reactions", new[] { "hit", "stagger", "knockback", "knockdown", "stun", "react", "flinch", "pain", "impact" }),
    ("Melee", new[] { "melee", "attack", "swing", "slash", "combo", "parry", "block", "strike", "bash", "kick", "punch", "charge" }),
    ("Shooting", new[] { "shoot", "fire", "aim", "reload", "recoil", "_ads", "overheat", "burst" }),
    ("Evade", new[] { "dodge", "roll", "evade", "dash" }),
    ("Traversal", new[] { "jump", "fall", "land", "climb", "vault", "ledge", "ladder", "jetpack", "fly", "leap" }),
    ("Locomotion", new[] { "walk", "run", "sprint", "jog", "strafe", "turn", "start", "stop", "move", "loco" }),
    ("Idles & poses", new[] { "idle", "pose", "stand", "breath", "wait" }),
    ("Emotes & gestures", new[] { "emote", "gesture", "taunt", "salute", "cheer", "talk" }),
    ("Facial & lipsync", new[] { "lipsync", "face", "phoneme", "facial" }),
  };

  /// <summary>HEURISTIC: action group from the clip file name (first matching rule), or null.</summary>
  public static string? ActionGroup(string path)
  {
    var stem = AssetClassification.BaseName(path).ToLowerInvariant();
    foreach (var (group, words) in ActionRules)
      foreach (var w in words)
        if (stem.Contains(w, StringComparison.Ordinal))
          return group;
    return null;
  }

  private static bool PlaceAnimation(
    FileEntry file, CategoryNode anims,
    Dictionary<string, List<string>> clipBundles,
    Dictionary<string, string> bundleFaction,
    Dictionary<int, string> reasons,
    Dictionary<string, int> bundleSizes)
  {
    var path = file.Path.Replace('\\', '/');
    var lower = path.ToLowerInvariant();
    var parts = lower.Split('/');
    var placed = false;
    var why = new List<string>();

    if (clipBundles.TryGetValue(path, out var bundles))
    {
      foreach (var bundle in bundles)
      {
        var label = bundleFaction.TryGetValue(bundle, out var faction) ? $"{bundle} · {faction}" : bundle;
        var node = anims.Child("By character (Morpheme bundle)", note: "Clips a Morpheme network links (morpheme/<bundle>/<bundle>.mr.resource linksAnimation; data).")
          .Child(label);
        // Big networks (primaris links 1.8k clips) split by folder: animation/<set>/<class>/..., synced kills by pair.
        if (bundleSizes.GetValueOrDefault(bundle) > 200 && parts.Length > 3)
        {
          var g = Array.IndexOf(parts, "group_animations");
          node = g >= 0 && g + 1 < parts.Length - 1
            ? node.Child("Synced kills").Child(parts[g + 1])
            : node.Child(parts[2]);
          if (g < 0 && parts.Length > 4)
            node = node.Child(parts[3]);
        }

        node.Add(file.Index);
      }

      why.Add("Morpheme bundle " + string.Join(", ", bundles));
      placed = true;
    }

    var group = Array.IndexOf(parts, "group_animations");
    if (group >= 0 && group + 1 < parts.Length - 1)
    {
      anims.Child("Synced kills", note: "animation/group_animations/<a_vs_b> (folder).").Child(parts[group + 1]).Add(file.Index);
      why.Add("group_animations/" + parts[group + 1]);
      placed = true;
    }
    else if (lower.Contains("_vs_", StringComparison.Ordinal))
    {
      anims.Child("Synced kills").Child("Other _vs_ clips", heuristic: true).Add(file.Index);
      why.Add("HEURISTIC: _vs_ in name");
      placed = true;
    }

    if (CharacterAnimations.IsLipsync(lower))
    {
      anims.Child("Lipsync").Add(file.Index);
      placed = true;
    }
    else if (lower.Contains("/interplay/", StringComparison.Ordinal))
    {
      anims.Child("Interplay").Child(parts.Length > 3 ? FolderAfter(parts, "interplay") : "Other").Add(file.Index);
      placed = true;
    }
    else if (CharacterAnimations.IsCinematic(lower))
    {
      anims.Child("Cinematics").Child(FolderAfter(parts, "cinematics")).Add(file.Index);
      placed = true;
    }

    if (!placed)
    {
      // Unlinked gameplay clips: by folder (animation/<a>/<b>).
      var a = parts.Length > 2 ? parts[1] : "(root)";
      var node = anims.Child("By folder", note: "Clips no Morpheme bundle links, by animation/<group>/<set> folder.").Child(a);
      if (parts.Length > 3)
        node = node.Child(parts[2]);
      node.Add(file.Index);
      why.Add("folder " + string.Join("/", parts.Take(Math.Min(3, parts.Length - 1))));
      placed = true;
    }

    var action = CharacterAnimations.IsLipsync(lower) ? "Facial & lipsync" : ActionGroup(path) ?? "Other";
    anims.Child("By action (heuristic)", heuristic: true, note: "HEURISTIC: clip-name tokens (idle, run, attack, death, ...).").Child(action).Add(file.Index);
    reasons[file.Index] = string.Join("; ", why) + $"; action '{action}' (HEURISTIC)";
    return placed;
  }

  private static string FolderAfter(string[] parts, string marker)
  {
    var i = Array.IndexOf(parts, marker);
    return i >= 0 && i + 1 < parts.Length - 1 ? parts[i + 1] : "Other";
  }

  // ---------------------------------------------------------------- effects & textures

  private static bool PlaceEffect(FileEntry file, CategoryNode effects, Dictionary<int, string> reasons)
  {
    var parts = file.Path.Replace('\\', '/').ToLowerInvariant().Split('/');
    var sfx = Array.IndexOf(parts, "sfx");
    if (sfx >= 0 && sfx + 1 < parts.Length - 1)
    {
      var node = effects.Child(parts[sfx + 1]);
      if (sfx + 2 < parts.Length - 1)
        node = node.Child(parts[sfx + 2]);
      node.Add(file.Index);
      reasons[file.Index] = "folder " + string.Join("/", parts.Take(Math.Min(sfx + 3, parts.Length - 1)));
      return true;
    }

    effects.Child("Other").Add(file.Index);
    reasons[file.Index] = "outside ssl/sfx";
    return true;
  }

  private static bool PlaceTexture(FileEntry file, CategoryNode textures, Dictionary<int, string> reasons)
  {
    var p = file.Path.Replace('\\', '/').ToLowerInvariant();
    var stem = AssetClassification.BaseName(p);
    string group;
    bool heuristic;
    if (p.StartsWith("textures/ui/", StringComparison.Ordinal) || p.Contains("/ui/", StringComparison.Ordinal))
      (group, heuristic) = ("UI", false);
    else if (p.StartsWith("scenes/", StringComparison.Ordinal))
      (group, heuristic) = ("Scenes", false);
    else
    {
      heuristic = true;
      group = stem switch
      {
        _ when stem.EndsWith("_nm", StringComparison.Ordinal) || stem.Contains("_nm_", StringComparison.Ordinal) => "Normal maps",
        _ when stem.EndsWith("_spec", StringComparison.Ordinal) || stem.Contains("_spec_", StringComparison.Ordinal) => "Specular",
        _ when stem.EndsWith("_em", StringComparison.Ordinal) || stem.Contains("_em_", StringComparison.Ordinal) => "Emissive",
        _ when stem.EndsWith("_hdetm", StringComparison.Ordinal) || stem.EndsWith("_mask", StringComparison.Ordinal) || stem.EndsWith("_msk", StringComparison.Ordinal)
               || stem.EndsWith("_det", StringComparison.Ordinal) || stem.Contains("_mask_", StringComparison.Ordinal) || stem.EndsWith("_h", StringComparison.Ordinal) => "Masks & detail",
        _ when stem.StartsWith("fx_", StringComparison.Ordinal) => "FX",
        _ => "Albedo / colour",
      };
    }

    textures.Child(group, heuristic, heuristic ? "HEURISTIC: texture name suffix" : null).Add(file.Index);
    reasons[file.Index] = heuristic ? $"HEURISTIC: name '{stem}'" : "path";
    return true;
  }
}
