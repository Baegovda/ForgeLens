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
  private readonly string[] _extensions;
  // R87: sorted index arrays replace the per-file name strings and the four path/name dictionaries
  // (measured catalog heap before: 222.8 MB for 511,392 files). Lookups are binary searches (OrdinalIgnoreCase).
  private readonly int[] _byPath; // file indices ordered by path
  private readonly int[] _byName; // file indices ordered by file name, then index

  public string RootPath { get; }
  public IReadOnlyList<ArchiveRecord> Archives { get; }
  public CatalogDirectory Root { get; }
  public int FileCount => _files.Length;
  public int ArchivesSkipped { get; }

  /// <summary>
  ///   Identical internal paths were collapsed. The last archive in path order is the one kept.
  /// </summary>
  public int DuplicatesCollapsed { get; }

  public IReadOnlyList<string> Warnings { get; }

  private GameCatalog(
    string rootPath,
    ArchiveRecord[] archives,
    CatalogDirectory root,
    CatalogFile[] files,
    string[] paths,
    string[] extensions,
    int[] byPath,
    int[] byName,
    int archivesSkipped,
    int duplicatesCollapsed,
    IReadOnlyList<string> warnings)
  {
    RootPath = rootPath;
    Archives = archives;
    Root = root;
    _files = files;
    _paths = paths;
    _extensions = extensions;
    _byPath = byPath;
    _byName = byName;
    ArchivesSkipped = archivesSkipped;
    DuplicatesCollapsed = duplicatesCollapsed;
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
      Path = _paths[index],
      Name = NameOf(_paths[index]),
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

  public FileQuery Query(CatalogDirectory? directory, string? nameContains, AssetFilter filter, bool includeSubfolders = false)
    => Query(directory, nameContains, filter, includeSubfolders, previewableOnly: false, out _);

  /// <summary>
  ///   mtl1-42: <paramref name="previewableOnly"/> keeps only files the browser preview renders
  ///   (<see cref="AssetClassification.IsPreviewable"/>); <paramref name="hidden"/> counts the matches it left out.
  /// </summary>
  public FileQuery Query(CatalogDirectory? directory, string? nameContains, AssetFilter filter, bool includeSubfolders, bool previewableOnly, out int hidden)
  {
    hidden = 0;
    var text = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
    // vfx4-3: several words (space separated) must all occur in the path, in any order ("bolter muzzle").
    var words = text?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
    var global = text is not null;
    var span = directory is null
      ? 0
      : includeSubfolders ? directory.SubtreeFileCount : directory.FileCount;
    if (!global && filter == AssetFilter.All && directory is not null && !previewableOnly)
      return new FileQuery(directory.FirstFile, span, null);
    var hiddenCount = 0;
    bool Keep(int i)
    {
      if (!previewableOnly || AssetClassification.IsPreviewable(_extensions[_files[i].ExtensionId]))
        return true;
      hiddenCount++;
      return false;
    }

    var matches = new List<int>();
    if (!global && directory is not null)
    {
      var end = directory.FirstFile + span;
      for (var i = directory.FirstFile; i < end; i++)
      {
        if (AssetClassification.Matches(filter, KindOf(i)) && Keep(i))
          matches.Add(i);
      }
    }
    else
    {
      for (var i = 0; i < _files.Length; i++)
      {
        if (!AssetClassification.Matches(filter, KindOf(i)))
          continue;
        if (text is not null && !MatchesAll(_paths[i], words))
          continue;
        if (Keep(i))
          matches.Add(i);
      }
    }

    hidden = hiddenCount;
    return new FileQuery(0, matches.Count, matches.ToArray());
  }

  /// <summary>True when every word occurs in <paramref name="path"/> (case-insensitive).</summary>
  public static bool MatchesAll(string path, IReadOnlyList<string> words)
  {
    foreach (var w in words)
      if (path.IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0)
        return false;
    return true;
  }

  public bool TryGetByPath(string path, out int index)
  {
    var key = Normalize(path);
    index = FindPath(key);
    return index >= 0;
  }

  public IReadOnlyList<int> FindByPath(string path)
  {
    var key = Normalize(path);
    var index = FindPath(key); // paths are unique (CollapseDuplicates, OrdinalIgnoreCase)
    return index >= 0 ? new[] { index } : Array.Empty<int>();
  }

  public IReadOnlyList<int> FindByFileName(string fileName)
  {
    var name = Path.GetFileName(fileName.Replace('\\', '/'));
    var lo = LowerBoundName(name);
    var hi = lo;
    while (hi < _byName.Length && NameOf(_paths[_byName[hi]].AsSpan()).Equals(name, StringComparison.OrdinalIgnoreCase))
      hi++;
    if (hi == lo)
      return Array.Empty<int>();
    var all = new int[hi - lo];
    Array.Copy(_byName, lo, all, 0, all.Length);
    return all;
  }

  /// <summary>
  ///   Prefer the same archive and the same folder, then any file with that name.
  /// </summary>
  public int ResolveReference(string reference, int hintFileIndex)
  {
    var normalized = Normalize(reference);
    var exact = FindPath(normalized);
    if (exact >= 0)
      return exact;

    var hits = FindByFileName(normalized);
    if (hits.Count == 0)
      return -1;
    if (hits.Count == 1 || hintFileIndex < 0)
      return hits[0];

    var hint = _files[hintFileIndex];
    var hintDir = DirectoryOf(_paths[hintFileIndex]);
    var best = hits[0];
    foreach (var hit in hits)
    {
      var file = _files[hit];
      if (file.ArchiveId == hint.ArchiveId && DirectoryOf(_paths[hit]) == hintDir)
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
      var archiveName = RelativeArchiveName(directory, pakPath);
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

    var duplicatesCollapsed = CollapseDuplicates(raw);
    progress?.Report(new CatalogProgress(caches.Length, caches.Length, raw.Count, "Building folders"));

    var rootNode = new DirNode(Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "");
    for (var i = 0; i < raw.Count; i++)
      Insert(rootNode, raw[i].Path, i);

    var files = new CatalogFile[raw.Count];
    var paths = new string[raw.Count];
    var next = 0;
    var root = Flatten(rootNode, raw, files, paths, ref next);
    raw.Clear();
    raw.TrimExcess();

    var byPath = new int[files.Length];
    var byName = new int[files.Length];
    for (var i = 0; i < files.Length; i++) { byPath[i] = i; byName[i] = i; }
    Array.Sort(byPath, (a, b) => string.Compare(paths[a], paths[b], StringComparison.OrdinalIgnoreCase));
    Array.Sort(byName, (a, b) =>
    {
      var c = NameOf(paths[a].AsSpan()).CompareTo(NameOf(paths[b].AsSpan()), StringComparison.OrdinalIgnoreCase);
      return c != 0 ? c : a.CompareTo(b);
    });

    return new GameCatalog(
      directory,
      archives.ToArray(),
      root,
      files,
      paths,
      extensions.ToArray(),
      byPath,
      byName,
      skipped,
      duplicatesCollapsed,
      warnings);
  }

  private static string NameOf(string path)
  {
    var slash = path.LastIndexOf('/');
    return slash < 0 ? path : path[(slash + 1)..];
  }

  private static ReadOnlySpan<char> NameOf(ReadOnlySpan<char> path)
  {
    var slash = path.LastIndexOf('/');
    return slash < 0 ? path : path[(slash + 1)..];
  }

  private int FindPath(string key)
  {
    int lo = 0, hi = _byPath.Length - 1;
    while (lo <= hi)
    {
      var mid = (lo + hi) >>> 1;
      var c = string.Compare(_paths[_byPath[mid]], key, StringComparison.OrdinalIgnoreCase);
      if (c == 0) return _byPath[mid];
      if (c < 0) lo = mid + 1; else hi = mid - 1;
    }
    return -1;
  }

  private int LowerBoundName(string name)
  {
    int lo = 0, hi = _byName.Length;
    while (lo < hi)
    {
      var mid = (lo + hi) >>> 1;
      if (NameOf(_paths[_byName[mid]].AsSpan()).CompareTo(name, StringComparison.OrdinalIgnoreCase) < 0) lo = mid + 1; else hi = mid;
    }
    return lo;
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
    ref int next)
  {
    var first = next;
    if (node.Files.Count > 1)
      node.Files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(raw[a].Name, raw[b].Name));

    foreach (var rawIndex in node.Files)
    {
      var source = raw[rawIndex];
      paths[next] = source.Path;
      files[next] = new CatalogFile
      {
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
      children[childIndex++] = Flatten(child, raw, files, paths, ref next);

    var subtree = node.Files.Count;
    for (var i = 0; i < children.Length; i++)
      subtree += children[i].SubtreeFileCount;

    return new CatalogDirectory(node.Name, node.Path, children, first, node.Files.Count, subtree);
  }

  /// <summary>
  ///   Same internal path in more than one archive keeps the last copy. Archives are
  ///   read in path order, so the later path wins, matching extract-without-prefix.
  /// </summary>
  private static int CollapseDuplicates(List<RawFile> raw)
  {
    var winner = new Dictionary<string, int>(raw.Count, StringComparer.OrdinalIgnoreCase);
    var drop = new bool[raw.Count];
    var dropped = 0;
    for (var i = 0; i < raw.Count; i++)
    {
      var path = raw[i].Path;
      if (!winner.TryGetValue(path, out var previous))
      {
        winner.Add(path, i);
        continue;
      }

      drop[previous] = true;
      winner[path] = i;
      dropped++;
    }

    if (dropped == 0)
      return 0;

    var compact = new List<RawFile>(raw.Count - dropped);
    for (var i = 0; i < raw.Count; i++)
    {
      if (!drop[i])
        compact.Add(raw[i]);
    }

    raw.Clear();
    raw.AddRange(compact);
    return dropped;
  }

  private static string RelativeArchiveName(string root, string pakPath)
  {
    var relative = Path.GetRelativePath(root, pakPath);
    return relative.Replace('\\', '/');
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
