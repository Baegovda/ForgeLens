using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using LibSaber.IO;
using LibSaber.SpaceMarine2.Serialization;

namespace LibSaber.SpaceMarine2.Structures.Resources;

public class fioZIP_FILE : IDisposable
{

  public static readonly IReadOnlyList<string> SupportedExtensions = new[] { ".pak", ".pck" };

  private readonly MemoryMappedFile? _file;
  private readonly fioZIP_CACHE_FILE _cacheFile;

  private bool _isDisposed;

  public IReadOnlyDictionary<string, fioZIP_CACHE_FILE.ENTRY> Entries => _cacheFile;

  private fioZIP_FILE(MemoryMappedFile? file, fioZIP_CACHE_FILE cacheFile)
  {
    _file = file;
    _cacheFile = cacheFile;
  }

  public static bool IsSupportedArchive(string filePath)
  {
    var extension = Path.GetExtension(filePath);
    for (var i = 0; i < SupportedExtensions.Count; i++)
    {
      if (extension.Equals(SupportedExtensions[i], StringComparison.OrdinalIgnoreCase))
        return true;
    }

    return false;
  }

  public static fioZIP_FILE Open(string filePath)
  {
    if (!IsSupportedArchive(filePath))
      throw new InvalidDataException(
        $"File is not a Space Marine 2 archive (.pak or .pck): {filePath}");

    var cacheFile = ReadCacheFile(filePath);

    FileStream? fileStream = null;
    try
    {
      fileStream = new FileStream(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite);

      if (fileStream.Length == 0)
      {
        fileStream.Dispose();
        fileStream = null;
        return new fioZIP_FILE(null, cacheFile);
      }

      var file = MemoryMappedFile.CreateFromFile(
        fileStream,
        mapName: null,
        capacity: 0,
        access: MemoryMappedFileAccess.Read,
        inheritability: HandleInheritability.None,
        leaveOpen: false);
      fileStream = null;
      return new fioZIP_FILE(file, cacheFile);
    }
    catch
    {
      fileStream?.Dispose();
      throw;
    }
  }

  public Stream GetFileStream(string fileName)
  {
    if (!_cacheFile.TryGetValue(fileName, out var entry))
      throw new FileNotFoundException($"File not found: {fileName}", fileName);

    return GetFileStream(entry);
  }

  public Stream GetFileStream(fioZIP_CACHE_FILE.ENTRY entry)
  {
    switch (entry.CompressMethod)
    {
      case fioZIP_CACHE_FILE.COMPRESS_METHOD.STORE:
        return OpenStored(entry);
      case fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE:
        return OpenDeflate(entry);
      default:
        throw new NotSupportedException(
          $"Compression method {entry.CompressMethod} is not supported for '{entry.FileName}'.");
    }
  }

  private Stream OpenStored(fioZIP_CACHE_FILE.ENTRY entry)
    => OpenExtent(entry);

  private Stream OpenDeflate(fioZIP_CACHE_FILE.ENTRY entry)
  {
    using var compressedStream = OpenExtent(entry);
    if (compressedStream.Length == 0)
      return new MemoryStream(Array.Empty<byte>(), writable: false);

    try
    {
      using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
      var fileStream = new MemoryStream();
      deflateStream.CopyTo(fileStream);
      fileStream.Position = 0;
      return fileStream;
    }
    catch (InvalidDataException ex)
    {
      throw new InvalidDataException(
        $"Failed to deflate '{entry.FileName}': {ex.Message}",
        ex);
    }
  }

  private Stream OpenExtent(fioZIP_CACHE_FILE.ENTRY entry)
  {
    if (entry.CompressedSize == 0)
      return new MemoryStream(Array.Empty<byte>(), writable: false);

    if (entry.Offset < 0 || entry.CompressedSize < 0)
      throw new InvalidDataException($"Invalid extent for '{entry.FileName}'.");

    if (_file is null)
      throw new InvalidDataException($"Archive has no payload data for '{entry.FileName}'.");

    return _file.CreateViewStream(
      entry.Offset,
      entry.CompressedSize,
      MemoryMappedFileAccess.Read);
  }

  private static fioZIP_CACHE_FILE ReadCacheFile(string filePath)
  {
    var cacheFilePath = filePath + ".cache";
    if (!File.Exists(cacheFilePath))
      throw new FileNotFoundException(
        $"No cache file found for {filePath}. Expected {cacheFilePath}.",
        cacheFilePath);

    using (var cacheFileStream = File.OpenRead(cacheFilePath))
    {
      var reader = new NativeReader(cacheFileStream, Endianness.LittleEndian);
      return Serializer<fioZIP_CACHE_FILE>.Deserialize(reader);
    }
  }

  public void Dispose()
  {
    if (_isDisposed)
      return;

    _file?.Dispose();
    _isDisposed = true;
  }

}
