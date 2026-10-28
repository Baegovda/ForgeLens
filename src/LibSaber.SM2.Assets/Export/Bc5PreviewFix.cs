using LibSaber.SM2.Assets.Textures;
using LibSaber.SpaceMarine2.Enumerations;

namespace LibSaber.SM2.Assets.Export;

/// <summary>
/// Re-writes the PNG/TGA previews of BC5 (DXN) mask textures in an existing export folder from the .dds the export wrote
/// beside them. The .dds keeps the game's original blocks (DdsWriter writes document.Payload unchanged), so decoding it with
/// the same TextureDocument.Decode + <see cref="AssetExporter.FixBc5MaskPixels"/> path gives the same preview a fresh export
/// writes. Used to repair exports made between 37f43a1 and f25ea8b, where the BGRA byte 2 (R = metalness) was zeroed.
/// Only previews that already exist are replaced; normal maps (TextureInfo.IsNormalName) are skipped.
/// </summary>
public static class Bc5PreviewFix
{
  public const int DxgiBc5Unorm = 83;

  public static TextureDocument? ReadDds(string path)
  {
    var bytes = File.ReadAllBytes(path);
    if (bytes.Length < 148 || BitConverter.ToInt32(bytes, 0) != 0x20534444 || BitConverter.ToInt32(bytes, 84) != 0x30315844)
      return null;
    var dxgi = BitConverter.ToInt32(bytes, 128);
    if (dxgi != DxgiBc5Unorm)
      return null;
    var cube = (BitConverter.ToInt32(bytes, 136) & 0x4) != 0;
    var arr = Math.Max(1, BitConverter.ToInt32(bytes, 140));
    return new TextureDocument
    {
      Height = BitConverter.ToInt32(bytes, 12),
      Width = BitConverter.ToInt32(bytes, 16),
      MipCount = Math.Max(1, BitConverter.ToInt32(bytes, 28)),
      FaceCount = cube ? 6 : arr,
      Format = SM2TextureFormat.DXN,
      DxgiFormat = dxgi,
      Payload = bytes[148..],
    };
  }

  /// <summary>Returns (fixed, skipped) counts. <paramref name="log"/> gets one line per rewritten file.</summary>
  public static (int Fixed, int Skipped) Run(string directory, Action<string>? log = null)
  {
    int done = 0, skipped = 0;
    foreach (var dds in Directory.EnumerateFiles(directory, "*.dds", SearchOption.AllDirectories))
    {
      if (TextureInfo.IsNormalName(dds)) { skipped++; continue; }
      var stem = Path.Combine(Path.GetDirectoryName(dds)!, Path.GetFileNameWithoutExtension(dds));
      var png = stem + ".png";
      var tga = stem + ".tga";
      if (!File.Exists(png) && !File.Exists(tga)) { skipped++; continue; }
      var doc = ReadDds(dds);
      if (doc is null) { skipped++; continue; }
      var pixels = doc.Decode(0, 0, out var w, out var h, out _);
      AssetExporter.FixBc5MaskPixels(pixels);
      if (File.Exists(png)) ImageWriters.WritePng(png, pixels, w, h);
      if (File.Exists(tga)) ImageWriters.WriteTga(tga, pixels, w, h);
      done++;
      log?.Invoke(stem);
    }
    return (done, skipped);
  }
}
