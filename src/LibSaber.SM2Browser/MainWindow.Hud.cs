// R66: Unreal level-viewport-style HUD over the 3D view (owner t130u/t131u, 10/30). Top-left: mode, camera position,
// heading/pitch, FOV, key hints. Top-right: camera speed (m/s) with a log-scale bar like UE's speed slider; it updates
// live while the wheel changes speed with RMB held, and shows the Shift x4 boost. H or the HUD toggle hides it.
// Also R66: the bottom note shows ExportedMesh.Summary; Details swaps in the full reader notes (also the tooltip).
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private const float FovDegrees = 45f; // MeshPreview / MeshPreviewScene: CreatePerspectiveFieldOfView(PI / 4)
  private string _noteSummary = "", _noteDetails = "";

  private void InitHud()
  {
    AddHandler(KeyDownEvent, (_, e) =>
    {
      if (e.Key != Key.H || e.KeyModifiers != KeyModifiers.None || _mesh is null || FocusManager?.GetFocusedElement() is TextBox)
        return;
      HudToggle.IsChecked = HudToggle.IsChecked != true;
      UpdateHud();
      e.Handled = true;
    }, RoutingStrategies.Tunnel);
    PreviewNote.PropertyChanged += (_, e) =>
    {
      // R71: a plain Text set by any other code path replaces the coloured summary Runs.
      if (e.Property == TextBlock.TextProperty && PreviewNote.Text is not null && PreviewNote.Inlines is { Count: > 0 })
        PreviewNote.Inlines = null;
      if (e.Property == TextBlock.TextProperty && PreviewNote.Text is not null && PreviewNote.Text != _noteSummary && PreviewNote.Text != _noteDetails)
      {
        NoteDetails.IsVisible = false;
        ToolTip.SetTip(PreviewNote, PreviewNote.Text);
      }
    };
  }

  /// <summary>Model note: compact summary, full notes behind Details / tooltip.</summary>
  private void SetModelNote(LibSaber.SM2.Assets.Meshes.ExportedMesh mesh)
  {
    _noteDetails = mesh.Message;
    _noteSummary = mesh.Summary.Length > 0 ? mesh.Summary : mesh.Message;
    NoteDetails.IsChecked = false;
    PreviewNote.MaxLines = 5;
    ShowSummaryText(_noteSummary, mesh.Summary.Length > 0); // R71: coloured Runs

    NoteDetails.IsVisible = _noteDetails.Length > 0 && _noteDetails != _noteSummary;
    ToolTip.SetTip(PreviewNote, _noteDetails);
  }

  private void NoteDetailsClicked(object? sender, RoutedEventArgs e)
  {
    var open = NoteDetails.IsChecked == true;
    PreviewNote.MaxLines = open ? 0 : 5;
    ShowSummaryText(open ? _noteDetails : _noteSummary, !open && _mesh is { Summary.Length: > 0 });
  }

  private void HudToggleClicked(object? sender, RoutedEventArgs e) => UpdateHud();

  private bool FlyBoost => _flyKeys.Contains(Key.LeftShift) || _flyKeys.Contains(Key.RightShift);

  internal string HudSnapshot() => $"{HudMode.Text} | {HudCam.Text?.Replace("\n", " | ")} | speed {HudSpeedText.Text}";

  private void UpdateHud()
  {
    var show = _mesh is not null && _fx is null && HudToggle.IsChecked == true;
    HudToggle.IsVisible = _mesh is not null && _fx is null;
    HudInfo.IsVisible = show;
    HudSpeed.IsVisible = show && FlyToggle.IsChecked == true;
    if (!show)
      return;
    var fly = FlyToggle.IsChecked == true;
    var eye = FlyEye();
    var f = -Dir(_yaw, _pitch);
    var heading = MathF.Atan2(f.X, f.Z) * 180f / MathF.PI;
    if (heading < 0) heading += 360f;
    var pitch = MathF.Asin(Math.Clamp(f.Y, -1f, 1f)) * 180f / MathF.PI;
    HudMode.Text = (_level is not null ? "Level" : "Mesh") + " · " + (fly ? (_flyLooking ? "Fly (looking)" : "Fly") : "Orbit") + " · Perspective";
    HudCam.Text = $"Pos  {eye.X,8:0.00} {eye.Y,8:0.00} {eye.Z,8:0.00}\nYaw {heading,5:0.0}°  Pitch {pitch,5:0.0}°  FOV {FovDegrees:0}°";
    HudKeys.Text = fly
      ? "RMB look · WASD move · Q/E down/up · Shift ×4 · RMB+wheel speed · F frame · H hide"
      : "LMB orbit · wheel zoom · Fly toggle for WASD · H hide";
    var speed = _flySpeed * (FlyBoost ? 4f : 1f);
    HudSpeedText.Text = (speed >= 10 ? $"{speed:0}" : speed >= 1 ? $"{speed:0.0}" : $"{speed:0.00}") + " m/s" + (FlyBoost ? " ×4" : "");
    // log bar 0.05 .. 2000 m/s (the clamp in FlyAdjustSpeed)
    var t = Math.Clamp((MathF.Log(speed) - MathF.Log(0.05f)) / (MathF.Log(2000f) - MathF.Log(0.05f)), 0f, 1f);
    HudSpeedBar.Width = 132 * t;
  }
}
