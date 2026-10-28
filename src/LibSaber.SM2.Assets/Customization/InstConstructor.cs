using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>An <c>InstConstructorMeshTemplate</c> of <c>inst_constructor_mesh_templates.ssolib</c>: the tpl objects one variant shows.</summary>
public sealed record NpcMeshTemplate(string Key, IReadOnlyList<string> Objects)
{
  public bool IsEmpty => Objects.Count == 0;
}

/// <summary>One <c>templateVariants[]</c> entry of a body layer.</summary>
public sealed class NpcVariant
{
  /// <summary>The link as written (<c>template.__type</c>, e.g. <c>InstConstructorMeshTemplatesCultistHead01</c>).</summary>
  public required string Link { get; init; }
  /// <summary>Resolved mesh template, null when the link names none.</summary>
  public NpcMeshTemplate? Mesh { get; init; }
  /// <summary>The <c>probability</c> as written; null when the entry has none.</summary>
  public double? Probability { get; init; }
  /// <summary>
  ///   Weight for Randomize: <see cref="Probability"/>, else <see cref="InstConstructorLibrary.DefaultProbability"/>.
  ///   HEURISTIC: the default 1.0 is the only float default in inst_constructor_mesh_template_variant_release.sslbin
  ///   (offset 0x26F, field after <c>probability</c>); the game draws with GetElementIndexByDistribution /
  ///   CreateProbabilityDistributionArray (names in inst_constructor_template_link_variants_release.sslbin), whose
  ///   normalisation we assume (weights / sum).
  /// </summary>
  public double Weight => Probability ?? InstConstructorLibrary.DefaultProbability;
  public string Key => Mesh?.Key ?? Link;
  public bool IsEmpty => Mesh is null || Mesh.IsEmpty;
  public override string ToString() => Key;
}

/// <summary>An <c>InstConstructorBodyLayerTemplate</c>: one slot of an NPC template.</summary>
public sealed class NpcLayer
{
  public required string Name { get; init; }
  public List<NpcVariant> Variants { get; } = new();
  /// <summary><c>requiredLayer</c>: this layer only shows when that layer picked a non-empty mesh.</summary>
  public string? RequiredLayer { get; init; }
  /// <summary><c>disabledByLayer</c>: this layer hides when that layer picked a non-empty mesh.</summary>
  public string? DisabledByLayer { get; init; }
  public double TotalWeight => Variants.Sum(v => v.Weight);
  public double Chance(NpcVariant v) => TotalWeight > 0 ? v.Weight / TotalWeight : 0;
}

/// <summary>An <c>InstConstructorTemplate</c> of <c>inst_constructor_templates.sso</c> (an enemy / NPC body recipe).</summary>
public sealed class NpcTemplate
{
  public required string Name { get; init; }
  public List<NpcLayer> Layers { get; } = new();
  public string? MaterialModifier { get; init; }
  /// <summary>tpl names that show this template, best first, with where each came from.</summary>
  public List<(string Tpl, string Source)> Models { get; } = new();
  /// <summary>True for a pseudo entry that only groups custom presets of a tpl no template maps to.</summary>
  public bool PresetsOnly { get; init; }
  public override string ToString() => PresetsOnly ? Name + "  · presets only" : Name;
}

/// <summary>A fixed loadout: <c>prop_inst_constructor_custom_preset.customPreset.bodyLayers.*.geomObjNames</c> of a .cls.</summary>
public sealed record NpcPreset(string Name, string ClsPath, string? Tpl, IReadOnlyList<string> Objects, string? MaterialModifier, string? FixedTemplateId)
{
  public override string ToString() => Name + (Objects.Count == 0 ? "  · no objects" : "");
}

/// <summary>
///   ui-45 enemy / NPC Kitbasher data: <c>ssl/inst_constructor/inst_constructor_templates.sso</c> (75 templates →
///   bodyLayers → templateVariants {probability, template.__type}) linked to
///   <c>ssl/inst_constructor/inst_constructor_mesh_templates.ssolib</c> (classes → geomObjNames, tpl object names), plus the
///   .cls data: <c>prop_inst_constructor_project.fixedTemplateId</c> (which template a model uses) and
///   <c>prop_inst_constructor_custom_preset</c> (fixed loadouts).
///   HEURISTIC: a link <c>InstConstructorMeshTemplatesHormTermHeadAttach01</c> resolves to class
///   <c>Horm_Term_Head_Attach_01</c> by dropping the prefix and comparing case- and underscore-insensitively
///   (835/835 retail links resolve, no two classes collide).
/// </summary>
public sealed class InstConstructorLibrary
{
  public const string TemplatesPath = "ssl/inst_constructor/inst_constructor_templates.sso";
  public const string MeshTemplatesPath = "ssl/inst_constructor/inst_constructor_mesh_templates.ssolib";
  public const string LinkPrefix = "InstConstructorMeshTemplates";
  public const double DefaultProbability = 1.0;
  public const string EmptyKey = "EMPTY";

  public List<NpcTemplate> Templates { get; } = new();
  public Dictionary<string, NpcMeshTemplate> MeshTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, NpcMeshTemplate> _byNorm = new(StringComparer.Ordinal);
  public List<NpcPreset> Presets { get; } = new();
  /// <summary>fixedTemplateId → (tpl, cls) hits from the .cls scan.</summary>
  public Dictionary<string, List<(string Tpl, string Cls)>> FixedTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);
  public int ClassesScanned { get; private set; }
  public List<string> Notes { get; } = new();

  public static string Norm(string s) => s.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();

  public NpcMeshTemplate? Resolve(string link)
  {
    var key = link.StartsWith(LinkPrefix, StringComparison.OrdinalIgnoreCase) ? link[LinkPrefix.Length..] : link;
    return _byNorm.GetValueOrDefault(Norm(key));
  }

  public NpcTemplate? Template(string name) => Templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

  public static InstConstructorLibrary FromText(string templates, string meshTemplates)
  {
    var lib = new InstConstructorLibrary();
    if (SslText.Obj(SslText.Parse(meshTemplates), "classes") is { } classes)
    {
      foreach (var (key, value) in classes)
      {
        var objects = SslText.Strings((value as Dictionary<string, object?>)?.GetValueOrDefault("geomObjNames")).Where(n => n.Length > 0).ToList();
        var mesh = new NpcMeshTemplate(key, objects);
        lib.MeshTemplates[key] = mesh;
        lib._byNorm.TryAdd(Norm(key), mesh);
      }
    }

    var unresolved = 0;
    if (SslText.Obj(SslText.Parse(templates), "templates") is { } list)
    {
      foreach (var (name, value) in list)
      {
        if (value is not Dictionary<string, object?> map)
          continue;
        var t = new NpcTemplate { Name = name, MaterialModifier = map.GetValueOrDefault("materialModifier") as string };
        if (SslText.Obj(map, "bodyLayers") is { } layers)
        {
          foreach (var (layerName, layerValue) in layers)
          {
            if (layerValue is not Dictionary<string, object?> lm)
              continue;
            var layer = new NpcLayer
            {
              Name = layerName,
              RequiredLayer = lm.GetValueOrDefault("requiredLayer") as string,
              DisabledByLayer = lm.GetValueOrDefault("disabledByLayer") as string,
            };
            if (lm.GetValueOrDefault("templateVariants") is List<object?> variants)
            {
              foreach (var v in variants)
              {
                if (v is not Dictionary<string, object?> vm)
                  continue;
                var link = SslText.Obj(vm, "template")?.GetValueOrDefault("__type") as string ?? "";
                var mesh = lib.Resolve(link);
                if (mesh is null)
                  unresolved++;
                layer.Variants.Add(new NpcVariant { Link = link, Mesh = mesh, Probability = vm.GetValueOrDefault("probability") is double p ? p : null });
              }
            }

            t.Layers.Add(layer);
          }
        }

        lib.Templates.Add(t);
      }
    }

    if (unresolved > 0)
      lib.Notes.Add($"{unresolved} template link(s) name no mesh template.");
    return lib;
  }

  private static readonly Regex NameTplRx = new("nameTpl\\s*=\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);
  private static readonly Regex FixedRx = new("fixedTemplateId\\s*=\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);

  /// <summary>Reads one .cls text: fixedTemplateId hits and a custom preset. <paramref name="path"/> is the archive path.</summary>
  public void AddClass(string path, string text)
  {
    if (text.IndexOf("prop_inst_constructor", StringComparison.Ordinal) < 0)
      return;
    ClassesScanned++;
    string? tpl = null;
    Dictionary<string, object?>? root = null;
    try
    {
      root = SslText.Parse(text);
      tpl = SslText.Obj(root, "properties", "geom")?.GetValueOrDefault("nameTpl") as string;
    }
    catch
    {
      // fall back to the regexes below
    }

    if (string.IsNullOrEmpty(tpl))
      tpl = NameTplRx.Match(text) is { Success: true } m && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null; // HEURISTIC: first nameTpl
    var fixedId = SslText.Obj(root, "properties", "prop_inst_constructor_project")?.GetValueOrDefault("fixedTemplateId") as string
                  ?? (FixedRx.Match(text) is { Success: true } f ? f.Groups[1].Value : null);
    if (!string.IsNullOrEmpty(fixedId) && !string.IsNullOrEmpty(tpl))
    {
      if (!FixedTemplates.TryGetValue(fixedId, out var hits))
        FixedTemplates[fixedId] = hits = new();
      hits.Add((tpl, path));
    }

    var custom = SslText.Obj(root, "properties", "prop_inst_constructor_custom_preset");
    if (custom is null && text.Contains("prop_inst_constructor_custom_preset", StringComparison.Ordinal))
      custom = SslText.Obj(BlockAfter(text, "prop_inst_constructor_custom_preset"), "x");
    if (custom is null && !text.Contains("prop_inst_constructor_custom_preset", StringComparison.Ordinal))
      return;
    var objects = new List<string>();
    string? modifier = null;
    void Collect(Dictionary<string, object?>? preset)
    {
      if (preset is null)
        return;
      modifier ??= preset.GetValueOrDefault("materialModifier") as string;
      if (SslText.Obj(preset, "bodyLayers") is not { } layers)
        return;
      foreach (var (_, layer) in layers)
      {
        foreach (var name in SslText.Strings((layer as Dictionary<string, object?>)?.GetValueOrDefault("geomObjNames")))
        {
          if (name.Length > 0 && !objects.Contains(name, StringComparer.OrdinalIgnoreCase))
            objects.Add(name);
        }
      }
    }

    Collect(SslText.Obj(custom, "customPreset"));
    if (objects.Count == 0 && SslText.Obj(custom, "presetsDict") is { } dict)
    {
      foreach (var (_, p) in dict)
        Collect(p as Dictionary<string, object?>);
    }

    Presets.Add(new NpcPreset(Path.GetFileNameWithoutExtension(path), path, tpl, objects, modifier, fixedId));
  }

  /// <summary><c>x = { … }</c> for the brace block after <paramref name="key"/> (cls files our parser can't read whole).</summary>
  private static Dictionary<string, object?>? BlockAfter(string text, string key)
  {
    var at = text.IndexOf(key, StringComparison.Ordinal);
    var open = at < 0 ? -1 : text.IndexOf('{', at);
    if (open < 0)
      return null;
    var depth = 0;
    for (var i = open; i < text.Length; i++)
    {
      if (text[i] == '{')
        depth++;
      else if (text[i] == '}' && --depth == 0)
      {
        try { return SslText.Parse("x = " + text[open..(i + 1)]); } catch { return null; }
      }
    }

    return null;
  }

  /// <summary>
  ///   Fills <see cref="NpcTemplate.Models"/>: fixedTemplateId hits first (most .cls votes first), then the name rule
  ///   (HEURISTIC): <c>cc_{name}</c>, <c>cc_constructor_{name}</c>, <c>cc_{name}_constructor</c>, dropping one trailing
  ///   <c>_word</c> at a time, also with gaunt→gant (Termagaunt → cc_termagant). <paramref name="exists"/> says whether a tpl is in the game.
  ///   Adds a "presets only" pseudo template per preset tpl that no template uses.
  /// </summary>
  public void MapModels(Func<string, bool> exists) => MapModels(exists, null);

  /// <param name="tplNames">every tpl base name in the game (underscore-insensitive name matches); preset .cls tpls count too.</param>
  public void MapModels(Func<string, bool> exists, IEnumerable<string>? tplNames)
  {
    var known = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var n in (tplNames ?? Enumerable.Empty<string>()).Concat(Presets.Select(p => p.Tpl).OfType<string>()))
      known.TryAdd(Norm(n), n);
    string? resolve(string candidate) => exists(candidate) ? candidate : known.GetValueOrDefault(Norm(candidate));
    foreach (var t in Templates)
    {
      t.Models.Clear();
      if (FixedTemplates.TryGetValue(t.Name, out var hits))
      {
        foreach (var g in hits.GroupBy(h => h.Tpl, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()))
          t.Models.Add((g.Key, $"fixedTemplateId in {g.Count()} .cls ({Path.GetFileName(g.First().Cls)})"));
      }

      foreach (var candidate in NameCandidates(t.Name))
      {
        var name = resolve(candidate);
        if (name is null || t.Models.Any(m => m.Tpl.Equals(name, StringComparison.OrdinalIgnoreCase)))
          continue;
        t.Models.Add((name, "name match (HEURISTIC)"));
        break;
      }

      if (t.Models.Count > 1)
      {
        // a fixedTemplateId tpl that is not in this game folder goes after one that is
        var present = t.Models.Where(m => exists(m.Tpl)).ToList();
        var absent = t.Models.Where(m => !exists(m.Tpl)).ToList();
        t.Models.Clear();
        t.Models.AddRange(present);
        t.Models.AddRange(absent);
      }
    }

    // HEURISTIC: Space_Marine_Lyreo / Exalted_Sorcerer_Clone use the model of the template their name extends
    foreach (var t in Templates.Where(t => t.Models.Count == 0 && !t.PresetsOnly))
    {
      var parent = Templates.Where(o => o != t && o.Models.Count > 0 && t.Name.StartsWith(o.Name + "_", StringComparison.OrdinalIgnoreCase)).MaxBy(o => o.Name.Length);
      if (parent is not null)
        t.Models.Add((parent.Models[0].Tpl, $"model of template {parent.Name} (HEURISTIC)"));
    }

    Templates.RemoveAll(t => t.PresetsOnly);
    var used = new HashSet<string>(Templates.SelectMany(t => t.Models.Take(1).Select(m => m.Tpl)), StringComparer.OrdinalIgnoreCase);
    foreach (var g in Presets.Where(p => p.Tpl is not null && p.Objects.Count > 0 && !used.Contains(p.Tpl!)).GroupBy(p => p.Tpl!, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
    {
      var t = new NpcTemplate { Name = g.Key, PresetsOnly = true };
      t.Models.Add((g.Key, "custom preset .cls nameTpl"));
      Templates.Add(t);
    }
  }

  public static IEnumerable<string> NameCandidates(string template)
  {
    var words = template.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();
    for (var n = words.Count; n >= 1; n--)
    {
      var stem = string.Join('_', words.Take(n));
      foreach (var s in stem.Contains("gaunt") ? new[] { stem, stem.Replace("gaunt", "gant") } : new[] { stem })
      {
        yield return "cc_" + s;
        yield return "cc_constructor_" + s;
        yield return "cc_" + s + "_constructor";
      }
    }
  }

  /// <summary>Presets whose .cls names <paramref name="tpl"/>.</summary>
  public List<NpcPreset> PresetsFor(string? tpl)
    => tpl is null ? new() : Presets.Where(p => p.Tpl is not null && p.Tpl.Equals(tpl, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

  /// <summary>Default pick: the heaviest variant per layer (first on a tie).</summary>
  public static Dictionary<string, string?> DefaultPicks(NpcTemplate t)
    => t.Layers.ToDictionary(l => l.Name, l => l.Variants.Count == 0 ? null : (string?)l.Variants.Aggregate((a, b) => b.Weight > a.Weight ? b : a).Key, StringComparer.OrdinalIgnoreCase);

  /// <summary>Weighted random pick per layer (<see cref="NpcVariant.Weight"/>).</summary>
  public static Dictionary<string, string?> RandomPicks(NpcTemplate t, Random random)
  {
    var picks = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var layer in t.Layers)
    {
      if (layer.Variants.Count == 0)
      {
        picks[layer.Name] = null;
        continue;
      }

      var total = layer.TotalWeight;
      var roll = random.NextDouble() * total;
      var pick = layer.Variants[^1];
      foreach (var v in layer.Variants)
      {
        if ((roll -= v.Weight) < 0)
        {
          pick = v;
          break;
        }
      }

      picks[layer.Name] = pick.Key;
    }

    return picks;
  }

  /// <summary>
  ///   Layers that are off by the requiredLayer / disabledByLayer rules for these picks (a layer counts as filled when its pick
  ///   names a non-empty mesh template and the layer itself is active). HEURISTIC reading of the two field names; the sslbin
  ///   logic is not decoded.
  /// </summary>
  public HashSet<string> DisabledLayers(NpcTemplate t, IReadOnlyDictionary<string, string?> picks)
  {
    var off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool Filled(string layer) => !off.Contains(layer) && picks.GetValueOrDefault(layer) is { } key && MeshTemplates.GetValueOrDefault(key) is { IsEmpty: false };
    for (var pass = 0; pass < 4; pass++)
    {
      var changed = false;
      foreach (var layer in t.Layers)
      {
        if (off.Contains(layer.Name))
          continue;
        var disabled = (layer.RequiredLayer is { } req && !Filled(req)) || (layer.DisabledByLayer is { } by && Filled(by));
        if (disabled)
          changed |= off.Add(layer.Name);
      }

      if (!changed)
        break;
    }

    return off;
  }

  /// <summary>Spec for <c>--variant</c>: <c>npc=TEMPLATE,layer=MeshTemplate|none,…</c> or <c>npc=TEMPLATE,npcpreset=CLS</c>.</summary>
  public static string Spec(string template, IReadOnlyDictionary<string, string?> picks, string? preset)
  {
    var parts = new List<string> { "npc=" + template };
    if (preset is not null)
      parts.Add("npcpreset=" + preset);
    else
      foreach (var (layer, key) in picks)
        parts.Add(layer + "=" + (string.IsNullOrEmpty(key) ? KitbashLibrary.NoneUid : key));
    return string.Join(",", parts);
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameCatalog, InstConstructorLibrary?[]> Cache = new();

  /// <summary>Loads both .sso files and scans every .cls that mentions prop_inst_constructor (cached per catalog). Null when the templates are missing.</summary>
  public static InstConstructorLibrary? Load(GameCatalog catalog, ArchiveStore store)
  {
    var holder = Cache.GetValue(catalog, _ => new InstConstructorLibrary?[1]);
    lock (holder)
    {
      if (holder[0] is { } cached)
        return cached;
      if (!catalog.TryGetByPath(TemplatesPath, out var t) || !catalog.TryGetByPath(MeshTemplatesPath, out var m))
        return null;
      var lib = FromText(Encoding.UTF8.GetString(store.Read(t)), Encoding.UTF8.GetString(store.Read(m)));
      var query = catalog.Query(null, ".cls", AssetFilter.All);
      foreach (var index in query.Indices ?? Enumerable.Range(query.First, query.Count).ToArray())
      {
        var file = catalog.GetFile(index);
        if (!file.Path.EndsWith(".cls", StringComparison.OrdinalIgnoreCase))
          continue;
        string text;
        try { text = Encoding.UTF8.GetString(store.Read(index)); } catch { continue; }
        lib.AddClass(file.Path, text);
      }

      var tpls = new List<string>();
      var tq = catalog.Query(null, ".tpl", AssetFilter.All);
      foreach (var index in tq.Indices ?? Enumerable.Range(tq.First, tq.Count).ToArray())
      {
        var name = catalog.GetFile(index).Name;
        if (name.EndsWith(".tpl", StringComparison.OrdinalIgnoreCase))
          tpls.Add(name[..^4]);
      }

      lib.MapModels(name => catalog.FindByFileName(name + ".tpl").Count > 0, tpls);
      return holder[0] = lib;
    }
  }
}

/// <summary>ui-45 NPC filter: shows only the picked mesh templates (or a custom preset's object list).</summary>
public static class NpcVariantFilter
{
  /// <summary>
  ///   Layer picks that a custom preset's show-list amounts to on this mesh: per layer the variant whose objects all lie inside
  ///   the preset's subtrees (most objects first), else the layer's empty variant, else null. For display only; the preset
  ///   itself exports its exact list.
  /// </summary>
  public static Dictionary<string, string?> MatchPreset(ExportedMesh mesh, NpcTemplate template, NpcPreset preset)
  {
    var bones = mesh.Bones;
    var names = new string[bones.Count];
    for (var i = 0; i < bones.Count; i++)
      names[i] = Regex.Replace(bones[i].SourceName.Length > 0 ? bones[i].SourceName : bones[i].Name, "_LOD\\d+$", "", RegexOptions.IgnoreCase);
    var roots = new HashSet<string>(preset.Objects, StringComparer.OrdinalIgnoreCase);
    var inside = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < bones.Count; i++)
    {
      for (var n = i; n >= 0 && n < bones.Count; n = bones[n].Parent)
      {
        if (roots.Contains(names[n]))
        {
          inside.Add(names[i]);
          break;
        }
      }
    }

    var picks = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var layer in template.Layers)
    {
      var hit = layer.Variants.Where(v => !v.IsEmpty && v.Mesh!.Objects.All(inside.Contains)).MaxBy(v => v.Mesh!.Objects.Count);
      picks[layer.Name] = hit?.Key ?? layer.Variants.FirstOrDefault(v => v.IsEmpty && v.Mesh is not null)?.Key;
    }

    return picks;
  }

  /// <summary>
  ///   Shows only the picked objects: every surface whose node is not under a picked mesh template's geomObjNames (or a
  ///   preset's) hides. <c>X_LODn</c> nodes count as <c>X</c> (siblings, as in <see cref="WeaponVariant"/>).
  ///   Layers switched off by requiredLayer / disabledByLayer contribute nothing.
  /// </summary>
  public static VariantResult Apply(ExportedMesh mesh, InstConstructorLibrary lib, VariantRequest request)
  {
    var result = new VariantResult { Template = mesh.Name, Spec = request.Spec, Team = "NPC", Appearance = request.Npc ?? "" };
    var bones = mesh.Bones;
    var byName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
    var children = new List<int>[bones.Count];
    for (var i = 0; i < bones.Count; i++)
    {
      var name = bones[i].SourceName.Length > 0 ? bones[i].SourceName : bones[i].Name;
      if (!byName.TryGetValue(name, out var l))
        byName[name] = l = new List<int>();
      l.Add(i);
      var lod = Regex.Match(name, "^(.+)_LOD\\d+$", RegexOptions.IgnoreCase);
      if (lod.Success)
      {
        if (!byName.TryGetValue(lod.Groups[1].Value, out var bl))
          byName[lod.Groups[1].Value] = bl = new List<int>();
        bl.Add(i);
      }

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
    foreach (var mt in lib.MeshTemplates.Values)
      foreach (var n in mt.Objects)
        AddSubtree(n, customizable);
    result.CustomizableObjects = customizable.Count;

    var shown = new HashSet<int>();
    var objects = new List<string>();
    var template = request.Npc is null ? null : lib.Template(request.Npc);
    if (request.NpcPreset is { } presetName)
    {
      var preset = lib.Presets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase) || p.ClsPath.Equals(presetName, StringComparison.OrdinalIgnoreCase));
      if (preset is null)
        result.Notes.Add($"custom preset {presetName} is not in the .cls scan; nothing hidden.");
      else
      {
        result.Preset = preset.Name;
        objects.AddRange(preset.Objects);
        if (preset.MaterialModifier is { } mm)
          result.Notes.Add("preset materialModifier " + mm + " (not applied, mesh only)");
      }
    }
    else if (template is null)
    {
      result.Notes.Add($"NPC template {request.Npc} is not in {InstConstructorLibrary.TemplatesPath}; nothing hidden.");
    }
    else
    {
      var picks = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
      foreach (var layer in template.Layers)
      {
        string? key = layer.Variants.FirstOrDefault()?.Key;
        if (request.Regions.TryGetValue(layer.Name, out var asked))
          key = asked.Equals(KitbashLibrary.NoneUid, StringComparison.OrdinalIgnoreCase) ? null : asked;
        if (key is not null && !layer.Variants.Any(v => v.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) && lib.MeshTemplates.ContainsKey(key))
          result.Notes.Add($"{layer.Name}={key} is not a variant of this layer (used anyway).");
        else if (key is not null && !lib.MeshTemplates.ContainsKey(key))
        {
          result.Notes.Add($"{layer.Name}={key} is not a mesh template; layer left empty.");
          key = null;
        }

        picks[layer.Name] = key;
      }

      foreach (var asked in request.Regions.Keys.Where(k => !template.Layers.Any(l => l.Name.Equals(k, StringComparison.OrdinalIgnoreCase))))
        result.Notes.Add($"layer {asked} is not in {template.Name}.");
      var off = lib.DisabledLayers(template, picks);
      foreach (var layer in template.Layers)
      {
        var key = picks[layer.Name];
        result.AvailableRegions[layer.Name] = layer.Variants.Select(v => v.Key).ToList();
        if (off.Contains(layer.Name))
        {
          result.Regions[layer.Name] = "(off: " + (layer.RequiredLayer is { } r ? "requires " + r : "disabled by " + layer.DisabledByLayer) + ")";
          continue;
        }

        result.Regions[layer.Name] = key ?? KitbashLibrary.NoneUid;
        if (key is not null && lib.MeshTemplates.TryGetValue(key, out var mt))
          objects.AddRange(mt.Objects);
      }
    }

    foreach (var n in objects)
    {
      if (!byName.ContainsKey(n))
        result.Notes.Add($"object {n} is not in {mesh.Name}.");
      AddSubtree(n, shown);
      result.ShownObjects.Add(n);
    }

    foreach (var name in request.Show)
      AddSubtree(name, shown);
    var hiddenNodes = new HashSet<int>();
    foreach (var name in request.Hide)
      AddSubtree(name, hiddenNodes);
    var nothingPicked = template is null && request.NpcPreset is null;
    if (!nothingPicked && objects.Count > 0 && !objects.Any(byName.ContainsKey))
    {
      result.Notes.Insert(0, $"none of the {objects.Count} picked objects is in {mesh.Name} (wrong model for this template?); nothing hidden.");
      nothingPicked = true;
    }

    var outsideNames = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)).Where(p => p.Visible && p.IndexCount >= 3 && p.Node >= 0 && p.Node < bones.Count && !customizable.Contains(p.Node) && !shown.Contains(p.Node)).Select(p => bones[p.Node].Name).Distinct().ToList();
    if (outsideNames.Count > 0 && !nothingPicked)
      result.Notes.Add("outside every mesh template: " + string.Join(", ", outsideNames.Take(12)));
    var outside = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)).Count(p => p.Visible && p.IndexCount >= 3 && p.Node >= 0 && !customizable.Contains(p.Node) && !shown.Contains(p.Node));
    if (outside > 0 && !nothingPicked)
      result.Notes.Add($"{outside} visible part(s) belong to no mesh template; hidden too (preview shows only the picked meshes).");
    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)))
    {
      if (nothingPicked || !part.Visible || part.Node < 0)
        continue;
      if (!shown.Contains(part.Node) || hiddenNodes.Contains(part.Node))
      {
        part.Visible = false;
        part.HideReason = ArmorVariant.HideReason;
        result.HiddenParts++;
      }
    }

    result.VisibleParts = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)).Count(part => part.Visible && part.IndexCount >= 3);
    return result;
  }
}
