using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>One draw: every live particle of one emitter as camera-facing quads, ready for a vertex buffer.</summary>
public sealed class SpriteBatch
{
  /// <summary>Floats per vertex: position(3) uv(2) colour(4, linear HDR rgb + alpha) alphaClip(1) softDepth(1).</summary>
  public const int Stride = 13;
  public required EmitterDef Emitter { get; init; }
  public required TextureFrames Frames { get; init; }
  public float[] Vertices { get; set; } = [];
  public int QuadCount { get; set; }
  /// <summary>View-space depth of the emitter's particles (larger = farther); batches draw far to near.</summary>
  public float Depth { get; set; }
  /// <summary>Refraction pass: <see cref="EmitterDef.DistortTexture"/> quads whose colour is (power, 0, 0, alpha).</summary>
  public bool Distortion { get; init; }
}

/// <summary>
///   Turns an <see cref="EffectInstance"/> into sprite quads for a camera. Per particle: size over age
///   (<c>ageSize</c> @0x143645588 or <c>constExprRange</c>), colour <c>(t,A,R,G,B)</c> over age times
///   <c>colorIntensity</c> @0x1435630b8 (HDR) and <c>alphaIntensity</c> @0x143645238, flipbook frame from
///   <c>speedFrame</c> @0x143644718 (<c>constExpr {framerate, frameOffset}</c> in frames/s, or <c>ageSpline</c> of
///   frame over normalized age; <c>isFrameCycle</c> @0x143644708 wraps, else clamps) on the
///   <see cref="PartAliases"/> grid, rotation <c>transforms.rotation {angle, speed}</c> in degrees,
///   <c>isRandomFlip</c> @0x1436452e8, velocity alignment (<c>isAlignSpeed</c>, <c>ratioXY</c>) and
///   <c>sorting</c> @0x143645dd8 inside the batch.
/// </summary>
public static class EffectSprites
{
  public static List<SpriteBatch> Build(EffectInstance instance, PartAliases aliases, Matrix4x4 view, Func<EmitterDef, bool>? include = null)
  {
    Matrix4x4.Invert(view, out var inv);
    var right = Vector3.Normalize(new Vector3(inv.M11, inv.M12, inv.M13));
    var up = Vector3.Normalize(new Vector3(inv.M21, inv.M22, inv.M23));
    var back = Vector3.Normalize(new Vector3(inv.M31, inv.M32, inv.M33));
    var eye = inv.Translation;
    var batches = new List<SpriteBatch>();
    foreach (var state in instance.Emitters)
    {
      var def = state.Def;
      if (Debug && state.Particles.Count > 0)
        Console.Error.WriteLine($"spr {def.Name}: n {state.Particles.Count} sprite {def.IsSprite} visible {def.Visible} target {def.Target} tex '{def.Texture}' distort '{def.DistortTexture}' blend {def.Blend}");
      if (!def.IsSprite || (!def.Visible && !ForceVisible) || def.Target == DrawTarget.Simulation || state.Particles.Count == 0 || (include is not null && !include(def)))
        continue;
      var passes = new List<bool>(2);
      if (def.Target == DrawTarget.Color) passes.Add(false);
      if (def.DistortTexture is { Length: > 0 }) passes.Add(true);
      foreach (var distort in passes)
      {
      var frames = aliases[distort ? def.DistortTexture : def.Texture];
      var list = new List<(Particle P, float Key)>(state.Particles.Count);
      var depthSum = 0f;
      foreach (var p in state.Particles)
      {
        var d = Vector3.Dot(p.Position - eye, -back);
        depthSum += d;
        var key = def.Sorting switch
        {
          EffectSorting.SpawnOrder => p.Order,
          EffectSorting.SpawnOrderReversed => -p.Order,
          _ => -d,
        };
        list.Add((p, key));
      }

      list.Sort((a, b) => a.Key.CompareTo(b.Key));
      var vertices = new float[list.Count * 4 * SpriteBatch.Stride];
      var quads = 0;
      foreach (var (p, _) in list)
        if (Quad(def, frames, p, right, up, back, vertices, quads * 4 * SpriteBatch.Stride, distort, eye))
          quads++;
      if (quads == 0) continue;
      batches.Add(new SpriteBatch { Emitter = def, Frames = frames, Vertices = vertices, QuadCount = quads, Depth = depthSum / list.Count, Distortion = distort });
      }
    }

    batches.AddRange(EffectTrailSprites.Build(instance, eye, include));
    // Far to near between emitters; definition order breaks ties (emitters sharing a source).
    return batches.Select((b, i) => (b, i)).OrderByDescending(x => MathF.Round(x.b.Depth, 2)).ThenBy(x => x.i).Select(x => x.b).ToList();
  }

  /// <summary>Scene light reaching lit (<c>lighting != None</c>) particles, in the same HDR units as <c>colorIntensity</c>.
  /// Corpus: unlit additive intensity median ~450, lit blend median ~10, so a lit sprite sees roughly 100x (tuned against unlit fire at exposure 1/250).</summary>
  /// <remarks>
  ///   vfx3z: settled at 400. Lit particles (particles.sdc lit permutations) read only runtime scene light:
  ///   COMMON_SUN_INTENSITY @0x1436e9ed0, COMMON_SKY_SH_HARMONICS @0x1436eb790 and the clustered light grid
  ///   (COMMON_LIGHT_GRID_*), then scale by COMMON_HDR_EXPOSURE_DIV_AVG_LUM @0x1436e9ee8 (exposure / average luminance).
  ///   No per-effect light constant exists, so only LitScale x Exposure matters: with auto-exposure a lit albedo ends
  ///   near scene-average brightness, i.e. LitScale x (1/250) ~ 1..2 → 250..500. 400 matches the in-game grenade/muzzle
  ///   smoke refs (154743, 154843) best. The number itself stays HEURISTIC (bench has no scene); SM2_VFX_LIT overrides.
  /// </remarks>
  public static float LitScale { get; set; } = 400f;

  public static float Frame(EmitterDef def, TextureFrames frames, in Particle p)
  {
    var n = Math.Clamp(p.Age / p.Life, 0f, 1f);
    var f = def.FrameMode switch
    {
      FrameMode.Constant => p.FrameStart + p.Age * p.FrameRate,
      FrameMode.AgeSpline => def.FrameSpline.Sample(n, p.RandFrame),
      _ => 0f,
    };
    var max = frames.FrameMax;
    if (def.FrameCycle)
    {
      f %= max;
      if (f < 0) f += max;
    }

    return f;
  }

  /// <summary>When false, <see cref="EmitterDef.DegNear"/>/<see cref="EmitterDef.DegFar"/> are ignored (debug).</summary>
  public static bool DistanceFades { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_DEG") != "0";

  /// <summary>SM2_VFX_DEGNEAR_FIX=1: degNear scale applies near the camera (as proven for trails), not beyond radEnd.</summary>
  public static bool DegNearFix { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_DEGNEAR_FIX") == "1";

  /// <summary>SM2_VFX_SPRDBG=1: print per-emitter sprite state and the first reject reason (diagnostics only).</summary>
  public static bool Debug { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_SPRDBG") == "1";
  /// <summary>Debug SM2_VFX_FORCE_VISIBLE=1: draw emitters whose isVisible (+0x88) is false. Not exe behaviour; used to check
  /// gameplay-toggled effects (e.g. part_mag_chainsaw_blood_splash_red_01, all three emitters hidden) against in-game refs.</summary>
  public static bool ForceVisible { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_FORCE_VISIBLE") == "1";
  private static readonly Dictionary<string, int> DbgSeen = new();
  private static void Reject(EmitterDef def, string why) { if (Debug) Console.Error.WriteLine($"  reject {def.Name}: {why}"); }

  /// <summary>
  ///   <c>optimization.degNear/degFar</c> factors (size, intensity, alpha, distortion) at camera distance
  ///   <paramref name="dist"/>. Shared by sprites and lwiGeom mesh particles (same emitter optimization block).
  /// </summary>
  public static (float Size, float Intens, float Alpha, float Disp) DistanceFadeK(EmitterDef def, float dist)
  {
    float sizeK = 1, intensK = 1, alphaK = 1, dispK = 1;
    if (!DistanceFades || (def.DegNear is null && def.DegFar is null)) return (sizeK, intensK, alphaK, dispK);
    for (var fi = 0; fi < 2; fi++)
    {
      var fade = fi == 0 ? def.DegNear : def.DegFar;
      if (fade is null) continue;
      // HEURISTIC (SM2_VFX_DEGNEAR_FIX=1): use the trail-VS near semantics (scale inside radStart) for sprites too.
      var k = fi == 0 && DegNearFix ? 1 - fade.Amount(dist) : fade.Amount(dist);
      sizeK *= 1 + (fade.Size - 1) * k;
      intensK *= 1 + (fade.Intens - 1) * k;
      alphaK *= 1 + (fade.Alpha - 1) * k;
      dispK *= 1 + (fade.Disp - 1) * k;
    }
    return (sizeK, intensK, alphaK, dispK);
  }

  private static bool Quad(EmitterDef def, TextureFrames frames, in Particle p, Vector3 right, Vector3 up, Vector3 back, float[] v, int o, bool distort = false, Vector3? eye = null)
  {
    var n = Math.Clamp(p.Age / p.Life, 0f, 1f);
    var (sizeK, intensK, alphaK, dispK) = eye is { } cam ? DistanceFadeK(def, Vector3.Distance(p.Position, cam)) : (1f, 1f, 1f, 1f);

    var size = def.Size.Sample(n, p.RandSize) * def.Scale * sizeK;
    if (size <= 0) { Reject(def, $"size {size} n {n}"); return false; }
    var c = def.Color.Evaluate(n);
    var intensity = def.ColorIntensity.Sample(n, p.RandIntensity) * intensK;
    var alpha = c.W * def.AlphaIntensity * alphaK;
    if (alpha <= 0.0005f && def.Blend != EffectBlend.Add) { Reject(def, $"alpha {alpha} c.W {c.W} aInt {def.AlphaIntensity} aK {alphaK} n {n}"); return false; }
    var rgb = new Vector3(c.X, c.Y, c.Z) * intensity;
    if (def.Lighting is not ("None" or ""))
      rgb *= LitScale * Math.Clamp(def.AmbientLight + 0.55f, 0f, 1f);
    if (Debug && DbgSeen.TryGetValue(def.Name, out var seen) is var _ && seen < 4)
    {
      DbgSeen[def.Name] = seen + 1;
      Console.Error.WriteLine($"  quad {def.Name}: n {n:0.00} size {size:0.000} rgb {rgb} alpha {alpha:0.000} life {p.Life:0.000} mclip {(def.MaskClip is { } dm ? dm.Sample(n, 0) : -1):0.00} mask {def.AlphaMask} lit '{def.Lighting}'");
    }
    if (distort)
    {
      rgb = new Vector3(def.DistortPower.Sample(n, p.RandIntensity) * dispK, 0f, 0f);
      if (rgb.X <= 0f) return false;
      alpha = Math.Clamp(c.W * def.AlphaIntensity * alphaK, 0f, 1f);
      if (def.Target == DrawTarget.Distortion && def.ParticleType == "distortion") alpha = alphaK; // refraction-only: colour curve unused
    }

    var frame = Frame(def, frames, p);
    if (def.KillAfterSequence && !def.FrameCycle && frame >= frames.FrameMax) { Reject(def, $"frame {frame} >= {frames.FrameMax}"); return false; }
    var index = Math.Clamp((int)MathF.Floor(frame), 0, frames.FrameMax - 1);
    var fx = index % frames.FramesX;
    var fy = index / frames.FramesX;
    var u0 = fx / (float)frames.FramesX;
    var v0 = fy / (float)frames.FramesY;
    var u1 = (fx + 1) / (float)frames.FramesX;
    var v1 = (fy + 1) / (float)frames.FramesY;
    if (p.Flip) (u0, u1) = (u1, u0);

    Vector3 ax, ay;
    var halfW = size;
    var halfH = size;
    var vel = p.Velocity;
    var aligned = (def.AlignSpeed || def.StreakRatio > 0) && vel.LengthSquared() > 1e-6f;
    if (aligned)
    {
      var screen = vel - Vector3.Dot(vel, back) * back;
      if (screen.LengthSquared() < 1e-8f) screen = up;
      ay = Vector3.Normalize(screen);
      ax = Vector3.Cross(ay, back);
      var ratio = def.RatioXY.X;
      if (def.StreakRatio > 0) ratio = Math.Max(1f, 1f + vel.Length() * def.StreakRatio);
      halfH = size * Math.Max(0.01f, ratio);
    }
    else
    {
      var cs = MathF.Cos(p.Rotation);
      var sn = MathF.Sin(p.Rotation);
      ax = right * cs + up * sn;
      ay = -right * sn + up * cs;
      var ratio = def.RatioXY.X;
      if (MathF.Abs(ratio - 1f) > 1e-4f) halfH = size * ratio;
    }

    var center = p.Position + ax * (def.LocalOffset.X * halfW * 2f) + ay * (def.LocalOffset.Y * halfH * 2f);
    var clip = Math.Clamp(def.AlphaClip.Sample(n, p.RandClip), 0f, 0.999f);
    if (EmitterDef.MaskClipEnabled && def.MaskClip is { } mclip) clip = Math.Max(clip, Math.Clamp(mclip.Sample(n, p.RandClip), 0f, 0.999f));
    Span<(float X, float Y, float U, float V)> corners = [(-1, -1, u0, v1), (1, -1, u1, v1), (1, 1, u1, v0), (-1, 1, u0, v0)];
    var mc = MathF.Cos(p.MaskAngle);
    var ms = MathF.Sin(p.MaskAngle);
    foreach (var (x, y, u, w) in corners)
    {
      var mu = 0.5f + 0.5f * (x * mc - y * ms);
      var mv = 0.5f - 0.5f * (x * ms + y * mc);
      var pos = center + ax * (x * halfW) + ay * (y * halfH);
      v[o++] = pos.X; v[o++] = pos.Y; v[o++] = pos.Z;
      v[o++] = u; v[o++] = w;
      v[o++] = rgb.X; v[o++] = rgb.Y; v[o++] = rgb.Z; v[o++] = alpha;
      v[o++] = clip; v[o++] = def.SoftDepth;
      v[o++] = mu; v[o++] = mv;
    }

    return true;
  }
}
