using Avalonia;

namespace LibSaber.SM2Browser;

internal static class Program
{

  [STAThread]
  public static int Main(string[] args)
  {
    if (args.Length > 0 && args[0] == "--bench")
      return Bench.PreviewBench.Run(args);
    return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace();

}
