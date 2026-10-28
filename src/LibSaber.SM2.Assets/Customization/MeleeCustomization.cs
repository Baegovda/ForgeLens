using System.Text;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>One <c>MeleeSkinDescription</c> of melee_skins_library.sso.</summary>
public sealed record MeleeSkin(string Actor, string Key, IReadOnlyList<string> Regions, string? Material, string? ActorOverride, bool AvoidGeomFiltration)
{
  /// <summary>Spec value: <c>KEY</c> for the tpl's main actor, <c>ACTOR/KEY</c> otherwise.</summary>
  public string Spec(string mainActor) => Actor.Equals(mainActor, StringComparison.OrdinalIgnoreCase) ? Key : Actor + "/" + Key;
}

/// <summary>
///   ui-45 melee region skins (REA-first: ssl data only, same shape as firearm skins):
///   <c>ssl/melee/weapon_actors/melee_skins_library.sso</c> meleeActors.ACTOR = MeleeActorDescription { defaultMeleeActor,
///   skins{ KEY = MeleeSkinDescription { geomCustomizerRegions[]?, material?, meleeActorOverride?, hackShouldAvoidGeomFiltration? } } };
///   region names resolve in <c>ssl/geom_customizer/melee/geom_customizer_preset_melee.sso</c> regions{ name = { meshes[] } }.
///   The actor's tpl: <c>ssl/melee/weapon_actors/melee_actor_factory.sso</c> actorDescs.ACTOR.type.__type names a .cls whose
///   geom.nameTpl is the tpl (an actorDesc properties.geom.nameTpl overrides it).
///   power_fist_fake (cls tpl sfx_power_fist_inc) skins name powerfist_* regions whose objects live on the cc_sm/ch_constructor tpls.
/// </summary>
public sealed class MeleeCustomization
{
  public const string SkinsPath = "ssl/melee/weapon_actors/melee_skins_library.sso";
  public const string PresetPath = "ssl/geom_customizer/melee/geom_customizer_preset_melee.sso";
  public const string FactoryPath = "ssl/melee/weapon_actors/melee_actor_factory.sso";
  public const string PowerFistActor = "power_fist_fake";
  /// <summary>mtl1-47: powerFistIds + visibilityUnchangableMeshes ("Meshes (of hand), which visibility can't be changed by this system").</summary>
  public const string PowerFistPropPath = "ssl/body/customization/prop_power_fist_customizer_base.prop";
  /// <summary>
  ///   HEURISTIC: the hand region is the RIGHT_GAUNTLET body section. prop_power_fist_customizer_base has handRegion /
  ///   SetRightHandVisibility / FilterRightHandMeshes and keeps torso_r_gauntlet (a right gauntlet object); the CharacterBodySection
  ///   constant in its bytecode is not decoded.
  /// </summary>
  public const string HandSection = "RIGHT_GAUNTLET";
  public List<string> HandKeep { get; } = new() { "torso_r_gauntlet", "upper_arm_librarium_T", "upper_arm_librarium_G" };
  private static readonly Regex NameTpl = new("nameTpl\\s*=\\s*\"([^\"]+)\"", RegexOptions.CultureInvariant);

  public Dictionary<string, List<MeleeSkin>> Skins { get; } = new(StringComparer.OrdinalIgnoreCase);
  public Dictionary<string, List<string>> Regions { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>actor → excludeObjectsToApplyMaterial (objects that keep their material when a skin material applies).</summary>
  public Dictionary<string, List<string>> MaterialExclude { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>actor → .cls name (factory type) and optional tpl override.</summary>
  public Dictionary<string, (string? Cls, string? Tpl)> Factory { get; } = new(StringComparer.OrdinalIgnoreCase);
  /// <summary>actor → tpl (after <see cref="ResolveTpls"/>).</summary>
  public Dictionary<string, string> ActorTpl { get; } = new(StringComparer.OrdinalIgnoreCase);

  public static MeleeCustomization FromText(string skins, string preset, string? factory)
  {
    var c = new MeleeCustomization();
    if (SslText.Obj(SslText.Parse(skins), "meleeActors") is { } actors)
    {
      foreach (var (actor, value) in actors)
      {
        var list = new List<MeleeSkin>();
        if (SslText.Obj(value as Dictionary<string, object?>, "skins") is { } map)
        {
          foreach (var (key, sv) in map)
          {
            if (sv is not Dictionary<string, object?> s)
              continue;
            list.Add(new MeleeSkin(actor, key, SslText.Strings(s.GetValueOrDefault("geomCustomizerRegions")).Where(r => r.Length > 0).ToList(),
              s.GetValueOrDefault("material") as string, s.GetValueOrDefault("meleeActorOverride") as string,
              s.GetValueOrDefault("hackShouldAvoidGeomFiltration") is true or "True"));
          }
        }

        c.Skins[actor] = list;
        var exclude = SslText.Strings((value as Dictionary<string, object?>)?.GetValueOrDefault("excludeObjectsToApplyMaterial")).Where(n => n.Length > 0).ToList();
        if (exclude.Count > 0)
          c.MaterialExclude[actor] = exclude;
      }
    }

    if (SslText.Obj(SslText.Parse(preset), "regions") is { } regions)
    {
      foreach (var (name, value) in regions)
        c.Regions[name] = SslText.Strings((value as Dictionary<string, object?>)?.GetValueOrDefault("meshes")).Where(n => n.Length > 0).ToList();
    }

    if (factory is not null && SslText.Obj(SslText.Parse(factory), "actorDescs") is { } descs)
    {
      foreach (var (actor, value) in descs)
      {
        var d = value as Dictionary<string, object?>;
        c.Factory[actor] = (SslText.Obj(d, "type")?.GetValueOrDefault("__type") as string, (SslText.Obj(d, "type", "properties", "geom") ?? SslText.Obj(d, "properties", "geom"))?.GetValueOrDefault("nameTpl") as string);
        // mtl1-47: retail nests the nameTpl override under type.properties.geom (chainsword_01_artificer_01 → wpn_chainsword_01_artificer_01)
      }
    }

    return c;
  }

  /// <summary>Fills <see cref="ActorTpl"/>; <paramref name="clsTpl"/> returns a .cls name's geom.nameTpl.</summary>
  public void ResolveTpls(Func<string, string?> clsTpl)
  {
    foreach (var (actor, (cls, tpl)) in Factory)
    {
      var t = tpl ?? (cls is null ? null : clsTpl(cls));
      if (!string.IsNullOrEmpty(t))
        ActorTpl[actor] = t;
    }
  }

  /// <summary>Region-skinned actors (any skin with geomCustomizerRegions) whose tpl is <paramref name="tpl"/>; the actor named like its own defaultMeleeActor and with most skins first.</summary>
  public List<string> ActorsFor(string tpl)
    => ActorTpl.Where(a => a.Value.Equals(tpl, StringComparison.OrdinalIgnoreCase) && Skins.TryGetValue(a.Key, out var s) && s.Any(x => x.Regions.Count > 0))
      .Select(a => a.Key)
      .OrderBy(a => a.StartsWith("melee_mod_", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
      .ThenByDescending(a => Skins[a].Count).ThenBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();

  /// <summary>Region skins for a tpl: every skin of every region actor on it (main actor first).</summary>
  public List<MeleeSkin> SkinsFor(string tpl) => ActorsFor(tpl).SelectMany(a => Skins[a].Where(s => s.Regions.Count > 0)).ToList();

  /// <summary>
  ///   mtl1-47: skins of other actors whose meleeActorOverride resolves to this tpl (e.g. chainsword 01_red →
  ///   chainsword_01_artificer_01): the in-game look of that skin is this tpl plus the skin's material.
  /// </summary>
  public List<MeleeSkin> OverrideSkinsFor(string tpl)
    => Skins.Values.SelectMany(list => list)
      .Where(s => s.ActorOverride is { } o && !o.Equals(s.Actor, StringComparison.OrdinalIgnoreCase)
        && ActorTpl.TryGetValue(o, out var t) && t.Equals(tpl, StringComparison.OrdinalIgnoreCase))
      .ToList();

  /// <summary>Power fist skins (actor power_fist_fake) for constructor tpls.</summary>
  public List<MeleeSkin> PowerFistSkins => Skins.GetValueOrDefault(PowerFistActor)?.Where(s => s.Regions.Count > 0).ToList() ?? new();

  public MeleeSkin? Find(string tpl, string spec)
  {
    var slash = spec.IndexOf('/');
    var actor = slash > 0 ? spec[..slash] : ActorsFor(tpl).FirstOrDefault();
    var key = slash > 0 ? spec[(slash + 1)..] : spec;
    return actor is null ? null : Skins.GetValueOrDefault(actor)?.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>Objects of every powerfist_* region (what a constructor tpl's power fist pick chooses among).</summary>
  public IEnumerable<string> PowerFistObjects(MeleeSkin skin) => skin.Regions.SelectMany(r => Regions.GetValueOrDefault(r) ?? new());

  /// <summary>
  ///   ui-45 <c>powerfist=KEY</c> on a constructor tpl: forces the skin's powerfist_* region objects on (ArmorVariant hides every
  ///   powerfist_* otherwise) and hides the right hand it replaces (mtl1-47, <see cref="HandSection"/> minus <see cref="HandKeep"/>);
  ///   the ch_powerfist_01_grp arm stub is skipped on cc_sm_ tpls. Returns a note when the key is unknown.
  /// </summary>
  public string? AddPowerFist(VariantRequest request, string template)
  {
    if (request.PowerFist is not { } fist)
      return null;
    var skin = PowerFistSkins.FirstOrDefault(s => s.Key.Equals(fist, StringComparison.OrdinalIgnoreCase));
    if (skin is null)
      return $"powerfist={fist} is not a {PowerFistActor} skin in {SkinsPath}.";
    request.HideHandSection = HandSection;
    request.HandKeep.UnionWith(HandKeep);
    foreach (var obj in PowerFistObjects(skin))
    {
      if (obj.StartsWith("ch_", StringComparison.OrdinalIgnoreCase) && template.StartsWith("cc_sm_", StringComparison.OrdinalIgnoreCase))
        continue;
      request.Show.Add(obj);
      request.HandFist.Add(obj);
    }

    return null;
  }

  public static MeleeCustomization? Load(GameCatalog catalog, ArchiveStore store)
  {
    if (!catalog.TryGetByPath(SkinsPath, out var s) || !catalog.TryGetByPath(PresetPath, out var p))
      return null;
    string? factory = catalog.TryGetByPath(FactoryPath, out var f) ? Encoding.UTF8.GetString(store.Read(f)) : null;
    var c = FromText(Encoding.UTF8.GetString(store.Read(s)), Encoding.UTF8.GetString(store.Read(p)), factory);
    if (catalog.TryGetByPath(PowerFistPropPath, out var fp))
    {
      try
      {
        var keep = SslText.Strings(SslText.Parse(Encoding.UTF8.GetString(store.Read(fp))).GetValueOrDefault("visibilityUnchangableMeshes")).Where(n => n.Length > 0).ToList();
        if (keep.Count > 0)
        {
          c.HandKeep.Clear();
          c.HandKeep.AddRange(keep);
        }
      }
      catch
      {
        // keep the built-in list (same as the 2026 retail .prop)
      }
    }

    c.ResolveTpls(cls =>
    {
      foreach (var index in catalog.FindByFileName(cls + ".cls"))
      {
        try
        {
          var m = NameTpl.Match(Encoding.UTF8.GetString(store.Read(index)));
          if (m.Success)
            return m.Groups[1].Value;
        }
        catch
        {
          // unreadable cls: try the next copy
        }
      }

      return null;
    });
    return c;
  }
}

public static class MeleeVariant
{
  /// <summary>
  ///   Shows the picked skin's regions and hides the objects of every other region present in the tpl.
  ///   HEURISTIC: objects no melee region lists keep their visibility (as firearm skins, without a "main" region);
  ///   a skin with meleeActorOverride may switch tpl in game: noted, not followed.
  /// </summary>
  public static VariantResult Apply(ExportedMesh mesh, MeleeCustomization lib, string tpl, VariantRequest request)
  {
    var actors = lib.ActorsFor(tpl);
    var main = actors.FirstOrDefault() ?? "";
    var result = new VariantResult { Template = mesh.Name, Spec = request.Spec, Team = "MELEE", Appearance = main };
    result.Presets.AddRange(lib.SkinsFor(tpl).Select(s => s.Spec(main)));
    var skin = request.Skin is { } want ? lib.Find(tpl, want) : lib.SkinsFor(tpl).FirstOrDefault();
    if (actors.Count == 0 && request.Skin is { } key)
    {
      // material-only: a skin of another actor that switches to this tpl (no regions here)
      var os = lib.OverrideSkinsFor(tpl).FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
        || (s.Actor + "/" + s.Key).Equals(key, StringComparison.OrdinalIgnoreCase));
      result.Presets.AddRange(lib.OverrideSkinsFor(tpl).Select(s => s.Actor + "/" + s.Key));
      if (os is null)
      {
        result.Notes.Add($"melee skin {key} does not switch to {tpl}; nothing applied.");
        return result;
      }

      result.Preset = os.Actor + "/" + os.Key;
      result.Appearance = os.Actor;
      if (os.Material is { Length: > 0 } omat)
      {
        result.Material = omat;
        foreach (var a in new[] { os.Actor, os.ActorOverride! })
          if (lib.MaterialExclude.TryGetValue(a, out var ex))
            result.MaterialExclude.UnionWith(ex);
      }

      result.Notes.Add($"skin {os.Actor}/{os.Key} switches to actor {os.ActorOverride} (this tpl); material only, no regions.");
      return result;
    }

    if (skin is null && request.WeaponRegions is null)
    {
      result.Notes.Add($"melee skin {request.Skin} is not a region skin of {tpl} ({string.Join(", ", actors)}); nothing hidden.");
      return result;
    }

    result.Preset = skin?.Spec(main) ?? "";
    var active = request.WeaponRegions ?? skin!.Regions.ToList();
    if (skin?.Material is { Length: > 0 } mat)
    {
      result.Material = mat;
      foreach (var a in new[] { skin.Actor, skin.ActorOverride ?? skin.Actor })
        if (lib.MaterialExclude.TryGetValue(a, out var exclude))
          result.MaterialExclude.UnionWith(exclude);
    }
    if (skin?.ActorOverride is { } over && !over.Equals(skin.Actor, StringComparison.OrdinalIgnoreCase))
      result.Notes.Add($"skin switches to actor {over}" + (lib.ActorTpl.TryGetValue(over, out var otpl) ? $" (tpl {otpl})" : "") + " in game; this tpl shows its regions only.");
    // only regions this tpl's actors use: the preset also has generic names (body, geom, tip) other tpls own
    var used = new HashSet<string>(lib.SkinsFor(tpl).SelectMany(s => s.Regions).Concat(active), StringComparer.OrdinalIgnoreCase);
    var present = lib.Regions.Where(r => r.Value.Count > 0 && used.Contains(r.Key)).ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
    return WeaponVariant.ApplyRegions(mesh, present, active, MeleeCustomization.PresetPath, request, result);
  }
}
