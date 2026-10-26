using Avalonia;

namespace LibSaber.SM2Browser;

internal static class Program
{

  [STAThread]
  public static int Main(string[] args)
  {
    if (args.Length > 0 && args[0] == "--bench")
      return Bench.PreviewBench.Run(args);
    if (args.Length > 0 && args[0] == "--physbench")
      return Bench.PhysBench.Run(args);
    if (args.Length > 0 && args[0] == "--vfxbench")
      return Bench.VfxBench.Run(args);
    if (args.Length > 0 && args[0] == "--hostvfxbench")
      return Bench.HostVfxBench.Run(args);
    if (args.Length > 0 && args[0] == "--glbench")
      return Bench.GpuBench.Run(args);
    return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
      .UsePlatformDetect()
      // Windows: ANGLE (GLES on D3D11) first, desktop WGL next, software last (the CPU preview then takes over).
      .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software } })
      // Linux: allow Mesa llvmpipe (headless boxes); Avalonia blocks it by default.
      .With(new X11PlatformOptions { GlxRendererBlacklist = new List<string>() })
      .WithInterFont()
      .LogToTrace();

}
