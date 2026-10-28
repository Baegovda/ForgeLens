using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Demo host surface for <see cref="CoveringDemo"/>.</summary>
public enum CoveringHostKind { Off, Plane, Sphere }

/// <summary>
///   DEMO / HEURISTIC preview of <c>em_covering_*</c> (see <see cref="EffectDefinition.Coverings"/>), NOT game-accurate.
///   In the game the stub paints the covering layer of the hit surface (coveringParams @0x1436715a0 on the host
///   material, coveringLayerNumber @0x143644c48, covering_simulation / coveringMask shaders; mtl1-20). The host data
///   is not part of the effect, so this stands in a built-in plane or sphere with a runtime intensity mask:
///   the stub splats once around the effect origin (radius 0.5 m x radiusScale), and every particle that reaches
///   the surface (closer than <see cref="Contact"/>) splats <see cref="HitRadius"/> m x radiusScale. Radii, rates and
///   the per-kind tint/gloss are all HEURISTIC.
/// </summary>
public sealed class CoveringDemo
{
  public const int Res = 128;
  public const float Contact = 0.05f, HitRadius = 0.12f, OriginRadius = 0.5f, PlaneHalfSize = 1.5f, SphereRadius = 0.6f;
  public CoveringHostKind Kind { get; }
  public Vector3 Origin { get; }
  /// <summary>Plane normal / sphere pole (the effect's +Y): the surface faces the emission.</summary>
  public Vector3 Axis { get; }
  public Vector3 TangentU { get; }
  public Vector3 TangentV { get; }
  /// <summary>Sphere centre (Origin - Axis * R: the effect sits on top of the sphere); plane: Origin.</summary>
  public Vector3 Center => Kind == CoveringHostKind.Sphere ? Origin - Axis * SphereRadius : Origin;
  public float RadiusScale { get; }
  public string CoveringKind { get; }
  /// <summary>Intensity 0..1, row-major Res x Res (u along columns, v along rows).</summary>
  public float[] Mask { get; } = new float[Res * Res];
  /// <summary>Bumped on every change (GPU re-upload).</summary>
  public int Version { get; private set; }
  public int Hits { get; private set; }
  private readonly Vector3[] _texel = new Vector3[Res * Res];
  private float _last = float.NegativeInfinity;

  public CoveringDemo(CoveringHostKind kind, EffectDefinition def, Matrix4x4 root)
  {
    Kind = kind;
    Origin = root.Translation;
    var axis = Vector3.TransformNormal(Vector3.UnitY, root);
    Axis = axis.LengthSquared() < 1e-8f ? Vector3.UnitY : Vector3.Normalize(axis);
    var helper = MathF.Abs(Axis.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
    TangentU = Vector3.Normalize(Vector3.Cross(helper, Axis));
    TangentV = Vector3.Cross(Axis, TangentU);
    RadiusScale = def.Coverings.Count == 0 ? 1f : Math.Max(0.1f, def.Coverings.Max(c => c.RadiusScale));
    CoveringKind = def.Coverings.FirstOrDefault()?.Kind ?? "blood";
    for (var y = 0; y < Res; y++)
    for (var x = 0; x < Res; x++)
      _texel[y * Res + x] = SurfacePoint((x + 0.5f) / Res, (y + 0.5f) / Res);
  }

  /// <summary>World point of mask texel coordinate (u, v).</summary>
  public Vector3 SurfacePoint(float u, float v)
  {
    if (Kind == CoveringHostKind.Sphere)
    {
      float theta = v * MathF.PI, phi = (u - 0.5f) * 2f * MathF.PI;
      var n = Axis * MathF.Cos(theta) + (TangentU * MathF.Cos(phi) + TangentV * MathF.Sin(phi)) * MathF.Sin(theta);
      return Center + n * SphereRadius;
    }

    return Origin + TangentU * ((u - 0.5f) * 2f * PlaneHalfSize) + TangentV * ((v - 0.5f) * 2f * PlaneHalfSize);
  }

  /// <summary>Signed distance of <paramref name="p"/> to the host surface (negative = inside / behind) and its mask UV.</summary>
  public (float Distance, Vector2 Uv) Project(Vector3 p)
  {
    if (Kind == CoveringHostKind.Sphere)
    {
      var d = p - Center;
      var len = d.Length();
      var n = len < 1e-6f ? Axis : d / len;
      var theta = MathF.Acos(Math.Clamp(Vector3.Dot(n, Axis), -1f, 1f));
      var phi = MathF.Atan2(Vector3.Dot(n, TangentV), Vector3.Dot(n, TangentU));
      return (len - SphereRadius, new Vector2(phi / (2f * MathF.PI) + 0.5f, theta / MathF.PI));
    }

    var q = p - Origin;
    return (Vector3.Dot(q, Axis), new Vector2(Vector3.Dot(q, TangentU) / (2f * PlaneHalfSize) + 0.5f, Vector3.Dot(q, TangentV) / (2f * PlaneHalfSize) + 0.5f));
  }

  public void Clear()
  {
    Array.Clear(Mask);
    Hits = 0;
    _last = float.NegativeInfinity;
    Version++;
  }

  /// <summary>Paints for the instance's current time. Scrubbing backwards clears and restarts (forward play accumulates).</summary>
  public void Update(EffectInstance instance)
  {
    if (Kind == CoveringHostKind.Off) return;
    var t = instance.Time;
    if (t < _last) Clear();
    var first = float.IsNegativeInfinity(_last);
    var dt = first ? 1f / 60f : t - _last;
    _last = t;
    if (dt <= 0) return;
    if (first) Splat(Origin, OriginRadius * RadiusScale, 1f);
    var gain = Math.Clamp(dt * 20f, 0f, 1f);
    var budget = 2000;
    foreach (var e in instance.Emitters)
    {
      var stride = Math.Max(1, e.Particles.Count / Math.Max(1, budget));
      for (var i = 0; i < e.Particles.Count && budget > 0; i += stride, budget--)
      {
        var p = e.Particles[i].Position;
        var (d, _) = Project(p);
        if (d > Contact || d < -0.3f) continue;
        Splat(p, HitRadius * RadiusScale, gain);
        Hits++;
      }
    }
  }

  /// <summary>Adds a smooth splat (1 - (r/radius)^2)^2 x strength around the surface projection of <paramref name="p"/>.</summary>
  public void Splat(Vector3 p, float radius, float strength)
  {
    var (_, uv) = Project(p);
    var at = SurfacePoint(Math.Clamp(uv.X, 0f, 1f), Math.Clamp(uv.Y, 0f, 1f));
    var span = Kind == CoveringHostKind.Sphere ? MathF.PI * SphereRadius : 2f * PlaneHalfSize;
    var dv = radius / span;
    int y0 = Math.Max(0, (int)((uv.Y - dv) * Res) - 1), y1 = Math.Min(Res - 1, (int)((uv.Y + dv) * Res) + 1);
    int x0 = 0, x1 = Res - 1;
    if (Kind == CoveringHostKind.Plane)
    {
      x0 = Math.Max(0, (int)((uv.X - dv) * Res) - 1);
      x1 = Math.Min(Res - 1, (int)((uv.X + dv) * Res) + 1);
    }

    var r2 = radius * radius;
    for (var y = y0; y <= y1; y++)
    for (var x = x0; x <= x1; x++)
    {
      var k = y * Res + x;
      var q = Vector3.DistanceSquared(_texel[k], at) / r2;
      if (q >= 1f) continue;
      var w = (1f - q) * (1f - q) * strength;
      Mask[k] = Math.Min(1f, Mask[k] + w * (1f - Mask[k]));
    }

    Version++;
  }

  /// <summary>HEURISTIC look per covering kind: covered albedo (linear) and gloss 0..1 (wet = glossy).</summary>
  public static (Vector3 Albedo, float Gloss) Style(string kind) => kind.ToLowerInvariant() switch
  {
    var k when k.Contains("water") || k.Contains("wet") => (new Vector3(0.10f, 0.11f, 0.12f), 0.9f),
    var k when k.Contains("mud") => (new Vector3(0.09f, 0.06f, 0.03f), 0.3f),
    var k when k.Contains("slime") => (new Vector3(0.08f, 0.16f, 0.02f), 0.8f),
    _ => (new Vector3(0.20f, 0.008f, 0.006f), 0.7f), // blood and unknown kinds
  };
}
