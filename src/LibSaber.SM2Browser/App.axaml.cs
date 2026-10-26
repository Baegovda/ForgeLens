using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace LibSaber.SM2Browser;

public class App : Application
{
  /// <summary>Bench mode replaces the main window.</summary>
  internal static Func<Avalonia.Controls.Window>? WindowFactory { get; set; }

  public override void Initialize()
    => AvaloniaXamlLoader.Load(this);

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
      desktop.MainWindow = WindowFactory?.Invoke() ?? new MainWindow();

    base.OnFrameworkInitializationCompleted();
  }

}
