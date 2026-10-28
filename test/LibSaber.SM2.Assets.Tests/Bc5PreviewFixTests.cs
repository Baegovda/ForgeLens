using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Textures;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

public class Bc5PreviewFixTests
{
  static byte[] Block(byte r, byte g) => new byte[] { r, r, 0, 0, 0, 0, 0, 0, g, g, 0, 0, 0, 0, 0, 0 };

  [Fact]
  public void RedecodeKeepsRedMetalnessAndZerosBlue()
  {
    var dir = Directory.CreateTempSubdirectory("bc5fix").FullName;
    try
    {
      var payload = Block(200, 100);
      DdsWriter.Write(Path.Combine(dir, "x_spec.dds"), payload, 4, 4, 1, 1, Bc5PreviewFix.DxgiBc5Unorm, true);
      File.WriteAllBytes(Path.Combine(dir, "x_spec.png"), new byte[] { 1 });
      DdsWriter.Write(Path.Combine(dir, "x_nm.dds"), payload, 4, 4, 1, 1, Bc5PreviewFix.DxgiBc5Unorm, true);
      File.WriteAllBytes(Path.Combine(dir, "x_nm.png"), new byte[] { 1 });

      var doc = Bc5PreviewFix.ReadDds(Path.Combine(dir, "x_spec.dds"))!;
      Assert.Equal(4, doc.Width);
      var px = doc.Decode(0, 0, out _, out _, out _);
      AssetExporter.FixBc5MaskPixels(px);
      Assert.Equal(0, px[0]);      // B zeroed
      Assert.Equal(100, px[1]);    // G = roughness
      Assert.Equal(200, px[2]);    // R = metalness kept

      var (done, skipped) = Bc5PreviewFix.Run(dir);
      Assert.Equal(1, done);
      Assert.Equal(1, skipped);    // normal map untouched
      Assert.True(new FileInfo(Path.Combine(dir, "x_spec.png")).Length > 1);
      Assert.Equal(1, new FileInfo(Path.Combine(dir, "x_nm.png")).Length);
    }
    finally { Directory.Delete(dir, true); }
  }
}
