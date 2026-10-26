using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Small deterministic PRNG (SplitMix64) so a seed replays bit-identically on every platform.</summary>
public struct EffectRandom(ulong seed)
{
  private ulong _state = seed;

  public ulong Next()
  {
    var z = _state += 0x9E3779B97F4A7C15UL;
    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
    return z ^ (z >> 31);
  }

  /// <summary>Uniform in [0,1).</summary>
  public float Float() => (Next() >> 40) * (1f / (1 << 24));
  public float Range(Vector2 r) => r.X + (r.Y - r.X) * Float();
  public float Signed() => Float() * 2f - 1f;

  public Vector3 InSphere()
  {
    for (var i = 0; i < 16; i++)
    {
      var v = new Vector3(Signed(), Signed(), Signed());
      if (v.LengthSquared() <= 1f) return v;
    }

    return Vector3.Zero;
  }
}

public struct Particle
{
  public Vector3 Position;
  public Vector3 Velocity;
  public float Age;
  public float Life;
  public float RandSize, RandColor, RandIntensity, RandClip, RandFrame;
  public float MaskAngle;
  /// <summary>Fractional carry for <c>genOnFly</c> children.</summary>
  public float FlyCarry;
  public float Rotation, RotationSpeed;
  public float FrameStart, FrameRate;
  public bool Flip;
  public int Order;
  /// <summary>Mesh particles: template sub-object and initial orientation.</summary>
  public int Variant;
  public Quaternion Orientation;
}

public sealed class EmitterState(EmitterDef def, int index)
{
  public EmitterDef Def { get; } = def;
  public int Index { get; } = index;
  public List<Particle> Particles { get; } = [];
  public float Time;
  public float Accumulator;
  public bool Burst;
  public float RateRandom;
  public int Spawned;
  public EffectRandom Random;
  public Matrix4x4 World = Matrix4x4.Identity;
}

public readonly record struct LightSample(LightDef Def, Vector3 Position, Vector3 Color, float Radius, float Intensity);

/// <summary>
///   Fixed-step (60 Hz), seeded particle simulation of an <see cref="EffectDefinition"/>. <see cref="Seek"/> to any
///   time replays from zero when going backwards, so scrubbing is deterministic: the same seed and time always give
///   the same particles. Emission follows the retail fields: <c>constantFlow {x,y}</c> particles/s (one rate picked
///   per emitter), <c>explosion.numPartsToEmit</c> once after <c>emitDelay</c> (explosion struct @0x143644be0,
///   loaded at 0x141777823), <c>dynamicFlow.intensitySpline</c> particles/s over emitter seconds; all inside the
///   part's <c>timeLife</c> window (0 = endless) and scaled in time by <c>timeScale</c>.
/// </summary>
public sealed class EffectInstance
{
  public const float Step = 1f / 60f;
  private readonly List<EmitterState> _emitters = [];
  private int _order;

  public EffectInstance(EffectDefinition definition, ulong seed = 1)
  {
    Definition = definition;
    Seed = seed;
    Reset();
  }

  public EffectDefinition Definition { get; }
  public ulong Seed { get; set; }
  public float Time { get; private set; }
  public int StepCount { get; private set; }
  /// <summary>Where the effect is placed in the world.</summary>
  public Matrix4x4 Root { get; set; } = Matrix4x4.Identity;
  /// <summary>World matrix of a socket/bone by <c>nameParent</c> at a time; null (or unknown names) uses <see cref="Root"/>.</summary>
  public Func<string, float, Matrix4x4?>? Socket { get; set; }
  /// <summary>Height of the collision ground plane for <c>physics.cdt</c> (null = no ground).</summary>
  public float? Ground { get; set; } = 0f;
  public IReadOnlyList<EmitterState> Emitters => _emitters;
  /// <summary>Optional moving placement (e.g. a projectile path for trails); overrides <see cref="Root"/> per step.</summary>
  public Func<float, Matrix4x4>? RootMotion { get; set; }
  public IReadOnlyList<TrailState> Trails => _trails;
  private readonly List<TrailState> _trails = [];
  private Matrix4x4 CurrentRoot => RootMotion?.Invoke(Time) ?? Root;
  public int ParticleCount => _emitters.Sum(e => e.Particles.Count);

  /// <summary>
  ///   Placement for previewing without a host model. Effects bound to sockets (<c>nameParent</c>, e.g. a weapon's
  ///   <c>tip</c>; <c>sfx_source_pt</c> is the effect origin itself) are authored along the socket's +Y; standalone they are lifted 1.2 m and turned so +Y points along
  ///   world +X (a horizontal shot). Free effects (explosions, impacts) sit on the ground at the origin.
  /// </summary>
  public static Matrix4x4 StandaloneRoot(EffectDefinition def)
  {
    static bool Socket(string n) => n.Length > 0 && !n.Equals("sfx_source_pt", StringComparison.OrdinalIgnoreCase);
    var socketed = def.Emitters.Any(e => Socket(e.NameParent) && !e.IsAttachedChild) || def.Lights.Any(l => Socket(l.NameParent)) || def.Trails.Any(t => Socket(t.NameParent));
    return socketed ? Matrix4x4.CreateRotationZ(-MathF.PI / 2f) * Matrix4x4.CreateTranslation(0, 1.2f, 0) : Matrix4x4.Identity;
  }

  /// <summary>
  ///   Preview motion for effects with trails and no host: the root flies along its own +Y (world +X after
  ///   <see cref="StandaloneRoot"/>) at <paramref name="speed"/> m/s over [-span, span], restarting at the far end.
  /// </summary>
  public static Func<float, Matrix4x4>? StandaloneMotion(EffectDefinition def, Matrix4x4 root, float speed = 40f, float span = 5f)
  {
    if (def.Trails.Count == 0) return null;
    var axis = Vector3.TransformNormal(Vector3.UnitY, root);
    if (axis.LengthSquared() < 1e-8f) axis = Vector3.UnitX;
    axis = Vector3.Normalize(axis);
    var period = 2f * span / speed + 0.15f;
    return t =>
    {
      var x = Math.Min(-span + (t % period) * speed, span);
      return root * Matrix4x4.CreateTranslation(axis * x);
    };
  }

  /// <summary>An instance placed for previewing without a host (<see cref="StandaloneRoot"/> plus <see cref="StandaloneMotion"/>).</summary>
  public static EffectInstance Standalone(EffectDefinition def, ulong seed = 1, float trailSpeed = 40f)
  {
    var root = StandaloneRoot(def);
    return new EffectInstance(def, seed) { Root = root, RootMotion = StandaloneMotion(def, root, trailSpeed) };
  }

  public void Reset()
  {
    _trails.Clear();
    foreach (var t in Definition.Trails) _trails.Add(new TrailState(t));
    _emitters.Clear();
    Time = 0;
    StepCount = 0;
    _order = 0;
    for (var i = 0; i < Definition.Emitters.Count; i++)
    {
      var state = new EmitterState(Definition.Emitters[i], i) { Random = new EffectRandom(Seed * 0x100000001B3UL + (ulong)i * 0x9E3779B97F4A7C15UL + 7) };
      state.RateRandom = state.Random.Float();
      _emitters.Add(state);
    }

    foreach (var e in _emitters)
      if (e.Def.PrewarmTime > 0)
        for (var t = 0f; t < e.Def.PrewarmTime; t += Step)
          StepEmitter(e, Step * e.Def.TimeScale, prewarm: true);
  }

  /// <summary>Advances whole fixed steps until <see cref="Time"/> reaches <paramref name="time"/>; rewinds by replaying.</summary>
  public void Seek(float time)
  {
    var target = (int)MathF.Floor(Math.Max(0, time) / Step + 1e-4f);
    if (target < StepCount)
      Reset();
    while (StepCount < target)
      StepOnce();
  }

  public void StepOnce()
  {
    foreach (var e in _emitters)
      StepEmitter(e, Step * e.Def.TimeScale, prewarm: false);
    foreach (var t in _trails)
      StepTrail(t, Step);
    StepCount++;
    Time = StepCount * Step;
  }

  private Matrix4x4 EmitterWorld(EmitterDef def)
  {
    var parent = def.NameParent.Length > 0 && Socket?.Invoke(def.NameParent, Time) is { } socket ? socket : CurrentRoot;
    return def.Transform * parent;
  }

  /// <summary>One point per step at the parent (long jumps split by <c>maxSegLength</c>); points die after
  /// <c>timeLifeSeg</c>. A jump over 10 m (preview restart, teleport) starts a new trail.</summary>
  private void StepTrail(TrailState t, float dt)
  {
    var def = t.Def;
    var parent = def.NameParent.Length > 0 && Socket?.Invoke(def.NameParent, Time) is { } socket ? socket : CurrentRoot;
    var pos = (def.Transform * parent).Translation;
    t.Time += dt;
    for (var i = 0; i < t.Points.Count; i++) { var p = t.Points[i]; p.Age += dt; t.Points[i] = p; }
    t.Points.RemoveAll(p => p.Age > def.TimeLifeSeg);
    if (t.Started && Vector3.Distance(t.Head, pos) > 10f) t.Points.Clear();
    var last = t.Points.Count > 0 ? t.Points[^1].Position : t.Started ? t.Head : pos;
    var d = Vector3.Distance(last, pos);
    var seg = def.MaxSegLength * def.Scale;
    var splits = d > seg ? Math.Min(64, (int)MathF.Ceiling(d / seg)) : 1;
    for (var k = 1; k <= splits; k++)
      t.Points.Add(new TrailPoint { Position = Vector3.Lerp(last, pos, k / (float)splits), Age = dt * (1f - k / (float)splits) });
    t.Head = pos;
    t.Started = true;
  }

  private void StepEmitter(EmitterState e, float dt, bool prewarm)
  {
    var def = e.Def;
    e.World = EmitterWorld(def);
    // Update living particles first, then spawn (new particles are pre-aged by their sub-step offset).
    var particles = e.Particles;
    var write = 0;
    var hasChildren = def.Attached.Count > 0 && !prewarm;
    for (var i = 0; i < particles.Count; i++)
    {
      var p = particles[i];
      var alive = Integrate(ref p, def, dt, out var hit);
      if (hasChildren)
        FireChildren(def, ref p, dt, alive, hit);
      if (alive)
        particles[write++] = p;
    }

    particles.RemoveRange(write, particles.Count - write);
    if (!def.Visible || dt <= 0 || def.IsAttachedChild)
    {
      e.Time += dt;
      return;
    }

    var t0 = e.Time;
    var t1 = t0 + dt;
    var active = def.EmitDuration <= 0 || t0 < def.EmitDuration;
    var count = 0;
    switch (def.Emission)
    {
      case EmissionKind.Explosion:
        if (!e.Burst && t1 >= def.BurstDelay)
        {
          e.Burst = true;
          count = (int)MathF.Round(def.BurstCount.X + (def.BurstCount.Y - def.BurstCount.X) * e.Random.Float());
        }

        break;
      case EmissionKind.ConstantFlow:
        if (active)
          e.Accumulator += (def.Rate.X + (def.Rate.Y - def.Rate.X) * e.RateRandom) * dt;
        break;
      case EmissionKind.DynamicFlow:
        if (active)
        {
          var tt = def.DynamicCycled && def.DynamicRate.EndTime > 0 ? t0 % def.DynamicRate.EndTime : t0;
          e.Accumulator += Math.Max(0, def.DynamicRate.Evaluate(tt)) * dt;
        }

        break;
    }

    if (def.Emission != EmissionKind.Explosion)
    {
      count = (int)e.Accumulator;
      e.Accumulator -= count;
    }

    if (def.MaxParticles > 0)
      count = Math.Min(count, Math.Max(0, def.MaxParticles - particles.Count));
    count = Math.Min(count, 20000 - particles.Count);
    for (var k = 0; k < count; k++)
    {
      var p = Spawn(e, t0);
      // Spread a step's spawns over the step so streams do not clump at 60 Hz.
      var preAge = def.Emission == EmissionKind.Explosion ? 0f : dt * (1f - (k + 0.5f) / count);
      if (preAge <= 0 || Integrate(ref p, def, preAge, out _))
        particles.Add(p);
    }

    e.Time = t1;
  }

  /// <summary>Fires <c>attached</c> children for one parent particle: genOnFly at the child's own flow rate (or its
  /// burst once at the parent's birth) per living parent, genOnCDT/genOnDie <c>numParts</c> at the event.</summary>
  private void FireChildren(EmitterDef def, ref Particle parent, float dt, bool alive, bool hit)
  {
    foreach (var a in def.Attached)
    {
      if (a.Index < 0) continue;
      var child = _emitters[a.Index];
      var count = 0;
      switch (a.Event)
      {
        case AttachEvent.Die when !alive:
        case AttachEvent.Cdt when hit:
          count = (int)MathF.Floor(a.NumParts + child.Random.Float() * (a.NumParts % 1f > 0 ? 1f : 0f));
          break;
        case AttachEvent.Fly when alive:
          var cd = child.Def;
          if (cd.Emission == EmissionKind.Explosion)
          {
            if (parent.Age <= dt + 1e-6f) count = (int)MathF.Round(cd.BurstCount.X + (cd.BurstCount.Y - cd.BurstCount.X) * child.Random.Float());
            break;
          }

          var rate = cd.Emission == EmissionKind.DynamicFlow
            ? Math.Max(0f, cd.DynamicRate.Evaluate(parent.Age))
            : cd.Rate.X + (cd.Rate.Y - cd.Rate.X) * child.RateRandom;
          parent.FlyCarry += rate * dt * Math.Max(0.01f, cd.TimeScale);
          count = (int)parent.FlyCarry;
          parent.FlyCarry -= count;
          break;
      }

      count = Math.Min(count, 20000 - child.Particles.Count);
      for (var k = 0; k < count; k++)
      {
        var p = Spawn(child, child.Time, parent.Position);
        p.Velocity += parent.Velocity * child.Random.Range(child.Def.SpeedInherit);
        child.Particles.Add(p);
      }
    }
  }

  private Particle Spawn(EmitterState e, float emitterTime, Vector3? at = null)
  {
    var def = e.Def;
    ref var r = ref e.Random;
    var world = at is { } origin ? EmitterWorld(def) with { M41 = origin.X, M42 = origin.Y, M43 = origin.Z } : e.World;
    // Direction inside a cone of half-angle angleEmission around emitDir (uniform over the spherical cap).
    var half = Math.Clamp(def.AngleEmission, 0f, 180f) * MathF.PI / 180f;
    var cosT = 1f - r.Float() * (1f - MathF.Cos(half));
    var sinT = MathF.Sqrt(Math.Max(0f, 1f - cosT * cosT));
    var phi = r.Float() * MathF.Tau;
    var axis = at is null && def.EmitByNormal && def.NameParent.Length > 0 && Socket?.Invoke(def.NameParent, Time) is not null ? Vector3.UnitZ : def.EmitDir;
    var tangent = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
    var bitangent = Vector3.Cross(axis, tangent);
    var dir = axis * cosT + (tangent * MathF.Cos(phi) + bitangent * MathF.Sin(phi)) * sinT;
    var tangentDir = Vector3.Cross(dir, tangent * MathF.Cos(phi + 1.3f) + bitangent * MathF.Sin(phi + 1.3f));
    tangentDir = tangentDir.LengthSquared() > 1e-8f ? Vector3.Normalize(tangentDir) : tangent;

    var s = def.Scale;
    var speed = def.SpeedNorm.Sample(emitterTime, r.Float()) * s;
    var speedT = def.SpeedTang.Sample(emitterTime, r.Float()) * s;
    var offset = dir * (def.InitialOffset + def.InitialOffsetDisp * r.Signed()) * s;
    if (def.RandomOffsetRadius > 0)
      offset += r.InSphere() * def.RandomOffsetWeights * def.RandomOffsetRadius * s;

    var position = Vector3.Transform(offset, world);
    var velocity = Vector3.TransformNormal(dir * speed + tangentDir * speedT, world);
    var p = new Particle
    {
      Position = position,
      Velocity = velocity,
      Life = Math.Max(1e-4f, r.Range(def.Life)),
      RandSize = r.Float(),
      RandColor = r.Float(),
      RandIntensity = r.Float(),
      RandClip = r.Float(),
      RandFrame = r.Float(),
      Rotation = r.Range(def.RotationAngle) * MathF.PI / 180f,
      RotationSpeed = r.Range(def.RotationSpeed) * MathF.PI / 180f,
      FrameStart = r.Range(def.FrameOffset),
      FrameRate = r.Range(def.FrameRate),
      Flip = def.RandomFlip && r.Float() < 0.5f,
      MaskAngle = def.AlphaMask is null ? 0f : r.Range(def.AlphaMaskRotation) * MathF.PI / 180f,
      Order = _order++,
      Orientation = Quaternion.Identity,
    };
    if (def.Geom is { } geom)
    {
      // Drawn only for mesh emitters, so sprite emitters keep their random sequence.
      p.Variant = geom.InstanceMin + (int)MathF.Floor(r.Float() * (geom.InstanceMax - geom.InstanceMin + 1));
      var a = geom.RandomAxes;
      var q = Quaternion.Identity;
      if (a.X > 0) q *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, r.Float() * MathF.Tau);
      if (a.Y > 0) q *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, r.Float() * MathF.Tau);
      if (a.Z > 0) q *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ, r.Float() * MathF.Tau);
      p.Orientation = q;
    }

    e.Spawned++;
    return p;
  }

  private bool Integrate(ref Particle p, EmitterDef def, float dt, out bool hit)
  {
    hit = false;
    p.Age += dt;
    if (p.Age >= p.Life) return false;
    p.Velocity += def.Gravity * dt;
    if (def.Friction > 0)
      p.Velocity /= 1f + def.Friction * dt;
    p.Position += p.Velocity * dt;
    p.Rotation += p.RotationSpeed * dt;
    if (def.Collide && Ground is { } ground && p.Position.Y < ground && p.Velocity.Y < 0)
    {
      hit = true;
      if (def.KillOnCollide) return false;
      p.Position.Y = ground;
      p.Velocity = new Vector3(p.Velocity.X * 0.7f, -p.Velocity.Y * def.Resilience, p.Velocity.Z * 0.7f);
    }

    return true;
  }

  public IEnumerable<LightSample> Lights()
  {
    foreach (var light in Definition.Lights)
    {
      if (Time > light.Duration) continue;
      var n = Math.Clamp(Time / light.Duration, 0f, 1f);
      var parent = light.NameParent.Length > 0 && Socket?.Invoke(light.NameParent, Time) is { } socket ? socket : CurrentRoot;
      var world = light.Transform * parent;
      var c = light.Color.Evaluate(n);
      yield return new LightSample(light, world.Translation, new Vector3(c.X, c.Y, c.Z), light.Radius.Evaluate(n), light.Intensity.Evaluate(n));
    }
  }
}
