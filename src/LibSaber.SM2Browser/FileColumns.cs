using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LibSaber.SM2Browser;

/// <summary>
///   mtl1-42: widths of the file list's TYPE / SIZE / ARCHIVE columns. The header row has GridSplitters; every row binds
///   its fixed columns here, so dragging a header splitter resizes the whole list. PATH takes the rest (star), so it is
///   the wide column by default and grows / shrinks with the pane. Persisted in <see cref="BrowserSettings"/>.
/// </summary>
public sealed class FileColumns : INotifyPropertyChanged
{
  public static FileColumns Shared { get; } = new();

  public const double DefaultType = 72, DefaultSize = 64, DefaultArchive = 112, MinWidth = 36;

  private double _type = DefaultType, _size = DefaultSize, _archive = DefaultArchive;

  public double TypeWidth { get => _type; set => Set(ref _type, value); }
  public double SizeWidth { get => _size; set => Set(ref _size, value); }
  public double ArchiveWidth { get => _archive; set => Set(ref _archive, value); }

  public event PropertyChangedEventHandler? PropertyChanged;

  private void Set(ref double field, double value, [CallerMemberName] string? name = null)
  {
    value = double.IsFinite(value) ? Math.Max(MinWidth, Math.Round(value)) : field;
    if (Math.Abs(field - value) < 0.5)
      return;
    field = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}
