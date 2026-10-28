using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>One weapon colour scheme of the Forge Lens picker (mtl1-49).</summary>
/// <param name="Material">td material suffix (<c>red</c>, <c>mastercrafted</c>); empty = the base (Default) look.</param>
/// <param name="Name">Localized colour name (strings_english.str), e.g. "Mephiston Red".</param>
/// <param name="FullName">Localized item name, e.g. "Mephiston Red Chainsword" (empty when the game has none).</param>
/// <param name="LocalName">Same colour in the second language (Korean), for the tooltip.</param>
/// <param name="NameKey">String key the name came from (or "HEURISTIC: ..." when derived).</param>
/// <param name="SwatchSource">armor_color_library entry the swatch comes from, or "texture average".</param>
public sealed record ColourScheme(string Material, string Name, string FullName, string LocalName, string NameKey, byte R, byte G, byte B, string SwatchSource)
{
  public string Hex => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>
///   mtl1-49: the in-game weapon colour schemes of a tpl. Materials come from the diffuse td <c>materials</c> blocks (the
///   variants ApplyMaterialSuffix can select, PROVEN mtl1-48); names from <c>texts/strings_&lt;lang&gt;.str</c>
///   (<c>UI_FIREARM_&lt;W&gt;[_GL]_&lt;C&gt;_NAME</c>, <c>UI_&lt;MELEE|FIREARM&gt;_&lt;W&gt;_HM_&lt;C&gt;_NAME</c>); swatches from
///   <c>ssl/body/customization/armor_color_library.sso</c> by the heraldry colour of the same localized name
///   (<c>UI_HERALDRY_COLOR_&lt;X&gt;_NAME</c> → entry X; metallic → spec_metalnessTint, else tint).
/// </summary>
public static class ColourSchemes
{
  public const string PalettePath = "ssl/body/customization/armor_color_library.sso";

  /// <summary>Picker order: the game's appearance packs list white, blue, red, then the green/yellow/violet set.</summary>
  private static readonly string[] Order = { "white", "blue", "red", "green", "yellow", "violet", "gold", "twitch", "mastercrafted" };

  private static readonly string[] ClassPrefixes = { "arifle_", "brifle_", "hgun_", "hwpn_", "smg_", "srifle_", "shotgun_", "mgun_", "melee_", "wpn_" };

  /// <summary>td material names of one diffuse .td (<c>materials { default {..} red {..} }</c>), minus <c>default</c>.</summary>
  public static List<string> TdMaterials(string tdText)
  {
    var root = SslText.Parse(tdText);
    var list = new List<string>();
    if (SslText.Obj(root, "materials") is { } materials)
    {
      foreach (var (key, value) in materials)
        if (value is Dictionary<string, object?> && !key.Equals("default", StringComparison.OrdinalIgnoreCase))
          list.Add(key);
    }

    return list;
  }

  /// <summary>Materials offered for <paramref name="mesh"/>: union over its diffuse textures, game order first.</summary>
  public static List<string> MaterialsOf(ExportedMesh mesh, Func<string, string?> tdText)
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var found = new List<string>();
    foreach (var name in mesh.Parts.Concat(mesh.Levels.SelectMany(l => l.Parts)).Select(p => p.Texture).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
    {
      if (tdText(name!) is not { } text)
        continue;
      foreach (var m in TdMaterials(text))
        if (seen.Add(m))
          found.Add(m);
    }

    return found.OrderBy(m => Array.FindIndex(Order, o => o.Equals(m, StringComparison.OrdinalIgnoreCase)) is var i && i < 0 ? 99 : i)
      .ThenBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();
  }

  /// <summary><c>KEY \t "Value"</c> lines of a UTF-16 strings_&lt;lang&gt;.str.</summary>
  public static Dictionary<string, string> ParseStrings(byte[] data)
  {
    var text = data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE ? Encoding.Unicode.GetString(data, 2, data.Length - 2)
      : data.Length >= 2 && data[1] == 0 ? Encoding.Unicode.GetString(data) : Encoding.UTF8.GetString(data);
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var raw in text.Split('\n'))
    {
      var line = raw.TrimEnd('\r');
      var tab = line.IndexOf('\t');
      if (tab <= 0)
        continue;
      var value = line[tab..].Trim();
      if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        value = value[1..^1];
      map.TryAdd(line[..tab].Trim(), value);
    }

    return map;
  }

  public sealed record PaletteEntry(string Name, bool Metallic, (byte R, byte G, byte B)? Tint, (byte R, byte G, byte B)? MetalTint, string Section);

  /// <summary>armor_color_library.sso entries (first section that defines a name wins: primary, secondary, ...).</summary>
  public static Dictionary<string, PaletteEntry> ParsePalette(string text)
  {
    var result = new Dictionary<string, PaletteEntry>(StringComparer.OrdinalIgnoreCase);
    var root = SslText.Parse(text);
    foreach (var (section, sv) in root)
    {
      if (sv is not Dictionary<string, object?> entries)
        continue;
      foreach (var (name, ev) in entries)
      {
        if (ev is not Dictionary<string, object?> e || result.ContainsKey(name))
          continue;
        (byte, byte, byte)? Color(string key)
        {
          if (SslText.Obj(e, key, "value") is not { } v)
            return null;
          static byte B(object? o) => o is null ? (byte)0 : (byte)Math.Clamp(Convert.ToInt32(Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture)), 0, 255);
          return (B(v.GetValueOrDefault("r")), B(v.GetValueOrDefault("g")), B(v.GetValueOrDefault("b")));
        }

        var metallic = e.GetValueOrDefault("isMetallic") is { } m && (m is bool b ? b : string.Equals(m.ToString(), "True", StringComparison.OrdinalIgnoreCase));
        result[name] = new PaletteEntry(name, metallic, Color("tint"), Color("spec_metalnessTint"), section);
      }
    }

    return result;
  }

  /// <summary>Upper-case W tokens to try for a weapon: from the firearm uid (minus class prefix) and the tpl (minus wpn_ / _01...).</summary>
  public static List<string> WeaponTokens(string template, string? uid)
  {
    var tokens = new List<string>();
    void Add(string? s)
    {
      if (string.IsNullOrEmpty(s))
        return;
      var t = s.ToUpperInvariant();
      if (!tokens.Contains(t))
        tokens.Add(t);
    }

    if (uid is not null)
    {
      var u = uid;
      foreach (var p in ClassPrefixes)
        if (u.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { u = u[p.Length..]; break; }
      Add(u);
      Add(uid);
    }

    var tpl = template.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase) ? template[4..] : template;
    var cut = Regex.Match(tpl, "_\\d\\d(_|$)");
    if (cut.Success)
      tpl = tpl[..cut.Index];
    Add(tpl);
    return tokens;
  }

  /// <summary>
  ///   Picker entries for <paramref name="materials"/>. <paramref name="average"/> gives the mean colour of a material's
  ///   variant diffuse (swatch fallback when no heraldry colour has the same name).
  /// </summary>
  public static List<ColourScheme> Describe(IReadOnlyList<string> materials, string template, string? uid, bool grenadeLauncher,
    IReadOnlyDictionary<string, string> strings, IReadOnlyDictionary<string, string>? localStrings,
    IReadOnlyDictionary<string, PaletteEntry> palette, Func<string, (byte R, byte G, byte B)?> average)
  {
    var tokens = WeaponTokens(template, uid);
    // heraldry: localized name → key stem (MACRAGGE_BLUE)
    var heraldry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (key, value) in strings)
    {
      if (key.StartsWith("UI_HERALDRY_COLOR_", StringComparison.OrdinalIgnoreCase) && key.EndsWith("_NAME", StringComparison.OrdinalIgnoreCase))
        heraldry.TryAdd(value, key["UI_HERALDRY_COLOR_".Length..^"_NAME".Length]);
    }

    var list = new List<ColourScheme>();
    foreach (var material in materials)
    {
      var c = material.ToUpperInvariant();
      string? nameKey = null, full = null, weaponName = null;
      var fuzzy = false;
      foreach (var w in tokens)
      {
        foreach (var family in new[] { "FIREARM", "MELEE" })
        {
          var keys = new List<string>();
          if (grenadeLauncher)
            keys.Add($"UI_{family}_{w}_GL_{c}_NAME");
          keys.Add($"UI_{family}_{w}_{c}_NAME");
          keys.Add($"UI_{family}_{w}_HM_{c}_NAME");
          foreach (var k in keys)
          {
            if (strings.TryGetValue(k, out var v) && v.Length > 0)
            {
              nameKey = k;
              full = v;
              weaponName = strings.GetValueOrDefault($"UI_{family}_{w}_NAME");
              break;
            }
          }

          if (nameKey is not null) break;
        }

        if (nameKey is not null) break;
      }

      if (nameKey is null && Fuzzy(strings, tokens, c, grenadeLauncher) is { } fz)
      {
        // HEURISTIC: closest W token of the string table (power_halberd ~ POWER_HALBERT, thunder_hammer_one_handed ~ ONE_HANDED_HAMMER)
        nameKey = fz.Key;
        full = fz.Value;
        weaponName = null;
        fuzzy = true;
      }

      string name;
      if (full is not null)
      {
        name = ColourPart(full, weaponName, heraldry);
      }
      else
      {
        // HEURISTIC: the colour name most weapons use for this material
        name = MajorityName(strings, c, heraldry) ?? Title(material);
        nameKey = "HEURISTIC: majority of *_" + c + "_NAME";
        full = "";
      }

      if (material.Equals("mastercrafted", StringComparison.OrdinalIgnoreCase) && full!.Length == 0)
      {
        foreach (var k in new[] { "UI_RARITY_MASTERCRAFTED", "UI_RARITY_MASTERCRAFTED_NAME", "UI_WEAPON_RARITY_MASTERCRAFTED" })
          if (strings.TryGetValue(k, out var v) && v.Length > 0) { name = v; nameKey = k; break; }
      }

      var local = "";
      if (localStrings is not null && nameKey is not null && !nameKey.StartsWith("HEURISTIC") && localStrings.TryGetValue(nameKey, out var lv))
        local = lv;

      if (fuzzy)
        nameKey = "HEURISTIC fuzzy W token: " + nameKey;
      (byte R, byte G, byte B) rgb = (128, 128, 128);
      var source = "none";
      if (heraldry.TryGetValue(name, out var stem) && palette.TryGetValue(stem, out var entry) && (entry.Tint ?? entry.MetalTint) is not null)
      {
        rgb = entry.Metallic && entry.MetalTint is { } mt ? mt : entry.Tint ?? entry.MetalTint!.Value;
        source = $"{PalettePath} {entry.Section}.{entry.Name}" + (entry.Metallic ? " spec_metalnessTint" : " tint");
      }
      else if (average(material) is { } avg)
      {
        rgb = avg;
        source = "texture average (no heraldry colour named \"" + name + "\")";
      }

      list.Add(new ColourScheme(material, name, full ?? "", local, nameKey ?? "", rgb.R, rgb.G, rgb.B, source));
    }

    return list;
  }

  /// <summary>"Mephiston Red Chainsword" → "Mephiston Red" (weapon name removed; else the longest heraldry name inside).</summary>
  private static string ColourPart(string full, string? weaponName, IReadOnlyDictionary<string, string> heraldry)
  {
    var best = heraldry.Keys.Where(h => h.Length > 2 && full.Contains(h, StringComparison.OrdinalIgnoreCase)).OrderByDescending(h => h.Length).FirstOrDefault();
    if (best is not null)
      return best;
    if (!string.IsNullOrEmpty(weaponName) && full.Contains(weaponName, StringComparison.OrdinalIgnoreCase))
    {
      var rest = Regex.Replace(full, Regex.Escape(weaponName), "", RegexOptions.IgnoreCase).Trim(' ', '-', ',');
      if (rest.Length > 0)
        return rest;
    }

    return full;
  }

  private static readonly Regex NameKey = new("^UI_(FIREARM|MELEE)_(.+?)(_GL)?(_HM)?_([A-Z]+)_NAME$", RegexOptions.Compiled);

  /// <summary>Best string key whose W token is close to one of <paramref name="tokens"/> (edit ratio or word overlap ≥ 0.75).</summary>
  private static KeyValuePair<string, string>? Fuzzy(IReadOnlyDictionary<string, string> strings, List<string> tokens, string c, bool gl)
  {
    KeyValuePair<string, string>? best = null;
    var bestScore = 0.75;
    foreach (var pair in strings)
    {
      var m = NameKey.Match(pair.Key);
      if (!m.Success || m.Groups[5].Value != c || m.Groups[3].Success != gl || pair.Value.Length == 0)
        continue;
      foreach (var t in tokens)
      {
        var score = Math.Max(EditRatio(t, m.Groups[2].Value), WordOverlap(t, m.Groups[2].Value));
        if (score > bestScore)
        {
          bestScore = score;
          best = pair;
        }
      }
    }

    return best;
  }

  private static double WordOverlap(string a, string b)
  {
    var x = a.Split('_', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    var y = b.Split('_', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    var union = x.Union(y).Count();
    return union == 0 ? 0 : (double)x.Intersect(y).Count() / union;
  }

  private static double EditRatio(string a, string b)
  {
    var d = new int[a.Length + 1, b.Length + 1];
    for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
    for (var j = 0; j <= b.Length; j++) d[0, j] = j;
    for (var i = 1; i <= a.Length; i++)
      for (var j = 1; j <= b.Length; j++)
        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
    return 1 - (double)d[a.Length, b.Length] / Math.Max(1, Math.Max(a.Length, b.Length));
  }

  private static string? MajorityName(IReadOnlyDictionary<string, string> strings, string c, IReadOnlyDictionary<string, string> heraldry)
  {
    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var (key, value) in strings)
    {
      if (!(key.StartsWith("UI_FIREARM_") || key.StartsWith("UI_MELEE_")) || !key.EndsWith("_" + c + "_NAME", StringComparison.OrdinalIgnoreCase))
        continue;
      var name = ColourPart(value, null, heraldry);
      if (name != value || heraldry.ContainsKey(name))
        counts[name] = counts.GetValueOrDefault(name) + 1;
    }

    return counts.OrderByDescending(p => p.Value).Select(p => p.Key).FirstOrDefault();
  }

  private static string Title(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
