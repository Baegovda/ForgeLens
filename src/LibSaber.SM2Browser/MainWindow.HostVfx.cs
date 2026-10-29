using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2Browser.Effects;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser;

/// <summary>
///   Effects on the model view: attach an .sfx to the model's bones (each emitter / light / trail on its
///   <c>nameParent</c> bone, the rest on the chosen root socket) and play the effects the clip's morpheme events start
///   through the blueprint graphs (<see cref="AnimEventVfxMap"/>).
/// </summary>
public partial class MainWindow
{
  private HostVfx? _hostVfx;
  private AnimEventVfxMap? _hostVfxMap;
  private Task<AnimEventVfxMap>? _hostVfxMapTask;
  private readonly Stopwatch _hostVfxClock = new();
  private ExportedMesh? _hostVfxSocketsFor;

  private bool HostVfxActive => _hostVfx is { Active: true } && _mesh is not null;

  private void InitHostVfx()
  {
    AnimBar.PropertyChanged += (_, e) =>
    {
      if (e.Property != IsVisibleProperty) return;
      HostVfxBar.IsVisible = AnimBar.IsVisible && GpuToggle.IsChecked == true;
      if (HostVfxBar.IsVisible) FillHostVfxSockets();
    };
  }

  /// <summary>
  ///   Dev/QA without a game folder (after SM2_VFX_LOOSE): SM2_MODEL_OPEN="tplDir[|clip.sani]" shows a loose model,
  ///   SM2_HOST_OPEN=effect attaches it, SM2_HOST_EVENTS=1 turns on anim events (blueprints: SM2_VFX_BLUEPRINTS).
  /// </summary>
  private async Task OpenLooseModelFromEnvironmentAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_MODEL_OPEN") is not { Length: > 0 } spec) return;
    var parts = spec.Split('|');
    var dir = parts[0].TrimEnd('/', '\\');
    var stem = Path.GetFileName(dir).Replace(".tpl", "");
    var (mesh, clip) = await Task.Run(() =>
    {
      var m = TemplateMeshReader.LoadRaw(File.ReadAllBytes(Path.Combine(dir, stem + ".tpl")),
        File.Exists(Path.Combine(dir, stem + ".tpl_data")) ? File.ReadAllBytes(Path.Combine(dir, stem + ".tpl_data")) : null, stem);
      if (Environment.GetEnvironmentVariable("SM2_HOST_LOD1") == "1")
        foreach (var part in m.Parts)
          (part.Visible, part.HideReason) = part.Name.EndsWith("_LOD1", StringComparison.OrdinalIgnoreCase) ? (true, null) : (false, "LOD");
      AnimationClip? c = parts.Length > 1 && File.Exists(parts[1])
        ? SaniReader.FromBytes(File.ReadAllBytes(parts[1]), File.Exists(parts[1] + "_data") ? File.ReadAllBytes(parts[1] + "_data") : null, Path.GetFileNameWithoutExtension(parts[1]))
        : null;
      return (m, c);
    });
    _fx = null;
    VfxBar.IsVisible = false;
    _mesh = mesh;
    _previewFileIndex = -1;
    _modelKey = stem;
    _distance = 0;
    _time = 0;
    _yaw = 0.7f;
    _pitch = 0.18f;
    FillModelControls(mesh);
    SkeletonToggle.IsVisible = mesh.Bones.Count > 0;
    WireToggle.IsVisible = mesh.HasTriangles;
    GpuToggle.IsVisible = true;
    PreviewText.IsVisible = false;
    FolderText.Text = "loose model: " + dir;
    AnimBar.IsVisible = mesh.Bones.Count > 0;
    if (clip is not null)
    {
      _clip = clip;
      _suppressAnim = true;
      AnimBox.ItemsSource = new[] { new AnimChoice { Label = clip.Name, Index = -1 } };
      AnimBox.SelectedIndex = 0;
      _suppressAnim = false;
      ApplyClipToMesh(clip);
    }
    else if (mesh.TplAnim is { Sequences.Count: > 0 } tplAnim)
    {
      // propanim QA: the embedded sequences (SM2_PROP_COND picks one, else Auto).
      _animAll = PropSequenceRows(tplAnim);
      ApplyAnimFilter(keepSelection: false);
      SetupPropConditions();
      if (_propConds.FirstOrDefault(c => c.Name.Equals(_propCond, StringComparison.OrdinalIgnoreCase)) is { } cond)
        await PlayPropStandaloneAsync(cond, autoPlay: Environment.GetEnvironmentVariable("SM2_HOST_PLAY") == "1");
    }

    FillHostVfxSockets();
    HostVfxName.Text = Environment.GetEnvironmentVariable("SM2_HOST_OPEN") ?? "";
    HostVfxEventsToggle.IsChecked = Environment.GetEnvironmentVariable("SM2_HOST_EVENTS") == "1";
    HostVfxFilter.Text = Environment.GetEnvironmentVariable("SM2_VFX_BP_FILTER") ?? "";
    if (Environment.GetEnvironmentVariable("SM2_HOST_CAM") is { Length: > 0 } cam && cam.Split(':') is { Length: >= 3 } c)
    {
      _yaw = float.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture);
      _pitch = float.Parse(c[1], System.Globalization.CultureInfo.InvariantCulture);
      _distance = MeshPreview.FitDistance(mesh, 1f) * float.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture);
    }

    await ApplyHostVfxAsync();
    if (clip is { HasCurves: true } && Environment.GetEnvironmentVariable("SM2_HOST_PLAY") == "1")
      PlayClicked(null, new RoutedEventArgs());
    else
      DrawMesh();
  }

  /// <summary>Overlay hook for <see cref="GpuFrameRequest"/>: null when nothing is attached.</summary>
  private Func<ExportedMesh, PreviewScene, float, EffectLayer?>? HostVfxOverlay()
  {
    if (!HostVfxActive) return null;
    var vfx = _hostVfx!;
    var loop = HostVfxLoopToggle.IsChecked == true;
    return (mesh, scene, time) => vfx.Frame(mesh, scene, time, loop ? (float)_hostVfxClock.Elapsed.TotalSeconds : time);
  }

  private void FillHostVfxSockets()
  {
    if (_mesh is null || ReferenceEquals(_hostVfxSocketsFor, _mesh)) return;
    _hostVfxSocketsFor = _mesh;
    var names = new List<string> { "Auto (nameParent)" };
    // Likely sockets first (weapon tips, jets, sfx_* markers), then every bone.
    static bool Socket(string n) => n.StartsWith("sfx", StringComparison.OrdinalIgnoreCase) || n.Contains("tip", StringComparison.OrdinalIgnoreCase)
      || n.StartsWith("jet", StringComparison.OrdinalIgnoreCase) || n.Contains("flame", StringComparison.OrdinalIgnoreCase)
      || n.Contains("muzzle", StringComparison.OrdinalIgnoreCase) || n.Contains("source", StringComparison.OrdinalIgnoreCase);
    var bones = _mesh.Bones.Select(b => b.Name).Where(n => n.Length > 0).Distinct().ToList();
    names.AddRange(bones.Where(Socket).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    names.AddRange(bones.Where(n => !Socket(n)));
    HostVfxSocket.ItemsSource = names;
    HostVfxSocket.SelectedIndex = 0;
  }

  private async Task<HostVfx?> EnsureHostVfxAsync()
  {
    if (_hostVfx is not null) return _hostVfx;
    if (_vfxLibrary is null && _catalog is not null && _store is not null)
    {
      var catalog = _catalog;
      var store = _store;
      await Task.Run(() => EnsureVfxLibrary(catalog, store));
    }

    if (_vfxLibrary is null || _vfxTextures is null) return null;
    _hostVfx = new HostVfx { Library = _vfxLibrary, Aliases = _vfxAliases, Textures = _vfxTextures, Meshes = _vfxMeshes };
    var redraw = () => Dispatcher.UIThread.Post(() => { if (HostVfxActive) DrawMesh(); });
    var previous = _vfxTextures.Loaded;
    _vfxTextures.Loaded = () => { previous?.Invoke(); redraw(); };
    return _hostVfx;
  }

  /// <summary>Blueprints (.sso with morpheme event nodes) from the catalog, or SM2_VFX_BLUEPRINTS for loose QA.</summary>
  private Task<AnimEventVfxMap> LoadHostVfxMapAsync()
  {
    if (_hostVfxMap is not null) return Task.FromResult(_hostVfxMap);
    var catalog = _catalog;
    var store = _store;
    return _hostVfxMapTask ??= Task.Run(() =>
    {
      if (Environment.GetEnvironmentVariable("SM2_VFX_BLUEPRINTS") is { Length: > 0 } dir) return AnimEventVfxMap.FromDirectory(dir);
      var map = new AnimEventVfxMap();
      if (catalog is null || store is null) return map;
      for (var i = 0; i < catalog.FileCount; i++)
      {
        var path = catalog.GetFile(i).Path;
        if (!path.EndsWith(".sso", StringComparison.OrdinalIgnoreCase)) continue;
        byte[] bytes;
        try { bytes = store.Read(i); }
        catch { continue; }
        if (!SslText.LooksText(bytes)) continue;
        var text = Encoding.UTF8.GetString(bytes);
        if (AnimEventVfxMap.Relevant(text)) // morpheme hooks, and (propanim) prop Startanim graphs
          map.AddBlueprint(Path.GetFileNameWithoutExtension(path), text);
      }

      return map;
    }).ContinueWith(t => _hostVfxMap = t.Result, TaskScheduler.Default);
  }

  private async void HostVfxAttachClicked(object? sender, RoutedEventArgs e) => await ApplyHostVfxAsync();

  private async void HostVfxClearClicked(object? sender, RoutedEventArgs e)
  {
    HostVfxName.Text = "";
    HostVfxEventsToggle.IsChecked = false;
    await ApplyHostVfxAsync();
  }

  private async void HostVfxOptionsChanged(object? sender, RoutedEventArgs e) => await ApplyHostVfxAsync();

  private async Task ApplyHostVfxAsync()
  {
    if (_mesh is null) return;
    FillHostVfxSockets();
    var mesh = _mesh;
    var name = HostVfxName.Text?.Trim();
    var events = HostVfxEventsToggle.IsChecked == true;
    if (string.IsNullOrEmpty(name) && !events && _hostVfx is null) return;
    if (await EnsureHostVfxAsync() is not { } vfx)
    {
      HostVfxStatus.Text = "No effect library (open a game folder, or SM2_VFX_LOOSE).";
      return;
    }

    // mtl1-49: context playback keeps its events on while "Show effects" is on.
    events |= vfx.Context is not null && CtxFxToggle.IsChecked == true && ContextBar.IsVisible;
    var socket = HostVfxSocket.SelectedIndex > 0 ? HostVfxSocket.SelectedItem as string ?? "" : "";
    var status = new List<string>();
    var single = await Task.Run(() => vfx.SetEffect(mesh, name, socket));
    if (single.Length > 0) status.Add(single);
    if (events)
    {
      HostVfxStatus.Text = "Loading blueprints…";
      vfx.Map = await LoadHostVfxMapAsync();
      vfx.SourceFilter = HostVfxFilter.Text?.Trim() ?? "";
    }

    var ev = await Task.Run(() => vfx.SetEvents(mesh, events));
    if (ev.Length > 0) status.Add(ev);
    else if (events) status.Add(mesh.Clip is null ? "Anim events: pick and play a clip." : "");
    HostVfxStatus.Text = string.Join(" | ", status.Where(s => s.Length > 0));
    ToolTip.SetTip(HostVfxStatus, HostVfxStatus.Text);
    _hostVfxClock.Restart();
    DrawMesh();
  }
}
