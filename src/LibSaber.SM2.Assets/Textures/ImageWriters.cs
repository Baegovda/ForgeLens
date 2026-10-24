using System.IO.Compression;

namespace LibSaber.SM2.Assets.Textures;

public static class ImageWriters
{

  public static void WritePng(string path, ReadOnlySpan<byte> bgra, int width, int height)
  {
    var scanlines = new byte[height * (1 + width * 4)];
    var adler = 1u;
    for (var y = 0; y < height; y++)
    {
      var row = y * (1 + width * 4);
      scanlines[row] = 0;
      adler = Adler(adler, scanlines.AsSpan(row, 1));
      for (var x = 0; x < width; x++)
      {
        var source = (y * width + x) * 4;
        var dest = row + 1 + x * 4;
        scanlines[dest] = bgra[source + 2];
        scanlines[dest + 1] = bgra[source + 1];
        scanlines[dest + 2] = bgra[source];
        scanlines[dest + 3] = bgra[source + 3];
      }

      adler = Adler(adler, scanlines.AsSpan(row + 1, width * 4));
    }

    using var compressed = new MemoryStream();
    compressed.WriteByte(0x78);
    compressed.WriteByte(0x9C);
    using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
      deflate.Write(scanlines);
    compressed.Write(new[] { (byte)(adler >> 24), (byte)(adler >> 16), (byte)(adler >> 8), (byte)adler });

    using var output = File.Create(path);
    output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    Span<byte> ihdr = stackalloc byte[13];
    WriteUInt(ihdr, 0, (uint)width);
    WriteUInt(ihdr, 4, (uint)height);
    ihdr[8] = 8;
    ihdr[9] = 6;
    Chunk(output, "IHDR"u8, ihdr);
    Chunk(output, "IDAT"u8, compressed.ToArray());
    Chunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
  }

  private static uint Adler(uint current, ReadOnlySpan<byte> data)
  {
    var a = current & 0xFFFF;
    var b = current >> 16;
    foreach (var value in data)
    {
      a = (a + value) % 65521;
      b = (b + a) % 65521;
    }

    return (b << 16) | a;
  }

  private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
  {
    Span<byte> length = stackalloc byte[4];
    WriteUInt(length, 0, (uint)data.Length);
    stream.Write(length);
    stream.Write(type);
    stream.Write(data);
    var crc = Crc(type, data);
    Span<byte> crcBytes = stackalloc byte[4];
    WriteUInt(crcBytes, 0, crc);
    stream.Write(crcBytes);
  }

  private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
  {
    var crc = 0xFFFFFFFFu;
    crc = Update(crc, type);
    crc = Update(crc, data);
    return crc ^ 0xFFFFFFFFu;
  }

  private static uint Update(uint crc, ReadOnlySpan<byte> data)
  {
    foreach (var value in data)
    {
      crc ^= value;
      for (var i = 0; i < 8; i++)
        crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
    }

    return crc;
  }

  private static void WriteUInt(Span<byte> destination, int offset, uint value)
  {
    destination[offset] = (byte)(value >> 24);
    destination[offset + 1] = (byte)(value >> 16);
    destination[offset + 2] = (byte)(value >> 8);
    destination[offset + 3] = (byte)value;
  }

  public static void WriteTga(string path, ReadOnlySpan<byte> bgra, int width, int height)
  {
    if (width > ushort.MaxValue || height > ushort.MaxValue)
      throw new InvalidDataException("TGA cannot store a dimension above 65535.");

    using var stream = File.Create(path);
    Span<byte> header = stackalloc byte[18];
    header[2] = 2;
    header[12] = (byte)(width & 0xFF);
    header[13] = (byte)((width >> 8) & 0xFF);
    header[14] = (byte)(height & 0xFF);
    header[15] = (byte)((height >> 8) & 0xFF);
    header[16] = 32;
    header[17] = 8 | 32;
    stream.Write(header);
    stream.Write(bgra);
  }

}

public static class DdsWriter
{

  public static void Write(string path, ReadOnlySpan<byte> pixels, int width, int height, int mipCount, int faces, int dxgiFormat, bool blockCompressed)
  {
    mipCount = Math.Max(1, mipCount);
    faces = Math.Max(1, faces);
    var cube = faces == 6;
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(0x20534444);
    writer.Write(124);
    var flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x20000 | 0x80000;
    writer.Write(flags);
    writer.Write(height);
    writer.Write(width);
    writer.Write(Mip0Bytes(width, height, dxgiFormat, blockCompressed, pixels.Length));
    writer.Write(0);
    writer.Write(mipCount);
    for (var i = 0; i < 11; i++)
      writer.Write(0);

    writer.Write(32);
    writer.Write(0x4);
    writer.Write(0x30315844);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);

    var caps = 0x1000;
    if (mipCount > 1)
      caps |= 0x8 | 0x400000;
    writer.Write(caps);
    writer.Write(cube ? 0xFE00 : 0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);

    writer.Write(dxgiFormat);
    writer.Write(3);
    writer.Write(cube ? 0x4 : 0);
    writer.Write(cube ? 1 : faces);
    writer.Write(0);
    writer.Write(pixels);
  }

  private static int Mip0Bytes(int width, int height, int dxgiFormat, bool blockCompressed, int payloadLength)
  {
    if (!blockCompressed)
      return Math.Min(payloadLength, width * height * BytesPerPixel(dxgiFormat));

    var block = dxgiFormat is 71 or 80 ? 8 : 16;
    return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * block;
  }

  private static int BytesPerPixel(int dxgiFormat)
    => dxgiFormat switch
    {
      61 => 1,
      54 or 58 or 49 => 2,
      10 or 11 => 8,
      _ => 4,
    };

}
