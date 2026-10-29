using System.Globalization;
using System.Text;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>
///   mtl1-52: one armour customization layer (a <c>CustomizationLayerParamsProject</c> entry of
///   <c>armor_color_library.sso</c>). Colours are sRGB bytes as stored; <see cref="ArmorSchemeBake"/> linearises them.
/// </summary>
public sealed record ArmorLayer(
  string Slot, string Colour, string Name,
  byte TintR, byte TintG, byte TintB, byte TintA,
  byte MetalR, byte MetalG, byte MetalB,
  byte PatternR, byte PatternG, byte PatternB, byte PatternA,
  bool Metallic, float MetalnessScale, float MetalnessBias, float RoughnessScale, float RoughnessBias, int TextureIndex, string Source)
{
  public string Hex => $"#{TintR:X2}{TintG:X2}{TintB:X2}";

  /// <summary>rea-batch 2: <c>density</c> (glt record +4, UV multiplier; default 1).</summary>
  public float Density { get; init; } = 1f;

  /// <summary>rea-batch 2: <c>scrollScale</c> (glt record +16: v += scrollScale × time; default 0).</summary>
  public float ScrollScale { get; init; }

  /// <summary>rea-batch 2: <c>maps_normal / maps_spec / maps_emissive</c> slice indices (glt record +0+L .y/.z/.w; -1 = none).</summary>
  public int MapNormal { get; init; } = -1;
  public int MapSpec { get; init; } = -1;
  public int MapEmissive { get; init; } = -1;
  public float NormalScale { get; init; } = 1f;

  /// <summary>rea-batch 2: <c>emissive_tint</c> (sRGB bytes; record +6+L rgb), <c>emissive_intensity</c> (record +6+L w), bloom.</summary>
  public byte EmissiveR { get; init; } = 255;
  public byte EmissiveG { get; init; } = 255;
  public byte EmissiveB { get; init; } = 255;
  public float EmissiveIntensity { get; init; }
  public float EmissiveBloom { get; init; }

  public string EmissiveHex => $"#{EmissiveR:X2}{EmissiveG:X2}{EmissiveB:X2}";
}

/// <summary>mtl1-52: one character colour scheme (<c>global_armor_color_pattern_library.sso</c> characterColoringInfoCollection entry).</summary>
/// <param name="Id">Collection key, e.g. <c>Blood_Angels</c> (the value <c>SM2_COLOUR</c> / the picker use).</param>
/// <param name="Name">In-game name (ui_heraldry_library chapters.&lt;Id&gt;.patternName → strings_english), or a prettified id.</param>
/// <param name="NameKey">String key, or "HEURISTIC: ..." when the UI library has no entry.</param>
/// <param name="Main">layer0..2 = primary, secondary, tertiary (mainColoring, engine defaults when unset).</param>
/// <param name="Sections">bodySectionsColoring overrides by section (CHEST, HELMET, ...).</param>
public sealed record ArmorScheme(string Id, string Name, string LocalName, string NameKey, string Group, ArmorLayer[] Main,
  IReadOnlyDictionary<string, ArmorLayer[]> Sections, ArmorLayer? Lenses)
{
  public ArmorLayer Swatch => Main[0];
}

/// <summary>
///   mtl1-52: character (armour) colour schemes as the game's heraldry screen lists them.
///   <list type="bullet">
///     <item>Schemes: <c>ssl/body/customization/global_armor_color_pattern_library.sso</c> characterColoringInfoCollection
///       (PROVEN data: <c>mainColoring.colors{primary,secondary,tertiary}</c>, <c>bodySectionsColoring.&lt;SECTION&gt;</c>).
///       Unset colours take the <c>customization_body_coloring_info.sso</c> defaults Macragge_Blue / Macragge_Blue /
///       Liberator_Gold (PROVEN data; Ultramarines has no mainColoring).</item>
///     <item>Colours: <c>armor_color_library.sso</c> primaryShaderLayers / secondaryShaderLayers / tertiaryShaderLayers
///       by slot (color_set_types_desc_armor: PRIMARY→ARMOR_PRIMARY, SECONDARY→ARMOR_SECONDARY, TERTIARY→ARMOR_METAL), lenses from
///       lensesShaderLayers. Field defaults from <c>customization_layer_params_project.sso</c>.</item>
///     <item>Names: <c>ssl/ui/screens/codex/ui_heraldry_library.sso</c> chapters.&lt;Id&gt;.patternName (PROVEN key), colour names
///       <c>colors.&lt;Colour&gt;</c> → UI_HERALDRY_COLOR_*_NAME.</item>
///   </list>
/// </summary>
public static class ArmorColourSchemes
{
  public const string SchemesPath = "ssl/body/customization/global_armor_color_pattern_library.sso";
  public const string UiLibraryPath = "ssl/ui/screens/codex/ui_heraldry_library.sso";
  public const string HeraldryLibraryPath = "ssl/body/customization/heraldry_progression/heraldry_library.sso";

  /// <summary>customization_body_coloring_info.sso field defaults (PROVEN data).</summary>
  public static readonly string[] DefaultColours = { "Macragge_Blue", "Macragge_Blue", "Liberator_Gold" };

  public static readonly string[] Slots = { "primary", "secondary", "tertiary" };

  private static readonly string[] SlotSections = { "primaryShaderLayers", "secondaryShaderLayers", "tertiaryShaderLayers" };

  /// <summary>Parses the palette into section → colour → layer (sections keep their sso names).</summary>
  public static Dictionary<string, Dictionary<string, ArmorLayer>> ParseLayers(string paletteText)
  {
    var result = new Dictionary<string, Dictionary<string, ArmorLayer>>(StringComparer.OrdinalIgnoreCase);
    var root = SslText.Parse(paletteText);
    foreach (var (section, sv) in root)
    {
      if (sv is not Dictionary<string, object?> entries || !section.EndsWith("ShaderLayers", StringComparison.OrdinalIgnoreCase))
        continue;
      var map = new Dictionary<string, ArmorLayer>(StringComparer.OrdinalIgnoreCase);
      foreach (var (name, ev) in entries)
      {
        if (ev is Dictionary<string, object?> e)
          map[name] = Layer(section, name, e);
      }

      result[section] = map;
    }

    return result;
  }

  private static ArmorLayer Layer(string section, string name, Dictionary<string, object?> e)
  {
    (byte R, byte G, byte B, byte A) Color(string key, (byte, byte, byte, byte) fallback)
    {
      if (SslText.Obj(e, key, "value") is not { } v)
        return fallback;
      static byte B(object? o, byte d) => o is null ? d : (byte)Math.Clamp(Convert.ToInt32(Convert.ToDouble(o, CultureInfo.InvariantCulture)), 0, 255);
      return (B(v.GetValueOrDefault("r"), 255), B(v.GetValueOrDefault("g"), 255), B(v.GetValueOrDefault("b"), 255), B(v.GetValueOrDefault("a"), 255));
    }

    float F(string key, float d)
    {
      if (SslText.Obj(e, key) is { } o && o.GetValueOrDefault("value") is { } v)
        return (float)Convert.ToDouble(v, CultureInfo.InvariantCulture);
      return d;
    }

    // customization_layer_params_project.sso defaults: tint 255x4, patternTint a=0 rgb 255, spec_metalnessTint 255, scale 1 bias 0
    var tint = Color("tint", (255, 255, 255, 255));
    var metal = Color("spec_metalnessTint", (255, 255, 255, 255));
    var pattern = Color("patternTint", (255, 255, 255, 0));
    var metallic = e.GetValueOrDefault("isMetallic") is { } m && (m is bool b ? b : string.Equals(m.ToString(), "True", StringComparison.OrdinalIgnoreCase));
    var slot = section.Replace("ShaderLayers", "", StringComparison.OrdinalIgnoreCase);
    var emissive = Color("emissive_tint", (255, 255, 255, 255));
    return new ArmorLayer(slot, name, Pretty(name), tint.R, tint.G, tint.B, tint.A, metal.R, metal.G, metal.B, pattern.R, pattern.G, pattern.B, pattern.A,
      metallic, F("spec_metalness_scale", 1), F("spec_metalness_bias", 0), F("spec_roughness_scale", 1), F("spec_roughness_bias", 0), (int)F("textureIndex", -1),
      $"armor_color_library.sso {section}.{name}")
    {
      // rea-batch 2: the rest of CustomizationLayerParamsProject (defaults: customization_layer_params_project.sso, PROVEN data)
      Density = F("density", 1), ScrollScale = F("scrollScale", 0), NormalScale = F("normalScale", 1),
      MapNormal = (int)F("maps_normal", -1), MapSpec = (int)F("maps_spec", -1), MapEmissive = (int)F("maps_emissive", -1),
      EmissiveR = emissive.R, EmissiveG = emissive.G, EmissiveB = emissive.B, EmissiveIntensity = F("emissive_intensity", 0), EmissiveBloom = F("emissive_bloomIntensity", 0),
    };
  }

  /// <summary>Scheme list in heraldry-screen order (heraldry_library legions → nodes, then the rest in sso order).</summary>
  public static List<ArmorScheme> Parse(string schemesText, string paletteText, string? uiLibraryText, string? heraldryLibraryText,
    IReadOnlyDictionary<string, string> strings, IReadOnlyDictionary<string, string>? localStrings)
  {
    var layers = ParseLayers(paletteText);
    var root = SslText.Parse(schemesText);
    var ui = uiLibraryText is null ? null : SslText.Parse(uiLibraryText);
    var chapters = SslText.Obj(ui, "chapters");
    var colourNames = SslText.Obj(ui, "colors");

    string ColourName(string colour)
    {
      if (colourNames?.GetValueOrDefault(colour) is Dictionary<string, object?> c && c.GetValueOrDefault("__value") is string key && strings.TryGetValue(key, out var v) && v.Length > 0)
        return v;
      if (strings.TryGetValue($"UI_HERALDRY_COLOR_{colour.ToUpperInvariant()}_NAME", out var v2) && v2.Length > 0)
        return v2;
      return Pretty(colour);
    }

    ArmorLayer Resolve(int slot, string? colour, string section)
    {
      colour ??= DefaultColours[slot];
      var sec = layers.GetValueOrDefault(section);
      if (sec?.GetValueOrDefault(colour) is { } l)
        return l with { Slot = Slots.ElementAtOrDefault(slot) ?? l.Slot, Name = ColourName(colour) };
      // colour not in this slot's section: first section that has it (HEURISTIC fallback, rare)
      foreach (var (sn, s) in layers)
        if (s.GetValueOrDefault(colour) is { } any)
          return any with { Slot = Slots.ElementAtOrDefault(slot) ?? any.Slot, Name = ColourName(colour), Source = "HEURISTIC fallback " + any.Source + $" (not in {section})" };
      return new ArmorLayer(Slots.ElementAtOrDefault(slot) ?? "?", colour, ColourName(colour), 128, 128, 128, 255, 128, 128, 128, 255, 255, 255, 0, false, 1, 0, 1, 0, -1, "missing in armor_color_library.sso");
    }

    ArmorLayer[] Colours(Dictionary<string, object?>? coloring)
    {
      var colors = SslText.Obj(coloring, "colors");
      var result = new ArmorLayer[3];
      for (var i = 0; i < 3; i++)
        result[i] = Resolve(i, colors?.GetValueOrDefault(Slots[i]) as string, SlotSections[i]);
      return result;
    }

    // order + group (legion) from the heraldry library
    var order = new List<(string Id, string Group)>();
    if (heraldryLibraryText is not null && SslText.Obj(SslText.Parse(heraldryLibraryText), "legions") is { } legions)
    {
      foreach (var (legion, lv) in legions)
      {
        if (lv is not Dictionary<string, object?> l || l.GetValueOrDefault("nodes") is not List<object?> nodes)
          continue;
        foreach (var n in nodes)
          if (n is Dictionary<string, object?> node && node.GetValueOrDefault("chapterUid") is string uid && order.All(o => !o.Id.Equals(uid, StringComparison.OrdinalIgnoreCase)))
            order.Add((uid, legion));
      }
    }

    var collection = SslText.Obj(root, "characterColoringInfoCollection") ?? new Dictionary<string, object?>();
    var list = new List<ArmorScheme>();
    foreach (var (id, value) in collection)
    {
      if (value is not Dictionary<string, object?> info)
        continue;
      var main = Colours(SslText.Obj(info, "mainColoring"));
      var sections = new Dictionary<string, ArmorLayer[]>(StringComparer.OrdinalIgnoreCase);
      ArmorLayer? lenses = null;
      if (SslText.Obj(info, "bodySectionsColoring") is { } bsc)
      {
        foreach (var (section, sv) in bsc)
        {
          if (sv is not Dictionary<string, object?> coloring)
            continue;
          if (section.Equals("HELMET_LENSES", StringComparison.OrdinalIgnoreCase))
          {
            var c = SslText.Obj(coloring, "colors")?.GetValueOrDefault("primary") as string ?? "Mephiston_Red";
            lenses = layers.GetValueOrDefault("lensesShaderLayers")?.GetValueOrDefault(c) is { } ll ? ll with { Slot = "lenses", Name = ColourName(c) } : null;
            continue;
          }

          if (section.Equals("CLOTH", StringComparison.OrdinalIgnoreCase) || section.Equals("COAT", StringComparison.OrdinalIgnoreCase))
            continue;
          // a section colouring starts from the field defaults, not from mainColoring (PROVEN data: CHEST lists all three)
          sections[section] = Colours(coloring);
        }
      }

      string name, key, local = "";
      if (chapters?.GetValueOrDefault(id) is Dictionary<string, object?> ch && (ch.GetValueOrDefault("patternName") ?? ch.GetValueOrDefault("chapterName")) is string k && strings.TryGetValue(k, out var v) && v.Length > 0)
      {
        name = Title(v);
        key = k;
        if (localStrings?.TryGetValue(k, out var lv) == true)
          local = lv;
      }
      else
      {
        name = Pretty(id);
        key = "HEURISTIC: prettified id (no ui_heraldry_library chapters entry)";
      }

      var group = order.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Group ?? "";
      list.Add(new ArmorScheme(id, name, local, key, group, main, sections, lenses));
    }

    var rank = order.Select((o, i) => (o.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.OrdinalIgnoreCase);
    var defaultId = root.GetValueOrDefault("defaultColoringLink") as string ?? "Ultramarines";
    return list.Select((s, i) => (s, i))
      .OrderBy(p => p.s.Id.Equals(defaultId, StringComparison.OrdinalIgnoreCase) ? -1 : rank.TryGetValue(p.s.Id, out var r) ? r : 10000 + p.i)
      .Select(p => p.s).ToList();
  }

  /// <summary>"ULTRAMARINES" → "Ultramarines", "DOOM EAGLES" → "Doom Eagles" (the UI shows upper case; the picker title-cases).</summary>
  public static string Title(string s)
  {
    if (s.Any(char.IsLower))
      return s;
    return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
  }

  public static string Pretty(string id) => string.Join(" ", id.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));

  /// <summary>Body section of a part (HEURISTIC: affix token in the part / texture name; character_body_section_library affixes).</summary>
  public static string? SectionOf(string partName, string? texture)
  {
    var s = (partName + " " + texture).ToLowerInvariant();
    if (s.Contains("helmet") || s.Contains("_helm")) return "HELMET";
    if (s.Contains("backpack")) return "BACKPACK";
    if (s.Contains("chest") || s.Contains("torso")) return "CHEST";
    if (s.Contains("shield")) return "VANITY";
    return null;
  }

  /// <summary>Layers for a part: section override when the part's section has one, else mainColoring.</summary>
  public static ArmorLayer[] LayersFor(ArmorScheme scheme, string partName, string? texture)
    => SectionOf(partName, texture) is { } sec && scheme.Sections.TryGetValue(sec, out var l) ? l : scheme.Main;
}

/// <summary>
///   mtl1-52: the customization part of a td variant: <c>materials."&lt;variant&gt; CC".shaders.glt.customization</c>
///   (mask, texArray*, layer0..3 {tintBlendMode, enabled}) and its <c>tex.diffOverride</c> (the neutral <c>_a</c> albedo).
/// </summary>
public sealed record CustomizationSlot(string Variant, string Mask, string? DiffOverride, string[] BlendModes, bool[] Enabled, string? TexArrayDiffuse)
{
  /// <summary>rea-batch: per-layer <c>blendMode</c> (pattern mode: tint_only / tex_hardlight / tex_blend), null when absent.</summary>
  public string?[] PatternModes { get; init; } = new string?[4];

  /// <summary>rea-batch 2: <c>texArrayNormals / texArraySpec / texArrayEmissiveMask</c> (glt _58._m0[11] .y/.z/.w); null or empty = none.</summary>
  public string? TexArrayNormals { get; init; }
  public string? TexArraySpec { get; init; }
  public string? TexArrayEmissiveMask { get; init; }

  /// <summary>
  ///   rea-batch 2: td <c>layerN.density</c> — INFERRED to be CB_STATIC_GLT[176+L], the second UV factor of the pattern lookup
  ///   (uv = TEXCOORD × record density × CB[176+L]); 1 when absent (HEURISTIC default).
  /// </summary>
  public float[] LayerDensity { get; init; } = { 1f, 1f, 1f, 1f };

  /// <summary>rea-batch 2: td <c>layerN.useCustomUVSet</c> — INFERRED to be the per-layer bit that switches the lookup to TEXCOORD_16 (UV set 2).</summary>
  public bool[] UseCustomUVSet { get; init; } = new bool[4];

  /// <summary>Reads <c>"&lt;variant&gt; CC"</c> (falling back to <c>"default CC"</c>); null when the td has no customization variant.</summary>
  public static CustomizationSlot? FromTd(string tdText, string? variant)
  {
    var root = SslText.Parse(tdText);
    if (SslText.Obj(root, "materials") is not { } materials)
      return null;
    var v = string.IsNullOrWhiteSpace(variant) ? "default" : variant!;
    foreach (var name in new[] { v + " CC", "default CC" })
    {
      if (materials.GetValueOrDefault(name) is not Dictionary<string, object?> block)
        continue;
      var glt = SslText.Obj(block, "shaders", "glt");
      if (SslText.Obj(glt, "customization") is not { } cust || cust.GetValueOrDefault("mask") is not string mask || mask.Length == 0)
        continue;
      var modes = new string[4];
      var patternModes = new string?[4];
      var enabled = new bool[4];
      var density = new[] { 1f, 1f, 1f, 1f };
      var customUv = new bool[4];
      for (var i = 0; i < 4; i++)
      {
        var layer = SslText.Obj(cust, "layer" + i);
        if (layer?.GetValueOrDefault("density") is { } dv && float.TryParse(Convert.ToString(dv, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var df))
          density[i] = df;
        var cu = layer?.GetValueOrDefault("useCustomUVSet");
        customUv[i] = cu is bool cb ? cb : string.Equals(cu?.ToString(), "True", StringComparison.OrdinalIgnoreCase);
        modes[i] = layer?.GetValueOrDefault("tintBlendMode") as string ?? "linear_blend";
        patternModes[i] = layer?.GetValueOrDefault("blendMode") as string;
        var en = layer?.GetValueOrDefault("enabled");
        enabled[i] = layer is not null && !(en is bool b ? !b : string.Equals(en?.ToString(), "False", StringComparison.OrdinalIgnoreCase));
      }

      return new CustomizationSlot(name, mask, SslText.Obj(glt, "tex")?.GetValueOrDefault("diffOverride") as string, modes, enabled, cust.GetValueOrDefault("texArrayDiffuse") as string)
        {
          PatternModes = patternModes,
          TexArrayNormals = cust.GetValueOrDefault("texArrayNormals") as string,
          TexArraySpec = cust.GetValueOrDefault("texArraySpec") as string,
          TexArrayEmissiveMask = cust.GetValueOrDefault("texArrayEmissiveMask") as string,
          LayerDensity = density,
          UseCustomUVSet = customUv,
        };
    }

    return null;
  }
}

/// <summary>
///   mtl1-52: CPU bake of the glt customization layers into preview albedo / spec images. Per pixel, for layer L = 0..3
///   (glt PS 003784 layer loop, PROVEN shape):
///   <code>
///     w      = mask.channel[L] * tint.a                       (mask r,g,b,a = layer0..3)
///     metalL = isMetallic ? 1 : saturate(metal0*scale + bias)  (metal0 = spec metalness; HEURISTIC isMetallic)
///              blendMode tint_only: metalL = running metal, metal/rough not changed (PS branch rec+80+L == 0)
///     tintC  = lerp(tint, spec_metalnessTint, metalL)          (shader: lerp(rec[+17+L], rec[+21+L], metalness))
///     col    = tintBlendMode multiply ? base * tintC : tintC   (CB_STATIC_GLT[180+L] == 0 ? x base : replace)
///     albedo = lerp(albedo, col, w)
///     metal  = lerp(metal, metalL, w);  rough = lerp(rough, saturate(rough*rScale + rBias), w)
///   </code>
///   Layer L ↔ primary/secondary/tertiary is INFERRED from the slot order (color_set_types_desc_armor). texArray patterns
///   (textureIndex ≥ 0, e.g. Stormhost_Silver) are not sampled (texture array not decoded) — tint only.
/// </summary>
public static class ArmorSchemeBake
{
  private static readonly float[] ToLinear = Enumerable.Range(0, 256).Select(i =>
  {
    var c = i / 255f;
    return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
  }).ToArray();

  private static byte ToSrgb(float l)
  {
    l = Math.Clamp(l, 0f, 1f);
    var c = l <= 0.0031308f ? l * 12.92f : 1.055f * MathF.Pow(l, 1f / 2.4f) - 0.055f;
    return (byte)Math.Clamp((int)(c * 255f + 0.5f), 0, 255);
  }

  private static byte Nearest(PreviewImage img, int x, int y, int w, int h, int channel)
  {
    var sx = img.Width == w ? x : Math.Min(img.Width - 1, x * img.Width / w);
    var sy = img.Height == h ? y : Math.Min(img.Height - 1, y * img.Height / h);
    return img.Bgra[(sy * img.Width + sx) * 4 + channel];
  }

  /// <summary>Mask byte offset of layer L in BGRA (r,g,b,a = layer 0..3).</summary>
  private static readonly int[] MaskChannel = { 2, 1, 0, 3 };

  private readonly record struct L(float Tr, float Tg, float Tb, float Ta, float Mr, float Mg, float Mb, bool Metallic, float MS, float MB, float RS, float RB);

  private static L?[] Prepare(IReadOnlyList<ArmorLayer?> layers, bool[] enabled)
  {
    var result = new L?[4];
    for (var i = 0; i < 4; i++)
    {
      if (i >= layers.Count || layers[i] is not { } a || !enabled[i])
        continue;
      result[i] = new L(ToLinear[a.TintR], ToLinear[a.TintG], ToLinear[a.TintB], a.TintA / 255f, ToLinear[a.MetalR], ToLinear[a.MetalG], ToLinear[a.MetalB],
        a.Metallic, a.MetalnessScale, a.MetalnessBias, a.RoughnessScale, a.RoughnessBias);
    }

    return result;
  }

  private static (int Metal, int Rough) SpecOffsets(string channels)
    => (2, channels.Contains("Roughness(a)", StringComparison.OrdinalIgnoreCase) ? 3 : 1);

  /// <summary>Tinted albedo at the base albedo's size (base = the variant's diffOverride, the neutral _a texture).</summary>
  /// <summary>
  ///   rea-batch: <c>tintBlendMode</c> enum (exe string table 0x144ccbf40: multiply = 0, linear_blend = 1) reaches the glt PS as
  ///   CB_STATIC_GLT[180+L]: 0 multiplies the layer colour by the base albedo, anything else replaces it (PROVEN shader; enum index INFERRED
  ///   from the table order). Retail td: linear_blend 20,052 / multiply 1,127 layers → most armour paint is the tint itself, not tint × _a.
  /// </summary>
  public static bool MultipliesBase(string? tintBlendMode) => string.Equals(tintBlendMode, "multiply", StringComparison.OrdinalIgnoreCase);

  /// <summary>rea-batch: <c>blendMode</c> enum (exe table: tint_only = 0, tex_hardlight = 1, tex_blend = 2) = glt record +80+L.</summary>
  public static bool IsTintOnly(string? blendMode) => string.Equals(blendMode, "tint_only", StringComparison.OrdinalIgnoreCase);

  /// <summary>
  ///   rea-batch 2: the decoded texture-array slices of a CC variant (td texArrayDiffuse / texArraySpec; one PreviewImage per
  ///   face — retail arrays are pct cubemap-style faces, e.g. ch_custom_01 = 6 faces of 512², PROVEN data) and the td per-layer
  ///   UV factors. Null arrays = the td names none (the layer then takes the plain tint path, INFERRED: the engine has nothing to bind).
  /// </summary>
  public sealed record PatternArrays(IReadOnlyList<PreviewImage>? Diffuse, IReadOnlyList<PreviewImage>? Spec, float[] Density, bool[] CustomUv);

  /// <summary>Bilinear, wrap-addressed sample (BGRA bytes → 0..1 floats r,g,b,a).</summary>
  private static (float R, float G, float B, float A) Sample(PreviewImage img, float u, float v)
  {
    var fx = u * img.Width - 0.5f;
    var fy = v * img.Height - 0.5f;
    var x0 = (int)MathF.Floor(fx);
    var y0 = (int)MathF.Floor(fy);
    var tx = fx - x0;
    var ty = fy - y0;
    float r = 0, g = 0, b = 0, a = 0;
    for (var j = 0; j < 2; j++)
    for (var i = 0; i < 2; i++)
    {
      var wgt = (i == 0 ? 1 - tx : tx) * (j == 0 ? 1 - ty : ty);
      var xx = ((x0 + i) % img.Width + img.Width) % img.Width;
      var yy = ((y0 + j) % img.Height + img.Height) % img.Height;
      var o = (yy * img.Width + xx) * 4;
      b += img.Bgra[o] * wgt;
      g += img.Bgra[o + 1] * wgt;
      r += img.Bgra[o + 2] * wgt;
      a += img.Bgra[o + 3] * wgt;
    }

    return (r / 255f, g / 255f, b / 255f, a / 255f);
  }

  private static float Lin(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

  /// <summary>
  ///   rea-batch 2: pattern slice for layer <paramref name="i"/>, or null when the layer takes the tint path: textureIndex &lt; 0,
  ///   no td array, tint_only, or useCustomUVSet (the second UV set is not in the albedo's texture space — HEURISTIC fallback to tint).
  /// </summary>
  private static PreviewImage? PatternSlice(PatternArrays? arrays, ArmorLayer? layer, int i, bool tintOnly)
  {
    if (arrays?.Diffuse is not { Count: > 0 } d || layer is null || tintOnly || layer.TextureIndex < 0 || layer.TextureIndex >= d.Count)
      return null;
    if (i < arrays.CustomUv.Length && arrays.CustomUv[i])
      return null;
    return d[layer.TextureIndex];
  }

  private static PreviewImage? SpecSlice(PatternArrays? arrays, ArmorLayer? layer)
    => arrays?.Spec is { Count: > 0 } s && layer is { MapSpec: >= 0 } && layer.MapSpec < s.Count ? s[layer.MapSpec] : null;

  /// <summary>Pattern uv factor: record density × td layer density (CB_STATIC_GLT[176+L], INFERRED).</summary>
  private static float UvScale(PatternArrays arrays, ArmorLayer layer, int i) => layer.Density * (i < arrays.Density.Length ? arrays.Density[i] : 1f);

  public static PreviewImage Albedo(PreviewImage baseAlbedo, PreviewImage mask, PreviewImage? spec, IReadOnlyList<ArmorLayer?> layers, bool[] enabled,
    IReadOnlyList<string?>? tintModes = null, IReadOnlyList<string?>? patternModes = null, PatternArrays? arrays = null)
  {
    var mul = new bool[4];
    var tintOnly = new bool[4];
    var pattern = new PreviewImage?[4];
    var patSpec = new PreviewImage?[4];
    var uvScale = new float[4];
    var patTint = new (float R, float G, float B, float A)[4];
    for (var i = 0; i < 4; i++)
    {
      mul[i] = tintModes is not null && i < tintModes.Count && MultipliesBase(tintModes[i]);
      tintOnly[i] = patternModes is not null && i < patternModes.Count && IsTintOnly(patternModes[i]);
      var layer = i < layers.Count ? layers[i] : null;
      pattern[i] = PatternSlice(arrays, layer, i, tintOnly[i]);
      if (pattern[i] is not null)
      {
        patSpec[i] = SpecSlice(arrays, layer);
        uvScale[i] = UvScale(arrays!, layer!, i);
        patTint[i] = (ToLinear[layer!.PatternR], ToLinear[layer.PatternG], ToLinear[layer.PatternB], layer.PatternA / 255f);
      }
    }

    var w = baseAlbedo.Width;
    var h = baseAlbedo.Height;
    var src = baseAlbedo.Bgra;
    var dst = new byte[src.Length];
    var prepared = Prepare(layers, enabled);
    var (mo, _) = SpecOffsets(spec?.Channels ?? "");
    Parallel.For(0, h, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, y =>
    {
      for (var x = 0; x < w; x++)
      {
        var p = (y * w + x) * 4;
        float br = ToLinear[src[p + 2]], bg = ToLinear[src[p + 1]], bb = ToLinear[src[p]];
        float r = br, g = bg, b = bb;
        var metal0 = spec is null ? 0f : Nearest(spec, x, y, w, h, mo) / 255f;
        var metal = metal0;
        for (var i = 0; i < 4; i++)
        {
          if (prepared[i] is not { } l)
            continue;
          var weight = Nearest(mask, x, y, w, h, MaskChannel[i]) / 255f * l.Ta;
          if (weight <= 0f)
            continue;
          if (pattern[i] is { } slice)
          {
            // glt PS tex_blend with textureIndex >= 0 (PROVEN shader): pattern lerp, then the tint where the slice alpha is low
            var u = (x + 0.5f) / w * uvScale[i];
            var v = (y + 0.5f) / h * uvScale[i];
            var t = Sample(slice, u, v);
            var pt = patTint[i];
            var pr = Lin(t.R) + (pt.R - Lin(t.R)) * pt.A;
            var pg = Lin(t.G) + (pt.G - Lin(t.G)) * pt.A;
            var pb = Lin(t.B) + (pt.B - Lin(t.B)) * pt.A;
            if (mul[i]) { pr *= br; pg *= bg; pb *= bb; }
            var cr = br + (pr - br) * t.A;
            var cg = bg + (pg - bg) * t.A;
            var cb = bb + (pb - bb) * t.A;
            var mp = patSpec[i] is { } ps
              ? Math.Clamp(((Sample(ps, u, v).R - metal0) * t.A + metal0) * l.MS + l.MB, 0f, 1f)
              : Math.Clamp(metal0 * l.MS + l.MB, 0f, 1f);
            if (l.Metallic) mp = 1f;
            var qr = l.Tr + (l.Mr - l.Tr) * mp;
            var qg = l.Tg + (l.Mg - l.Tg) * mp;
            var qb = l.Tb + (l.Mb - l.Tb) * mp;
            if (mul[i]) { qr *= br; qg *= bg; qb *= bb; }
            var k = Math.Clamp(1f - t.A, 0f, 1f) * l.Ta;
            r += (cr + (qr - cr) * k - r) * weight;
            g += (cg + (qg - cg) * k - g) * weight;
            b += (cb + (qb - cb) * k - b) * weight;
            metal += (mp - metal) * weight;
            continue;
          }

          var ml = tintOnly[i] ? metal : l.Metallic ? 1f : Math.Clamp(metal0 * l.MS + l.MB, 0f, 1f);
          var tr = l.Tr + (l.Mr - l.Tr) * ml;
          var tg = l.Tg + (l.Mg - l.Tg) * ml;
          var tb = l.Tb + (l.Mb - l.Tb) * ml;
          if (mul[i])
          {
            tr *= br;
            tg *= bg;
            tb *= bb;
          }

          r += (tr - r) * weight;
          g += (tg - g) * weight;
          b += (tb - b) * weight;
          if (!tintOnly[i])
            metal += (ml - metal) * weight;
        }

        dst[p] = ToSrgb(b);
        dst[p + 1] = ToSrgb(g);
        dst[p + 2] = ToSrgb(r);
        dst[p + 3] = src[p + 3];
      }
    });
    return new PreviewImage { Width = w, Height = h, Bgra = dst, Channels = baseAlbedo.Channels };
  }

  /// <summary>Spec map with the layers' metalness / roughness blended in (same size and channel layout as <paramref name="spec"/>).</summary>
  public static PreviewImage Specular(PreviewImage spec, PreviewImage mask, IReadOnlyList<ArmorLayer?> layers, bool[] enabled, IReadOnlyList<string?>? patternModes = null,
    PatternArrays? arrays = null)
  {
    var w = spec.Width;
    var h = spec.Height;
    var dst = (byte[])spec.Bgra.Clone();
    var prepared = Prepare(layers, enabled);
    var (mo, ro) = SpecOffsets(spec.Channels);
    var pattern = new PreviewImage?[4];
    var patSpec = new PreviewImage?[4];
    var uvScale = new float[4];
    for (var i = 0; i < 4; i++)
    {
      var tintOnly = patternModes is not null && i < patternModes.Count && IsTintOnly(patternModes[i]);
      var layer = i < layers.Count ? layers[i] : null;
      pattern[i] = PatternSlice(arrays, layer, i, tintOnly);
      if (pattern[i] is not null)
      {
        patSpec[i] = SpecSlice(arrays, layer);
        uvScale[i] = UvScale(arrays!, layer!, i);
      }
    }

    Parallel.For(0, h, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, y =>
    {
      for (var x = 0; x < w; x++)
      {
        var p = (y * w + x) * 4;
        var metal = dst[p + mo] / 255f;
        var rough = dst[p + ro] / 255f;
        var m0 = metal;
        var r0 = rough;
        for (var i = 0; i < 4; i++)
        {
          if (prepared[i] is not { } l)
            continue;
          var weight = Nearest(mask, x, y, w, h, MaskChannel[i]) / 255f * l.Ta;
          if (weight <= 0f)
            continue;
          if (patternModes is not null && i < patternModes.Count && IsTintOnly(patternModes[i]))
            continue; // tint_only keeps metalness / roughness (glt PS branch rec+80+L == 0)
          float ml, rl;
          if (pattern[i] is { } slice && patSpec[i] is { } ps)
          {
            // tex_blend with a spec slice (PROVEN shader): metal/rough lerp toward the slice by the pattern alpha, then scale+bias
            var u = (x + 0.5f) / w * uvScale[i];
            var v = (y + 0.5f) / h * uvScale[i];
            var ta = Sample(slice, u, v).A;
            var sp = Sample(ps, u, v);
            ml = l.Metallic ? 1f : Math.Clamp(((sp.R - m0) * ta + m0) * l.MS + l.MB, 0f, 1f);
            rl = Math.Clamp(((sp.G - r0) * ta + r0) * l.RS + l.RB, 0f, 1f);
          }
          else
          {
            ml = l.Metallic ? 1f : Math.Clamp(m0 * l.MS + l.MB, 0f, 1f);
            rl = Math.Clamp(r0 * l.RS + l.RB, 0f, 1f);
          }

          metal += (ml - metal) * weight;
          rough += (rl - rough) * weight;
        }

        dst[p + mo] = (byte)Math.Clamp((int)(metal * 255f + 0.5f), 0, 255);
        dst[p + ro] = (byte)Math.Clamp((int)(rough * 255f + 0.5f), 0, 255);
      }
    });
    return new PreviewImage { Width = w, Height = h, Bgra = dst, Channels = spec.Channels };
  }

  /// <summary>
  ///   UE material-instance parameters for a scheme (JSON): per part the CC mask / neutral albedo texture names and
  ///   Layer{0..3}Tint / MetalTint (linear), Metalness/Roughness scale+bias, enabled, blend mode. A UE master material
  ///   reproduces the bake above with these (mtl1-52; the mask is exported alongside under its td name).
  /// </summary>
  public static string UeParams(ArmorScheme scheme, IEnumerable<(string Part, string? Texture, CustomizationSlot Slot)> parts)
  {
    var sb = new StringBuilder();
    static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
    static string J(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    sb.Append("{\n  \"scheme\": ").Append(J(scheme.Id)).Append(",\n  \"name\": ").Append(J(scheme.Name)).Append(",\n  \"nameKey\": ").Append(J(scheme.NameKey));
    sb.Append(",\n  \"rule\": \"albedo=lerp(albedo, col, mask[L]*Tint.a), col = lerp(Tint,MetalTint,metal) (TintBlendMode linear_blend) or base*that (multiply) per layer L (glt PS layer loop, rea-batch); mask r,g,b,a = layer0..3; layer0..2 = primary/secondary/tertiary (INFERRED)\"");
    if (scheme.Lenses is { } lens)
    {
      static float L2(byte c) { var x = c / 255f; return x <= 0.04045f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f); }
      sb.Append(",\n  \"lenses\": { \"colour\": ").Append(J(lens.Colour)).Append(", \"emissiveTint\": [").Append(F(L2(lens.EmissiveR))).Append(", ").Append(F(L2(lens.EmissiveG))).Append(", ").Append(F(L2(lens.EmissiveB)))
        .Append("], \"emissiveIntensity\": ").Append(F(lens.EmissiveIntensity)).Append(", \"bloomIntensity\": ").Append(F(lens.EmissiveBloom))
        .Append(", \"textureIndex\": ").Append(lens.TextureIndex).Append(", \"mapsEmissive\": ").Append(lens.MapEmissive)
        .Append(", \"rule\": \"emissive = lerp(em, lerp(baseEm, emArray[mapsEmissive].rgb, pattern.a) * emissiveTint, w); intensity log2(CB_COMMON[22].x*I+1) (glt PS, PROVEN shader; CB_COMMON[22].x unknown)\" }");
    }

    sb.Append(",\n  \"parts\": [");
    var first = true;
    foreach (var (part, texture, slot) in parts)
    {
      var layers = ArmorColourSchemes.LayersFor(scheme, part, texture);
      sb.Append(first ? "\n" : ",\n").Append("    { \"part\": ").Append(J(part)).Append(", \"texture\": ").Append(J(texture ?? "")).Append(", \"variant\": ").Append(J(slot.Variant))
        .Append(", \"mask\": ").Append(J(slot.Mask)).Append(", \"albedo\": ").Append(J(slot.DiffOverride ?? texture ?? ""))
        .Append(", \"texArrayDiffuse\": ").Append(J(slot.TexArrayDiffuse ?? "")).Append(", \"texArrayNormals\": ").Append(J(slot.TexArrayNormals ?? ""))
        .Append(", \"texArraySpec\": ").Append(J(slot.TexArraySpec ?? "")).Append(", \"texArrayEmissiveMask\": ").Append(J(slot.TexArrayEmissiveMask ?? "")).Append(", \"params\": {");
      first = false;
      var pf = true;
      void P(string k, string v)
      {
        sb.Append(pf ? " " : ", ").Append(J(k)).Append(": ").Append(v);
        pf = false;
      }

      for (var i = 0; i < 4; i++)
      {
        var on = slot.Enabled[i] && i < layers.Length;
        P($"Layer{i}Enabled", on ? "1" : "0");
        if (!on)
          continue;
        var l = layers[i];
        static float Lin(byte c) { var x = c / 255f; return x <= 0.04045f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f); }
        P($"Layer{i}Tint", $"[{F(Lin(l.TintR))}, {F(Lin(l.TintG))}, {F(Lin(l.TintB))}, {F(l.TintA / 255f)}]");
        P($"Layer{i}MetalTint", $"[{F(Lin(l.MetalR))}, {F(Lin(l.MetalG))}, {F(Lin(l.MetalB))}, 1]");
        P($"Layer{i}PatternTint", $"[{F(Lin(l.PatternR))}, {F(Lin(l.PatternG))}, {F(Lin(l.PatternB))}, {F(l.PatternA / 255f)}]");
        P($"Layer{i}Metallic", l.Metallic ? "1" : "0");
        P($"Layer{i}MetalnessScaleBias", $"[{F(l.MetalnessScale)}, {F(l.MetalnessBias)}]");
        P($"Layer{i}RoughnessScaleBias", $"[{F(l.RoughnessScale)}, {F(l.RoughnessBias)}]");
        P($"Layer{i}TextureIndex", l.TextureIndex.ToString(CultureInfo.InvariantCulture));
        P($"Layer{i}MapsNormalSpecEmissive", $"[{l.MapNormal}, {l.MapSpec}, {l.MapEmissive}]");
        P($"Layer{i}Density", $"[{F(l.Density)}, {F(i < slot.LayerDensity.Length ? slot.LayerDensity[i] : 1f)}]");
        P($"Layer{i}UseCustomUVSet", slot.UseCustomUVSet.ElementAtOrDefault(i) ? "1" : "0");
        P($"Layer{i}ScrollScale", F(l.ScrollScale));
        P($"Layer{i}NormalScale", F(l.NormalScale));
        P($"Layer{i}PatternBlendMode", J(slot.PatternModes.ElementAtOrDefault(i) ?? ""));
        P($"Layer{i}Colour", J(l.Colour));
        P($"Layer{i}TintBlendMode", J(slot.BlendModes.ElementAtOrDefault(i) ?? "linear_blend"));
        P($"Layer{i}MultiplyBase", ArmorSchemeBake.MultipliesBase(slot.BlendModes.ElementAtOrDefault(i)) ? "1" : "0");
      }

      sb.Append(" } }");
    }

    sb.Append("\n  ]\n}\n");
    return sb.ToString();
  }
}
