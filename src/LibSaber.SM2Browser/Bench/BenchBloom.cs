namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   vfx3y bench bloom (HEURISTIC, not the game's post chain): the bloom source is a mesh-only pass whose colour is
///   weighted by the material <c>emissive.bloomIntensity</c> (EffectRenderer.BloomGain) over the clear colour. It is
///   blurred (3 box passes, radius SM2_VFX_BLOOM_RADIUS px, default 10) and added with SM2_VFX_BLOOM_STRENGTH (default 1.5).
/// </summary>
internal static class BenchBloom
{
  private static readonly int Radius = int.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_BLOOM_RADIUS"), out var r) ? r : 10;
  private static readonly float Strength = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_BLOOM_STRENGTH"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1.5f;
  private static readonly int[] Clear = [28, 24, 22];

  public static void Apply(byte[] dst, byte[] src, int w, int h)
  {
    var buf = new float[3][];
    for (var c = 0; c < 3; c++)
    {
      var ch = new float[w * h];
      for (var i = 0; i < w * h; i++) ch[i] = Math.Max(0, src[i * 4 + c] - Clear[c]) / 255f;
      for (var pass = 0; pass < 3; pass++) { Box(ch, w, h, true); Box(ch, w, h, false); }
      buf[c] = ch;
    }
    for (var i = 0; i < w * h; i++)
      for (var c = 0; c < 3; c++)
        dst[i * 4 + c] = (byte)Math.Clamp(dst[i * 4 + c] + buf[c][i] * Strength * 255f, 0f, 255f);
  }

  public static void Box(float[] a, int w, int h, bool horizontal)
  {
    var n = horizontal ? w : h;
    var lines = horizontal ? h : w;
    var tmp = new float[n];
    for (var l = 0; l < lines; l++)
    {
      int Idx(int k) => horizontal ? l * w + k : k * w + l;
      float sum = 0;
      for (var k = -Radius; k <= Radius; k++) sum += a[Idx(Math.Clamp(k, 0, n - 1))];
      for (var k = 0; k < n; k++)
      {
        tmp[k] = sum / (2 * Radius + 1);
        sum += a[Idx(Math.Min(n - 1, k + Radius + 1))] - a[Idx(Math.Max(0, k - Radius))];
      }
      for (var k = 0; k < n; k++) a[Idx(k)] = tmp[k];
    }
  }
}
