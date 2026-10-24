using System.Text.Json;

namespace LibSaber.SM2Browser;

public sealed class BrowserSettings
{

  public string? LastFolder { get; set; }

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
