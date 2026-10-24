using System.IO.Compression;

namespace LibSaber.SM2.Assets.Catalog;

/// <summary>
///   Reads one archived file at a time. A few payload streams stay open; the
///   rest of a client_pc folder is never mapped.
/// </summary>
public sealed class ArchiveStore : IDisposable
{

  private const int Capacity = 4;

  private readonly GameCatalog _catalog;
  private readonly Dictionary<int, FileStream> _open = new();
  private readonly LinkedList<int> _lru = new();
  private readonly object _gate = new();
  private bool _disposed;

  public ArchiveStore(GameCatalog catalog)
  {
    _catalog = catalog;
  }

  public byte[] Read(int fileIndex)
  {
    _catalog.GetExtent(fileIndex, out var archiveId, out var offset, out var size, out var compressedSize, out var method);
    var archive = _catalog.Archives[archiveId];
    if (!archive.HasPayload)
      throw new FileNotFoundException($"Archive payload is missing: {archive.PakPath}", archive.PakPath);

    if (compressedSize < 0 || offset < 0)
      throw new InvalidDataException($"Invalid extent for '{_catalog.GetFile(fileIndex).Path}'.");

    if (compressedSize == 0)
      return Array.Empty<byte>();

    if (method == 9)
      throw new NotSupportedException($"DEFLATE64 is not supported for '{_catalog.GetFile(fileIndex).Path}'.");

    var compressed = new byte[compressedSize];
    lock (_gate)
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
      var stream = Rent(archiveId, archive.PakPath);
      stream.Position = offset;
      stream.ReadExactly(compressed);
    }

    if (method == 0)
      return compressed;

    if (method != 8)
      throw new NotSupportedException($"Compression method {method} is not supported for '{_catalog.GetFile(fileIndex).Path}'.");

    try
    {
      using var source = new MemoryStream(compressed, writable: false);
      using var deflate = new DeflateStream(source, CompressionMode.Decompress);
      if (size <= 0)
      {
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
      }

      var outputBuffer = new byte[size];
      var read = 0;
      while (read < outputBuffer.Length)
      {
        var n = deflate.Read(outputBuffer, read, outputBuffer.Length - read);
        if (n == 0)
          break;
        read += n;
      }

      if (read != outputBuffer.Length)
        throw new InvalidDataException(
          $"Deflate of '{_catalog.GetFile(fileIndex).Path}' produced {read} bytes, expected {size}.");

      return outputBuffer;
    }
    catch (InvalidDataException ex) when (ex.Message.StartsWith("Deflate of", StringComparison.Ordinal))
    {
      throw;
    }
    catch (InvalidDataException ex)
    {
      throw new InvalidDataException($"Failed to deflate '{_catalog.GetFile(fileIndex).Path}': {ex.Message}", ex);
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      foreach (var stream in _open.Values)
        stream.Dispose();
      _open.Clear();
      _lru.Clear();
      _disposed = true;
    }
  }

  private FileStream Rent(int archiveId, string path)
  {
    if (_open.TryGetValue(archiveId, out var stream))
    {
      _lru.Remove(archiveId);
      _lru.AddFirst(archiveId);
      return stream;
    }

    stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    _open.Add(archiveId, stream);
    _lru.AddFirst(archiveId);
    while (_open.Count > Capacity)
    {
      var last = _lru.Last!.Value;
      _lru.RemoveLast();
      _open[last].Dispose();
      _open.Remove(last);
    }

    return stream;
  }

}
