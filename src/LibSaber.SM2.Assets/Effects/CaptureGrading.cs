using LibSaber.SM2.Assets.Textures;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   mtl1-35: colour grading LUT. Content LUTs (<c>neutral_lut</c>, <c>story_tower_lut</c>, ... 22 <c>*_lut.td</c> in
///   client_pc) are 32x32x32 B8G8R8A8_UNORM volumes, <c>linear_rgb = false</c>, texel (r, g, b) at byte offset
///   ((b*32 + g)*32 + r)*4 in BGRA order; <c>neutral_lut</c> is the identity (texel i = round(i*255/31)). Values are
///   display-space (sRGB-encoded) and read raw (UNORM, no sRGB decode).
/// </summary>
public sealed class GradingLut
{
  public int Size { get; }
  /// <summary>rgb 0..1, index ((b*Size + g)*Size + r)*3.</summary>
  public float[] Rgb { get; }

  public GradingLut(int size, float[] rgb)
  {
    if (rgb.Length != size * size * size * 3) throw new ArgumentException("LUT size mismatch", nameof(rgb));
    Size = size; Rgb = rgb;
  }

  public static GradingLut Identity(int size = 32)
  {
    var v = new float[size * size * size * 3];
    for (var b = 0; b < size; b++)
    for (var g = 0; g < size; g++)
    for (var r = 0; r < size; r++)
    {
      var o = ((b * size + g) * size + r) * 3;
      v[o] = r / (size - 1f); v[o + 1] = g / (size - 1f); v[o + 2] = b / (size - 1f);
    }
    return new GradingLut(size, v);
  }

  /// <summary>Volume of <paramref name="size"/>^3 BGRA8 texels (r fastest, then g, then b).</summary>
  public static GradingLut FromBgra(ReadOnlySpan<byte> bgra, int size = 32)
  {
    var n = size * size * size;
    if (bgra.Length < n * 4) throw new InvalidDataException($"LUT payload {bgra.Length} < {n * 4}");
    var v = new float[n * 3];
    for (var i = 0; i < n; i++)
    {
      v[i * 3] = bgra[i * 4 + 2] / 255f; v[i * 3 + 1] = bgra[i * 4 + 1] / 255f; v[i * 3 + 2] = bgra[i * 4] / 255f;
    }
    return new GradingLut(size, v);
  }

  /// <summary>A loaded content LUT texture (32x32 with 32 slices, 4 bytes per texel).</summary>
  public static GradingLut FromTexture(TextureDocument doc)
  {
    var size = doc.Width;
    if (doc.Height != size || doc.Depth != size) throw new InvalidDataException($"not a cube LUT: {doc.Width}x{doc.Height}x{doc.Depth}");
    // DXGI 87 B8G8R8A8_UNORM, 91 _SRGB (raw bytes either way: s1996 samples the values as stored), 28/29 R8G8B8A8.
    var bgra = doc.Payload.AsSpan(0, size * size * size * 4).ToArray();
    if (doc.DxgiFormat is 28 or 29)
      for (var i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
    return FromBgra(bgra, size);
  }

  /// <summary>Trilinear sample at texture coordinates 0..1 (clamped), like a linear-filtered Texture3D.</summary>
  public (float R, float G, float B) Sample(float u, float v, float w)
  {
    float x = Math.Clamp(u * Size - 0.5f, 0f, Size - 1f), y = Math.Clamp(v * Size - 0.5f, 0f, Size - 1f), z = Math.Clamp(w * Size - 0.5f, 0f, Size - 1f);
    int x0 = (int)x, y0 = (int)y, z0 = (int)z;
    int x1 = Math.Min(x0 + 1, Size - 1), y1 = Math.Min(y0 + 1, Size - 1), z1 = Math.Min(z0 + 1, Size - 1);
    float fx = x - x0, fy = y - y0, fz = z - z0;
    float r = 0, g = 0, b = 0;
    for (var k = 0; k < 8; k++)
    {
      var xi = (k & 1) == 0 ? x0 : x1; var yi = (k & 2) == 0 ? y0 : y1; var zi = (k & 4) == 0 ? z0 : z1;
      var wgt = ((k & 1) == 0 ? 1 - fx : fx) * ((k & 2) == 0 ? 1 - fy : fy) * ((k & 4) == 0 ? 1 - fz : fz);
      var o = ((zi * Size + yi) * Size + xi) * 3;
      r += Rgb[o] * wgt; g += Rgb[o + 1] * wgt; b += Rgb[o + 2] * wgt;
    }
    return (r, g, b);
  }

  /// <summary>Lookup of a display (sRGB-encoded) colour with the s1996 coordinate transform: uvw = c * scale + offset
  /// (scale = (N-1)/N, offset = 0.5/N, i.e. 0.96875/0.015625 for 32^3, 0.9375/0.03125 for 16^3).</summary>
  public (float R, float G, float B) Lookup(float r, float g, float b)
  {
    var scale = (Size - 1f) / Size; var offset = 0.5f / Size;
    return Sample(r * scale + offset, g * scale + offset, b * scale + offset);
  }
}

/// <summary>
///   mtl1-35: TONEMAP_LUT bake, compute shader s1996 (smoke 88302 eid 3226, plasma eid 5994, outdoor 137266 eid 39915;
///   32x32x32 R16G16B16A16 output HDR_LUT_OUT). Thread (i, j, k) -> u = (i, j, k)/31;
///   x = max(0, 0.18*2^((u - GREY_POINT)*RANGE_LOG)) - max(0, 0.18*2^(-GREY_POINT*RANGE_LOG)) (GREY 0.43, RANGE 14);
///   t = sRGB(sat(Hable(x) * WHITEPOINT_SCALE)); out = lerp(PREV(t*sP+oP), NEXT(t*sN+oN), INTERPOLATION_WEIGHT).
///   Captures: weight 1.0 (= NEXT). smoke/plasma NEXT = runtime COLOR_GRADING_0x 16^3 (contents not dumped: identity
///   is HEURISTIC); outdoor NEXT = content texture story_tower_lut (32^3, exact).
///   Apply side PROVEN (mtl1-38, static hdr PS 44/47, docs/handoff/SHADERS.md): c = EXPOSURE*(min(scene,64512) +
///   min(bloom,64512)); uvw = sat(0.43 + log2((c + c0)/0.18)/14)*31/32 + 1/64, c0 = 0.18*2^(-0.43*14), i.e. exactly
///   <see cref="Coordinate"/> followed by the texel-centre scale/offset of <see cref="GradingLut.Lookup"/>. PS 47 adds a
///   triangular dither (not modelled); output alpha 0.
/// </summary>
public static class CaptureLut
{
  public const int Size = 32;

  /// <summary>Exposed HDR value of LUT coordinate <paramref name="u"/> (0..1), s1996.</summary>
  public static float Input(float u)
  {
    const float grey = CaptureTonemap.LutGreyPoint, range = CaptureTonemap.LutRangeLog;
    return MathF.Max(0f, 0.18f * MathF.Pow(2f, (u - grey) * range)) - MathF.Max(0f, 0.18f * MathF.Pow(2f, -grey * range));
  }

  /// <summary>Inverse of <see cref="Input"/>, clamped to 0..1: the PROVEN apply-side mapping (hdr PS 44/47).</summary>
  public static float Coordinate(float exposed)
  {
    const float grey = CaptureTonemap.LutGreyPoint, range = CaptureTonemap.LutRangeLog;
    var c0 = 0.18f * MathF.Pow(2f, -grey * range);
    return Math.Clamp(grey + MathF.Log2((MathF.Max(exposed, 0f) + c0) / 0.18f) / range, 0f, 1f);
  }

  /// <summary>The 32^3 TONEMAP_LUT (rgb display values, same layout as <see cref="GradingLut"/>).</summary>
  public static GradingLut Bake(GradingLut? next = null, GradingLut? prev = null, float weight = 1f, float whitePointScale = CaptureTonemap.WhitePointScale)
  {
    var v = new float[Size * Size * Size * 3];
    var t = new float[Size];
    for (var i = 0; i < Size; i++)
      t[i] = VfxFrameExport.Srgb(Math.Clamp(CaptureTonemap.Curve(Input(i / (Size - 1f))) * whitePointScale, 0f, 1f));
    for (var k = 0; k < Size; k++)
    for (var j = 0; j < Size; j++)
    for (var i = 0; i < Size; i++)
    {
      float r = t[i], g = t[j], b = t[k];
      var (nr, ng, nb) = next?.Lookup(r, g, b) ?? (r, g, b);
      var (pr, pg, pb) = prev?.Lookup(r, g, b) ?? (r, g, b);
      var o = ((k * Size + j) * Size + i) * 3;
      v[o] = pr + (nr - pr) * weight; v[o + 1] = pg + (ng - pg) * weight; v[o + 2] = pb + (nb - pb) * weight;
    }
    return new GradingLut(Size, v);
  }

  /// <summary>Display colour (sRGB-encoded 0..1) of an exposed HDR colour through a baked LUT (HEURISTIC apply).</summary>
  public static (float R, float G, float B) Apply(GradingLut baked, float r, float g, float b)
    => baked.Lookup(Coordinate(r), Coordinate(g), Coordinate(b));

  /// <summary>Baked LUT as a 2D strip (width Size*Size, height Size; x = r + Size*b, y = g), BGRA8 for PNG export.</summary>
  public static byte[] Strip(GradingLut lut)
  {
    var n = lut.Size; var px = new byte[n * n * n * 4];
    for (var b = 0; b < n; b++)
    for (var g = 0; g < n; g++)
    for (var r = 0; r < n; r++)
    {
      var s = ((b * n + g) * n + r) * 3; var d = (g * n * n + b * n + r) * 4;
      px[d] = Q(lut.Rgb[s + 2]); px[d + 1] = Q(lut.Rgb[s + 1]); px[d + 2] = Q(lut.Rgb[s]); px[d + 3] = 255;
    }
    return px;
  }

  private static byte Q(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
