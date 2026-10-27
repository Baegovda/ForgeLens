using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>
///   <c>--variant</c> request. Tokens are separated by <c>,</c> or <c>;</c>:
///   <c>default</c> (PREDEFINED_PRESET_1 of the template's appearance), <c>all</c> (no filter, every armor piece),
///   <c>preset=N</c>, <c>appearance=TEAM/UID</c>, <c>SECTION=regionUid</c> (for example <c>HELMET=helmet_tank_common_01</c>),
///   <c>+object</c> to force an object on and <c>-object</c> to force it off.
/// </summary>
public sealed class VariantRequest
{
  public string Spec { get; private set; } = "default";
  public bool All { get; private set; }
  public int Preset { get; private set; } = 1;
  public string? Team { get; private set; }
  public string? Appearance { get; private set; }
  public Dictionary<string, string> Regions { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> Show { get; } = new(StringComparer.OrdinalIgnoreCase);
  public HashSet<string> Hide { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>Weapon tpls: FirearmSkin key in firearm_skins_library (skin_01 when null).</summary>
  public string? Skin { get; private set; }
  /// <summary>Weapon tpls: explicit geomCustomizerRegions (replaces the skin's list when set).</summary>
  public List<string>? WeaponRegions { get; private set; }

  public static VariantRequest Default => new();

  public static VariantRequest Parse(string? spec)
  {
    var request = new VariantRequest { Spec = string.IsNullOrWhiteSpace(spec) ? "default" : spec.Trim() };
    foreach (var raw in request.Spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      if (raw.Equals("default", StringComparison.OrdinalIgnoreCase))
        continue;
      if (raw.Equals("all", StringComparison.OrdinalIgnoreCase) || raw.Equals("none", StringComparison.OrdinalIgnoreCase))
      {
        request.All = true;
        continue;
      }

      if (raw[0] == '+' && raw.Length > 1)
      {
        request.Show.Add(raw[1..]);
        continue;
      }

      if (raw[0] == '-' && raw.Length > 1)
      {
        request.Hide.Add(raw[1..]);
        continue;
      }

      var eq = raw.IndexOf('=');
      if (eq <= 0 || eq == raw.Length - 1)
        throw new ArgumentException($"--variant token '{raw}' is not default, all, preset=N, appearance=TEAM/UID, SECTION=regionUid, +object or -object.");
      var key = raw[..eq].Trim();
      var value = raw[(eq + 1)..].Trim();
      switch (key.ToLowerInvariant())
      {
        case "preset":
          var number = value.StartsWith(CustomizationLibrary.PresetPrefix, StringComparison.OrdinalIgnoreCase) ? value[CustomizationLibrary.PresetPrefix.Length..] : value;
          if (!int.TryParse(number, out var preset) || preset < 1)
            throw new ArgumentException($"--variant preset '{value}' is not a number from 1 (PREDEFINED_PRESET_N).");
          request.Preset = preset;
          break;
        case "appearance":
          var slash = value.IndexOf('/');
          if (slash > 0)
          {
            request.Team = value[..slash].ToUpperInvariant();
            request.Appearance = value[(slash + 1)..];
          }
          else
          {
            request.Appearance = value;
          }

          break;
        case "team":
          request.Team = value.ToUpperInvariant();
          break;
        case "skin":
          request.Skin = value;
          break;
        case "regions":
          request.WeaponRegions = value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
          break;
        default:
          request.Regions[key.ToUpperInvariant()] = value;
          break;
      }
    }

    return request;
  }
}

public sealed class VariantResult
{
  public string Template { get; set; } = "";
  public string Spec { get; set; } = "";
  public string Team { get; set; } = "";
  public string Appearance { get; set; } = "";
  public string AppearanceSource { get; set; } = "";
  public string Preset { get; set; } = "";
  public Dictionary<string, string> Regions { get; } = new(StringComparer.OrdinalIgnoreCase);
  public List<string> Presets { get; } = new();
  public Dictionary<string, List<string>> AvailableRegions { get; } = new(StringComparer.OrdinalIgnoreCase);
  public List<string> ShownObjects { get; } = new();
  public int CustomizableObjects { get; set; }
  public int HiddenParts { get; set; }
  public int VisibleParts { get; set; }
  public List<string> Notes { get; } = new();
}

/// <summary>
///   <c>ssl/body/customization/character_customization_library.sso</c>: every armor region per team and appearance,
///   and which template objects (<c>meshes</c>) each region shows. The game's <c>geom_customizer_region</c> script
///   ("List of meshes which will show/hide on show/hide this region") drives the native Geom bindings
///   ShowObj / HideObj (sm2 retail 0x14133fec0 / 0x14133eae0), which both call 0x14133e750 per object name.
/// </summary>
public sealed class CustomizationLibrary
{
  public const string LibraryPath = "ssl/body/customization/character_customization_library.sso";
  public const string PresetPrefix = "PREDEFINED_PRESET_";
  /// <summary>Constructor dummies: <c>ssl/loading_scene_logic/customizable_character_dummy_*.cls</c> (geom.nameTpl + characterInfo).</summary>
  public const string DummyClassStem = "customizable_character_dummy";

  private static readonly Regex Constructor = new("^cc_(sm|ch)_constructor_([a-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

  public CustomizationLibrary(Dictionary<string, object?> root) => Root = root;

  public Dictionary<string, object?> Root { get; }

  public static bool IsConstructorTemplate(string? name) => !string.IsNullOrEmpty(name) && Constructor.IsMatch(name);

  public static CustomizationLibrary? Load(GameCatalog catalog, ArchiveStore store)
  {
    if (!catalog.TryGetByPath(LibraryPath, out var index))
      return null;
    return FromText(Encoding.UTF8.GetString(store.Read(index)));
  }

  public static CustomizationLibrary FromText(string text) => new(SslText.Parse(text));

  public Dictionary<string, object?>? Team(string team) => SslText.Obj(Root, "appearancesByTeams", team);

  public Dictionary<string, object?>? AppearanceOf(string team, string appearance)
    => SslText.Obj(Team(team), "customizationPresets", appearance);

  /// <summary>Every region in the library: (team, appearance or <c>*shared*</c>, section, uid, region).</summary>
  public IEnumerable<(string Team, string Appearance, string Section, string Uid, Dictionary<string, object?> Region)> AllRegions()
  {
    if (SslText.Obj(Root, "appearancesByTeams") is not { } teams)
      yield break;
    foreach (var (teamName, teamValue) in teams)
    {
      if (SslText.Obj(teamValue, "customizationPresets") is { } presets)
      {
        foreach (var (appName, appValue) in presets)
        {
          if (SslText.Obj(appValue, "bodySections") is not { } sections)
            continue;
          foreach (var (section, sectionValue) in sections)
          {
            if (SslText.Obj(sectionValue, "regions") is not { } regions)
              continue;
            foreach (var (uid, region) in regions)
            {
              if (region is Dictionary<string, object?> map)
                yield return (teamName, appName, section, uid, map);
            }
          }
        }
      }

      if (SslText.Obj(teamValue, "sharedOutfitParts", "outfitParts") is { } shared)
      {
        foreach (var (section, sectionValue) in shared)
        {
          if (SslText.Obj(sectionValue, "regions") is not { } regions)
            continue;
          foreach (var (uid, region) in regions)
          {
            if (region is Dictionary<string, object?> map)
              yield return (teamName, "*shared*", section, uid, map);
          }
        }
      }
    }
  }

  public Dictionary<string, object?>? Region(string team, string appearance, string section, string uid)
    => SslText.Obj(AppearanceOf(team, appearance), "bodySections", section, "regions", uid)
       ?? SslText.Obj(Team(team), "sharedOutfitParts", "outfitParts", section, "regions", uid);

  /// <summary>Appearance whose <c>masteryTypes</c> lists <paramref name="mastery"/> (PVE_TANK is in PVE_PVP_TANK).</summary>
  public string? AppearanceForMastery(string team, string mastery)
  {
    if (SslText.Obj(Team(team), "customizationPresets") is not { } presets)
      return null;
    foreach (var (name, value) in presets)
    {
      if (value is Dictionary<string, object?> map && map.TryGetValue("masteryTypes", out var types)
          && SslText.Strings(types).Contains(mastery, StringComparer.OrdinalIgnoreCase))
        return name;
    }

    return null;
  }

  /// <summary>Team and mastery for a constructor tpl, from the name: cc_sm_constructor_X is LOYALIST/PVE_X, cc_ch_ is CHAOS.</summary>
  public static (string Team, string Mastery)? CharacterFromName(string template)
  {
    var match = Constructor.Match(template);
    if (!match.Success)
      return null;
    var team = match.Groups[1].Value.Equals("ch", StringComparison.OrdinalIgnoreCase) ? "CHAOS" : "LOYALIST";
    return (team, "PVE_" + match.Groups[2].Value.ToUpperInvariant());
  }

  /// <summary>characterInfo (team, masteryType) of the dummy .cls whose geom.nameTpl is <paramref name="template"/>.</summary>
  public static (string Team, string Mastery, string Source)? CharacterFromDummyClass(GameCatalog catalog, ArchiveStore store, string template)
  {
    var query = catalog.Query(null, DummyClassStem, AssetFilter.All);
    foreach (var fileIndex in query.Indices ?? Enumerable.Range(query.First, query.Count).ToArray())
    {
      var file = catalog.GetFile(fileIndex);
      if (!file.Path.EndsWith(".cls", StringComparison.OrdinalIgnoreCase))
        continue;
      Dictionary<string, object?> root;
      try
      {
        root = SslText.Parse(Encoding.UTF8.GetString(store.Read(fileIndex)));
      }
      catch
      {
        continue;
      }

      var found = FindCharacter(root, template);
      if (found is { } hit)
        return (hit.Team, hit.Mastery, file.Path);
    }

    return null;
  }

  public static (string Team, string Mastery)? FindCharacter(Dictionary<string, object?> root, string template)
  {
    string? nameTpl = null, team = null, mastery = null;
    void Walk(object? value)
    {
      if (value is Dictionary<string, object?> map)
      {
        foreach (var (key, child) in map)
        {
          if (key == "nameTpl" && child is string tpl)
            nameTpl ??= tpl;
          else if (key == "characterInfo" && child is Dictionary<string, object?> info)
          {
            team ??= info.TryGetValue("team", out var t) ? t as string : null;
            mastery ??= info.TryGetValue("masteryType", out var m) ? m as string : null;
          }
          else
            Walk(child);
        }
      }
      else if (value is List<object?> list)
      {
        foreach (var item in list)
          Walk(item);
      }
    }

    Walk(root);
    if (nameTpl is null || team is null || mastery is null || !nameTpl.Equals(template, StringComparison.OrdinalIgnoreCase))
      return null;
    return (team, mastery);
  }
}

public static class ArmorVariant
{
  public const string HideReason = "variant";

  /// <summary>
  ///   Hide the armor pieces a constructor tpl carries for every customization region except the selected ones.
  ///   Objects no region mentions keep their visibility. <c>powerfist_*</c> belong to <c>prop_power_fist_customizer_by_loadout</c>
  ///   (picked by the weapon loadout, not the armor) and are off unless forced with <c>+powerfist_01</c>.
  /// </summary>
  public static VariantResult Apply(
    ExportedMesh mesh,
    CustomizationLibrary library,
    string team,
    string appearance,
    VariantRequest request)
  {
    var result = new VariantResult
    {
      Template = mesh.Name,
      Spec = request.Spec,
      Team = team,
      Appearance = appearance,
    };
    var app = library.AppearanceOf(team, appearance);
    if (app is null)
    {
      result.Notes.Add($"appearance {team}/{appearance} is not in {CustomizationLibrary.LibraryPath}; nothing hidden.");
      return result;
    }

    var bones = mesh.Bones;
    var byName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
    var children = new List<int>[bones.Count];
    for (var i = 0; i < bones.Count; i++)
    {
      var name = bones[i].SourceName.Length > 0 ? bones[i].SourceName : bones[i].Name;
      if (!byName.TryGetValue(name, out var list))
        byName[name] = list = new List<int>();
      list.Add(i);
      var parent = bones[i].Parent;
      if (parent >= 0 && parent < bones.Count)
        (children[parent] ??= new List<int>()).Add(i);
    }

    void AddSubtree(string name, HashSet<int> into)
    {
      if (!byName.TryGetValue(name, out var roots))
        return;
      var stack = new Stack<int>(roots);
      while (stack.Count > 0)
      {
        var node = stack.Pop();
        if (!into.Add(node) || children[node] is not { } kids)
          continue;
        foreach (var kid in kids)
          stack.Push(kid);
      }
    }

    var customizable = new HashSet<int>();
    foreach (var region in library.AllRegions())
    {
      foreach (var name in SslText.Strings(region.Region.GetValueOrDefault("meshes")))
        AddSubtree(name, customizable);
    }

    result.CustomizableObjects = customizable.Count;

    if (SslText.Obj(app, "armorPresets") is { } presets)
      result.Presets.AddRange(presets.Keys);
    if (SslText.Obj(app, "bodySections") is { } sections)
    {
      foreach (var (section, value) in sections)
      {
        if (SslText.Obj(value, "regions") is { } regions)
          result.AvailableRegions[section] = regions.Keys.ToList();
      }
    }

    var presetName = CustomizationLibrary.PresetPrefix + request.Preset;
    result.Preset = presetName;
    if (SslText.Obj(app, "armorPresets", presetName, "bodySectionRegionUids") is { } uids)
    {
      foreach (var (section, value) in uids)
      {
        if (value is Dictionary<string, object?> holder && holder.GetValueOrDefault("__value") is string uid)
          result.Regions[section] = uid;
      }
    }
    else
    {
      result.Notes.Add($"{presetName} is not an armor preset of {team}/{appearance} (has {string.Join(", ", result.Presets)}).");
    }

    foreach (var (section, uid) in request.Regions)
    {
      if (library.Region(team, appearance, section, uid) is null)
      {
        var known = result.AvailableRegions.TryGetValue(section, out var list) ? string.Join(", ", list.Take(6)) + (list.Count > 6 ? ", ..." : "") : "none";
        result.Notes.Add($"region {section}={uid} is not in {team}/{appearance} or the shared outfit parts (known: {known}); kept {result.Regions.GetValueOrDefault(section) ?? "nothing"}.");
        continue;
      }

      result.Regions[section] = uid;
    }

    var shown = new HashSet<int>();
    var hidden = new HashSet<int>();
    foreach (var (section, uid) in result.Regions)
    {
      var region = library.Region(team, appearance, section, uid);
      if (region is null)
      {
        result.Notes.Add($"region {section}={uid} is not in {team}/{appearance} or the shared outfit parts.");
        continue;
      }

      foreach (var name in SslText.Strings(region.GetValueOrDefault("meshes")))
      {
        if (!byName.ContainsKey(name))
          result.Notes.Add($"{section}={uid}: object {name} is not in {mesh.Name}.");
        AddSubtree(name, shown);
        result.ShownObjects.Add(name);
      }

      foreach (var name in SslText.Strings(region.GetValueOrDefault("meshesToHide")))
        AddSubtree(name, hidden);
    }

    foreach (var node in customizable)
    {
      if (!shown.Contains(node))
        hidden.Add(node);
    }

    foreach (var (name, _) in byName)
    {
      if (name.StartsWith("powerfist", StringComparison.OrdinalIgnoreCase))
        AddSubtree(name, hidden);
    }

    foreach (var name in request.Hide)
      AddSubtree(name, hidden);
    var forced = new HashSet<int>();
    foreach (var name in request.Show)
    {
      if (!byName.ContainsKey(name))
        result.Notes.Add($"+{name} is not an object in {mesh.Name}.");
      AddSubtree(name, forced);
    }

    hidden.ExceptWith(forced);

    void Mark(MeshPart part)
    {
      if (part.Node >= 0 && part.Visible && hidden.Contains(part.Node))
      {
        part.Visible = false;
        part.HideReason = HideReason;
        result.HiddenParts++;
      }
    }

    foreach (var part in mesh.Parts)
      Mark(part);
    foreach (var level in mesh.Levels)
    {
      foreach (var part in level.Parts)
        Mark(part);
    }

    result.VisibleParts = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)).Count(part => part.Visible && part.IndexCount >= 3);
    return result;
  }
}
