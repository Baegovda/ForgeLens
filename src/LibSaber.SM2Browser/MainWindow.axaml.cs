using LibSaber.SM2.Assets.Customization;
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
  /// <summary>Level shown in the viewer (R54), or null for a single model.</summary>
  private LibSaber.SM2Browser.Level.LevelScene? _level;
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
    InitContext();
    InitPropAnim();
    RecentList.ItemsSource = _recent.Items;
    InitLayoutMemory();
    ShowAllBox.IsChecked = _settings.ShowAllFiles;
    RestoreFilterState();
    InitContentBrowser();
    InitKitbash();
    // ui-43: the wheel over a CLOSED clip / LOD / part box steps to the previous (wheel up) or next (wheel down) row of
    // the current (filtered) list and loads it like a click would (WheelComboBox; the dropdown stays closed). A clip
    // chosen with the wheel also starts playing.
    AnimBox.WheelStepping += (_, _) => _playOnLoad = true;
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
    InitFly();
    InitHud();
    Opened += async (_, _) =>
    {
      if (!string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
        await LoadFolderAsync(_settings.LastFolder);
      await OpenLooseEffectFromEnvironmentAsync();
      await OpenLooseModelFromEnvironmentAsync();
      await OpenCatalogFileFromEnvironmentAsync();
      await SelectFromEnvironmentAsync();
    };
    if (Environment.GetEnvironmentVariable("SM2_SELECT") is { Length: > 0 })
      ShowActivated = false;
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
      ContentBrowserCatalogChanged(catalog, _store);
      BindTree(catalog);
      ContentBrowserTreeBound();
      KitbashCatalogChanged();
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
      if (!_scopeRestorePending)
        _scope = Scope.Folder;
      RefreshFiles();
    };
    source.RowExpanded += (_, e) => _expanded.Add(e.Row.Model.Path);
    source.RowCollapsed += (_, e) => _expanded.Remove(e.Row.Model.Path);
    FolderTree.Source = source;
    _tree = source;
    _directory = catalog.Root;
    var root = new IndexPath(0);
    source.Expand(root);
    if (RestoreTreeState(catalog))
      return;
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
        if (_gridView)
          TileGrid.Focus();
        else
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
    // mtl1-42: F (no modifier) re-frames the effect preview, unless a text box has focus.
    if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.None && _fx is not null
        && FocusManager?.GetFocusedElement() is not TextBox && e.Source is not TextBox)
    {
      if (VfxFit()) e.Handled = true;
      return;
    }
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
    if (ChipAll is not null)
      SyncChips();
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
    if (_catalog is null || _scopeRestorePending)
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
    var auxHidden = 0;
    string scopeLabel = "";
    try
    {
      // ui-43: a category or collection lists its own files (search and the type chips narrow them).
      if (_scope != Scope.Folder)
      {
        var showAllFiles = ShowAllBox?.IsChecked == true;
        var picked = await Task.Run(() => ScopeFiles(catalog, filter, search, showAllFiles, out var label) is { } arr ? (arr, label) : (null, ""));
        if (picked.arr is null)
        {
          _scope = Scope.Folder;
          query = await Task.Run(() => catalog.Query(scoped, search, filter, includeSubfolders && scoped is not null, ShowAllBox?.IsChecked != true && filter == AssetFilter.All, out auxHidden));
        }
        else
        {
          query = new FileQuery(0, picked.arr.Length, picked.arr);
          scopeLabel = picked.label;
        }
      }
      else
      {
        // mtl1-42: All types lists only previewable assets unless Show all files is on; an explicit type filter lists the whole type.
        var previewableOnly = ShowAllBox?.IsChecked != true && filter == AssetFilter.All;
        query = await Task.Run(() => catalog.Query(scoped, search, filter, includeSubfolders && scoped is not null, previewableOnly, out auxHidden));
      }
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
    _anchor = -1;
    UpdateScopeText(scopeLabel);
    RebuildGrid();
    EmptyText.Text = query.Length > 0 ? ""
      : !string.IsNullOrWhiteSpace(search) ? $"Nothing here matches '{search.Trim()}'."
      : _scope == Scope.Collection ? "This collection is empty. Drag tiles onto it, or right-click › Add to collection."
      : "No assets of this type here.";
    EmptyText.IsVisible = query.Length == 0 && !RecentList.IsVisible;
    UpdateExportButton();
    if (_scope != Scope.Folder)
    {
      StatusText.Text = $"{query.Length:N0} shown in {scopeLabel}. {catalog.FileCount:N0} files in {catalog.Archives.Count} archives."
                        + (_scope == Scope.Category && _category?.Heuristic == true ? " This group is HEURISTIC (name / keyword rule)." : "");
      ApplyPendingFile();
      return;
    }
    var scope = scoped is null
      ? "search"
      : includeSubfolders ? directory.Name + " and subfolders" : directory.Name;
    var hidden = catalog.DuplicatesCollapsed == 0
      ? ""
      : $" {catalog.DuplicatesCollapsed:N0} duplicate paths hidden; the later archive is shown.";
    var skipped = catalog.ArchivesSkipped == 0 ? "" : $" {catalog.ArchivesSkipped} caches skipped.";
    var aux = auxHidden == 0 ? "" : $" {auxHidden:N0} auxiliary files hidden (Show all files).";
    if (query.Length == 0 && !string.IsNullOrWhiteSpace(search))
    {
      StatusText.Text = auxHidden > 0
        ? $"No previewable asset matches '{search.Trim()}'; {auxHidden:N0} auxiliary files do (tick Show all files)."
        : $"Nothing matches '{search.Trim()}'. Try fewer words, or set the type filter to All types.";
      ApplyPendingFile();
      return;
    }
    StatusText.Text = $"{query.Length:N0} shown in {scope}. {directory.Children.Length:N0} subfolders here. {catalog.FileCount:N0} files in {catalog.Archives.Count} archives.{aux}{hidden}{skipped}";
    ApplyPendingFile();
  }

  private async void FileSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    UpdateExportButton();
    ContentBrowser.AssetTile.RefreshAll();
    // mtl1-42: a click / arrow key previews the row it just selected (Ctrl/Shift-adding rows previews the newest one).
    _previewRow = e.AddedItems?.OfType<FileRow>().LastOrDefault();
    await PreviewSelectionAsync();
  }

  private FileRow? _previewRow;

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
    var row = _previewRow is { } added && FileList.SelectedItems?.Contains(added) == true ? added : FileList.SelectedItem as FileRow;
    _previewRow = null;
    if (row is null)
      return;
    await PreviewFileAsync(row.Index);
  }

  /// <summary>Shows one catalog file in the viewer (the file list selection, or the Kitbasher's class tpl).</summary>
  private async Task PreviewFileAsync(int index)
  {
    if (_catalog is null || _store is null)
      return;
    var generation = ++_previewGeneration;
    var file = _catalog.GetFile(index);
    _level = null;
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
      UpdateHud();
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
    ResetColours();
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
        var document = await Task.Run(() => TextureLoader.Load(_catalog, _store, index));
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
          var decoded = TemplateMeshReader.Load(_catalog, _store, index);
          return (decoded, ResolvePreview(_catalog, _store, decoded, index));
        });
        if (generation != _previewGeneration)
          return;
        var mesh = loaded.decoded;
        _mesh = mesh;
        _previewFileIndex = index;
        ContextModelLoaded(mesh);
        _modelKey = ModelKey(file.Path);
        SmoothToggle.IsVisible = mesh.HasTriangles;
        _albedo = loaded.Item2.Albedo;
        _normals = loaded.Item2.Normals;
        _specular = loaded.Item2.Specular;
        _ = LoadColoursAsync(mesh, index, file.Name, generation);
        _distance = 0;
        _time = 0;
        _yaw = 0.7f;
        _pitch = 0.18f;
        FlyReset(false);
        FillModelControls(mesh);
        KitbashModelLoaded(mesh, index);
        SkeletonToggle.IsVisible = mesh.Bones.Count > 0;
        SkeletonToggle.IsChecked = false;
        WireToggle.IsVisible = mesh.HasTriangles;
        GpuToggle.IsVisible = true;
        SetModelNote(mesh);
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
        var clip = await Task.Run(() => SaniReader.Load(_catalog, _store, index));
        if (generation != _previewGeneration)
          return;
        _clip = clip;
        _clipIndex = index;
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

      if (LibSaber.SM2Browser.Level.LevelScene.IsLevelFile(file.Path))
      {
        // R54: a level = scenes/<name>.scn/<name>.cd_list. Built from the user's game files (templates, textures, lights), cached in memory.
        PreviewText.Text = "Building level " + file.Name + " (templates, textures, lights)…";
        var catalog = _catalog; var store = _store;
        var scene = await Task.Run(() => LibSaber.SM2Browser.Level.LevelScene.Load(catalog, store, index));
        if (generation != _previewGeneration)
          return;
        _mesh = scene.Mesh;
        _level = scene;
        _previewFileIndex = index;
        _albedo = scene.Albedo; _normals = scene.Normals; _specular = scene.Specular;
        _yaw = scene.Persp.Yaw; _pitch = scene.Persp.Pitch; _distance = scene.Persp.Distance; _time = 0;
        FlyReset(true);
        WireToggle.IsVisible = true;
        PreviewNote.Text = scene.Stats;
        PreviewText.IsVisible = false;
        PreviewImage.IsVisible = true;
        DrawMesh();
        return;
      }

      if (file.Size > 32L * 1024 * 1024)
      {
        PreviewText.Text = $"{file.Path} is {file.SizeText}. Preview reads are capped so a multi-megabyte buffer is not pulled into the text pane. Export copies the original bytes.";
        HideLoading(generation);
        return;
      }

      if (file.Path.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase) && await TryShowEffectAsync(index, file.Path, generation))
      {
        HideLoading(generation);
        _ = Guard(AfterEffectShownAsync(Path.GetFileNameWithoutExtension(file.Path)));
        return;
      }

      if (generation != _previewGeneration)
        return;
      var bytes = await Task.Run(() => _store.Read(index));
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
    var fx = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)).Where(MaterialMaps.IsSfxPart).ToList();
    SfxPartsBox.IsVisible = fx.Count > 0;
    SfxPartsBox.IsChecked = fx.Count > 0 && fx.Any(part => part.Visible);
    _suppressMesh = false;
  }

  /// <summary>mtl1-51: FX parts toggle: shows / hides every part <see cref="MaterialMaps.HideSfxParts"/> hid by default.</summary>
  private void SfxPartsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (_mesh is not { } mesh)
      return;
    var show = SfxPartsBox.IsChecked == true;
    foreach (var part in mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts)))
      if (part.HideReason is "sfx" or "sfxSource")
        part.Visible = show;
    FillMeshes(mesh);
    DrawMesh(false);
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
    UpdateHud();
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
    var pivot = _pivot;
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
    var level = _level;
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
        new OrbitCamera { Yaw = yaw, Pitch = pitch, Distance = distance, Target = pivot ?? level?.Centre },
        skeleton,
        time,
        out var note,
        new PreviewSettings
        {
          Lod = lod, Part = part, Wireframe = wire, Albedo = albedo, Normals = normals, Specular = specular,
          MaxEdge = 2048, Supersample = level is null ? supersample : 1, InPlace = inPlace,
          PointLights = level?.Lights, Ambient = level is null ? 0.16f : 0.22f, HeadLight = 0.6f, Exposure = level is null ? 1f : 1.3f,
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
          // R66: the CPU renderer echoes mesh.Message; keep the compact summary (Details shows the notes)
          if (!string.IsNullOrEmpty(task.Result.note) && task.Result.note != _noteDetails)
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
      if (LoadPreviewImage(catalog, store, hint, pair.Key, pair.Value) is not { } image)
        continue;
      if (pair.Value.Contains("Normal", StringComparison.OrdinalIgnoreCase))
        normals[pair.Key] = image;
      else if (IsSpecularChannels(pair.Value))
        specular[pair.Key] = image;
      else
        albedo[pair.Key] = image;
    }

    return new PreviewTextures(albedo, normals, specular);
  }

  internal static bool IsSpecularChannels(string channels)
    => channels.Contains("Metal", StringComparison.OrdinalIgnoreCase)
      || channels.Contains("Rough", StringComparison.OrdinalIgnoreCase)
      || channels.Contains("MetGray", StringComparison.OrdinalIgnoreCase);

  /// <summary>Mip 0 of a texture by name as a preview image (null when missing / undecodable: the part draws as a lit solid).</summary>
  internal static PreviewImage? LoadPreviewImage(GameCatalog catalog, ArchiveStore store, int hint, string name, string channels)
  {
    var index = TemplateMeshReader.ResolveTexture(catalog, hint, name);
    if (index < 0)
      return null;
    try
    {
      var document = TextureLoader.Load(catalog, store, index);
      if (!document.CanDecode)
        return null;
      var pixels = document.Decode(0, 0, out var width, out var height, out _);
      return new PreviewImage { Width = width, Height = height, Bgra = pixels, Channels = channels };
    }
    catch (Exception)
    {
      return null;
    }
  }

  /// <summary>rea-batch 2: every face (array slice) of a texture at the first mip ≤ 512², or null.</summary>
  internal static IReadOnlyList<PreviewImage>? LoadPreviewSlices(GameCatalog catalog, ArchiveStore store, int hint, string name)
  {
    var index = TemplateMeshReader.ResolveTexture(catalog, hint, name);
    if (index < 0)
      return null;
    try
    {
      var document = TextureLoader.Load(catalog, store, index);
      if (!document.CanDecode)
        return null;
      var mip = 0;
      while (mip < document.MipCount - 1 && Math.Max(document.Width, document.Height) >> mip > 512)
        mip++;
      var list = new List<PreviewImage>();
      for (var f = 0; f < Math.Max(1, document.FaceCount); f++)
      {
        var pixels = document.Decode(mip, f, out var width, out var height, out _);
        list.Add(new PreviewImage { Width = width, Height = height, Bgra = pixels });
      }

      return list;
    }
    catch (Exception)
    {
      return null;
    }
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
    // R54b: levels draw on the CPU path: it frames the level focus (OrbitCamera.Target) and lights it with the level's
    // point lights; the GPU mesh path has neither (pvp_city rendered black there, measured 10/30).
    => GpuToggle.IsChecked == true && !GpuView.HasFailed && _texture is null && _level is null;

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
      new OrbitCamera { Yaw = _yaw, Pitch = _pitch, Distance = _distance, Target = _pivot },
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
    var previewIndex = _previewFileIndex;
    var (choices, owner) = await Task.Run(() =>
    {
      _clipHeaders ??= SaniIndex.ReadHeaders(catalog, store);
      if (physicsMesh.Physics is null && physicsMesh.Bones.Count > 0)
      {
        try
        {
          physicsMesh.Physics = PhysicsLocator.Find(catalog, store, modelPath) ?? new PhysicsSetup();
          if (previewIndex >= 0 && !physicsMesh.Physics.IsEmpty)
            CdtObjectResolver.Apply(physicsMesh.Physics, physicsMesh.Bones, TplMarkup.Find(catalog, store, previewIndex)); // mtl1-37
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

      // propanim: clips in the model's own folder (tpl/x.tpl/animations), even with unnamed tracks (animNmb binding).
      var listed = rows.Select(r => r.Index).ToHashSet();
      foreach (var header in SaniIndex.InFolder(_clipHeaders, modelPath))
        if (listed.Add(header.Index))
          rows.Add(new AnimChoice
          {
            Label = $"★ {Path.GetFileName(header.Path)}  ({header.Duration:0.00}s, model folder)", Index = header.Index, Path = header.Path,
            Rank = ClipRank.Main, SameModel = true,
          });

      // ★ this character's gameplay set, ☆ its cinematic clips, then the rest (clips named after it first), each by path.
      rows.Sort((a, b) =>
        a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank)
        : a.SameModel != b.SameModel ? (a.SameModel ? -1 : 1)
        : string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
      rows.InsertRange(0, PropSequenceRows(physicsMesh.TplAnim));
      return (rows, owner);
    });
    if (generation != _animGeneration || _mesh is null)
      return;
    _animAll = choices;
    SetupPropConditions();
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
    var play = _playOnLoad;
    _playOnLoad = false;
    if (choice.Seq is { } seq)
    {
      _propCond = seq.Name;
      SyncConditionBoxes();
      await PlayPropStandaloneAsync(new PropCondition(seq.Name, PropAnimStates.Label(seq.Name), seq), play);
      return;
    }

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
    if (play && clip.HasCurves && PlayButton.IsVisible)
      PlayClicked(PlayButton, new RoutedEventArgs()); // ui-43: wheel-picked clips play straight away
    else
      DrawMesh();
  }

  /// <summary>ui-43: set by a wheel step on the clip box; the next clip that finishes loading starts playing.</summary>
  private bool _playOnLoad;

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
    AnimBox.SelectedIndex = selected is not null && rows.Contains(selected) ? rows.IndexOf(selected)
      : !keepSelection && _clipIndex >= 0 && rows.FindIndex(r => r.Index == _clipIndex) is var playing and > 0 ? playing : 0;
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
    if (_ctxBase is not null && _mesh is not null)
      _ = Guard(UpdateModelContextAsync()); // mtl1-49: held weapon + effects for this clip
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
    if (FlyPressed(e))
      return;
    _dragging = true;
    _lastPoint = e.GetPosition(ViewBorder);
  }

  private void ViewReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (FlyReleased(e))
      return;
    if (!_dragging)
      return;
    _dragging = false;
    DrawMesh(false);
  }

  private void ViewMoved(object? sender, PointerEventArgs e)
  {
    if (FlyMoved(e))
      return;
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
    if (FlyWheel(e))
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

  private void ShowAllChanged(object? sender, RoutedEventArgs e)
  {
    _settings.ShowAllFiles = ShowAllBox.IsChecked == true;
    try { _settings.Save(); } catch { /* best effort */ }
    RefreshFiles();
  }

  private void FilesTabClicked(object? sender, RoutedEventArgs e)
    => ShowFiles();

  private void RecentTabClicked(object? sender, RoutedEventArgs e)
    => ShowRecent();

  private void ShowFiles()
  {
    ApplyViewVisibility();
    FileHeader.IsVisible = true;
    RecentList.IsVisible = false;
    EmptyText.IsVisible = FileList.ItemCount == 0 && FileList.ItemsSource is not null;
    FilesTab.Classes.Set("active", true);
    RecentTab.Classes.Set("active", false);
  }

  private void ShowRecent()
  {
    FileList.IsVisible = false;
    TileGrid.IsVisible = false;
    EmptyText.IsVisible = false;
    FileHeader.IsVisible = false;
    RecentList.IsVisible = true;
    FilesTab.Classes.Set("active", false);
    RecentTab.Classes.Set("active", true);
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
    EnterFolderScope();
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
    if (FindDirectory(directoryPath) is not var (indices, node))
      return false;

    for (var depth = 1; depth < indices.Count; depth++)
      _tree.Expand(new IndexPath(indices.GetRange(0, depth)));
    var already = _directory is not null && string.Equals(_directory.Path, node.Path, StringComparison.OrdinalIgnoreCase);
    _tree.RowSelection!.Select(new IndexPath(indices));
    if (already)
      RefreshFiles();
    return true;
  }

  /// <summary>Tree index path (root = 0) and node of a catalog directory path ("a/b/c"), or null.</summary>
  private (List<int> Indices, CatalogDirectory Node)? FindDirectory(string directoryPath)
  {
    if (_catalog is null)
      return null;
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
          return null;
        indices.Add(found);
        node = node.Children[found];
      }
    }
    return (indices, node);
  }

  private void ApplyPendingFile()
  {
    if (_pendingFileIndex < 0 || FileList.ItemsSource is not FileRowList rows)
      return;
    if (_scope == Scope.Folder && (_directory is null || !string.Equals(_directory.Path, _pendingDirectory, StringComparison.OrdinalIgnoreCase)))
      return;
    var pending = _pendingFileIndex;
    _pendingFileIndex = -1;
    var row = rows.RowForCatalogIndex(pending);
    if (row < 0)
    {
      StatusText.Text = ShowAllBox.IsChecked == true
        ? "The recent file is not visible in the current list."
        : "The recent file is not visible in the current list (auxiliary files are hidden: tick Show all files).";
      return;
    }

    FileList.SelectedIndex = row;
    FileList.ScrollIntoView(rows[row]);
    if (_gridView && _gridColumns > 0)
      Dispatcher.UIThread.Post(() => TileGrid.ScrollIntoView(row / _gridColumns), DispatcherPriority.Background);
    RestoreScroll();
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
    await ExportIndicesAsync(indices);
  }

  private async Task ExportIndicesAsync(int[] indices)
  {
    if (_catalog is null || _store is null)
      return;
    if (indices.Length == 0)
    {
      StatusText.Text = "Select one or more files in the list first.";
      return;
    }

    var window = new ExportWindow();
    VariantResult? parts = null;
    Dictionary<string, List<string>>? skinRegions = null;
    if (_catalog is { } pc && _store is { } ps && _previewFileIndex >= 0 && indices.Contains(_previewFileIndex))
    {
      var pi = _previewFileIndex;
      StatusText.Text = "Reading customization parts…";
      try
      {
        (parts, skinRegions) = await Task.Run(() =>
        {
          var d = AssetExporter.DescribeVariant(pc, ps, pi);
          Dictionary<string, List<string>>? sr = null;
          if (d is { Team: "WEAPON" } && AssetExporter.WeaponLibrary(pc, ps) is { } wl && wl.Skins.TryGetValue(d.Appearance, out var skins))
            sr = skins.ToDictionary(k => k.Key, k => SslText.Strings(k.Skin.GetValueOrDefault("geomCustomizerRegions")).ToList(), StringComparer.OrdinalIgnoreCase);
          else if (d is { Team: "MELEE" } && AssetExporter.MeleeLibrary(pc, ps) is { } ml)
            sr = ml.SkinsFor(d.Template).ToDictionary(k => k.Spec(d.Appearance), k => k.Regions.ToList(), StringComparer.OrdinalIgnoreCase);
          return (d, sr);
        });
      }
      catch (Exception ex)
      {
        StatusText.Text = "Customization parts unavailable: " + ex.Message;
      }
    }

    window.SetDirect(_settings.LastUnrealProject, DirectAssetName(indices));
    var choice = await window.Choose(this, _settings.LastExportFolder, parts, skinRegions);
    if (choice is null)
      return;
    _settings.LastExportFolder = choice.Folder;
    if (choice.Unreal is { } un)
      _settings.LastUnrealProject = un.Project;
    _settings.Save();
    // mtl1-49: the colour scheme picked in the viewer exports with the previewed tpl (material=..., td variant swap)
    if (!_armorMode && _colourMaterial is { Length: > 0 } colour && _previewFileIndex >= 0 && indices.Contains(_previewFileIndex))
      choice.Settings.Variant = string.IsNullOrWhiteSpace(choice.Settings.Variant) ? "material=" + colour : choice.Settings.Variant + ",material=" + colour;
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
      // oneclick: a prop's embedded sequence (▶ SEQ row) has no .sani file index; "The clip playing in the preview"
      // then exports that clip itself (same clip object as the Extractor's --anim seq:NAME).
      var previewSeq = choice.Clips == ClipChoice.PreviewClip && previewClip < 0 && _clip is { HasCurves: true } seq ? seq : null;
      var notes = new List<string>();
      var report = await Task.Run(() =>
      {
        var files = ExportPlan.Apply(catalog, store, indices, choice.Settings, choice.Clips, previewClip, notes);
        if (previewSeq is not null)
        {
          choice.Settings.Clips = new List<AnimationClip> { previewSeq };
          notes.RemoveAll(n => n.StartsWith("No clip is playing", StringComparison.Ordinal));
        }
        return AssetExporter.Export(catalog, store, files, choice.Folder, choice.Settings, progress);
      });
      report.Messages.InsertRange(0, notes);
      // mtl1-52: the picked character scheme as UE material-instance parameters (masks / layer tints) next to the export
      if (_previewFileIndex >= 0 && indices.Contains(_previewFileIndex) && ArmorSchemeExportJson() is { } schemeJson && _catalog is { } cat)
      {
        try
        {
          var tpl = AssetClassification.BaseName(cat.GetFile(_previewFileIndex).Name);
          var path = System.IO.Path.Combine(choice.Folder, $"{tpl}.colour_scheme.{_colourMaterial}.json");
          await System.IO.File.WriteAllTextAsync(path, schemeJson);
          report.Messages.Insert(0, "colour scheme params: " + System.IO.Path.GetFileName(path) + ".");
          if (report.Models.FirstOrDefault(m => m.Name.Equals(tpl, StringComparison.OrdinalIgnoreCase)) is { } rec)
            rec.ColourScheme = path;
        }
        catch (Exception ex)
        {
          report.Messages.Insert(0, "colour scheme params not written: " + ex.Message);
        }
      }

      StatusText.Text = $"Exported {report.Written} files, skipped {report.Skipped}. {string.Join(" ", report.Messages.Take(3))}";
      if (choice.Unreal is { } direct)
        await RunDirectImportAsync(report, choice, direct);
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

  /// <summary>
  ///   mtl1-42: file list columns follow the header splitters (<see cref="FileColumns"/>); column and pane widths are
  ///   restored from / saved to settings.json.
  /// </summary>
  private void InitLayoutMemory()
  {
    var cols = FileColumnHeader.ColumnDefinitions;
    var shared = FileColumns.Shared;
    static bool Ok(double? v) => v is { } d && double.IsFinite(d) && d >= FileColumns.MinWidth && d < 4000;
    if (Ok(_settings.FileTypeWidth)) shared.TypeWidth = _settings.FileTypeWidth!.Value;
    if (Ok(_settings.FileSizeWidth)) shared.SizeWidth = _settings.FileSizeWidth!.Value;
    if (Ok(_settings.FileArchiveWidth)) shared.ArchiveWidth = _settings.FileArchiveWidth!.Value;
    cols[2].Width = new GridLength(shared.TypeWidth);
    cols[4].Width = new GridLength(shared.SizeWidth);
    cols[6].Width = new GridLength(shared.ArchiveWidth);
    foreach (var c in new[] { cols[2], cols[4], cols[6] })
      c.MinWidth = FileColumns.MinWidth;
    void Sync()
    {
      // A splitter writes pixel widths; a star (PATH) side keeps taking the rest.
      double W(ColumnDefinition c) => c.Width.IsAbsolute ? c.Width.Value : c.ActualWidth;
      shared.TypeWidth = W(cols[2]);
      shared.SizeWidth = W(cols[4]);
      shared.ArchiveWidth = W(cols[6]);
    }
    foreach (var c in cols)
      c.PropertyChanged += (_, e) =>
      {
        if (e.Property == ColumnDefinition.WidthProperty)
          Sync();
      };

    var main = MainColumns.ColumnDefinitions;
    if (_settings.FolderPaneWidth is { } folder && double.IsFinite(folder) && folder >= 120 && folder < 2000)
      main[0].Width = new GridLength(folder);
    if (_settings.FilePaneWidth is { } files && double.IsFinite(files) && files > 0.1 && files < 10)
    {
      main[2].Width = new GridLength(files, GridUnitType.Star);
      main[4].Width = new GridLength(2, GridUnitType.Star);
    }
  }

  private readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);
  private bool _restoring;

  /// <summary>mtl1-42: search text, type filter and Include subfolders from the last run (before any folder loads).</summary>
  private void RestoreFilterState()
  {
    _restoring = true;
    try
    {
      if (_settings.FilterIndex is { } f && f >= 0 && f < FilterBox.ItemCount)
        FilterBox.SelectedIndex = f;
      SubfoldersBox.IsChecked = _settings.IncludeSubfolders;
      if (!string.IsNullOrEmpty(_settings.SearchText))
      {
        _suppressSearch = true;
        SearchBox.Text = _settings.SearchText;
        _suppressSearch = false;
      }
    }
    finally { _restoring = false; }
  }

  /// <summary>mtl1-42: expanded folders, selected folder and file, and the list scroll from the last run.</summary>
  private bool RestoreTreeState(GameCatalog catalog)
  {
    if (_tree is null || _settings.SelectedFolder is null && _settings.ExpandedFolders is not { Count: > 0 })
      return false;
    _restoring = true;
    try
    {
      foreach (var path in (_settings.ExpandedFolders ?? new()).OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p.Length))
        if (FindDirectory(path) is var (indices, _))
          _tree.Expand(new IndexPath(indices));
      if (_settings.SelectedFile is { } file)
      {
        var hits = catalog.FindByPath(file);
        var index = hits.FirstOrDefault(h => string.Equals(catalog.GetFile(h).ArchiveName, _settings.SelectedFileArchive, StringComparison.OrdinalIgnoreCase), hits.Count > 0 ? hits[0] : -1);
        if (index >= 0)
        {
          _pendingFileIndex = index;
          _pendingDirectory = _settings.SelectedFolder ?? "";
          _pendingScroll = _settings.FileScroll;
        }
      }
      if (FindDirectory(_settings.SelectedFolder ?? "") is var (sel, node))
      {
        if (_directory is not null && string.Equals(_directory.Path, node.Path, StringComparison.OrdinalIgnoreCase))
          _directory = null; // force the selection handler to run
        _tree.RowSelection!.Select(new IndexPath(sel));
        if (_directory is null)
        {
          _directory = node;
          RefreshFiles();
        }
        return true;
      }
      _pendingFileIndex = -1;
      return false;
    }
    finally { _restoring = false; }
  }

  private double? _pendingScroll;

  private void RestoreScroll()
  {
    if (_pendingScroll is not { } y)
      return;
    _pendingScroll = null;
    Dispatcher.UIThread.Post(() =>
    {
      if (FileList.Scroll is ScrollViewer viewer)
        viewer.Offset = new Vector(viewer.Offset.X, Math.Min(y, Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height)));
      // The saved scroll wins as long as the selected file stays on screen.
      Dispatcher.UIThread.Post(() =>
      {
        if (FileList.SelectedItem is { } selected)
        {
          FileList.ScrollIntoView(selected);
          // Up/Down continue from the restored file straight away.
          if (FileList.ContainerFromItem(selected) is { } item)
            item.Focus(NavigationMethod.Directional);
        }
      }, DispatcherPriority.Background);
    }, DispatcherPriority.Background);
  }

  private void SaveBrowserState()
  {
    _settings.SearchText = SearchBox.Text ?? "";
    _settings.FilterIndex = FilterBox.SelectedIndex;
    _settings.IncludeSubfolders = SubfoldersBox.IsChecked == true;
    SaveContentBrowserState();
    if (_catalog is null)
      return;
    // Keep only folders whose parents are expanded too (a collapsed parent hides them).
    bool Visible(string p)
    {
      for (var slash = p.LastIndexOf('/'); slash > 0; slash = p.LastIndexOf('/', slash - 1))
        if (!_expanded.Contains(p[..slash]))
          return false;
      return true;
    }
    _settings.ExpandedFolders = _expanded.Where(p => p.Length > 0 && Visible(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    _settings.SelectedFolder = _directory?.Path ?? "";
    if (FileList.SelectedItem is FileRow row)
    {
      var f = _catalog.GetFile(row.Index);
      _settings.SelectedFile = f.Path;
      _settings.SelectedFileArchive = f.ArchiveName;
    }
    else
    {
      _settings.SelectedFile = null;
      _settings.SelectedFileArchive = null;
    }
    _settings.FileScroll = FileList.Scroll is ScrollViewer viewer ? viewer.Offset.Y : null;
  }

  private void SaveLayoutMemory()
  {
    try { SaveBrowserState(); } catch { /* best effort */ }
    var shared = FileColumns.Shared;
    _settings.FileTypeWidth = shared.TypeWidth;
    _settings.FileSizeWidth = shared.SizeWidth;
    _settings.FileArchiveWidth = shared.ArchiveWidth;
    var main = MainColumns.ColumnDefinitions;
    if (main[0].ActualWidth > 0)
      _settings.FolderPaneWidth = Math.Round(main[0].ActualWidth);
    if (main[2].ActualWidth > 0 && main[4].ActualWidth > 0)
      _settings.FilePaneWidth = Math.Round(2 * main[2].ActualWidth / main[4].ActualWidth, 3);
    try { _settings.Save(); } catch { /* settings are best effort */ }
  }

  protected override void OnClosed(EventArgs e)
  {
    SaveLayoutMemory();
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
  /// <summary>propanim: an embedded tpl sequence row (Index -2).</summary>
  public TplSequence? Seq { get; init; }
  public override string ToString() => Label;
}
