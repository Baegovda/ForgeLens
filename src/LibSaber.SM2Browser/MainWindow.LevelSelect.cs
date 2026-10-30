// R54b: SM2_SELECT=<catalog path> drives the file list exactly like a click (folder scope -> row selected ->
// FileSelectionChanged -> PreviewFileAsync). SM2_WINSHOT=<png> saves the real window (RenderTargetBitmap) once the
// level is shown, plus <png>.txt with the level stats and the open time; SM2_WINSHOT_EXIT=1 closes the window after.
// With SM2_SELECT the window opens without activation (no focus stealing on the PC).
using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private async Task SelectFromEnvironmentAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_SELECT") is not { Length: > 0 } path || _catalog is null)
      return;
    var sw = Stopwatch.StartNew();
    var showAll = _settings.ShowAllFiles;
    if (ShowAllBox.IsChecked != true) { ShowAllBox.IsChecked = true; ShowAllChanged(ShowAllBox, new Avalonia.Interactivity.RoutedEventArgs()); }
    OpenRecent(new RecentEntry { Path = path.Replace('\\', '/') });
    _settings.ShowAllFiles = showAll; try { _settings.Save(); } catch { /* best effort */ }
    if (Environment.GetEnvironmentVariable("SM2_WINSHOT") is not { Length: > 0 } shot)
      return;
    // wait for the level build and the first rendered frame
    for (var i = 0; i < 6000 && _level is null; i++)
      await Task.Delay(100);
    var open = sw.Elapsed.TotalSeconds;
    await Task.Delay(6000); // first frame (CPU or GPU path)
    var selected = (FileList.SelectedItem as FileRow) is { } row && _catalog is { } c ? c.GetFile(row.Index).Path : "(none)";
    try
    {
      var size = new PixelSize(Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
      using var bmp = new RenderTargetBitmap(size, new Vector(96, 96));
      bmp.Render(this);
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(shot))!);
      bmp.Save(shot);
      File.WriteAllText(shot + ".txt", $"selected {selected}\nlevel {(_level is null ? "(none)" : _level.Stats)}\nselect_to_level_shown {open:0.0}s gpu {UseGpu()} from_disk_cache {_level?.FromDiskCache}\ntiming {_level?.Timing}\n");
    }
    catch (Exception e) { File.WriteAllText(shot + ".txt", "winshot failed: " + e); }
    if (Environment.GetEnvironmentVariable("SM2_WINSHOT_EXIT") == "1")
      Dispatcher.UIThread.Post(Close);
  }
}
