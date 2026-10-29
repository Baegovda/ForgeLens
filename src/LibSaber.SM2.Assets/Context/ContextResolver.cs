using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Context;

/// <summary>One way an effect plays in game: character + (held weapon) + the morpheme events that start it.</summary>
public sealed class EffectContext
{
  public required string Effect { get; init; }
  public required string Character { get; init; }
  public string? Tpl { get; init; }
  public string? Bundle { get; init; }
  public string? Weapon { get; init; }
  public string? WeaponTpl { get; init; }
  /// <summary>The class whose sfx.list / blueprint names the effect (character, weapon or a spawned actor).</summary>
  public required string Owner { get; init; }
  /// <summary>Morpheme events whose blueprint starts the effect (empty: no event link, clip found by name).</summary>
  public List<string> Events { get; } = [];
  /// <summary>Blueprint sources the character chain owns (the HostVfx source filter).</summary>
  public HashSet<string> Blueprints { get; } = new(StringComparer.OrdinalIgnoreCase);
  public List<string> Notes { get; } = [];
  /// <summary>True when every hop came from cooked links (resource / inventory / blueprint), no name guess.</summary>
  public bool Proven { get; set; }
  public int Depth { get; init; }
  public string Label => $"{Character}{(Weapon is null ? "" : " + " + Weapon)}{(Proven ? "" : " (HEURISTIC)")}";
  public override string ToString() => Label;
}

/// <summary>A weapon a model may hold for a clip.</summary>
public sealed record WeaponChoice(string Class, string Tpl, string Hold, bool Proven, string Why)
{
  public string Label => $"{Class}{(Proven ? "" : " (HEURISTIC)")}";
  public override string ToString() => Label;
}

/// <summary>An effect cue a clip track starts without a blueprint link (e.g. <c>Shoot</c> → the weapon's muzzle).</summary>
public sealed record TrackCue(string Track, string SlotKey, string Effect, string Source, bool Proven);

/// <summary>
///   mtl1-49 context playback rules over <see cref="ContextIndex"/>.
///   Forward (effect → model + clip): owners of the effect, walked up "who creates / inherits me" to a character
///   (a class with a template and a morpheme bundle); blueprint events of that chain that start the effect.
///   Reverse (model + clip → weapon + effects): the character's inventory, else clip-name tokens vs weapon class
///   names (HEURISTIC), held at <c>prop_model_weapon_holds</c> (GUN_R / GUN_L); a <c>Shoot</c> track starts the
///   weapon's <c>muzzle*</c> sfx.list entry (HEURISTIC: the firearm code fires on it, no blueprint names it).
/// </summary>
public sealed class ContextResolver
{
  private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
  {
    "client", "wpn", "melee", "firearm", "mgs", "fx", "vfx", "sfx", "npc", "base", "v01", "v02", "v03", "01", "02", "03",
    "the", "of", "cc", "tpl", "add1", "add2", "stand", "anim", "and", "a", "b", "l", "r", "scrab", "scarab", "loop",
    "start", "end", "idle", "shoot", "fire", "shot", "weapon", "weapons", "actors", "common", "characters", "animation",
  };

  private readonly ContextIndex _index;
  public ContextIndex Index => _index;

  public ContextResolver(ContextIndex index) => _index = index;

  /// <summary>Ranked contexts for an effect (best first). <paramref name="map"/> gets the inline blueprints of the chains.</summary>
  public List<EffectContext> ForEffect(string effect, AnimEventVfxMap map, int max = 12)
  {
    var (owners, fromResource) = _index.OwnersOf(effect);
    var results = new List<EffectContext>();
    var seenChars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var owner in owners.OrderBy(o => _index.IsCharacter(o) ? 0 : _index.IsWeapon(o) ? 1 : 2).ThenBy(o => o, StringComparer.OrdinalIgnoreCase))
    {
      // BFS up: children (inherit the sfx.list) and users (create / hold the class), to a character.
      var queue = new Queue<(string Cls, int Depth, string? Weapon, bool Proven)>();
      queue.Enqueue((owner, 0, null, fromResource));
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      while (queue.Count > 0 && results.Count < max * 3)
      {
        var (cls, depth, weapon, proven) = queue.Dequeue();
        if (!seen.Add(cls)) continue;
        var isChar = _index.IsCharacter(cls) && !_index.IsWeapon(cls);
        if (isChar)
        {
          if (cls.EndsWith("_authority", StringComparison.OrdinalIgnoreCase) || !seenChars.Add(cls)) continue;
          results.Add(Build(effect, cls, owner, weapon, depth, proven, map));
          // Derived characters (npc_x_client of npc_x_client_base) inherit the same link: list them too.
          if (depth < 4)
            foreach (var child in _index.ChildrenOf(cls)) queue.Enqueue((child, depth + 1, weapon, proven));
          continue;
        }

        if (depth >= 4) continue;
        var nextWeapon = weapon ?? (_index.IsWeapon(cls) ? cls : null);
        foreach (var child in _index.ChildrenOf(cls)) queue.Enqueue((child, depth + 1, nextWeapon, proven));
        foreach (var user in _index.UsersOf(cls)) queue.Enqueue((user, depth + 1, nextWeapon, proven));
      }
    }

    return results
      .OrderByDescending(r => r.Events.Count > 0)
      .ThenByDescending(r => r.Proven)
      .ThenBy(r => r.Character.EndsWith("_base", StringComparison.OrdinalIgnoreCase))
      .ThenBy(r => r.Character.StartsWith("auto_", StringComparison.OrdinalIgnoreCase))
      .ThenBy(r => r.Depth)
      .ThenBy(r => r.Character.Contains("clone", StringComparison.OrdinalIgnoreCase) || r.Character.Contains("mirror", StringComparison.OrdinalIgnoreCase))
      .ThenBy(r => r.Character.Length)
      .Take(max)
      .ToList();
  }

  private EffectContext Build(string effect, string character, string owner, string? weapon, int depth, bool proven, AnimEventVfxMap map)
  {
    var ctx = new EffectContext
    {
      Effect = effect,
      Character = character,
      Tpl = _index.TplOf(character),
      Bundle = _index.BundleOf(character),
      Weapon = weapon,
      WeaponTpl = weapon is null ? null : _index.TplOf(weapon),
      Owner = owner,
      Depth = depth,
      Proven = proven,
    };
    if (!proven) ctx.Notes.Add($"HEURISTIC: no class lists {effect} in its cooked links; {owner} names it in text.");
    _index.AddInlineBlueprints(character, map);
    if (weapon is not null) _index.AddInlineBlueprints(weapon, map);
    ctx.Blueprints.UnionWith(_index.BlueprintsOf(character));
    if (weapon is not null) ctx.Blueprints.UnionWith(_index.BlueprintsOf(weapon));
    var resolve = Resolver(character, weapon, map);
    foreach (var (ev, list) in map.Events)
      foreach (var v in list)
        if (OwnedSource(ctx.Blueprints, v.Source) && resolve(v.Effect).Equals(effect, StringComparison.OrdinalIgnoreCase))
        {
          if (!ctx.Events.Contains(ev, StringComparer.OrdinalIgnoreCase)) ctx.Events.Add(ev);
        }

    if (ctx.Events.Count == 0)
    {
      ctx.Proven = false;
      ctx.Notes.Add("HEURISTIC: no owned blueprint starts it on a morpheme event (ability / script driven); clip picked by name tokens.");
    }

    return ctx;
  }

  /// <summary>Owned blueprint test: <paramref name="source"/> is an archive path / name / <c>cls:</c> source, maybe " (bulk)".</summary>
  public static bool OwnedSource(IReadOnlySet<string> owned, string source)
  {
    if (owned.Count == 0) return true;
    var s = source.EndsWith(" (bulk)", StringComparison.Ordinal) ? source[..^7] : source;
    if (owned.Contains(s)) return true;
    var stem = Path.GetFileNameWithoutExtension(s.Replace('\\', '/'));
    return owned.Contains(stem);
  }

  /// <summary>sfx.list resolution for the weapon (first) and character chains, then the global map.</summary>
  public Func<string, string> Resolver(string? character, string? weapon, AnimEventVfxMap? map)
  {
    var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (weapon is not null)
      foreach (var (k, v) in _index.SfxKeys(weapon)) keys.TryAdd(k, v);
    if (character is not null)
      foreach (var (k, v) in _index.SfxKeys(character)) keys.TryAdd(k, v);
    return name => keys.TryGetValue(name, out var e) ? e : map?.Resolve(name) ?? name;
  }

  /// <summary>
  ///   mtl1-53: sfx.list entry for a blueprint name, weapon chain first then character (same order as <see cref="Resolver"/>).
  ///   The entry is a prototype override of its <c>__type</c> effect (wpn_melee_chainsword: trail_mele_01_yellow =
  ///   trail_melee_01_yellow with timeLifeSeg 0.15, colorIntensity 5 x 0.4), merged like sfx_group list entries (INFERRED).
  /// </summary>
  public Func<string, System.Text.Json.Nodes.JsonObject?> Overrides(string? character, string? weapon)
    => key => (weapon is null ? null : _index.SfxEntry(weapon, key)) ?? (character is null ? null : _index.SfxEntry(character, key));

  /// <summary>Is the effect started by the weapon's chain (so it goes on the weapon's sockets)?</summary>
  public bool WeaponOwns(string weapon, string effectOrKey)
  {
    var keys = _index.SfxKeys(weapon);
    return keys.ContainsKey(effectOrKey) || keys.Values.Contains(effectOrKey, StringComparer.OrdinalIgnoreCase);
  }

  /// <summary>propanim: any class whose template stem is <paramref name="tplStem"/> (weapons first, client side first).</summary>
  public List<string> ClassesForTpl(string tplStem)
  {
    var stem = Stem(tplStem);
    return _index.Classes.Values
      .Where(c => Stem(_index.TplOf(c.Name) ?? "").Equals(stem, StringComparison.OrdinalIgnoreCase))
      .Select(c => c.Name)
      .OrderBy(n => _index.IsWeapon(n) ? 0 : 1)
      .ThenBy(n => n.EndsWith("_authority", StringComparison.OrdinalIgnoreCase))
      .ThenBy(n => n.Length)
      .ToList();
  }

  /// <summary>Character classes whose template stem is <paramref name="tplStem"/> (players / npcs, client side first).</summary>
  public List<string> CharactersForTpl(string tplStem)
  {
    var stem = Stem(tplStem);
    return _index.Classes.Values
      .Where(c => !c.Name.EndsWith("_authority", StringComparison.OrdinalIgnoreCase) && _index.IsCharacter(c.Name) && !_index.IsWeapon(c.Name)
        && Stem(_index.TplOf(c.Name) ?? "").Equals(stem, StringComparison.OrdinalIgnoreCase))
      .Select(c => c.Name)
      .OrderBy(n => n.Contains("clone", StringComparison.OrdinalIgnoreCase))
      .ThenBy(n => n.EndsWith("_base", StringComparison.OrdinalIgnoreCase))
      .ThenBy(n => n.Length)
      .ToList();
  }

  /// <summary>
  ///   Characters for a model + clip: same template (PROVEN), else characters whose morpheme bundle ends the clip name
  ///   (clips are named <c>&lt;action&gt;_&lt;bundle&gt;</c>, HEURISTIC), longest bundle first.
  /// </summary>
  public (List<string> Characters, bool Proven) CharactersFor(string tplStem, string? clipPath)
  {
    var exact = CharactersForTpl(tplStem);
    if (exact.Count > 0 || clipPath is null) return (exact, exact.Count > 0);
    var clip = Path.GetFileNameWithoutExtension(clipPath.Replace('\\', '/'));
    var byBundle = _index.Classes.Values
      .Where(c => !c.Name.EndsWith("_authority", StringComparison.OrdinalIgnoreCase) && _index.IsCharacter(c.Name) && !_index.IsWeapon(c.Name))
      .Select(c => (c.Name, Bundle: _index.BundleOf(c.Name)!))
      .Where(c => clip.EndsWith("_" + c.Bundle, StringComparison.OrdinalIgnoreCase))
      .OrderByDescending(c => c.Bundle.Length)
      .ThenBy(c => c.Name.Contains("clone", StringComparison.OrdinalIgnoreCase))
      .ThenBy(c => c.Name.EndsWith("_base", StringComparison.OrdinalIgnoreCase))
      .ThenBy(c => c.Name.Length)
      .Select(c => c.Name)
      .ToList();
    return (byBundle, false);
  }

  public static string Stem(string tpl)
  {
    var s = Path.GetFileName(tpl.Replace('\\', '/'));
    foreach (var ext in new[] { ".resource", ".tpl" })
      if (s.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) s = s[..^ext.Length];
    return s;
  }

  /// <summary>
  ///   Weapons the model may hold for a clip, best first: the character's inventory (PROVEN), else weapon classes whose
  ///   name tokens appear in the clip path (HEURISTIC), firearms first when the clip has a <c>Shoot</c> track.
  /// </summary>
  public List<WeaponChoice> WeaponsFor(IReadOnlyList<string> characters, string clipPath, bool shoots, int max = 8)
  {
    var choices = new List<WeaponChoice>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var clipTokens = Tokens(clipPath);
    foreach (var ch in characters)
    {
      foreach (var c in _index.Chain(ch))
      {
        var d = _index.Detail(c.Name);
        foreach (var t in d.WeaponTypes)
          if (_index.Get(t) is not null && _index.TplOf(t) is { } tpl && seen.Add(t))
            choices.Add(new WeaponChoice(t, tpl, HoldFor(ch, t), true, $"{c.Name} inventory"));
      }
    }

    // Name tokens: characters' own tokens too (rubric clip → rubric_* weapons).
    var charTokens = characters.SelectMany(Tokens).Concat(characters.Select(c => _index.BundleOf(c) ?? "").SelectMany(Tokens)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var scored = new List<(WeaponChoice W, double Score)>();
    foreach (var c in _index.Classes.Values)
    {
      if (!c.IsWeaponPath || seen.Contains(c.Name) || c.Name.EndsWith("_authority", StringComparison.OrdinalIgnoreCase)) continue;
      if (_index.TplOf(c.Name) is not { Length: > 0 } tpl) continue;
      var firearm = c.Path.Contains("/firearm/", StringComparison.OrdinalIgnoreCase);
      if (shoots && !firearm) continue;
      if (!firearm && !c.Path.Contains("weapon_actors", StringComparison.OrdinalIgnoreCase)) continue;
      if (firearm && !c.Name.EndsWith("_client", StringComparison.OrdinalIgnoreCase)) continue;
      var wTokens = Tokens(c.Name).Concat(Tokens(tpl)).ToHashSet(StringComparer.OrdinalIgnoreCase);
      double score = 0;
      foreach (var t in wTokens)
      {
        if (clipTokens.Contains(t)) score += 2;
        else if (clipTokens.Any(ct => ct.Length >= 4 && t.Length >= 4 && (ct.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(ct, StringComparison.OrdinalIgnoreCase)))) score += 1.5;
        if (charTokens.Contains(t)) score += 0.5;
      }

      if (score < 2) continue;
      score -= wTokens.Count * 0.05;
      scored.Add((new WeaponChoice(c.Name, tpl, "", false, "clip / class name tokens"), score));
    }

    foreach (var (w, _) in scored.OrderByDescending(s => s.Score).ThenBy(s => s.W.Class.Length).Take(max))
      if (seen.Add(w.Class))
        choices.Add(w with { Hold = HoldFor(characters.FirstOrDefault(), w.Class) });
    return choices.Take(max).ToList();
  }

  /// <summary>Hand locator for a weapon: drop slot whose event names the weapon → right/left hold; else the right hold (GUN_R).</summary>
  public string HoldFor(string? character, string weapon)
  {
    string right = "GUN_R", left = "GUN_L";
    var slot = "right_hand";
    if (character is not null)
    {
      foreach (var c in _index.Chain(character))
      {
        var d = _index.Detail(c.Name);
        if (d.RightHold is { Length: > 0 } r) { right = r; }
        if (d.LeftHold is { Length: > 0 } l) { left = l; }
        if (d.RightHold is not null || d.LeftHold is not null) break;
      }

      var wTokens = Tokens(weapon).Concat(Tokens(_index.TplOf(weapon) ?? "")).ToHashSet(StringComparer.OrdinalIgnoreCase);
      foreach (var c in _index.Chain(character))
        foreach (var (ev, s) in _index.Detail(c.Name).DropSlots)
          if (Tokens(ev).Any(t => wTokens.Any(w => w.Length >= 4 && (w.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(w, StringComparison.OrdinalIgnoreCase)))))
          {
            slot = s;
            goto done;
          }
    }

    var cat = _index.Detail(weapon).EquipCategory ?? "";
    if (cat.Contains("offhand", StringComparison.OrdinalIgnoreCase)) slot = "left_hand";
    done:
    return slot.StartsWith("left", StringComparison.OrdinalIgnoreCase) ? left : right;
  }

  /// <summary>Muzzle cues for a weapon: sfx.list keys containing "muzzle" (several → picker). HEURISTIC timing: the clip's <c>Shoot</c> track.</summary>
  public List<TrackCue> ShootCues(string weapon)
  {
    var keys = _index.SfxKeys(weapon);
    return keys.Where(k => k.Key.Contains("muzzle", StringComparison.OrdinalIgnoreCase))
      .OrderBy(k => k.Key.Equals("muzzle", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
      .Select(k => new TrackCue("Shoot", k.Key, k.Value, weapon, false))
      .ToList();
  }

  public static HashSet<string> Tokens(string text)
  {
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var s = text.Replace('\\', '/');
    foreach (var raw in s.Split(new[] { '/', '_', '.', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries))
    {
      var t = raw.ToLowerInvariant();
      if (t.Length < 2 || Generic.Contains(t) || t.All(char.IsDigit) || t is "sani" or "cls" or "tpl" or "sfx") continue;
      if (t.Length > 1 && t[0] == 'v' && t[1..].All(char.IsDigit)) continue;
      set.Add(t);
      if (t.EndsWith("er", StringComparison.Ordinal) && t.Length > 5) set.Add(t[..^2]); // bolter → bolt
    }

    return set;
  }

  /// <summary>
  ///   Ranks a character's clips for a context: clips with an event track entry named in <paramref name="events"/>
  ///   (PROVEN), else by effect-name token overlap (HEURISTIC).
  /// </summary>
  public static List<(string Clip, bool Proven, double Score)> RankClips(
    IEnumerable<(string Clip, IReadOnlyCollection<string> EventTags)> clips, IReadOnlyCollection<string> events, string effect, int max = 12)
  {
    var effectTokens = Tokens(effect);
    var ranked = new List<(string, bool, double)>();
    foreach (var (clip, tags) in clips)
    {
      var hits = events.Count(e => tags.Contains(e, StringComparer.OrdinalIgnoreCase));
      var name = Path.GetFileNameWithoutExtension(clip);
      var overlap = Tokens(name).Count(t => effectTokens.Contains(t));
      if (hits > 0) ranked.Add((clip, true, 100 + hits * 10 + overlap - name.Length * 0.01));
      else if (overlap > 0) ranked.Add((clip, false, overlap - name.Length * 0.01));
    }

    return ranked.OrderByDescending(r => r.Item3).Take(max).ToList();
  }
}
