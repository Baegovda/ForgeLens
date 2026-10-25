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
  private List<SaniIndex.Header>? _clipHeaders;

  public MainWindow()
  {
    InitializeComponent();
    RecentList.ItemsSource = _recent.Items;
    ShowFiles();
    _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    _searchTimer.Tick += (_, _) =>
    {
      _searchTimer.Stop();
      RefreshFiles();
    };
    _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
    _playTimer.Tick += (_, _) =>
    {
      if (_clip is null)
        return;
      _time += 0.033f;
      if (_clip.Duration > 0 && _time > _clip.Duration)
        _time = 0;
      SyncScrub();
      DrawMesh();
    };
    Opened += async (_, _) =>
    {
      if (!string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
        await LoadFolderAsync(_settings.LastFolder);
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
      ExportButton.IsEnabled = catalog.FileCount > 0;
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
    if (_suppressSearch)
      return;
    _searchTimer.Stop();
    _searchTimer.Start();
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
    var scope = scoped is null
      ? "search"
      : includeSubfolders ? directory.Name + " and subfolders" : directory.Name;
    var hidden = catalog.DuplicatesCollapsed == 0
      ? ""
      : $" {catalog.DuplicatesCollapsed:N0} duplicate paths hidden; the later archive is shown.";
    var skipped = catalog.ArchivesSkipped == 0 ? "" : $" {catalog.ArchivesSkipped} caches skipped.";
    StatusText.Text = $"{query.Length:N0} shown in {scope}. {directory.Children.Length:N0} subfolders here. {catalog.FileCount:N0} files in {catalog.Archives.Count} archives.{hidden}{skipped}";
    ApplyPendingFile();
  }

  private async void FileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    => await PreviewSelectionAsync();

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
    toggles.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
    MeshList.ItemsSource = toggles;
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
    var bounds = ViewBorder.Bounds;
    var width = bounds.Width < 64 ? 720 : (int)bounds.Width;
    var height = bounds.Height < 64 ? 900 : (int)bounds.Height;
    if (fast)
    {
      width /= 2;
      height /= 2;
    }

    width = Math.Clamp(width, 32, 1100);
    height = Math.Clamp(height, 32, 1400);
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
        new PreviewSettings { Lod = lod, Part = part, Wireframe = wire, Albedo = albedo, Normals = normals, Specular = specular, MaxEdge = 1400 });
      return (pixels, width, height, note);
    }).ContinueWith(task =>
    {
      Dispatcher.UIThread.Post(() =>
      {
        if (generation == _drawGeneration && task.Status == TaskStatus.RanToCompletion)
        {
          ShowPixels(task.Result.pixels, task.Result.width, task.Result.height);
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

  private readonly record struct PreviewTextures(
    Dictionary<string, PreviewImage> Albedo,
    Dictionary<string, PreviewImage> Normals,
    Dictionary<string, PreviewImage> Specular);

  private static PreviewTextures ResolvePreview(GameCatalog catalog, ArchiveStore store, ExportedMesh mesh, int hint)
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
    var bitmap = new WriteableBitmap(new Avalonia.PixelSize(width, height), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
    using (var frame = bitmap.Lock())
    {
      for (var y = 0; y < height; y++)
      {
        System.Runtime.InteropServices.Marshal.Copy(pixels, y * width * 4, frame.Address + y * frame.RowBytes, width * 4);
      }
    }

    PreviewImage.Source = bitmap;
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

  private void PlayClicked(object? sender, RoutedEventArgs e)
  {
    if (_playTimer.IsEnabled)
    {
      _playTimer.Stop();
      PlayButton.Content = "Play";
      return;
    }

    if (_mesh is null || _clip is null || !_clip.HasCurves)
    {
      PreviewNote.Text = "Select a model, then an animation that shares its bone names.";
      return;
    }

    _mesh.Clip = _clip;
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
    AnimBar.IsVisible = true;
    _suppressAnim = true;
    AnimBox.ItemsSource = new[] { new AnimChoice { Label = "Finding animations…", Index = -1 } };
    AnimBox.SelectedIndex = 0;
    _suppressAnim = false;
    var choices = await Task.Run(() =>
    {
      _clipHeaders ??= SaniIndex.ReadHeaders(catalog, store);
      var rows = new List<AnimChoice> { new() { Label = "Bind pose", Index = -1 } };
      foreach (var header in SaniIndex.Compatible(_clipHeaders, names))
      {
        var file = Path.GetFileName(header.Path);
        rows.Add(new AnimChoice { Label = $"{file}  ({header.Duration:0.00}s)", Index = header.Index });
      }

      return rows;
    });
    if (generation != _animGeneration || _mesh is null)
      return;
    _suppressAnim = true;
    AnimBox.ItemsSource = choices;
    AnimBox.SelectedIndex = 0;
    _suppressAnim = false;
    StatusText.Text = choices.Count <= 1
      ? "No animation shares 8 or more bone names with this model."
      : $"{choices.Count - 1:N0} animations share bone names with this model.";
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
      _clip = null;
      _mesh.Clip = null;
      _time = 0;
      PlayButton.IsVisible = false;
      ScrubSlider.IsEnabled = false;
      ScrubLabel.Text = "0.00s";
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
    ApplyClipToMesh(clip);
    PreviewNote.Text = clip.HasCurves
      ? $"{clip.Name}: {clip.Tracks.Count} tracks, {clip.Duration:0.###}s. Bones this clip does not name stay on the bind pose."
      : (clip.Notes.Count == 0 ? "This clip has no curve keys." : clip.Notes[0]);
    DrawMesh();
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

    ExportButton.IsEnabled = false;
    try
    {
      var progress = new Progress<string>(path => StatusText.Text = "Exporting " + path);
      var report = await Task.Run(() => AssetExporter.Export(_catalog, _store, indices, choice.Folder, choice.Settings, progress));
      StatusText.Text = $"Exported {report.Written} files, skipped {report.Skipped}. {string.Join(" ", report.Messages.Take(3))}";
    }
    catch (Exception ex)
    {
      StatusText.Text = ex.Message;
    }
    finally
    {
      ExportButton.IsEnabled = true;
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
  public override string ToString() => Label;
}
