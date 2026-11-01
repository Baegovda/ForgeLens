using LibSaber.SM2.Assets.Games.Poe2;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Effects;

/// <summary>
///   PoE2 (M2 D): particle textures by game path, decoded from the prepared raw .dds by <see cref="Poe2Reader.Texture"/>.
///   HEURISTIC: a texture without an alpha channel (DXT1 / BC7 with alpha all 255, e.g. glow_star_015, flash_mid) gets
///   alpha = max(R,G,B), the luminance-as-alpha the PoE particle graphs (LuminanceAlpha, ...) apply; otherwise the SM2 blend
///   draws the black background as a square.
/// </summary>
internal static class Poe2FxTextures
{
  public static EffectTextures For(Poe2Reader reader) => EffectTextures.FromImages(path =>
  {
    // M2 E: "#strip" = first column of an N x 1 lightning sheet, rotated 90 degrees so the bolt runs along the trail
    // (LIGHTNING_Main "Lightning Texture (4x1)" + TRAIL_UVSelect "Rotate UV" 0.5 on lightningSpark.mat).
    var strip = path.EndsWith("#strip", StringComparison.Ordinal);
    if (strip) path = path[..^6];
    var lum = path.EndsWith("#lum", StringComparison.Ordinal);
    if (lum) path = path[..^4];
    var img = reader.Texture(path);
    if (img is null) return null;
    var rgba = (byte[])img.Bgra.Clone();
    var (w, h) = (img.Width, img.Height);
    if (strip && LibSaber.SM2.Assets.Games.Poe2.Poe2Particles.StripColumns(path) is var cols and > 1)
    {
      var cw = w / cols;
      var rot = new byte[cw * h * 4];
      // output width h (along the trail), height cw (across); out(x=y_src, y=x_src)
      for (var y = 0; y < h; y++)
        for (var x = 0; x < cw; x++)
          Buffer.BlockCopy(rgba, (y * w + x) * 4, rot, (x * h + y) * 4, 4);
      rgba = rot; (w, h) = (h, cw);
    }
    EffectTextures.SwapRedBlue(rgba);
    var opaque = true;
    for (var i = 3; i < rgba.Length && opaque; i += 4) opaque = rgba[i] == 255;
    var grey = true; // M2 H: grey luminance masks (gradients) carry no colour; the Colour/tint graphs do
    for (var i = 0; i + 3 < rgba.Length && grey; i += 4 * 97) grey = Math.Abs(rgba[i] - rgba[i + 1]) < 12 && Math.Abs(rgba[i + 1] - rgba[i + 2]) < 12;
    if (lum)
      for (var i = 0; i + 3 < rgba.Length; i += 4)
      {
        rgba[i + 3] = (byte)Math.Min(rgba[i + 3], Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])));
        if (grey && path.Contains("/gradients/", StringComparison.OrdinalIgnoreCase)) rgba[i] = rgba[i + 1] = rgba[i + 2] = 255; // M2 H: Glacial Bolt mist drew dark (alpha-blended dark texels)
      }
    else if (path.Contains("SIM_", StringComparison.OrdinalIgnoreCase)) // packed smoke sim maps: density ~ luminance
      for (var i = 0; i + 3 < rgba.Length; i += 4)
      {
        // HEURISTIC: SIM_* maps pack normal/density, not albedo; density (max channel, capped by alpha) becomes alpha and
        // the colour comes from the material tint (SIM_Smoke2 04.Albedo_RGB).
        rgba[i + 3] = (byte)Math.Min(rgba[i + 3], Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])));
        // M2 H: shade from the packed normal (R,G = normal x,y; HEURISTIC layout) with a light from the upper left, instead of flat 200
        var nx = rgba[i] / 127.5f - 1f; var ny = rgba[i + 1] / 127.5f - 1f;
        var shade = Math.Clamp(0.62f + 0.38f * (-0.6f * nx + 0.8f * ny), 0.25f, 1f);
        rgba[i] = rgba[i + 1] = rgba[i + 2] = (byte)(225 * shade);
      }
    else if (opaque)
      for (var i = 0; i + 3 < rgba.Length; i += 4) rgba[i + 3] = Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2]));
    if (path.Contains("/glow/", StringComparison.OrdinalIgnoreCase))
    {
      // M2 H: glow_star cards do not fade to zero at the quad border, so large cards showed a straight cut edge (Frost Bomb).
      // Radial fade to 0 at the inscribed circle.
      for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
          var dx = (x + 0.5f) / w * 2 - 1; var dy = (y + 0.5f) / h * 2 - 1;
          var r = MathF.Sqrt(dx * dx + dy * dy);
          var f = Math.Clamp((1f - r) / 0.35f, 0f, 1f); f = f * f * (3 - 2 * f);
          var k = (y * w + x) * 4;
          rgba[k] = (byte)(rgba[k] * f); rgba[k + 1] = (byte)(rgba[k + 1] * f); rgba[k + 2] = (byte)(rgba[k + 2] * f); rgba[k + 3] = (byte)(rgba[k + 3] * f);
        }
    }
    return new EffectImage(w, h, rgba);
  });
}
