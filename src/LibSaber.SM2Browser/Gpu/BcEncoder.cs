// R85: minimal CPU BC1 / BC3 (DXT1 / DXT5) encoder for preview textures (bounding-box endpoints with a 1/16 inset,
// nearest-on-line indices). Quality is preview grade, not a production encoder. RGBA8 input, rows top-down.
namespace LibSaber.SM2Browser.Gpu;

internal static class BcEncoder
{
  public static bool HasAlpha(byte[] rgba)
  {
    for (var i = 3; i < rgba.Length; i += 4)
      if (rgba[i] < 250) return true;
    return false;
  }

  public static byte[] Encode(byte[] rgba, int w, int h, bool alpha)
  {
    int bw = (w + 3) / 4, bh = (h + 3) / 4, bs = alpha ? 16 : 8;
    var dst = new byte[bw * bh * bs];
    Parallel.For(0, bh, by =>
    {
      Span<byte> blk = stackalloc byte[64];
      for (var bx = 0; bx < bw; bx++)
      {
        for (var py = 0; py < 4; py++)
          for (var px = 0; px < 4; px++)
          {
            int x = Math.Min(w - 1, bx * 4 + px), y = Math.Min(h - 1, by * 4 + py);
            int s = (y * w + x) * 4, d = (py * 4 + px) * 4;
            blk[d] = rgba[s]; blk[d + 1] = rgba[s + 1]; blk[d + 2] = rgba[s + 2]; blk[d + 3] = rgba[s + 3];
          }
        var o = (by * bw + bx) * bs;
        if (alpha) { Alpha(blk, dst.AsSpan(o, 8)); Color(blk, dst.AsSpan(o + 8, 8)); }
        else Color(blk, dst.AsSpan(o, 8));
      }
    });
    return dst;
  }

  static void Alpha(ReadOnlySpan<byte> b, Span<byte> o)
  {
    int lo = 255, hi = 0;
    for (var i = 0; i < 16; i++) { int a = b[i * 4 + 3]; if (a < lo) lo = a; if (a > hi) hi = a; }
    o[0] = (byte)hi; o[1] = (byte)lo;
    ulong bits = 0;
    if (hi > lo)
      for (var i = 0; i < 16; i++)
      {
        int a = b[i * 4 + 3];
        var t = (int)Math.Round((a - lo) * 7.0 / (hi - lo)); // 0 = lo .. 7 = hi
        int idx = t == 7 ? 0 : t == 0 ? 1 : 8 - t; // a0 > a1 mode: idx 0 = a0 (hi), 1 = a1 (lo), 2..7 = (8-idx)/7 hi
        bits |= (ulong)idx << (3 * i);
      }
    for (var i = 0; i < 6; i++) o[2 + i] = (byte)(bits >> (8 * i));
  }

  static int To565(int r, int g, int b) => ((r * 31 + 127) / 255 << 11) | ((g * 63 + 127) / 255 << 5) | ((b * 31 + 127) / 255);
  static (int, int, int) From565(int c) { int r = c >> 11 & 31, g = c >> 5 & 63, b = c & 31; return ((r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 2)); }

  static void Color(ReadOnlySpan<byte> b, Span<byte> o)
  {
    int r0 = 255, g0 = 255, b0 = 255, r1 = 0, g1 = 0, b1 = 0;
    for (var i = 0; i < 16; i++)
    {
      int r = b[i * 4], g = b[i * 4 + 1], bl = b[i * 4 + 2];
      if (r < r0) r0 = r; if (g < g0) g0 = g; if (bl < b0) b0 = bl;
      if (r > r1) r1 = r; if (g > g1) g1 = g; if (bl > b1) b1 = bl;
    }
    // inset 1/16 of the range
    int ir = (r1 - r0) >> 4, ig = (g1 - g0) >> 4, ib = (b1 - b0) >> 4;
    int cMax = To565(Math.Min(255, r1 - ir), Math.Min(255, g1 - ig), Math.Min(255, b1 - ib));
    int cMin = To565(Math.Max(0, r0 + ir), Math.Max(0, g0 + ig), Math.Max(0, b0 + ib));
    uint idx = 0;
    if (cMax < cMin) (cMax, cMin) = (cMin, cMax);
    if (cMax != cMin)
    {
      var (ar, ag, ab) = From565(cMax); var (br, bg, bb) = From565(cMin);
      Span<int> pr = stackalloc int[4], pg = stackalloc int[4], pb = stackalloc int[4];
      pr[0] = ar; pg[0] = ag; pb[0] = ab; pr[1] = br; pg[1] = bg; pb[1] = bb;
      pr[2] = (2 * ar + br) / 3; pg[2] = (2 * ag + bg) / 3; pb[2] = (2 * ab + bb) / 3;
      pr[3] = (ar + 2 * br) / 3; pg[3] = (ag + 2 * bg) / 3; pb[3] = (ab + 2 * bb) / 3;
      for (var i = 0; i < 16; i++)
      {
        int r = b[i * 4], g = b[i * 4 + 1], bl = b[i * 4 + 2], best = 0, bd = int.MaxValue;
        for (var k = 0; k < 4; k++)
        {
          int dr = r - pr[k], dg = g - pg[k], db = bl - pb[k], dd = dr * dr + dg * dg + db * db;
          if (dd < bd) { bd = dd; best = k; }
        }
        idx |= (uint)best << (2 * i);
      }
    }
    o[0] = (byte)cMax; o[1] = (byte)(cMax >> 8); o[2] = (byte)cMin; o[3] = (byte)(cMin >> 8);
    o[4] = (byte)idx; o[5] = (byte)(idx >> 8); o[6] = (byte)(idx >> 16); o[7] = (byte)(idx >> 24);
  }
}
