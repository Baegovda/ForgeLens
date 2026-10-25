using System.Collections.ObjectModel;
using System.Text.Json;

namespace LibSaber.SM2Browser;

public sealed class RecentEntry
{

  public string Path { get; set; } = "";
  public string Archive { get; set; } = "";

}

/// <summary>
///   Recently selected assets. The file lives in the roaming profile
///   (%APPDATA%/LibSaber.SM2Browser/recent.json on Windows) so it survives the next launch.
/// </summary>
public sealed class RecentAssets
{

  public const int Limit = 40;

  public ObservableCollection<RecentEntry> Items { get; } = new();

  public static string FilePath()
    => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "LibSaber.SM2Browser",
      "recent.json");

  public static RecentAssets Load()
    => Load(FilePath());

  public static RecentAssets Load(string path)
  {
    var recent = new RecentAssets();
    try
    {
      if (!File.Exists(path))
        return recent;
      var stored = JsonSerializer.Deserialize<RecentFile>(File.ReadAllText(path));
      if (stored?.Items is null)
        return recent;
      foreach (var entry in stored.Items)
      {
        if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
          continue;
        recent.Items.Add(new RecentEntry
        {
          Path = entry.Path.Replace('\\', '/'),
          Archive = entry.Archive ?? "",
        });
        if (recent.Items.Count >= Limit)
          break;
      }
    }
    catch
    {
      // A damaged file should not stop the browser. The next selection rewrites it.
    }

    return recent;
  }

  public void Touch(string path, string archive)
  {
    path = path.Replace('\\', '/');
    if (string.IsNullOrWhiteSpace(path))
      return;
    archive ??= "";
    for (var i = Items.Count - 1; i >= 0; i--)
    {
      if (string.Equals(Items[i].Path, path, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Items[i].Archive, archive, StringComparison.OrdinalIgnoreCase))
        Items.RemoveAt(i);
    }

    Items.Insert(0, new RecentEntry { Path = path, Archive = archive });
    while (Items.Count > Limit)
      Items.RemoveAt(Items.Count - 1);
    Save();
  }

  public void Save()
    => Save(FilePath());

  public void Save(string path)
  {
    try
    {
      var directory = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
      var stored = new RecentFile { Items = Items.ToList() };
      File.WriteAllText(path, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch
    {
      // The list stays in memory when the profile folder is not writable.
    }
  }

  private sealed class RecentFile
  {

    public List<RecentEntry> Items { get; set; } = new();

  }

}
