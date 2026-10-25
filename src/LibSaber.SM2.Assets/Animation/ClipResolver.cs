using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>
///   Picks the .sani clips exported beside a model. Shared by the extractor (<c>--anim</c>) and the browser's Export window,
///   so both write the same <c>{model}__{clip}.fbx</c> files.
/// </summary>
public static class ClipResolver
{
  /// <param name="token"><c>all</c> for every clip sharing <see cref="SaniIndex.MinimumSharedBones"/> bone names, else a path substring.</param>
  public static List<SaniIndex.Header> Choose(GameCatalog catalog, ArchiveStore store, int modelIndex, string token, out string error, IReadOnlyList<SaniIndex.Header>? headers = null)
  {
    error = "";
    headers ??= SaniIndex.ReadHeaders(catalog, store);
    if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
      var mesh = TemplateMeshReader.Load(catalog, store, modelIndex);
      var chosen = SaniIndex.Compatible(headers, mesh.Bones.Select(bone => bone.Name).ToList());
      if (chosen.Count == 0)
        error = $"No .sani shares at least {SaniIndex.MinimumSharedBones} bone names with this model. Pass --anim and a clip path to force one.";
      return chosen;
    }

    var matched = SaniIndex.Match(headers, token);
    if (matched.Count == 0)
      error = $"No .sani matches '{token}'.";
    return matched;
  }

  /// <summary>Loads clips by catalog index, keeping those with curve keys. Problems go to <paramref name="notes"/>.</summary>
  public static List<AnimationClip> Load(GameCatalog catalog, ArchiveStore store, IEnumerable<int> saniIndices, List<string> notes)
  {
    var clips = new List<AnimationClip>();
    foreach (var index in saniIndices.Distinct())
    {
      var clip = SaniReader.Load(catalog, store, index);
      if (!clip.HasCurves)
      {
        notes.Add(catalog.GetFile(index).Path + ": " + (clip.Notes.Count == 0 ? "no curve keys." : clip.Notes[0]));
        continue;
      }

      clips.Add(clip);
    }

    return clips;
  }
}
