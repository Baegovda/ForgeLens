using Avalonia;
using Avalonia.Controls;
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
  private float _pitch = 0.35f;
  private float _distance;
  private float _time;
  private bool _dragging;
  private Point _lastPoint;
  private bool _suppressMip;

  public MainWindow()
  {
    InitializeComponent();
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
      Title = "Space Marine 2 client_pc folder",
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
      _settings.LastFolder = path;
      _settings.Save();
      BindTree(catalog);
      var skipped = catalog.ArchivesSkipped == 0 ? "" : $", {catalog.ArchivesSkipped} caches skipped";
      StatusText.Text = $"{catalog.Archives.Count} archives, {catalog.FileCount:N0} files{skipped}. Folders are on the left; files stay in the list so 486k entries are not tree nodes.";
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
          new TextColumn<CatalogDirectory, string>("Folder", directory => directory.Path.Length == 0 ? directory.Name : directory.Name, width: new GridLength(1, GridUnitType.Star)),
          directory => directory.Children,
          directory => directory.Children.Length > 0),
      },
    };
    source.RowSelection!.SingleSelect = true;
    source.RowSelection.SelectionChanged += (_, _) =>
    {
      _directory = source.RowSelection.SelectedItem;
      RefreshFiles();
    };
    FolderTree.Source = source;
    _directory = catalog.Root;
    RefreshFiles();
  }

  private void SearchChanged(object? sender, TextChangedEventArgs e)
  {
    _searchTimer.Stop();
    _searchTimer.Start();
  }

  private void FilterChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (FolderText is null)
      return;
    RefreshFiles();
  }

  private void RefreshFiles()
  {
    if (_catalog is null)
      return;

    var filter = FilterBox.SelectedIndex switch
    {
      1 => AssetFilter.Textures,
      2 => AssetFilter.Models,
      3 => AssetFilter.Animations,
      4 => AssetFilter.Effects,
      5 => AssetFilter.Text,
      _ => AssetFilter.All,
    };
    var directory = string.IsNullOrWhiteSpace(SearchBox.Text) ? _directory ?? _catalog.Root : null;
    var query = _catalog.Query(directory, SearchBox.Text, filter);
    FileList.ItemsSource = new FileRowList(_catalog, query);
    StatusText.Text = $"{query.Length:N0} shown of {_catalog.FileCount:N0} files in {_catalog.Archives.Count} archives.";
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
    _playTimer.Stop();
    _texture = null;
    _clip = null;
    if (file.Kind != AssetKind.Animation)
      _mesh = null;
    SkeletonToggle.IsVisible = false;
    PlayButton.IsVisible = false;
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
        return;
      }

      if (file.Kind is AssetKind.Model)
      {
        var mesh = await Task.Run(() => TemplateMeshReader.Load(_catalog, _store, row.Index));
        if (generation != _previewGeneration)
          return;
        _mesh = mesh;
        _distance = 0;
        _time = 0;
        SkeletonToggle.IsVisible = mesh.Bones.Count > 0;
        PreviewNote.Text = mesh.Message;
        if (mesh.HasTriangles || mesh.Bones.Count > 0)
        {
          PreviewText.IsVisible = false;
          PreviewImage.IsVisible = true;
          DrawMesh();
        }
        else
        {
          PreviewText.Text = mesh.Message;
        }

        return;
      }

      if (file.Kind is AssetKind.Animation)
      {
        var clip = await Task.Run(() => SaniReader.Load(_catalog, _store, row.Index));
        if (generation != _previewGeneration)
          return;
        _clip = clip;
        PlayButton.IsVisible = clip.HasCurves;
        SkeletonToggle.IsVisible = _mesh is { Bones.Count: > 0 };
        PreviewText.Text = Describe(clip);
        PreviewNote.Text = clip.HasCurves
          ? "Play samples the curves onto the skeleton when a model is also loaded. Retail .sani packing is unverified."
          : "No sampled curves. The note above is what the reader could see.";
        return;
      }

      if (file.Size > 32L * 1024 * 1024)
      {
        PreviewText.Text = $"{file.Path} is {file.SizeText}. Preview reads are capped so a multi-megabyte buffer is not pulled into the text pane. Export copies the original bytes.";
        return;
      }

      var bytes = await Task.Run(() => _store.Read(row.Index));
      if (generation != _previewGeneration)
        return;
      if (file.Kind == AssetKind.TextureMip)
      {
        PreviewText.Text = "Loose .pct_mip payload. Open the .pct.resource that lists this file. The first bytes are below.\n\n" + BytePreview.Hex(bytes);
        return;
      }

      PreviewText.Text = TextureLoader.LooksText(bytes) ? BytePreview.Text(bytes) : BytePreview.Hex(bytes);
      if (file.Kind == AssetKind.Effect)
        PreviewNote.Text = "Effects and particle systems are not converted. Export copies the raw file and any texture or model names found in it.";
    }
    catch (Exception ex)
    {
      if (generation != _previewGeneration)
        return;
      PreviewText.IsVisible = true;
      PreviewImage.IsVisible = false;
      PreviewText.Text = ex.Message;
    }
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
    }
    catch (Exception ex)
    {
      if (generation != _previewGeneration)
        return;
      PreviewImage.IsVisible = false;
      PreviewText.IsVisible = true;
      PreviewText.Text = string.IsNullOrEmpty(document.Message) ? ex.Message : document.Message + "\n\n" + ex.Message;
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

  private void DrawMesh()
  {
    if (_mesh is null)
      return;
    var pixels = MeshPreview.Render(
      _mesh,
      480,
      480,
      new OrbitCamera { Yaw = _yaw, Pitch = _pitch, Distance = _distance },
      SkeletonToggle.IsChecked == true,
      _time,
      out var note);
    ShowPixels(pixels, 480, 480);
    if (!string.IsNullOrEmpty(note))
      PreviewNote.Text = note;
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
    => DrawMesh();

  private void PlayClicked(object? sender, RoutedEventArgs e)
  {
    if (_playTimer.IsEnabled)
    {
      _playTimer.Stop();
      PlayButton.Content = "Play";
      return;
    }

    if (_mesh is null || _clip is null)
    {
      PreviewNote.Text = "Load a .tpl with a skeleton, then a .sani, to pose the bones. Playback of retail curves is unverified.";
      return;
    }

    _mesh.Clip = _clip;
    _playTimer.Start();
    PlayButton.Content = "Pause";
  }

  private void ViewPressed(object? sender, PointerPressedEventArgs e)
  {
    _dragging = true;
    _lastPoint = e.GetPosition(ViewBorder);
  }

  private void ViewReleased(object? sender, PointerReleasedEventArgs e)
    => _dragging = false;

  private void ViewMoved(object? sender, PointerEventArgs e)
  {
    if (!_dragging || _mesh is null)
      return;
    var point = e.GetPosition(ViewBorder);
    _yaw += (float)(point.X - _lastPoint.X) * 0.01f;
    _pitch += (float)(point.Y - _lastPoint.Y) * 0.01f;
    _lastPoint = point;
    DrawMesh();
  }

  private void ViewWheel(object? sender, PointerWheelEventArgs e)
  {
    if (_mesh is null)
      return;
    var step = _distance <= 0 ? 0.5f : _distance * 0.1f;
    _distance = Math.Max(0.2f, (_distance <= 0 ? 4f : _distance) - (float)e.Delta.Y * step);
    DrawMesh();
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

    var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
      Title = "Export folder",
      AllowMultiple = false,
    });
    if (folders.Count == 0)
      return;
    var output = folders[0].TryGetLocalPath();
    if (string.IsNullOrWhiteSpace(output))
      return;

    var window = new ExportWindow();
    var settings = await window.Choose(this);
    if (settings is null)
      return;

    ExportButton.IsEnabled = false;
    try
    {
      var progress = new Progress<string>(path => StatusText.Text = "Exporting " + path);
      var report = await Task.Run(() => AssetExporter.Export(_catalog, _store, indices, output, settings, progress));
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
