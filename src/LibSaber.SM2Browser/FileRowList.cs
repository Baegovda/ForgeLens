using System.Collections;
using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2Browser;

public sealed class FileRow
{

  public FileRow(GameCatalog catalog, int index)
  {
    var file = catalog.GetFile(index);
    Index = index;
    Path = file.Path;
    Extension = file.Extension;
    SizeText = file.SizeText;
    Archive = file.ArchiveName;
    Kind = file.Kind.ToString();
  }

  public int Index { get; }
  public string Path { get; }
  public string Extension { get; }
  public string SizeText { get; }
  public string Archive { get; }
  public string Kind { get; }

}

/// <summary>
///   Rows are created the first time a virtualized list asks for them, then reused
///   so selection stays stable. A 178k mip folder does not allocate up front.
/// </summary>
public sealed class FileRowList : IReadOnlyList<FileRow>, IList
{

  private readonly GameCatalog _catalog;
  private readonly FileQuery _query;
  private readonly FileRow?[] _cache;

  public FileRowList(GameCatalog catalog, FileQuery query)
  {
    _catalog = catalog;
    _query = query;
    _cache = new FileRow[query.Length];
  }

  public int Count => _cache.Length;

  public FileRow this[int index]
    => _cache[index] ??= new FileRow(_catalog, _query.IndexAt(index));

  public IEnumerator<FileRow> GetEnumerator()
  {
    for (var i = 0; i < Count; i++)
      yield return this[i];
  }

  IEnumerator IEnumerable.GetEnumerator()
    => GetEnumerator();

  bool IList.IsFixedSize => true;
  bool IList.IsReadOnly => true;
  bool ICollection.IsSynchronized => false;
  object ICollection.SyncRoot => this;

  object? IList.this[int index]
  {
    get => this[index];
    set => throw new NotSupportedException();
  }

  int IList.Add(object? value) => throw new NotSupportedException();
  void IList.Clear() => throw new NotSupportedException();
  bool IList.Contains(object? value) => value is FileRow row && IndexOf(row) >= 0;
  int IList.IndexOf(object? value) => value is FileRow row ? IndexOf(row) : -1;
  void IList.Insert(int index, object? value) => throw new NotSupportedException();
  void IList.Remove(object? value) => throw new NotSupportedException();
  void IList.RemoveAt(int index) => throw new NotSupportedException();

  void ICollection.CopyTo(Array array, int index)
  {
    for (var i = 0; i < Count; i++)
      array.SetValue(this[i], index + i);
  }

  private int IndexOf(FileRow row)
  {
    for (var i = 0; i < Count; i++)
    {
      if (ReferenceEquals(_cache[i], row) || (_cache[i] is null && _query.IndexAt(i) == row.Index))
        return i;
    }

    return -1;
  }

}
