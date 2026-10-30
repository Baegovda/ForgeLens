// R95: worker threads + working-memory budget (settings window "Performance", BrowserSettings.WorkerThreads / MaxMemoryMB).
// Threads: Auto = every logical processor for CPU-bound pools (texture decode + BC prep, mesh build); background workers
// run at BelowNormal priority so the UI / render thread stays responsive. IO-bound stages may use 2x (capped at 64).
// Manual 1..2x logical processors. SM2_THREADS env overrides (0 = Auto). .NET refuses a pool max below the core count,
// so values under the core count are enforced through MaxDegreeOfParallelism of our own parallel loops.
// Memory: Auto = 50 % of installed RAM, further limited so at least 4 GB of system RAM stays free. Manual 512 MB..90 % of
// RAM. SM2_MAXMEM (MB) overrides. Enforced at the parallel texture decode (each new decode waits while the process working
// set is over budget and other decodes are still running, after a GC + cache eviction) and by evicting the effect
// texture / mesh caches. One decode is always allowed so an open never deadlocks: a single model bigger than the budget
// still goes over it (reported as OverBudgetHits).
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LibSaber.SM2Browser;

internal static class PerfBudget
{
  public static int Logical => Environment.ProcessorCount;
  public static int MaxManualThreads => Logical * 2;
  public static long TotalRamMB { get; } = QueryTotalMB();
  public const int MinMemoryMB = 512;
  public static int MaxManualMemoryMB => (int)Math.Max(MinMemoryMB, TotalRamMB * 9 / 10);

  public static int SettingThreads { get; private set; }   // 0 = Auto
  public static int SettingMemoryMB { get; private set; }  // 0 = Auto
  public static string ThreadSource { get; private set; } = "auto";
  public static string MemorySource { get; private set; } = "auto";
  public static int Threads { get; private set; } = Environment.ProcessorCount;
  public static long BudgetMB { get; private set; }
  public static long OverBudgetHits, Throttled, Evictions;
  public static event Action? Pressure;

  public static int IoThreads => Math.Min(64, Threads * 2);
  public static ParallelOptions Cpu => new() { MaxDegreeOfParallelism = Threads };

  public static void Init(BrowserSettings s) => Apply(s.WorkerThreads ?? 0, s.MaxMemoryMB ?? 0);

  public static void Apply(int threads, int memoryMB)
  {
    SettingThreads = Math.Clamp(threads, 0, MaxManualThreads);
    SettingMemoryMB = memoryMB <= 0 ? 0 : Math.Clamp(memoryMB, MinMemoryMB, MaxManualMemoryMB);
    if (int.TryParse(Environment.GetEnvironmentVariable("SM2_THREADS"), out var envT) && envT > 0)
    { Threads = Math.Clamp(envT, 1, MaxManualThreads); ThreadSource = "SM2_THREADS"; }
    else if (SettingThreads > 0) { Threads = SettingThreads; ThreadSource = "manual"; }
    else { Threads = Logical; ThreadSource = "auto"; }
    if (long.TryParse(Environment.GetEnvironmentVariable("SM2_MAXMEM"), out var envM) && envM > 0)
    { BudgetMB = Math.Max(256, envM); MemorySource = "SM2_MAXMEM"; }
    else if (SettingMemoryMB > 0) { BudgetMB = SettingMemoryMB; MemorySource = "manual"; }
    else { BudgetMB = Math.Max(MinMemoryMB, TotalRamMB / 2); MemorySource = "auto"; }
    ThreadPool.GetMinThreads(out _, out var minIo);
    ThreadPool.GetMaxThreads(out _, out var maxIo);
    // pool max cannot go below the core count (.NET returns false); keep the default max when Threads < cores
    ThreadPool.SetMaxThreads(Math.Max(Math.Max(Threads, IoThreads), Logical), Math.Max(maxIo, IoThreads));
    ThreadPool.SetMinThreads(Math.Min(Threads, Logical), minIo);
  }

  /// <summary>Budget right now: the setting, but never leaving less than 4 GB of system RAM free.</summary>
  public static long EffectiveBudgetMB()
  {
    var free = QueryAvailMB();
    var ws = WorkingSetMB();
    return free > 0 ? Math.Min(BudgetMB, Math.Max(MinMemoryMB / 2, ws + free - 4096)) : BudgetMB;
  }

  public static string Describe()
    => $"{Threads} worker threads ({ThreadSource}, {Logical} logical processors), memory budget {BudgetMB} MB ({MemorySource}, {TotalRamMB} MB RAM)";

  // R95b: per-texture reservation + mip pick. A decode reserves its estimated peak (BGRA + BC prep copy + transient
  // ~2.5x w*h*4) before it starts; when the headroom (budget - working set - reservations) is too small the decode
  // drops to a smaller mip (min 128 px) instead of going over. Auto: headroom is tens of GB, so mip 0 as before.
  private static long _reservedMB;
  public static long MipDrops;
  public static long HeadroomMB() => EffectiveBudgetMB() - WorkingSetMB() - Interlocked.Read(ref _reservedMB);
  public static int PickMip(int w, int h, int mipCount, out long reservedMB)
  {
    var mip = 0; reservedMB = 0;
    if (Roomy) return 0;
    long Need(int m) => Math.Max(1, ((long)Math.Max(1, w >> m) * Math.Max(1, h >> m) * 4 * 5 / 2) >> 20);
    var room = HeadroomMB();
    while (mip < mipCount - 1 && Math.Max(w, h) >> mip > 128 && Need(mip) > Math.Max(0, room)) mip++;
    if (mip > 0) Interlocked.Add(ref MipDrops, mip);
    reservedMB = Need(mip); Interlocked.Add(ref _reservedMB, reservedMB);
    return mip;
  }
  /// <summary>Budget far above need (checked at most every 500 ms): skip per-texture accounting.</summary>
  private static long _roomyAt; private static bool _roomy;
  public static bool Roomy { get { var now = Environment.TickCount64; if (now - _roomyAt > 500) { _roomy = EffectiveBudgetMB() - WorkingSetMB() > 16384; _roomyAt = now; } return _roomy; } }
  public static void Unreserve(long mb) => Interlocked.Add(ref _reservedMB, -mb);
  /// <summary>Parallelism for the decode loop: all Threads with room, fewer (down to 1) when the headroom is small.</summary>
  public static ParallelOptions DecodeOptions()
  {
    var room = HeadroomMB();
    var n = room >= 4096 ? Threads : (int)Math.Clamp(room / 128, 1, Threads);
    return new ParallelOptions { MaxDegreeOfParallelism = n };
  }
  /// <summary>True when the budget is small enough that a new model should drop the previous model's textures first.</summary>
  public static bool Tight => EffectiveBudgetMB() < 8192;
  private static int _inFlight;
  private static long _lastGc;

  /// <summary>Runs one memory-heavy background job (texture decode) at BelowNormal priority under the memory budget.</summary>
  public static T Heavy<T>(Func<T> job)
  {
    if (Roomy) { var th = Thread.CurrentThread; var pr = th.Priority; try { th.Priority = ThreadPriority.BelowNormal; return job(); } finally { th.Priority = pr; } }
    var spins = 0;
    while (WorkingSetMB() > EffectiveBudgetMB())
    {
      Relieve();
      if (WorkingSetMB() <= EffectiveBudgetMB()) break;
      if (Volatile.Read(ref _inFlight) == 0) { Interlocked.Increment(ref OverBudgetHits); break; }
      if (spins++ == 0) Interlocked.Increment(ref Throttled);
      Thread.Sleep(5);
    }
    Interlocked.Increment(ref _inFlight);
    var t = Thread.CurrentThread; var old = t.Priority;
    try { t.Priority = ThreadPriority.BelowNormal; return job(); }
    finally { t.Priority = old; Interlocked.Decrement(ref _inFlight); }
  }

  public static void Relieve(bool force = false)
  {
    var now = Environment.TickCount64;
    var last = Interlocked.Read(ref _lastGc);
    if (now - last < (force ? 250 : 1000) || Interlocked.CompareExchange(ref _lastGc, now, last) != last) return;
    Interlocked.Increment(ref Evictions);
    try { Pressure?.Invoke(); } catch { }
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, false); // R95b: background GC (blocking compacting GC froze the UI 1-3.5 s at 1 GB)
    if (OperatingSystem.IsWindows()) { try { SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1); } catch { } }
  }

  public static long WorkingSetMB()
  {
    if (OperatingSystem.IsWindows())
    {
      var c = new PMC { cb = (uint)Marshal.SizeOf<PMC>() };
      if (GetProcessMemoryInfo(GetCurrentProcess(), ref c, c.cb)) return (long)(c.WorkingSetSize / (1024 * 1024));
    }
    return Environment.WorkingSet / (1024 * 1024);
  }

  private static long QueryTotalMB()
  {
    if (OperatingSystem.IsWindows()) { var m = new MSX { dwLength = (uint)Marshal.SizeOf<MSX>() }; if (GlobalMemoryStatusEx(ref m)) return (long)(m.ullTotalPhys / (1024 * 1024)); }
    return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
  }

  private static long QueryAvailMB()
  {
    if (OperatingSystem.IsWindows()) { var m = new MSX { dwLength = (uint)Marshal.SizeOf<MSX>() }; if (GlobalMemoryStatusEx(ref m)) return (long)(m.ullAvailPhys / (1024 * 1024)); }
    return 0;
  }

  [StructLayout(LayoutKind.Sequential)] private struct MSX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
  [StructLayout(LayoutKind.Sequential)] private struct PMC { public uint cb, PageFaultCount; public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage; }
  [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MSX m);
  [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
  [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(IntPtr p, ref PMC c, uint cb);
  [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr p, nint min, nint max);
}
