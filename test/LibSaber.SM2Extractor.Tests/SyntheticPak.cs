using System.IO.Compression;
using System.Text;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SM2Extractor.Tests;

internal static class SyntheticPak
{

  public enum Format
  {
    Legacy,
    Version7
  }

  public readonly record struct Entry(
    string Name,
    byte[] Data,
    fioZIP_CACHE_FILE.COMPRESS_METHOD Method = fioZIP_CACHE_FILE.COMPRESS_METHOD.STORE,
    byte[]? PayloadOverride = null);

  public static string Create(
    string directory,
    string fileName,
    IEnumerable<Entry> entries,
    Format format = Format.Legacy,
    int payloadPrefix = 0,
    byte[]? cacheTrailer = null)
  {
    Directory.CreateDirectory(directory);
    var archivePath = Path.Combine(directory, fileName);
    var built = new List<(Entry Entry, long Offset, byte[] Payload)>();
    long offset = payloadPrefix;
    foreach (var entry in entries)
    {
      var payload = entry.PayloadOverride ?? Encode(entry);
      built.Add((entry, offset, payload));
      offset += payload.Length;
    }

    using (var archive = File.Create(archivePath))
    {
      if (payloadPrefix > 0)
        archive.Write(new byte[payloadPrefix]);

      foreach (var item in built)
        archive.Write(item.Payload);
    }

    using (var cache = File.Create(archivePath + ".cache"))
    using (var writer = new BinaryWriter(cache, Encoding.ASCII, leaveOpen: false))
    {
      if (format == Format.Version7)
        writer.Write(Enumerable.Repeat((byte)0xFF, 0x20).ToArray());

      foreach (var item in built)
      {
        writer.Write(format == Format.Version7 ? 7 : 4);
        writer.Write((byte)0);
        var name = Encoding.ASCII.GetBytes(item.Entry.Name);
        writer.Write(name.Length);
        writer.Write(name);
        writer.Write(item.Offset);
        writer.Write((long)item.Entry.Data.Length);
        writer.Write((long)item.Payload.Length);
        writer.Write((short)item.Entry.Method);
        if (format == Format.Version7)
          writer.Write(0L);
      }

      if (cacheTrailer is not null && cacheTrailer.Length > 0)
        writer.Write(cacheTrailer);
    }

    return archivePath;
  }

  private static byte[] Encode(Entry entry)
  {
    if (entry.Method != fioZIP_CACHE_FILE.COMPRESS_METHOD.DEFLATE)
      return entry.Data;

    using var output = new MemoryStream();
    using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
      deflate.Write(entry.Data);

    return output.ToArray();
  }

}
