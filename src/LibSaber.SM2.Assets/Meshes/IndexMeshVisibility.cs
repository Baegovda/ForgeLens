using LibSaber.SpaceMarine2.Structures;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   IndexV2's default mesh checkboxes
///   (<c>IdentifyMeshesJob</c> + <c>SceneViewModel.ApplyMeshAsset</c>).
///   A node is named <c>obj.GetName()</c>. It starts hidden when that name is in the
///   LOD set or, failing that, the volume set. The LOD set stores <c>GetMeshName()</c>
///   for every object whose mesh name contains <c>_LOD</c>. <c>LodIndices</c> is empty
///   in Index because <c>AddLodDefinitions</c> is commented out, so it adds nothing here.
///   Volume affixes and the <c>cdt</c> / <c>_glr</c> / <c>rb_</c> name tests match the
///   job, including the branch where a non-empty affix list is what allows the name test.
///   <c>_SIM</c> is not a hide rule. <c>tpl_markup</c> <c>visibleDefault</c> is not consulted:
///   Index leaves <c>cloth_stamp_leg_SIM</c> checked even though the markup says otherwise.
/// </summary>
public static class IndexMeshVisibility
{
  private static readonly HashSet<string> VolumeAffixes = new()
  {
    "visibility_hidden",
    "visibility_occluder",
    "lighting_sm_invisible_shadowcaster_affix",
  };

  public static void Classify(objGEOM_MNG graph, out HashSet<string> lodNames, out HashSet<string> volumeNames)
  {
    lodNames = new HashSet<string>();
    volumeNames = new HashSet<string>();
    if (graph.objects is null)
      return;

    foreach (var obj in graph.objects)
    {
      var meshName = obj.GetMeshName();
      if (meshName is not null && meshName.Contains("_LOD", StringComparison.InvariantCultureIgnoreCase))
        lodNames.Add(meshName);
    }

    AddCompositeLods(graph, lodNames);
    AddMergedProxies(graph, lodNames);

    foreach (var obj in graph.objects)
    {
      var objName = obj.GetName();
      if (string.IsNullOrEmpty(objName))
        continue;
      if (obj.Affixes is not null)
      {
        var affixes = obj.Affixes.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var affix in affixes)
        {
          if (VolumeAffixes.Contains(affix))
          {
            volumeNames.Add(objName);
            break;
          }

          if (objName.Contains("cdt", StringComparison.InvariantCultureIgnoreCase))
            volumeNames.Add(objName);
          else if (objName.Contains("_glr", StringComparison.Ordinal))
            volumeNames.Add(objName);
          else if (objName.StartsWith("rb_", StringComparison.Ordinal))
            volumeNames.Add(objName);
        }
      }
      else if (objName.Contains("cdt", StringComparison.InvariantCultureIgnoreCase))
        volumeNames.Add(objName);
      else if (objName.Contains("_glr", StringComparison.Ordinal))
        volumeNames.Add(objName);
      else if (objName.StartsWith("rb_", StringComparison.Ordinal))
        volumeNames.Add(objName);
    }
  }

  /// <summary>
  ///   Weapons such as wpn_bolt_rifle_01 also carry 3ds Max default-named merged meshes (<c>Object0080</c> ..
  ///   <c>Object0098</c>, children of the root, no mesh name) in every vertex pool next to the
  ///   <c>_LODn</c> chain. They are whole-model proxies (one is a 1.2 m shell around the gun) and stack into a
  ///   drum when shown with LOD0. Only applied when the template has a <c>_LOD</c> chain.
  /// </summary>
  internal static void AddMergedProxies(objGEOM_MNG graph, HashSet<string> lodNames)
  {
    var objects = graph.objects;
    if (objects is null || !objects.Any(obj => (obj.GetMeshName() ?? "").Contains("_LOD", StringComparison.InvariantCultureIgnoreCase)))
      return; // props and composites (rhino, doors) use Object* names for real geometry
    foreach (var obj in objects)
    {
      var name = obj.GetName();
      if (name is not { Length: > 6 } || !name.StartsWith("Object", StringComparison.Ordinal)
          || !name.Skip(6).All(char.IsAsciiHexDigit) || !string.IsNullOrEmpty(obj.GetMeshName())
          || (obj.Affixes ?? "").Contains("visibility_", StringComparison.Ordinal))
        continue;
      lodNames.Add(name);
    }
  }

  /// <summary>
  ///   Vehicles such as refrt_imp_rhino_01 are LOD composites. A <c>lod_composite_marker</c>
  ///   group holds level 0 parts (affixes <c>compound_skin_lod_index / index / 0</c>, plus
  ///   <c>lod_composite_root_part_marker</c>), and its sibling <c>lod_composite_marker</c>
  ///   objects are the coarser levels: groups whose parts say index 1, 2, ... and merged far
  ///   proxies that are markers with geometry of their own. Every level shares one vertex
  ///   pool, so without this all levels export stacked on top of each other.
  ///   Parts with index &gt; 0, and every marker sibling of an index 0 part (with its
  ///   descendants), are treated as LOD shells.
  /// </summary>
  internal static void AddCompositeLods(objGEOM_MNG graph, HashSet<string> lodNames)
  {
    var objects = graph.objects;
    int Level(objOBJ obj)
    {
      if (obj.Affixes is null) return -1;
      var tokens = obj.Affixes.Split("\n", StringSplitOptions.RemoveEmptyEntries);
      var i = Array.IndexOf(tokens, "compound_skin_lod_index");
      return i >= 0 && i + 2 < tokens.Length && tokens[i + 1] == "index" && int.TryParse(tokens[i + 2], out var n) ? n : -1;
    }
    bool Marker(objOBJ obj) => obj.Affixes is not null
      && obj.Affixes.Split("\n", StringSplitOptions.RemoveEmptyEntries).Contains("lod_composite_marker");

    var coarse = new HashSet<int>();
    foreach (var obj in objects)
    {
      var level = Level(obj);
      if (level > 0)
        coarse.Add(obj.id);
      if (level != 0)
        continue;
      foreach (var sibling in objects)
        if (sibling.parentId == obj.parentId && sibling.id != obj.id && Level(sibling) < 0 && Marker(sibling))
          coarse.Add(sibling.id);
    }

    if (coarse.Count == 0)
      return;
    foreach (var obj in objects)
    {
      var walk = obj;
      for (var guard = 0; walk is not null && guard < 256; guard++)
      {
        if (coarse.Contains(walk.id))
        {
          var name = obj.GetName();
          if (!string.IsNullOrEmpty(name))
            lodNames.Add(name);
          break;
        }
        walk = walk.parentId >= 0 && walk.parentId < objects.Count ? objects[walk.parentId] : null;
      }
    }
  }

  public static bool Hidden(string? nodeName, HashSet<string> lodNames, HashSet<string> volumeNames, out string? reason)
  {
    reason = null;
    if (string.IsNullOrEmpty(nodeName))
      return false;
    if (lodNames.Contains(nodeName))
    {
      reason = "LOD";
      return true;
    }

    if (volumeNames.Contains(nodeName))
    {
      reason = "volume";
      return true;
    }

    return false;
  }
}
