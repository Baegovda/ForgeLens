using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SM2Browser.Effects;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser;

/// <summary>.sfx effects from the open game catalog, by basename.</summary>
internal sealed class CatalogEffectSource : IEffectSource
{
  private readonly ArchiveStore _store;
  private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, int> _index = new(StringComparer.OrdinalIgnoreCase);

  public CatalogEffectSource(GameCatalog catalog, ArchiveStore store)
  {
    _store = store;
    for (var i = 0; i < catalog.FileCount; i++)
    {
      var file = catalog.GetFile(i);
      if (!file.Path.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase)) continue;
      var name = Path.GetFileNameWithoutExtension(file.Path);
      if (_paths.TryAdd(name, file.Path)) _index[file.Path] = i;
    }
  }

  public IReadOnlyDictionary<string, string> Paths => _paths;

  public string? ReadText(string path)
  {
    if (!_index.TryGetValue(path, out var index)) return null;
    var bytes = _store.Read(index);
    return SslText.LooksText(bytes) ? Encoding.UTF8.GetString(bytes) : null;
  }
}

/// <summary>Standalone VFX view: play / pause (Space), restart, loop, scrub, orbit camera, on the GPU preview.</summary>
public partial class MainWindow
{
  private sealed class VfxState
  {
    public required EffectDefinition Definition;
    public required EffectInstance Instance;
    public required EffectCamera Camera;
    public float Time;
    public bool Playing;
    public double Last;
    public int Particles;
    public int Quads;
  }

  private GameCatalog? _vfxCatalog;
  private EffectLibrary? _vfxLibrary;
  private PartAliases _vfxAliases = PartAliases.Empty;
  private EffectTextures? _vfxTextures;
  private EffectMeshes _vfxMeshes = EffectMeshes.None;
  private VfxState? _fx;
  private bool _suppressVfxScrub;
  private bool _vfxStatsHooked;

  private void EnsureVfxLibrary(GameCatalog catalog, ArchiveStore store)
  {
    if (ReferenceEquals(_vfxCatalog, catalog) && _vfxLibrary is not null) return;
    _vfxLibrary = new EffectLibrary(new CatalogEffectSource(catalog, store));
    _vfxAliases = catalog.FindByFileName("part_aliases.ps") is { Count: > 0 } a && SslText.LooksText(store.Read(a[0]))
      ? PartAliases.Parse(Encoding.UTF8.GetString(store.Read(a[0])))
      : PartAliases.Empty;
    _vfxTextures = new EffectTextures(name =>
      catalog.FindByFileName(name + ".pct.resource") is { Count: > 0 } t ? TextureLoader.Load(catalog, store, t[0]) : null);
    _vfxTextures.Loaded = () => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_fx is not null) GpuView.ShowEffect(GpuView.Effect); });
    _vfxMeshes = new EffectMeshes(
      name => catalog.FindByFileName(name + ".tpl") is { Count: > 0 } t ? LibSaber.SM2.Assets.Meshes.TemplateMeshReader.Load(catalog, store, t[0]) : null,
      name => catalog.FindByFileName(name + ".td") is { Count: > 0 } t && SslText.LooksText(store.Read(t[0])) ? Encoding.UTF8.GetString(store.Read(t[0])) : null);
    _vfxMeshes.Loaded = _vfxTextures.Loaded;
    _vfxCatalog = catalog;
  }

  /// <summary>Shows an .sfx on the GPU preview. False when it cannot (no GPU, or the file is not an effect tree).</summary>
  private async Task<bool> TryShowEffectAsync(int fileIndex, string path, int generation)
  {
    if (_catalog is null || _store is null || GpuView.HasFailed || GpuToggle.IsChecked != true) return false;
    var catalog = _catalog;
    var store = _store;
    await Task.Run(() => EnsureVfxLibrary(catalog, store));
    return await ShowEffectAsync(Path.GetFileNameWithoutExtension(path), generation);
  }

  /// <summary>Dev/QA without a game folder: SM2_VFX_LOOSE="sfxDir|part_aliases.ps|textureDir[|meshDir]" and SM2_VFX_OPEN=name
  /// (meshDir defaults to a <c>mesh</c> folder next to textureDir).</summary>
  private async Task OpenLooseEffectFromEnvironmentAsync()
  {
    if (Environment.GetEnvironmentVariable("SM2_VFX_LOOSE") is not { Length: > 0 } loose || Environment.GetEnvironmentVariable("SM2_VFX_OPEN") is not { Length: > 0 } name) return;
    var parts = loose.Split('|');
    await Task.Run(() =>
    {
      _vfxLibrary = new EffectLibrary(new DirectoryEffectSource(parts[0]));
      _vfxAliases = PartAliases.Load(parts[1]);
      _vfxTextures = EffectTextures.FromDirectory(parts[2]);
      _vfxMeshes = EffectMeshes.FromDirectory(parts.Length > 3 ? parts[3] : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(parts[2]).TrimEnd('/', '\\')) ?? ".", "mesh"), parts[2]);
    });
    _vfxTextures!.Loaded = () => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_fx is not null) GpuView.ShowEffect(GpuView.Effect); });
    _vfxMeshes.Loaded = _vfxTextures.Loaded;
    FolderText.Text = "loose: " + parts[0];
    if (!await ShowEffectAsync(name, ++_previewGeneration)) PreviewNote.Text = name + ": not an effect";
  }

  private async Task<bool> ShowEffectAsync(string name, int generation)
  {
    var built = await Task.Run(() =>
    {
      if (_vfxLibrary!.Resolve(name) is not { } tree) return null;
      var def = EffectDefinition.Build(name, tree);
      if (def.Emitters.Count == 0 && def.Lights.Count == 0 && def.Trails.Count == 0) return null;
      _vfxTextures!.Prefetch(def.Textures);
      _vfxMeshes.Prefetch(def.Emitters.Select(e => e.Geom?.Template));
      var instance = EffectInstance.Standalone(def, 1);
      var camera = EffectCamera.Fit(instance, Math.Min(def.Duration, 3f));
      return new VfxState { Definition = def, Instance = instance, Camera = camera };
    });
    if (generation != _previewGeneration || built is null) return false;

    _fx = built;
    var def = built.Definition;
    if (!_vfxStatsHooked)
    {
      _vfxStatsHooked = true;
      GpuView.Rendered += stats =>
      {
        if (_fx is not { } fx || GpuView.Effect is null) return;
        HideLoading(_previewGeneration);
        RenderStats.Text = $"GPU {stats.Width}x{stats.Height} · {fx.Particles} particles · {fx.Quads} quads · sprites {stats.UploadMs:0.0} ms · {stats.Fps:0} fps";
        SyncVfxScrub();
      };
    }

    var sprites = def.Emitters.Count(e => e.IsSprite && e.Target == DrawTarget.Color);
    var notes = new List<string>
    {
      $"{name}: {def.Emitters.Count} emitters ({sprites} sprite, {def.Emitters.Count(e => e.Geom is not null)} mesh), {def.Lights.Count} lights, {def.Trails.Count} trails, {def.Duration:0.00}s{(def.Loops ? " (loops)" : "")}.",
    };
    var skipped = def.Emitters.Where(e => !e.IsSprite && e.Geom is null).Select(e => e.ParticleType)
      .Concat(def.Emitters.Where(e => e.IsSprite && e.Target != DrawTarget.Color).Select(e => e.Target.ToString().ToLowerInvariant()))
      .Concat(def.Unsupported.Select(u => u.Type)).GroupBy(t => t).Select(g => $"{g.Key} x{g.Count()}").ToList();
    if (skipped.Count > 0) notes.Add("Not drawn: " + string.Join(", ", skipped) + ".");
    if (def.Coverings.Count > 0)
      notes.Add("Surface covering (paints the coveringMask of nearby surfaces, not a particle): "
        + string.Join(", ", def.Coverings.Select(c => $"{c.Kind} r×{c.RadiusScale:0.##}{(c.Visible ? "" : " (off)")}")) + ".");
    notes.Add("Standalone view on a ground grid; socketed emitters sit 1.2 m up, pointing +X; effects with trails fly along +X at 40 m/s. Drag to orbit, wheel to zoom, Space plays.");
    PreviewNote.Text = string.Join(" ", notes);
    PreviewText.Text = "";
    PreviewText.IsVisible = false;
    PreviewImage.IsVisible = false;
    AnimBar.IsVisible = false;
    VfxBar.IsVisible = true;
    PlayButton.IsVisible = true;
    GpuView.IsVisible = true;
    built.Playing = true;
    built.Last = _playClock.Elapsed.TotalSeconds;
    PlayButton.Content = "Pause";
    GpuView.Continuous = true;
    GpuView.ShowEffect(EffectFrameFor);
    return true;
  }

  private void StopEffect()
  {
    if (_fx is null) return;
    _fx = null;
    VfxBar.IsVisible = false;
    GpuView.Continuous = false;
    GpuView.ShowEffect(null);
    PlayButton.Content = "Play";
  }

  /// <summary>GL-thread frame callback: advance the clock, step the fixed-rate simulation, build sprite batches.</summary>
  private EffectFrame? EffectFrameFor(int width, int height)
  {
    if (_fx is not { } fx || _vfxTextures is null) return null;
    lock (fx)
    {
      var now = _playClock.Elapsed.TotalSeconds;
      if (fx.Playing)
      {
        fx.Time += (float)Math.Clamp(now - fx.Last, 0, 0.1);
        var end = Math.Max(0.1f, fx.Definition.Duration);
        if (fx.Time > end + 0.25f)
        {
          if (VfxLoopIsOn) fx.Time = 0;
          else
          {
            fx.Time = end;
            fx.Playing = false;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { PlayButton.Content = "Play"; GpuView.Continuous = false; });
          }
        }
      }

      fx.Last = now;
      fx.Instance.Seek(fx.Time);
      var (view, projection, eye) = fx.Camera.Matrices(width, height);
      var watch = Stopwatch.StartNew();
      var layer = new EffectLayer
      {
        Batches = EffectSprites.Build(fx.Instance, _vfxAliases, view),
        Lights = fx.Instance.Lights().ToList(),
        Texture = _vfxTextures.Get,
        Ground = VfxGroundIsOn,
        Meshes = EffectMeshParticles.Build(fx.Instance, view),
        Mesh = _vfxMeshes.Get,
      };
      fx.Particles = fx.Instance.ParticleCount;
      fx.Quads = layer.Quads;
      return new EffectFrame(layer, view, projection, eye);
    }
  }

  private volatile bool VfxLoopIsOn = true;
  private volatile bool VfxGroundIsOn = true;

  private void VfxTogglePlay()
  {
    if (_fx is not { } fx) return;
    lock (fx)
    {
      if (!fx.Playing && fx.Time >= Math.Max(0.1f, fx.Definition.Duration)) fx.Time = 0;
      fx.Playing = !fx.Playing;
      fx.Last = _playClock.Elapsed.TotalSeconds;
    }

    PlayButton.Content = fx.Playing ? "Pause" : "Play";
    GpuView.Continuous = fx.Playing;
    GpuView.ShowEffect(GpuView.Effect);
  }

  private void VfxRestartClicked(object? sender, RoutedEventArgs e)
  {
    if (_fx is not { } fx) return;
    lock (fx)
    {
      fx.Time = 0;
      fx.Instance.Reset();
      fx.Last = _playClock.Elapsed.TotalSeconds;
    }

    GpuView.ShowEffect(GpuView.Effect);
  }

  private void VfxOptionsChanged(object? sender, RoutedEventArgs e)
  {
    VfxLoopIsOn = VfxLoopToggle.IsChecked == true;
    VfxGroundIsOn = VfxGroundToggle.IsChecked == true;
    GpuView.ShowEffect(GpuView.Effect);
  }

  private void VfxScrubChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
  {
    if (_suppressVfxScrub || _fx is not { } fx) return;
    lock (fx)
    {
      fx.Playing = false;
      fx.Time = (float)VfxScrub.Value;
    }

    PlayButton.Content = "Play";
    GpuView.Continuous = false;
    GpuView.ShowEffect(GpuView.Effect);
  }

  private void SyncVfxScrub()
  {
    if (_fx is not { } fx) return;
    _suppressVfxScrub = true;
    VfxScrub.Maximum = Math.Max(0.1f, fx.Definition.Duration);
    VfxScrub.Value = Math.Clamp(fx.Time, 0, VfxScrub.Maximum);
    VfxTime.Text = $"{fx.Time:0.00} / {fx.Definition.Duration:0.00}s";
    _suppressVfxScrub = false;
  }

  private bool VfxPointerMoved(Avalonia.Point delta)
  {
    if (_fx is not { } fx) return false;
    lock (fx)
      fx.Camera = fx.Camera with { Yaw = fx.Camera.Yaw - (float)delta.X * 0.01f, Pitch = fx.Camera.Pitch + (float)delta.Y * 0.01f };
    GpuView.ShowEffect(GpuView.Effect);
    return true;
  }

  private bool VfxWheel(PointerWheelEventArgs e)
  {
    if (_fx is not { } fx) return false;
    lock (fx)
      fx.Camera = fx.Camera with { Distance = Math.Clamp(fx.Camera.Distance * (1f - (float)e.Delta.Y * 0.1f), 0.2f, 200f) };
    GpuView.ShowEffect(GpuView.Effect);
    return true;
  }
}
