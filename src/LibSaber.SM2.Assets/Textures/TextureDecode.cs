using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using LibSaber.SpaceMarine2.Enumerations;

namespace LibSaber.SM2.Assets.Textures;

public enum PreviewFormat
{
  Unknown = 0,
  Bgra8,
  Bgrx8,
  R8,
  Bc1,
  Bc1Alpha,
  Bc3,
  Bc4,
  Bc5,
  Bc6U,
  Bc6S,
  Bc7,
}

public static class TextureInfo
{

  public static PreviewFormat PreviewOf(SM2TextureFormat format)
    => format switch
    {
      SM2TextureFormat.ARGB8888 => PreviewFormat.Bgra8,
      SM2TextureFormat.XRGB8888 => PreviewFormat.Bgrx8,
      SM2TextureFormat.R8U or SM2TextureFormat.A8 or SM2TextureFormat.I8 => PreviewFormat.R8,
      SM2TextureFormat.OXT1 => PreviewFormat.Bc1,
      SM2TextureFormat.AXT1 => PreviewFormat.Bc1Alpha,
      SM2TextureFormat.XT5 => PreviewFormat.Bc3,
      SM2TextureFormat.DXT5A => PreviewFormat.Bc4,
      SM2TextureFormat.DXN => PreviewFormat.Bc5,
      SM2TextureFormat.BC6U => PreviewFormat.Bc6U,
      SM2TextureFormat.BC6S => PreviewFormat.Bc6S,
      SM2TextureFormat.BC7 or SM2TextureFormat.BC7A => PreviewFormat.Bc7,
      _ => PreviewFormat.Unknown,
    };

  public static int Dxgi(SM2TextureFormat format)
    => format switch
    {
      SM2TextureFormat.ARGB8888 => 87,
      SM2TextureFormat.XRGB8888 => 88,
      SM2TextureFormat.ARGB16161616U => 11,
      SM2TextureFormat.BC6U => 95,
      SM2TextureFormat.BC6S => 96,
      SM2TextureFormat.BC7 or SM2TextureFormat.BC7A => 98,
      SM2TextureFormat.DXN => 83,
      SM2TextureFormat.DXT5A => 80,
      SM2TextureFormat.AXT1 or SM2TextureFormat.OXT1 => 71,
      SM2TextureFormat.R8U or SM2TextureFormat.A8 or SM2TextureFormat.I8 => 61,
      SM2TextureFormat.R16 => 58,
      SM2TextureFormat.R16G16 => 38,
      SM2TextureFormat.RGBA16161616F => 10,
      SM2TextureFormat.XT5 => 77,
      SM2TextureFormat.R16F => 54,
      SM2TextureFormat.R32F => 41,
      SM2TextureFormat.R8G8U => 49,
      SM2TextureFormat.R16G16F => 34,
      SM2TextureFormat.R10G10B10A2_UNORM => 24,
      _ => 0,
    };

  public static bool IsBlock(PreviewFormat format)
    => format is PreviewFormat.Bc1 or PreviewFormat.Bc1Alpha or PreviewFormat.Bc3
      or PreviewFormat.Bc4 or PreviewFormat.Bc5 or PreviewFormat.Bc6U
      or PreviewFormat.Bc6S or PreviewFormat.Bc7;

  public static int BlockBytes(PreviewFormat format)
    => format switch
    {
      PreviewFormat.Bc1 or PreviewFormat.Bc1Alpha or PreviewFormat.Bc4 => 8,
      PreviewFormat.Bc3 or PreviewFormat.Bc5 or PreviewFormat.Bc6U or PreviewFormat.Bc6S or PreviewFormat.Bc7 => 16,
      _ => 0,
    };

  public static int UncompressedBytes(SM2TextureFormat format)
    => format switch
    {
      SM2TextureFormat.ARGB8888 or SM2TextureFormat.XRGB8888 or SM2TextureFormat.R32F => 4,
      SM2TextureFormat.R8U or SM2TextureFormat.A8 or SM2TextureFormat.I8 => 1,
      SM2TextureFormat.R16 or SM2TextureFormat.R16F or SM2TextureFormat.R8G8U => 2,
      SM2TextureFormat.R16G16 or SM2TextureFormat.R16G16F => 4,
      SM2TextureFormat.RGBA16161616F or SM2TextureFormat.ARGB16161616U => 8,
      _ => 0,
    };

  public static int MipByteSize(SM2TextureFormat format, int width, int height)
  {
    width = Math.Max(1, width);
    height = Math.Max(1, height);
    var preview = PreviewOf(format);
    var block = BlockBytes(preview);
    if (block > 0)
      return Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * block;

    var bpp = UncompressedBytes(format);
    return bpp == 0 ? 0 : width * height * bpp;
  }

  public static byte[] Decode(SM2TextureFormat format, ReadOnlySpan<byte> data, int width, int height)
  {
    width = Math.Max(1, width);
    height = Math.Max(1, height);
    var preview = PreviewOf(format);
    return preview switch
    {
      PreviewFormat.Bgra8 => CopyBgra(data, width, height, forceAlpha: false),
      PreviewFormat.Bgrx8 => CopyBgra(data, width, height, forceAlpha: true),
      PreviewFormat.R8 => ExpandR8(data, width, height),
      PreviewFormat.Bc1 => BlockDecoder.DecodeBc1(data, width, height, punchThrough: true),
      PreviewFormat.Bc1Alpha => BlockDecoder.DecodeBc1(data, width, height, punchThrough: false),
      PreviewFormat.Bc3 => BlockDecoder.DecodeBc3(data, width, height),
      PreviewFormat.Bc4 => BlockDecoder.DecodeBc4(data, width, height),
      PreviewFormat.Bc5 => BlockDecoder.DecodeBc5(data, width, height),
      PreviewFormat.Bc6U => DecodeLibrary(data, width, height, CompressionFormat.Bc6U),
      PreviewFormat.Bc6S => DecodeLibrary(data, width, height, CompressionFormat.Bc6S),
      PreviewFormat.Bc7 => DecodeLibrary(data, width, height, CompressionFormat.Bc7),
      _ => throw new NotSupportedException($"No preview decoder for {format}. DDS export can still keep the original blocks."),
    };
  }

  private static byte[] CopyBgra(ReadOnlySpan<byte> data, int width, int height, bool forceAlpha)
  {
    var count = width * height * 4;
    if (data.Length < count)
      throw new InvalidDataException($"Texture is {data.Length} bytes, expected {count}.");

    var pixels = data[..count].ToArray();
    if (forceAlpha)
    {
      for (var i = 3; i < pixels.Length; i += 4)
        pixels[i] = 255;
    }

    return pixels;
  }

  private static byte[] ExpandR8(ReadOnlySpan<byte> data, int width, int height)
  {
    var count = width * height;
    if (data.Length < count)
      throw new InvalidDataException($"Texture is {data.Length} bytes, expected {count}.");

    var pixels = new byte[count * 4];
    for (var i = 0; i < count; i++)
    {
      var value = data[i];
      pixels[i * 4] = value;
      pixels[i * 4 + 1] = value;
      pixels[i * 4 + 2] = value;
      pixels[i * 4 + 3] = 255;
    }

    return pixels;
  }

  private static byte[] DecodeLibrary(ReadOnlySpan<byte> data, int width, int height, CompressionFormat format)
  {
    var decoder = new BcDecoder();
    var colors = decoder.DecodeRaw(data.ToArray(), width, height, format);
    var pixels = new byte[colors.Length * 4];
    for (var i = 0; i < colors.Length; i++)
    {
      pixels[i * 4] = colors[i].b;
      pixels[i * 4 + 1] = colors[i].g;
      pixels[i * 4 + 2] = colors[i].r;
      pixels[i * 4 + 3] = colors[i].a;
    }

    return pixels;
  }

}

static class BlockDecoder
{

  public static byte[] DecodeBc1(ReadOnlySpan<byte> data, int width, int height, bool punchThrough)
  {
    var pixels = new byte[width * height * 4];
    var blocksWide = Math.Max(1, (width + 3) / 4);
    var blocksHigh = Math.Max(1, (height + 3) / 4);
    var needed = blocksWide * blocksHigh * 8;
    if (data.Length < needed)
      throw new InvalidDataException($"BC1 data is {data.Length} bytes, expected {needed}.");

    var offset = 0;
    for (var by = 0; by < blocksHigh; by++)
    {
      for (var bx = 0; bx < blocksWide; bx++)
      {
        DecodeBc1Block(data.Slice(offset, 8), pixels, width, height, bx * 4, by * 4, punchThrough);
        offset += 8;
      }
    }

    return pixels;
  }

  public static byte[] DecodeBc3(ReadOnlySpan<byte> data, int width, int height)
  {
    var pixels = new byte[width * height * 4];
    var blocksWide = Math.Max(1, (width + 3) / 4);
    var blocksHigh = Math.Max(1, (height + 3) / 4);
    var needed = blocksWide * blocksHigh * 16;
    if (data.Length < needed)
      throw new InvalidDataException($"BC3 data is {data.Length} bytes, expected {needed}.");

    var offset = 0;
    for (var by = 0; by < blocksHigh; by++)
    {
      for (var bx = 0; bx < blocksWide; bx++)
      {
        DecodeBc1Block(data.Slice(offset + 8, 8), pixels, width, height, bx * 4, by * 4, punchThrough: false);
        ApplyBc4Alpha(data.Slice(offset, 8), pixels, width, height, bx * 4, by * 4);
        offset += 16;
      }
    }

    return pixels;
  }

  public static byte[] DecodeBc4(ReadOnlySpan<byte> data, int width, int height)
  {
    var pixels = new byte[width * height * 4];
    var blocksWide = Math.Max(1, (width + 3) / 4);
    var blocksHigh = Math.Max(1, (height + 3) / 4);
    var needed = blocksWide * blocksHigh * 8;
    if (data.Length < needed)
      throw new InvalidDataException($"BC4 data is {data.Length} bytes, expected {needed}.");

    var offset = 0;
    for (var by = 0; by < blocksHigh; by++)
    {
      for (var bx = 0; bx < blocksWide; bx++)
      {
        var alphas = Bc4Palette(data.Slice(offset, 8));
        var indices = Bc4Indices(data.Slice(offset, 8));
        for (var py = 0; py < 4; py++)
        {
          for (var px = 0; px < 4; px++)
          {
            var x = bx * 4 + px;
            var y = by * 4 + py;
            if (x >= width || y >= height)
              continue;
            var value = alphas[(indices >> (3 * (py * 4 + px))) & 7];
            var pixel = (y * width + x) * 4;
            pixels[pixel] = value;
            pixels[pixel + 1] = value;
            pixels[pixel + 2] = value;
            pixels[pixel + 3] = 255;
          }
        }

        offset += 8;
      }
    }

    return pixels;
  }

  public static byte[] DecodeBc5(ReadOnlySpan<byte> data, int width, int height)
  {
    var pixels = new byte[width * height * 4];
    var blocksWide = Math.Max(1, (width + 3) / 4);
    var blocksHigh = Math.Max(1, (height + 3) / 4);
    var needed = blocksWide * blocksHigh * 16;
    if (data.Length < needed)
      throw new InvalidDataException($"BC5 data is {data.Length} bytes, expected {needed}.");

    var offset = 0;
    for (var by = 0; by < blocksHigh; by++)
    {
      for (var bx = 0; bx < blocksWide; bx++)
      {
        var red = Bc4Palette(data.Slice(offset, 8));
        var redIndex = Bc4Indices(data.Slice(offset, 8));
        var green = Bc4Palette(data.Slice(offset + 8, 8));
        var greenIndex = Bc4Indices(data.Slice(offset + 8, 8));
        for (var py = 0; py < 4; py++)
        {
          for (var px = 0; px < 4; px++)
          {
            var x = bx * 4 + px;
            var y = by * 4 + py;
            if (x >= width || y >= height)
              continue;
            var bit = 3 * (py * 4 + px);
            var r = red[(redIndex >> bit) & 7];
            var g = green[(greenIndex >> bit) & 7];
            var rn = r / 255f * 2f - 1f;
            var gn = g / 255f * 2f - 1f;
            var bn = 1f - rn * rn - gn * gn;
            var b = (byte)(Math.Clamp(bn > 0 ? MathF.Sqrt(bn) : 0f, 0f, 1f) * 255f);
            var pixel = (y * width + x) * 4;
            pixels[pixel] = b;
            pixels[pixel + 1] = g;
            pixels[pixel + 2] = r;
            pixels[pixel + 3] = 255;
          }
        }

        offset += 16;
      }
    }

    return pixels;
  }

  private static void DecodeBc1Block(ReadOnlySpan<byte> block, byte[] pixels, int width, int height, int originX, int originY, bool punchThrough)
  {
    var c0 = (ushort)(block[0] | (block[1] << 8));
    var c1 = (ushort)(block[2] | (block[3] << 8));
    Rgb565(c0, out var r0, out var g0, out var b0);
    Rgb565(c1, out var r1, out var g1, out var b1);
    Span<byte> red = stackalloc byte[4];
    Span<byte> green = stackalloc byte[4];
    Span<byte> blue = stackalloc byte[4];
    Span<byte> alpha = stackalloc byte[4];
    red[0] = r0;
    green[0] = g0;
    blue[0] = b0;
    alpha[0] = 255;
    red[1] = r1;
    green[1] = g1;
    blue[1] = b1;
    alpha[1] = 255;
    if (c0 > c1 || !punchThrough)
    {
      red[2] = (byte)((2 * r0 + r1) / 3);
      green[2] = (byte)((2 * g0 + g1) / 3);
      blue[2] = (byte)((2 * b0 + b1) / 3);
      alpha[2] = 255;
      red[3] = (byte)((r0 + 2 * r1) / 3);
      green[3] = (byte)((g0 + 2 * g1) / 3);
      blue[3] = (byte)((b0 + 2 * b1) / 3);
      alpha[3] = 255;
    }
    else
    {
      red[2] = (byte)((r0 + r1) / 2);
      green[2] = (byte)((g0 + g1) / 2);
      blue[2] = (byte)((b0 + b1) / 2);
      alpha[2] = 255;
      red[3] = 0;
      green[3] = 0;
      blue[3] = 0;
      alpha[3] = 0;
    }

    var indices = (uint)(block[4] | (block[5] << 8) | (block[6] << 16) | (block[7] << 24));
    for (var py = 0; py < 4; py++)
    {
      for (var px = 0; px < 4; px++)
      {
        var x = originX + px;
        var y = originY + py;
        if (x >= width || y >= height)
          continue;
        var index = (int)((indices >> (2 * (py * 4 + px))) & 3);
        var pixel = (y * width + x) * 4;
        pixels[pixel] = blue[index];
        pixels[pixel + 1] = green[index];
        pixels[pixel + 2] = red[index];
        pixels[pixel + 3] = alpha[index];
      }
    }
  }

  private static void ApplyBc4Alpha(ReadOnlySpan<byte> block, byte[] pixels, int width, int height, int originX, int originY)
  {
    var palette = Bc4Palette(block);
    var indices = Bc4Indices(block);
    for (var py = 0; py < 4; py++)
    {
      for (var px = 0; px < 4; px++)
      {
        var x = originX + px;
        var y = originY + py;
        if (x >= width || y >= height)
          continue;
        pixels[(y * width + x) * 4 + 3] = palette[(indices >> (3 * (py * 4 + px))) & 7];
      }
    }
  }

  private static byte[] Bc4Palette(ReadOnlySpan<byte> block)
  {
    var a0 = block[0];
    var a1 = block[1];
    var palette = new byte[8];
    palette[0] = a0;
    palette[1] = a1;
    if (a0 > a1)
    {
      palette[2] = (byte)((6 * a0 + 1 * a1) / 7);
      palette[3] = (byte)((5 * a0 + 2 * a1) / 7);
      palette[4] = (byte)((4 * a0 + 3 * a1) / 7);
      palette[5] = (byte)((3 * a0 + 4 * a1) / 7);
      palette[6] = (byte)((2 * a0 + 5 * a1) / 7);
      palette[7] = (byte)((1 * a0 + 6 * a1) / 7);
    }
    else
    {
      palette[2] = (byte)((4 * a0 + 1 * a1) / 5);
      palette[3] = (byte)((3 * a0 + 2 * a1) / 5);
      palette[4] = (byte)((2 * a0 + 3 * a1) / 5);
      palette[5] = (byte)((1 * a0 + 4 * a1) / 5);
      palette[6] = 0;
      palette[7] = 255;
    }

    return palette;
  }

  private static ulong Bc4Indices(ReadOnlySpan<byte> block)
  {
    ulong indices = 0;
    for (var i = 0; i < 6; i++)
      indices |= (ulong)block[2 + i] << (8 * i);
    return indices;
  }

  private static void Rgb565(ushort color, out byte r, out byte g, out byte b)
  {
    r = (byte)(((color >> 11) & 31) * 255 / 31);
    g = (byte)(((color >> 5) & 63) * 255 / 63);
    b = (byte)((color & 31) * 255 / 31);
  }

}
