using LibSaber.SM2Browser.Gpu;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.SM2.Assets.Physics;
using LibSaber.SM2.Assets.Textures;

namespace LibSaber.SM2Browser;

public partial class MainWindow : Window
{

  private readonly BrowserSettings _settings = BrowserSettings.Load();
  private readonly RecentAssets _recent = RecentAssets.Load();
  private readonly DispatcherTimer _searchTimer;
  private readonly DispatcherTimer _playTimer;
  private CancellationTokenSource? _load;
  private GameCatalog? _catalog;
  private ArchiveStore? _store;
  private CatalogDirectory? _directory;
  private int _previewGeneration;
  private TextureDocument? _texture;
  private ExportedMesh? _mesh;
  private AnimationClip? _clip;
  private int _clipIndex = -1;
  private float _yaw = 0.7f;
  private float _pitch = 0.18f;
  private float _distance;
  private float _time;
  private bool _dragging;
  private Point _lastPoint;
  private bool _suppressMip;
  private bool _suppressView;
  private int _listGeneration;
  private int _drawGeneration;
  private bool _drawBusy;
  private bool _drawAgain;
  private bool _drawFast;
  private Dictionary<string, PreviewImage>? _albedo;
  private Dictionary<string, PreviewImage>? _normals;
  private Dictionary<string, PreviewImage>? _specular;
  private HierarchicalTreeDataGridSource<CatalogDirectory>? _tree;
  private int _pendingFileIndex = -1;
  private string _pendingDirectory = "";
  private bool _suppressSearch;
  private bool _suppressRecent;
  private bool _suppressMesh;
  private int _previewFileIndex = -1;
  private int _animGeneration;
  private bool _suppressAnim;
  private bool _suppressScrub;
  private readonly System.Diagnostics.Stopwatch _playClock = System.Diagnostics.Stopwatch.StartNew();
  private double _playLast;
  private List<AnimChoice> _animAll = new();
  private string _modelKey = "";
  private readonly WriteableBitmap?[] _frames = new WriteableBitmap?[2];
  private int _frameFlip;
  private readonly System.Diagnostics.Stopwatch _fpsClock = System.Diagnostics.Stopwatch.StartNew();
  private int _fpsFrames;
  private double _fpsStart;
  private double _fps;
  private List<SaniIndex.Header>? _clipHeaders;

  public MainWindow()
  {
    InitializeComponent();
    InitHostVfx();
    RecentList.ItemsSource = _recent.Items;
    // A closed, focused ComboBox steps its selection on the mouse wheel; on the clip list that silently
    // loads a different (possibly 100 s cinematic) clip while the user is just scrolling the window.
    foreach (var box in new ComboBox[] { AnimBox, LodBox, PartBox })
      box.AddHandler(PointerWheelChangedEvent, (_, e) => { if (!box.IsDropDownOpen) e.Handled = true; }, RoutingStrategies.Tunnel);
    ShowFiles();
    _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    _searchTimer.Tick += (_, _) =>
    {
      _searchTimer.Stop();
      RefreshFiles();
    };
    _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
    _playTimer.Tick += (_, _) =>
    {
      if (_clip is null)
        return;
      var now = _playClock.Elapsed.TotalSeconds;
      _time += (float)Math.Clamp(now - _playLast, 0, 0.1);
      _playLast = now;
      if (_clip.Duration > 0 && _time > _clip.Duration)
        _time = 0;
      SyncScrub();
      DrawMesh();
    };
    // Space = Play/Pause anywhere in the window, except while typing (search, anim filter) or in an open dropdown.
    AddHandler(KeyDownEvent, SpaceKeyDown, RoutingStrategies.Tunnel);
    AddHandler(KeyDownEvent, ShortcutKeyDown, RoutingStrategies.Tunnel);
    Opened += async (_, _) =>
    {
      if (!string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
        await LoadFolderAsync(_settings.LastFolder);
      await OpenLooseEffectFromEnvironmentAsync();
      await OpenLooseModelFromEnvironmentAsync();
    };
  }

  private async void OpenClicked(object? sender, RoutedEventArgs e)
  {
    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
      Title = "Space Marine 2 folder (game root or client_pc)",
      AllowMultiple = false,
    });
    if (folders.Count == 0)
      return;
    var path = folders[0].TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(path))
    {
      StatusText.Text = "That folder is not a local path.";
      return;
    }

    await LoadFolderAsync(path);
  }

  private async Task LoadFolderAsync(string path)
  {
    _load?.Cancel();
    _load = new CancellationTokenSource();
    var token = _load.Token;
    OpenButton.IsEnabled = false;
    ExportButton.IsEnabled = false;
    FolderText.Text = path;
    StatusText.Text = "Reading archive caches…";
    var progress = new Progress<CatalogProgress>(update =>
    {
      StatusText.Text = update.ArchiveCount == 0
        ? "No archives."
        : $"Reading {update.CurrentArchive} ({update.ArchivesDone}/{update.ArchiveCount}), {update.FilesIndexed:N0} files";
    });

    try
    {
      var catalog = await Task.Run(() => GameCatalog.Load(path, progress, token), token);
      token.ThrowIfCancellationRequested();
      _store?.Dispose();
      _catalog = catalog;
      _store = new ArchiveStore(catalog);
      _clipHeaders = null;
      _settings.LastFolder = path;
      _settings.Save();
      BindTree(catalog);
      UpdateExportButton();
    }
    catch (OperationCanceledException)
    {
      StatusText.Text = "Folder load cancelled.";
    }
    catch (Exception ex)
    {
      StatusText.Text = ex.Message;
    }
    finally
    {
      OpenButton.IsEnabled = true;
    }
  }

  private void BindTree(GameCatalog catalog)
  {
    var source = new HierarchicalTreeDataGridSource<CatalogDirectory>(new[] { catalog.Root })
    {
      Columns =
      {
        new HierarchicalExpanderColumn<CatalogDirectory>(
          new TextColumn<CatalogDirectory, string>("Folder", directory => directory.Name, width: new GridLength(1, GridUnitType.Star)),
          directory => directory.Children,
          directory => directory.Children.Length > 0),
      },
    };
    source.RowSelection!.SingleSelect = true;
    source.RowSelection.SelectionChanged += (_, _) =>
    {
      if (source.RowSelection.SelectedItem is not CatalogDirectory directory)
        return;
      _directory = directory;
      RefreshFiles();
    };
    FolderTree.Source = source;
    _tree = source;
    _directory = catalog.Root;
    var root = new IndexPath(0);
    source.Expand(root);
    source.RowSelection.Select(root);
    RefreshFiles();
  }

  private void SearchChanged(object? sender, TextChangedEventArgs e)
  {
    SearchClear.IsVisible = !string.IsNullOrEmpty(SearchBox.Text);
    if (_suppressSearch)
      return;
    _searchTimer.Stop();
    _searchTimer.Start();
  }

  // vfx4-3 usability: Enter = run the search now and preview the first hit, Down = move into the list, Esc = clear.
  private void SearchKeyDown(object? sender, KeyEventArgs e)
  {
    switch (e.Key)
    {
      case Key.Escape when !string.IsNullOrEmpty(SearchBox.Text):
        SearchBox.Text = "";
        e.Handled = true;
        break;
      case Key.Enter:
        _searchTimer.Stop();
        _selectFirstAfterRefresh = true;
        RefreshFiles();
        e.Handled = true;
        break;
      case Key.Down when FileList.ItemCount > 0:
        if (FileList.SelectedIndex < 0)
          FileList.SelectedIndex = 0;
        FileList.Focus();
        e.Handled = true;
        break;
    }
  }

  private void SearchClearClicked(object? sender, RoutedEventArgs e)
  {
    SearchBox.Text = "";
    SearchBox.Focus();
  }

  private bool _selectFirstAfterRefresh;
  private bool _exporting;

  /// <summary>Ctrl+F focuses the search box, Ctrl+E opens export for the selection.</summary>
  private void ShortcutKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.KeyModifiers != KeyModifiers.Control)
      return;
    if (e.Key == Key.F)
    {
      SearchBox.Focus();
      SearchBox.SelectAll();
      e.Handled = true;
    }
    else if (e.Key == Key.E && ExportButton.IsEnabled)
    {
      e.Handled = true;
      ExportClicked(ExportButton, new RoutedEventArgs());
    }
  }

  /// <summary>Export is only offered when something is selected; the label says how many.</summary>
  private void UpdateExportButton()
  {
    var n = FileList.SelectedItems?.Count ?? 0;
    ExportButton.IsEnabled = !_exporting && _catalog is not null && n > 0;
    ExportButton.Content = _exporting ? "Exporting…" : n switch { 0 => "Export…", 1 => "Export 1 file…", _ => $"Export {n} files…" };
  }

  private void FilterChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (FolderText is null)
      return;
    RefreshFiles();
  }

  private void SubfoldersChanged(object? sender, RoutedEventArgs e)
  {
    if (_catalog is null)
      return;
    RefreshFiles();
  }

  private async void RefreshFiles()
  {
    if (_catalog is null)
      return;

    var catalog = _catalog;
    var directory = _directory ?? catalog.Root;
    var filter = FilterBox.SelectedIndex switch
    {
      1 => AssetFilter.Textures,
      2 => AssetFilter.Models,
      3 => AssetFilter.Animations,
      4 => AssetFilter.Effects,
      5 => AssetFilter.Text,
      _ => AssetFilter.All,
    };
    var search = SearchBox.Text;
    var includeSubfolders = SubfoldersBox?.IsChecked == true;
    var scoped = string.IsNullOrWhiteSpace(search) ? directory : null;
    var generation = ++_listGeneration;
    FileQuery query;
    try
    {
      query = await Task.Run(() => catalog.Query(scoped, search, filter, includeSubfolders && scoped is not null));
    }
    catch (Exception ex)
    {
      if (generation == _listGeneration)
        StatusText.Text = ex.Message;
      return;
    }

    if (generation != _listGeneration)
      return;

    FileList.ItemsSource = new FileRowList(catalog, query);
    if (_selectFirstAfterRefresh && query.Length > 0)
      FileList.SelectedIndex = 0;
    _selectFirstAfterRefresh = false;
    UpdateExportButton();
    var scope = scoped is null
      ? "search"
      : includeSubfolders ? directory.Name + " and subfolders" : directory.Name;
    var hidden = catalog.DuplicatesCollapsed == 0
      ? ""
      : $" {catalog.DuplicatesCollapsed:N0} duplicate paths hidden; the later archive is shown.";
    var skipped = catalog.ArchivesSkipped == 0 ? "" : $" {catalog.ArchivesSkipped} caches skipped.";
    if (query.Length == 0 && !string.IsNullOrWhiteSpace(search))
    {
      StatusText.Text = $"Nothing matches '{search.Trim()}'. Try fewer words, or set the type filter to All types.";
      ApplyPendingFile();
      return;
    }
    StatusText.Text = $"{query.Length:N0} shown in {scope}. {directory.Children.Length:N0} subfolders here. {catalog.FileCount:N0} files in {catalog.Archives.Count} archives.{hidden}{skipped}";
    ApplyPendingFile();
  }

  private async void FileSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    UpdateExportButton();
    await PreviewSelectionAsync();
  }

  private async void MipChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressMip || _texture is null)
      return;
    await ShowTextureAsync(_texture);
  }

  private async Task PreviewSelectionAsync()
  {
    if (_catalog is null || _store is null)
      return;
    if (FileList.SelectedItem is not FileRow row)
      return;

    var generation = ++_previewGeneration;
    var file = _catalog.GetFile(row.Index);
    _recent.Touch(file.Path, file.ArchiveName);
    ShowLoading();
    _playTimer.Stop();
    StopEffect();
    _texture = null;
    _clip = null;
    if (file.Kind != AssetKind.Animation)
    {
      _mesh = null;
      _previewFileIndex = -1;
      AnimBar.IsVisible = false;
      if (file.Kind != AssetKind.Model)
        MeshList.ItemsSource = null;
    }
    SkeletonToggle.IsVisible = false;
    WireToggle.IsVisible = false;
    LodBox.IsVisible = false;
    LodLabel.IsVisible = false;
    PartBox.IsVisible = false;
    PartLabel.IsVisible = false;
    PlayButton.IsVisible = false;
    _albedo = null;
    _normals = null;
    _specular = null;
    PreviewImage.IsVisible = false;
    GpuView.IsVisible = false;
    GpuView.Continuous = false;
    GpuToggle.IsVisible = false;
    PreviewText.IsVisible = true;
    PreviewText.Text = "Reading " + file.Path + "…";
    PreviewNote.Text = "";

    try
    {
      if (file.Kind is AssetKind.Texture)
      {
        var document = await Task.Run(() => TextureLoader.Load(_catalog, _store, row.Index));
        if (generation != _previewGeneration)
          return;
        _texture = document;
        FillLevels(document.MipCount, document.FaceCount);
        await ShowTextureAsync(document);
        HideLoading(generation);
        return;
      }

      if (file.Kind is AssetKind.Model)
      {
        var loaded = await Task.Run(() =>
        {
          var decoded = TemplateMeshReader.Load(_catalog, _store, row.Index);
          return (decoded, ResolvePreview(_catalog, _store, decoded, row.Index));
        });
        if (generation != _previewGeneration)
          return;
        var mesh = loaded.decoded;
        _mesh = mesh;
        _previewFileIndex = row.Index;
        _modelKey = ModelKey(file.Path);
        SmoothToggle.IsVisible = mesh.HasTriangles;
        _albedo = loaded.Item2.Albedo;
        _normals = loaded.Item2.Normals;
        _specular = loaded.Item2.Specular;
        _distance = 0;
        _time = 0;
        _yaw = 0.7f;
        _pitch = 0.18f;
        FillModelControls(mesh);
        SkeletonToggle.IsVisible = mesh.Bones.Count > 0;
        SkeletonToggle.IsChecked = false;
        WireToggle.IsVisible = mesh.HasTriangles;
        GpuToggle.IsVisible = true;
        PreviewNote.Text = mesh.Message;
        if (mesh.Bones.Count > 0)
          _ = ListAnimationsAsync(++_animGeneration);
        else
          AnimBar.IsVisible = false;
        if (mesh.HasTriangles || mesh.Bones.Count > 0)
        {
          PreviewText.IsVisible = false;
          PreviewImage.IsVisible = true;
          DrawMesh();
        }
        else
        {
          PreviewText.Text = mesh.Message;
          HideLoading(generation);
        }

        return;
      }

      if (file.Kind is AssetKind.Animation)
      {
        var clip = await Task.Run(() => SaniReader.Load(_catalog, _store, row.Index));
        if (generation != _previewGeneration)
          return;
        _clip = clip;
        _clipIndex = row.Index;
        ApplyClipToMesh(clip);
        SkeletonToggle.IsVisible = _mesh is { Bones.Count: > 0 };
        PreviewText.Text = Describe(clip);
        if (_mesh is { Bones.Count: > 0 } && clip.HasCurves)
        {
          PreviewText.IsVisible = false;
          PreviewImage.IsVisible = true;
          DrawMesh();
        }
        PreviewNote.Text = clip.HasCurves
          ? "Play or the slider samples these curves onto the loaded skeleton. Bones this clip does not name stay on the bind pose."
          : "No sampled curves. The note above is what the reader could see.";
        HideLoading(generation);
        return;
      }

      if (file.Size > 32L * 1024 * 1024)
      {
        PreviewText.Text = $"{file.Path} is {file.SizeText}. Preview reads are capped so a multi-megabyte buffer is not pulled into the text pane. Export copies the original bytes.";
        HideLoading(generation);
        return;
      }

      if (file.Path.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase) && await TryShowEffectAsync(row.Index, file.Path, generation))
      {
        HideLoading(generation);
        return;
      }

      if (generation != _previewGeneration)
        return;
      var bytes = await Task.Run(() => _store.Read(row.Index));
      if (generation != _previewGeneration)
        return;
      if (file.Kind == AssetKind.TextureMip)
      {
        PreviewText.Text = "Loose .pct_mip payload. Open the .pct.resource that lists this file. The first bytes are below.\n\n" + BytePreview.Hex(bytes);
        HideLoading(generation);
        return;
      }

      PreviewText.Text = TextureLoader.LooksText(bytes) ? BytePreview.Text(bytes) : BytePreview.Hex(bytes);
      if (file.Kind == AssetKind.Effect)
        PreviewNote.Text = "Effects and particle systems are not converted. Export copies the raw file and any texture or model names found in it.";
      HideLoading(generation);
    }
    catch (Exception ex)
    {
      if (generation != _previewGeneration)
        return;
      PreviewText.IsVisible = true;
      PreviewImage.IsVisible = false;
      GpuView.IsVisible = false;
      PreviewText.Text = ex.Message;
      HideLoading(generation);
    }
  }

  private void ShowLoading()
    => LoadingOverlay.IsVisible = true;

  private void HideLoading(int generation)
  {
    if (generation == _previewGeneration)
      LoadingOverlay.IsVisible = false;
  }

  private async Task ShowTextureAsync(TextureDocument document)
  {
    var generation = _previewGeneration;
    var mip = MipBox.SelectedIndex < 0 ? 0 : MipBox.SelectedIndex;
    var face = FaceBox.SelectedIndex < 0 ? 0 : FaceBox.SelectedIndex;
    try
    {
      var decoded = await Task.Run(() =>
      {
        var pixels = document.Decode(mip, face, out var width, out var height, out var note);
        return (pixels, width, height, note);
      });
      if (generation != _previewGeneration)
        return;
      ShowPixels(decoded.pixels, decoded.width, decoded.height);
      PreviewText.IsVisible = false;
      PreviewImage.IsVisible = true;
      PreviewNote.Text = $"{document.Message} Showing mip {mip}, face {face}, {decoded.width}x{decoded.height}. {decoded.note}";
      HideLoading(generation);
    }
    catch (Exception ex)
    {
      if (generation != _previewGeneration)
        return;
      PreviewImage.IsVisible = false;
      PreviewText.IsVisible = true;
      PreviewText.Text = string.IsNullOrEmpty(document.Message) ? ex.Message : document.Message + "\n\n" + ex.Message;
      HideLoading(generation);
    }
  }

  private void FillLevels(int mips, int faces)
  {
    _suppressMip = true;
    MipBox.Items.Clear();
    FaceBox.Items.Clear();
    for (var i = 0; i < Math.Max(1, mips); i++)
      MipBox.Items.Add(i);
    for (var i = 0; i < Math.Max(1, faces); i++)
      FaceBox.Items.Add(i);
    MipBox.SelectedIndex = 0;
    FaceBox.SelectedIndex = 0;
    _suppressMip = false;
  }

  private void FillModelControls(ExportedMesh mesh)
  {
    _suppressView = true;
    LodBox.Items.Clear();
    LodBox.Items.Add($"LOD 0 · {mesh.Positions.Count:N0}");
    foreach (var level in mesh.Levels)
      LodBox.Items.Add($"{level.Name} · {level.Positions.Count:N0}");
    LodBox.SelectedIndex = 0;
    LodBox.IsVisible = mesh.HasTriangles;
    LodLabel.IsVisible = mesh.HasTriangles;
    FillParts(mesh, 0);
    FillMeshes(mesh);
    _suppressView = false;
  }

  private void FillMeshes(ExportedMesh mesh)
  {
    _suppressMesh = true;
    var toggles = new List<MeshToggle>();
    void Add(IEnumerable<MeshPart> parts)
    {
      foreach (var part in parts)
      {
        var toggle = new MeshToggle(part);
        toggle.Toggled += MeshToggled;
        toggles.Add(toggle);
      }
    }

    Add(mesh.Parts);
    foreach (var level in mesh.Levels)
      Add(level.Parts);
    MeshList.ItemsSource = MeshToggle.Group(toggles);
    _suppressMesh = false;
  }

  private void MeshToggled(object? sender, EventArgs e)
  {
    if (_suppressMesh || _suppressView || _mesh is null)
      return;
    if (LodBox.SelectedIndex > 0)
    {
      _suppressView = true;
      PartBox.SelectedIndex = 0;
      _suppressView = false;
      LodBox.SelectedIndex = 0;
      return;
    }

    if (PartBox.SelectedIndex > 0)
    {
      PartBox.SelectedIndex = 0;
      return;
    }

    DrawMesh(false);
  }

  private void FillParts(ExportedMesh mesh, int lod)
  {
    _suppressView = true;
    PartBox.Items.Clear();
    PartBox.Items.Add("All parts");
    var parts = lod <= 0 ? mesh.Parts : (uint)(lod - 1) < (uint)mesh.Levels.Count ? mesh.Levels[lod - 1].Parts : mesh.Parts;
    foreach (var part in parts)
      PartBox.Items.Add($"{part.Name} ({part.IndexCount / 3:N0})");
    PartBox.SelectedIndex = 0;
    var show = mesh.HasTriangles && parts.Count > 0;
    PartBox.IsVisible = show;
    PartLabel.IsVisible = show;
    _suppressView = false;
  }

  private void LodChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressView || _mesh is null || LodBox.SelectedIndex < 0)
      return;
    FillParts(_mesh, LodBox.SelectedIndex);
    DrawMesh(false);
  }

  private void DrawMesh()
    => DrawMesh(false);

  private void DrawMesh(bool fast)
  {
    if (_mesh is null)
      return;
    if (UseGpu())
    {
      DrawGpu(fast);
      return;
    }

    _drawFast = fast;
    _drawAgain = true;
    if (_drawBusy)
      return;
    _drawBusy = true;
    PumpDraw();
  }

  private void PumpDraw()
  {
    if (_mesh is null)
    {
      _drawBusy = false;
      _drawAgain = false;
      return;
    }

    _drawAgain = false;
    var generation = ++_drawGeneration;
    var mesh = _mesh;
    var fast = _drawFast;
    var yaw = _yaw;
    var pitch = _pitch;
    var distance = _distance;
    var time = _time;
    var skeleton = SkeletonToggle.IsChecked == true;
    var wire = WireToggle.IsChecked == true;
    var lod = LodBox.SelectedIndex < 0 ? 0 : LodBox.SelectedIndex;
    var part = PartBox.SelectedIndex <= 0 ? -1 : PartBox.SelectedIndex - 1;
    var albedo = _albedo;
    var normals = _normals;
    var specular = _specular;
    var previewGeneration = _previewGeneration;
    var inPlace = InPlaceToggle.IsChecked == true;
    var bounds = ViewBorder.Bounds;
    var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
    // Render at physical pixels so the image is not stretched (stretching magnified every aliased texel).
    var width = bounds.Width < 64 ? 720 : (int)(bounds.Width * scaling);
    var height = bounds.Height < 64 ? 900 : (int)(bounds.Height * scaling);
    width = Math.Clamp(width, 32, 2048);
    height = Math.Clamp(height, 32, 2048);
    var moving = fast || _playTimer.IsEnabled;
    var supersample = !moving && SmoothToggle.IsChecked == true && (long)width * height <= 2_400_000 ? 2 : 1;
    Task.Run(() =>
    {
      var pixels = MeshPreview.Render(
        mesh,
        width,
        height,
        new OrbitCamera { Yaw = yaw, Pitch = pitch, Distance = distance },
        skeleton,
        time,
        out var note,
        new PreviewSettings
        {
          Lod = lod, Part = part, Wireframe = wire, Albedo = albedo, Normals = normals, Specular = specular,
          MaxEdge = 2048, Supersample = supersample, InPlace = inPlace,
        });
      return (pixels, width, height, note, MeshPreview.LastRenderMs, supersample);
    }).ContinueWith(task =>
    {
      Dispatcher.UIThread.Post(() =>
      {
        if (generation == _drawGeneration && task.Status == TaskStatus.RanToCompletion)
        {
          ShowPixels(task.Result.pixels, task.Result.width, task.Result.height);
          if (_texture is null && !UseGpu())
          {
            GpuView.IsVisible = false;
            PreviewImage.IsVisible = true;
          }

          UpdateStats(task.Result.Item5, task.Result.width, task.Result.height, task.Result.supersample);
          if (!string.IsNullOrEmpty(task.Result.note))
            PreviewNote.Text = task.Result.note;
          HideLoading(previewGeneration);
        }
        else if (generation == _drawGeneration && task.IsFaulted)
        {
          PreviewNote.Text = task.Exception?.GetBaseException().Message ?? "Preview failed.";
          HideLoading(previewGeneration);
        }

        if (_drawAgain && _mesh is not null)
          PumpDraw();
        else
          _drawBusy = false;
      });
    });
  }

  internal readonly record struct PreviewTextures(
    Dictionary<string, PreviewImage> Albedo,
    Dictionary<string, PreviewImage> Normals,
    Dictionary<string, PreviewImage> Specular);

  internal static PreviewTextures ResolvePreview(GameCatalog catalog, ArchiveStore store, ExportedMesh mesh, int hint)
  {
    var albedo = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    var normals = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    var specular = new Dictionary<string, PreviewImage>(StringComparer.OrdinalIgnoreCase);
    var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    void Take(string? name, string channels)
    {
      if (!string.IsNullOrWhiteSpace(name))
        wanted[name] = channels;
    }

    void TakePart(MeshPart part)
    {
      Take(part.Texture, "");
      Take(part.Normal, "Normalmap(rgb)");
      Take(part.Specular, part.SpecularChannels);
      if (MaterialMaps.TrySolid(part.Texture ?? "", out var r, out var g, out var b, out var a)
        && !string.IsNullOrWhiteSpace(part.Texture)
        && !albedo.ContainsKey(part.Texture))
      {
        albedo[part.Texture] = new PreviewImage
        {
          Width = 1,
          Height = 1,
          Bgra = new[] { b, g, r, a },
        };
      }
    }

    foreach (var part in mesh.Parts)
      TakePart(part);
    foreach (var level in mesh.Levels)
    {
      foreach (var part in level.Parts)
        TakePart(part);
    }

    foreach (var pair in wanted)
    {
      var index = TemplateMeshReader.ResolveTexture(catalog, hint, pair.Key);
      if (index < 0)
        continue;
      try
      {
        var document = TextureLoader.Load(catalog, store, index);
        if (!document.CanDecode)
          continue;
        var pixels = document.Decode(0, 0, out var width, out var height, out _);
        var image = new PreviewImage { Width = width, Height = height, Bgra = pixels, Channels = pair.Value };
        if (pair.Value.Contains("Normal", StringComparison.OrdinalIgnoreCase))
          normals[pair.Key] = image;
        else if (pair.Value.Contains("Metal", StringComparison.OrdinalIgnoreCase)
          || pair.Value.Contains("Rough", StringComparison.OrdinalIgnoreCase)
          || pair.Value.Contains("MetGray", StringComparison.OrdinalIgnoreCase))
          specular[pair.Key] = image;
        else
          albedo[pair.Key] = image;
      }
      catch (Exception)
      {
        // A missing or unsupported texture leaves that part as a lit solid.
      }
    }

    return new PreviewTextures(albedo, normals, specular);
  }

  private void ShowPixels(byte[] pixels, int width, int height)
  {
    // Two reused bitmaps: swapping the Source makes the Image redraw without a fresh allocation per frame.
    _frameFlip ^= 1;
    var bitmap = _frames[_frameFlip];
    if (bitmap is null || bitmap.PixelSize.Width != width || bitmap.PixelSize.Height != height)
    {
      bitmap?.Dispose();
      bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
      _frames[_frameFlip] = bitmap;
    }

    using (var frame = bitmap.Lock())
    {
      for (var y = 0; y < height; y++)
      {
        System.Runtime.InteropServices.Marshal.Copy(pixels, y * width * 4, frame.Address + y * frame.RowBytes, width * 4);
      }
    }

    PreviewImage.Source = bitmap;
    PreviewImage.InvalidateVisual();
  }

  // ---- GPU preview -------------------------------------------------------------------------------------------

  private bool _gpuHooked;
  private DispatcherTimer? _gpuWatchdog;

  private bool UseGpu()
    => GpuToggle.IsChecked == true && !GpuView.HasFailed && _texture is null;

  private void DrawGpu(bool fast)
  {
    if (_mesh is null)
      return;
    if (!_gpuHooked)
    {
      _gpuHooked = true;
      GpuView.Failed += reason => FallBackToCpu(reason);
      GpuView.Rendered += stats =>
      {
        HideLoading(_previewGeneration);
        _gpuWatchdog?.Stop();
        var fps = GpuView.Continuous || _dragging ? $" · {stats.Fps:0} fps" : "";
        var upload = stats.UploadMs > 1 ? $" · upload {stats.UploadMs:0} ms" : "";
        RenderStats.Text = $"GPU {stats.Width}x{stats.Height} MSAA {stats.Samples}x · {stats.CpuMs:0.0} ms{fps}{upload}";
        ToolTip.SetTip(RenderStats, stats.Info);
      };
    }

    var playing = _playTimer.IsEnabled && _clip is not null;
    var clip = _clip;
    var request = new GpuFrameRequest(
      _mesh,
      new OrbitCamera { Yaw = _yaw, Pitch = _pitch, Distance = _distance },
      SkeletonToggle.IsChecked == true,
      WireToggle.IsChecked == true,
      _time,
      new PreviewSettings
      {
        Lod = LodBox.SelectedIndex < 0 ? 0 : LodBox.SelectedIndex,
        Part = PartBox.SelectedIndex <= 0 ? -1 : PartBox.SelectedIndex - 1,
        Wireframe = WireToggle.IsChecked == true,
        Albedo = _albedo, Normals = _normals, Specular = _specular,
        MaxEdge = 4096, Supersample = 1, InPlace = InPlaceToggle.IsChecked == true,
        Physics = PhysicsToggle.IsChecked == true,
      },
      // While playing, each presented frame samples the clip at its own time (not at the 16 ms timer's).
      playing ? () =>
      {
        var t = _time + (float)Math.Clamp(_playClock.Elapsed.TotalSeconds - _playLast, 0, 0.1);
        return clip!.Duration > 0 && t > clip.Duration ? 0 : t;
      } : null,
      HostVfxOverlay());
    GpuView.Continuous = playing || HostVfxActive;
    PreviewImage.IsVisible = false;
    GpuView.IsVisible = true;
    SmoothToggle.IsEnabled = false;
    GpuView.Show(request);
    if (GpuView.FramesRendered == 0)
    {
      // No GL context (software compositor, blocked driver): Avalonia then never calls the GL callbacks.
      _gpuWatchdog ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
      _gpuWatchdog.Tick -= GpuWatchdogTick;
      _gpuWatchdog.Tick += GpuWatchdogTick;
      if (!_gpuWatchdog.IsEnabled)
        _gpuWatchdog.Start();
    }
  }

  private void GpuWatchdogTick(object? sender, EventArgs e)
  {
    _gpuWatchdog?.Stop();
    if (GpuView.FramesRendered == 0 && GpuView.IsVisible && UseGpu())
      FallBackToCpu(GpuView.InitAttempted
        ? "GPU preview produced no frame."
        : "No OpenGL context (software rendering).");
  }

  private void FallBackToCpu(string reason)
  {
    _gpuWatchdog?.Stop();
    GpuToggle.IsChecked = false;
    GpuView.Continuous = false;
    GpuView.IsVisible = false;
    SmoothToggle.IsEnabled = true;
    PreviewNote.Text = reason + " Switched to the CPU preview.";
    if (_mesh is not null && _texture is null)
    {
      PreviewImage.IsVisible = true;
      DrawMesh(false);
    }
  }

  private void GpuToggled(object? sender, RoutedEventArgs e)
  {
    if (GpuToggle.IsChecked == true && GpuView.HasFailed)
    {
      GpuToggle.IsChecked = false;
      PreviewNote.Text = "GPU preview is unavailable: " + GpuView.FailReason;
      return;
    }

    if (GpuToggle.IsChecked != true)
    {
      GpuView.Continuous = false;
      GpuView.IsVisible = false;
      SmoothToggle.IsEnabled = true;
      if (_mesh is not null && _texture is null)
        PreviewImage.IsVisible = true;
    }

    DrawMesh(false);
  }

  private void UpdateStats(double renderMs, int width, int height, int supersample)
  {
    _fpsFrames++;
    var now = _fpsClock.Elapsed.TotalSeconds;
    if (now - _fpsStart >= 0.5)
    {
      _fps = _fpsFrames / (now - _fpsStart);
      _fpsFrames = 0;
      _fpsStart = now;
    }

    var mode = supersample > 1 ? $" {supersample}x{supersample} AA" : "";
    RenderStats.Text = _playTimer.IsEnabled || _dragging
      ? $"{width}x{height}{mode} · {renderMs:0} ms · {_fps:0} fps"
      : $"{width}x{height}{mode} · {renderMs:0} ms";
  }

  private static string Describe(AnimationClip clip)
  {
    var lines = new List<string>();
    lines.Add(clip.Name);
    if (clip.Sequences.Count > 0)
      lines.AddRange(clip.Sequences);
    lines.AddRange(clip.Notes);
    lines.Add($"{clip.Tracks.Count} tracks, {clip.Duration:0.###}s");
    return string.Join(Environment.NewLine, lines);
  }

  private void ViewChanged(object? sender, RoutedEventArgs e)
  {
    if (_suppressView)
      return;
    DrawMesh(false);
  }

  private void SpaceKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None)
      return;
    var focused = FocusManager?.GetFocusedElement();
    if (focused is TextBox || e.Source is TextBox)
      return;
    if (focused is ComboBox { IsDropDownOpen: true } || focused is ComboBoxItem)
      return;
    if (_fx is not null)
    {
      e.Handled = true;
      VfxTogglePlay();
      return;
    }

    if (_mesh is null || _clip is null || !_clip.HasCurves || !PlayButton.IsVisible)
      return;
    e.Handled = true; // and the focused button does not also get clicked
    PlayClicked(PlayButton, new RoutedEventArgs());
  }

  private void PlayClicked(object? sender, RoutedEventArgs e)
  {
    if (_fx is not null)
    {
      VfxTogglePlay();
      return;
    }

    if (_playTimer.IsEnabled)
    {
      _playTimer.Stop();
      PlayButton.Content = "Play";
      DrawMesh();
      return;
    }

    if (_mesh is null || _clip is null || !_clip.HasCurves)
    {
      PreviewNote.Text = "Select a model, then an animation that shares its bone names.";
      return;
    }

    _mesh.Clip = _clip;
    _playLast = _playClock.Elapsed.TotalSeconds;
    _playTimer.Start();
    PlayButton.Content = "Pause";
  }

  private async Task ListAnimationsAsync(int generation)
  {
    if (_catalog is null || _store is null || _mesh is null || _mesh.Bones.Count == 0)
      return;
    var catalog = _catalog;
    var store = _store;
    var names = _mesh.Bones.Select(bone => bone.Name).ToArray();
    ResetClip(bumpGeneration: false);
    var key = _modelKey;
    AnimBar.IsVisible = true;
    _suppressAnim = true;
    AnimBox.ItemsSource = new[] { new AnimChoice { Label = "Finding animations…", Index = -1 } };
    AnimBox.SelectedIndex = 0;
    _suppressAnim = false;
    var modelPath = _previewFileIndex >= 0 ? catalog.GetFile(_previewFileIndex).Path : key;
    var physicsMesh = _mesh;
    var (choices, owner) = await Task.Run(() =>
    {
      _clipHeaders ??= SaniIndex.ReadHeaders(catalog, store);
      if (physicsMesh.Physics is null && physicsMesh.Bones.Count > 0)
      {
        try
        {
          physicsMesh.Physics = PhysicsLocator.Find(catalog, store, modelPath) ?? new PhysicsSetup();
        }
        catch (Exception exception)
        {
          physicsMesh.Physics = new PhysicsSetup();
          physicsMesh.Physics.Notes.Add("class scan failed: " + exception.Message);
        }
      }

      CharacterAnimations.Result owner;
      try
      {
        owner = CharacterAnimations.Resolve(catalog, store, modelPath);
      }
      catch (Exception exception)
      {
        owner = new CharacterAnimations.Result(new HashSet<string>(), new[] { CharacterAnimations.ModelToken(modelPath) }, Array.Empty<string>(), Array.Empty<string>(), "class scan failed: " + exception.Message);
      }

      var rows = new List<AnimChoice>();
      foreach (var header in SaniIndex.Compatible(_clipHeaders, names))
      {
        var file = Path.GetFileName(header.Path);
        var rank = owner.Rank(header.Path);
        var mark = rank switch { ClipRank.Main => "★ ", ClipRank.Cinematic => "☆ ", _ => "" };
        rows.Add(new AnimChoice
        {
          Label = $"{mark}{file}  ({header.Duration:0.00}s)", Index = header.Index, Path = header.Path,
          Rank = rank, SameModel = owner.Named(header.Path),
        });
      }

      // ★ this character's gameplay set, ☆ its cinematic clips, then the rest (clips named after it first), each by path.
      rows.Sort((a, b) =>
        a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank)
        : a.SameModel != b.SameModel ? (a.SameModel ? -1 : 1)
        : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
      return (rows, owner);
    });
    if (generation != _animGeneration || _mesh is null)
      return;
    _animAll = choices;
    ApplyAnimFilter(keepSelection: false);
    if (physicsMesh.Physics is { IsEmpty: false } physics)
    {
      PhysicsToggle.IsVisible = true;
      var runtime = MeshPreview.PhysicsFor(physicsMesh);
      ToolTip.SetTip(PhysicsToggle,
        $"{physics.Cloths.Count} cloth(s), {physics.Jiggles.Count} jiggle bone(s) from {physics.SourceClass}" +
        (runtime is { Missing.Count: > 0 } ? $"\nnot in this model: {string.Join(", ", runtime.Missing)}" : "") +
        "\nGPU preview only; resets on clip change or scrub.");
    }
    else
      PhysicsToggle.IsVisible = false;
    var main = choices.Count(c => c.Rank == ClipRank.Main);
    var cinematic = choices.Count(c => c.Rank == ClipRank.Cinematic);
    StatusText.Text = choices.Count == 0
      ? "No animation shares 8 or more bone names with this model."
      : $"{choices.Count:N0} animations share bone names with this model · ★ {main:N0} main · ☆ {cinematic:N0} cinematic ('{string.Join("/", owner.Tokens)}') · {owner.Source}";
    ToolTip.SetTip(StatusText, owner.Classes.Count == 0 ? owner.Source : string.Join("\n", owner.Classes.Take(20)));
  }

  private async void AnimChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressAnim || _catalog is null || _store is null || _mesh is null)
      return;
    if (AnimBox.SelectedItem is not AnimChoice choice)
      return;
    _playTimer.Stop();
    PlayButton.Content = "Play";
    if (choice.Index < 0)
    {
      ResetClip();
      PreviewNote.Text = "Bind pose.";
      DrawMesh();
      return;
    }

    var generation = ++_animGeneration;
    var index = choice.Index;
    var clip = await Task.Run(() => SaniReader.Load(_catalog, _store, index));
    if (generation != _animGeneration || _mesh is null)
      return;
    _clip = clip;
    _clipIndex = index;
    ApplyClipToMesh(clip);
    PreviewNote.Text = clip.HasCurves
      ? $"{clip.Name}: {clip.Tracks.Count} tracks, {clip.Duration:0.###}s. Bones this clip does not name stay on the bind pose."
      : (clip.Notes.Count == 0 ? "This clip has no curve keys." : clip.Notes[0]);
    DrawMesh();
  }

  /// <summary>Back to the bind pose: no clip, time zero, slider and label reset, pending clip loads ignored.</summary>
  private void ResetClip(bool bumpGeneration = true)
  {
    _playTimer.Stop();
    PlayButton.Content = "Play";
    PlayButton.IsVisible = false;
    if (bumpGeneration)
      ++_animGeneration;
    _clip = null;
    _clipIndex = -1;
    if (_mesh is not null)
      _mesh.Clip = null;
    _time = 0;
    ScrubSlider.IsEnabled = false;
    SyncScrub();
  }

  private void AnimFilterChanged(object? sender, TextChangedEventArgs e)
    => ApplyAnimFilter(keepSelection: true);

  /// <summary>Live substring filter (case-insensitive) over the compatible clips; Bind pose always stays first.</summary>
  private void ApplyAnimFilter(bool keepSelection)
  {
    var text = AnimFilter.Text?.Trim() ?? "";
    var selected = keepSelection ? AnimBox.SelectedItem as AnimChoice : null;
    var rows = new List<AnimChoice> { BindChoice };
    foreach (var choice in _animAll)
    {
      if (text.Length == 0 || choice.Path.Contains(text, StringComparison.OrdinalIgnoreCase))
        rows.Add(choice);
    }

    _suppressAnim = true;
    AnimBox.ItemsSource = rows;
    AnimBox.SelectedIndex = selected is not null && rows.Contains(selected) ? rows.IndexOf(selected) : 0;
    _suppressAnim = false;
    if (text.Length > 0)
      StatusText.Text = $"{rows.Count - 1:N0} of {_animAll.Count:N0} animations match '{text}'.";
  }

  private static readonly AnimChoice BindChoice = new() { Label = "Bind pose", Index = -1 };

  /// <summary>The model's own name, for example <c>titus</c> from <c>cc_titus.tpl</c>.</summary>
  private static string ModelKey(string path)
  {
    var stem = Path.GetFileNameWithoutExtension(path);
    var tokens = stem.Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries)
      .Where(token => token.Length >= 3 && !token.All(char.IsDigit))
      .ToArray();
    return tokens.Length == 0 ? stem : tokens.OrderByDescending(token => token.Length).First();
  }

  private void ApplyClipToMesh(AnimationClip clip)
  {
    _time = 0;
    PlayButton.IsVisible = clip.HasCurves && _mesh is { Bones.Count: > 0 };
    ScrubSlider.IsEnabled = clip.HasCurves;
    if (_mesh is not null)
      _mesh.Clip = clip.HasCurves ? clip : null;
    SyncScrub();
  }

  private void ScrubChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
  {
    if (_suppressScrub || _clip is null || !_clip.HasCurves)
      return;
    _time = (float)ScrubSlider.Value;
    ScrubLabel.Text = $"{_time:0.00} / {_clip.Duration:0.00}s";
    DrawMesh();
  }

  private void SyncScrub()
  {
    _suppressScrub = true;
    if (_clip is { HasCurves: true, Duration: > 0 })
    {
      ScrubSlider.Maximum = _clip.Duration;
      ScrubSlider.Value = Math.Clamp(_time, 0, _clip.Duration);
      ScrubLabel.Text = $"{_time:0.00} / {_clip.Duration:0.00}s";
      ScrubSlider.IsEnabled = true;
    }
    else
    {
      ScrubSlider.Value = 0;
      ScrubLabel.Text = "0.00s";
    }

    _suppressScrub = false;
  }

  private void ViewPressed(object? sender, PointerPressedEventArgs e)
  {
    _dragging = true;
    _lastPoint = e.GetPosition(ViewBorder);
  }

  private void ViewReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (!_dragging)
      return;
    _dragging = false;
    DrawMesh(false);
  }

  private void ViewMoved(object? sender, PointerEventArgs e)
  {
    if (_dragging && _fx is not null)
    {
      var p = e.GetPosition(ViewBorder);
      VfxPointerMoved(p - _lastPoint);
      _lastPoint = p;
      return;
    }

    if (!_dragging || _mesh is null)
      return;
    var point = e.GetPosition(ViewBorder);
    _yaw += (float)(point.X - _lastPoint.X) * 0.01f;
    _pitch += (float)(point.Y - _lastPoint.Y) * 0.01f;
    _lastPoint = point;
    DrawMesh(true);
  }

  private void ViewWheel(object? sender, PointerWheelEventArgs e)
  {
    if (VfxWheel(e))
      return;
    if (_mesh is null)
      return;
    if (_distance <= 0 && _mesh is not null)
    {
      var bounds = ViewBorder.Bounds;
      var aspect = bounds.Height < 32 ? 0.8f : (float)(bounds.Width / Math.Max(1, bounds.Height));
      var lod = LodBox.SelectedIndex < 0 ? 0 : LodBox.SelectedIndex;
      var part = PartBox.SelectedIndex <= 0 ? -1 : PartBox.SelectedIndex - 1;
      _distance = MeshPreview.FitDistance(_mesh, aspect, lod, part);
    }

    var step = _distance * 0.1f;
    _distance = Math.Max(0.15f, _distance - (float)e.Delta.Y * step);
    DrawMesh(true);
  }

  private void FilesTabClicked(object? sender, RoutedEventArgs e)
    => ShowFiles();

  private void RecentTabClicked(object? sender, RoutedEventArgs e)
    => ShowRecent();

  private void ShowFiles()
  {
    FileList.IsVisible = true;
    FileHeader.IsVisible = true;
    RecentList.IsVisible = false;
    FilesTab.FontWeight = FontWeight.SemiBold;
    RecentTab.FontWeight = FontWeight.Normal;
  }

  private void ShowRecent()
  {
    FileList.IsVisible = false;
    FileHeader.IsVisible = false;
    RecentList.IsVisible = true;
    FilesTab.FontWeight = FontWeight.Normal;
    RecentTab.FontWeight = FontWeight.SemiBold;
  }

  private void RecentSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressRecent || RecentList.SelectedItem is not RecentEntry entry)
      return;
    _suppressRecent = true;
    RecentList.SelectedItem = null;
    _suppressRecent = false;
    OpenRecent(entry);
  }

  private void OpenRecent(RecentEntry entry)
  {
    ShowFiles();
    if (_catalog is null || _tree is null)
    {
      StatusText.Text = "Open the game folder, then choose the recent file again.";
      return;
    }

    var hits = _catalog.FindByPath(entry.Path);
    var index = -1;
    foreach (var hit in hits)
    {
      if (string.Equals(_catalog.GetFile(hit).ArchiveName, entry.Archive, StringComparison.OrdinalIgnoreCase))
      {
        index = hit;
        break;
      }
    }

    if (index < 0 && hits.Count > 0)
      index = hits[0];
    if (index < 0)
    {
      StatusText.Text = "That recent file is not in the open folder: " + entry.Path;
      return;
    }

    var directory = ParentPath(entry.Path);
    _pendingFileIndex = index;
    _pendingDirectory = directory;
    _suppressSearch = true;
    SearchBox.Text = "";
    _suppressSearch = false;
    _searchTimer.Stop();
    if (FilterBox.SelectedIndex != 0)
      FilterBox.SelectedIndex = 0;
    if (!TrySelectDirectory(directory))
    {
      _pendingFileIndex = -1;
      StatusText.Text = "That recent file's folder is not in the open catalog: " + entry.Path;
    }
  }

  private bool TrySelectDirectory(string directoryPath)
  {
    if (_catalog is null || _tree is null)
      return false;

    var indices = new List<int> { 0 };
    var node = _catalog.Root;
    if (directoryPath.Length > 0)
    {
      var start = 0;
      for (var i = 0; i <= directoryPath.Length; i++)
      {
        if (i != directoryPath.Length && directoryPath[i] != '/')
          continue;
        var segment = directoryPath.Substring(start, i - start);
        start = i + 1;
        if (segment.Length == 0 || segment == ".")
          continue;
        var found = -1;
        for (var child = 0; child < node.Children.Length; child++)
        {
          if (string.Equals(node.Children[child].Name, segment, StringComparison.OrdinalIgnoreCase))
          {
            found = child;
            break;
          }
        }

        if (found < 0)
          return false;
        indices.Add(found);
        node = node.Children[found];
      }
    }

    for (var depth = 1; depth < indices.Count; depth++)
      _tree.Expand(new IndexPath(indices.GetRange(0, depth)));
    var already = _directory is not null && string.Equals(_directory.Path, node.Path, StringComparison.OrdinalIgnoreCase);
    _tree.RowSelection!.Select(new IndexPath(indices));
    if (already)
      RefreshFiles();
    return true;
  }

  private void ApplyPendingFile()
  {
    if (_pendingFileIndex < 0 || FileList.ItemsSource is not FileRowList rows)
      return;
    if (_directory is null || !string.Equals(_directory.Path, _pendingDirectory, StringComparison.OrdinalIgnoreCase))
      return;
    var pending = _pendingFileIndex;
    _pendingFileIndex = -1;
    var row = rows.RowForCatalogIndex(pending);
    if (row < 0)
    {
      StatusText.Text = "The recent file is not visible in the current list.";
      return;
    }

    FileList.SelectedIndex = row;
    FileList.ScrollIntoView(rows[row]);
  }

  private static string ParentPath(string filePath)
  {
    var normalized = filePath.Replace('\\', '/');
    var slash = normalized.LastIndexOf('/');
    return slash <= 0 ? "" : normalized[..slash];
  }

  private async void ExportClicked(object? sender, RoutedEventArgs e)
  {
    if (_catalog is null || _store is null)
      return;
    var indices = FileList.SelectedItems?.OfType<FileRow>().Select(row => row.Index).Distinct().ToArray() ?? Array.Empty<int>();
    if (indices.Length == 0)
    {
      StatusText.Text = "Select one or more files in the list first.";
      return;
    }

    var window = new ExportWindow();
    var choice = await window.Choose(this, _settings.LastExportFolder);
    if (choice is null)
      return;
    _settings.LastExportFolder = choice.Folder;
    _settings.Save();
    if (_mesh is not null && _previewFileIndex >= 0 && indices.Contains(_previewFileIndex))
      choice.Settings.PartVisibility = new Dictionary<int, MeshVisibility> { [_previewFileIndex] = MeshVisibility.Capture(_mesh) };

    _exporting = true;
    UpdateExportButton();
    try
    {
      var progress = new Progress<string>(path => StatusText.Text = "Exporting " + path);
      var catalog = _catalog;
      var store = _store;
      var previewClip = _clip is { HasCurves: true } ? _clipIndex : -1;
      var notes = new List<string>();
      var report = await Task.Run(() =>
      {
        var files = ExportPlan.Apply(catalog, store, indices, choice.Settings, choice.Clips, previewClip, notes);
        return AssetExporter.Export(catalog, store, files, choice.Folder, choice.Settings, progress);
      });
      report.Messages.InsertRange(0, notes);
      StatusText.Text = $"Exported {report.Written} files, skipped {report.Skipped}. {string.Join(" ", report.Messages.Take(3))}";
    }
    catch (Exception ex)
    {
      StatusText.Text = ex.Message;
    }
    finally
    {
      _exporting = false;
      UpdateExportButton();
    }
  }

  protected override void OnClosed(EventArgs e)
  {
    _load?.Cancel();
    _store?.Dispose();
    base.OnClosed(e);
  }

}

public sealed class AnimChoice
{
  public string Label { get; init; } = "";
  public int Index { get; init; } = -1;
  public string Path { get; init; } = "";
  public bool SameModel { get; init; }
  public ClipRank Rank { get; init; } = ClipRank.Other;
  public override string ToString() => Label;
}
