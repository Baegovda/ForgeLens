using System.IO.Compression;
using System.Text;

namespace LibSaber.SM2.Assets.Tests;

internal static class CacheFile
{

  public readonly record struct Entry(string Name, byte[] Data, bool Deflate = false);

  public static void Create(string directory, string fileName, IEnumerable<Entry> entries)
  {
    Directory.CreateDirectory(directory);
    var archivePath = Path.Combine(directory, fileName);
    var built = new List<(Entry Entry, long Offset, byte[] Payload)>();
    long offset = 0;
    foreach (var entry in entries)
    {
      var payload = entry.Deflate ? Deflate(entry.Data) : entry.Data;
      built.Add((entry, offset, payload));
      offset += payload.Length;
    }

    using (var archive = File.Create(archivePath))
    {
      foreach (var item in built)
        archive.Write(item.Payload);
    }

    using var cache = File.Create(archivePath + ".cache");
    using var writer = new BinaryWriter(cache, Encoding.ASCII, leaveOpen: false);
    foreach (var item in built)
    {
      writer.Write(4);
      writer.Write((byte)0);
      var name = Encoding.ASCII.GetBytes(item.Entry.Name);
      writer.Write(name.Length);
      writer.Write(name);
      writer.Write(item.Offset);
      writer.Write((long)item.Entry.Data.Length);
      writer.Write((long)item.Payload.Length);
      writer.Write((short)(item.Entry.Deflate ? 8 : 0));
    }
  }

  private static byte[] Deflate(byte[] data)
  {
    using var output = new MemoryStream();
    using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
      deflate.Write(data);
    return output.ToArray();
  }

}

internal sealed class TempDirectory : IDisposable
{

  public TempDirectory()
  {
    Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "libsaber-sm2-assets-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path);
  }

  public string Path { get; }

  public void Dispose()
  {
    if (Directory.Exists(Path))
      Directory.Delete(Path, recursive: true);
  }

}
