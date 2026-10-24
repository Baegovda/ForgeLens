using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public sealed class ExportChoice
{
  public required string Folder { get; init; }
  public required ExportSettings Settings { get; init; }
}

public partial class ExportWindow : Window
{

  private TaskCompletionSource<ExportChoice?>? _result;

  public ExportWindow()
  {
    InitializeComponent();
  }

  public Task<ExportChoice?> Choose(Window owner, string? initialFolder)
  {
    FolderBox.Text = initialFolder ?? "";
    FitToScreen(owner);
    _result = new TaskCompletionSource<ExportChoice?>();
    ShowDialog(owner).ContinueWith(_ => _result.TrySetResult(null), TaskScheduler.FromCurrentSynchronizationContext());
    return _result.Task;
  }

  private void FitToScreen(Window owner)
  {
    var screen = owner.Screens?.ScreenFromVisual(owner) ?? owner.Screens?.Primary;
    if (screen is null || screen.Scaling < 0.5 || screen.WorkingArea.Height < 1)
    {
      OptionsScroll.MaxHeight = 480;
      return;
    }

    // WorkingArea is physical pixels. Window sizes are device-independent.
    var height = screen.WorkingArea.Height / screen.Scaling;
    var width = screen.WorkingArea.Width / screen.Scaling;
    var availableHeight = Math.Max(240, height - 48);
    var availableWidth = Math.Max(360, width - 48);
    if (availableHeight < MinHeight)
      MinHeight = availableHeight;
    MaxHeight = availableHeight;
    MaxWidth = Math.Min(760, availableWidth);
    if (Width > MaxWidth)
      Width = MaxWidth;
    OptionsScroll.MaxHeight = Math.Max(120, availableHeight - 240);
  }

  private async void BrowseClicked(object? sender, RoutedEventArgs e)
  {
    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
      Title = "Export folder",
      AllowMultiple = false,
    });
    if (folders.Count == 0)
      return;
    var path = folders[0].TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
      return;
    FolderBox.Text = path;
    FolderError.Text = "";
  }

  private void ExportClicked(object? sender, RoutedEventArgs e)
  {
    var folder = FolderBox.Text?.Trim() ?? "";
    if (folder.Length == 0 || !Directory.Exists(folder))
    {
      FolderError.Text = "Choose a folder that exists.";
      return;
    }

    var settings = new ExportSettings
    {
      WriteDds = DdsBox.IsChecked == true,
      WritePng = PngBox.IsChecked == true,
      WriteTga = TgaBox.IsChecked == true,
      WriteGltf = GltfBox.IsChecked == true,
      WriteFbx = FbxBox.IsChecked == true,
      WriteMaterials = MaterialBox.IsChecked == true,
      WriteModelTextures = ModelTexturesBox.IsChecked == true,
      CopyOriginalBytes = RawBox.IsChecked == true,
      Space = SpaceBox.SelectedIndex == 1 ? ExportSpace.Blender : ExportSpace.Unreal,
    };
    _result?.TrySetResult(new ExportChoice { Folder = folder, Settings = settings });
    Close();
  }

  private void CancelClicked(object? sender, RoutedEventArgs e)
  {
    _result?.TrySetResult(null);
    Close();
  }

}
