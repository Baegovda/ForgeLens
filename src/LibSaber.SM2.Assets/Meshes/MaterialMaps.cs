using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Textures;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   One texture on a material. <see cref="Channels"/> is the texture definition's
///   <c>format_descr</c>. <see cref="Srgb"/> is false for normals and packed masks.
/// </summary>
public sealed class MaterialMap
{
  public string Role { get; set; } = "";
  public string Name { get; set; } = "";
  public string Channels { get; set; } = "";
  public bool Srgb { get; set; }
  /// <summary>The archive has no pixels for this name. A flat color was written instead.</summary>
  public bool Generated { get; set; }
  /// <summary>Named by the mesh or the <c>.td</c>, and not present in any open archive.</summary>
  public bool Missing { get; set; }
  public byte SolidR { get; set; }
  public byte SolidG { get; set; }
  public byte SolidB { get; set; }
  public byte SolidA { get; set; } = 255;
  public List<ChannelPack> Packing { get; } = new();
  /// <summary>Stored texture format, for example <c>DXN</c> (BC5) or <c>DXT5A</c> (BC4).</summary>
  public string Format { get; set; } = "";
  /// <summary><see cref="Channels"/> with physical channels, for example <c>MetGray(r)->r+Roughness(a)->g</c>.</summary>
  public string StoredChannels { get; set; } = "";
}

/// <param name="Stored">Physical channel in the exported image, when the stored format moved it (BC5 a -> g, BC4 a -> r).</param>
public readonly record struct ChannelPack(string Channel, string Meaning, string Stored = "");

/// <summary>
///   SM2 draws a part with <c>shadingMtl_Tex</c> (the layer <c>texName</c>). That file's
///   <c>.td</c> is the material: <c>materials.&lt;variant&gt;.shaders.glt.tex</c> names NM, spec,
///   diffOverride, det, and the other maps, and <c>convert_settings.format_descr</c> says
///   which channel is which. Index's mesh builder always also asks for <c>{diffuse}_nm</c>
///   and <c>{diffuse}_spec</c>. A full game keeps shared textures (purity seals, eyelashes,
///   <c>sc_grey_90</c>) in other paks; resolution searches every archive in the folder.
/// </summary>
public static class MaterialMaps
{
  private static readonly Regex Assignment = new(
    "([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*\"([^\"]*)\"",
    RegexOptions.Compiled);

  private static readonly Regex FormatDescr = new(
    "format_descr\\s*=\\s*\"([^\"]+)\"",
    RegexOptions.Compiled);

  private static readonly Regex ChannelSpec = new(
    "([A-Za-z]+)\\(([rgba]+)\\)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

  private static readonly Regex GreyName = new(
    "^sc_grey_(\\d+)$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

  public static void Attach(GameCatalog catalog, ArchiveStore store, int hint, ExportedMesh mesh, ICollection<string>? notes = null)
  {
    var cache = new Dictionary<string, List<MaterialMap>>(StringComparer.OrdinalIgnoreCase);
    var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var variants = new Dictionary<string, (float[]? Tint, string? Mode, SecondSpec? Second, AnisoSpec? Aniso, BaseLayer Base)>(StringComparer.OrdinalIgnoreCase);
    Attach(mesh.Parts);
    foreach (var level in mesh.Levels)
      Attach(level.Parts);

    void Attach(IList<MeshPart> parts)
    {
      foreach (var part in parts)
      {
        if (string.IsNullOrWhiteSpace(part.Texture))
          continue;
        var key = Stem(part.Texture) + "\n" + part.Variant;
        if (!cache.TryGetValue(key, out var maps))
        {
          maps = Build(catalog, store, hint, part.Texture, part.Variant, texts);
          cache[key] = maps;
        }

        part.Maps.Clear();
        part.Maps.AddRange(maps);
        var diffuse = maps.FirstOrDefault(map => map.Role == "diffuse" && !map.Missing);
        if (diffuse is not null)
          part.Texture = diffuse.Name;
        part.Normal = FirstPresent(maps, "normal");
        part.Specular = FirstPresent(maps, "specular");
        part.Emissive = FirstPresent(maps, "emissive");
        part.SpecularChannels = maps.FirstOrDefault(map => map.Role == "specular")?.Channels ?? "";
        if (!variants.TryGetValue(key, out var shading))
        {
          var text = DefinitionText(catalog, store, hint, key[..key.IndexOf('\n')], texts);
          var block = text is null ? null : VariantBlock(text, part.Variant);
          shading = (
            block is null ? null : AlbedoTint(block),
            block is null ? null : BlendMode(block),
            block is null ? null : SpecLayers.Second(block),
            block is null ? null : SpecLayers.Aniso(block),
            block is null ? new BaseLayer() : SpecLayers.Base(block));
          variants[key] = shading;
        }

        var tint = shading.Tint;
        part.BlendMode = shading.Mode ?? "opaque";
        part.Blend = BlendOf(maps.FirstOrDefault(map => map.Role == "diffuse"), shading.Mode);
        part.AlbedoTint = tint;
        part.SecondSpec = shading.Second;
        part.AnisoSpec = shading.Aniso;
        part.BaseLayer = shading.Base;
      }
    }

    if (notes is null)
      return;
    var unique = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts))
      .Select(part => part.Texture)
      .Where(name => !string.IsNullOrWhiteSpace(name))
      .Select(name => Stem(name!))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
    var missing = unique.Where(name =>
    {
      var part = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts))
        .FirstOrDefault(candidate => string.Equals(Stem(candidate.Texture ?? ""), name, StringComparison.OrdinalIgnoreCase));
      var diffuse = part?.Maps.FirstOrDefault(map => map.Role == "diffuse");
      return diffuse is null || (diffuse.Missing && !diffuse.Generated);
    }).ToList();
    if (missing.Count > 0)
      notes.Add("No pixels in the open archives for " + string.Join(", ", missing) + ".");
  }

  public static bool TrySolid(string name, out byte r, out byte g, out byte b, out byte a)
  {
    r = g = b = 0;
    a = 255;
    var stem = Stem(name);
    var grey = GreyName.Match(stem);
    if (grey.Success && int.TryParse(grey.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
    {
      var value = (byte)Math.Clamp((int)Math.Round(percent / 100.0 * 255.0), 0, 255);
      r = g = b = value;
      return true;
    }

    if (stem.Equals("sc_white", StringComparison.OrdinalIgnoreCase)
      || stem.Equals("__white__", StringComparison.OrdinalIgnoreCase))
    {
      r = g = b = 255;
      return true;
    }

    if (stem.Equals("sc_black", StringComparison.OrdinalIgnoreCase)
      || stem.Equals("__black__", StringComparison.OrdinalIgnoreCase))
      return true;

    if (stem.Equals("__black_alpha_one__", StringComparison.OrdinalIgnoreCase))
      return true;

    if (stem.Equals("__default_material__", StringComparison.OrdinalIgnoreCase)
      || stem.Equals("sc_grey", StringComparison.OrdinalIgnoreCase))
    {
      r = g = b = 128;
      return true;
    }

    return false;
  }

  internal static string Channels(string text)
  {
    var match = FormatDescr.Match(text);
    return match.Success ? match.Groups[1].Value : "";
  }

  /// <summary>
  ///   Fills <see cref="MaterialMap.Format"/> and where each logical channel physically sits.
  ///   BC5 keeps r,g and BC4 keeps r; the logical channels fill those in order
  ///   (MetGray(r)+Roughness(a) on DXN is r,g; AO(a) on DXT5A is r). Normals stay rgb.
  /// </summary>
  internal static void ResolveStorage(GameCatalog catalog, ArchiveStore store, int textureIndex, MaterialMap map)
  {
    SpaceMarine2.Enumerations.SM2TextureFormat format;
    try
    {
      format = TextureLoader.Load(catalog, store, textureIndex).Format;
    }
    catch
    {
      return;
    }

    map.Format = format.ToString();
    var physical = TextureInfo.StoredChannels(format);
    var next = 0;
    var parts = new List<string>();
    for (var i = 0; i < map.Packing.Count; i++)
    {
      var pack = map.Packing[i];
      var stored = pack.Channel;
      if (physical is not null && pack.Meaning is not ("normal" or "detnormal"))
      {
        if (pack.Channel.Length == 1 && next < physical.Length)
          stored = physical[next].ToString();
        next += pack.Channel.Length;
      }

      map.Packing[i] = pack with { Stored = stored };
      parts.Add(pack.Meaning + "(" + pack.Channel + ")->" + stored);
    }

    map.StoredChannels = string.Join("+", parts);
  }

  internal static List<ChannelPack> Pack(string channels)
  {
    var list = new List<ChannelPack>();
    foreach (Match match in ChannelSpec.Matches(channels))
      list.Add(new ChannelPack(match.Groups[2].Value.ToLowerInvariant(), Meaning(match.Groups[1].Value)));
    return list;
  }

  internal static bool IsSrgb(IReadOnlyList<ChannelPack> packing)
    => packing.Count == 0 || packing.Any(channel => channel.Meaning is "albedo" or "emissive");

  private static readonly Regex AlbedoTintSpec = new(
    @"albedo_tint\s*=\s*\[\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\]",
    RegexOptions.Compiled);

  /// <summary>
  ///   <c>shaders.glt.albedo_tint = [A, R, G, B]</c> (the same byte order as <c>second_spec.metallicTint</c>,
  ///   whose [255, 252, 204, 133] is gold). Returned as R, G, B, A in 0..1, or null when absent.
  /// </summary>
  internal static float[]? AlbedoTint(string variantBlock)
  {
    var match = AlbedoTintSpec.Match(variantBlock);
    if (!match.Success)
      return null;
    float Byte(int group) => Math.Clamp(int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture), 0, 255) / 255f;
    return new[] { Byte(2), Byte(3), Byte(4), Byte(1) };
  }

  internal static string? VariantBlock(string text, string variant)
  {
    var materials = Block(text, "materials");
    if (materials is null)
      return null;
    return Block(materials, string.IsNullOrWhiteSpace(variant) ? "default" : variant)
      ?? Block(materials, "default");
  }

  private static List<MaterialMap> Build(
    GameCatalog catalog,
    ArchiveStore store,
    int hint,
    string texture,
    string variant,
    Dictionary<string, string> texts)
  {
    var stem = Stem(texture);
    var text = DefinitionText(catalog, store, hint, stem, texts);
    var block = text is null ? null : VariantBlock(text, variant);
    var named = block is null ? new List<(string Role, string Name)>() : NamedMaps(block);
    var diffuseName = named.FirstOrDefault(pair => pair.Role == "diffuse").Name;
    if (string.IsNullOrWhiteSpace(diffuseName) || TemplateMeshReader.ResolveTexture(catalog, hint, diffuseName) < 0)
      diffuseName = stem;

    var maps = new List<MaterialMap>();
    maps.Add(Describe(catalog, store, hint, "diffuse", diffuseName, texts, allowSolid: true));
    foreach (var pair in named)
    {
      if (pair.Role == "diffuse")
        continue;
      if (maps.Any(map => map.Role == pair.Role))
        continue;
      maps.Add(Describe(catalog, store, hint, pair.Role, pair.Name, texts, allowSolid: false));
    }

    void Sibling(string role, string suffix, Func<string, bool> accept)
    {
      if (maps.Any(map => map.Role == role && !map.Missing))
        return;
      var name = diffuseName + suffix;
      if (TemplateMeshReader.ResolveTexture(catalog, hint, name) < 0)
        return;
      var described = Describe(catalog, store, hint, role, name, texts, allowSolid: false);
      // A sibling file is only that role when its format_descr agrees. ch_titus_hair_ao
      // is Diff(rgb) and no .td references it; an _em file can be AO(a) instead of RGBEm.
      if (described.Channels.Length > 0 && !accept(described.Channels))
        return;
      maps.RemoveAll(map => map.Role == role);
      maps.Add(described);
    }

    bool Has(string channels, string token)
      => channels.Contains(token, StringComparison.OrdinalIgnoreCase);

    // Index's MeshBuilder always binds {diffuse}_nm and {diffuse}_spec. The default
    // .td variant often omits them and only overrides NM/spec for damaged and titlescene.
    Sibling("normal", "_nm", channels => Has(channels, "Normal"));
    Sibling("specular", "_spec", channels => Has(channels, "Metal") || Has(channels, "Rough") || Has(channels, "Spec"));
    Sibling("emissive", "_em", channels => Has(channels, "RGBEm") || Has(channels, "AO("));
    Sibling("detailMask", "_hdetm", channels => Has(channels, "DetMask") || Has(channels, "Heightmap"));
    Sibling("ao", "_ao", channels => Has(channels, "AO("));
    foreach (var map in maps)
    {
      // Several _em siblings are AO(a), not an emissive color. The format string wins.
      if (map.Role == "emissive"
        && map.Channels.Contains("AO(", StringComparison.OrdinalIgnoreCase)
        && !map.Channels.Contains("RGBEm", StringComparison.OrdinalIgnoreCase))
        map.Role = "ao";
    }

    return maps;
  }

  private static MaterialMap Describe(
    GameCatalog catalog,
    ArchiveStore store,
    int hint,
    string role,
    string name,
    Dictionary<string, string> texts,
    bool allowSolid)
  {
    var stem = Stem(name);
    var text = DefinitionText(catalog, store, hint, stem, texts);
    var channels = text is null ? "" : Channels(text);
    var packing = Pack(channels);
    if (packing.Count == 0)
      packing = Pack(ImpliedChannels(role));
    var map = new MaterialMap
    {
      Role = role,
      Name = stem,
      Channels = channels.Length == 0 ? ImpliedChannels(role) : channels,
      // anisoDir _dm maps say Diff(rgb) but hold a direction (usage "MD", fade color 127 = zero vector), so linear.
      Srgb = role != "anisoDir" && IsSrgb(packing.Count == 0 ? Pack(ImpliedChannels(role)) : packing),
    };
    map.Packing.AddRange(packing.Count == 0 ? Pack(map.Channels) : packing);
    var textureIndex = TemplateMeshReader.ResolveTexture(catalog, hint, stem);
    if (textureIndex >= 0)
    {
      ResolveStorage(catalog, store, textureIndex, map);
      return map;
    }
    if (allowSolid && TrySolid(stem, out var r, out var g, out var b, out var a))
    {
      map.Generated = true;
      map.SolidR = r;
      map.SolidG = g;
      map.SolidB = b;
      map.SolidA = a;
      map.Channels = map.Channels.Length == 0 ? "Diff(rgb)" : map.Channels;
      if (map.Packing.Count == 0)
        map.Packing.Add(new ChannelPack("rgb", "albedo"));
      map.Srgb = true;
      return map;
    }

    map.Missing = true;
    return map;
  }

  private static string ImpliedChannels(string role)
    => role switch
    {
      "normal" or "detail" or "wrinkleNormal" => "Normalmap(rgb)",
      "specular" => "Metalness(r)+Roughness(g)+AO(b)",
      "emissive" => "RGBEm(rgb)",
      "detailMask" => "DetMask(g)",
      "ao" => "AO(a)",
      "height" => "Heightmap(r)",
      _ => "Diff(rgb)",
    };

  private static string? FirstPresent(List<MaterialMap> maps, string role)
  {
    var map = maps.FirstOrDefault(candidate => candidate.Role == role && !candidate.Missing);
    return map?.Name;
  }

  private static readonly Regex BlendModeSpec = new(@"\bblendMode\s*=\s*""([A-Za-z_]+)""", RegexOptions.Compiled);

  /// <summary><c>shaders.glt.blendMode</c> of a variant block, or null (the engine default, <c>opaque</c>).</summary>
  internal static string? BlendMode(string variantBlock)
  {
    var match = BlendModeSpec.Match(variantBlock);
    return match.Success ? match.Groups[1].Value : null;
  }

  /// <summary>
  ///   The exe's glt blend-mode enum is opaque, blend, add, add_no_alpha, add_smooth_no_alpha,
  ///   premul_a, mul, mul_inv (string table at 0x144cc9190, default opaque). A variant is only
  ///   translucent when its glt.blendMode says so: ch_titus_head is Diff(rgb)+Transp(a) but its
  ///   default (skin) variant has no blendMode and draws opaque; only its hair variant is "blend".
  ///   AKill in format_descr is alpha test (akill, 0x14364a800) and is independent of blendMode.
  /// </summary>
  private static string BlendOf(MaterialMap? diffuse, string? blendMode)
  {
    if (blendMode is not null && !blendMode.Equals("opaque", StringComparison.OrdinalIgnoreCase))
      return "translucent";
    if (diffuse is not null && diffuse.Channels.Contains("AKill", StringComparison.OrdinalIgnoreCase))
      return "masked";
    return "opaque";
  }


  private static List<(string Role, string Name)> NamedMaps(string block)
  {
    var list = new List<(string Role, string Name)>();
    foreach (Match match in Assignment.Matches(block))
    {
      var key = match.Groups[1].Value;
      var value = match.Groups[2].Value.Trim();
      if (value.Length == 0 || !LooksLikeTexture(value))
        continue;
      var role = RoleOf(key);
      if (role is null)
        continue;
      list.Add((role, value));
    }

    return list;
  }

  private static string? RoleOf(string key)
    => key switch
    {
      "diffOverride" => "diffuse",
      "NM" or "nm" or "texNM" => "normal",
      "spec" => "specular",
      "emissiveTex" => "emissive",
      "det" => "detail",
      "coveringMask" => "detailMask",
      "mask" => "colorMask",
      "texArrayNormals" => "wrinkleNormal",
      "texArrayMasks" => "wrinkleMask",
      "texArraySpec" => "specular",
      "texArrayEmissiveMask" => "emissive",
      "texArrayDiffuse" => "diffuseArray",
      // anisotropic_spec.dirMap (FUN_142579ff0 +8): hair flow map, bound as GLT_TEX_ANISO_DIR (0x143729de0).
      "dirMap" => "anisoDir",
      _ => null,
    };

  private static bool LooksLikeTexture(string value)
  {
    if (value is "default" or "wrap" or "clamp" or "blend" or "true" or "false" or "normal")
      return false;
    foreach (var c in value)
    {
      if (c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '.' or '/'))
        return false;
    }

    return value.Contains('_') || value.Contains('/') || value.Contains('.');
  }

  private static string? DefinitionText(
    GameCatalog catalog,
    ArchiveStore store,
    int hint,
    string stem,
    Dictionary<string, string> texts)
  {
    if (texts.TryGetValue(stem, out var cached))
      return cached.Length == 0 ? null : cached;

    string? text = null;
    foreach (var candidate in new[] { stem + ".td", "td/" + stem + ".td", stem + ".td.resource", "td/" + stem + ".td.resource" })
    {
      var index = catalog.ResolveReference(candidate, hint);
      if (index < 0)
        continue;
      byte[] bytes;
      try
      {
        bytes = store.Read(index);
      }
      catch (Exception)
      {
        continue;
      }
      if (!TextureLoader.LooksText(bytes))
        continue;
      var body = Encoding.UTF8.GetString(bytes);
      if (body.Contains("format_descr", StringComparison.Ordinal))
      {
        text = body;
        break;
      }

      var pointed = PointedTd(body);
      if (pointed is null)
        continue;
      var nested = catalog.ResolveReference(pointed, index);
      if (nested < 0)
        continue;
      var nestedBytes = store.Read(nested);
      if (!TextureLoader.LooksText(nestedBytes))
        continue;
      text = Encoding.UTF8.GetString(nestedBytes);
      break;
    }

    texts[stem] = text ?? "";
    return text;
  }

  private static string? PointedTd(string resource)
  {
    foreach (var line in resource.Split('\n'))
    {
      var trimmed = line.Trim();
      if (!trimmed.StartsWith("td:", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("td =", StringComparison.OrdinalIgnoreCase))
        continue;
      var value = trimmed[(trimmed.IndexOf(':') >= 0 && trimmed.StartsWith("td:", StringComparison.OrdinalIgnoreCase) ? trimmed.IndexOf(':') + 1 : trimmed.IndexOf('=') + 1)..].Trim().Trim('"');
      if (value.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        value = value[6..];
      if (value.Length > 0)
        return value;
    }

    return null;
  }

  private static string? Block(string text, string key)
  {
    var pattern = "(^|\\n)\\s*" + Regex.Escape(key) + "\\s*=\\s*\\{";
    var match = Regex.Match(text, pattern);
    if (!match.Success)
      return null;
    var open = text.IndexOf('{', match.Index);
    if (open < 0)
      return null;
    var depth = 0;
    for (var i = open; i < text.Length; i++)
    {
      if (text[i] == '{')
        depth++;
      else if (text[i] == '}')
      {
        depth--;
        if (depth == 0)
          return text[open..(i + 1)];
      }
    }

    return null;
  }

  internal static string Stem(string name)
  {
    var file = name.Replace('\\', '/').Trim();
    if (file.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
      file = file[6..];
    var slash = file.LastIndexOf('/');
    if (slash >= 0)
      file = file[(slash + 1)..];
    if (file.EndsWith(".pct.resource", StringComparison.OrdinalIgnoreCase))
      file = file[..^".pct.resource".Length];
    else if (file.EndsWith(".td.resource", StringComparison.OrdinalIgnoreCase))
      file = file[..^".td.resource".Length];
    else if (file.EndsWith(".pct", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".td", StringComparison.OrdinalIgnoreCase))
      file = file[..^file.LastIndexOf('.')];
    return file;
  }

  private static string Meaning(string token)
    => token.ToLowerInvariant() switch
    {
      "diff" => "albedo",
      "akill" => "alphaTest",
      "transp" => "opacity",
      "normalmap" => "normal",
      "metgray" or "metalness" => "metalness",
      "roughness" => "roughness",
      "ao" => "ao",
      "rgbem" => "emissive",
      "detmask" => "detailMask",
      "heightmap" => "height",
      _ => token.ToLowerInvariant(),
    };

  internal static List<(string Name, MeshPart Part)> Slots(ExportedMesh mesh)
  {
    var list = new List<(string, MeshPart)>();
    var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var partIndex = 0;
    foreach (var part in mesh.Parts)
    {
      if (part.IndexCount < 3)
        continue;
      var baseName = string.IsNullOrWhiteSpace(part.Name) ? "part" + partIndex : part.Name;
      var name = baseName;
      var suffix = 2;
      while (!used.Add(name))
        name = baseName + "_" + suffix++;
      list.Add((name, part));
      partIndex++;
    }

    return list;
  }
}
