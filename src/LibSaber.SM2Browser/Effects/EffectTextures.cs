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
  private readonly ConcurrentDictionary<string, Task<EffectImage>> _cache = new(StringComparer.OrdinalIgnoreCase); // R95: completed entries evicted on PerfBudget.Pressure
  private void EvictCompleted() { foreach (var kv in _cache) if (kv.Value.IsCompleted) _cache.TryRemove(kv.Key, out _); }
  private bool _evictHooked;
  private void HookEvict() { if (_evictHooked) return; _evictHooked = true; var w = new WeakReference<EffectTextures>(this); Action? h = null; h = () => { if (w.TryGetTarget(out var me)) me.EvictCompleted(); else PerfBudget.Pressure -= h; }; PerfBudget.Pressure += h; }
  private readonly Func<string, TextureDocument?> _resolve;
  public Action? Loaded { get; set; }

  public EffectTextures(Func<string, TextureDocument?> resolve) => _resolve = resolve;
  private Func<string, EffectImage?>? _images;
  /// <summary>PoE2 (M2 D): images decoded by the game reader (RGBA).</summary>
  public static EffectTextures FromImages(Func<string, EffectImage?> images) => new(_ => null) { _images = images };

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
    HookEvict(); var task = _cache.GetOrAdd(name, n => Task.Run(() =>
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

  /// <summary>BGRA -> RGBA in place.</summary>
  internal static void SwapRedBlue(byte[] pixels)
  {
    for (var i = 0; i + 3 < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
  }

  private EffectImage Decode(string name)
  {
    try
    {
      if (_images is not null) return _images(name) ?? new EffectImage(0, 0, []);
      var doc = _resolve(name);
      if (doc is null) return new EffectImage(0, 0, []);
      var rgba = doc.Decode(0, 0, out var w, out var h, out _);
      if (rgba is not { Length: > 0 } || w <= 0) return new EffectImage(0, 0, []);
      // TextureDocument.Decode returns BGRA (TextureDecode / AssetExporter); EffectRenderer uploads RGBA, so swap here
      // (GlPreview does the same swap for mesh textures). mtl1-34: fire/muzzle atlases are warm only in this order.
      SwapRedBlue(rgba);
      return new EffectImage(w, h, rgba);
    }
    catch (Exception)
    {
      return new EffectImage(0, 0, []);
    }
  }
}

/// <summary>Orbit camera for the standalone effect view (same lens as the mesh preview).</summary>
internal readonly record struct EffectCamera(Vector3 Center, float Yaw, float Pitch, float Distance, float Fov = MathF.PI / 4f)
{
  public (Matrix4x4 View, Matrix4x4 Projection, Vector3 Eye) Matrices(int width, int height)
  {
    var pitch = Math.Clamp(Pitch, -1.45f, 1.45f);
    var planar = MathF.Cos(pitch);
    var eye = Center + new Vector3(planar * MathF.Sin(Yaw), MathF.Sin(pitch), planar * MathF.Cos(Yaw)) * Distance;
    var view = Matrix4x4.CreateLookAt(eye, Center, Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(Fov, width / (float)Math.Max(1, height), Math.Max(0.01f, Distance * 0.01f), Distance * 60f);
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
    var distance = Math.Clamp(extent / MathF.Tan(MathF.PI / 8f) * 1.15f, 0.15f, 80f);
    // optimization.degNear fades sprites out in the camera's face (e.g. candle/muzzle smoke radStart 6 -> radEnd 2,
    // scaleAlpha 0: alpha 0 at <= 2 m). A tight frame for a small effect sat inside that zone, so every quad was culled
    // (alphaK 0). Keep the eye at the distance where the fade is fully off and narrow the lens instead.
    var clear = NearFadeClear(instance);
    if (clear > distance)
    {
      var fov = 2f * MathF.Atan(extent * 1.15f / clear);
      return new EffectCamera(center, 0.6f, 0.22f, clear, Math.Clamp(fov, 0.05f, MathF.PI / 4f));
    }

    return new EffectCamera(center, 0.6f, 0.22f, distance);
  }

  /// <summary>
  ///   mtl1-42: browser auto-frame. Pre-simulates the effect over its first run (<paramref name="seconds"/>, 48 samples)
  ///   and frames everything it draws: sprite and mesh particles (position +- their size), trail points (+- width) and
  ///   light centres, so the whole effect is on screen. Only the 1 % tails per axis are dropped (one stray spark or a
  ///   debris chunk flying 35 m must not shrink the effect to a dot) and debris below y = -0.5 is ignored (the preview
  ///   has a ground). The bounding sphere is fitted to the narrower of the vertical/horizontal FOV with a 10 % margin.
  ///   The bench keeps <see cref="Fit"/> (its sheets and UE references are framed with it).
  /// </summary>
  public static EffectCamera FitAll(LibSaber.SM2.Assets.Effects.EffectInstance instance, float seconds, float aspect, float yaw = 0.6f, float pitch = 0.22f)
  {
    var lo = new List<Vector3>(); var hi = new List<Vector3>();
    void Add(Vector3 p, float r)
    {
      if (p.Y < -0.5f || !float.IsFinite(p.X + p.Y + p.Z)) return;
      r = float.IsFinite(r) ? Math.Clamp(r, 0f, 20f) : 0f;
      lo.Add(p - new Vector3(r)); hi.Add(p + new Vector3(r));
    }
    const int samples = 48;
    for (var i = 1; i <= samples; i++)
    {
      instance.Seek(Math.Max(seconds, 0.05f) * i / samples);
      foreach (var e in instance.Emitters)
        foreach (var p in e.Particles)
        {
          var size = e.Def.Size.Sample(Math.Clamp(p.Age / Math.Max(p.Life, 1e-4f), 0, 1), 0.5f) * e.Def.Scale;
          Add(p.Position, (e.Def.IsSprite ? 0.5f : 1f) * size);
        }
      foreach (var t in instance.Trails)
        foreach (var p in t.Points) Add(p.Position, t.Def.Width.Max * t.Def.Scale * 0.5f);
      foreach (var l in instance.Lights()) Add(l.Position, 0f);
    }
    instance.Reset();
    if (lo.Count == 0) return new EffectCamera(new Vector3(0, 0.5f, 0), yaw, pitch, 4f);
    static float Q(List<float> v, float q) { v.Sort(); return v[(int)Math.Clamp(MathF.Round(q * (v.Count - 1)), 0, v.Count - 1)]; }
    var tail = lo.Count >= 200 ? 0.01f : 0f;
    var min = new Vector3(Q(lo.Select(v => v.X).ToList(), tail), Q(lo.Select(v => v.Y).ToList(), tail), Q(lo.Select(v => v.Z).ToList(), tail));
    var max = new Vector3(Q(hi.Select(v => v.X).ToList(), 1 - tail), Q(hi.Select(v => v.Y).ToList(), 1 - tail), Q(hi.Select(v => v.Z).ToList(), 1 - tail));
    return FitBox(min, max, aspect, yaw, pitch, NearFadeClear(instance));
  }

  /// <summary>Camera that keeps the box [min, max] fully inside a perspective view of the given aspect (10 % margin).</summary>
  public static EffectCamera FitBox(Vector3 min, Vector3 max, float aspect, float yaw = 0.6f, float pitch = 0.22f, float nearClear = 0f, float fov = MathF.PI / 4f)
  {
    var center = (min + max) * 0.5f;
    var radius = Math.Max((max - min).Length() * 0.5f, 0.05f);
    var halfV = fov * 0.5f;
    var halfH = MathF.Atan(MathF.Tan(halfV) * Math.Max(aspect, 0.1f));
    var half = Math.Min(halfV, halfH);
    var distance = Math.Clamp(radius / MathF.Sin(half) * 1.1f, 0.15f, 190f);
    if (nearClear > distance)
    {
      // degNear fades sprites close to the eye: stay at the clear distance and narrow the lens (as Fit does).
      var narrow = 2f * MathF.Atan(radius * 1.1f / nearClear / Math.Min(1f, Math.Max(aspect, 0.1f)));
      return new EffectCamera(center, yaw, pitch, nearClear, Math.Clamp(narrow, 0.05f, fov));
    }
    return new EffectCamera(center, yaw, pitch, distance, fov);
  }

  /// <summary>Smallest camera distance at which no sprite emitter's <c>degNear</c> reduces size/intensity/alpha
  /// (Amount = 0 at radStart for a start &gt; end ramp). Capped at 20 m.</summary>
  public static float NearFadeClear(LibSaber.SM2.Assets.Effects.EffectInstance instance)
  {
    var clear = 0f;
    foreach (var e in instance.Emitters)
      if (e.Def.IsSprite && e.Def.DegNear is { } f && (f.Size < 1 || f.Intens < 1 || f.Alpha < 1))
        clear = Math.Max(clear, Math.Max(f.RadStart, f.RadEnd) * 1.05f);
    return Math.Min(clear, 20f);
  }
}
