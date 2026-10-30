// R75 (t157u): Orbit physics. (1) Turning the model in Orbit mode moves the cloth / jiggle simulation's world frame
// (inverse orbit yaw), so seals / tabards / jiggle bones swing with inertial lag and settle; the sim keeps stepping on
// the wall clock while the clip is paused. The frame follows the orbit yaw at most MaxFrameRate rad/s (no explosion on
// fast drags). (2) Rotation inertia: releasing a left-drag keeps orbiting with exp(-OrbitDamping * t) decay; any press
// stops it. Orbit mode only (Fly toggle off); the fly camera is unchanged. Toggle "Orbit physics" (default on).
using System.Diagnostics;
using System.Numerics;
using Avalonia.Threading;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  /// <summary>Inertia decay per second (v *= exp(-k dt)); env SM2_ORBIT_DAMPING overrides. 4 = ~1/e in 0.25 s.</summary>
  internal static readonly float OrbitDamping = float.TryParse(Environment.GetEnvironmentVariable("SM2_ORBIT_DAMPING"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var k) && k > 0 ? k : 4f;
  private const float MaxFrameRate = 8f;       // rad/s the sim frame may turn
  private const float SettleSeconds = 3f;      // keep simulating after the last motion
  private readonly Stopwatch _orbitClock = Stopwatch.StartNew();
  private DispatcherTimer? _orbitTimer;
  private float _orbitVelYaw, _orbitVelPitch;  // rad/s
  private float _physYaw;                       // sim frame yaw (follows _yaw, rate-limited)
  private double _orbitLastMove, _orbitLastTick, _orbitSettleUntil;
  private bool _orbitPhysInit;

  private bool OrbitPhysicsOn => OrbitPhysicsToggle.IsChecked == true && FlyToggle.IsChecked != true && _level is null;

  /// <summary>Physics frame + clock for this frame's request (null when orbit physics is off).</summary>
  private (Matrix4x4? Frame, float? Clock) OrbitPhysicsFrame()
  {
    if (!OrbitPhysicsOn || PhysicsToggle.IsChecked != true || !PhysicsToggle.IsVisible)
      return (null, null);
    if (!_orbitPhysInit) { _physYaw = _yaw; _orbitPhysInit = true; }
    return (Matrix4x4.CreateRotationY(-_physYaw), (float)_orbitClock.Elapsed.TotalSeconds);
  }

  /// <summary>Orbit drag step (called with the yaw/pitch delta just applied).</summary>
  private void OrbitDragged(float dYaw, float dPitch)
  {
    var now = _orbitClock.Elapsed.TotalSeconds;
    var dt = Math.Clamp(now - _orbitLastMove, 1e-3, 0.1);
    _orbitLastMove = now;
    var a = 0.5f; // smoothing
    _orbitVelYaw = _orbitVelYaw * (1 - a) + (float)(dYaw / dt) * a;
    _orbitVelPitch = _orbitVelPitch * (1 - a) + (float)(dPitch / dt) * a;
    KickOrbitTimer();
  }

  private void OrbitReleased()
  {
    // A pause before release (>80 ms without motion) means the user stopped: no fling.
    if (_orbitClock.Elapsed.TotalSeconds - _orbitLastMove > 0.08 || !OrbitPhysicsOn)
      _orbitVelYaw = _orbitVelPitch = 0;
    KickOrbitTimer();
  }

  private void OrbitStop()
  {
    _orbitVelYaw = _orbitVelPitch = 0;
  }


  private void KickOrbitTimer()
  {
    _orbitSettleUntil = _orbitClock.Elapsed.TotalSeconds + SettleSeconds;
    if (_orbitTimer is null)
    {
      _orbitTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
      _orbitTimer.Tick += (_, _) => OrbitTick();
    }
    _orbitLastTick = _orbitClock.Elapsed.TotalSeconds;
    if (!_orbitTimer.IsEnabled) _orbitTimer.Start();
  }

  private void OrbitTick()
  {
    var now = _orbitClock.Elapsed.TotalSeconds;
    var dt = (float)Math.Clamp(now - _orbitLastTick, 0, 0.1);
    _orbitLastTick = now;
    if (_mesh is null) { _orbitTimer?.Stop(); return; }
    var moved = false;
    if (!_dragging && OrbitPhysicsOn && (MathF.Abs(_orbitVelYaw) > 0.02f || MathF.Abs(_orbitVelPitch) > 0.02f))
    {
      _yaw += _orbitVelYaw * dt;
      _pitch = Math.Clamp(_pitch + _orbitVelPitch * dt, -1.2f, 1.2f);
      var decay = MathF.Exp(-OrbitDamping * dt);
      _orbitVelYaw *= decay;
      _orbitVelPitch *= decay;
      moved = true;
      _orbitSettleUntil = now + SettleSeconds;
    }
    else if (!_dragging)
      _orbitVelYaw = _orbitVelPitch = 0;
    // sim frame follows the orbit yaw, rate-limited
    var diff = _yaw - _physYaw;
    var max = MaxFrameRate * dt;
    _physYaw += Math.Clamp(diff, -max, max);
    var physicsLive = OrbitPhysicsOn && PhysicsToggle.IsChecked == true && PhysicsToggle.IsVisible;
    if (moved || (physicsLive && now < _orbitSettleUntil))
      DrawMesh(true);
    else
    {
      _orbitTimer?.Stop();
      DrawMesh(false);
    }
  }
}

public partial class MainWindow
{
  /// <summary>
  ///   SM2_ORBITTEST=1 (after SM2_WINSHOT): fling the orbit (6 rad/s, damped) with Orbit physics ON, then replay the same
  ///   yaw path with it OFF; logs the mean cloth-vertex deviation from the rest pose (model space) every 100 ms and saves
  ///   GPU frames at 0.3 s for both.
  /// </summary>
  private async Task OrbitTestAsync(string shot)
  {
    var stem = Path.ChangeExtension(shot, null);
    var log = new System.Text.StringBuilder();
    for (var i = 0; i < 300 && !PhysicsToggle.IsVisible; i++) await Task.Delay(100);
    log.Append($"orbittest physics visible {PhysicsToggle.IsVisible} damping {OrbitDamping}\n");
    FlyToggle.IsChecked = false;
    PhysicsToggle.IsChecked = true;
    Vector3[]? Snap() => GpuView.LastScene?.VertexOverrides.SelectMany(o => o.Positions).ToArray();
    static double Dev(Vector3[]? a, Vector3[]? b) { if (a is null || b is null || a.Length == 0 || a.Length != b.Length) return double.NaN; double s = 0; for (var i = 0; i < a.Length; i++) s += (a[i] - b[i]).Length(); return s / a.Length; }
    static double Max(Vector3[]? a, Vector3[]? b) { if (a is null || b is null || a.Length != b.Length) return double.NaN; double m = 0; for (var i = 0; i < a.Length; i++) m = Math.Max(m, (a[i] - b[i]).Length()); return m; }
    foreach (var on in new[] { true, false })
    {
      OrbitPhysicsToggle.IsChecked = on;
      _orbitPhysInit = false;
      var yaw0 = _yaw;
      // settle at rest
      KickOrbitTimer();
      await Task.Delay(2500);
      var rest = Snap();
      log.Append($"[{(on ? "ON" : "OFF")}] cloth verts {rest?.Length ?? 0}\n");
      var t0 = _orbitClock.Elapsed.TotalSeconds;
      if (on) { _orbitVelYaw = 6f; KickOrbitTimer(); }
      var shotDone = false;
      double peak = 0; var peakT = 0.0;
      for (var k = 1; k <= 40; k++)
      {
        if (!on)
        {
          // same yaw path as the damped fling: yaw(t) = 6/k (1 - e^-kt)
          var tt = _orbitClock.Elapsed.TotalSeconds - t0;
          _yaw = yaw0 + 6f / OrbitDamping * (1f - MathF.Exp(-OrbitDamping * (float)tt));
          DrawMesh(true);
        }
        await Task.Delay(100);
        var t = _orbitClock.Elapsed.TotalSeconds - t0;
        var now = Snap();
        var d = Dev(now, rest); var m = Max(now, rest);
        if (d > peak) { peak = d; peakT = t; }
        if (k % 2 == 0 || k <= 6) log.Append($"  t {t:0.00}s yaw {(_yaw - yaw0):0.000} rad  mean dev {d * 100:0.00} cm  max {m * 100:0.00} cm\n");
        if (!shotDone && t >= 0.3) { shotDone = true; await GpuView.CaptureNextAsync($"{stem}_orbit_{(on ? "on" : "off")}_0.3s.png"); }
      }
      log.Append($"  peak mean dev {peak * 100:0.00} cm at {peakT:0.00}s; final yaw {(_yaw - yaw0):0.000} rad\n");
    }
    OrbitPhysicsToggle.IsChecked = true;
    File.AppendAllText(shot + ".txt", log.ToString() + "orbittest done\n");
  }
}
