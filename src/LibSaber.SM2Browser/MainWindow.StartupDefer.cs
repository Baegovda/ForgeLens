using Avalonia.Interactivity;

namespace LibSaber.SM2Browser;

// R87: measured idle private bytes after start (r85, 75 s, no input): nothing selected 758 MB, last file calgar 2515 MB,
// last file hub_battle_barge level 5835 MB. The 5.9 GB idle came from the startup restore rebuilding the last level.
// Now a level restored at startup stays selected but is not built until the user clicks "Open". Models still reopen.
public partial class MainWindow
{
  private int _deferIndex = -1;
  private bool _startupRestoreSeen;

  private void StartupDeferCheck(int catalogIndex)
  {
    if (_startupRestoreSeen || _catalog is null) return;
    _startupRestoreSeen = true;
    if (Environment.GetEnvironmentVariable("SM2_REOPEN_LEVEL") == "1") return;
    if (Environment.GetEnvironmentVariable("SM2_SELECT") is { Length: > 0 }) return;
    if (Level.LevelScene.IsLevelFile(_catalog.GetFile(catalogIndex).Path)) _deferIndex = catalogIndex;
  }

  private bool StartupDeferConsume(FileRow? row)
  {
    if (_deferIndex < 0) { DeferredOpenButton.IsVisible = false; return false; }
    if (row is null || row.Index != _deferIndex) { _deferIndex = -1; DeferredOpenButton.IsVisible = false; return false; }
    var name = _catalog?.GetFile(row.Index).Name ?? "level";
    DeferredOpenButton.Content = new Avalonia.Controls.TextBlock { Text = $"Open last level: {name}" }; // TextBlock: no "_" access keys
    DeferredOpenButton.Tag = row.Index;
    DeferredOpenButton.IsVisible = true;
    _deferIndex = -1;
    StatusText.Text = "Last level selected but not rebuilt at startup (saves several GB). Click Open in the viewport.";
    return true;
  }

  private async void DeferredOpenClicked(object? sender, RoutedEventArgs e)
  {
    DeferredOpenButton.IsVisible = false;
    if (DeferredOpenButton.Tag is int index) await PreviewFileAsync(index);
  }
}

