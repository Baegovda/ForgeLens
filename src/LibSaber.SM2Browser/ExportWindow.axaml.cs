using Avalonia.Controls;
using Avalonia.Interactivity;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public partial class ExportWindow : Window
{

  private TaskCompletionSource<ExportSettings?>? _result;

  public ExportWindow()
  {
    InitializeComponent();
  }

  public Task<ExportSettings?> Choose(Window owner)
  {
    _result = new TaskCompletionSource<ExportSettings?>();
    ShowDialog(owner).ContinueWith(_ => _result.TrySetResult(null), TaskScheduler.FromCurrentSynchronizationContext());
    return _result.Task;
  }

  private void ExportClicked(object? sender, RoutedEventArgs e)
  {
    var settings = new ExportSettings
    {
      WriteDds = DdsBox.IsChecked == true,
      WritePng = PngBox.IsChecked == true,
      WriteTga = TgaBox.IsChecked == true,
      WriteGltf = GltfBox.IsChecked == true,
      WriteFbx = FbxBox.IsChecked == true,
      WriteMaterials = MaterialBox.IsChecked == true,
      CopyOriginalBytes = RawBox.IsChecked == true,
      Space = SpaceBox.SelectedIndex == 1 ? ExportSpace.Blender : ExportSpace.Unreal,
    };
    _result?.TrySetResult(settings);
    Close();
  }

  private void CancelClicked(object? sender, RoutedEventArgs e)
  {
    _result?.TrySetResult(null);
    Close();
  }

}
