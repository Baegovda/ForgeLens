using LibSaber.IO;
using LibSaber.SpaceMarine2.Serialization;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SM2.Assets.Catalog;

public readonly record struct CatalogProgress(int ArchivesDone, int ArchiveCount, int FilesIndexed, string CurrentArchive);

public sealed class ArchiveRecord
{

  public string Name { get; }
  public string PakPath { get; }
  public bool HasPayload { get; }
  public int FileCount { get; }

  internal ArchiveRecord(string name, string pakPath, bool hasPayload, int fileCount)
  {
    Name = name;
    PakPath = pakPath;
    HasPayload = hasPayload;
    FileCount = fileCount;
  }

}

/// <summary>
///   Merged directory tree built from .pak.cache / .pck.cache files only.
///   Payloads stay on disk until a preview or export asks for one file.
/// </summary>
public sealed class GameCatalog
{

  private readonly CatalogFile[] _files;
  private readonly string[] _paths;
  private readonly string[] _names;
  private readonly string[] _extensions;
  private readonly Dictionary<string, int> _pathFirst;
  private readonly Dictionary<string, int[]> _pathAll;
  private readonly Dictionary<string, int> _nameFirst;
  private readonly Dictionary<string, int[]> _nameAll;

  public string RootPath { get; }
  public IReadOnlyList<ArchiveRecord> Archives { get; }
  public CatalogDirectory Root { get; }
  public int FileCount => _files.Length;
  public int ArchivesSkipped { get; }
  public IReadOnlyList<string> Warnings { get; }

  private GameCatalog(
    string rootPath,
    ArchiveRecord[] archives,
    CatalogDirectory root,
    CatalogFile[] files,
    string[] paths,
    string[] names,
    string[] extensions,
    Dictionary<string, int> pathFirst,
    Dictionary<string, int[]> pathAll,
    Dictionary<string, int> nameFirst,
    Dictionary<string, int[]> nameAll,
    int archivesSkipped,
    IReadOnlyList<string> warnings)
  {
    RootPath = rootPath;
    Archives = archives;
    Root = root;
    _files = files;
    _paths = paths;
    _names = names;
    _extensions = extensions;
    _pathFirst = pathFirst;
    _pathAll = pathAll;
    _nameFirst = nameFirst;
    _nameAll = nameAll;
    ArchivesSkipped = archivesSkipped;
    Warnings = warnings;
  }

  public CatalogFileInfo GetFile(int index)
  {
    var file = _files[index];
    var archive = Archives[file.ArchiveId];
    var extension = _extensions[file.ExtensionId];
    return new CatalogFileInfo
    {
      Index = index,
      Path = _paths[file.PathId],
      Name = _names[file.NameId],
      Extension = extension,
      ArchiveName = archive.Name,
      ArchiveId = file.ArchiveId,
      Size = file.Size,
      CompressedSize = file.CompressedSize,
      Method = MethodName(file.Method),
      Kind = AssetClassification.Classify(extension),
      PayloadAvailable = archive.HasPayload,
    };
  }

  public FileQuery Query(CatalogDirectory? directory, string? nameContains, AssetFilter filter)
  {
    var text = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
    var global = text is not null;
    if (!global && filter == AssetFilter.All && directory is not null)
      return new FileQuery(directory.FirstFile, directory.FileCount, null);

    var matches = new List<int>();
    if (!global && directory is not null)
    {
      var end = directory.FirstFile + directory.FileCount;
      for (var i = directory.FirstFile; i < end; i++)
      {
        if (AssetClassification.Matches(filter, KindOf(i)))
          matches.Add(i);
      }
    }
    else
    {
      for (var i = 0; i < _files.Length; i++)
      {
        if (!AssetClassification.Matches(filter, KindOf(i)))
          continue;
        if (text is not null && _paths[_files[i].PathId].IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
          continue;
        matches.Add(i);
      }
    }

    return new FileQuery(0, matches.Count, matches.ToArray());
  }

  public bool TryGetByPath(string path, out int index)
  {
    var key = Normalize(path);
    return _pathFirst.TryGetValue(key, out index);
  }

  public IReadOnlyList<int> FindByPath(string path)
  {
    var key = Normalize(path);
    if (_pathAll.TryGetValue(key, out var all))
      return all;
    if (_pathFirst.TryGetValue(key, out var index))
      return new[] { index };
    return Array.Empty<int>();
  }

  public IReadOnlyList<int> FindByFileName(string fileName)
  {
    var name = Path.GetFileName(fileName.Replace('\\', '/'));
    if (_nameAll.TryGetValue(name, out var all))
      return all;
    if (_nameFirst.TryGetValue(name, out var index))
      return new[] { index };
    return Array.Empty<int>();
  }

  /// <summary>
  ///   Prefer the same archive and the same folder, then any file with that name.
  /// </summary>
  public int ResolveReference(string reference, int hintFileIndex)
  {
    var normalized = Normalize(reference);
    if (_pathFirst.TryGetValue(normalized, out var exact))
      return exact;

    var hits = FindByFileName(normalized);
    if (hits.Count == 0)
      return -1;
    if (hits.Count == 1 || hintFileIndex < 0)
      return hits[0];

    var hint = _files[hintFileIndex];
    var hintDir = DirectoryOf(_paths[hint.PathId]);
    var best = hits[0];
    foreach (var hit in hits)
    {
      var file = _files[hit];
      if (file.ArchiveId == hint.ArchiveId && DirectoryOf(_paths[file.PathId]) == hintDir)
        return hit;
    }

    foreach (var hit in hits)
    {
      if (_files[hit].ArchiveId == hint.ArchiveId)
        return hit;
    }

    return best;
  }

  internal void GetExtent(int index, out int archiveId, out long offset, out long size, out long compressedSize, out byte method)
  {
    var file = _files[index];
    archiveId = file.ArchiveId;
    offset = file.Offset;
    size = file.Size;
    compressedSize = file.CompressedSize;
    method = file.Method;
  }

  public static GameCatalog Load(
    string directory,
    IProgress<CatalogProgress>? progress = null,
    CancellationToken cancellation = default,
    bool recursive = true)
  {
    if (!Directory.Exists(directory))
      throw new DirectoryNotFoundException($"Folder not found: {directory}");

    var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
    var caches = Directory.EnumerateFiles(directory, "*.cache", option)
      .Where(path =>
      {
        var ext = Path.GetExtension(path[..^".cache".Length]);
        return ext.Equals(".pak", StringComparison.OrdinalIgnoreCase)
          || ext.Equals(".pck", StringComparison.OrdinalIgnoreCase);
      })
      .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
      .ToArray();

    var archives = new List<ArchiveRecord>(caches.Length);
    var raw = new List<RawFile>(capacity: 1024);
    var warnings = new List<string>();
    var skipped = 0;
    var extensionLookup = new Dictionary<string, ushort>(StringComparer.Ordinal);
    var extensions = new List<string> { string.Empty };

    for (var i = 0; i < caches.Length; i++)
    {
      cancellation.ThrowIfCancellationRequested();
      var cachePath = caches[i];
      var pakPath = cachePath[..^".cache".Length];
      var archiveName = Path.GetFileName(pakPath);
      progress?.Report(new CatalogProgress(i, caches.Length, raw.Count, archiveName));

      fioZIP_CACHE_FILE cache;
      try
      {
        using var stream = File.Open(cachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var reader = new NativeReader(stream, Endianness.LittleEndian);
        cache = Serializer<fioZIP_CACHE_FILE>.Deserialize(reader);
      }
      catch (Exception ex)
      {
        skipped++;
        if (warnings.Count < 40)
          warnings.Add($"{archiveName}.cache: {ex.Message}");
        continue;
      }

      var archiveId = (ushort)archives.Count;
      var hasPayload = File.Exists(pakPath);
      var added = 0;
      foreach (var entry in cache.Values)
      {
        if (string.IsNullOrEmpty(entry.FileName))
          continue;

        var path = Normalize(entry.FileName);
        if (path.Length == 0)
          continue;

        var name = Path.GetFileName(path);
        var extension = AssetClassification.CompoundExtension(name);
        if (!extensionLookup.TryGetValue(extension, out var extensionId))
        {
          extensionId = (ushort)extensions.Count;
          extensions.Add(extension);
          extensionLookup.Add(extension, extensionId);
        }

        raw.Add(new RawFile
        {
          Path = path,
          Name = name,
          ArchiveId = archiveId,
          ExtensionId = extensionId,
          Offset = entry.Offset,
          Size = entry.Size,
          CompressedSize = entry.CompressedSize,
          Method = (byte)entry.CompressMethod,
        });
        added++;
      }

      archives.Add(new ArchiveRecord(archiveName, pakPath, hasPayload, added));
    }

    progress?.Report(new CatalogProgress(caches.Length, caches.Length, raw.Count, "Building folders"));

    var rootNode = new DirNode(Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "");
    for (var i = 0; i < raw.Count; i++)
      Insert(rootNode, raw[i].Path, i);

    var files = new CatalogFile[raw.Count];
    var paths = new string[raw.Count];
    var names = new string[raw.Count];
    var next = 0;
    var root = Flatten(rootNode, raw, files, paths, names, ref next);

    var pathFirst = new Dictionary<string, int>(raw.Count, StringComparer.OrdinalIgnoreCase);
    var nameFirst = new Dictionary<string, int>(raw.Count, StringComparer.OrdinalIgnoreCase);
    var pathLists = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
    var nameLists = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < files.Length; i++)
    {
      var path = paths[files[i].PathId];
      var name = names[files[i].NameId];
      if (!pathFirst.ContainsKey(path))
        pathFirst.Add(path, i);
      if (!nameFirst.ContainsKey(name))
        nameFirst.Add(name, i);
      AddMulti(pathLists, path, i);
      AddMulti(nameLists, name, i);
    }

    return new GameCatalog(
      directory,
      archives.ToArray(),
      root,
      files,
      paths,
      names,
      extensions.ToArray(),
      pathFirst,
      Freeze(pathLists),
      nameFirst,
      Freeze(nameLists),
      skipped,
      warnings);
  }

  private AssetKind KindOf(int index)
    => AssetClassification.Classify(_extensions[_files[index].ExtensionId]);

  private static void AddMulti(Dictionary<string, List<int>> map, string key, int index)
  {
    if (!map.TryGetValue(key, out var list))
    {
      list = new List<int>(1);
      map.Add(key, list);
    }

    list.Add(index);
  }

  private static Dictionary<string, int[]> Freeze(Dictionary<string, List<int>> map)
  {
    var frozen = new Dictionary<string, int[]>(map.Count, map.Comparer);
    foreach (var pair in map)
    {
      if (pair.Value.Count > 1)
        frozen.Add(pair.Key, pair.Value.ToArray());
    }

    return frozen;
  }

  private static void Insert(DirNode root, string path, int rawIndex)
  {
    var node = root;
    var start = 0;
    for (var i = 0; i <= path.Length; i++)
    {
      if (i != path.Length && path[i] != '/')
        continue;

      if (i == path.Length)
      {
        node.Files.Add(rawIndex);
        return;
      }

      var length = i - start;
      if (length > 0)
      {
        var segment = path.Substring(start, length);
        if (segment != ".")
        {
          if (!node.Children.TryGetValue(segment, out var child))
          {
            var childPath = node.Path.Length == 0 ? segment : node.Path + "/" + segment;
            child = new DirNode(segment, childPath);
            node.Children.Add(segment, child);
          }

          node = child;
        }
      }

      start = i + 1;
    }
  }

  private static CatalogDirectory Flatten(
    DirNode node,
    List<RawFile> raw,
    CatalogFile[] files,
    string[] paths,
    string[] names,
    ref int next)
  {
    var first = next;
    if (node.Files.Count > 1)
      node.Files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(raw[a].Name, raw[b].Name));

    foreach (var rawIndex in node.Files)
    {
      var source = raw[rawIndex];
      paths[next] = source.Path;
      names[next] = source.Name;
      files[next] = new CatalogFile
      {
        PathId = next,
        NameId = next,
        ArchiveId = source.ArchiveId,
        ExtensionId = source.ExtensionId,
        Offset = source.Offset,
        Size = source.Size,
        CompressedSize = source.CompressedSize,
        Method = source.Method,
      };
      next++;
    }

    var children = new CatalogDirectory[node.Children.Count];
    var childIndex = 0;
    foreach (var child in node.Children.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
      children[childIndex++] = Flatten(child, raw, files, paths, names, ref next);

    return new CatalogDirectory(node.Name, node.Path, children, first, node.Files.Count);
  }

  internal static string Normalize(string path)
  {
    if (string.IsNullOrEmpty(path))
      return string.Empty;

    var slash = path.IndexOf('\\');
    var trimmed = path[0] == '/' || path[0] == '\\' ? path.TrimStart('/', '\\') : path;
    return slash < 0 && ReferenceEquals(trimmed, path) ? path : trimmed.Replace('\\', '/');
  }

  private static string DirectoryOf(string path)
  {
    var slash = path.LastIndexOf('/');
    return slash < 0 ? string.Empty : path[..slash];
  }

  private static string MethodName(byte method)
    => method switch
    {
      0 => "store",
      8 => "deflate",
      9 => "deflate64",
      _ => method.ToString(),
    };

  private struct CatalogFile
  {
    public int PathId;
    public int NameId;
    public ushort ArchiveId;
    public ushort ExtensionId;
    public long Offset;
    public long Size;
    public long CompressedSize;
    public byte Method;
  }

  private struct RawFile
  {
    public string Path;
    public string Name;
    public ushort ArchiveId;
    public ushort ExtensionId;
    public long Offset;
    public long Size;
    public long CompressedSize;
    public byte Method;
  }

  private sealed class DirNode
  {
    public readonly string Name;
    public readonly string Path;
    public readonly Dictionary<string, DirNode> Children = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<int> Files = new();

    public DirNode(string name, string path)
    {
      Name = name;
      Path = path;
    }
  }

}
