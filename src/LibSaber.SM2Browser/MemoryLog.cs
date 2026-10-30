using System.Diagnostics;

namespace LibSaber.SM2Browser;

/// <summary>
///   R74: memory readout in the log (not the UI). Every <see cref="Period"/> one line goes to
///   <c>%LOCALAPPDATA%\ForgeLens\memory.log</c>: working set, private bytes, managed heap, GC committed, gen2 count,
///   level cache entries. <see cref="Line"/> is also used by the SM2_MEMSOAK hook.
/// </summary>
internal static class MemoryLog
{
  public static readonly TimeSpan Period = TimeSpan.FromSeconds(30);
  static Timer? _timer;
  static readonly object Gate = new();

  public static string File => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForgeLens", "memory.log");

  public static void Start()
  {
    if (_timer is not null) return;
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(File)!);
      var fi = new FileInfo(File);
      if (fi.Exists && fi.Length > 4 << 20) fi.Delete(); // keep the log small
    }
    catch { /* best effort */ }
    Write("start pid " + Environment.ProcessId + " " + "v" + AppVersion.Version + " " + Line("start"));
    _timer = new Timer(_ => { Write(Line("tick")); Trim("tick"); }, null, Period, Period);
  }

  static int _trimQueued;

  /// <summary>Queues one <see cref="Trim"/> a few seconds from now (after a level / model switch dropped its data).</summary>
  public static void TrimSoon()
  {
    if (Interlocked.Exchange(ref _trimQueued, 1) == 1) return;
    _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => { Interlocked.Exchange(ref _trimQueued, 0); Trim("switch"); });
  }

  /// <summary>
  ///   R74: the GC kept freed level/mesh arrays committed (measured: 16 GB committed vs 6 GB live after 4 level switches);
  ///   most of it is large-object-heap space (mesh streams, texture pixels are all &gt; 85 KB arrays) that a normal GC
  ///   never compacts. When committed exceeds live by &gt; 1 GB and 50 %, one blocking full GC with LOH compaction gives it
  ///   back to Windows. Off with SM2_GC_TRIM=0.
  /// </summary>
  public static void Trim(string why)
  {
    if (Environment.GetEnvironmentVariable("SM2_GC_TRIM") == "0") return;
    var info = GC.GetGCMemoryInfo();
    long committed = info.TotalCommittedBytes, live = GC.GetTotalMemory(false);
    if (committed - live < 1L << 30 || committed - live < live / 2) return;
    var sw = Stopwatch.StartNew();
    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    Write(Line($"gc trim ({why}) {sw.ElapsedMilliseconds} ms, committed before {committed >> 20} MB live before {live >> 20} MB ->"));
  }

  public static string Line(string tag)
  {
    using var p = Process.GetCurrentProcess();
    var gc = GC.GetGCMemoryInfo();
    return $"{DateTime.Now:HH:mm:ss} {tag} ws_mb {p.WorkingSet64 >> 20} private_mb {p.PrivateMemorySize64 >> 20} peak_ws_mb {p.PeakWorkingSet64 >> 20}"
      + $" managed_mb {GC.GetTotalMemory(false) >> 20} gc_committed_mb {gc.TotalCommittedBytes >> 20} gc_heap_mb {gc.HeapSizeBytes >> 20}"
      + $" gen2 {GC.CollectionCount(2)} levels_cached {Level.LevelScene.CachedCount} level_cache_mb {Level.LevelScene.CachedBytes >> 20}";
  }

  public static void Write(string line)
  {
    try { lock (Gate) System.IO.File.AppendAllText(File, line + Environment.NewLine); } catch { /* best effort */ }
  }
}
