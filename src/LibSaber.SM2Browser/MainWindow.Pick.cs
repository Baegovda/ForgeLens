// R75 (t154u): UE-viewport-style picking. Hover outlines the part under the cursor, a left click (press->release
// moved < 5 px, so an orbit drag never selects) selects it with an orange outline + readout, click on empty space
// deselects, the Meshes tree follows the selection and selecting a tree row selects the part in 3D.
// Fly camera (RMB look / WASD, R70 cursor lock) is untouched: picks only run for the left button and while not looking.
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private static readonly Vector3 HoverColour = new(0.35f, 0.75f, 1f);   // light blue (UE hover)
  private static readonly Vector3 SelectColour = new(1f, 0.55f, 0.05f);  // orange (UE selection)
  private MeshPart? _hoverPart;
  private MeshPart? _selPart;
  private Point? _pickDown;
  private long _pickLastHover;
  private bool _pickSyncing;
  internal string PickLog { get; private set; } = "";

  /// <summary>The control the 3D image fills: the GPU view, or (CPU path, e.g. levels) the viewport border.</summary>
  private Control PickHost => GpuView.IsVisible && GpuView.Bounds.Width > 1 ? GpuView : ViewBorder;

  private void PickPressed(PointerPressedEventArgs e)
  {
    _pickDown = e.GetCurrentPoint(PickHost).Properties.IsLeftButtonPressed ? e.GetPosition(PickHost) : null;
  }

  /// <summary>Left release: select when the pointer moved less than 5 px since the press.</summary>
  private void PickReleased(PointerReleasedEventArgs e)
  {
    if (_pickDown is not { } down || e.InitialPressMouseButton != MouseButton.Left)
      return;
    _pickDown = null;
    var up = e.GetPosition(PickHost);
    if (Math.Abs(up.X - down.X) + Math.Abs(up.Y - down.Y) >= 5)
      return;
    PickAt(up, select: true);
  }

  private void PickHover(PointerEventArgs e)
  {
    if (_mesh is null || _fx is not null || _flyLooking || _dragging)
      return;
    var now = Environment.TickCount64;
    if (now - _pickLastHover < 40)
      return;
    _pickLastHover = now;
    PickAt(e.GetPosition(PickHost), select: false);
  }

  private void PickLeave()
  {
    if (_hoverPart is null) return;
    _hoverPart = null;
    ApplyHighlights();
  }

  /// <summary>Pick at a point in GpuView coordinates. Returns the hit (null = empty space).</summary>
  /// <summary>Point in <see cref="PickHost"/> coordinates.</summary>
  internal PickHit? PickAt(Point point, bool select)
  {
    if (_mesh is null || PickScene() is not { } scene)
      return null;
    var w = Math.Max(1, PickHost.Bounds.Width);
    var h = Math.Max(1, PickHost.Bounds.Height);
    var ndc = new Vector2((float)(point.X / w * 2 - 1), (float)(1 - point.Y / h * 2));
    var hit = MeshPick.Pick(scene, ndc);
    var part = hit?.Part.Source;
    if (select)
    {
      _selPart = part;
      ShowPickInfo(hit);
      SyncTreeToSelection();
      PickLog += $"select at ({point.X:0},{point.Y:0}) -> {(hit is null ? "none" : $"{hit.Part.Name} t={hit.Distance:0.###} tri {hit.Triangle}")} {(hit?.Ms ?? 0):0.0} ms tested {hit?.TrianglesTested ?? 0}\n";
    }
    else
    {
      if (ReferenceEquals(part, _hoverPart)) return hit;
      _hoverPart = part;
      PickLog += $"hover at ({point.X:0},{point.Y:0}) -> {hit?.Part.Name ?? "none"} {(hit?.Ms ?? 0):0.0} ms\n";
    }
    ApplyHighlights();
    return hit;
  }

  /// <summary>The frame the user sees: the GPU's last scene, else (CPU path) the same scene rebuilt without physics.</summary>
  private PreviewScene? PickScene()
  {
    if (UseGpu() && GpuView.LastScene is { } s)
      return s;
    var b = ViewBorder.Bounds;
    var sc = RenderScaling > 0 ? RenderScaling : 1.0;
    return MeshPreview.BuildScene(_mesh!, Math.Max(32, (int)(b.Width * sc)), Math.Max(32, (int)(b.Height * sc)),
      new OrbitCamera { Yaw = _yaw, Pitch = _pitch, Distance = _distance, Target = _pivot ?? _level?.Centre }, false, _time,
      new PreviewSettings { Lod = LodBox.SelectedIndex < 0 ? 0 : LodBox.SelectedIndex, Part = PartBox.SelectedIndex <= 0 ? -1 : PartBox.SelectedIndex - 1, InPlace = InPlaceToggle.IsChecked == true });
  }

  private void ApplyHighlights()
  {
    var list = new List<(MeshPart, Vector3, float)>();
    if (_hoverPart is not null && !ReferenceEquals(_hoverPart, _selPart)) list.Add((_hoverPart, HoverColour, 2f));
    if (_selPart is not null) list.Add((_selPart, SelectColour, 3f));
    GpuView.Highlights = list;
    if (UseGpu()) GpuView.Redraw();
  }

  private void ClearPick()
  {
    _hoverPart = null;
    _selPart = null;
    GpuView.Highlights = Array.Empty<(MeshPart, Vector3, float)>();
    PickInfo.IsVisible = false;
  }

  private void ShowPickInfo(PickHit? hit)
  {
    if (hit?.Part.Source is not { } p)
    {
      PickInfo.IsVisible = false;
      return;
    }
    var tris = p.IndexCount / 3;
    PickName.Text = p.Name;
    PickDetail.Text = $"{tris:N0} tris · material {p.Variant}" + (p.Texture is { Length: > 0 } t ? $" · {t}" : "") + (p.Node >= 0 ? $" · node {p.Node}" : "")
      + (p.HideReason is { } r ? $" · {r}" : "") + $"\npick {hit.Ms:0.0} ms · {hit.TrianglesTested:N0} tris tested";
    PickInfo.IsVisible = true;
  }

  /// <summary>Meshes tree follows the 3D selection: expand the parents (group / LOD base), select, scroll to it.</summary>
  private void SyncTreeToSelection()
  {
    if (MeshList.ItemsSource is not IEnumerable<MeshToggle> roots)
      return;
    _pickSyncing = true;
    try
    {
      if (_selPart is null) { MeshList.SelectedItem = null; return; }
      List<MeshToggle>? path = null;
      bool Find(MeshToggle t, List<MeshToggle> stack)
      {
        stack.Add(t);
        if (ReferenceEquals(t.Part, _selPart)) { path = new List<MeshToggle>(stack); return true; }
        foreach (var c in t.Children) if (Find(c, stack)) return true;
        stack.RemoveAt(stack.Count - 1);
        return false;
      }
      foreach (var r in roots) if (Find(r, new List<MeshToggle>())) break;
      if (path is null) return;
      for (var i = 0; i < path.Count - 1; i++)
      {
        MeshList.ScrollIntoView(path[i]);
        if (MeshList.TreeContainerFromItem(path[i]) is TreeViewItem tvi) { tvi.IsExpanded = true; tvi.UpdateLayout(); }
      }
      MeshList.SelectedItem = path[^1];
      MeshList.ScrollIntoView(path[^1]);
    }
    finally { _pickSyncing = false; }
  }

  /// <summary>Tree row selected by the user: select that part in 3D (outline + readout).</summary>
  private void MeshListSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_pickSyncing || MeshList.SelectedItem is not MeshToggle { Part: { } part })
      return;
    _selPart = part;
    PickName.Text = part.Name;
    PickDetail.Text = $"{part.IndexCount / 3:N0} tris · material {part.Variant}" + (part.Texture is { Length: > 0 } t ? $" · {t}" : "") + (part.Node >= 0 ? $" · node {part.Node}" : "") + "\nselected in the Meshes list";
    PickInfo.IsVisible = true;
    ApplyHighlights();
  }

  /// <summary>SM2_PICKTEST=1 (after SM2_WINSHOT): hover centre, click centre, click a corner; GPU viewport PNGs + log.</summary>
  private async Task PickTestAsync(string shot)
  {
    var stem = Path.ChangeExtension(shot, null);
    var w = PickHost.Bounds.Width; var h = PickHost.Bounds.Height;
    var centre = new Point(w * 0.5, h * 0.45);
    if (Environment.GetEnvironmentVariable("SM2_PICK_XY") is { Length: > 0 } xy && xy.Split(',') is [var xs, var ys]
        && double.TryParse(xs, System.Globalization.CultureInfo.InvariantCulture, out var fx) && double.TryParse(ys, System.Globalization.CultureInfo.InvariantCulture, out var fy))
      centre = new Point(w * fx, h * fy);
    async Task Cap(string name) { await Task.Delay(400); if (UseGpu()) await GpuView.CaptureNextAsync($"{stem}_{name}.png"); await SaveWindowAsync($"{stem}_{name}_window.png"); }
    await Cap("pick0_none");
    PickAt(centre, select: false);
    await Cap("pick1_hover");
    var sw = Stopwatch.StartNew();
    var hit = PickAt(centre, select: true);
    PickLog += $"click latency (pick + tree sync) {sw.Elapsed.TotalMilliseconds:0.0} ms\n";
    await Cap("pick2_selected");
    PickLog += $"tree selected {(MeshList.SelectedItem as MeshToggle)?.Label ?? "(none)"} readout {PickName.Text} | {PickDetail.Text?.Replace("\n", " | ")}\n";
    _hoverPart = null;
    PickAt(new Point(3, 3), select: true);
    await Cap("pick3_deselected");
    PickLog += $"after empty click: selected {(_selPart?.Name ?? "(none)")} tree {(MeshList.SelectedItem as MeshToggle)?.Label ?? "(none)"} readout visible {PickInfo.IsVisible}\n";
    // 20 repeated picks for latency
    var times = new List<double>();
    for (var i = 0; i < 20; i++) { var s = Stopwatch.StartNew(); MeshPick.Pick(PickScene()!, new Vector2((float)(centre.X / w * 2 - 1), (float)(1 - centre.Y / h * 2))); times.Add(s.Elapsed.TotalMilliseconds); }
    times.Sort();
    PickLog += $"pick latency x20: min {times[0]:0.0} median {times[10]:0.0} max {times[^1]:0.0} ms (hit {hit?.Part.Name ?? "none"})\n";
    File.AppendAllText(shot + ".txt", PickLog);
  }
}
