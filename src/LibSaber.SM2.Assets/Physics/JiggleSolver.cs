using System.Numerics;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   Port of the engine's PhysJiggle step (loop body at 0x14147e5b5 inside FUN_14147e500). One object per jiggle
///   bone: a damped spring point that chases a target offset from the bone, clamped in the bone frame, which then
///   rotates the bone so its offset axis points at the point. Row-vector matrices (v * M) like the engine.
/// </summary>
public sealed class JiggleObject
{
  /// <summary>Sentinel the engine stores before the first step (0x1435571a0 = 888.888).</summary>
  public const float Unset = 888.888f;

  public JiggleObject(JiggleSettings settings, int bone, int parent, Matrix4x4 bindLocal)
  {
    Settings = settings;
    Bone = bone;
    Parent = parent;
    BindLocal = bindLocal;
    Reset();
  }

  public JiggleSettings Settings { get; }
  public int Bone { get; }
  public int Parent { get; }
  /// <summary>Object +0x08: the bone's local transform captured at setup.</summary>
  public Matrix4x4 BindLocal { get; }
  /// <summary>Object +0x48 / +0x54 / +0x60.</summary>
  public Vector3 Position, Velocity, PreviousVelocity;

  public void Reset()
  {
    Position = new Vector3(Unset);
    Velocity = Vector3.Zero;
    PreviousVelocity = Vector3.Zero;
  }

  /// <summary>Shift the spring point with a teleport so it does not swing back across the jump.</summary>
  public void Translate(Vector3 delta)
  {
    if (Position.X != Unset)
      Position += delta;
  }

  /// <summary>One engine step. Returns the bone's new world matrix (scales preserved).</summary>
  public Matrix4x4 Step(Matrix4x4 parentWorld, float dt)
  {
    var s = Settings;
    // M = bindLocal * parentWorld (FUN_1427a1a40), row lengths kept, then orthonormalised (FUN_140fd45e0).
    var m = BindLocal * parentWorld;
    var r0 = new Vector3(m.M11, m.M12, m.M13);
    var r1 = new Vector3(m.M21, m.M22, m.M23);
    var r2 = new Vector3(m.M31, m.M32, m.M33);
    var scale = new Vector3(r0.Length(), r1.Length(), r2.Length());
    Orthonormalize(ref r0, ref r1, ref r2);
    var origin = new Vector3(m.M41, m.M42, m.M43);

    // Offset direction (default (0,0,1) at 0x143609db8) normalised (falls back when shorter than eps 0x143c84348).
    var dir = s.OffsetDirection;
    dir = dir.LengthSquared() >= 1e-12f ? Vector3.Normalize(dir) : Vector3.UnitZ;
    var offset = (r0 * dir.X + r1 * dir.Y + r2 * dir.Z) * s.OffsetDistance;
    var target = origin + offset;
    if (MathF.Abs(Position.X - Unset) < 1e-3f && MathF.Abs(Position.Y - Unset) < 1e-3f && MathF.Abs(Position.Z - Unset) < 1e-3f)
      Position = target;

    // Spring: v = (v + (target - p) * k / m * dt (+ gravity / m * dt on y)) * exp(-damping * dt); p += v * dt.
    var k = s.Stiffness;
    var invMass = s.Mass != 0 ? 1f / s.Mass : 0f;
    var decay = MathF.Exp(-(dt * s.Damping));
    var v = Velocity;
    v.X = ((target.X - Position.X) * k * invMass * dt + v.X) * decay;
    v.Y = (((target.Y - Position.Y) * k + s.Gravity) * invMass * dt + v.Y) * decay;
    v.Z = ((target.Z - Position.Z) * k * invMass * dt + v.Z) * decay;
    Velocity = v;
    Position += v * dt;

    // Clamp in the target frame along each orthonormal axis (FUN_14171ad80). The x and z bounds and every max are
    // scaled by offsetDistance, the y min as well (0x14147ea5b..0x14147eb3c).
    var d = s.OffsetDistance;
    var clamped = Position;
    Clamp(target, r0, s.ConstraintMin.X * d, s.ConstraintMax.X * d, ref clamped);
    Clamp(target, r1, s.ConstraintMin.Y * d, s.ConstraintMax.Y * d, ref clamped);
    Clamp(target, r2, s.ConstraintMin.Z * d, s.ConstraintMax.Z * d, ref clamped);
    if (s.WriteBack)
      Position = clamped;

    // Jumps: y velocity times intensity, clamped to [jumpsConstraintMin, jumpsConstraintMax].
    var jump = Velocity.Y * s.JumpsIntensity;
    if (s.JumpsConstraintMax <= jump) jump = s.JumpsConstraintMax;
    if (jump <= s.JumpsConstraintMin) jump = s.JumpsConstraintMin;
    PreviousVelocity = Velocity;

    // Rotate the basis so normalize(offset) turns onto normalize(clamped - origin), then translate by offset * jump.
    var rr0 = r0;
    var rr1 = r1;
    var rr2 = r2;
    var toPoint = clamped - origin;
    if (toPoint.LengthSquared() >= 1e-12f && offset.LengthSquared() >= 1e-12f)
    {
      var from = Vector3.Normalize(offset);
      var to = Vector3.Normalize(toPoint);
      var axis = Vector3.Cross(from, to);
      if (axis.LengthSquared() >= 1e-12f)
      {
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(from, to), -1f, 1f));
        var q = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle);
        rr0 = Vector3.Transform(r0, q);
        rr1 = Vector3.Transform(r1, q);
        rr2 = Vector3.Transform(r2, q);
      }
    }

    var t = origin + offset * jump;
    rr0 *= scale.X;
    rr1 *= scale.Y;
    rr2 *= scale.Z;
    return new Matrix4x4(
      rr0.X, rr0.Y, rr0.Z, 0,
      rr1.X, rr1.Y, rr1.Z, 0,
      rr2.X, rr2.Y, rr2.Z, 0,
      t.X, t.Y, t.Z, 1);
  }

  /// <summary>FUN_14171ad80: s = dot(p - origin, axis); below min push up to min, else above max pull down to max.</summary>
  public static void Clamp(Vector3 origin, Vector3 axis, float min, float max, ref Vector3 p)
  {
    var s = Vector3.Dot(p - origin, axis);
    if (min <= s)
    {
      if (max < s)
        p -= axis * (s - max);
    }
    else
      p += axis * (min - s);
  }

  private static void Orthonormalize(ref Vector3 x, ref Vector3 y, ref Vector3 z)
  {
    x = x.LengthSquared() > 1e-20f ? Vector3.Normalize(x) : Vector3.UnitX;
    z = Vector3.Cross(x, y);
    z = z.LengthSquared() > 1e-20f ? Vector3.Normalize(z) : Vector3.UnitZ;
    y = Vector3.Cross(z, x);
  }
}
