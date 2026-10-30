// R81 (owner t178u/t179u): lighter idle viewer. The loading overlay's indeterminate ProgressBar animated forever while
// hidden (its style animation keeps the compositor ticking at vsync), so it now runs only while the overlay is shown.
using Avalonia;
using Avalonia.Controls;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private void InitLightweight()
  {
    LoadingBar.IsIndeterminate = LoadingOverlay.IsVisible;
    LoadingOverlay.PropertyChanged += (_, e) =>
    {
      if (e.Property == IsVisibleProperty) LoadingBar.IsIndeterminate = LoadingOverlay.IsVisible;
    };
  }
}
