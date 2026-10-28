using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2Browser.ContentBrowser;

/// <summary>A user collection as saved in settings.json: a name, a colour and asset paths (archive-relative).</summary>
public sealed class CollectionData
{
  public string Name { get; set; } = "Collection";
  public string Color { get; set; } = "#E8892B";
  public List<string> Paths { get; set; } = new();
}

public abstract class Observable : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;
  protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CollectionItem : Observable
{
  public static readonly string[] Palette = { "#E8892B", "#D0574F", "#4FA3E0", "#57C27B", "#C77BE8", "#E0C34F", "#4FD0C4", "#9A9CA2" };

  public CollectionItem(CollectionData data) => Data = data;

  public CollectionData Data { get; }
  public string Name => Data.Name;
  public IBrush Brush => new SolidColorBrush(Avalonia.Media.Color.TryParse(Data.Color, out var c) ? c : Colors.Orange);
  public string CountText => Data.Paths.Count.ToString("N0");

  public void Changed()
  {
    Raise(nameof(Name));
    Raise(nameof(Brush));
    Raise(nameof(CountText));
  }
}

public sealed class FavoriteItem
{
  public FavoriteItem(string path) => Path = path;
  public string Path { get; }
  public string Name => Path.Length == 0 ? "(root)" : Path[(Path.LastIndexOf('/') + 1)..];
  public string Tip => Path.Length == 0 ? "Archive root" : Path;
}

/// <summary>Category tree node for the TreeView (children are built when first expanded).</summary>
public sealed class CategoryItem : Observable
{
  private ObservableCollection<CategoryItem>? _children;
  private bool _expanded;

  public CategoryItem(CategoryNode node) => Node = node;

  public CategoryNode Node { get; }
  public string Name => Node.Name;
  public string CountText => Node.TotalCount.ToString("N0");
  public bool Heuristic => Node.Heuristic;
  public string Tip => (Node.Heuristic ? "HEURISTIC (name / keyword rule). " : "") + (Node.Note ?? Node.Key);

  public ObservableCollection<CategoryItem> Children
    => _children ??= new ObservableCollection<CategoryItem>(Node.Children.Select(c => new CategoryItem(c)));

  public bool IsExpanded
  {
    get => _expanded;
    set
    {
      if (_expanded == value) return;
      _expanded = value;
      Raise();
    }
  }
}
