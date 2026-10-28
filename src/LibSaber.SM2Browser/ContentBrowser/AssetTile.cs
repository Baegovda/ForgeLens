using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2Browser.ContentBrowser;

/// <summary>One row of the tile grid: <see cref="Count"/> files starting at <see cref="Start"/> of the file list.</summary>
public sealed class TileRow
{
  public TileRow(FileRowList rows, int start, int count)
  {
    Rows = rows;
    Start = start;
    Count = count;
  }

  public FileRowList Rows { get; }
  public int Start { get; }
  public int Count { get; }
}

/// <summary>The file list cut into rows of <c>columns</c> tiles; rows are created when the virtualized grid asks.</summary>
public sealed class TileRowList : IReadOnlyList<TileRow>, IList
{
  private readonly FileRowList _rows;
  private readonly TileRow?[] _cache;

  public TileRowList(FileRowList rows, int columns)
  {
    _rows = rows;
    Columns = Math.Max(1, columns);
    _cache = new TileRow[(rows.Count + Columns - 1) / Columns];
  }

  public int Columns { get; }
  public FileRowList Files => _rows;
  public int Count => _cache.Length;

  public TileRow this[int index]
    => _cache[index] ??= new TileRow(_rows, index * Columns, Math.Min(Columns, _rows.Count - index * Columns));

  public IEnumerator<TileRow> GetEnumerator()
  {
    for (var i = 0; i < Count; i++)
      yield return this[i];
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  bool IList.IsFixedSize => true;
  bool IList.IsReadOnly => true;
  bool ICollection.IsSynchronized => false;
  object ICollection.SyncRoot => this;
  object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
  int IList.Add(object? value) => throw new NotSupportedException();
  void IList.Clear() => throw new NotSupportedException();
  bool IList.Contains(object? value) => ((IList)this).IndexOf(value) >= 0;

  int IList.IndexOf(object? value)
  {
    if (value is not TileRow r || r.Start < 0 || r.Start % Columns != 0)
      return -1;
    var i = r.Start / Columns;
    return i < _cache.Length && ReferenceEquals(_cache[i], r) ? i : -1;
  }
  void IList.Insert(int index, object? value) => throw new NotSupportedException();
  void IList.Remove(object? value) => throw new NotSupportedException();
  void IList.RemoveAt(int index) => throw new NotSupportedException();
  void ICollection.CopyTo(Array array, int index)
  {
    for (var i = 0; i < Count; i++)
      array.SetValue(this[i], index + i);
  }
}

/// <summary>A row of the grid: recycles its <see cref="AssetTile"/> children when the virtualized list reuses it.</summary>
public sealed class TileRowPanel : StackPanel
{
  public TileRowPanel()
  {
    Orientation = Orientation.Horizontal;
    Margin = new Thickness(2, 0);
  }

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (DataContext is not TileRow row)
    {
      foreach (var child in Children.OfType<AssetTile>())
        child.DataContext = null;
      return;
    }

    while (Children.Count < row.Count)
      Children.Add(new AssetTile());
    while (Children.Count > row.Count)
    {
      ((AssetTile)Children[^1]).DataContext = null;
      Children.RemoveAt(Children.Count - 1);
    }

    for (var i = 0; i < row.Count; i++)
    {
      var tile = (AssetTile)Children[i];
      tile.Position = row.Start + i;
      tile.DataContext = row.Rows[row.Start + i];
      tile.UpdateSelected();
    }
  }
}

/// <summary>
///   ui-43 Content-Browser tile: thumbnail (from <see cref="ThumbnailService"/>), a type-coloured bar, the asset name and
///   its type. Animations and effects loop their frame strip while hovered (or always with <see cref="AnimateAll"/>).
/// </summary>
public sealed class AssetTile : Border
{
  public static ThumbnailService? Service { get; set; }
  /// <summary>Is file-list row N selected.</summary>
  public static Func<int, bool>? IsSelectedIndex { get; set; }
  public static double TileSize { get; set; } = 112;
  public static bool AnimateAll { get; set; }

  private static readonly HashSet<AssetTile> Live = new();
  private static readonly DispatcherTimer Animator = new() { Interval = TimeSpan.FromMilliseconds(100) };
  private static ThumbnailService? _hooked;

  private readonly Image _image = new() { Stretch = Stretch.Uniform };
  private readonly TextBlock _glyph = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 30, Opacity = 0.55 };
  private readonly Border _bar = new() { Height = 3 };
  private readonly TextBlock _name = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 3, 5, 0) };
  private readonly TextBlock _kind = new() { FontSize = 10, Margin = new Thickness(5, 0, 5, 4) };
  private readonly Border _frame;
  private Thumb? _thumb;
  private int _index = -1;
  private int _frameNo;
  private bool _hover;

  static AssetTile()
  {
    Animator.Tick += (_, _) =>
    {
      var any = false;
      foreach (var tile in Live)
        any |= tile.Step();
      if (!any && !AnimateAll)
        Animator.Stop();
    };
  }

  // Styles in App.axaml target Border.tile: keep Border as the style key.
  protected override Type StyleKeyOverride => typeof(Border);

  public AssetTile()
  {
    Classes.Add("tile");
    CornerRadius = new CornerRadius(4);
    BorderThickness = new Thickness(1);
    Margin = new Thickness(3);
    _frame = new Border
    {
      CornerRadius = new CornerRadius(3, 3, 0, 0),
      ClipToBounds = true,
      Child = new Grid { Children = { _glyph, _image } },
    };
    var stack = new StackPanel { Children = { _frame, _bar, _name, _kind } };
    Child = stack;
    _kind.Foreground = new SolidColorBrush(Color.Parse("#9A9CA2"));
    ApplySize();
  }

  public static Color KindColor(string extension) => extension switch
  {
    ".pct" or ".pct.resource" or ".pct_mip" => Color.Parse("#D0574F"),
    ".tpl" or ".tpl_data" or ".tpl_markup" or ".lods_base" => Color.Parse("#4FA3E0"),
    ".sani" => Color.Parse("#57C27B"),
    ".sfx" => Color.Parse("#C77BE8"),
    _ => Color.Parse("#8C8F96"),
  };

  public static string KindLabel(string extension) => extension switch
  {
    ".pct" or ".pct.resource" => "Texture",
    ".tpl" => "Model",
    ".sani" => "Animation",
    ".sfx" => "Effect",
    _ => extension.TrimStart('.'),
  };

  private static string Glyph(string extension) => extension switch
  {
    ".pct" or ".pct.resource" => "▦",
    ".tpl" => "◆",
    ".sani" => "▶",
    ".sfx" => "✦",
    _ => "≡",
  };

  public static string DisplayName(string path)
  {
    var name = Path.GetFileName(path.Replace('\\', '/'));
    var ext = AssetClassification.CompoundExtension(name);
    return ext.Length > 0 && name.Length > ext.Length ? name[..^ext.Length] : name;
  }

  public FileRow? Row => DataContext as FileRow;

  /// <summary>Row of the file list this tile shows (selection lives on the list).</summary>
  public int Position { get; set; } = -1;

  private void ApplySize()
  {
    Width = TileSize;
    _frame.Width = TileSize - 2;
    _frame.Height = TileSize - 2;
    _name.IsVisible = TileSize >= 72;
    _kind.IsVisible = TileSize >= 88;
  }

  public static void RefreshAll()
  {
    foreach (var tile in Live)
    {
      tile.UpdateSelected();
      tile.ApplySize();
    }

    if (AnimateAll)
      Animator.Start();
  }

  /// <summary>Asks again for every visible tile that has no thumbnail yet (e.g. after the category tree finished).</summary>
  public static void RequestMissing()
  {
    foreach (var tile in Live)
      if (tile._index >= 0 && tile._thumb is null && tile.DataContext is FileRow row && Service is not null
          && ThumbnailService.HasThumbnail(row.Extension) && Service.TryGet(row.Index, out var thumb))
        tile.SetThumb(thumb);
  }

  public void UpdateSelected()
    => Classes.Set("selected", _index >= 0 && Position >= 0 && IsSelectedIndex?.Invoke(Position) == true);

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);
    Live.Add(this);
    if (!ReferenceEquals(_hooked, Service) && Service is not null)
    {
      _hooked = Service;
      Service.Ready += (index, thumb) =>
      {
        foreach (var tile in Live)
          if (tile._index == index)
            tile.SetThumb(thumb);
      };
    }

    ApplySize();
    UpdateSelected();
    if (AnimateAll)
      Animator.Start();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
  {
    base.OnDetachedFromVisualTree(e);
    Live.Remove(this);
    if (_index >= 0 && _thumb is null)
      Service?.Cancel(_index);
  }

  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);
    if (_index >= 0 && _thumb is null)
      Service?.Cancel(_index);
    _thumb = null;
    _image.Source = null;
    _frameNo = 0;
    if (DataContext is not FileRow row)
    {
      _index = -1;
      return;
    }

    _index = row.Index;
    _name.Text = DisplayName(row.Path);
    _kind.Text = KindLabel(row.Extension);
    var color = KindColor(row.Extension);
    _bar.Background = new SolidColorBrush(color);
    _glyph.Text = Glyph(row.Extension);
    _glyph.Foreground = new SolidColorBrush(color);
    _glyph.IsVisible = true;
    ToolTip.SetTip(this, row.Path + "\n" + KindLabel(row.Extension) + " · " + row.SizeText + " · " + row.Archive);
    UpdateSelected();
    if (Service is not null && ThumbnailService.HasThumbnail(row.Extension) && Service.TryGet(row.Index, out var thumb))
      SetThumb(thumb);
  }

  private void SetThumb(Thumb? thumb)
  {
    _thumb = thumb;
    _image.Source = thumb?.Still;
    _glyph.IsVisible = thumb is null;
    if (thumb is { Frames: > 1 } && (_hover || AnimateAll))
      Animator.Start();
  }

  private bool Step()
  {
    if (_thumb is not { Frames: > 1 } thumb || !(_hover || AnimateAll))
      return false;
    _frameNo = (_frameNo + 1) % thumb.Frames;
    _image.Source = thumb.Images[_frameNo];
    return true;
  }

  protected override void OnPointerEntered(PointerEventArgs e)
  {
    base.OnPointerEntered(e);
    _hover = true;
    Classes.Set("hover", true);
    if (_thumb is { Frames: > 1 })
      Animator.Start();
  }

  protected override void OnPointerExited(PointerEventArgs e)
  {
    base.OnPointerExited(e);
    _hover = false;
    Classes.Set("hover", false);
    if (_thumb is { } thumb && !AnimateAll)
      _image.Source = thumb.Still;
  }
}
