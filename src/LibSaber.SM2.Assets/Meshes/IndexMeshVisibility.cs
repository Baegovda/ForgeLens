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
