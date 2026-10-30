// R79 (owner t176u/t177u): more Unreal-viewport-like UI, all in this file (hooks: InitUeViewport in the constructor,
// SM2_UETEST in LevelSelect). Gizmo deliberately left out (owner t177u).
//  - F frames the selected part (no selection: old FlyFrame).  - Wheel zooms toward the cursor (Shift x4).
//  - Alt+LMB orbit (= LMB), Alt+RMB drag = dolly zoom.  - Ctrl+1..9 save / 1..9 recall camera bookmarks (per model).
//  - Viewport toolbar: View mode (Lit / Unlit / Wireframe / Clay / Normals / UV checker; GPU path only) + Show menu
//    (Skeleton, Collision/Helpers group, Bounds, Grid) + Stats overlay (bottom right) + F1 / ? shortcut help.
//  - Meshes outliner: search box, right-click menu (Hide, Show, Solo, Show all, Copy name, Copy asset path), Ctrl+Z / Ctrl+Y
//    for visibility + selection.  - Viewport settings persisted in %LOCALAPPDATA%\ForgeLens\viewport_r79.json.
using System.Numerics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private sealed class UeViewportSettings
  {
    public int ViewMode { get; set; }
    public bool ShowBounds { get; set; }
    public bool ShowGrid { get; set; }
    public bool ShowStats { get; set; } = true;
    public Dictionary<string, float[]> Bookmarks { get; set; } = new();
    // R83 panel layout + cloth vertices
    public bool ShowClothVerts { get; set; }
    public bool DetailsOpen { get; set; } = true;
    public double DetailsHeight { get; set; } = 220;
    public bool DockLeft { get; set; }
    public bool OutlinerCollapsed { get; set; }
    public double OutlinerWidth { get; set; } = 240;
  }

  private static readonly string UeSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForgeLens", "viewport_r79.json");
  private UeViewportSettings _ue = new();
  private ComboBox? _ueViewBox;
  private TextBlock? _ueStats;
  private Border? _ueStatsBorder, _ueHelp;
  private TextBox? _ueSearch;
  private IEnumerable<MeshToggle>? _ueRoots;
  private bool _ueSettingRoots;
  private bool _ueZoomDrag;
  private Point _ueZoomLast;
  private double _ueFrameMs, _ueFps;
  private long _ueStatsLast;
  private object? _ueBoundsFor;
  private (Vector3 Min, Vector3 Max)? _ueBounds;
  private readonly Stack<(Dictionary<MeshPart, bool> Vis, MeshPart? Sel)> _ueUndo = new(), _ueRedo = new();
  private (Dictionary<MeshPart, bool> Vis, MeshPart? Sel)? _ueLast;
  private bool _ueRestoring;
  internal string UeLog { get; private set; } = "";

  private static readonly string[] UeViewModes = { "Lit", "Unlit", "Wireframe", "Clay", "Normals", "UV checker" };

  private void InitUeViewport()
  {
    try { if (File.Exists(UeSettingsPath)) _ue = JsonSerializer.Deserialize<UeViewportSettings>(File.ReadAllText(UeSettingsPath)) ?? new(); } catch { _ue = new(); }
    AddHandler(KeyDownEvent, UeKeyDown, RoutingStrategies.Tunnel);
    ViewBorder.AddHandler(PointerPressedEvent, UePointerPressed, RoutingStrategies.Tunnel);
    ViewBorder.AddHandler(PointerMovedEvent, UePointerMoved, RoutingStrategies.Tunnel);
    ViewBorder.AddHandler(PointerReleasedEvent, UePointerReleased, RoutingStrategies.Tunnel);
    ViewBorder.AddHandler(PointerWheelChangedEvent, UeWheel, RoutingStrategies.Tunnel);
    GpuView.Rendered += s =>
    {
      _ueFrameMs = s.CpuMs; _ueFps = s.Fps;
      var now = Environment.TickCount64;
      if (now - _ueStatsLast < 250) return;
      _ueStatsLast = now;
      Dispatcher.UIThread.Post(UeUpdateStats);
    };
    BuildUeOverlays();
    BuildUeOutliner();
    BuildUePanels(); // R83
    InitPinnedWindow(); // R83 SM2_WINRECT
    InitInputProbe(); // R83 SM2_INPUTPROBE
    GpuView.ViewMode = UeGlMode(_ue.ViewMode);
    GpuView.ShowClothVerts = _ue.ShowClothVerts;
  }

  private static int UeGlMode(int ui) => ui switch { 1 => 1, 3 => 2, 4 => 3, 5 => 4, _ => 0 };

  private void UeSave() { try { Directory.CreateDirectory(Path.GetDirectoryName(UeSettingsPath)!); File.WriteAllText(UeSettingsPath, JsonSerializer.Serialize(_ue)); } catch { } }

  // ---------------- overlays: toolbar, stats, help ----------------
  private void BuildUeOverlays()
  {
    if (ViewBorder.Child is not Grid grid) return;
    _ueViewBox = new ComboBox { ItemsSource = UeViewModes, SelectedIndex = Math.Clamp(_ue.ViewMode, 0, UeViewModes.Length - 1), FontSize = 11, MinWidth = 108, Height = 26, Padding = new Thickness(6, 0) };
    ToolTip.SetTip(_ueViewBox, "View mode (UE: Lit / Unlit / Wireframe / Clay / Normals / UV checker). R83: GPU and CPU paths both draw every mode.");
    _ueViewBox.SelectionChanged += (_, _) => UeSetViewMode(_ueViewBox.SelectedIndex);
    var show = new Button { Content = "Show ▾", FontSize = 11, Height = 26, Padding = new Thickness(8, 0) };
    var menu = new MenuFlyout();
    MenuItem Check(string text, Func<bool> get, Action<bool> set)
    {
      var mi = new MenuItem { Header = text, ToggleType = MenuItemToggleType.CheckBox };
      mi.Click += (_, _) => { set(!get()); };
      menu.Opening += (_, _) => mi.IsChecked = get();
      return mi;
    }
    menu.Items.Add(Check("Skeleton", () => SkeletonToggle.IsChecked == true, v => { SkeletonToggle.IsChecked = v; ViewChanged(null, new RoutedEventArgs()); }));
    menu.Items.Add(Check("Collision / Helpers", UeHelpersShown, UeShowHelpers));
    menu.Items.Add(Check("Bounds", () => _ue.ShowBounds, v => { _ue.ShowBounds = v; UeSave(); UeApplyLines(); }));
    menu.Items.Add(Check("Grid", () => _ue.ShowGrid, v => { _ue.ShowGrid = v; UeSave(); UeApplyLines(); }));
    menu.Items.Add(Check("Stats", () => _ue.ShowStats, v => { _ue.ShowStats = v; UeSave(); UeUpdateStats(); }));
    menu.Items.Add(Check("Cloth vertices (physics)", () => _ue.ShowClothVerts, UeShowClothVerts)); // R83
    show.Flyout = menu;
    var help = new Button { Content = "?", FontSize = 11, Height = 26, Width = 26, Padding = new Thickness(0) };
    ToolTip.SetTip(help, "Shortcuts (F1)");
    help.Click += (_, _) => UeToggleHelp();
    var bar = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#99101216")), CornerRadius = new CornerRadius(4), Padding = new Thickness(4), Margin = new Thickness(8),
      HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
      Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _ueViewBox, show, BuildUePanelsButton(), help } },
    };
    bar.AddHandler(PointerPressedEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble);
    bar.Bind(IsVisibleProperty, HudInfo.GetObservable(IsVisibleProperty));
    grid.Children.Add(bar);
    // the toolbar owns the top strip: HUD panels start under it (UE layout)
    HudInfo.Margin = new Thickness(8, 44, 8, 8);
    HudSpeed.Margin = new Thickness(8, 44, 8, 8);

    _ueStats = new TextBlock { FontSize = 11, FontFamily = new FontFamily("Consolas,Courier New,monospace"), Foreground = new SolidColorBrush(Color.Parse("#D8D8D8")) };
    _ueStatsBorder = new Border
    {
      IsVisible = false, IsHitTestVisible = false, Background = new SolidColorBrush(Color.Parse("#99101216")), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5),
      Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Child = _ueStats,
    };
    grid.Children.Add(_ueStatsBorder);

    _ueHelp = new Border
    {
      IsVisible = false, Background = new SolidColorBrush(Color.Parse("#F5101216")), CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 12),
      HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BorderBrush = new SolidColorBrush(Color.Parse("#FF4FA3FF")), BorderThickness = new Thickness(1),
      Margin = new Thickness(8),
      Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = new TextBlock
      {
        FontSize = 12, FontFamily = new FontFamily("Consolas,Courier New,monospace"), Foreground = new SolidColorBrush(Color.Parse("#E8E8E8")),
        Text = "VIEWPORT SHORTCUTS (F1 / ? to close)\n\n" +
               "LMB / Alt+LMB drag    orbit\n" +
               "LMB click             select part (empty = deselect)\n" +
               "RMB hold              fly look   + WASD move, Q/E down/up\n" +
               "RMB + wheel           fly speed (HUD top right)\n" +
               "Shift                 x4 speed (fly keys, wheel zoom)\n" +
               "Wheel                 zoom toward the cursor\n" +
               "Alt+RMB drag          dolly zoom\n" +
               "F                     frame selection (none = whole model)\n" +
               "Ctrl+1..9 / 1..9      save / recall camera bookmark\n" +
               "Ctrl+Z / Ctrl+Y       undo / redo hide, show, selection\n" +
               "H                     HUD on / off\n" +
               "Ctrl+F                file search    Ctrl+E export\n" +
               "Meshes list           right-click: Hide, Solo, Copy name / path",
      } },
    };
    _ueHelp.AddHandler(PointerPressedEvent, (_, e) => { _ueHelp.IsVisible = false; e.Handled = true; }, RoutingStrategies.Bubble);
    grid.Children.Add(_ueHelp);
  }

  /// <summary>R83: cyan crosses on every physics-driven (cloth) vertex. GPU path only: the cloth simulation runs in the GPU preview.</summary>
  internal void UeShowClothVerts(bool v)
  {
    _ue.ShowClothVerts = v; UeSave();
    GpuView.ShowClothVerts = v;
    UeLog += $"show cloth vertices {v} (physics {PhysicsToggle.IsChecked == true}, visible {PhysicsToggle.IsVisible})\n";
    if (_mesh is not null) DrawMesh(false);
  }

  private void UeToggleHelp() { if (_ueHelp is not null) _ueHelp.IsVisible = !_ueHelp.IsVisible; }

  private void UeSetViewMode(int ui)
  {
    if (ui < 0) return;
    _ue.ViewMode = ui; UeSave();
    var wire = ui == 2;
    if (WireToggle.IsChecked != wire) WireToggle.IsChecked = wire;
    GpuView.ViewMode = UeGlMode(ui);
    UeLog += $"view mode {UeViewModes[ui]} (gl {GpuView.ViewMode}) gpu {UseGpu()}\n";
    if (_mesh is not null) ViewChanged(null, new RoutedEventArgs());
  }

  private void UeUpdateStats()
  {
    if (_ueStatsBorder is null || _ueStats is null) return;
    var show = _ue.ShowStats && HudInfo.IsVisible && _mesh is not null;
    _ueStatsBorder.IsVisible = show;
    if (!show) return;
    var mesh = _mesh!;
    long tris = 0, verts = 0;
    var seen = new HashSet<MeshPart>(ReferenceEqualityComparer.Instance);
    foreach (var (pos, _, parts) in UeGeometry(mesh))
    {
      var any = false;
      foreach (var p in parts) if (p.Visible && seen.Add(p)) { tris += p.IndexCount / 3; any = true; }
      if (any) verts += pos.Count;
    }
    _ueStats.Text = $"Triangles {tris,12:N0}\nVertices  {verts,12:N0}\nFrame     {_ueFrameMs,9:0.0} ms\nFPS       {(_ueFps < 20 ? "on demand" : _ueFps.ToString("0")),12}\nCam speed {_flySpeed,8:0.00} m/s";
  }

  // ---------------- Show: helpers group, bounds, grid ----------------
  private IEnumerable<MeshToggle> UeAllToggles(IEnumerable<MeshToggle>? roots = null)
  {
    foreach (var t in roots ?? _ueRoots ?? Enumerable.Empty<MeshToggle>())
    {
      yield return t;
      foreach (var c in UeAllToggles(t.Children)) yield return c;
    }
  }

  private bool UeHelpersShown() => UeAllToggles().FirstOrDefault(t => t.IsGroup) is { } g && g.Visible;

  private void UeShowHelpers(bool v)
  {
    if (UeAllToggles().FirstOrDefault(t => t.IsGroup) is not { } g) { UeLog += "show helpers: no Collision/Helpers group\n"; return; }
    UeRecord();
    g.Visible = v;
  }

  private void UeApplyLines()
  {
    var lines = new List<(Vector3, Vector3, Vector3)>();
    if (_mesh is { } mesh && (_ue.ShowBounds || _ue.ShowGrid) && UeBounds(mesh) is { } b)
    {
      var (mn, mx) = b;
      if (_ue.ShowBounds)
      {
        var c = new Vector3(1f, 0.85f, 0.2f);
        Vector3 P(int i) => new((i & 1) == 0 ? mn.X : mx.X, (i & 2) == 0 ? mn.Y : mx.Y, (i & 4) == 0 ? mn.Z : mx.Z);
        foreach (var (i, j) in new[] { (0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7) })
          lines.Add((P(i), P(j), c));
      }
      if (_ue.ShowGrid)
      {
        var size = MathF.Max(MathF.Max(mx.X - mn.X, mx.Z - mn.Z), 0.5f) * 1.5f;
        var step = MathF.Pow(10, MathF.Floor(MathF.Log10(size / 4)));
        var half = MathF.Ceiling(size / step / 2) * step;
        var cx = MathF.Round((mn.X + mx.X) / 2 / step) * step; var cz = MathF.Round((mn.Z + mx.Z) / 2 / step) * step;
        var g = new Vector3(0.38f, 0.38f, 0.40f);
        for (var t = -half; t <= half + step * 0.01f; t += step)
        {
          lines.Add((new Vector3(cx + t, mn.Y, cz - half), new Vector3(cx + t, mn.Y, cz + half), g));
          lines.Add((new Vector3(cx - half, mn.Y, cz + t), new Vector3(cx + half, mn.Y, cz + t), g));
        }
      }
    }
    GpuView.ExtraLines = lines;
    if (_mesh is not null) DrawMesh(false);
  }

  private (Vector3, Vector3)? UeBounds(ExportedMesh mesh)
  {
    if (ReferenceEquals(_ueBoundsFor, mesh)) return _ueBounds;
    _ueBoundsFor = mesh; _ueBounds = null;
    var mn = new Vector3(float.MaxValue); var mx = new Vector3(float.MinValue); var any = false;
    foreach (var (pos, ind, parts) in UeGeometry(mesh))
      foreach (var p in parts)
      {
        if (!p.Visible) continue;
        var end = Math.Min(ind.Count, p.IndexStart + p.IndexCount);
        for (var i = Math.Max(0, p.IndexStart); i < end; i++)
        {
          var idx = ind[i];
          if ((uint)idx >= (uint)pos.Count) continue;
          var v = pos[idx]; mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); any = true;
        }
      }
    if (any) _ueBounds = (mn, mx);
    return _ueBounds;
  }

  /// <summary>LOD0 and every LOD level (visible parts can live in either; e.g. calgar draws level parts).</summary>
  private static IEnumerable<(List<Vector3> Pos, List<int> Ind, List<MeshPart> Parts)> UeGeometry(ExportedMesh mesh)
  {
    yield return (mesh.Positions, mesh.Indices, mesh.Parts);
    foreach (var l in mesh.Levels) yield return (l.Positions, l.Indices, l.Parts);
  }

  // ---------------- keys ----------------
  private bool UeTyping(KeyEventArgs e) => e.Source is TextBox || FocusManager?.GetFocusedElement() is TextBox;

  internal string UeLastKey { get; private set; } = "-";

  private void UeKeyDown(object? sender, KeyEventArgs e)
  {
    UeLastKey = $"{e.Key}/{e.PhysicalKey}/{e.KeyModifiers}"; // R83 input probe
    if (UeTyping(e)) return;
    if (e.Key == Key.F1 || (e.Key == Key.OemQuestion && e.KeyModifiers == KeyModifiers.Shift)) { UeToggleHelp(); e.Handled = true; return; }
    if (_mesh is null || _fx is not null) return;
    if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z) { UeUndo(); e.Handled = true; return; }
    if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Y) { UeRedo(); e.Handled = true; return; }
    if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None && _selPart is not null && !_flyLooking)
    {
      if (UeFrameSelection()) { e.Handled = true; return; }
    }
    var digit = e.Key is >= Key.D1 and <= Key.D9 ? e.Key - Key.D1 + 1 : e.Key is >= Key.NumPad1 and <= Key.NumPad9 ? e.Key - Key.NumPad1 + 1 : 0;
    if (digit > 0 && !_flyLooking)
    {
      if (e.KeyModifiers == KeyModifiers.Control) { UeBookmarkSave(digit); e.Handled = true; }
      else if (e.KeyModifiers == KeyModifiers.None && UeBookmarkRecall(digit)) e.Handled = true;
    }
  }

  /// <summary>F with a selection: pivot = part bounds centre, distance = fit (bind-pose bounds, LOD0 part list or a LOD level).</summary>
  private bool UeFrameSelection()
  {
    if (_mesh is not { } mesh || _selPart is not { } part) return false;
    var lod = 0; var idx = mesh.Parts.IndexOf(part);
    for (var l = 0; idx < 0 && l < mesh.Levels.Count; l++) { idx = mesh.Levels[l].Parts.IndexOf(part); lod = l + 1; }
    if (idx < 0) return false;
    var b = ViewBorder.Bounds;
    var aspect = b.Height < 32 ? 0.8f : (float)(b.Width / Math.Max(1, b.Height));
    _pivot = MeshPreview.FitCentre(mesh, lod, idx);
    _distance = Math.Max(0.15f, MeshPreview.FitDistance(mesh, aspect, lod, idx));
    UeLog += $"frame selection {part.Name}: pivot ({_pivot.Value.X:0.00},{_pivot.Value.Y:0.00},{_pivot.Value.Z:0.00}) dist {_distance:0.00}\n";
    DrawMesh(false);
    return true;
  }

  private string UeModelKey() => _level is not null ? "level:" + (_mesh?.Name ?? "") : _mesh?.Name ?? "";

  private void UeBookmarkSave(int n)
  {
    var p = CurrentPivot();
    _ue.Bookmarks[$"{UeModelKey()}#{n}"] = new[] { _yaw, _pitch, CurrentDistance(), p.X, p.Y, p.Z };
    UeSave();
    UeLog += $"bookmark {n} saved: eye {FlyEye()}\n";
    if (PreviewNote is not null) ToolTip.SetTip(ViewBorder, $"Bookmark {n} saved");
  }

  private bool UeBookmarkRecall(int n)
  {
    if (!_ue.Bookmarks.TryGetValue($"{UeModelKey()}#{n}", out var v) || v.Length < 6) return false;
    _yaw = v[0]; _pitch = v[1]; _distance = v[2]; _pivot = new Vector3(v[3], v[4], v[5]);
    UeLog += $"bookmark {n} recalled: eye {FlyEye()}\n";
    DrawMesh(false);
    return true;
  }

  // ---------------- mouse: wheel toward cursor, Alt+RMB dolly ----------------
  private void UeWheel(object? sender, PointerWheelEventArgs e)
  {
    if (_mesh is null || _fx is not null || _flyLooking || e.Handled) return; // RMB+wheel = fly speed (FlyWheel)
    var host = PickHost;
    var pt = e.GetPosition(host);
    var boost = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 4f : 1f;
    UeZoomToward(pt, (float)e.Delta.Y * boost);
    e.Handled = true;
  }

  /// <summary>Dolly the eye along the ray under <paramref name="pt"/> (host DIPs) by 12 % of the pivot distance per notch.</summary>
  internal void UeZoomToward(Point pt, float notches)
  {
    var host = PickHost;
    var w = Math.Max(1, host.Bounds.Width); var h = Math.Max(1, host.Bounds.Height);
    var nx = (float)(pt.X / w * 2 - 1); var ny = (float)(1 - pt.Y / h * 2);
    var dist = CurrentDistance();
    var eye = FlyEye();
    var f = -Dir(_yaw, _pitch);
    var r = Vector3.Normalize(Vector3.Cross(f, Vector3.UnitY));
    var u = Vector3.Cross(r, f);
    var tan = MathF.Tan(FovDegrees * MathF.PI / 360f);
    var ray = Vector3.Normalize(f + r * nx * tan * (float)(w / h) + u * ny * tan);
    var step = Math.Clamp(dist * 0.12f * notches, -dist * 2f, dist * 0.9f);
    eye += ray * step;
    _distance = Math.Max(0.15f, dist - step * Vector3.Dot(ray, f));
    _pivot = eye - Dir(_yaw, _pitch) * _distance;
    DrawMesh(true);
  }

  private void UePointerPressed(object? sender, PointerPressedEventArgs e)
  {
    if (_mesh is null || _fx is not null) return;
    var props = e.GetCurrentPoint(ViewBorder).Properties;
    if (props.IsRightButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
    {
      _ueZoomDrag = true; _ueZoomLast = e.GetPosition(ViewBorder); _distance = CurrentDistance();
      e.Pointer.Capture(ViewBorder); e.Handled = true;
    }
  }

  private void UePointerMoved(object? sender, PointerEventArgs e)
  {
    if (!_ueZoomDrag) return;
    var p = e.GetPosition(ViewBorder);
    var d = (p.X - _ueZoomLast.X) - (p.Y - _ueZoomLast.Y);
    _ueZoomLast = p;
    UeDolly((float)d);
    e.Handled = true;
  }

  internal void UeDolly(float px)
  {
    var dist = CurrentDistance(); var pivot = CurrentPivot();
    _distance = Math.Clamp(dist * MathF.Exp(-px * 0.01f), 0.15f, 100000f);
    _pivot = pivot;
    DrawMesh(true);
  }

  private void UePointerReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (!_ueZoomDrag) return;
    _ueZoomDrag = false; e.Pointer.Capture(null); e.Handled = true; DrawMesh(false);
  }

  // ---------------- outliner: search, context menu, undo ----------------
  private void BuildUeOutliner()
  {
    _ueSearch = new TextBox { Watermark = "Search meshes…", FontSize = 11, Margin = new Thickness(4, 0, 4, 4), Height = 26, MinHeight = 26, Padding = new Thickness(6, 3) };
    _ueSearch.TextChanged += (_, _) => UeApplySearch();
    if (MeshList.Parent is Grid g)
    {
      // R79: search box sits in the MESHES title row (Grid.Row 2) under the title, inside a small panel.
      var title = g.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetRow(t) == 2);
      if (title is not null)
      {
        g.Children.Remove(title);
        var panel = new StackPanel();
        panel.Children.Add(title);
        panel.Children.Add(_ueSearch);
        Grid.SetRow(panel, 2);
        g.Children.Add(panel);
      }
    }
    MeshList.PropertyChanged += (_, e) =>
    {
      if (e.Property != ItemsControl.ItemsSourceProperty || _ueSettingRoots) return;
      _ueRoots = MeshList.ItemsSource as IEnumerable<MeshToggle>;
      _ueUndo.Clear(); _ueRedo.Clear(); _ueLast = null; _ueBoundsFor = null;
      foreach (var t in UeAllToggles()) t.PropertyChanged += UeTogglePropertyChanged;
      if (_ueSearch is { Text.Length: > 0 }) { _ueSearch.Text = ""; }
      _ueLast = UeSnapshot();
      Dispatcher.UIThread.Post(() => { UeApplyLines(); UeUpdateStats(); });
    };
    MeshList.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(UeSelectionMaybeChanged);
    var menu = new ContextMenu();
    void Item(string header, Action act) { var mi = new MenuItem { Header = header }; mi.Click += (_, _) => act(); menu.Items.Add(mi); }
    Item("Hide", () => UeSetVis(UeSelectedToggle(), false));
    Item("Show", () => UeSetVis(UeSelectedToggle(), true));
    Item("Solo (hide all others)", () => UeSolo(UeSelectedToggle()));
    Item("Show all", UeShowAll);
    Item("Copy name", () => UeCopy(UeSelectedToggle()?.Label));
    Item("Copy asset path", () => UeCopy(UeAssetPath(UeSelectedToggle())));
    MeshList.ContextMenu = menu;
  }

  private MeshToggle? UeSelectedToggle() => MeshList.SelectedItem as MeshToggle;

  private string? UeAssetPath(MeshToggle? t)
  {
    if (t is null) return null;
    var file = (FileList.SelectedItem as FileRow) is { } row && _catalog is { } c ? c.GetFile(row.Index).Path : _mesh?.Name ?? "";
    return $"{file}#{t.Label}";
  }

  private async void UeCopy(string? text)
  {
    if (string.IsNullOrEmpty(text)) return;
    UeLog += $"copy '{text}'\n";
    try { if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb) await cb.SetTextAsync(text); } catch (Exception ex) { UeLog += "clipboard failed: " + ex.Message + "\n"; }
  }

  private void UeApplySearch()
  {
    var q = _ueSearch?.Text?.Trim() ?? "";
    _ueSettingRoots = true;
    try
    {
      if (q.Length == 0) MeshList.ItemsSource = _ueRoots;
      else MeshList.ItemsSource = UeAllToggles().Where(t => !t.IsGroup && t.Label.Contains(q, StringComparison.OrdinalIgnoreCase)).Select(UeLeaf).ToList();
    }
    finally { _ueSettingRoots = false; }
  }

  // Flat search results: a MeshToggle shows its Children in the tree; results list the matching rows only (children
  // appear as their own rows when they match), so the same toggle object is reused (checkbox stays bound to the part).
  private static MeshToggle UeLeaf(MeshToggle t) => t;

  private void UeTogglePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (e.PropertyName != nameof(MeshToggle.Visible) || _ueRestoring) return;
    Dispatcher.UIThread.Post(UeCommitIfChanged);
  }

  private Dictionary<MeshPart, bool> UeVisMap()
  {
    var d = new Dictionary<MeshPart, bool>(ReferenceEqualityComparer.Instance);
    if (_mesh is { } m) foreach (var (_, _, parts) in UeGeometry(m)) foreach (var p in parts) d[p] = p.Visible;
    return d;
  }

  private (Dictionary<MeshPart, bool>, MeshPart?) UeSnapshot() => (UeVisMap(), _selPart);

  private static bool UeSame((Dictionary<MeshPart, bool> Vis, MeshPart? Sel) a, (Dictionary<MeshPart, bool> Vis, MeshPart? Sel) b)
    => ReferenceEquals(a.Sel, b.Sel) && a.Vis.Count == b.Vis.Count && a.Vis.All(kv => b.Vis.TryGetValue(kv.Key, out var v) && v == kv.Value);

  /// <summary>Push the last committed state if the current one differs (checkbox toggles, menu actions, selection).</summary>
  private void UeCommitIfChanged()
  {
    if (_ueRestoring || _mesh is null) return;
    var now = UeSnapshot();
    if (_ueLast is { } last && !UeSame(last, now)) { _ueUndo.Push(last); _ueRedo.Clear(); }
    _ueLast = now;
  }

  private void UeRecord() => UeCommitIfChanged();

  private void UeSelectionMaybeChanged() => UeCommitIfChanged();

  private void UeSetVis(MeshToggle? t, bool v)
  {
    if (t is null) return;
    UeLog += $"{(v ? "show" : "hide")} {t.Label}\n";
    t.Visible = v;
    UeCommitIfChanged();
  }

  private void UeSolo(MeshToggle? t)
  {
    if (t is null || _mesh is not { } m) return;
    UeLog += $"solo {t.Label}\n";
    var keep = new HashSet<MeshPart>(ReferenceEqualityComparer.Instance);
    if (t.Part is { } solo) keep.Add(solo); // the row itself (its LOD children stay as they are: hidden)
    else foreach (var c in UeAllToggles(t.Children)) if (c.Part is { } gp && gp.Visible) keep.Add(gp);
    UeApplyVis(m.Parts.Concat(m.Levels.SelectMany(l => l.Parts)).Distinct(ReferenceEqualityComparer.Instance).Cast<MeshPart>().ToDictionary(p => p, p => keep.Contains(p), (IEqualityComparer<MeshPart>)ReferenceEqualityComparer.Instance));
    UeCommitIfChanged();
  }

  private void UeShowAll()
  {
    if (_mesh is not { } m) return;
    UeLog += "show all (render parts)\n";
    UeApplyVis(m.Parts.Concat(m.Levels.SelectMany(l => l.Parts)).Distinct(ReferenceEqualityComparer.Instance).Cast<MeshPart>()
      .ToDictionary(p => p, p => (!MeshToggle.IsNonRender(p) && p.HideReason is null) || p.Visible, (IEqualityComparer<MeshPart>)ReferenceEqualityComparer.Instance));
    UeCommitIfChanged();
  }

  /// <summary>Apply a visibility map through the toggles (UI stays in sync) with one redraw.</summary>
  private void UeApplyVis(Dictionary<MeshPart, bool> map)
  {
    var was = _ueRestoring; _ueRestoring = true;
    try
    {
      foreach (var t in UeAllToggles()) if (t.Part is { } p && map.TryGetValue(p, out var v) && p.Visible != v) t.SetVisible(v);
      foreach (var (p, v) in map) p.Visible = v;
    }
    finally { _ueRestoring = was; }
    _ueBoundsFor = null;
    UeApplyLines();
    DrawMesh(false);
  }

  private void UeUndo()
  {
    UeCommitIfChanged();
    if (_ueUndo.Count == 0) { UeLog += "undo: empty\n"; return; }
    _ueRedo.Push(UeSnapshot());
    UeRestore(_ueUndo.Pop());
    UeLog += "undo\n";
  }

  private void UeRedo()
  {
    if (_ueRedo.Count == 0) { UeLog += "redo: empty\n"; return; }
    _ueUndo.Push(UeSnapshot());
    UeRestore(_ueRedo.Pop());
    UeLog += "redo\n";
  }

  private void UeRestore((Dictionary<MeshPart, bool> Vis, MeshPart? Sel) s)
  {
    _ueRestoring = true;
    try
    {
      UeApplyVis(s.Vis);
      _selPart = s.Sel;
      if (_selPart is null) PickInfo.IsVisible = false;
      SyncTreeToSelection();
      ApplyHighlights();
    }
    finally { _ueRestoring = false; }
    _ueLast = UeSnapshot();
  }

  // ---------------- SM2_UETEST hook ----------------
  /// <summary>SM2_UETEST=1 (after SM2_WINSHOT): every R79 feature through the same methods the keys / menus call; GPU + window PNGs and a log.</summary>
  private async Task UeTestAsync(string shot)
  {
    var stem = Path.ChangeExtension(shot, null);
    async Task Cap(string name) { await Task.Delay(700); if (UseGpu()) await GpuView.CaptureNextAsync($"{stem}_ue_{name}.png"); await SaveWindowAsync($"{stem}_ue_{name}_window.png"); }
    UeLog += $"start eye {FlyEye()} dist {CurrentDistance():0.00} gpu {UseGpu()} renderer {GpuView.Info}\n";
    for (var m = 0; m < UeViewModes.Length; m++) { _ueViewBox!.SelectedIndex = m; await Cap("view" + m + "_" + UeViewModes[m].Replace(' ', '_')); }
    _ueViewBox!.SelectedIndex = 0;
    _ue.ShowBounds = true; _ue.ShowGrid = true; UeApplyLines(); await Cap("show_bounds_grid");
    UeLog += $"extra lines {GpuView.ExtraLines.Count}\n";
    SkeletonToggle.IsChecked = true; ViewChanged(null, new RoutedEventArgs()); await Cap("show_skeleton");
    SkeletonToggle.IsChecked = false; ViewChanged(null, new RoutedEventArgs());
    UeShowHelpers(true); await Cap("show_helpers"); UeLog += $"helpers shown {UeHelpersShown()}\n"; UeShowHelpers(false);
    _ue.ShowBounds = false; _ue.ShowGrid = false; UeApplyLines();
    // wheel toward cursor: 3 notches at the left third
    var host = PickHost; var e0 = FlyEye();
    UeZoomToward(new Point(host.Bounds.Width * 0.3, host.Bounds.Height * 0.5), 3);
    UeLog += $"wheel x3 at 30%/50%: eye {e0} -> {FlyEye()} dist {CurrentDistance():0.00}\n"; await Cap("wheel_toward_left");
    var d0 = CurrentDistance(); UeDolly(-80); UeLog += $"alt+rmb dolly -80px: dist {d0:0.00} -> {CurrentDistance():0.00}\n";
    UeBookmarkSave(1);
    FlyFrame(); UeLog += $"F (no selection): eye {FlyEye()}\n"; await Cap("frame_all");
    UeBookmarkRecall(1); await Cap("bookmark1_recalled");
    // select centre part, F frames it
    var hit = PickAt(new Point(host.Bounds.Width * 0.5, host.Bounds.Height * 0.45), select: true);
    UeCommitIfChanged();
    UeLog += $"selected {hit?.Part.Name ?? "none"}\n";
    if (UeFrameSelection()) await Cap("frame_selected");
    // outliner: search, hide, solo, undo/redo
    var sel = UeSelectedToggle();
    _ueSearch!.Text = sel?.Label.Length > 3 ? sel.Label[..4] : "body"; await Task.Delay(500);
    UeLog += $"search '{_ueSearch.Text}': {(MeshList.ItemsSource as System.Collections.ICollection)?.Count} rows\n"; await Cap("search");
    _ueSearch.Text = ""; await Task.Delay(500);
    sel = UeAllToggles().FirstOrDefault(t => t.Part is not null && ReferenceEquals(t.Part, _selPart)) ?? UeAllToggles().FirstOrDefault(t => !t.IsGroup && t.Visible);
    MeshList.SelectedItem = sel;
    FlyFrame();
    int Vis() => UeGeometry(_mesh!).Sum(g => g.Parts.Count(p => p.Visible));
    UeLog += $"visible parts {Vis()}\n";
    UeSetVis(sel, false); UeLog += $"after hide {Vis()}\n"; await Cap("hidden");
    UeSolo(UeAllToggles().FirstOrDefault(t => !t.IsGroup && t.Visible)); UeLog += $"after solo {Vis()}\n"; await Cap("solo");
    UeUndo(); UeLog += $"after undo {Vis()}\n"; UeUndo(); UeLog += $"after undo2 {Vis()}\n"; await Cap("undo2");
    UeRedo(); UeLog += $"after redo {Vis()}\n";
    UeShowAll(); UeLog += $"after show all {Vis()}\n";
    UeCopy(sel?.Label); UeCopy(UeAssetPath(sel));
    _ue.ShowStats = true; UeUpdateStats(); UeToggleHelp(); await Cap("help_stats"); UeToggleHelp();
    UeLog += $"stats: {_ueStats?.Text?.Replace("\n", " | ")}\nsettings file {UeSettingsPath} exists {File.Exists(UeSettingsPath)}\n";
    File.AppendAllText(shot + ".txt", UeLog);
  }
}
