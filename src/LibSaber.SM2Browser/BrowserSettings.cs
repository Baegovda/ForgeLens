using System.Text.Json;

namespace LibSaber.SM2Browser;

public sealed class BrowserSettings
{

  public string? LastFolder { get; set; }
  public string? LastExportFolder { get; set; }
  /// <summary>oneclick: last .uproject picked for "Direct to Unreal".</summary>
  public string? LastUnrealProject { get; set; }
  /// <summary>mtl1-42: file list column widths (TYPE, SIZE, ARCHIVE; PATH takes the rest) and the folder / file pane widths.</summary>
  /// <summary>mtl1-42: list auxiliary / text files too (off = only previewable assets).</summary>
  public bool ShowAllFiles { get; set; }
  public double? FileTypeWidth { get; set; }
  public double? FileSizeWidth { get; set; }
  public double? FileArchiveWidth { get; set; }
  public double? FolderPaneWidth { get; set; }
  public double? FilePaneWidth { get; set; }
  /// <summary>mtl1-42: browser state restored on the next run (folder tree, selection, scroll, search / filters).</summary>
  public List<string>? ExpandedFolders { get; set; }
  public string? SelectedFolder { get; set; }
  public string? SelectedFile { get; set; }
  public string? SelectedFileArchive { get; set; }
  public double? FileScroll { get; set; }
  public string? SearchText { get; set; }
  public int? FilterIndex { get; set; }
  public bool IncludeSubfolders { get; set; }
  /// <summary>ui-43 Content Browser: pinned folders, collections, tree mode (Folders / Categories), selected category or
  /// collection, Grid / List view, tile size and "animate all thumbnails".</summary>
  public List<string>? Favorites { get; set; }
  public List<ContentBrowser.CollectionData>? Collections { get; set; }
  public string? TreeMode { get; set; }
  public string? SelectedCategory { get; set; }
  public string? SelectedCollection { get; set; }
  public string? ViewMode { get; set; }
  public double? TileSize { get; set; }
  public bool AnimateAll { get; set; }
  /// <summary>ui-44 Kitbasher: panel open, picked team / class / preset / part per slot, and the two option filters.</summary>
  public bool KitbashOpen { get; set; }
  public string? KitbashTeam { get; set; }
  public string? KitbashClass { get; set; }
  public string? KitbashPreset { get; set; }
  public Dictionary<string, string>? KitbashSlots { get; set; }
  public bool KitbashAllClasses { get; set; }
  public bool KitbashShowMissing { get; set; }
  /// <summary>ui-45: Kitbash mode ("npc" = enemy / NPC inst_constructor templates, else Space Marine armor), NPC template,
  /// custom preset (.cls stem), layer picks per template (MeshTemplate key or "none"), and the armor mode's power fist skin.</summary>
  public string? KitbashMode { get; set; }
  public string? KitbashNpcTemplate { get; set; }
  public string? KitbashNpcPreset { get; set; }
  public Dictionary<string, Dictionary<string, string>>? KitbashNpcLayers { get; set; }
  public string? KitbashPowerFist { get; set; }
  /// <summary>propanim: context view "Prop anim" (null = default on) and the last playback condition (null/Auto).</summary>
  public bool? PropAnim { get; set; }
  public string? PropCondition { get; set; }

  public static BrowserSettings Load()
  {
    try
    {
      var path = FilePath();
      if (!File.Exists(path))
        return new BrowserSettings();
      return JsonSerializer.Deserialize<BrowserSettings>(File.ReadAllText(path)) ?? new BrowserSettings();
    }
    catch
    {
      return new BrowserSettings();
    }
  }

  public void Save()
  {
    var path = FilePath();
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
  }

  private static string FilePath()
    => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "LibSaber",
      "SM2Browser",
      "settings.json");

}
