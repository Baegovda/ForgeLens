namespace LibSaber.SM2.Assets.Customization;

/// <summary>One selectable armor part: a region of a class's <c>bodySections</c> or of the team's <c>sharedOutfitParts</c>.</summary>
public sealed record KitbashPart(
  string Uid,
  string Slot,
  bool Shared,
  IReadOnlyList<string> Meshes,
  IReadOnlyList<string> AppearanceUids,
  bool ForAllCharacters,
  bool Hidden,
  string? Rarity)
{
  public string Label => Shared ? Uid + "  · shared" : Uid;
  public override string ToString() => Label;
}

/// <summary>An <c>armorPresets</c> entry: region per slot, or a champion skin (separate tpl, nothing to pick).</summary>
public sealed record KitbashPreset(string Name, bool IsChampion, string? ChampionSkin, string? DlcUid, IReadOnlyDictionary<string, string> Regions)
{
  public int Number => int.TryParse(Name.Replace(CustomizationLibrary.PresetPrefix, ""), out var n) ? n : 0;
  public string Label => Name.Replace(CustomizationLibrary.PresetPrefix, "Preset ") + (IsChampion ? "  · champion" + (DlcUid is { Length: > 0 } d ? " (" + d + ")" : "") : "");
  public override string ToString() => Label;
}

/// <summary>A <c>customizationPresets</c> appearance (class) of one team.</summary>
public sealed class KitbashClass
{
  public required string Team { get; init; }
  public required string Name { get; init; }
  public List<string> Masteries { get; } = new();
  public List<KitbashPreset> Presets { get; } = new();
  /// <summary>Class regions per slot (<c>bodySections.SLOT.regions</c>).</summary>
  public Dictionary<string, List<KitbashPart>> Sections { get; } = new(StringComparer.OrdinalIgnoreCase);
  public bool HasParts => Sections.Values.Any(list => list.Count > 0);
  public override string ToString() => Name;
}

/// <summary>
///   ui-44 Kitbasher: the part picker data, read like the Kitbasher Blender add-on (P14) does from
///   <c>character_customization_library.sso</c>: appearancesByTeams → team → customizationPresets → class →
///   armorPresets / bodySections.SLOT.regions → meshes, plus the team's sharedOutfitParts.outfitParts.
///   A chosen region shows its <c>meshes</c> (objects of the constructor tpl) and the rest of the customizable objects hide
///   (<see cref="ArmorVariant"/>), so preview and export use the same code.
/// </summary>
public sealed class KitbashLibrary
{
  /// <summary>Slot order of the Kitbasher add-on (UI_SLOTS); slots it does not list follow alphabetically.</summary>
  public static readonly string[] SlotOrder =
  {
    "HELMET", "CHEST", "BACKPACK", "LEFT_PAULDRON", "RIGHT_PAULDRON", "LEFT_GAUNTLET", "RIGHT_GAUNTLET",
    "LEFT_GREAVE", "RIGHT_GREAVE", "LEFT_BRACER", "RIGHT_BRACER", "HELMET_LENSES", "VANITY", "VANITY_2", "VANITY_3",
    "COAT", "HALO", "ANTENNA", "CLOTH",
  };

  public const string NoneUid = "none";

  private KitbashLibrary(CustomizationLibrary library) => Library = library;

  public CustomizationLibrary Library { get; }
  public List<string> Teams { get; } = new();
  public Dictionary<string, List<KitbashClass>> Classes { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>Team-wide <c>sharedOutfitParts.outfitParts</c> regions per slot.</summary>
  public Dictionary<string, Dictionary<string, List<KitbashPart>>> Shared { get; } = new(StringComparer.OrdinalIgnoreCase);

  public static KitbashLibrary From(CustomizationLibrary library)
  {
    var kit = new KitbashLibrary(library);
    if (SslText.Obj(library.Root, "appearancesByTeams") is not { } teams)
      return kit;
    foreach (var (teamName, teamValue) in teams)
    {
      if (teamValue is not Dictionary<string, object?>)
        continue;
      kit.Teams.Add(teamName);
      var classes = kit.Classes[teamName] = new List<KitbashClass>();
      var shared = kit.Shared[teamName] = new Dictionary<string, List<KitbashPart>>(StringComparer.OrdinalIgnoreCase);
      if (SslText.Obj(teamValue, "sharedOutfitParts", "outfitParts") is { } outfit)
      {
        foreach (var (slot, slotValue) in outfit)
          shared[slot] = Parts(slot, SslText.Obj(slotValue, "regions"), true);
      }

      if (SslText.Obj(teamValue, "customizationPresets") is not { } presets)
        continue;
      foreach (var (className, classValue) in presets)
      {
        if (classValue is not Dictionary<string, object?> map)
          continue;
        var cls = new KitbashClass { Team = teamName, Name = className };
        cls.Masteries.AddRange(SslText.Strings(map.GetValueOrDefault("masteryTypes")));
        if (SslText.Obj(map, "armorPresets") is { } armor)
        {
          foreach (var (presetName, presetValue) in armor)
          {
            if (presetValue is not Dictionary<string, object?> p)
              continue;
            var regions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (SslText.Obj(p, "bodySectionRegionUids") is { } uids)
            {
              foreach (var (slot, value) in uids)
              {
                if (value is Dictionary<string, object?> holder && holder.GetValueOrDefault("__value") is string uid)
                  regions[slot] = uid;
                else if (value is string direct)
                  regions[slot] = direct;
              }
            }

            var champion = Truthy(p.GetValueOrDefault("isChampion"));
            string? skin = null;
            if (SslText.Obj(p, "skinOverrides") is { } skins)
            {
              foreach (var (_, skinValue) in skins)
              {
                if (skinValue is Dictionary<string, object?> s && s.GetValueOrDefault("__value") is string name)
                {
                  // PVE skin first (PveSkinChampion01); PVP is the same body.
                  if (skin is null || name.StartsWith("Pve", StringComparison.OrdinalIgnoreCase))
                    skin = name;
                }
              }
            }

            var dlc = SslText.Obj(p, "dlcLock")?.GetValueOrDefault("dlcUid") as string;
            cls.Presets.Add(new KitbashPreset(presetName, champion, skin, dlc, regions));
          }
        }

        if (SslText.Obj(map, "bodySections") is { } sections)
        {
          foreach (var (slot, slotValue) in sections)
            cls.Sections[slot] = Parts(slot, SslText.Obj(slotValue, "regions"), false);
        }

        classes.Add(cls);
      }
    }

    return kit;
  }

  private static bool Truthy(object? value)
    => value is true || (value is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase));

  private static List<KitbashPart> Parts(string slot, Dictionary<string, object?>? regions, bool shared)
  {
    var list = new List<KitbashPart>();
    if (regions is null)
      return list;
    foreach (var (uid, value) in regions)
    {
      if (value is not Dictionary<string, object?> region)
        continue;
      var meshes = SslText.Strings(region.GetValueOrDefault("meshes"))
        .Concat(SslText.Strings(region.GetValueOrDefault("equipmentActorMeshes")))
        .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
      list.Add(new KitbashPart(uid, slot, shared, meshes,
        SslText.Strings(region.GetValueOrDefault("appearanceUids")).ToList(),
        Truthy(region.GetValueOrDefault("isForAllCharacters")),
        Truthy(region.GetValueOrDefault("isHidden")),
        region.GetValueOrDefault("rarity") as string));
    }

    return list;
  }

  public KitbashClass? Class(string team, string name)
    => Classes.TryGetValue(team, out var list) ? list.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) : null;

  /// <summary>PVE_PVP_X and PVP_X name the same class (the add-on's get_archetype_keys).</summary>
  public static bool SameClass(string a, string b)
  {
    static string Core(string s) => s.StartsWith("PVE_PVP_", StringComparison.OrdinalIgnoreCase) ? s[8..] : s.StartsWith("PVP_", StringComparison.OrdinalIgnoreCase) || s.StartsWith("PVE_", StringComparison.OrdinalIgnoreCase) ? s[4..] : s;
    return Core(a).Equals(Core(b), StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>A shared part is offered to a class when it is for all characters, names no appearance, or names this class.</summary>
  public static bool SharedFits(KitbashPart part, KitbashClass cls)
    => part.ForAllCharacters || part.AppearanceUids.Count == 0 || part.AppearanceUids.Any(a => SameClass(a, cls.Name));

  /// <summary>Slots of a class: its own body sections, then every shared slot, in add-on order.</summary>
  public List<string> Slots(KitbashClass cls, bool classOnly)
  {
    var slots = new HashSet<string>(cls.Sections.Where(s => s.Value.Count > 0).Select(s => s.Key), StringComparer.OrdinalIgnoreCase);
    if (Shared.TryGetValue(cls.Team, out var shared))
    {
      foreach (var (slot, parts) in shared)
      {
        if (parts.Any(p => !classOnly || SharedFits(p, cls)))
          slots.Add(slot);
      }
    }

    return slots.OrderBy(s => Array.IndexOf(SlotOrder, s.ToUpperInvariant()) is var i && i >= 0 ? i : 100).ThenBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary>Choices for one slot: the class's regions, then the team's shared regions (filtered to the class unless <paramref name="classOnly"/> is off).</summary>
  public List<KitbashPart> Options(KitbashClass cls, string slot, bool classOnly)
  {
    var list = new List<KitbashPart>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (cls.Sections.TryGetValue(slot, out var own))
    {
      foreach (var part in own)
      {
        if (seen.Add(part.Uid))
          list.Add(part);
      }
    }

    if (Shared.TryGetValue(cls.Team, out var shared) && shared.TryGetValue(slot, out var parts))
    {
      foreach (var part in parts.OrderBy(p => p.Uid, StringComparer.OrdinalIgnoreCase))
      {
        if ((!classOnly || SharedFits(part, cls)) && seen.Add(part.Uid))
          list.Add(part);
      }
    }

    return list;
  }

  /// <summary>
  ///   Template that carries the class's armor objects. Constructors: <c>cc_sm_constructor_x</c> / <c>cc_ch_constructor_x</c>
  ///   (PVE_PVP_X, also the dummy .cls nameTpl). STORY_X uses the loyalist constructor of X (its regions name the same objects;
  ///   HEURISTIC). Champion presets use <c>cc_sm_champion_x_NN</c> from the skin name PveSkinChampionNN (HEURISTIC).
  /// </summary>
  public static List<string> TemplateCandidates(string team, string className, KitbashPreset? preset)
  {
    var core = className;
    foreach (var prefix in new[] { "PVE_PVP_", "PVP_", "PVE_", "STORY_" })
    {
      if (core.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      {
        core = core[prefix.Length..];
        break;
      }
    }

    core = core.ToLowerInvariant();
    var side = team.Equals("CHAOS", StringComparison.OrdinalIgnoreCase) ? "ch" : "sm";
    var list = new List<string>();
    if (preset is { IsChampion: true, ChampionSkin: { } skin })
    {
      var digits = new string(skin.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
      if (digits.Length > 0)
        list.Add($"cc_{side}_champion_{core}_{int.Parse(digits):00}");
    }

    list.Add($"cc_{side}_constructor_{core}");
    if (className.StartsWith("STORY_", StringComparison.OrdinalIgnoreCase))
      list.Add($"cc_sm_{core}_story");
    return list;
  }

  /// <summary><c>--variant</c> spec for a picked combination: appearance, preset and every slot (<c>SLOT=none</c> for an empty slot).</summary>
  public static string Spec(string team, string className, KitbashPreset? preset, IReadOnlyDictionary<string, string?> slots)
  {
    var parts = new List<string> { "appearance=" + team + "/" + className };
    if (preset is { Number: > 0 })
      parts.Add("preset=" + preset.Number);
    foreach (var (slot, uid) in slots.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
      parts.Add(slot + "=" + (string.IsNullOrEmpty(uid) ? NoneUid : uid));
    return string.Join(",", parts);
  }
}
