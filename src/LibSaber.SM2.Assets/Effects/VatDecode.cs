using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
/// Blood-splash VAT decode, transcribed from the combine-material VS (pc_dx12_combine_material.sdc records 00000/00001,
/// e.g. 00000_0; exe permutation string COMBINE_MATERIAL_SHADER0_BLOOD_SPLASH). Material cb (binding 3) rows 6..8 hold
/// floatParam0..: [6].xyz = boundsMin (fp0-2), [6].w,[7].x,[7].y = scale (fp3-5), [7].z = rate (fp6), [7].w = frames (fp7),
/// [8].z = bands (fp10). HEURISTIC: the row-6 = floatParam0 alignment is inferred from the values matching solid_blood.td.
/// Textures: [18].x = anim_data (position, RGB8), [19].y = addInfo _n (normal). The _d texture is NOT read by this VS.
/// </summary>
public static class VatDecode
{
  /// <summary>Free-running frame: frames*frac(seed/frames) + rate*time (VS fallback path when no anim control).</summary>
  public static float FreeFrame(float seed, float frames, float rate, float time)
  {
    var q = seed / frames;
    var fr = q - MathF.Truncate(q);
    return frames * fr + rate * time;
  }

  /// <summary>Anim-control path (flags&amp;25==25): frame = animStage * (frames - 1).</summary>
  public static float StageFrame(float animStage, float frames) => animStage * (frames - 1);

  /// <summary>Texture V for integer frame f: bands&lt;2 -> -f/frames (wrap); else uv1.y - frac(f/frames)/bands.</summary>
  public static float RowV(float uv1Y, float frame, float frames, int bands)
  {
    var q = frame / frames;
    if (bands < 2) return -q;
    var fr = MathF.Abs(q) - MathF.Floor(MathF.Abs(q));
    return uv1Y - (q >= 0 ? fr : -fr) / bands;
  }

  /// <summary>Returns (v0, v1, t): the two rows sampled and the lerp weight.</summary>
  public static (float V0, float V1, float T) Rows(float uv1Y, float frame, float frames, int bands)
  {
    var f0 = MathF.Floor(frame);
    return (RowV(uv1Y, f0, frames, bands), RowV(uv1Y, f0 + 1, frames, bands), frame - f0);
  }

  /// <summary>Local position = boundsMin + texel.rgb(0..1) * 255 * scale.</summary>
  public static Vector3 Position(Vector3 texel, Vector3 boundsMin, Vector3 scale) => boundsMin + texel * 255f * scale;

  /// <summary>Normal = texel * 2.007874 - 1.007874 (= (b*255 - 128)/127), normalised after the instance transform.</summary>
  public static Vector3 Normal(Vector3 texel) => texel * 2.007874f - new Vector3(1.007874f);
}
