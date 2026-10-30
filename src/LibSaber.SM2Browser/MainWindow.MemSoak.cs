// R74: SM2_MEMSOAK test hook (memory soak). SM2_MEMSOAK="<catalog path>;<catalog path>;..." opens each file exactly
// like a click on a recent entry (OpenRecent -> file list -> PreviewFileAsync), keeps it on screen for
// SM2_MEMSOAK_DWELL seconds (default 20) while sampling working set / private bytes every second, then writes one row
// per step (open time, peak WS, peak private, after-step values, managed heap after a forced full GC) to
// SM2_MEMSOAK_OUT (tsv). SM2_MEMSOAK_ROUNDS repeats the list (default 1). The window closes at the end.
// New file on purpose (R74 must not edit the other MainWindow*.cs files); started from App.
using System.Diagnostics;
using System.Text;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  internal async Task RunMemSoakAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_MEMSOAK") is not { Length: > 0 } list)
      return;
    var outFile = Environment.GetEnvironmentVariable("SM2_MEMSOAK_OUT") is { Length: > 0 } o ? o : Path.Combine(Path.GetTempPath(), "forgelens_memsoak.tsv");
    var dwell = int.TryParse(Environment.GetEnvironmentVariable("SM2_MEMSOAK_DWELL"), out var d) ? d : 20;
    var rounds = int.TryParse(Environment.GetEnvironmentVariable("SM2_MEMSOAK_ROUNDS"), out var r) ? r : 1;
    for (var i = 0; i < 3000 && _catalog is null; i++)
      await Task.Delay(100);
    var sb = new StringBuilder("round\tstep\tpath\topen_s\tpeak_ws_mb\tpeak_private_mb\tend_ws_mb\tend_private_mb\tafter_gc_private_mb\tmanaged_after_gc_mb\tlevels_cached\n");
    void Flush() { try { File.WriteAllText(outFile, sb.ToString()); } catch { } }
    if (ShowAllBox.IsChecked != true) { ShowAllBox.IsChecked = true; ShowAllChanged(ShowAllBox, new Avalonia.Interactivity.RoutedEventArgs()); } // levels are hidden kinds
    using var self = Process.GetCurrentProcess();
    MemoryLog.Write(MemoryLog.Line("memsoak begin"));
    var paths = list.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    for (var round = 1; round <= rounds; round++)
      for (var step = 0; step < paths.Length; step++)
      {
        var path = paths[step].Replace('\\', '/');
        var sw = Stopwatch.StartNew();
        long peakWs = 0, peakPriv = 0;
        void Sample() { self.Refresh(); peakWs = Math.Max(peakWs, self.WorkingSet64); peakPriv = Math.Max(peakPriv, self.PrivateMemorySize64); }
        var want = _catalog!.TryGetByPath(path, out var wantIndex) ? wantIndex : -2;
        OpenRecent(new RecentEntry { Path = path });
        await Task.Delay(500);
        for (var i = 0; i < 6000 && !(_previewFileIndex == want && _mesh is not null); i++) { if (i % 10 == 0) Sample(); await Task.Delay(100); }
        if (want < 0 || _previewFileIndex != want) path += " (NOT OPENED)";
        var open = sw.Elapsed.TotalSeconds;
        for (var s = 0; s < dwell; s++) { Sample(); await Task.Delay(1000); }
        Sample();
        long endWs = self.WorkingSet64, endPriv = self.PrivateMemorySize64;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        self.Refresh();
        sb.Append(round).Append('\t').Append(step).Append('\t').Append(path).Append('\t').Append(open.ToString("0.0"))
          .Append('\t').Append(peakWs >> 20).Append('\t').Append(peakPriv >> 20).Append('\t').Append(endWs >> 20).Append('\t').Append(endPriv >> 20)
          .Append('\t').Append(self.PrivateMemorySize64 >> 20).Append('\t').Append(GC.GetTotalMemory(false) >> 20).Append('\t').Append(Level.LevelScene.CachedCount).Append('\n');
        Flush();
        MemoryLog.Write(MemoryLog.Line($"memsoak step {round}/{step} {path}"));
      }
    sb.Append("# done\n");
    Flush();
    Close();
  }
}
