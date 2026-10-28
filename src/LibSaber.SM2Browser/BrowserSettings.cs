using System.Text.Json;

namespace LibSaber.SM2Browser;

public sealed class BrowserSettings
{

  public string? LastFolder { get; set; }
  public string? LastExportFolder { get; set; }
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
