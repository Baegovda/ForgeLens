using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2Browser.ContentBrowser;

namespace LibSaber.SM2Browser;

/// <summary>
///   ui-43: UE5-style Content Browser around the existing viewer. Left: Favorites, Folders | Categories tree, Collections.
///   Centre: type chips, search, a virtualized thumbnail grid or the detail list. The (possibly hidden) <c>FileList</c>
///   stays the single source of truth for the shown files and the selection, so preview, export, Recent and the saved
///   UI state work the same in both views.
/// </summary>
public partial class MainWindow
{
  private enum Scope { Folder, Category, Collection }

  private const string AssetsFormat = "ForgeLens.Assets";
  private Scope _scope = Scope.Folder;
  private bool _categoriesMode;
  private CategoryTree? _categories;
  private CategoryNode? _category;
  private CollectionItem? _collection;
  private ThumbnailService? _thumbs;
  private readonly ObservableCollection<FavoriteItem> _favorites = new();
  private readonly ObservableCollection<CollectionItem> _collections = new();
  private bool _gridView = true;
  private bool _scopeRestorePending;
  private int _gridColumns;
  private Point? _dragStart;
  private int _dragFrom = -1;
  private int _anchor = -1;
  private bool _deferSelectOnly;
  private bool _suppressLeft;

  private void InitContentBrowser()
  {
    foreach (var f in _settings.Favorites ?? new())
      _favorites.Add(new FavoriteItem(f));
    foreach (var c in _settings.Collections ?? new())
      _collections.Add(new CollectionItem(c));
    FavoritesList.ItemsSource = _favorites;
    CollectionsList.ItemsSource = _collections;
    _favorites.CollectionChanged += (_, _) => FavoritesEmpty.IsVisible = _favorites.Count == 0;
    FavoritesEmpty.IsVisible = _favorites.Count == 0;

    _gridView = _settings.ViewMode != "List";
    var size = _settings.TileSize is { } t && double.IsFinite(t) ? Math.Clamp(t, 64, 256) : 112;
    AssetTile.TileSize = size;
    TileSizeSlider.Value = size;
    AnimateAllBox.IsChecked = _settings.AnimateAll;
    AssetTile.AnimateAll = _settings.AnimateAll;
    _categoriesMode = _settings.TreeMode == "Categories";
    AssetTile.IsSelectedIndex = pos => FileList.Selection.IsSelected(pos);
    ApplyTreeTabs();
    SyncChips();

    TileGrid.AddHandler(PointerPressedEvent, TilePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    TileGrid.AddHandler(PointerMovedEvent, TileMoved, RoutingStrategies.Bubble, handledEventsToo: true);
    TileGrid.AddHandler(PointerReleasedEvent, TileReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    TileGrid.AddHandler(KeyDownEvent, TileKeyDown, RoutingStrategies.Tunnel);
    TileGrid.ContextRequested += (_, e) => { e.Handled = true; ShowAssetMenu(TileGrid); };
    TileGrid.SizeChanged += (_, _) =>
    {
      if (Columns() != _gridColumns)
        RebuildGrid();
    };
    FileList.ContextRequested += (_, e) => { e.Handled = true; ShowAssetMenu(FileList); };
    FileList.AddHandler(PointerPressedEvent, (_, e) =>
    {
      _dragStart = e.GetCurrentPoint(FileList).Properties.IsLeftButtonPressed ? e.GetPosition(FileList) : null;
      _dragFrom = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true)?.DataContext is FileRow r && FileList.ItemsSource is FileRowList l ? l.RowForCatalogIndex(r.Index) : -1;
    }, RoutingStrategies.Tunnel, handledEventsToo: true);
    FileList.AddHandler(PointerMovedEvent, (_, e) => MaybeDrag(e, FileList), RoutingStrategies.Tunnel, handledEventsToo: true);
    FileList.AddHandler(PointerReleasedEvent, (_, _) => _dragStart = null, RoutingStrategies.Tunnel, handledEventsToo: true);

    FolderTree.ContextRequested += (_, e) => { e.Handled = true; ShowFolderMenu(); };
    FolderTree.AddHandler(PointerReleasedEvent, (_, _) =>
    {
      if (_scope != Scope.Folder && _directory is not null)
        SetScope(Scope.Folder);
    }, RoutingStrategies.Bubble, handledEventsToo: true);
    CategoryTree.AddHandler(PointerReleasedEvent, (_, _) =>
    {
      if (_scope != Scope.Category && CategoryTree.SelectedItem is CategoryItem item)
      {
        _category = item.Node;
        SetScope(Scope.Category);
      }
    }, RoutingStrategies.Bubble, handledEventsToo: true);
    CollectionsList.AddHandler(PointerReleasedEvent, (_, _) =>
    {
      if (_scope != Scope.Collection && CollectionsList.SelectedItem is CollectionItem item)
      {
        _collection = item;
        SetScope(Scope.Collection);
      }
    }, RoutingStrategies.Bubble, handledEventsToo: true);
    CollectionsList.AddHandler(DragDrop.DragOverEvent, CollectionDragOver);
    CollectionsList.AddHandler(DragDrop.DropEvent, CollectionDrop);
    CollectionsList.ContextRequested += (_, e) =>
    {
      e.Handled = true;
      var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true)?.DataContext as CollectionItem;
      ShowCollectionMenu(item);
    };
    FavoritesList.ContextRequested += (_, e) =>
    {
      e.Handled = true;
      if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true)?.DataContext is not FavoriteItem fav)
        return;
      var menu = new ContextMenu();
      menu.Items.Add(Item("Remove from Favorites", () => { _favorites.Remove(fav); SaveLeftPane(); }));
      menu.Open(FavoritesList);
    };
  }

  /// <summary>New catalog: thumbnails for it, and the category tree built in the background.</summary>
  private void ContentBrowserCatalogChanged(GameCatalog catalog, ArchiveStore store)
  {
    _thumbs?.Dispose();
    _thumbs = new ThumbnailService(catalog, store, () => ReferenceEquals(_catalog, catalog) ? _categories : null);
    AssetTile.Service = _thumbs;
    _categories = null;
    _category = null;
    CategoryTree.ItemsSource = null;
    _scope = Scope.Folder;
    var wantsCategory = _settings.TreeMode == "Categories";
    var wantsCollection = _settings.SelectedCollection is { } sc && _collections.Any(c => c.Name == sc);
    _scopeRestorePending = wantsCategory || wantsCollection;
    _ = BuildCategoriesAsync(catalog, store, restore: wantsCategory && !wantsCollection);
  }

  /// <summary>End of BindTree: a saved collection scope needs no category tree, so it restores straight away.</summary>
  private void ContentBrowserTreeBound()
  {
    if (_settings.SelectedCollection is { } name && _collections.FirstOrDefault(c => c.Name == name) is { } item)
    {
      _scopeRestorePending = false;
      _collection = item;
      _suppressLeft = true;
      CollectionsList.SelectedItem = item;
      _suppressLeft = false;
      SetScope(Scope.Collection);
    }
  }

  private async Task BuildCategoriesAsync(GameCatalog catalog, ArchiveStore store, bool restore)
  {
    CategoryStatus.Text = "Building categories from classes and Morpheme bundles…";
    CategoryStatus.IsVisible = _categoriesMode;
    CategoryTree tree;
    try
    {
      tree = await Task.Run(() => AssetCategories.Build(catalog, store));
    }
    catch (Exception ex)
    {
      CategoryStatus.Text = "Categories failed: " + ex.Message;
      if (_scopeRestorePending)
      {
        _scopeRestorePending = false;
        RefreshFiles();
      }
      return;
    }

    if (!ReferenceEquals(catalog, _catalog))
      return;
    _categories = tree;
    AssetTile.RequestMissing();
    var roots = new ObservableCollection<CategoryItem>(tree.Root.Children.Select(n => new CategoryItem(n)));
    CategoryTree.ItemsSource = roots;
    CategoryStatus.IsVisible = false;
    if (restore && _scopeRestorePending)
    {
      _scopeRestorePending = false;
      var item = RevealCategory(roots, _settings.SelectedCategory) ?? roots.FirstOrDefault();
      _category = item?.Node ?? tree.All;
      _suppressLeft = true;
      CategoryTree.SelectedItem = item;
      _suppressLeft = false;
      SetScope(Scope.Category);
    }
    else if (_scopeRestorePending)
    {
      _scopeRestorePending = false;
      RefreshFiles();
    }
  }

  /// <summary>Expands the path to a saved category key and returns its item.</summary>
  private static CategoryItem? RevealCategory(IEnumerable<CategoryItem> roots, string? key)
  {
    if (string.IsNullOrEmpty(key))
      return null;
    CategoryItem? found = null;
    var level = roots;
    while (true)
    {
      var next = level.FirstOrDefault(i => string.Equals(i.Node.Key, key, StringComparison.OrdinalIgnoreCase)
                                           || key.StartsWith(i.Node.Key + "/", StringComparison.OrdinalIgnoreCase));
      if (next is null)
        return found;
      found = next;
      if (string.Equals(next.Node.Key, key, StringComparison.OrdinalIgnoreCase))
        return found;
      next.IsExpanded = true;
      level = next.Children;
    }
  }

  // ------------------------------------------------------------------------------------------ scope

  private void SetScope(Scope scope)
  {
    _scope = scope;
    if (scope != Scope.Collection && CollectionsList.SelectedItem is not null)
    {
      _suppressLeft = true;
      CollectionsList.SelectedItem = null;
      _suppressLeft = false;
    }

    RefreshFiles();
  }

  private void FoldersTabClicked(object? sender, RoutedEventArgs e)
  {
    _categoriesMode = false;
    ApplyTreeTabs();
    SetScope(Scope.Folder);
  }

  private void CategoriesTabClicked(object? sender, RoutedEventArgs e)
  {
    _categoriesMode = true;
    ApplyTreeTabs();
    if (_categories is null)
      return;
    if (_category is null)
    {
      var first = (CategoryTree.ItemsSource as IEnumerable<CategoryItem>)?.FirstOrDefault();
      _category = first?.Node ?? _categories.All;
      _suppressLeft = true;
      CategoryTree.SelectedItem = first;
      _suppressLeft = false;
    }

    SetScope(Scope.Category);
  }

  private void ApplyTreeTabs()
  {
    FolderTree.IsVisible = !_categoriesMode;
    CategoryTree.IsVisible = _categoriesMode;
    CategoryStatus.IsVisible = _categoriesMode && _categories is null && _catalog is not null;
    FoldersTab.Classes.Set("active", !_categoriesMode);
    CategoriesTab.Classes.Set("active", _categoriesMode);
  }

  private void CategorySelected(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressLeft || CategoryTree.SelectedItem is not CategoryItem item)
      return;
    _category = item.Node;
    SetScope(Scope.Category);
  }

  private void CollectionSelected(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressLeft || CollectionsList.SelectedItem is not CollectionItem item)
      return;
    _collection = item;
    SetScope(Scope.Collection);
  }

  /// <summary>Recent / Show in folder / a favorite: back to the Folders tree before selecting a directory.</summary>
  private void EnterFolderScope()
  {
    _scope = Scope.Folder;
    if (_categoriesMode)
    {
      _categoriesMode = false;
      ApplyTreeTabs();
    }

    if (CollectionsList.SelectedItem is not null)
    {
      _suppressLeft = true;
      CollectionsList.SelectedItem = null;
      _suppressLeft = false;
    }
  }

  private void FavoriteSelected(object? sender, SelectionChangedEventArgs e)
  {
    if (_suppressLeft || FavoritesList.SelectedItem is not FavoriteItem fav)
      return;
    _suppressLeft = true;
    FavoritesList.SelectedItem = null;
    _suppressLeft = false;
    EnterFolderScope();
    if (!TrySelectDirectory(fav.Path))
      StatusText.Text = "That favorite folder is not in the open catalog: " + fav.Path;
  }

  /// <summary>
  ///   Category / collection scopes: the files to list (filtered by type chip and search), or null for the folder scope.
  /// </summary>
  private int[]? ScopeFiles(GameCatalog catalog, AssetFilter filter, string? search, bool showAllFiles, out string label)
  {
    label = "";
    int[] source;
    if (_scope == Scope.Category && _category is { } node)
    {
      source = node.AllFiles;
      label = "Categories › " + node.Key.Replace("/", " › ");
    }
    else if (_scope == Scope.Collection && _collection is { } col)
    {
      var list = new List<int>();
      foreach (var path in col.Data.Paths)
        if (catalog.FindByPath(path) is { Count: > 0 } hits)
          list.Add(hits[0]);
      source = list.ToArray();
      label = "Collection › " + col.Name;
    }
    else
      return null;

    var words = string.IsNullOrWhiteSpace(search) ? Array.Empty<string>() : search.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    var showAll = showAllFiles || filter != AssetFilter.All;
    var result = new List<int>(source.Length);
    foreach (var i in source)
    {
      var f = catalog.GetFile(i);
      if (!AssetClassification.Matches(filter, f.Kind))
        continue;
      if (words.Length > 0 && !GameCatalog.MatchesAll(f.Path, words))
        continue;
      if (!showAll && _scope == Scope.Category && !AssetClassification.IsPreviewable(f.Extension))
        continue;
      result.Add(i);
    }

    return result.ToArray();
  }

  private void UpdateScopeText(string? label)
  {
    ScopeText.Text = label is { Length: > 0 } ? label
      : _directory is null ? ""
      : "Folders › " + (_directory.Path.Length == 0 ? "(root)" : _directory.Path.Replace("/", " › "));
  }

  // ------------------------------------------------------------------------------------------ chips and view

  private void ChipClicked(object? sender, RoutedEventArgs e)
  {
    if (sender is ToggleButton { Tag: string tag } && int.TryParse(tag, out var index))
      FilterBox.SelectedIndex = index;
    SyncChips();
  }

  private void SyncChips()
  {
    var f = FilterBox.SelectedIndex < 0 ? 0 : FilterBox.SelectedIndex;
    foreach (var chip in new[] { ChipAll, ChipTextures, ChipModels, ChipAnims, ChipEffects, ChipText })
      chip.IsChecked = chip.Tag is string tag && tag == f.ToString();
    GridViewButton.IsChecked = _gridView;
    ListViewButton.IsChecked = !_gridView;
  }

  private void GridViewClicked(object? sender, RoutedEventArgs e) => SetGridView(true);

  private void ListViewClicked(object? sender, RoutedEventArgs e) => SetGridView(false);

  private void SetGridView(bool grid)
  {
    _gridView = grid;
    SyncChips();
    if (RecentList.IsVisible)
      return;
    ShowFiles();
    if (grid)
      RebuildGrid();
    else if (FileList.SelectedItem is { } selected)
      Dispatcher.UIThread.Post(() => FileList.ScrollIntoView(selected), DispatcherPriority.Background);
  }

  private void ApplyViewVisibility()
  {
    FileList.IsVisible = !_gridView;
    TileGrid.IsVisible = _gridView;
    FileColumnHeader.IsVisible = !_gridView;
    TileSizeSlider.IsEnabled = _gridView;
  }

  private void TileSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
  {
    if (TileGrid is null)
      return;
    AssetTile.TileSize = Math.Round(e.NewValue);
    RebuildGrid();
  }

  private void AnimateAllChanged(object? sender, RoutedEventArgs e)
  {
    AssetTile.AnimateAll = AnimateAllBox.IsChecked == true;
    _settings.AnimateAll = AssetTile.AnimateAll;
    AssetTile.RefreshAll();
  }

  private int Columns()
  {
    var width = TileGrid.Bounds.Width > 32 ? TileGrid.Bounds.Width : 600;
    return Math.Max(1, (int)((width - 22) / (AssetTile.TileSize + 6)));
  }

  /// <summary>Re-cuts the file list into tile rows (new list, tile size or width) and keeps the selection in view.</summary>
  private void RebuildGrid()
  {
    if (FileList.ItemsSource is not FileRowList rows)
    {
      TileGrid.ItemsSource = null;
      return;
    }

    _gridColumns = Columns();
    TileGrid.SelectedItem = null; // the row ListBox's own selection is unused (selection lives on FileList)
    TileGrid.ItemsSource = new TileRowList(rows, _gridColumns);
    var count = rows.Count;
    EmptyText.IsVisible = count == 0 && !RecentList.IsVisible;
    if (FileList.SelectedIndex >= 0)
    {
      var row = FileList.SelectedIndex / _gridColumns;
      Dispatcher.UIThread.Post(() => TileGrid.ScrollIntoView(row), DispatcherPriority.Background);
    }
  }

  // ------------------------------------------------------------------------------------------ tile input

  private void SelectOnly(int pos)
  {
    FileList.SelectedIndex = pos;
    _anchor = pos;
  }

  private void TilePressed(object? sender, PointerPressedEventArgs e)
  {
    TileGrid.Focus();
    var tile = (e.Source as Visual)?.FindAncestorOfType<AssetTile>(true);
    _dragStart = null;
    if (tile is null || tile.Position < 0)
      return;
    var point = e.GetCurrentPoint(TileGrid);
    var pos = tile.Position;
    if (point.Properties.IsRightButtonPressed)
    {
      if (!FileList.Selection.IsSelected(pos))
        SelectOnly(pos);
      return;
    }

    if (!point.Properties.IsLeftButtonPressed)
      return;
    var mods = e.KeyModifiers;
    _deferSelectOnly = false;
    if (mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Meta))
    {
      if (FileList.Selection.IsSelected(pos))
        FileList.Selection.Deselect(pos);
      else
        FileList.Selection.Select(pos);
      _anchor = pos;
    }
    else if (mods.HasFlag(KeyModifiers.Shift) && _anchor >= 0)
    {
      FileList.Selection.BeginBatchUpdate();
      FileList.Selection.Clear();
      FileList.Selection.SelectRange(Math.Min(_anchor, pos), Math.Max(_anchor, pos));
      FileList.Selection.EndBatchUpdate();
    }
    else if (FileList.Selection.IsSelected(pos) && FileList.Selection.Count > 1)
      _deferSelectOnly = true; // a drag keeps the multi-selection; a plain click selects only this one on release
    else
      SelectOnly(pos);

    _dragStart = e.GetPosition(TileGrid);
    _dragFrom = pos;
    e.Handled = true;
  }

  private void TileReleased(object? sender, PointerReleasedEventArgs e)
  {
    if (_deferSelectOnly && _dragFrom >= 0)
      SelectOnly(_dragFrom);
    _deferSelectOnly = false;
    _dragStart = null;
  }

  private void TileMoved(object? sender, PointerEventArgs e) => MaybeDrag(e, TileGrid);

  private async void MaybeDrag(PointerEventArgs e, Control host)
  {
    if (_dragStart is not { } start || !e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
      return;
    var delta = e.GetPosition(host) - start;
    if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 8 || _catalog is null)
      return;
    _dragStart = null;
    _deferSelectOnly = false;
    var paths = DragPaths();
    if (paths.Count == 0)
      return;
    var data = new DataObject();
    data.Set(AssetsFormat, string.Join("\n", paths));
    data.Set(DataFormats.Text, string.Join("\n", paths));
    StatusText.Text = paths.Count == 1 ? "Drop on a collection to add " + paths[0] : $"Drop on a collection to add {paths.Count} assets";
    await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Link);
  }

  private List<string> DragPaths()
  {
    if (FileList.ItemsSource is not FileRowList rows)
      return new();
    if (_dragFrom >= 0 && _dragFrom < rows.Count && !FileList.Selection.IsSelected(_dragFrom))
      return new() { rows[_dragFrom].Path };
    return SelectedRows().Select(r => r.Path).ToList();
  }

  private List<FileRow> SelectedRows()
    => FileList.SelectedItems?.OfType<FileRow>().ToList() ?? new();

  private void TileKeyDown(object? sender, KeyEventArgs e)
  {
    if (FileList.ItemsSource is not FileRowList rows || rows.Count == 0)
      return;
    var cols = Math.Max(1, _gridColumns);
    var cur = FileList.SelectedIndex;
    var next = e.Key switch
    {
      Key.Left => cur - 1,
      Key.Right => cur + 1,
      Key.Up => cur - cols,
      Key.Down => cur < 0 ? 0 : cur + cols,
      Key.Home => 0,
      Key.End => rows.Count - 1,
      Key.PageDown => cur + cols * 4,
      Key.PageUp => cur - cols * 4,
      _ => int.MinValue,
    };
    if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
    {
      FileList.SelectAll();
      e.Handled = true;
      return;
    }

    if (e.Key == Key.Delete && _scope == Scope.Collection)
    {
      RemoveFromCollection();
      e.Handled = true;
      return;
    }

    if (next == int.MinValue)
      return;
    next = Math.Clamp(next, 0, rows.Count - 1);
    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _anchor >= 0)
    {
      FileList.Selection.BeginBatchUpdate();
      FileList.Selection.Clear();
      FileList.Selection.SelectRange(Math.Min(_anchor, next), Math.Max(_anchor, next));
      FileList.Selection.EndBatchUpdate();
    }
    else
      SelectOnly(next);
    TileGrid.ScrollIntoView(next / cols);
    e.Handled = true;
  }

  // ------------------------------------------------------------------------------------------ menus

  private static MenuItem Item(string header, Action action, bool enabled = true)
  {
    var item = new MenuItem { Header = header, IsEnabled = enabled };
    item.Click += (_, _) => action();
    return item;
  }

  private static Control Dot(string color)
    => new Avalonia.Controls.Shapes.Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.TryParse(color, out var c) ? c : Colors.Orange) };

  private void ShowAssetMenu(Control target)
  {
    var rows = SelectedRows();
    if (rows.Count == 0 || _catalog is null)
      return;
    var menu = new ContextMenu();
    if (target == TileGrid)
      TileGrid.SelectedItem = null;
    var add = new MenuItem { Header = rows.Count == 1 ? "Add to collection" : $"Add {rows.Count} to collection" };
    foreach (var col in _collections)
    {
      var c = col;
      var mi = Item(c.Name, () => AddToCollection(c, rows.Select(r => r.Path)));
      mi.Icon = Dot(c.Data.Color);
      add.Items.Add(mi);
    }

    if (_collections.Count > 0)
      add.Items.Add(new Separator());
    add.Items.Add(Item("New collection…", async () => await NewCollectionAsync(rows.Select(r => r.Path).ToList())));
    menu.Items.Add(add);
    if (_scope == Scope.Collection && _collection is { } current)
      menu.Items.Add(Item($"Remove from '{current.Name}'", RemoveFromCollection));
    menu.Items.Add(new Separator());
    menu.Items.Add(Item("Show in folder", () =>
    {
      var r = rows[^1];
      OpenRecent(new RecentEntry { Path = r.Path, Archive = r.Archive });
    }, rows.Count >= 1));
    menu.Items.Add(Item(rows.Count == 1 ? "Copy path" : "Copy paths", async () =>
    {
      if (Clipboard is { } clip)
        await clip.SetTextAsync(string.Join(Environment.NewLine, rows.Select(r => r.Path)));
    }));
    if (_categories is not null && rows.Count == 1 && _categories.Reason(rows[0].Index) is { } why)
      menu.Items.Add(new MenuItem { Header = "Category: " + why, IsEnabled = false });
    menu.Items.Add(new Separator());
    menu.Items.Add(Item(rows.Count == 1 ? "Export…" : $"Export {rows.Count} files…", () => ExportClicked(ExportButton, new RoutedEventArgs()), ExportButton.IsEnabled));
    menu.Open(target);
  }

  private void ShowFolderMenu()
  {
    if (_directory is null)
      return;
    var path = _directory.Path;
    var menu = new ContextMenu();
    var pinned = _favorites.Any(f => f.Path == path);
    menu.Items.Add(pinned
      ? Item("Remove from Favorites", () => { foreach (var f in _favorites.Where(f => f.Path == path).ToList()) _favorites.Remove(f); SaveLeftPane(); })
      : Item("Pin to Favorites ★", () => PinFolder(path)));
    menu.Open(FolderTree);
  }

  private void ShowCollectionMenu(CollectionItem? item)
  {
    var menu = new ContextMenu();
    menu.Items.Add(Item("New collection…", async () => await NewCollectionAsync(new List<string>())));
    if (item is not null)
    {
      menu.Items.Add(new Separator());
      menu.Items.Add(Item("Rename…", async () =>
      {
        var name = await PromptAsync("Rename collection", item.Name);
        if (string.IsNullOrWhiteSpace(name))
          return;
        item.Data.Name = UniqueCollectionName(name.Trim(), item);
        item.Changed();
        SaveLeftPane();
        if (_scope == Scope.Collection && _collection == item)
          UpdateScopeText("Collection › " + item.Name);
      }));
      var colour = new MenuItem { Header = "Colour" };
      foreach (var hex in CollectionItem.Palette)
      {
        var h = hex;
        var mi = Item(h, () => { item.Data.Color = h; item.Changed(); SaveLeftPane(); });
        mi.Icon = Dot(h);
        colour.Items.Add(mi);
      }

      menu.Items.Add(colour);
      menu.Items.Add(Item("Delete…", async () =>
      {
        if (!await ConfirmAsync($"Delete the collection '{item.Name}' ({item.Data.Paths.Count:N0} items)? The assets themselves are not touched."))
          return;
        _collections.Remove(item);
        SaveLeftPane();
        if (_collection == item)
        {
          _collection = null;
          SetScope(_categoriesMode && _category is not null ? Scope.Category : Scope.Folder);
        }
      }));
    }

    menu.Open(CollectionsList);
  }

  private void PinFolderClicked(object? sender, RoutedEventArgs e)
  {
    if (_directory is null)
    {
      StatusText.Text = "Select a folder in the Folders tree first.";
      return;
    }

    PinFolder(_directory.Path);
  }

  private void PinFolder(string path)
  {
    if (_favorites.Any(f => f.Path == path))
      return;
    _favorites.Add(new FavoriteItem(path));
    SaveLeftPane();
  }

  private async void NewCollectionClicked(object? sender, RoutedEventArgs e)
    => await NewCollectionAsync(SelectedRows().Select(r => r.Path).ToList());

  private async Task NewCollectionAsync(List<string> paths)
  {
    var name = await PromptAsync(paths.Count > 0 ? $"New collection ({paths.Count} assets)" : "New collection", "Collection " + (_collections.Count + 1));
    if (string.IsNullOrWhiteSpace(name))
      return;
    var data = new CollectionData
    {
      Name = UniqueCollectionName(name.Trim(), null),
      Color = CollectionItem.Palette[_collections.Count % CollectionItem.Palette.Length],
      Paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
    };
    var item = new CollectionItem(data);
    _collections.Add(item);
    SaveLeftPane();
    StatusText.Text = $"Collection '{data.Name}' created with {data.Paths.Count:N0} assets.";
  }

  private string UniqueCollectionName(string name, CollectionItem? self)
  {
    var candidate = name;
    for (var n = 2; _collections.Any(c => c != self && string.Equals(c.Name, candidate, StringComparison.OrdinalIgnoreCase)); n++)
      candidate = $"{name} ({n})";
    return candidate;
  }

  private void AddToCollection(CollectionItem item, IEnumerable<string> paths)
  {
    var have = new HashSet<string>(item.Data.Paths, StringComparer.OrdinalIgnoreCase);
    var added = 0;
    foreach (var p in paths)
      if (have.Add(p))
      {
        item.Data.Paths.Add(p);
        added++;
      }

    item.Changed();
    SaveLeftPane();
    StatusText.Text = added == 0 ? $"Already in '{item.Name}'." : $"Added {added:N0} to '{item.Name}' ({item.Data.Paths.Count:N0} items).";
    if (_scope == Scope.Collection && _collection == item)
      RefreshFiles();
  }

  private void RemoveFromCollection()
  {
    if (_collection is not { } item)
      return;
    var gone = new HashSet<string>(SelectedRows().Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
    if (gone.Count == 0)
      return;
    item.Data.Paths.RemoveAll(gone.Contains);
    item.Changed();
    SaveLeftPane();
    StatusText.Text = $"Removed {gone.Count:N0} from '{item.Name}'.";
    RefreshFiles();
  }

  private void CollectionDragOver(object? sender, DragEventArgs e)
  {
    var target = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true)?.DataContext as CollectionItem;
    e.DragEffects = e.Data.Contains(AssetsFormat) && target is not null ? DragDropEffects.Copy : DragDropEffects.None;
  }

  private void CollectionDrop(object? sender, DragEventArgs e)
  {
    if (!e.Data.Contains(AssetsFormat) || e.Data.Get(AssetsFormat) is not string text)
      return;
    var target = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true)?.DataContext as CollectionItem;
    if (target is null)
      return;
    AddToCollection(target, text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
  }

  private void SaveLeftPane()
  {
    _settings.Favorites = _favorites.Select(f => f.Path).ToList();
    _settings.Collections = _collections.Select(c => c.Data).ToList();
    try { _settings.Save(); } catch { /* best effort */ }
  }

  private void SaveContentBrowserState()
  {
    _settings.Favorites = _favorites.Select(f => f.Path).ToList();
    _settings.Collections = _collections.Select(c => c.Data).ToList();
    _settings.TreeMode = _categoriesMode ? "Categories" : "Folders";
    _settings.SelectedCategory = _category?.Key;
    _settings.SelectedCollection = _scope == Scope.Collection ? _collection?.Name : null;
    _settings.ViewMode = _gridView ? "Grid" : "List";
    _settings.TileSize = AssetTile.TileSize;
    _settings.AnimateAll = AssetTile.AnimateAll;
  }

  // ------------------------------------------------------------------------------------------ small dialogs

  private async Task<string?> PromptAsync(string title, string initial)
  {
    var box = new TextBox { Text = initial, Width = 300 };
    var ok = new Button { Content = "OK", Classes = { "accent" }, IsDefault = true, MinWidth = 80 };
    var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
    var dialog = new Window
    {
      Title = title,
      SizeToContent = SizeToContent.WidthAndHeight,
      CanResize = false,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new StackPanel
      {
        Margin = new Thickness(16),
        Spacing = 12,
        Children =
        {
          new TextBlock { Text = title },
          box,
          new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } },
        },
      },
    };
    string? result = null;
    ok.Click += (_, _) => { result = box.Text; dialog.Close(); };
    cancel.Click += (_, _) => dialog.Close();
    dialog.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
    await dialog.ShowDialog(this);
    return result;
  }

  private async Task<bool> ConfirmAsync(string text)
  {
    var yes = new Button { Content = "Delete", Classes = { "accent" }, MinWidth = 80 };
    var no = new Button { Content = "Cancel", IsCancel = true, IsDefault = true, MinWidth = 80 };
    var dialog = new Window
    {
      Title = "Forge Lens",
      SizeToContent = SizeToContent.WidthAndHeight,
      CanResize = false,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new StackPanel
      {
        Margin = new Thickness(16),
        Spacing = 12,
        Children =
        {
          new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 380 },
          new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { yes, no } },
        },
      },
    };
    var result = false;
    yes.Click += (_, _) => { result = true; dialog.Close(); };
    no.Click += (_, _) => dialog.Close();
    await dialog.ShowDialog(this);
    return result;
  }
}
