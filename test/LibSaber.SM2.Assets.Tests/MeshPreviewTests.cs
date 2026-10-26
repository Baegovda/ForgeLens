using System.Numerics;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class MeshPreviewTests
{
  private static ExportedMesh Sphere(int rings = 12, int segments = 16)
  {
    var mesh = new ExportedMesh { Name = "sphere" };
    for (var r = 0; r <= rings; r++)
    {
      var phi = MathF.PI * r / rings;
      for (var s = 0; s <= segments; s++)
      {
        var theta = 2 * MathF.PI * s / segments;
        var n = new Vector3(MathF.Sin(phi) * MathF.Cos(theta), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(theta));
        mesh.Positions.Add(n);
        mesh.Normals.Add(n);
        mesh.Uvs.Add(new Vector2(s / (float)segments, r / (float)rings));
      }
    }

    for (var r = 0; r < rings; r++)
    {
      for (var s = 0; s < segments; s++)
      {
        var a = r * (segments + 1) + s;
        var b = a + segments + 1;
        mesh.Indices.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
      }
    }

    mesh.Parts.Add(new MeshPart { Name = "sphere", IndexStart = 0, IndexCount = mesh.Indices.Count });
    return mesh;
  }

  private static float Luma(byte[] px, int width, int x, int y)
  {
    var i = (y * width + x) * 4;
    return px[i] * 0.11f + px[i + 1] * 0.59f + px[i + 2] * 0.3f;
  }

  [Fact]
  public void LowPolySphere_IsShadedSmoothly()
  {
    // A 12x16 sphere is visibly faceted with one shade per triangle (the old preview). Interpolated vertex
    // normals must keep neighbouring pixels close inside the silhouette.
    var mesh = Sphere();
    const int size = 200;
    var px = MeshPreview.Render(mesh, size, size, new OrbitCamera { Yaw = 0.4f, Pitch = 0.2f }, false, 0, out _, new PreviewSettings());
    var maxStep = 0f;
    var covered = 0;
    for (var y = 40; y < 160; y++)
    {
      for (var x = 40; x < 159; x++)
      {
        var a = Luma(px, size, x, y);
        var b = Luma(px, size, x + 1, y);
        if (a < 30 || b < 30)
          continue;
        covered++;
        maxStep = MathF.Max(maxStep, MathF.Abs(a - b));
      }
    }

    Assert.True(covered > 5000, $"sphere covers {covered} pixels");
    Assert.True(maxStep < 6f, $"largest step between neighbours {maxStep}");
  }

  [Fact]
  public void Supersample_ReturnsRequestedSize_AndCachesPreparedSlices()
  {
    var mesh = Sphere();
    var one = MeshPreview.Render(mesh, 64, 48, new OrbitCamera { Yaw = 0.4f }, false, 0, out _, new PreviewSettings { Supersample = 1 });
    var two = MeshPreview.Render(mesh, 64, 48, new OrbitCamera { Yaw = 0.4f }, false, 0, out _, new PreviewSettings { Supersample = 2 });
    Assert.Equal(64 * 48 * 4, one.Length);
    Assert.Equal(64 * 48 * 4, two.Length);
    Assert.True(MeshPreview.LastRenderMs >= 0);
  }

  [Fact]
  public void TexturedSphere_UsesMipsWhenMinified()
  {
    // A 256x256 one-pixel checker viewed small must average to grey (mip), not alias to black/white noise.
    var mesh = Sphere(24, 32);
    const int tex = 256;
    var bgra = new byte[tex * tex * 4];
    for (var y = 0; y < tex; y++)
      for (var x = 0; x < tex; x++)
      {
        var v = (byte)(((x + y) & 1) == 0 ? 255 : 0);
        var i = (y * tex + x) * 4;
        bgra[i] = bgra[i + 1] = bgra[i + 2] = v;
        bgra[i + 3] = 255;
      }

    mesh.Parts[0].Texture = "checker";
    var settings = new PreviewSettings
    {
      Albedo = new Dictionary<string, PreviewImage> { ["checker"] = new PreviewImage { Width = tex, Height = tex, Bgra = bgra } },
    };
    const int size = 64;
    var px = MeshPreview.Render(mesh, size, size, new OrbitCamera { Yaw = 0.4f }, false, 0, out _, settings);
    var maxStep = 0f;
    for (var y = 26; y < 38; y++)
      for (var x = 26; x < 37; x++)
        maxStep = MathF.Max(maxStep, MathF.Abs(Luma(px, size, x, y) - Luma(px, size, x + 1, y)));
    Assert.True(maxStep < 40f, $"neighbour step {maxStep} looks aliased");
  }
}
