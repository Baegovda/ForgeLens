using System.Collections.Concurrent;
using System.Numerics;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Effects;

/// <summary>
///   Particle textures by name, decoded to RGBA on worker threads. Loose mode looks for
///   <c>&lt;root&gt;/**/&lt;name&gt;.pct.resource</c> (case-insensitive: .sfx files say <c>q_sparks_A_01</c>, the archive
///   stores <c>q_sparks_a_01</c>); a custom resolver can read from the game catalog instead.
/// </summary>
internal sealed class EffectTextures
{
  private readonly ConcurrentDictionary<string, Task<EffectImage>> _cache = new(StringComparer.OrdinalIgnoreCase);
  private readonly Func<string, TextureDocument?> _resolve;
  public Action? Loaded { get; set; }

  public EffectTextures(Func<string, TextureDocument?> resolve) => _resolve = resolve;

  public static EffectTextures FromDirectory(string root)
  {
    var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (Directory.Exists(root))
      foreach (var f in Directory.EnumerateFiles(root, "*.pct.resource", SearchOption.AllDirectories))
        files.TryAdd(Path.GetFileName(f)[..^".pct.resource".Length], f);
    return new EffectTextures(name => files.TryGetValue(name, out var path) ? TextureLoader.LoadLoose(path) : null);
  }

  /// <summary>Null while decoding; a 0x0 image when the texture is missing (drawn white).</summary>
  public EffectImage? Get(string name)
  {
    var task = _cache.GetOrAdd(name, n => Task.Run(() =>
    {
      var image = Decode(n);
      Loaded?.Invoke();
      return image;
    }));
    return task.IsCompleted ? task.Result : null;
  }

  public void Prefetch(IEnumerable<string?> names)
  {
    foreach (var n in names)
      if (!string.IsNullOrEmpty(n))
        Get(n);
  }

  public void WaitAll(TimeSpan timeout) => Task.WaitAll(_cache.Values.ToArray(), timeout);

  public bool IsMissing(string name) => _cache.TryGetValue(name, out var t) && t.IsCompleted && t.Result.Width == 0;

  private EffectImage Decode(string name)
  {
    try
    {
      var doc = _resolve(name);
      if (doc is null) return new EffectImage(0, 0, []);
      var rgba = doc.Decode(0, 0, out var w, out var h, out _);
      return rgba is { Length: > 0 } && w > 0 ? new EffectImage(w, h, rgba) : new EffectImage(0, 0, []);
    }
    catch (Exception)
    {
      return new EffectImage(0, 0, []);
    }
  }
}

/// <summary>Orbit camera for the standalone effect view (same lens as the mesh preview).</summary>
internal readonly record struct EffectCamera(Vector3 Center, float Yaw, float Pitch, float Distance)
{
  public (Matrix4x4 View, Matrix4x4 Projection, Vector3 Eye) Matrices(int width, int height)
  {
    var pitch = Math.Clamp(Pitch, -1.45f, 1.45f);
    var planar = MathF.Cos(pitch);
    var eye = Center + new Vector3(planar * MathF.Sin(Yaw), MathF.Sin(pitch), planar * MathF.Cos(Yaw)) * Distance;
    var view = Matrix4x4.CreateLookAt(eye, Center, Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, width / (float)Math.Max(1, height), Math.Max(0.01f, Distance * 0.01f), Distance * 60f);
    return (view, projection, eye);
  }

  /// <summary>Frames the particles an effect produces over its run (10th..90th percentile of positions).</summary>
  public static EffectCamera Fit(LibSaber.SM2.Assets.Effects.EffectInstance instance, float seconds)
  {
    var xs = new List<float>(); var ys = new List<float>(); var zs = new List<float>();
    var sizes = new List<float>();
    for (var i = 1; i <= 24; i++)
    {
      // Whole capture window (frames sample up to 15/16 of it). Sprites only: mesh debris (hierophant stone02_big, 180
      // chunks) flies ~35 m in 3 s and would pull the frame off the core.
      instance.Seek(seconds * i / 24f);
      foreach (var e in instance.Emitters)
      {
        if (!e.Def.IsSprite) continue;
        foreach (var p in e.Particles)
        {
          // The bench has no floor: gravity debris falls for metres below the origin. In game it lands on the ground
          // (y ≈ 0 at the spawn point), so it does not set the frame (HEURISTIC).
          if (p.Position.Y < -0.5f) continue;
          xs.Add(p.Position.X); ys.Add(p.Position.Y); zs.Add(p.Position.Z);
          sizes.Add(e.Def.Size.Sample(Math.Clamp(p.Age / p.Life, 0, 1), 0.5f) * e.Def.Scale);
        }
      }

      foreach (var t in instance.Trails)
        foreach (var p in t.Points)
        {
          xs.Add(p.Position.X); ys.Add(p.Position.Y); zs.Add(p.Position.Z);
          sizes.Add(t.Def.Width.Max * t.Def.Scale);
        }
    }

    if (Environment.GetEnvironmentVariable("SM2_VFX_FITDBG") == "1")
      foreach (var e in instance.Emitters)
        if (e.Particles.Any())
          Console.Error.WriteLine($"  em {e.Def.Name} n {e.Particles.Count()} mean {new Vector3(e.Particles.Average(p => p.Position.X), e.Particles.Average(p => p.Position.Y), e.Particles.Average(p => p.Position.Z))}");
    instance.Reset();
    if (xs.Count == 0) return new EffectCamera(new Vector3(0, 0.5f, 0), 0.6f, 0.25f, 4f);
    float P(List<float> v, float q) { v.Sort(); return v[(int)Math.Clamp(q * (v.Count - 1), 0, v.Count - 1)]; }
    // Centre on the median and size on the 25..75 % box ×1.6 (was 10..90 %): long-flying debris/sparks no longer pull the
    // camera away from the dense core (hierophant bomb: 10..90 % box spanned 32 m, the core is ~3 m). HEURISTIC.
    var lo = new Vector3(P(xs, 0.25f), P(ys, 0.25f), P(zs, 0.25f));
    var hi = new Vector3(P(xs, 0.75f), P(ys, 0.75f), P(zs, 0.75f));
    var med = new Vector3(P(xs, 0.5f), P(ys, 0.5f), P(zs, 0.5f));
    var min = med - (med - lo) * 1.6f;
    var max = med + (hi - med) * 1.6f;
    var center = (min + max) * 0.5f;
    if (Environment.GetEnvironmentVariable("SM2_VFX_FITDBG") == "1")
      Console.Error.WriteLine($"fit: n {xs.Count} p0 ({xs[0]:0.00},{ys[0]:0.00},{zs[0]:0.00}) p100 ({xs[^1]:0.00},{ys[^1]:0.00},{zs[^1]:0.00}) min {min} max {max} size75 {P(sizes, 0.75f):0.000}");
    // Small effects (muzzle, blood, candle): the particle spread can be near zero, so the sprite size sets the frame
    // and the minimum distance is 0.15 (was 0.4) so a 5 cm puff still fills a useful part of the view.
    var extent = Math.Max((max - min).Length() * 0.5f + Math.Min(P(sizes, 0.75f), 3f), Math.Min(P(sizes, 0.9f), 3f) * 0.75f);
    return new EffectCamera(center, 0.6f, 0.22f, Math.Clamp(extent / MathF.Tan(MathF.PI / 8f) * 1.15f, 0.15f, 80f));
  }
}
