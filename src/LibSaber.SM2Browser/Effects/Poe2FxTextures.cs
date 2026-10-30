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
    if (lum)
      for (var i = 0; i + 3 < rgba.Length; i += 4) rgba[i + 3] = (byte)Math.Min(rgba[i + 3], Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])));
    else if (path.Contains("SIM_", StringComparison.OrdinalIgnoreCase)) // packed smoke sim maps: density ~ luminance
      for (var i = 0; i + 3 < rgba.Length; i += 4)
      {
        // HEURISTIC: SIM_* maps pack normal/density, not albedo; density (max channel, capped by alpha) becomes alpha and
        // the colour comes from the material tint (SIM_Smoke2 04.Albedo_RGB).
        rgba[i + 3] = (byte)Math.Min(rgba[i + 3], Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])));
        rgba[i] = rgba[i + 1] = rgba[i + 2] = 200;
      }
    else if (opaque)
      for (var i = 0; i + 3 < rgba.Length; i += 4) rgba[i + 3] = Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2]));
    return new EffectImage(w, h, rgba);
  });
}
