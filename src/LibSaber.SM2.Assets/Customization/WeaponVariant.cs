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
      var uid = Path.GetFileName(Path.GetDirectoryName(file.Path) ?? "");
      if (!c.Skins.ContainsKey(uid))
        continue;
      string text;
      try { text = Encoding.UTF8.GetString(store.Read(fileIndex)); } catch { continue; }
      var m = NameTpl.Match(text);
      if (!m.Success)
        continue;
      if (!c.UidsByTpl.TryGetValue(m.Groups[1].Value, out var uids))
        c.UidsByTpl[m.Groups[1].Value] = uids = new List<string>();
      if (!uids.Contains(uid, StringComparer.OrdinalIgnoreCase))
        uids.Add(uid);
    }

    foreach (var uids in c.UidsByTpl.Values)
      uids.Sort((a, b) => c.Skins[b].Count != c.Skins[a].Count ? c.Skins[b].Count.CompareTo(c.Skins[a].Count) : a.Length.CompareTo(b.Length)); // the weapon with the skin set (not an aux/attachment user), plain uid before *_tac_reserve
    return c;
  }

  public string? UidFor(string template) => UidsByTpl.TryGetValue(template, out var u) && u.Count > 0 ? u[0] : null;
}

public static class WeaponVariant
{
  /// <summary>
  ///   Hide every tpl object a firearm region lists except the regions of the selected skin.
  ///   HEURISTIC: region <c>main</c> (body_geom/back) stays shown (equipment defaultRegion is not in the data we decode);
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
    if (skin?.GetValueOrDefault("material") is string mat)
      result.Notes.Add("skin material " + mat + " (not applied, mesh only)");
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
