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
  }

  /// <summary>Called when a mesh or level is shown: pivot back to the renderer default, fly on for levels.</summary>
  private void FlyReset(bool level)
  {
    _pivot = null;
    _flyKeys.Clear();
    _flyLooking = false;
    FlyToggle.IsVisible = true;
    FlyToggle.IsChecked = true; // R66: fly default for meshes too (owner t130u); LMB still orbits
    _flySpeed = Math.Clamp((level && _level is { } l ? l.Spread : Math.Max(0.5f, CurrentDistance())) * 0.25f, 0.2f, 200f);
  }

  private void FlyToggleClicked(object? sender, RoutedEventArgs e) { _flyKeys.Clear(); UpdateHud(); }

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
    _flyTimer?.Start();
    e.Handled = true;
    return true;
  }

  private bool FlyReleased(PointerReleasedEventArgs e)
  {
    if (!_flyLooking || e.InitialPressMouseButton != MouseButton.Right)
      return false;
    _flyLooking = false;
    _flyKeys.Clear();
    _flyTimer?.Stop();
    e.Pointer.Capture(null);
    DrawMesh(false);
    return true;
  }

  private bool FlyMoved(PointerEventArgs e)
  {
    if (!_flyLooking)
      return false;
    var p = e.GetPosition(ViewBorder);
    FlyLook(p.X - _lastPoint.X, p.Y - _lastPoint.Y);
    _lastPoint = p;
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
    File.AppendAllText(shot + ".txt", log.ToString());
  }
}
