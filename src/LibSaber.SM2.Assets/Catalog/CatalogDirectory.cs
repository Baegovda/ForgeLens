namespace LibSaber.SM2.Assets.Catalog;

/// <summary>
///   A folder in the merged archive tree. Files are not children: they live in a
///   flat array addressed by <see cref="FirstFile"/> and <see cref="FileCount"/>.
/// </summary>
public sealed class CatalogDirectory
{

  public string Name { get; }
  public string Path { get; }
  public CatalogDirectory[] Children { get; }
  public int FirstFile { get; }
  public int FileCount { get; }

  /// <summary>
  ///   Direct files plus every file under a child folder. Those entries are contiguous
  ///   in the catalog, starting at <see cref="FirstFile"/>.
  /// </summary>
  public int SubtreeFileCount { get; }

  internal CatalogDirectory(string name, string path, CatalogDirectory[] children, int firstFile, int fileCount, int subtreeFileCount)
  {
    Name = name;
    Path = path;
    Children = children;
    FirstFile = firstFile;
    FileCount = fileCount;
    SubtreeFileCount = subtreeFileCount;
  }

}
