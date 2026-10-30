using Avalonia;

namespace LibSaber.SM2Browser;

internal static class Program
{

  [STAThread]
  public static int Main(string[] args)
  {
    ApplyThreadCap();
    if (args.Length > 0 && args[0] == "--levelbench")
      return Bench.LevelBench.Run(args);
    if (args.Length > 0 && args[0] == "--bench")
      return Bench.PreviewBench.Run(args);
    if (args.Length > 0 && args[0] == "--clothpen")
      return Bench.ClothPenBench.Run(args);
    if (args.Length > 0 && args[0] == "--physbench")
      return Bench.PhysBench.Run(args);
    if (args.Length > 0 && args[0] == "--vfxbench")
      return Bench.VfxBench.Run(args);
    if (args.Length > 0 && args[0] == "--hostvfxbench")
      return Bench.HostVfxBench.Run(args);
    if (args.Length > 0 && args[0] == "--vfxue")
      return Bench.VfxUeExport.Run(args);
    if (args.Length > 0 && args[0] == "--vfxlights")
      return Bench.VfxLightDump.Run(args);
    if (args.Length > 0 && args[0] == "--vfxemitters")
      return Bench.VfxEmitterDump.Run(args);
    if (args.Length > 0 && args[0] == "--vfxue-rules")
      return Bench.VfxUeExport.Rules(args);
    if (args.Length > 0 && args[0] == "--fxparts")
      return Bench.FxPartsBench.Run(args);
    if (args.Length > 0 && args[0] == "--propbench")
      return Bench.PropAnimBench.Run(args);
    if (args.Length > 0 && args[0] == "--thumbbench")
      return Bench.ThumbBench.Run(args);
    if (args.Length > 0 && args[0] == "--meshparts")
      return Bench.MeshPartsBench.Run(args);
    if (args.Length > 0 && args[0] == "--openbench")
      return Bench.OpenBench.Run(args);
    if (args.Length > 0 && args[0] == "--glbench")
      return Bench.GpuBench.Run(args);
    return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  /// <summary>
  ///   R94 cap replaced in R95 by <see cref="PerfBudget"/>: Auto = every logical processor (settings window "Performance",
  ///   SM2_THREADS overrides). Note: the R94 SetMaxThreads(cores - 2) call returned false (.NET rejects a max below the
  ///   core count), so the R94 cap never applied.
  /// </summary>
  internal static int ThreadCap => PerfBudget.Threads;

  private static void ApplyThreadCap() => PerfBudget.Init(BrowserSettings.Load());

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
