using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2.Assets.Export;

public enum ClipChoice
{
  /// <summary>Selected .sani rows export as standalone .anim.json (the old behavior).</summary>
  None,
  /// <summary>Selected .sani rows become anim-only FBX against each selected model's skeleton.</summary>
  SelectedClips,
  /// <summary>The clip playing in the preview.</summary>
  PreviewClip,
  /// <summary>Every clip sharing enough bone names with the model (the extractor's <c>--anim all</c>).</summary>
  AllMatching,
}

/// <summary>
///   The browser's Export button: turns a list selection plus <see cref="ClipChoice"/> into the file list and the
///   <see cref="ExportSettings.Clips"/> that <see cref="AssetExporter.Export"/> takes. The extractor builds the same settings.
/// </summary>
public static class ExportPlan
{
  public static List<int> Apply(
    GameCatalog catalog,
    ArchiveStore store,
    IReadOnlyList<int> selection,
    ExportSettings settings,
    ClipChoice choice,
    int previewClipIndex,
    List<string> notes)
  {
    var files = selection.Distinct().ToList();
    var models = files.Where(index => catalog.GetFile(index).Kind == AssetKind.Model).ToList();
    if (choice == ClipChoice.None || models.Count == 0)
      return files;

    var sani = files.Where(index => catalog.GetFile(index).Kind == AssetKind.Animation).ToList();
    var clipIndices = new List<int>();
    switch (choice)
    {
      case ClipChoice.SelectedClips:
        clipIndices.AddRange(sani);
        if (clipIndices.Count == 0)
          notes.Add("No .sani rows were selected, so no clips were exported.");
        break;
      case ClipChoice.PreviewClip:
        if (previewClipIndex >= 0)
          clipIndices.Add(previewClipIndex);
        else
          notes.Add("No clip is playing in the preview, so no clips were exported.");
        break;
      case ClipChoice.AllMatching:
        var headers = SaniIndex.ReadHeaders(catalog, store);
        foreach (var model in models)
        {
          var chosen = ClipResolver.Choose(catalog, store, model, "all", out var error, headers);
          if (chosen.Count == 0)
            notes.Add(catalog.GetFile(model).Path + ": " + error);
          clipIndices.AddRange(chosen.Select(header => header.Index));
        }

        break;
    }

    settings.Clips = ClipResolver.Load(catalog, store, clipIndices, notes);
    // The clips ride on the model. Do not also write them as standalone .anim.json.
    files.RemoveAll(index => sani.Contains(index));
    return files;
  }
}
