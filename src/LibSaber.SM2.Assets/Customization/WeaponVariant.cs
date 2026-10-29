using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>
///   Weapon skins (REA-first, sm2 retail data + ssl scripts):
///   <c>ssl/weapons/creator/customization/firearm_skins_library.sso</c> skinsGroups[] = { firearmUid, skins{ skin_N = FirearmSkin
///   { geomCustomizerRegions[], material?, attachingActors?, overridedActorName? } } } (firearm_skin.sslbin: GetGeomCustomizerRegions,
///   GetMaterial, GetAttachingActors, GetOverridedActorName). Region names resolve in
///   <c>ssl/geom_customizer/geom_customizer_preset_firearm.sso</c> regions{ name = GeomCustomizerRegion { meshes[] } } (tpl object
///   names). prop_geom_customizer_equipment.sslbin ShowActiveRegions = HideAll + Show(region) per active region; Show/Hide end in the
///   native Geom ShowObj/HideObj (0x14133fec0 / 0x14133eae0, per object 0x14133e750). The tpl comes from
///   <c>ssl/weapons/common/firearm/mgs/&lt;firearmUid&gt;/&lt;firearmUid&gt;_client.cls</c> geom.nameTpl.
///   None of these field names are plaintext in sm2_retail.exe: the selection logic is ssl-only.
/// </summary>
public sealed class WeaponCustomization
{
  public const string SkinsPath = "ssl/weapons/creator/customization/firearm_skins_library.sso";
  public const string PresetPath = "ssl/geom_customizer/geom_customizer_preset_firearm.sso";
  /// <summary>mtl1-46: <c>actors{ name = IactorDesc{ type = { properties{ geom{ nameTpl, scale? } }, __type = class } } }</c>.</summary>
  public const string AttachingActorsPath = "ssl/geom_customizer/attaching_actors_library.sso";
  private static readonly Regex NameTpl = new("nameTpl\\s*=\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant);

  /// <summary>firearmUid -> skin key -> FirearmSkin (ordered as in the library).</summary>
  public Dictionary<string, List<(string Key, Dictionary<string, object?> Skin)>> Skins { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>region -> tpl object names.</summary>
  public Dictionary<string, List<string>> Regions { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>tpl name -> firearmUids using it (first is the plain mgs folder when it exists).</summary>
  public Dictionary<string, List<string>> UidsByTpl { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>attaching actor name -> (tpl, geom scale, class). Scale is the actor's own geom.scale, else its class's (1 when neither).</summary>
  public Dictionary<string, (string Tpl, float Scale, string Class)> AttachingActors { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>
  ///   Parses the attaching actors library. <paramref name="classText"/> returns the text of <c>&lt;class&gt;.cls</c> (null when absent);
  ///   HEURISTIC: the IactorDesc type block overrides its <c>__type</c> class the way ssl .cls inheritance does, so a missing
  ///   geom.scale / nameTpl falls back to the class's (purity_seal_01.cls geom.scale = 0.7).
  /// </summary>
  public void LoadAttachingActors(string text, Func<string, string?> classText)
  {
    if (SslText.Obj(SslText.Parse(text), "actors") is not { } actors)
      return;
    foreach (var (name, value) in actors)
    {
      if (value is not Dictionary<string, object?> desc || SslText.Obj(desc, "type") is not { } type)
        continue;
      var cls = type.GetValueOrDefault("__type") as string ?? "";
      var geom = SslText.Obj(type, "properties", "geom");
      Dictionary<string, object?>? baseGeom = null;
      if (cls.Length > 0 && classText(cls) is { } ct)
        baseGeom = SslText.Obj(SslText.Parse(ct), "properties", "geom");
      var tpl = geom?.GetValueOrDefault("nameTpl") as string ?? baseGeom?.GetValueOrDefault("nameTpl") as string;
      if (string.IsNullOrEmpty(tpl))
        continue;
      var scale = Number(geom?.GetValueOrDefault("scale")) ?? Number(baseGeom?.GetValueOrDefault("scale")) ?? 1f;
      AttachingActors[name] = (tpl, scale, cls);
    }
  }

  private static float? Number(object? value) => value switch
  {
    double d => (float)d,
    float f => f,
    int i => i,
    long l => l,
    string t when float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) => v,
    _ => null,
  };

  /// <summary>attachingActors of one skin: (slot key, locator, actor name).</summary>
  public static List<(string Slot, string Locator, string Actor)> SkinAttachments(Dictionary<string, object?>? skin)
  {
    var list = new List<(string, string, string)>();
    if (skin is null || SslText.Obj(skin, "attachingActors") is not { } aa)
      return list;
    foreach (var (slot, value) in aa)
    {
      if (value is Dictionary<string, object?> d && d.GetValueOrDefault("actorName") is string actor)
        list.Add((slot, d.GetValueOrDefault("locator") as string ?? slot, actor));
    }

    return list;
  }

  public static WeaponCustomization FromText(string skins, string preset)
  {
    var c = new WeaponCustomization();
    var root = SslText.Parse(skins);
    if (root.GetValueOrDefault("skinsGroups") is System.Collections.IEnumerable groups)
    {
      foreach (var g in groups)
      {
        if (g is not Dictionary<string, object?> group || group.GetValueOrDefault("firearmUid") is not string uid)
          continue;
        var list = new List<(string, Dictionary<string, object?>)>();
        if (SslText.Obj(group, "skins") is { } skinsObj)
        {
          foreach (var (key, value) in skinsObj)
          {
            if (value is Dictionary<string, object?> skin)
              list.Add((key, skin));
          }
        }

        c.Skins[uid] = list;
      }
    }

    if (SslText.Obj(SslText.Parse(preset), "regions") is { } regions)
    {
      foreach (var (name, value) in regions)
        c.Regions[name] = SslText.Strings((value as Dictionary<string, object?>)?.GetValueOrDefault("meshes")).ToList();
    }

    return c;
  }

  public static WeaponCustomization? Load(GameCatalog catalog, ArchiveStore store)
  {
    if (!catalog.TryGetByPath(SkinsPath, out var s) || !catalog.TryGetByPath(PresetPath, out var p))
      return null;
    var c = FromText(Encoding.UTF8.GetString(store.Read(s)), Encoding.UTF8.GetString(store.Read(p)));
    if (catalog.TryGetByPath(AttachingActorsPath, out var aa))
    {
      string? ClassText(string cls)
      {
        foreach (var hit in catalog.FindByFileName(cls + ".cls"))
        {
          if (catalog.GetFile(hit).Path.StartsWith("ssl/", StringComparison.OrdinalIgnoreCase))
          {
            try { return Encoding.UTF8.GetString(store.Read(hit)); } catch { }
          }
        }

        return null;
      }

      c.LoadAttachingActors(Encoding.UTF8.GetString(store.Read(aa)), ClassText);
    }

    var query = catalog.Query(null, "_client", AssetFilter.All);
    foreach (var fileIndex in query.Indices ?? Enumerable.Range(query.First, query.Count).ToArray())
    {
      var file = catalog.GetFile(fileIndex);
      if (!file.Path.StartsWith("ssl/weapons/common/firearm/", StringComparison.OrdinalIgnoreCase) || !file.Path.EndsWith("_client.cls", StringComparison.OrdinalIgnoreCase))
        continue;
      string text;
      try { text = Encoding.UTF8.GetString(store.Read(fileIndex)); } catch { continue; }
      var m = NameTpl.Match(text);
      if (m.Success)
        c.AddActor(file.Path, m.Groups[1].Value);
    }

    c.SortUids();
    return c;
  }

  /// <summary>
  ///   mtl1-49: one firearm actor <c>.../&lt;actor&gt;_client.cls</c> naming <paramref name="tpl"/>. The actor is the file stem
  ///   (not the folder: <c>firearm_versions/arifle_auto_bolt_rifle/arifle_auto_bolt_rifle_artificer_01_client.cls</c> is the actor
  ///   <c>arifle_auto_bolt_rifle_artificer_01</c>, which mtl1-47 wrongly filed under arifle_auto_bolt_rifle).
  /// </summary>
  public void AddActor(string path, string tpl)
  {
    var stem = Path.GetFileName(path);
    stem = stem.EndsWith("_client.cls", StringComparison.OrdinalIgnoreCase) ? stem[..^"_client.cls".Length] : Path.GetFileNameWithoutExtension(stem);
    ActorTpl[stem] = tpl;
    if (path.Replace('\\', '/').Contains("/mgs/", StringComparison.OrdinalIgnoreCase))
      ActorInMgs.Add(stem);
    if (Skins.ContainsKey(stem))
    {
      if (!UidsByTpl.TryGetValue(tpl, out var uids))
        UidsByTpl[tpl] = uids = new List<string>();
      if (!uids.Contains(stem, StringComparer.OrdinalIgnoreCase))
        uids.Add(stem);
    }
  }

  /// <summary>The weapon with the skin set first (not an aux/attachment user), plain uid before *_tac_reserve.</summary>
  public void SortUids()
  {
    foreach (var uids in UidsByTpl.Values)
      uids.Sort((a, b) => Skins[b].Count != Skins[a].Count ? Skins[b].Count.CompareTo(Skins[a].Count) : a.Length.CompareTo(b.Length));
  }

  /// <summary>overridedActorName values of every skin (mgs uids without their own skin group, e.g. arifle_bolt_rifle_artificer_01).</summary>
  public HashSet<string> OverrideTargets => Skins.Values.SelectMany(l => l).Select(s => s.Skin.GetValueOrDefault("overridedActorName") as string)
    .Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToHashSet(StringComparer.OrdinalIgnoreCase);

  /// <summary>Firearm actor (the <c>_client.cls</c> stem) → its geom nameTpl, for every firearm actor in the catalog (mtl1-49).</summary>
  public Dictionary<string, string> ActorTpl { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Actors defined in their own <c>mgs/&lt;actor&gt;/</c> folder (a weapon's own variant) rather than <c>firearm_versions/</c>.</summary>
  public HashSet<string> ActorInMgs { get; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Skin group that owns an actor: the actor itself, else the longest skin-group uid that prefixes it (<c>arifle_bolt_rifle_relic_01</c> → arifle_bolt_rifle).</summary>
  public string? OwnerUid(string actor)
  {
    if (Skins.ContainsKey(actor))
      return actor;
    return Skins.Keys.Where(u => actor.StartsWith(u + "_", StringComparison.OrdinalIgnoreCase)).OrderByDescending(u => u.Length).FirstOrDefault();
  }

  /// <summary>
  ///   mtl1-49 generic firearm resolution for a tpl (+ optional skin key, + optional explicit <c>uid=</c>):
  ///   1. an explicit skin-group uid wins;
  ///   2. with a skin: every group whose skin <paramref name="skin"/> has an overridedActorName whose actor names this tpl;
  ///   3. groups whose own actor names this tpl (<see cref="UidsByTpl"/>);
  ///   4. owners (<see cref="OwnerUid"/>) of any actor naming this tpl.
  ///   Ties: actor in <c>mgs/</c> before <c>firearm_versions/</c> (a weapon's own version before a re-used one), then the
  ///   shorter uid. E.g. wpn_bolt_rifle_01_artificer_01 is named by mgs/arifle_bolt_rifle_artificer_01 and by
  ///   firearm_versions/arifle_auto_bolt_rifle/arifle_auto_bolt_rifle_artificer_01 → arifle_bolt_rifle unless uid=arifle_auto_bolt_rifle.
  /// </summary>
  public (string? Uid, bool Override, List<string> Candidates) Resolve(string template, string? skin, string? explicitUid = null)
  {
    var actors = ActorTpl.Where(a => a.Value.Equals(template, StringComparison.OrdinalIgnoreCase)).Select(a => a.Key).ToList();
    int Rank(string actor) => ActorInMgs.Contains(actor) ? 0 : 1;
    if (!string.IsNullOrEmpty(explicitUid) && Skins.ContainsKey(explicitUid))
    {
      var over = skin is not null && Skins[explicitUid].Any(k => k.Key.Equals(skin, StringComparison.OrdinalIgnoreCase)
        && k.Skin.GetValueOrDefault("overridedActorName") is string o && actors.Contains(o, StringComparer.OrdinalIgnoreCase));
      return (explicitUid, over, new List<string> { explicitUid });
    }

    if (skin is not null)
    {
      var hits = new List<(string Uid, string Actor)>();
      foreach (var (uid, list) in Skins)
      {
        foreach (var (key, value) in list)
        {
          if (key.Equals(skin, StringComparison.OrdinalIgnoreCase) && value.GetValueOrDefault("overridedActorName") is string o
              && actors.Contains(o, StringComparer.OrdinalIgnoreCase))
            hits.Add((uid, o));
        }
      }

      if (hits.Count > 0)
      {
        var ordered = hits.OrderBy(h => Rank(h.Actor)).ThenBy(h => h.Uid.Length).ThenBy(h => h.Uid, StringComparer.OrdinalIgnoreCase).Select(h => h.Uid).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return (ordered[0], true, ordered);
      }
    }

    if (UidsByTpl.TryGetValue(template, out var direct) && direct.Count > 0)
      return (direct[0], false, direct.ToList());

    var owners = actors.Select(a => (Actor: a, Uid: OwnerUid(a))).Where(o => o.Uid is not null)
      .OrderBy(o => Rank(o.Actor)).ThenBy(o => o.Uid!.Length).ThenBy(o => o.Uid, StringComparer.OrdinalIgnoreCase)
      .Select(o => o.Uid!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    return (owners.FirstOrDefault(), false, owners);
  }

  /// <summary>mtl1-47 API: the skin-group uid whose skin switches to <paramref name="template"/> via overridedActorName.</summary>
  public string? OverrideUidFor(string template, string? skin)
    => skin is null ? null : Resolve(template, skin) is { Override: true } r ? r.Uid : null;

  public string? UidFor(string template) => Resolve(template, null).Uid;
}

public static class WeaponVariant
{
  /// <summary>
  ///   Hide every tpl object a firearm region lists except the regions of the selected skin.
  ///   HEURISTIC: region <c>main</c> (body_geom/back) stays shown. datapars: PropGeomCustomizerEquipment.Init (sslbin) is HideAll, then
  ///   initialBehaviour 0 ShowActiveRegions / 1 ShowDefault(defaultRegion); it never names <c>main</c>, and the firearm skin scripts
  ///   (weapons/creator/customization/firearm_skin_release.sslbin) are not decoded yet;
  ///   objects no region mentions keep their visibility.
  /// </summary>
  public static VariantResult Apply(ExportedMesh mesh, WeaponCustomization lib, string uid, VariantRequest request)
  {
    var result = new VariantResult { Template = mesh.Name, Spec = request.Spec, Team = "WEAPON", Appearance = uid };
    var skins = lib.Skins.GetValueOrDefault(uid) ?? new();
    result.Presets.AddRange(skins.Select(s => s.Key));
    var skinKey = request.Skin ?? skins.FirstOrDefault().Key;
    var skin = skins.FirstOrDefault(s => s.Key.Equals(skinKey, StringComparison.OrdinalIgnoreCase)).Skin;
    if (skin is null && request.WeaponRegions is null)
    {
      result.Notes.Add($"skin {skinKey} is not in {uid} (has {string.Join(", ", result.Presets)}); nothing hidden.");
      return result;
    }

    result.Preset = skinKey ?? "";
    var active = request.WeaponRegions ?? SslText.Strings(skin!.GetValueOrDefault("geomCustomizerRegions")).ToList();
    if (skin?.GetValueOrDefault("material") is string mat && mat.Length > 0)
      result.Material = mat;
    foreach (var (slot, locator, actor) in WeaponCustomization.SkinAttachments(skin))
    {
      var known = lib.AttachingActors.TryGetValue(actor, out var a);
      result.Attachments.Add(new VariantAttachment { Slot = slot, Locator = locator, Actor = actor, Template = known ? a.Tpl : "", Scale = known ? a.Scale : 1f, Class = known ? a.Class : "" });
      if (!known)
        result.Notes.Add($"attaching actor {actor} is not in {WeaponCustomization.AttachingActorsPath}.");
    }

    return ApplyRegions(mesh, lib.Regions, active.Append("main"), WeaponCustomization.PresetPath, request, result);
  }

  /// <summary>
  ///   Shared region filter (firearm and melee skins): hide every object some region of <paramref name="regions"/> lists, except
  ///   the subtrees of the <paramref name="active"/> regions; <c>X_LODn</c> siblings count as <c>X</c>; +obj/-obj apply last.
  /// </summary>
  public static VariantResult ApplyRegions(ExportedMesh mesh, IReadOnlyDictionary<string, List<string>> regions, IEnumerable<string> active, string presetPath, VariantRequest request, VariantResult result)
  {
    var bones = mesh.Bones;
    var byName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
    var children = new List<int>[bones.Count];
    for (var i = 0; i < bones.Count; i++)
    {
      var name = bones[i].SourceName.Length > 0 ? bones[i].SourceName : bones[i].Name;
      if (!byName.TryGetValue(name, out var l))
        byName[name] = l = new List<int>();
      l.Add(i);
      var lod = Regex.Match(name, "^(.+)_LOD\\d+$", RegexOptions.IgnoreCase); // base_01_LOD1 is a sibling node, not a child: same region
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
    foreach (var (region, meshes) in regions)
    {
      var present = meshes.Where(byName.ContainsKey).ToList();
      if (present.Count == 0)
        continue;
      result.AvailableRegions[region] = present;
      foreach (var n in present)
        AddSubtree(n, customizable);
    }

    result.CustomizableObjects = customizable.Count;
    var shown = new HashSet<int>();
    foreach (var region in active)
    {
      if (!regions.TryGetValue(region, out var meshes))
      {
        result.Notes.Add($"region {region} is not in {presetPath}.");
        continue;
      }

      result.Regions[region] = string.Join("|", meshes);
      foreach (var n in meshes)
      {
        if (byName.ContainsKey(n))
        {
          AddSubtree(n, shown);
          result.ShownObjects.Add(n);
        }
      }
    }

    var hidden = new HashSet<int>(customizable.Where(n => !shown.Contains(n)));
    foreach (var name in request.Hide)
      AddSubtree(name, hidden);
    var forced = new HashSet<int>();
    foreach (var name in request.Show)
      AddSubtree(name, forced);
    hidden.ExceptWith(forced);
    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)))
    {
      if (part.Node >= 0 && part.Visible && hidden.Contains(part.Node))
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

/// <summary>
///   mtl1-47: skin <c>material</c> (firearm and melee skins: mastercrafted, red, blue, white, yellow, green, violet, twitch, chaos).
///   mtl1-48 PROVEN path (static RE): the ssl binding <c>ApplyMaterialSuffix</c> (native FUN_141342870 → FUN_141ef5170) walks the
///   object (optionally its subtree) and FUN_1420bfa40 swaps each submesh material to the variant FUN_14261c830 finds by suffix in
///   the material list built from the diffuse texture's <c>.td</c> <c>materials</c> block (FUN_14261b0c0 / FUN_14262abb0,
///   "No default material found for texture '%s'"). So the data is
///   <c>td/&lt;diffuse&gt;.td materials.&lt;material&gt;.shaders.glt.tex { diffOverride, NM, spec, ... }</c>, used by <see cref="TdOverrides"/>
///   / <see cref="ApplyTd"/>; e.g. chainsword artificer <c>blue</c> = blue diffuse + the <b>red</b> NM/spec.
///   Fallback HEURISTIC (naming, when no diffuse td has the block): textures named
///   <c>&lt;base&gt;_&lt;material&gt;</c> beside the base ones, with optional <c>_nm</c> / <c>_spec</c> / <c>_em</c> siblings
///   (td/wpn_chainsword_01_mastercrafted{,_nm,_spec,_em}, wpn_bolt_rifle_01_artificer_01_01_red{,_spec}; blue has only the diffuse).
///   Each map whose material sibling exists is swapped. Maps without a sibling keep the base texture.
/// </summary>
public static class SkinMaterial
{
  private static readonly string[] Suffixes = { "_nm", "_spec", "_em" };

  /// <summary>mtl1-48: <c>materials.&lt;material&gt;.shaders.glt.tex</c> of a diffuse .td (key → texture name); null when absent.</summary>
  public static Dictionary<string, string>? TdOverrides(string tdText, string material)
  {
    var root = SslText.Parse(tdText);
    if (SslText.Obj(root, "materials", material) is not { } m)
      return null;
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (SslText.Obj(m, "shaders") is { } shaders)
    {
      foreach (var (_, sh) in shaders)
      {
        if (SslText.Obj(sh, "tex") is not { } tex)
          continue;
        foreach (var (key, value) in tex)
        {
          if (value is string name && name.Length > 0)
            map.TryAdd(key, name);
        }
      }
    }

    return map;
  }

  /// <summary>Role of a td material tex key: diffuse / normal / specular / emissive, else null (kept as an unknown key).</summary>
  public static string? Role(string key) => key.ToLowerInvariant() switch
  {
    "diffoverride" or "diff" or "md" or "diffuse" => "diffuse",
    "nm" or "normal" => "normal",
    "spec" or "specular" => "specular",
    "em" or "emissive" or "emis" => "emissive",
    _ => null,
  };

  /// <summary>
  ///   Applies the td material variant to every part whose diffuse td has <c>materials.&lt;material&gt;</c>. Returns
  ///   (texture references swapped, parts matched); <paramref name="unknown"/> collects tex keys with no role.
  /// </summary>
  public static (int Swaps, int Parts) ApplyTd(ExportedMesh mesh, string material, Func<string, string?> tdText, ICollection<string>? exclude, ISet<string>? unknown = null, IDictionary<string, string>? renamed = null)
  {
    var skip = Excluded(mesh, exclude);
    var cache = new Dictionary<string, Dictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
    int swaps = 0, parts = 0;
    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)))
    {
      if (string.IsNullOrWhiteSpace(part.Texture) || skip.Contains(part.Node) || (exclude?.Contains(part.Name ?? "") ?? false))
        continue;
      if (!cache.TryGetValue(part.Texture, out var ov))
        cache[part.Texture] = ov = tdText(part.Texture) is { } text ? TdOverrides(text, material) : null;
      if (ov is null)
        continue;
      parts++;
      var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var (key, name) in ov)
      {
        string? old;
        switch (Role(key))
        {
          case "diffuse": old = part.Texture; part.Texture = name; break;
          case "normal": old = part.Normal; part.Normal = name; break;
          case "specular": old = part.Specular; part.Specular = name; break;
          case "emissive": old = part.Emissive; part.Emissive = name; break;
          default: unknown?.Add(key); continue;
        }

        swaps++;
        if (!string.IsNullOrEmpty(old))
        {
          renames[old] = name;
          renamed?.TryAdd(old, name);
        }
      }

      foreach (var map in part.Maps)
        if (renames.TryGetValue(map.Name, out var newName))
          map.Name = newName;
    }

    return (swaps, parts);
  }

  private static HashSet<int> Excluded(ExportedMesh mesh, ICollection<string>? exclude)
  {
    var skip = new HashSet<int>();
    if (exclude is not { Count: > 0 })
      return skip;
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      for (var b = i; b >= 0 && b < mesh.Bones.Count; b = mesh.Bones[b].Parent)
      {
        if (exclude.Contains(mesh.Bones[b].Name) || exclude.Contains(mesh.Bones[b].SourceName ?? ""))
        {
          skip.Add(i);
          break;
        }
      }
    }

    return skip;
  }

  /// <summary><c>base_nm</c> -> <c>base_red_nm</c>; <c>base</c> -> <c>base_red</c>.</summary>
  public static string Candidate(string name, string material)
  {
    foreach (var suffix in Suffixes)
    {
      if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        return name[..^suffix.Length] + "_" + material + name[^suffix.Length..];
    }

    return name + "_" + material;
  }

  /// <summary>Returns the number of map names swapped (diffuse, normal, specular, emissive and the material maps of every part).</summary>
  public static int Apply(ExportedMesh mesh, string material, Func<string, bool> exists, ICollection<string>? exclude = null)
  {
    var skip = Excluded(mesh, exclude);
    var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var swapped = 0;
    string? Swap(string? name)
    {
      if (string.IsNullOrWhiteSpace(name) || name.Contains("_" + material, StringComparison.OrdinalIgnoreCase))
        return name;
      if (!cache.TryGetValue(name, out var hit))
        cache[name] = hit = exists(Candidate(name, material)) ? Candidate(name, material) : null;
      if (hit is null)
        return name;
      swapped++;
      return hit;
    }

    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)))
    {
      if (skip.Contains(part.Node) || (exclude?.Contains(part.Name ?? "") ?? false))
        continue;
      part.Texture = Swap(part.Texture);
      part.Normal = Swap(part.Normal);
      part.Specular = Swap(part.Specular);
      part.Emissive = Swap(part.Emissive);
      foreach (var map in part.Maps)
        map.Name = Swap(map.Name) ?? map.Name;
    }

    return swapped;
  }
}

