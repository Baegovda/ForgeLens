using LibSaber.IO;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SpaceMarine2.Enumerations;
using LibSaber.SpaceMarine2.Serialization;
using LibSaber.SpaceMarine2.Structures;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SM2.Assets.Textures;

public sealed class TextureDocument
{

  public int Width { get; init; }
  public int Height { get; init; }
  public int Depth { get; init; }
  public int MipCount { get; init; }
  public int FaceCount { get; init; }
  public SM2TextureFormat Format { get; init; }
  public int DxgiFormat { get; init; }
  public byte[] Payload { get; init; } = Array.Empty<byte>();
  public string Message { get; init; } = "";
  public bool CanDecode => TextureInfo.PreviewOf(Format) != PreviewFormat.Unknown && DxgiFormat != 0;

  public byte[] Decode(int mip, int face, out int mipWidth, out int mipHeight, out string note)
  {
    mip = Math.Clamp(mip, 0, Math.Max(0, MipCount - 1));
    face = Math.Clamp(face, 0, Math.Max(0, FaceCount - 1));
    while (mip < MipCount - 1 && (Math.Max(1, Width >> mip) > 2048 || Math.Max(1, Height >> mip) > 2048))
      mip++;

    mipWidth = Math.Max(1, Width >> mip);
    mipHeight = Math.Max(1, Height >> mip);
    note = "";
    var slice = Slice(Payload, mip, face, mipWidth, mipHeight);
    if (slice.Length == 0)
      throw new InvalidDataException("This mip is outside the texture payload.");

    if (TextureInfo.PreviewOf(Format) == PreviewFormat.Unknown)
    {
      note = $"{Format} has no preview decoder. DDS export still writes the original blocks.";
      throw new NotSupportedException(note);
    }

    return TextureInfo.Decode(Format, slice, mipWidth, mipHeight);
  }

  public byte[] Slice(byte[] payload, int mip, int face, int mipWidth, int mipHeight)
  {
    var one = TextureInfo.MipByteSize(Format, mipWidth, mipHeight);
    if (one <= 0)
      return payload;

    long offset = 0;
    for (var f = 0; f < Math.Max(1, FaceCount); f++)
    {
      for (var m = 0; m < Math.Max(1, MipCount); m++)
      {
        var w = Math.Max(1, Width >> m);
        var h = Math.Max(1, Height >> m);
        var size = TextureInfo.MipByteSize(Format, w, h);
        if (f == face && m == mip)
        {
          if (offset + size > payload.Length)
          {
            if (offset >= payload.Length)
              return Array.Empty<byte>();
            size = (int)Math.Min(size, payload.Length - offset);
          }

          var slice = new byte[size];
          Buffer.BlockCopy(payload, (int)offset, slice, 0, size);
          return slice;
        }

        offset += size;
      }
    }

    return Array.Empty<byte>();
  }

}

public static class TextureLoader
{

  public static TextureDocument Load(GameCatalog catalog, ArchiveStore store, int fileIndex)
  {
    var file = catalog.GetFile(fileIndex);
    if (file.Kind == AssetKind.TextureMip)
    {
      return new TextureDocument
      {
        Message = "This is a loose mip payload (.pct_mip). Open the matching .pct.resource; the header and mip list live there. Retail textures almost never store pixels in a .pct.",
      };
    }

    var bytes = store.Read(fileIndex);
    if (file.Extension.Equals(".pct", StringComparison.OrdinalIgnoreCase))
      return FromPicture(bytes, "Embedded .pct picture.");

    if (!LooksText(bytes))
    {
      return new TextureDocument
      {
        Message = "This file is not YAML resource text. Texture preview uses .pct.resource plus the .pct_mip files it names.",
      };
    }

    return FromResource(bytes, name =>
    {
      var index = catalog.ResolveReference(name, fileIndex);
      return index < 0 ? null : store.Read(index);
    });
  }

  /// <summary>
  ///   Loose files: a <c>X.pct.resource</c> plus its <c>X_N.pct_mip</c> files in the same folder (or the parent's
  ///   <c>pct</c>/<c>pct_mip</c> folders), as the extractor writes them.
  /// </summary>
  public static TextureDocument LoadLoose(string resourcePath)
  {
    var dir = Path.GetDirectoryName(Path.GetFullPath(resourcePath)) ?? ".";
    var parent = Path.GetDirectoryName(dir) ?? dir;
    return FromResource(File.ReadAllBytes(resourcePath), name =>
    {
      var file = Path.GetFileName(name.Replace("res://", "").Replace('\\', '/'));
      foreach (var candidate in new[] { Path.Combine(dir, file), Path.Combine(parent, "pct_mip", file), Path.Combine(parent, "pct", file) })
        if (File.Exists(candidate))
          return File.ReadAllBytes(candidate);
      return null;
    });
  }

  /// <summary>Texture from the YAML <c>res_desc_pct</c> text; <paramref name="read"/> returns a named payload file or null.</summary>
  public static TextureDocument FromResource(byte[] bytes, Func<string, byte[]?> read)
  {
    resDESC description;
    try
    {
      using var stream = new MemoryStream(bytes, writable: false);
      var reader = new NativeReader(stream, Endianness.LittleEndian);
      description = Serializer<resDESC>.Deserialize(reader);
    }
    catch (Exception ex)
    {
      throw new InvalidDataException($"Could not read the texture resource: {ex.Message}", ex);
    }

    if (description is not resDESC_PCT pct || pct.header is null)
      throw new InvalidDataException(description.__type is null
        ? "The resource is not a texture (res_desc_pct)."
        : $"The resource is {description.__type}, not a texture.");

    var format = pct.TextureFormat;
    var width = Math.Max(1, pct.header.sx);
    var height = Math.Max(1, pct.header.sy);
    var depth = Math.Max(1, pct.header.sz);
    var mips = Math.Max(1, pct.header.nMipMap);
    var faces = Math.Max(1, pct.header.nFaces);
    byte[] payload;
    string message;

    if (!string.IsNullOrWhiteSpace(pct.pct))
    {
      var picture = read(pct.pct)
        ?? throw new FileNotFoundException($"The resource points at '{pct.pct}', which is not available.");
      payload = PictureBytes(picture);
      message = $"Picture container {pct.pct}, {format}, {width}x{height}, {mips} mip(s), {faces} face(s).";
    }
    else
    {
      var names = pct.mipMaps ?? Array.Empty<string>();
      if (names.Length == 0)
        throw new InvalidDataException("The texture resource has no mipMaps and no pct. There is nothing to decode.");

      var loaded = new byte[names.Length][];
      var missing = new List<string>();
      for (var i = 0; i < names.Length; i++)
      {
        var mipBytes = read(names[i]);
        if (mipBytes is null)
        {
          missing.Add(names[i]);
          continue;
        }

        var expect = TextureInfo.MipByteSize(format, Math.Max(1, width >> i), Math.Max(1, height >> i));
        if (expect > 0 && mipBytes.Length < expect)
        {
          missing.Add(names[i]);
          continue;
        }

        loaded[i] = mipBytes;
      }

      var start = 0;
      while (start < loaded.Length && loaded[start] is null)
        start++;
      if (start == loaded.Length)
        throw new FileNotFoundException($"None of the {names.Length} mip files named by this resource are in the catalog.");

      var count = 0;
      while (start + count < loaded.Length && loaded[start + count] is not null)
        count++;

      using var concat = new MemoryStream();
      for (var i = 0; i < count; i++)
        concat.Write(loaded[start + i]);

      // A partial extract often lacks the largest mips. The bytes we have are the next
      // complete mip, so the image size has to match that mip or the block decoder reads off the end.
      if (start > 0)
      {
        width = Math.Max(1, width >> start);
        height = Math.Max(1, height >> start);
      }

      mips = count;
      payload = concat.ToArray();
      message = $"{format}, {width}x{height}, {mips} mip(s), {faces} face(s), {names.Length} mip file(s)";
      if (missing.Count > 0)
        message += $", {missing.Count} missing";
      if (start > 0)
        message += $". The first {start} mip file(s) are not in this folder, so the image starts at this size";
      message += ". Pixels come from .pct_mip payloads, not from a .pct.";
    }

    return new TextureDocument
    {
      Width = width,
      Height = height,
      Depth = depth,
      MipCount = mips,
      FaceCount = faces,
      Format = format,
      DxgiFormat = TextureInfo.Dxgi(format),
      Payload = payload,
      Message = message,
    };
  }

  private static TextureDocument FromPicture(byte[] bytes, string message)
  {
    pctPICTURE picture;
    try
    {
      using var stream = new MemoryStream(bytes, writable: false);
      var reader = new NativeReader(stream, Endianness.LittleEndian);
      picture = Serializer<pctPICTURE>.Deserialize(reader);
    }
    catch (Exception ex)
    {
      throw new InvalidDataException($"Could not read the .pct picture: {ex.Message}", ex);
    }

    return new TextureDocument
    {
      Width = Math.Max(1, picture.Width),
      Height = Math.Max(1, picture.Height),
      Depth = Math.Max(1, picture.Depth),
      MipCount = Math.Max(1, picture.MipMapCount),
      FaceCount = Math.Max(1, picture.Faces),
      Format = picture.Format,
      DxgiFormat = TextureInfo.Dxgi(picture.Format),
      Payload = picture.Data ?? Array.Empty<byte>(),
      Message = message,
    };
  }

  private static byte[] PictureBytes(byte[] bytes)
  {
    using var stream = new MemoryStream(bytes, writable: false);
    var reader = new NativeReader(stream, Endianness.LittleEndian);
    var picture = Serializer<pctPICTURE>.Deserialize(reader);
    return picture.Data ?? Array.Empty<byte>();
  }

  public static bool LooksText(ReadOnlySpan<byte> data)
  {
    var n = Math.Min(data.Length, 256);
    if (n == 0)
      return false;

    var printable = 0;
    for (var i = 0; i < n; i++)
    {
      var b = data[i];
      if (b == 0)
        return false;
      if (b is 9 or 10 or 13 || (b >= 32 && b < 127))
        printable++;
    }

    return printable * 10 > n * 9;
  }

}
