namespace LibSaber.SM2.Assets.Catalog;

public readonly struct CatalogFileInfo
{

  public int Index { get; init; }
  public string Path { get; init; }
  public string Name { get; init; }
  public string Extension { get; init; }
  public string ArchiveName { get; init; }
  public int ArchiveId { get; init; }
  public long Size { get; init; }
  public long CompressedSize { get; init; }
  public string Method { get; init; }
  public AssetKind Kind { get; init; }
  public bool PayloadAvailable { get; init; }

  public string SizeText => AssetClassification.FormatSize(Size);

}

public readonly struct FileQuery
{

  public int First { get; }
  public int Count { get; }
  public int[]? Indices { get; }

  public FileQuery(int first, int count, int[]? indices)
  {
    First = first;
    Count = count;
    Indices = indices;
  }

  public int Length => Indices?.Length ?? Count;

  public int IndexAt(int row)
    => Indices is null ? First + row : Indices[row];

}
