// R64: Unreal-viewport-style fly camera for single meshes and levels (owner t123u, 10/30).
// Fly mode: hold RMB = mouse look (eye fixed), WASD = forward/left/back/right relative to the view, Q/E = down/up,
// Shift = x4, wheel while RMB held = move speed x1.25 per notch, wheel without RMB = dolly along the view, F = frame
// (level: default perspective; mesh: fit). LMB still orbits the current pivot. Orbit mode (toggle off) = old behaviour.
// The renderer is an orbit camera (eye = pivot + dir(yaw,pitch)*distance); fly moves the pivot (_pivot) and, while
// looking, re-places the pivot so the eye stays put. Default: fly on for levels and meshes (R66), LMB orbits in both.
// Test hook: SM2_FLYTEST=1 with SM2_WINSHOT runs FlyLook/FlyMove (the same code the mouse/keys call) after the first
// shot and saves <png>_fly1/_fly2/_fly3.png + eye positions into <png>.txt.
using System.Numerics;
using LibSaber.SM2.Assets.Meshes;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private Vector3? _pivot;
  private bool _flyLooking;
  private float _flySpeed = 5f; // m/s
  private readonly HashSet<Key> _flyKeys = new();
  private DispatcherTimer? _flyTimer;
  private DateTime _flyLast;

  private bool FlyOn => FlyToggle.IsChecked == true && _mesh is not null && _fx is null;

  private void InitFly()
  {
    AddHandler(KeyDownEvent, FlyKeyDown, RoutingStrategies.Tunnel);
    AddHandler(KeyUpEvent, FlyKeyUp, RoutingStrategies.Tunnel);
    _flyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
    _flyTimer.Tick += (_, _) => FlyTick();
    // R70: never leave the cursor hidden/clipped: end mouse look on any focus/capture loss and on exit.
    Deactivated += (_, _) => FlyEndLook("deactivated");
    LostFocus += (_, _) => { if (!IsActive) FlyEndLook("lostfocus"); };
    Closing += (_, _) => FlyEndLook("closing");
    ViewBorder.PointerCaptureLost += (_, _) => FlyEndLook("capturelost");
    AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { CursorLock.Release(); } catch { } };
  }

  /// <summary>Called when a mesh or level is shown: pivot back to the renderer default, fly on for levels.</summary>
  private void FlyReset(bool level)
  {
    _pivot = null;
    _flyKeys.Clear();
    FlyEndLook("reset");
    FlyToggle.IsVisible = true;
    FlyToggle.IsChecked = true; // R66: fly default for meshes too (owner t130u); LMB still orbits
    _flySpeed = Math.Clamp((level && _level is { } l ? l.Spread : Math.Max(0.5f, CurrentDistance())) * 0.25f, 0.2f, 200f);
  }

  private void FlyToggleClicked(object? sender, RoutedEventArgs e) { _flyKeys.Clear(); FlyEndLook("toggle"); UpdateHud(); }

  private float CurrentDistance()
  {
    if (_distance > 0 || _mesh is null)
      return _distance;
    var b = ViewBorder.Bounds;
    var aspect = b.Height < 32 ? 0.8f : (float)(b.Width / Math.Max(1, b.Height));
    return MeshPreview.FitDistance(_mesh, aspect, Math.Max(0, LodBox.SelectedIndex), PartBox.SelectedIndex <= 0 ? -1 : PartBox.SelectedIndex - 1);
  }

  private Vector3 CurrentPivot()
  {
    if (_pivot is { } p) return p;
    if (_level is { } l) return l.Centre;
    return _mesh is null ? Vector3.Zero : MeshPreview.FitCentre(_mesh, Math.Max(0, LodBox.SelectedIndex), PartBox.SelectedIndex <= 0 ? -1 : PartBox.SelectedIndex - 1);
  }

  private static Vector3 Dir(float yaw, float pitch)
  {
    pitch = Math.Clamp(pitch, -1.2f, 1.2f);
    var planar = MathF.Cos(pitch);
    return new Vector3(planar * MathF.Sin(yaw), MathF.Sin(pitch), planar * MathF.Cos(yaw));
  }

  /// <summary>Eye position the renderer will use.</summary>
  private Vector3 FlyEye() => CurrentPivot() + Dir(_yaw, _pitch) * CurrentDistance();

  /// <summary>Mouse look in pixels: the eye stays put, the view turns (right = turn right, up = look up).</summary>
  private void FlyLook(double dx, double dy)
  {
    var dist = CurrentDistance();
    var eye = FlyEye();
    _distance = dist;
    _yaw -= (float)dx * 0.005f;
    _pitch = Math.Clamp(_pitch + (float)dy * 0.005f, -1.2f, 1.2f);
    _pivot = eye - Dir(_yaw, _pitch) * dist;
  }

  /// <summary>Move in view space: forward (+W), right (+D), up (+E), metres.</summary>
  private void FlyMove(float forward, float right, float up)
  {
    var f = -Dir(_yaw, _pitch);
    var r = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY));
    _distance = CurrentDistance();
    _pivot = CurrentPivot() + f * forward + r * right + Vector3.UnitY * up;
  }

  private bool FlyPressed(PointerPressedEventArgs e)
  {
    if (!FlyOn || !e.GetCurrentPoint(ViewBorder).Properties.IsRightButtonPressed)
      return false;
    _flyLooking = true;
    _lastPoint = e.GetPosition(ViewBorder);
    _flyLast = DateTime.UtcNow;
    e.Pointer.Capture(ViewBorder);
    FlyBeginCursorLock();
    _flyTimer?.Start();
    e.Handled = true;
    return true;
  }

  /// <summary>R70 (owner t140u): UE-style RMB look: hide the cursor, clip it to the viewport, keep it at the press point.</summary>
  private void FlyBeginCursorLock()
  {
    ViewBorder.Cursor = new Cursor(StandardCursorType.None);
    var tl = ViewBorder.PointToScreen(new Avalonia.Point(0, 0));
    var br = ViewBorder.PointToScreen(new Avalonia.Point(ViewBorder.Bounds.Width, ViewBorder.Bounds.Height));
    CursorLock.Begin(tl.X, tl.Y, br.X, br.Y);
  }

  /// <summary>End mouse look (RMB up, focus/capture loss, toggle, reset, exit). Idempotent; restores cursor + position.</summary>
  private void FlyEndLook(string why)
  {
    var was = _flyLooking || CursorLock.Active;
    _flyLooking = false;
    _flyKeys.Clear();
    _flyTimer?.Stop();
    ViewBorder.Cursor = null;
    CursorLock.Release();
    if (was) _flyEndReason = why;
  }
  private string _flyEndReason = "";

  private bool FlyReleased(PointerReleasedEventArgs e)
  {
    if (!_flyLooking || e.InitialPressMouseButton != MouseButton.Right)
      return false;
    FlyEndLook("rmb-up");
    e.Pointer.Capture(null);
    DrawMesh(false);
    return true;
  }

  private bool FlyMoved(PointerEventArgs e)
  {
    if (!_flyLooking)
      return false;
    if (CursorLock.Active)
    {
      // Physical-pixel delta from the anchor, then warp back. The anchor never leaves the viewport (same monitor), so
      // physical / RenderScaling = the old DIP delta on 100% and 125% monitors alike; the warp's own move event reads 0.
      if (!CursorLock.TakeDelta(out var px, out var py)) return true;
      var sc = Math.Max(0.5, RenderScaling);
      FlyLook(px / sc, py / sc);
    }
    else
    {
      var p = e.GetPosition(ViewBorder);
      FlyLook(p.X - _lastPoint.X, p.Y - _lastPoint.Y);
      _lastPoint = p;
    }
    DrawMesh(true);
    return true;
  }

  private bool FlyWheel(PointerWheelEventArgs e)
  {
    if (!FlyOn)
      return false;
    if (_flyLooking)
      FlyAdjustSpeed((float)e.Delta.Y);
    else
    {
      FlyMove((float)e.Delta.Y * Math.Max(0.1f, _flySpeed * 0.5f), 0, 0);
      DrawMesh(true);
    }
    e.Handled = true;
    return true;
  }

  /// <summary>Wheel notches while RMB held: speed x1.25 per notch (0.05..2000 m/s); HUD updates live.</summary>
  private void FlyAdjustSpeed(float notches)
  {
    _flySpeed = Math.Clamp(_flySpeed * MathF.Pow(1.25f, notches), 0.05f, 2000f);
    UpdateHud();
  }

  private static bool IsFlyKey(Key k) => k is Key.W or Key.A or Key.S or Key.D or Key.Q or Key.E or Key.LeftShift or Key.RightShift;

  private void FlyKeyDown(object? sender, KeyEventArgs e)
  {
    if (!FlyOn || e.Source is Avalonia.Controls.TextBox || FocusManager?.GetFocusedElement() is Avalonia.Controls.TextBox)
      return;
    if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None)
    {
      FlyFrame();
      e.Handled = true;
      return;
    }
    if (_flyLooking && IsFlyKey(e.Key)) { _flyKeys.Add(e.Key); e.Handled = true; UpdateHud(); }
  }

  private void FlyKeyUp(object? sender, KeyEventArgs e)
  {
    if (_flyKeys.Remove(e.Key)) { e.Handled = true; UpdateHud(); }
  }

  /// <summary>F: level -> its default perspective, mesh -> fit the bounds.</summary>
  private void FlyFrame()
  {
    _pivot = null;
    if (_level is { } l) { _yaw = l.Persp.Yaw; _pitch = l.Persp.Pitch; _distance = l.Persp.Distance; }
    else _distance = 0;
    DrawMesh(false);
  }

  private void FlyTick()
  {
    var now = DateTime.UtcNow;
    var dt = (float)Math.Clamp((now - _flyLast).TotalSeconds, 0, 0.1);
    _flyLast = now;
    if (_flyLooking && !FlyOn) { FlyEndLook("flyoff"); return; }
    if (!_flyLooking || _flyKeys.Count == 0)
      return;
    float K(Key k) => _flyKeys.Contains(k) ? 1f : 0f;
    var s = _flySpeed * dt * (K(Key.LeftShift) + K(Key.RightShift) > 0 ? 4f : 1f);
    FlyMove((K(Key.W) - K(Key.S)) * s, (K(Key.D) - K(Key.A)) * s, (K(Key.E) - K(Key.Q)) * s);
    DrawMesh(true);
  }

  /// <summary>SM2_FLYTEST hook: same FlyLook/FlyMove calls as mouse/keys, three shots.</summary>
  private async Task FlyTestAsync(string shot, Func<string, Task> save)
  {
    var log = new System.Text.StringBuilder();
    FlyToggle.IsChecked = true;
    void Note(string what) { var e = FlyEye(); log.Append($"fly {what}: eye ({e.X:0.00},{e.Y:0.00},{e.Z:0.00}) yaw {_yaw:0.000} pitch {_pitch:0.000} speed {_flySpeed:0.00}\n"); }
    Note("start");
    var sp = _flySpeed;
    // W held 2 s at the default speed (60 ticks of 1/30 s)
    for (var i = 0; i < 60; i++) FlyMove(sp / 30f, 0, 0);
    DrawMesh(false); await Task.Delay(6000); Note("after W 2s"); await save(shot.Replace(".png", "_fly1.png"));
    // mouse look 300 px right, 60 px up
    FlyLook(300, -60);
    DrawMesh(false); await Task.Delay(6000); Note("after look +300,-60 px"); await save(shot.Replace(".png", "_fly2.png"));
    // D 1 s + E 0.5 s with Shift (x4)
    for (var i = 0; i < 30; i++) FlyMove(0, sp * 4 / 30f, 0);
    for (var i = 0; i < 15; i++) FlyMove(0, 0, sp * 4 / 30f);
    DrawMesh(false); await Task.Delay(6000); Note("after Shift+D 1s, Shift+E 0.5s"); await save(shot.Replace(".png", "_fly3.png"));
    // RMB+wheel: 3 notches up (x1.25^3), HUD speed must change live
    _flyLooking = true; FlyAdjustSpeed(3); Note("after RMB+wheel +3"); await Task.Delay(1500); await save(shot.Replace(".png", "_fly4.png")); _flyLooking = false;
    log.Append("hud: " + HudSnapshot() + "\n");
    FlyFrame(); Note("after F");
    // R66: Details expander (full notes) and HUD hidden
    if (NoteDetails.IsVisible) { NoteDetails.IsChecked = true; NoteDetailsClicked(null, new RoutedEventArgs()); await Task.Delay(1500); await save(shot.Replace(".png", "_details.png")); NoteDetails.IsChecked = false; NoteDetailsClicked(null, new RoutedEventArgs()); }
    HudToggle.IsChecked = false; UpdateHud(); await Task.Delay(1500); await save(shot.Replace(".png", "_nohud.png")); HudToggle.IsChecked = true; UpdateHud();
    log.Append("details/nohud shots saved\n");
    await FlyCursorTestAsync(log, shot, save);
    File.AppendAllText(shot + ".txt", log.ToString());
  }

  /// <summary>R70 hook: begin/warp/end the cursor lock through the same code as RMB, log GetCursorInfo + ShowCursor state.</summary>
  private async Task FlyCursorTestAsync(System.Text.StringBuilder log, string shot, Func<string, Task> save)
  {
    if (!OperatingSystem.IsWindows()) { log.Append("cursor: not windows\n"); return; }
    log.Append($"cursor before: {CursorLock.Describe()} scaling {RenderScaling:0.###}\n");
    _flyLooking = true; FlyBeginCursorLock();
    await Task.Delay(300);
    log.Append($"cursor looking: {CursorLock.Describe()}\n");
    var y0 = _yaw; var p0 = _pitch;
    CursorLock.Nudge(50, -20);
    if (CursorLock.TakeDelta(out var dx, out var dy)) { var sc = Math.Max(0.5, RenderScaling); FlyLook(dx / sc, dy / sc); }
    log.Append($"cursor warp: delta ({dx},{dy}) phys, yaw {y0:0.0000}->{_yaw:0.0000} pitch {p0:0.0000}->{_pitch:0.0000}, after warp {CursorLock.Describe()}\n");
    var y1 = _yaw; CursorLock.TakeDelta(out var dx2, out var dy2);
    log.Append($"cursor second read (no move): delta ({dx2},{dy2}) yaw change {_yaw - y1:0.0000}\n");
    DrawMesh(false); await Task.Delay(3000); await save(shot.Replace(".png", "_cursor_look.png"));
    FlyEndLook("test-rmb-up");
    log.Append($"cursor released ({_flyEndReason}): {CursorLock.Describe()}\n");
    _flyLooking = true; FlyBeginCursorLock(); await Task.Delay(200);
    log.Append($"cursor relock: {CursorLock.Describe()}\n");
    FlyEndLook("deactivated"); // same handler body as Window.Deactivated
    log.Append($"cursor after deactivate ({_flyEndReason}): {CursorLock.Describe()}\n");
    _flyLooking = true; FlyBeginCursorLock(); FlyToggle.IsChecked = false; FlyTick();
    log.Append($"cursor after fly toggle off ({_flyEndReason}): {CursorLock.Describe()}\n");
    FlyToggle.IsChecked = true;
    await Task.Delay(500); await save(shot.Replace(".png", "_cursor_released.png"));
  }
}

/// <summary>R70: Win32 cursor hide/clip/warp for fly look. Physical pixels throughout (Avalonia is per-monitor-v2 aware).</summary>
internal static class CursorLock
{
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct POINT { public int X, Y; }
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
  [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT pt; }
  [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out POINT p);
  [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
  [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int ShowCursor(bool show);
  [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ClipCursor(ref RECT r);
  [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "ClipCursor")] private static extern bool ClipCursorNull(IntPtr r);
  [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CURSORINFO ci);
  [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetClipCursor(out RECT r);

  private static POINT _anchor, _saved;
  private static bool _hidden;
  public static bool Active { get; private set; }
  public static int LastShowCount { get; private set; }
  public static string LastError { get; private set; } = "";

  public static void Begin(int l, int t, int r, int b)
  {
    LastError = "";
    if (!OperatingSystem.IsWindows() || Active) { LastError = "skip(active)"; return; }
    if (!GetCursorPos(out _saved)) { LastError = $"GetCursorPos err {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}"; return; }
    // anchor = press point, kept 2 px inside the viewport so the clip rect never blocks the warp
    _anchor = new POINT { X = Math.Clamp(_saved.X, l + 2, Math.Max(l + 2, r - 3)), Y = Math.Clamp(_saved.Y, t + 2, Math.Max(t + 2, b - 3)) };
    var rc = new RECT { L = l, T = t, R = r, B = b };
    ClipCursor(ref rc);
    SetCursorPos(_anchor.X, _anchor.Y);
    if (!_hidden) { LastShowCount = ShowCursor(false); _hidden = true; }
    Active = true;
  }

  /// <summary>Physical delta since the anchor; warps back when non-zero.</summary>
  public static bool TakeDelta(out int dx, out int dy)
  {
    dx = dy = 0;
    if (!Active || !GetCursorPos(out var c)) return false;
    dx = c.X - _anchor.X; dy = c.Y - _anchor.Y;
    if (dx == 0 && dy == 0) return false;
    SetCursorPos(_anchor.X, _anchor.Y);
    return true;
  }

  public static void Nudge(int dx, int dy) { if (Active) SetCursorPos(_anchor.X + dx, _anchor.Y + dy); }

  public static void Release()
  {
    if (!OperatingSystem.IsWindows()) return;
    if (Active) { ClipCursorNull(IntPtr.Zero); SetCursorPos(_saved.X, _saved.Y); Active = false; }
    if (_hidden) { LastShowCount = ShowCursor(true); _hidden = false; }
  }

  public static string Describe()
  {
    var ci = new CURSORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<CURSORINFO>() };
    GetCursorInfo(ref ci); GetClipCursor(out var cr);
    return $"GetCursorInfo flags {ci.flags} (1=SHOWING) hCursor 0x{ci.hCursor.ToInt64():X} pos ({ci.pt.X},{ci.pt.Y}) clip [{cr.L},{cr.T},{cr.R},{cr.B}] ourShowCount {LastShowCount} hidden {_hidden} active {Active} err '{LastError}'";
  }
}
