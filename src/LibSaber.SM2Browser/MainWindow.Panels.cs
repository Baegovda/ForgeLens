// R83 (owner t176u/t177u follow-up): Details panel (separate from the viewport selection readout), collapsible /
// dockable right panel with its layout saved in viewport_r79.json, Show menu "Cloth vertices".
//  - DETAILS: an Expander under the Meshes tree (own splitter); shows the selected part (3D pick or tree row):
//    name, pool (LOD 0 / LOD n), tris, index range, vertex pool size, bounds, material variant, blend, textures,
//    hide reason, node, td scalars. Copy button copies the text.
//  - Panel layout: "Panels" button on the viewport toolbar: Outliner on the right / left, collapse the outliner,
//    collapse Details. Outliner width, Details height and every toggle persist (viewport_r79.json) and restore on start.
using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2Browser;

public partial class MainWindow
{
  private Expander? _ueDetails;
  private SelectableTextBlock? _ueDetailsText;
  private GridSplitter? _ueDetailsSplitter;
  private Control? _ueViewportPane, _ueOutlinerPane;
  private GridSplitter? _ueOutlinerSplitter;
  internal string PanelsLog { get; private set; } = "";

  private void BuildUePanels()
  {
    // --- Details expander (RightPanel rows: 0 kitbash, 1 splitter, 2 title, 3 meshes, 4 splitter, 5 details) ---
    _ueDetailsText = new SelectableTextBlock
    {
      FontSize = 11, FontFamily = new FontFamily("Consolas,Courier New,monospace"), TextWrapping = TextWrapping.Wrap,
      Foreground = new SolidColorBrush(Color.Parse("#D0D0D0")), Text = "Select a part (click it in the viewport or a row above).",
    };
    var copy = new Button { Content = "Copy", FontSize = 10, Padding = new Thickness(6, 1), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 4) };
    copy.Click += (_, _) => UeCopy(_ueDetailsText.Text);
    _ueDetails = new Expander
    {
      Header = "DETAILS", IsExpanded = _ue.DetailsOpen, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
      Padding = new Thickness(6, 4), Margin = new Thickness(0, 2, 0, 0),
      Content = new DockPanel { Children = { copy, new ScrollViewer { Content = _ueDetailsText, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } } },
    };
    DockPanel.SetDock(copy, Dock.Top);
    ToolTip.SetTip(_ueDetails, "Details of the selected part (UE Details panel). Collapse with the header; the height is the splitter above.");
    _ueDetailsSplitter = new GridSplitter { Height = 4, Background = (IBrush?)this.FindResource("FL.Border") ?? Brushes.Gray, ResizeDirection = GridResizeDirection.Rows };
    RightPanel.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
    RightPanel.RowDefinitions.Add(new RowDefinition(new GridLength(_ue.DetailsHeight is > 40 and < 3000 ? _ue.DetailsHeight : 220)));
    Grid.SetRow(_ueDetailsSplitter, 4); Grid.SetRow(_ueDetails, 5);
    RightPanel.Children.Add(_ueDetailsSplitter); RightPanel.Children.Add(_ueDetails);
    _ueDetails.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) UeApplyDetailsOpen(); };
    _ueDetailsSplitter.DragCompleted += (_, _) => UeSaveLayout();
    UeApplyDetailsOpen(save: false);

    // --- dock side + collapse (ViewerColumns: viewport | splitter | outliner) ---
    _ueViewportPane = ViewerColumns.Children.OfType<Control>().FirstOrDefault(c => Grid.GetColumn(c) == 0);
    _ueOutlinerSplitter = ViewerColumns.Children.OfType<GridSplitter>().FirstOrDefault();
    _ueOutlinerPane = ViewerColumns.Children.OfType<Control>().FirstOrDefault(c => Grid.GetColumn(c) == 2);
    if (_ueOutlinerSplitter is not null) _ueOutlinerSplitter.DragCompleted += (_, _) => UeSaveLayout();
    if (_ue.OutlinerWidth is > 120 and < 3000) ViewerColumns.ColumnDefinitions[_ue.DockLeft ? 0 : 2].Width = new GridLength(_ue.OutlinerWidth);
    UeApplyDock();
  }

  /// <summary>The "Panels" menu for the viewport toolbar.</summary>
  private Button BuildUePanelsButton()
  {
    var b = new Button { Content = "Panels ▾", FontSize = 11, Height = 26, Padding = new Thickness(8, 0) };
    ToolTip.SetTip(b, "Panel layout: outliner side, collapse outliner / details. Saved between runs.");
    var menu = new MenuFlyout();
    MenuItem Check(string text, Func<bool> get, Action<bool> set)
    {
      var mi = new MenuItem { Header = text, ToggleType = MenuItemToggleType.CheckBox };
      mi.Click += (_, _) => set(!get());
      menu.Opening += (_, _) => mi.IsChecked = get();
      return mi;
    }
    menu.Items.Add(Check("Outliner on the left", () => _ue.DockLeft, v => UeSetDock(v, _ue.OutlinerCollapsed)));
    menu.Items.Add(Check("Hide outliner (Meshes + Details)", () => _ue.OutlinerCollapsed, v => UeSetDock(_ue.DockLeft, v)));
    menu.Items.Add(Check("Details panel", () => _ue.DetailsOpen, v => { if (_ueDetails is not null) _ueDetails.IsExpanded = v; }));
    var reset = new MenuItem { Header = "Reset panel layout" };
    reset.Click += (_, _) => UeResetLayout();
    menu.Items.Add(reset);
    b.Flyout = menu;
    return b;
  }

  internal void UeSetDock(bool left, bool collapsed)
  {
    UeCaptureWidths();
    _ue.DockLeft = left; _ue.OutlinerCollapsed = collapsed;
    UeApplyDock();
    UeSaveLayout();
  }

  private void UeResetLayout()
  {
    _ue.DockLeft = false; _ue.OutlinerCollapsed = false; _ue.OutlinerWidth = 240; _ue.DetailsHeight = 220;
    if (_ueDetails is not null) _ueDetails.IsExpanded = true;
    _ue.DetailsOpen = true;
    UeApplyDock();
    UeApplyDetailsOpen();
    PanelsLog += "layout reset\n";
  }

  private void UeCaptureWidths()
  {
    var cols = ViewerColumns.ColumnDefinitions;
    var oc = _ue.DockLeft ? 0 : 2;
    if (!_ue.OutlinerCollapsed && cols[oc].ActualWidth > 120) _ue.OutlinerWidth = Math.Round(cols[oc].ActualWidth);
  }

  private void UeApplyDock()
  {
    if (_ueViewportPane is null || _ueOutlinerPane is null) return;
    var cols = ViewerColumns.ColumnDefinitions;
    var w = _ue.OutlinerWidth is > 120 and < 3000 ? _ue.OutlinerWidth : 240;
    var vc = _ue.DockLeft ? 2 : 0; var oc = _ue.DockLeft ? 0 : 2;
    Grid.SetColumn(_ueViewportPane, vc); Grid.SetColumn(_ueOutlinerPane, oc);
    cols[vc].Width = new GridLength(1, GridUnitType.Star); cols[vc].MinWidth = 0;
    cols[oc].Width = _ue.OutlinerCollapsed ? new GridLength(0) : new GridLength(w);
    cols[1].Width = _ue.OutlinerCollapsed ? new GridLength(0) : new GridLength(6);
    _ueOutlinerPane.IsVisible = !_ue.OutlinerCollapsed;
    if (_ueOutlinerSplitter is not null) _ueOutlinerSplitter.IsVisible = !_ue.OutlinerCollapsed;
    _ueViewportPane.Margin = _ue.DockLeft ? new Thickness(4, 8, 8, 8) : new Thickness(8, 8, 4, 8);
    _ueOutlinerPane.Margin = _ue.DockLeft ? new Thickness(8, 8, 4, 8) : new Thickness(4, 8, 8, 8);
    PanelsLog += $"dock {(_ue.DockLeft ? "left" : "right")} collapsed {_ue.OutlinerCollapsed} width {w}\n";
  }

  private void UeApplyDetailsOpen(bool save = true)
  {
    if (_ueDetails is null) return;
    var rows = RightPanel.RowDefinitions;
    if (rows.Count < 6) return;
    if (!_ueDetails.IsExpanded && rows[5].Height.IsAbsolute && rows[5].ActualHeight >= 120) _ue.DetailsHeight = Math.Round(rows[5].ActualHeight);
    _ue.DetailsOpen = _ueDetails.IsExpanded;
    rows[5].Height = _ue.DetailsOpen ? new GridLength(_ue.DetailsHeight is > 40 and < 3000 ? _ue.DetailsHeight : 220) : GridLength.Auto;
    if (_ueDetailsSplitter is not null) _ueDetailsSplitter.IsVisible = _ue.DetailsOpen;
    if (save) UeSaveLayout();
    UeUpdateDetails();
  }

  private void UeSaveLayout()
  {
    UeCaptureWidths();
    var rows = RightPanel.RowDefinitions;
    if (_ue.DetailsOpen && !_ue.OutlinerCollapsed && rows.Count >= 6 && rows[5].ActualHeight >= 120) _ue.DetailsHeight = Math.Round(rows[5].ActualHeight);
    UeSave();
  }

  /// <summary>
  ///   R83 A/B pinning: SM2_WINRECT="x,y,w,h" (physical pixels, virtual-screen coordinates) puts the window at the same
  ///   place and size every run, so two builds render the viewport at the same DPI and pixel size (R82 runs opened on
  ///   different monitors). Applied once the window opened (DIP size = px / RenderScaling of that monitor).
  /// </summary>
  private void InitPinnedWindow()
  {
    if (Environment.GetEnvironmentVariable("SM2_WINRECT") is not { Length: > 0 } r) return;
    var v = r.Split(',').Select(s => int.TryParse(s.Trim(), out var n) ? n : int.MinValue).ToArray();
    if (v.Length != 4 || v.Any(n => n == int.MinValue) || v[2] < 400 || v[3] < 300) return;
    WindowStartupLocation = WindowStartupLocation.Manual;
    WindowState = WindowState.Normal;
    Position = new PixelPoint(v[0], v[1]);
    void Apply()
    {
      Position = new PixelPoint(v[0], v[1]);
      var s = RenderScaling > 0 ? RenderScaling : 1;
      Width = v[2] / s; Height = v[3] / s;
      PanelsLog += $"pinned window {v[0]},{v[1]} {v[2]}x{v[3]} px scaling {s}\n";
    }
    Opened += (_, _) => { Apply(); Dispatcher.UIThread.Post(Apply, DispatcherPriority.Background); };
  }

  /// <summary>
  ///   R83 real-input probe: SM2_INPUTPROBE=file writes one line every 150 ms with the camera, hover / selection, visible
  ///   part count, undo depth, view mode and the screen pixel positions of the viewport centre and the first outliner
  ///   eye icon, so tools/re/pc/r83_sendinput.ps1 can drive the window with real SendInput and check each effect.
  /// </summary>
  private void InitInputProbe()
  {
    if (Environment.GetEnvironmentVariable("SM2_INPUTPROBE") is not { Length: > 0 } file) return;
    var keyLog = file + ".keys.txt";
    AddHandler(KeyDownEvent, (_, e) => { try { File.AppendAllText(keyLog, $"keydown {e.Key} phys {e.PhysicalKey} mods {e.KeyModifiers} handled {e.Handled} hud {HudToggle.IsChecked} src {e.Source?.GetType().Name} focus {FocusManager?.GetFocusedElement()?.GetType().Name}\n"); } catch { } }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    AddHandler(TextInputEvent, (_, e) => { try { File.AppendAllText(keyLog, $"textinput '{e.Text}' src {e.Source?.GetType().Name}\n"); } catch { } }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    try
    {
      Win32Properties.AddWndProcHookCallback(this, (IntPtr h, uint msg, IntPtr w, IntPtr l, ref bool handled) =>
      {
        if (msg is 0x100 or 0x101 or 0x102 or 0x104 or 0x105 or 0x10D or 0x10E or 0x10F or 0x281 or 0x282 or 0x286)
          try { File.AppendAllText(keyLog, $"wm 0x{msg:X} w 0x{w.ToInt64():X} l 0x{l.ToInt64():X}\n"); } catch { }
        return IntPtr.Zero;
      });
    }
    catch (Exception e) { File.AppendAllText(keyLog, "wndproc hook failed " + e.Message + "\n"); }
    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
    timer.Tick += (_, _) =>
    {
      try
      {
        string Scr(Visual? v, double fx, double fy)
        {
          if (v is null || !v.IsEffectivelyVisible || v.Bounds.Width < 1) return "-";
          var p = v.PointToScreen(new Point(v.Bounds.Width * fx, v.Bounds.Height * fy));
          return $"{p.X},{p.Y}";
        }
        Visual? eye = null;
        foreach (var d in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(MeshList))
        {
          if (eye is null && d is Avalonia.Controls.Primitives.ToggleButton tb && tb.Classes.Contains("eye") && tb.IsEffectivelyVisible
              && tb.TranslatePoint(new Point(tb.Bounds.Width / 2, tb.Bounds.Height / 2), MeshList) is { } q && q.Y > 4 && q.Y < MeshList.Bounds.Height - 4) eye = tb;
          if (eye is not null) break;
        }
        var vis = _mesh is null ? 0 : UeGeometry(_mesh).Sum(g => g.Parts.Count(p => p.Visible));
        var eyeVal = (eye as Avalonia.Controls.Primitives.ToggleButton)?.DataContext is MeshToggle mt ? $"{mt.Label}={mt.Visible}" : "-";
        var pv = CurrentPivot();
        var line = FormattableString.Invariant($"t {Environment.TickCount64} mesh {_mesh?.Name ?? "-"} yaw {_yaw:0.0000} pitch {_pitch:0.0000} dist {CurrentDistance():0.0000} pivot {pv.X:0.000},{pv.Y:0.000},{pv.Z:0.000} hover {_hoverPart?.Name ?? "-"} sel {_selPart?.Name ?? "-"} visible {vis} undo {_ueUndo.Count} redo {_ueRedo.Count} view {_ue.ViewMode} help {_ueHelp?.IsVisible} focus {(IsActive ? 1 : 0)} vpc {Scr(ViewBorder, 0.5, 0.5)} vp30 {Scr(ViewBorder, 0.3, 0.45)} vpempty {Scr(ViewBorder, 0.97, 0.22)} hud {HudToggle.IsChecked} lastkey {UeLastKey} eye0 {Scr(eye, 0.5, 0.5)} eye0val {eyeVal} log {UeLog.Length}");
        File.WriteAllText(file, line + "\n");
      }
      catch (Exception e) { try { File.WriteAllText(file, "probe error " + e.Message + "\n"); } catch { } }
    };
    timer.Start();
  }

  /// <summary>R83 test (SM2_UETEST=1, after the R79 steps): Details, dock / collapse, cloth vertices, CPU view modes.</summary>
  private async Task UeTest83Async(string shot)
  {
    var stem = Path.ChangeExtension(shot, null);
    var log = new StringBuilder();
    async Task Win(string name) { await Task.Delay(900); await SaveWindowAsync($"{stem}_r83_{name}_window.png"); }
    async Task Gpu(string name) { await Task.Delay(700); if (UseGpu()) await GpuView.CaptureNextAsync($"{stem}_r83_{name}.png"); }
    var host = PickHost;
    var hit = PickAt(new Point(host.Bounds.Width * 0.5, host.Bounds.Height * 0.45), select: true);
    log.Append($"r83 selected {hit?.Part.Name ?? "none"}\nr83 details:\n{_ueDetailsText?.Text}\n");
    await Win("details");
    // F through the real routed KeyDown (every window handler in its real order, Fly mode as the user has it)
    var fly0 = FlyToggle.IsChecked; var d0 = CurrentDistance(); var pv0 = CurrentPivot();
    RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Avalonia.Input.Key.F, KeyModifiers = Avalonia.Input.KeyModifiers.None, Source = this });
    await Task.Delay(300);
    log.Append($"r83 routed F (fly {fly0}, sel {_selPart?.Name}): dist {d0:0.000} -> {CurrentDistance():0.000} pivot {pv0} -> {CurrentPivot()}\n");
    UeSetDock(true, false); await Win("dock_left");
    log.Append($"r83 dock left: viewport col {Grid.GetColumn(_ueViewportPane!)} outliner col {Grid.GetColumn(_ueOutlinerPane!)} widths {string.Join("/", ViewerColumns.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth)))}\n");
    UeSetDock(true, true); await Win("collapsed");
    log.Append($"r83 collapsed: widths {string.Join("/", ViewerColumns.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth)))} gpu {GpuView.Bounds.Width:0}x{GpuView.Bounds.Height:0}\n");
    UeSetDock(false, false);
    _ueDetails!.IsExpanded = false; await Win("details_collapsed"); _ueDetails.IsExpanded = true;
    log.Append($"r83 settings {File.ReadAllText(UeSettingsPath).Replace("\"Bookmarks\"", "\n  \"Bookmarks\"")[..Math.Min(400, File.ReadAllText(UeSettingsPath).Length)]}\n");
    // cloth vertices: physics on, settle (orbit physics runs the cloth at rest), cyan crosses
    PhysicsToggle.IsChecked = true; ViewChanged(null, new Avalonia.Interactivity.RoutedEventArgs());
    UeShowClothVerts(true); KickOrbitTimer(); await Task.Delay(2500);
    await Gpu("cloth_verts_on");
    log.Append($"r83 cloth verts drawn {GpuView.LastClothVerts} (physics visible {PhysicsToggle.IsVisible}, overrides {GpuView.LastScene?.VertexOverrides.Count ?? 0})\n");
    UeShowClothVerts(false); await Gpu("cloth_verts_off");
    log.Append($"r83 cloth verts after off {GpuView.LastClothVerts}\n");
    // CPU path view modes (RenderTargetBitmap sees the CPU image)
    ClearPick();
    GpuToggle.IsChecked = false; GpuToggled(null, new Avalonia.Interactivity.RoutedEventArgs()); await Task.Delay(3000);
    for (var m = 0; m < UeViewModes.Length; m++) { _ueViewBox!.SelectedIndex = m; await Task.Delay(2500); await Win("cpu_view" + m + "_" + UeViewModes[m].Replace(' ', '_')); }
    _ueViewBox!.SelectedIndex = 0;
    GpuToggle.IsChecked = true; GpuToggled(null, new Avalonia.Interactivity.RoutedEventArgs()); await Task.Delay(1500);
    log.Append($"r83 cpu view modes done gpu {UseGpu()}\n{PanelsLog}");
    File.AppendAllText(shot + ".txt", log.ToString());
  }

  /// <summary>Fill the Details panel from the selected part (null = hint).</summary>
  private void UeUpdateDetails()
  {
    if (_ueDetailsText is null) return;
    if (_mesh is not { } mesh || _selPart is not { } p) { _ueDetailsText.Text = "Select a part (click it in the viewport or a row above)."; return; }
    var sb = new StringBuilder();
    var pool = "?"; var poolVerts = 0; List<Vector3>? pos = null; List<int>? ind = null;
    if (mesh.Parts.Contains(p)) { pool = "LOD 0"; pos = mesh.Positions; ind = mesh.Indices; }
    else for (var l = 0; l < mesh.Levels.Count; l++) if (mesh.Levels[l].Parts.Contains(p)) { pool = $"LOD {l + 1}"; pos = mesh.Levels[l].Positions; ind = mesh.Levels[l].Indices; break; }
    poolVerts = pos?.Count ?? 0;
    var used = new HashSet<int>(); var mn = new Vector3(float.MaxValue); var mx = new Vector3(float.MinValue);
    if (pos is not null && ind is not null)
      for (var i = Math.Max(0, p.IndexStart); i < Math.Min(ind.Count, p.IndexStart + p.IndexCount); i++)
        if ((uint)ind[i] < (uint)pos.Count && used.Add(ind[i])) { mn = Vector3.Min(mn, pos[ind[i]]); mx = Vector3.Max(mx, pos[ind[i]]); }
    void Row(string k, string? v) { if (!string.IsNullOrEmpty(v)) sb.Append($"{k,-10} {v}\n"); }
    Row("Name", p.Name);
    Row("Pool", $"{pool} ({poolVerts:N0} verts in pool)");
    Row("Triangles", $"{p.IndexCount / 3:N0}");
    Row("Vertices", $"{used.Count:N0} used");
    Row("Indices", $"{p.IndexStart:N0} .. {p.IndexStart + p.IndexCount:N0}");
    if (used.Count > 0)
    {
      var s = mx - mn;
      Row("Bounds", $"size {s.X:0.000} x {s.Y:0.000} x {s.Z:0.000}");
      Row("  min", $"{mn.X:0.000}, {mn.Y:0.000}, {mn.Z:0.000}");
      Row("  max", $"{mx.X:0.000}, {mx.Y:0.000}, {mx.Z:0.000}");
    }
    Row("Visible", p.Visible ? "yes" : "no");
    Row("Hidden by", p.HideReason);
    Row("Node", p.Node >= 0 ? $"{p.Node}" + (p.Node < mesh.Bones.Count ? $" ({mesh.Bones[p.Node].Name})" : "") : null);
    sb.Append("-- material --\n");
    Row("Variant", p.Variant);
    Row("Blend", p.Blend + (p.BlendMode != "opaque" ? $" (td {p.BlendMode})" : ""));
    Row("Albedo", p.Texture);
    Row("Normal", p.Normal);
    Row("Specular", p.Specular is { } sp ? sp + (p.SpecularChannels.Length > 0 ? $" [{p.SpecularChannels}]" : "") : null);
    Row("Emissive", p.Emissive);
    if (p.AlbedoTint is { Length: >= 3 } t) Row("Tint", $"{t[0]:0.00} {t[1]:0.00} {t[2]:0.00}");
    if (p.Sfx) Row("Sfx", $"{p.SfxBlend}");
    if (p.AnisoSpec is not null) Row("Aniso", "anisotropic_spec");
    if (p.Chameleon is not null) Row("Chameleon", "yes");
    foreach (var (k, v) in p.TdScalars.OrderBy(k => k.Key).Take(24)) Row("  " + k, v.ToString("0.###"));
    _ueDetailsText.Text = sb.ToString().TrimEnd();
  }
}
