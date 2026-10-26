using System.Numerics;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   Position-based cloth solver standing in for the engine's NvCloth (SwSolver) instance of one <c>clothes[]</c>
///   entry. The engine's solver is not reproduced bit for bit; this keeps its structure and parameters:
///   iterations = dt * solverFrequency, stiffness given per 1/stiffnessFrequency seconds, phase constraints
///   (vertical, horizontal, bend, shear) with compression and stretch limits, tethers to the pinned particles,
///   motion spheres of radius COLOR0 * motion.distanceScale around the skinned particle, capsule / sphere / box
///   collision, damping, gravity and NvCloth-style wind drag and lift.
/// </summary>
public sealed class ClothInstance
{
  private readonly ClothEntry _entry;
  private readonly ClothSimShell _shell;
  private readonly Vector3[] _bind;
  private readonly float[] _maxDistance;
  private readonly float[] _invMass;
  private readonly int[] _triangles;
  private readonly Constraint[][] _phases;
  private readonly (int Particle, int Anchor, float Length)[] _tethers;
  private readonly (int Bone, float Radius, Vector3 Offset, int Bone2, float Radius2, Vector3 Offset2)[] _capsules;
  private readonly (int Bone, float Radius, Vector3 Offset)[] _spheres;
  private readonly (int Bone, Vector3 Half, Vector3 Offset)[] _boxes;
  private Vector3[] _current;
  private Vector3[] _previous;
  private Vector3[] _skinned;
  private bool _started;
  private float _windClock;

  private readonly struct Constraint
  {
    public readonly int A, B;
    public readonly float Rest;

    public Constraint(int a, int b, float rest)
    {
      A = a;
      B = b;
      Rest = rest;
    }
  }

  public ClothInstance(ClothEntry entry, ClothSimShell shell, IReadOnlyList<Bone> bones)
  {
    _entry = entry;
    _shell = shell;
    var n = shell.Positions.Count;
    _bind = shell.Positions.ToArray();
    _triangles = shell.Indices.ToArray();
    _maxDistance = new float[n];
    _invMass = new float[n];
    for (var i = 0; i < n; i++)
    {
      // COLOR0 (grey, 0 at the attachment, rising to the free end) is the motion-constraint max distance.
      var c = i < shell.Colors.Count ? shell.Colors[i].X : 1f;
      _maxDistance[i] = c * entry.Config.Motion.DistanceScale;
      _invMass[i] = c <= 1e-4f ? 0f : 1f;
    }

    _phases = BuildPhases(_bind, _triangles);
    _tethers = BuildTethers(_bind, _triangles, _invMass, entry.Config.Tether.UseTopologicalDistance || entry.UseTopologicalDistance);
    int Find(string name) => bones is null ? -1 : FindBone(bones, name);
    _capsules = entry.Config.Capsules
      .Select(c => (Find(c.BoneFirst), c.RadiusFirst, c.BoneFirstOffset, Find(c.BoneSecond.Length > 0 ? c.BoneSecond : c.BoneFirst), c.RadiusSecond, c.BoneSecondOffset))
      .Where(c => c.Item1 >= 0 && c.Item4 >= 0)
      .ToArray();
    _spheres = entry.Config.Spheres.Select(s => (Find(s.Bone), s.Radius, s.BoneOffset)).Where(s => s.Item1 >= 0).ToArray();
    _boxes = entry.Config.Boxes.Select(b => (Find(b.Bone), b.Size * 0.5f, b.BoneOffset)).Where(b => b.Item1 >= 0).ToArray();
    _current = (Vector3[])_bind.Clone();
    _previous = (Vector3[])_bind.Clone();
    _skinned = (Vector3[])_bind.Clone();
  }

  public string Name => _shell.Name;
  public ClothEntry Entry => _entry;
  public ClothSimShell Shell => _shell;
  public Vector3[] BindPositions => _bind;
  public int[] Triangles => _triangles;
  /// <summary>Simulated particle positions (world).</summary>
  public Vector3[] Positions => _current;
  public int ParticleCount => _bind.Length;
  public int PinnedCount => _invMass.Count(w => w == 0);

  public void Reset() => _started = false;

  public void Translate(Vector3 delta)
  {
    for (var i = 0; i < _current.Length; i++)
    {
      _current[i] += delta;
      _previous[i] += delta;
    }
  }

  /// <summary>Advance by <paramref name="dt"/> seconds against the posed skeleton.</summary>
  public void Step(Matrix4x4[] skin, Matrix4x4[] worlds, float dt)
  {
    var cfg = _entry.Config;
    Skin(skin);
    if (!_started)
    {
      Array.Copy(_skinned, _current, _current.Length);
      Array.Copy(_skinned, _previous, _previous.Length);
      _started = true;
      if (dt <= 0)
        return;
    }

    if (dt <= 0)
      return;
    var iterations = Math.Clamp((int)MathF.Round(dt * Math.Max(1f, cfg.SolverFrequency)), 1, 64);
    var h = dt / iterations;
    var exponent = h * Math.Max(1f, cfg.StiffnessFrequency);
    float PerIteration(float stiffness) => 1f - MathF.Pow(1f - Math.Clamp(stiffness, 0f, 1f), exponent);
    var damping = MathF.Pow(1f - Math.Clamp(cfg.Damping, 0f, 0.999f), exponent);
    var motionStiffness = PerIteration(cfg.Motion.Stiffness);
    var tetherStiffness = PerIteration(cfg.Tether.Stiffness);
    var phaseStiffness = new[] { cfg.Vertical, cfg.Horizontal, cfg.Bend, cfg.Shear }
      .Select(p => (Inside: PerIteration(p.Stiffness * p.StiffnessMultiplier * cfg.StiffnessMultiplier), Outside: PerIteration(p.Stiffness * cfg.StiffnessMultiplier), p.CompressionLimit, p.StretchLimit))
      .ToArray();
    var gravity = cfg.Gravity * (h * h);
    var colliders = Colliders(worlds);
    var startSkinned = (Vector3[])_prevSkinnedOrSkinned.Clone();
    for (var it = 0; it < iterations; it++)
    {
      var f = (it + 1f) / iterations;
      _windClock += h;
      // Pinned particles follow the animation, blended across the frame like NvCloth's interpolated targets.
      for (var i = 0; i < _current.Length; i++)
      {
        var target = Vector3.Lerp(startSkinned[i], _skinned[i], f);
        if (_invMass[i] == 0)
        {
          _previous[i] = _current[i];
          _current[i] = target;
          continue;
        }

        var velocity = (_current[i] - _previous[i]) * damping;
        _previous[i] = _current[i];
        _current[i] += velocity + gravity;
      }

      ApplyWind(cfg, h);
      for (var phase = 0; phase < _phases.Length; phase++)
      {
        var (inside, outside, compression, stretch) = phaseStiffness[phase];
        if (inside <= 0 && outside <= 0)
          continue;
        foreach (var c in _phases[phase])
          SolveDistance(c, inside, outside, compression, stretch);
      }

      if (tetherStiffness > 0)
      {
        var scale = cfg.Tether.Scale;
        foreach (var (p, a, length) in _tethers)
        {
          var d = _current[p] - _current[a];
          var len = d.Length();
          var limit = length * scale;
          if (len > limit && len > 1e-9f)
            _current[p] -= d * ((len - limit) / len * tetherStiffness);
        }
      }

      if (cfg.Motion.Enable && motionStiffness > 0)
      {
        for (var i = 0; i < _current.Length; i++)
        {
          if (_invMass[i] == 0)
            continue;
          var target = Vector3.Lerp(startSkinned[i], _skinned[i], f);
          var d = _current[i] - target;
          var len = d.Length();
          var radius = _maxDistance[i];
          if (len > radius && len > 1e-9f)
            _current[i] -= d * ((len - radius) / len * motionStiffness);
        }
      }

      Collide(colliders, cfg.CollisionThickness);
    }

    _prevSkinnedOrSkinned = (Vector3[])_skinned.Clone();
    var worst = 0f;
    for (var i = 0; i < _current.Length; i++)
      worst = MathF.Max(worst, (_current[i] - _skinned[i]).Length());
    MaxDeviation = worst;
  }

  /// <summary>Largest distance of a particle from its skinned (animation-only) position after the last step.</summary>
  public float MaxDeviation { get; private set; }

  /// <summary>Skinned (animation-only) particle positions of the last step.</summary>
  public Vector3[] SkinnedPositions => _skinned;

  private Vector3[] _prevSkinnedOrSkinned = Array.Empty<Vector3>();

  private void Skin(Matrix4x4[] skin)
  {
    var weights = _shell.Skin;
    for (var i = 0; i < _bind.Length; i++)
    {
      if (i >= weights.Count)
      {
        _skinned[i] = _bind[i];
        continue;
      }

      var w = weights[i];
      var p = _bind[i];
      var sum = Vector3.Zero;
      var total = 0f;
      void Add(ushort joint, float weight)
      {
        if (weight <= 0 || joint >= skin.Length)
          return;
        sum += Vector3.Transform(p, skin[joint]) * weight;
        total += weight;
      }

      Add(w.J0, w.W0);
      Add(w.J1, w.W1);
      Add(w.J2, w.W2);
      Add(w.J3, w.W3);
      _skinned[i] = total > 1e-6f ? sum / total : p;
    }

    if (_prevSkinnedOrSkinned.Length != _skinned.Length || !_started)
      _prevSkinnedOrSkinned = (Vector3[])_skinned.Clone();
  }

  private void SolveDistance(Constraint c, float inside, float outside, float compression, float stretch)
  {
    var wa = _invMass[c.A];
    var wb = _invMass[c.B];
    var wsum = wa + wb;
    if (wsum <= 0)
      return;
    var d = _current[c.B] - _current[c.A];
    var len = d.Length();
    if (len < 1e-9f)
      return;
    var ratio = len / c.Rest;
    // PhysX/NvCloth fibre: inside [compressionLimit, stretchLimit] the stiffness is scaled by the multiplier.
    var k = ratio >= compression && ratio <= stretch ? inside : outside;
    if (k <= 0)
      return;
    var correction = d * ((len - c.Rest) / len * k / wsum);
    _current[c.A] += correction * wa;
    _current[c.B] -= correction * wb;
  }

  private void ApplyWind(ClothConfig cfg, float h)
  {
    var env = cfg.Environment;
    if (env.AirDrag <= 0 && env.AirLift <= 0)
      return;
    // Local wind plus a slow gust (stand-in for the engine wind system when useWindSystem is set).
    var wind = env.LocalWindVelocity + env.GlobalWindVelocity;
    if (env.UseWindSystem)
    {
      var gust = 0.65f + 0.35f * MathF.Sin(_windClock * 1.7f) * MathF.Sin(_windClock * 0.63f + 1.3f);
      wind *= gust * env.WindSystemInfluenceScale;
    }

    // Aerodynamic impulse per triangle as NvCloth's applyWind: drag along the relative flow, lift across it,
    // q = 0.5 * fluidDensity * |v|^2 times the triangle area, applied to each of its three particles through the
    // particle inverse mass (NvCloth particles carry w = 1/mass; the engine fabric uses unit mass, so a
    // triangle's push is proportional to its area and small panels only flutter).
    const float particleMass = 1f;
    var density = env.FluidDensity;
    for (var t = 0; t + 2 < _triangles.Length; t += 3)
    {
      int a = _triangles[t], b = _triangles[t + 1], c = _triangles[t + 2];
      var wsum = _invMass[a] + _invMass[b] + _invMass[c];
      if (wsum <= 0)
        continue;
      var velocity = ((_current[a] - _previous[a]) + (_current[b] - _previous[b]) + (_current[c] - _previous[c])) / (3f * h);
      var rel = wind - velocity;
      var speed = rel.Length();
      if (speed < 1e-4f)
        continue;
      var cross = Vector3.Cross(_current[b] - _current[a], _current[c] - _current[a]);
      var doubleArea = cross.Length();
      if (doubleArea < 1e-12f)
        continue;
      var normal = cross / doubleArea;
      var flow = rel / speed;
      var cos = Vector3.Dot(normal, flow);
      var sin = MathF.Sqrt(MathF.Max(0, 1 - cos * cos));
      var liftDir = Vector3.Cross(Vector3.Cross(normal, flow), flow);
      liftDir = liftDir.LengthSquared() > 1e-12f ? Vector3.Normalize(liftDir) : Vector3.Zero;
      var q = 0.5f * density * speed * speed;
      var force = (flow * (env.AirDrag * MathF.Abs(cos)) + liftDir * (env.AirLift * cos * sin)) * q;
      // Force per unit area over mass per unit area: acceleration, same for the whole triangle.
      var dx = force * (doubleArea * 0.5f) / particleMass * (h * h);
      if (_invMass[a] > 0) _current[a] += dx;
      if (_invMass[b] > 0) _current[b] += dx;
      if (_invMass[c] > 0) _current[c] += dx;
    }
  }

  private readonly record struct Collider(int Kind, Vector3 A, Vector3 B, float Ra, float Rb, Matrix4x4 Frame, Vector3 Half);

  private List<Collider> Colliders(Matrix4x4[] worlds)
  {
    var list = new List<Collider>();
    foreach (var c in _capsules)
    {
      if (c.Bone >= worlds.Length || c.Bone2 >= worlds.Length)
        continue;
      var a = Vector3.Transform(c.Offset, worlds[c.Bone]);
      var b = Vector3.Transform(c.Offset2, worlds[c.Bone2]);
      list.Add(new Collider(0, a, b, c.Radius, c.Radius2, default, default));
    }

    foreach (var s in _spheres)
    {
      if (s.Bone >= worlds.Length)
        continue;
      var a = Vector3.Transform(s.Offset, worlds[s.Bone]);
      list.Add(new Collider(0, a, a, s.Radius, s.Radius, default, default));
    }

    foreach (var b in _boxes)
    {
      if (b.Bone >= worlds.Length)
        continue;
      var frame = Normalized(worlds[b.Bone]);
      var centre = Vector3.Transform(b.Offset, frame);
      frame.Translation = centre;
      list.Add(new Collider(1, centre, centre, 0, 0, frame, b.Half));
    }

    return list;
  }

  private void Collide(List<Collider> colliders, float thickness)
  {
    if (colliders.Count == 0)
      return;
    for (var i = 0; i < _current.Length; i++)
    {
      if (_invMass[i] == 0)
        continue;
      var p = _current[i];
      foreach (var c in colliders)
      {
        if (c.Kind == 0)
        {
          // Tapered capsule: closest point on the axis, radius interpolated along it.
          var ab = c.B - c.A;
          var len2 = ab.LengthSquared();
          var t = len2 > 1e-12f ? Math.Clamp(Vector3.Dot(p - c.A, ab) / len2, 0f, 1f) : 0f;
          var axis = c.A + ab * t;
          var r = c.Ra + (c.Rb - c.Ra) * t + thickness;
          var d = p - axis;
          var dist = d.Length();
          if (dist < r && dist > 1e-9f)
            p = axis + d * (r / dist);
        }
        else
        {
          Matrix4x4.Invert(c.Frame, out var inv);
          var local = Vector3.Transform(p, inv);
          var half = c.Half + new Vector3(thickness);
          var inside = MathF.Abs(local.X) < half.X && MathF.Abs(local.Y) < half.Y && MathF.Abs(local.Z) < half.Z;
          if (!inside)
            continue;
          var px = half.X - MathF.Abs(local.X);
          var py = half.Y - MathF.Abs(local.Y);
          var pz = half.Z - MathF.Abs(local.Z);
          if (px <= py && px <= pz) local.X = MathF.CopySign(half.X, local.X);
          else if (py <= pz) local.Y = MathF.CopySign(half.Y, local.Y);
          else local.Z = MathF.CopySign(half.Z, local.Z);
          p = Vector3.Transform(local, c.Frame);
        }
      }

      _current[i] = p;
    }
  }

  private static Matrix4x4 Normalized(Matrix4x4 m)
  {
    var x = Vector3.Normalize(new Vector3(m.M11, m.M12, m.M13));
    var y = Vector3.Normalize(new Vector3(m.M21, m.M22, m.M23));
    var z = Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33));
    return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, m.M41, m.M42, m.M43, 1);
  }

  internal static int FindBone(IReadOnlyList<Bone> bones, string name)
  {
    if (string.IsNullOrEmpty(name))
      return -1;
    for (var i = 0; i < bones.Count; i++)
      if (string.Equals(bones[i].Name, name, StringComparison.OrdinalIgnoreCase))
        return i;
    return -1;
  }

  /// <summary>
  ///   Fibres in NvCloth's four phases from the triangle mesh: edges split into vertical and horizontal by their
  ///   bind direction (vertical = mostly along gravity), shear = the opposite corners of two triangles that share an
  ///   edge and are nearly coplanar (quad diagonals), bend = the opposite corners otherwise.
  /// </summary>
  private static Constraint[][] BuildPhases(Vector3[] p, int[] tris)
  {
    var edges = new Dictionary<(int, int), List<int>>();
    for (var t = 0; t + 2 < tris.Length; t += 3)
    {
      for (var e = 0; e < 3; e++)
      {
        var a = tris[t + e];
        var b = tris[t + (e + 1) % 3];
        var key = a < b ? (a, b) : (b, a);
        if (!edges.TryGetValue(key, out var list))
          edges[key] = list = new List<int>(2);
        list.Add(tris[t + (e + 2) % 3]);
      }
    }

    var vertical = new List<Constraint>();
    var horizontal = new List<Constraint>();
    var bend = new List<Constraint>();
    var shear = new List<Constraint>();
    var seen = new HashSet<(int, int)>();
    foreach (var ((a, b), _) in edges)
    {
      var d = p[b] - p[a];
      var rest = d.Length();
      if (rest < 1e-7f)
        continue;
      seen.Add((a, b));
      (MathF.Abs(d.Y) >= rest * 0.7071f ? vertical : horizontal).Add(new Constraint(a, b, rest));
    }

    foreach (var ((a, b), opposite) in edges)
    {
      if (opposite.Count != 2)
        continue;
      int c = opposite[0], d = opposite[1];
      var key = c < d ? (c, d) : (d, c);
      if (c == d || seen.Contains(key))
        continue;
      seen.Add(key);
      var rest = (p[d] - p[c]).Length();
      if (rest < 1e-7f)
        continue;
      var n1 = Vector3.Cross(p[b] - p[a], p[c] - p[a]);
      var n2 = Vector3.Cross(p[d] - p[a], p[b] - p[a]);
      var coplanar = n1.LengthSquared() > 0 && n2.LengthSquared() > 0 && Vector3.Dot(Vector3.Normalize(n1), Vector3.Normalize(n2)) > 0.95f;
      // A quad's diagonal pair is longer than both shared-edge ends: treat it as shear; everything else bends.
      (coplanar && rest > (p[b] - p[a]).Length() ? shear : bend).Add(new Constraint(c, d, rest));
    }

    return new[] { vertical.ToArray(), horizontal.ToArray(), bend.ToArray(), shear.ToArray() };
  }

  /// <summary>Each free particle tethers to its nearest pinned particle (graph distance when topological).</summary>
  private static (int, int, float)[] BuildTethers(Vector3[] p, int[] tris, float[] invMass, bool topological)
  {
    var pinned = Enumerable.Range(0, p.Length).Where(i => invMass[i] == 0).ToArray();
    if (pinned.Length == 0)
      return Array.Empty<(int, int, float)>();
    var result = new List<(int, int, float)>();
    if (topological)
    {
      var adjacency = new List<(int, float)>[p.Length];
      for (var i = 0; i < p.Length; i++)
        adjacency[i] = new List<(int, float)>();
      for (var t = 0; t + 2 < tris.Length; t += 3)
      {
        for (var e = 0; e < 3; e++)
        {
          var a = tris[t + e];
          var b = tris[t + (e + 1) % 3];
          var w = (p[a] - p[b]).Length();
          adjacency[a].Add((b, w));
          adjacency[b].Add((a, w));
        }
      }

      var dist = new float[p.Length];
      var source = new int[p.Length];
      Array.Fill(dist, float.MaxValue);
      Array.Fill(source, -1);
      var queue = new PriorityQueue<int, float>();
      foreach (var i in pinned)
      {
        dist[i] = 0;
        source[i] = i;
        queue.Enqueue(i, 0);
      }

      while (queue.TryDequeue(out var u, out var du))
      {
        if (du > dist[u])
          continue;
        foreach (var (v, w) in adjacency[u])
        {
          var nd = du + w;
          if (nd < dist[v])
          {
            dist[v] = nd;
            source[v] = source[u];
            queue.Enqueue(v, nd);
          }
        }
      }

      for (var i = 0; i < p.Length; i++)
        if (invMass[i] > 0 && source[i] >= 0)
          result.Add((i, source[i], dist[i]));
      // Unconnected islands fall back to the nearest pinned particle in space.
      for (var i = 0; i < p.Length; i++)
        if (invMass[i] > 0 && source[i] < 0)
          result.Add(Nearest(i));
    }
    else
    {
      for (var i = 0; i < p.Length; i++)
        if (invMass[i] > 0)
          result.Add(Nearest(i));
    }

    return result.ToArray();

    (int, int, float) Nearest(int i)
    {
      var best = pinned[0];
      var bestD = float.MaxValue;
      foreach (var j in pinned)
      {
        var d = (p[i] - p[j]).LengthSquared();
        if (d < bestD)
        {
          bestD = d;
          best = j;
        }
      }

      return (i, best, MathF.Sqrt(bestD));
    }
  }
}
