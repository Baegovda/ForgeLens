// R94: model-open stage profile. SM2_OPENPROF=<tsv> appends one row per model open:
// mesh (archive read + decode), tex (ResolvePreview: texture read + decode), ui (control setup on the UI thread),
// first frame (time to the first GPU frame of this model), full (first frame with no pending texture upload),
// plus the summed BuildScene / GPU upload / render cost of the frames up to "full". Times in ms since the click.
using System.Diagnostics;

namespace LibSaber.SM2Browser;

internal static class OpenProf
{
  private static readonly string? Out = Environment.GetEnvironmentVariable("SM2_OPENPROF") is { Length: > 0 } p ? p : null;
  public static bool On => Out is not null;
  private static readonly Stopwatch Clock = new();
  private static string _path = "", _stages = "";
  private static double _mesh, _tex, _ui, _first, _build, _upload, _render;
  private static int _frames, _verts, _texN;
  private static bool _active;
  private static readonly object Gate = new();
  // R95: peak working set during the open (50 ms sampler while active)
  private static long _peak, _wsMesh; private static double _gap, _lastTick; private static Avalonia.Threading.DispatcherTimer? _uiTimer;
  // R95b: longest gap between 15 ms UI-thread timer ticks while an open is active (= worst UI freeze)
  private static void StartUiTimer() { if (_uiTimer is not null) return; _uiTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(15), Avalonia.Threading.DispatcherPriority.Input, (_, _) => { var t = Clock.Elapsed.TotalMilliseconds; if (_active && _lastTick > 0 && t - _lastTick > _gap) _gap = t - _lastTick; _lastTick = t; }); _uiTimer.Start(); } private static Thread? _sampler;
  private static void StartSampler() { if (_sampler is not null) return; _sampler = new Thread(() => { while (true) { if (_active) { var w = PerfBudget.WorkingSetMB(); if (w > _peak) _peak = w; } Thread.Sleep(50); } }) { IsBackground = true, Name = "openprof-peak" }; _sampler.Start(); }

  public static void Begin(string path) { if (!On) return; lock (Gate) { _peak = PerfBudget.WorkingSetMB(); StartSampler(); _gap = 0; _lastTick = 0; try { StartUiTimer(); } catch { } _path = path; _mesh = _tex = _ui = _first = _build = _upload = _render = 0; _frames = 0; _active = true; Clock.Restart(); } }
  public static void Mesh(int verts, string? stages = null) { if (On) { _mesh = Clock.Elapsed.TotalMilliseconds; _wsMesh = PerfBudget.WorkingSetMB(); _verts = verts; _stages = stages ?? ""; } }
  public static void Tex(int n) { if (On) { _tex = Clock.Elapsed.TotalMilliseconds; _texN = n; } }
  public static void Ui() { if (On) _ui = Clock.Elapsed.TotalMilliseconds; }

  /// <summary>Called after every GPU model frame.</summary>
  public static void Frame(double buildMs, double uploadMs, double renderMs, bool pending)
  {
    if (!On || !_active || _ui == 0) return;
    lock (Gate)
    {
      _frames++; _build += buildMs; _upload += uploadMs; _render += renderMs;
      var now = Clock.Elapsed.TotalMilliseconds;
      if (_first == 0) _first = now;
      if (pending) return;
      _active = false;
      var header = !File.Exists(Out) ? "time\tpath\tverts\ttex_n\tmesh_ms\ttex_ms\tui_ms\tfirst_frame_ms\tfull_ms\tframes\tbuild_sum_ms\tupload_sum_ms\trender_sum_ms\tmesh_stages\tpeak_ws_mb\tthreads\tbudget_mb\tthrottled\tover_budget\tmip_drops\tws_after_mesh_mb\tui_max_gap_ms\n" : "";
      try
      {
        File.AppendAllText(Out!, header + $"{DateTime.Now:HH:mm:ss}\t{_path}\t{_verts}\t{_texN}\t{_mesh:0}\t{_tex - _mesh:0}\t{_ui - _tex:0}\t{_first:0}\t{now:0}\t{_frames}\t{_build:0}\t{_upload:0}\t{_render - _upload:0}\t{_stages}\t{_peak}\t{PerfBudget.Threads}\t{PerfBudget.BudgetMB}\t{PerfBudget.Throttled}\t{PerfBudget.OverBudgetHits}\t{PerfBudget.MipDrops}\t{_wsMesh}\t{_gap:0}\n");
      }
      catch { /* best effort */ }
    }
  }
}

