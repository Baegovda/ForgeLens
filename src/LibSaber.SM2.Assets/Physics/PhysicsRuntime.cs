using System.Numerics;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>Replacement vertex data for one preview batch: model-space positions and normals, already posed.</summary>
public sealed class VertexOverride
{
  public required object BatchKey { get; init; }
  public required int[] Indices { get; init; }
  public required Vector3[] Positions { get; init; }
  public required Vector3[] Normals { get; init; }
}

/// <summary>
///   Secondary motion for one mesh: the jiggle bones (exact port of the engine step) and the cloth shells
///   (NvCloth approximation, <see cref="ClothInstance"/>). Driven by the preview clock: a forward step advances,
///   a wrap of a looping clip continues, a scrub or a long pause resets.
/// </summary>
public sealed class PhysicsRuntime
{
  /// <summary>Engine-like fixed step for the jiggle springs (the game steps once per frame at 60 fps or more).</summary>
  public const float JiggleStep = 1f / 60f;
  public const float MaxFrameGap = 0.25f;

  private readonly ExportedMesh _mesh;
  private readonly List<(JiggleObject Obj, int[] Descendants)> _jiggles = new();
  private readonly List<ClothInstance> _cloths = new();
  private readonly Dictionary<object, List<RenderBinding>> _bindings = new(ReferenceEqualityComparer.Instance);
  private float _lastTime = float.NaN;
  private Vector3 _lastRoot;
  private int _root = -1;

  public PhysicsRuntime(ExportedMesh mesh, PhysicsSetup setup)
  {
    _mesh = mesh;
    Setup = setup;
    var bones = mesh.Bones;
    var depth = new int[bones.Count];
    for (var i = 0; i < bones.Count; i++)
    {
      var d = 0;
      for (var p = bones[i].Parent; p >= 0 && p < bones.Count && d < bones.Count; p = bones[p].Parent)
        d++;
      depth[i] = d;
      if (_root < 0 && bones[i].Parent < 0)
        _root = i;
    }

    foreach (var settings in setup.Jiggles)
    {
      var bone = ClothInstance.FindBone(bones, settings.Name);
      if (bone < 0)
      {
        Missing.Add(settings.Name);
        continue;
      }

      var parent = bones[bone].Parent;
      var descendants = Enumerable.Range(0, bones.Count).Where(i => IsDescendant(bones, i, bone)).OrderBy(i => depth[i]).ToArray();
      _jiggles.Add((new JiggleObject(settings, bone, parent, bones[bone].Local), descendants));
    }

    _jiggles.Sort((a, b) => depth[a.Obj.Bone].CompareTo(depth[b.Obj.Bone]));
    foreach (var entry in setup.Cloths)
    {
      var shell = mesh.ClothSims.FirstOrDefault(s => string.Equals(s.Name, entry.SimMeshName, StringComparison.OrdinalIgnoreCase));
      if (shell is null || shell.Positions.Count == 0 || shell.Indices.Count < 3)
      {
        Missing.Add(entry.SimMeshName);
        continue;
      }

      _cloths.Add(new ClothInstance(entry, shell, bones));
    }
  }

  public PhysicsSetup Setup { get; }
  public List<string> Missing { get; } = new();
  /// <summary>One line per bound render part (vertex count and how many sit within maxInfluenceDist).</summary>
  public List<string> BindNotes { get; } = new();
  public IReadOnlyList<ClothInstance> Cloths => _cloths;
  public int JiggleCount => _jiggles.Count;
  public bool IsEmpty => _jiggles.Count == 0 && _cloths.Count == 0;
  public double LastStepMs { get; private set; }
  public int ResetCount { get; private set; }

  private object? _clip;

  /// <summary>Reset when the mesh's clip changes (animation switch).</summary>
  public void Track(object? clip)
  {
    if (ReferenceEquals(clip, _clip))
      return;
    _clip = clip;
    Reset();
  }

  public void Reset()
  {
    _lastTime = float.NaN;
    foreach (var (obj, _) in _jiggles)
      obj.Reset();
    foreach (var cloth in _cloths)
      cloth.Reset();
    ResetCount++;
  }

  /// <summary>
  ///   Advance to <paramref name="time"/> and write the jiggle bones (and their children) into
  ///   <paramref name="worlds"/>. <paramref name="duration"/> lets a looping clip wrap without a reset.
  /// </summary>
  public void Step(Matrix4x4[] worlds, float time, float duration)
  {
    var watch = System.Diagnostics.Stopwatch.StartNew();
    float dt;
    if (float.IsNaN(_lastTime))
      dt = 0;
    else
    {
      dt = time - _lastTime;
      if (dt < 0 && duration > 0 && _lastTime - time > duration * 0.5f)
        dt += duration;
      if (dt < 0 || dt > MaxFrameGap)
      {
        Reset();
        dt = 0;
      }
    }

    // Root jumps (loop wrap of a clip with root motion) teleport the simulation with it, as the engine's
    // teleport check does (0x14147ed64: velocity change over dt^2 above a threshold zeroes the spring).
    if (_root >= 0 && _root < worlds.Length)
    {
      var root = worlds[_root].Translation;
      if (!float.IsNaN(_lastTime) && dt > 0 && (root - _lastRoot).Length() > 0.5f)
      {
        var delta = root - _lastRoot;
        foreach (var (obj, _) in _jiggles)
          obj.Translate(delta);
        foreach (var cloth in _cloths)
          cloth.Translate(delta);
      }

      _lastRoot = root;
    }

    _lastTime = time;
    var steps = dt > 0 ? Math.Max(1, (int)MathF.Ceiling(dt / JiggleStep - 1e-3f)) : 1;
    var h = dt > 0 ? dt / steps : 0;
    foreach (var (obj, descendants) in _jiggles)
    {
      if (obj.Bone >= worlds.Length)
        continue;
      var parentWorld = obj.Parent >= 0 && obj.Parent < worlds.Length ? worlds[obj.Parent] : Matrix4x4.Identity;
      Matrix4x4 next = worlds[obj.Bone];
      if (h <= 0)
      {
        // No time passed: hold the last pose (first frame seeds the spring at its target).
        next = obj.Step(parentWorld, 0f);
      }
      else
      {
        for (var s = 0; s < steps; s++)
          next = obj.Step(parentWorld, h);
      }

      if (!Matrix4x4.Invert(worlds[obj.Bone], out var inverse))
        continue;
      var delta = inverse * next;
      worlds[obj.Bone] = next;
      foreach (var d in descendants)
        worlds[d] *= delta;
    }

    if (_cloths.Count > 0)
    {
      var skin = new Matrix4x4[worlds.Length];
      for (var i = 0; i < skin.Length && i < _mesh.Bones.Count; i++)
        skin[i] = _mesh.Bones[i].InverseBind * worlds[i];
      foreach (var cloth in _cloths)
        cloth.Step(skin, worlds, dt);
    }

    LastStepMs = watch.Elapsed.TotalMilliseconds;
  }

  private static bool IsDescendant(List<Bone> bones, int i, int ancestor)
  {
    var guard = 0;
    for (var p = bones[i].Parent; p >= 0 && p < bones.Count && guard++ < bones.Count; p = bones[p].Parent)
      if (p == ancestor)
        return true;
    return false;
  }

  private readonly struct RenderBinding
  {
    public required ClothInstance Cloth { get; init; }
    public required int[] Vertices { get; init; }
    public required int[] Triangle { get; init; }
    public required Vector3[] Bary { get; init; }
    public required float[] Height { get; init; }
    public required float[] Weight { get; init; }
    public required Vector3[] BindNormal { get; init; }
  }

  /// <summary>
  ///   Posed positions and normals for the render parts the cloths drive in one batch. <paramref name="rebase"/>
  ///   is applied after the simulation (the sim runs in clip space so locomotion still drags the cloth).
  /// </summary>
  public VertexOverride? Override(PreviewBatch batch, Matrix4x4[] skinMatrices, Matrix4x4 rebase)
  {
    if (_cloths.Count == 0)
      return null;
    List<RenderBinding> bindings;
    lock (_bindings)
    {
      if (!_bindings.TryGetValue(batch.Key, out bindings!))
        _bindings[batch.Key] = bindings = Bind(batch);
    }

    if (bindings.Count == 0)
      return null;
    var total = bindings.Sum(b => b.Vertices.Length);
    var indices = new int[total];
    var positions = new Vector3[total];
    var normals = new Vector3[total];
    var o = 0;
    foreach (var b in bindings)
    {
      var sim = b.Cloth.Positions;
      var tris = b.Cloth.Triangles;
      var bindSim = b.Cloth.BindPositions;
      for (var k = 0; k < b.Vertices.Length; k++, o++)
      {
        var v = b.Vertices[k];
        indices[o] = v;
        var skinnedPos = SkinPoint(batch, v, skinMatrices, out var skinnedNormal);
        var t = b.Triangle[k] * 3;
        int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
        var bc = b.Bary[k];
        var pa = Vector3.Transform(sim[ia], rebase);
        var pb = Vector3.Transform(sim[ib], rebase);
        var pc = Vector3.Transform(sim[ic], rebase);
        var cross = Vector3.Cross(pb - pa, pc - pa);
        var n = cross.LengthSquared() > 1e-20f ? Vector3.Normalize(cross) : Vector3.UnitY;
        var simPos = pa * bc.X + pb * bc.Y + pc * bc.Z + n * b.Height[k];
        var bindCross = Vector3.Cross(bindSim[ib] - bindSim[ia], bindSim[ic] - bindSim[ia]);
        var bindN = bindCross.LengthSquared() > 1e-20f ? Vector3.Normalize(bindCross) : Vector3.UnitY;
        var rotation = FromTo(bindN, n);
        var simNormal = Vector3.Normalize(Vector3.Transform(b.BindNormal[k], rotation));
        var w = b.Weight[k];
        positions[o] = Vector3.Lerp(skinnedPos, simPos, w);
        var blended = Vector3.Lerp(skinnedNormal, simNormal, w);
        normals[o] = blended.LengthSquared() > 1e-12f ? Vector3.Normalize(blended) : simNormal;
      }
    }

    return new VertexOverride { BatchKey = batch.Key, Indices = indices, Positions = positions, Normals = normals };
  }

  private static Quaternion FromTo(Vector3 from, Vector3 to)
  {
    var d = Vector3.Dot(from, to);
    if (d > 0.999999f)
      return Quaternion.Identity;
    var axis = Vector3.Cross(from, to);
    if (axis.LengthSquared() < 1e-12f)
      axis = MathF.Abs(from.X) < 0.9f ? Vector3.Cross(from, Vector3.UnitX) : Vector3.Cross(from, Vector3.UnitY);
    return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(d, -1f, 1f)));
  }

  private static Vector3 SkinPoint(PreviewBatch batch, int v, Matrix4x4[] skin, out Vector3 normal)
  {
    var p = batch.Positions[v];
    var n = batch.Normals[v];
    if (batch.Skin is null || v >= batch.Skin.Length || skin.Length == 0)
    {
      normal = n;
      return p;
    }

    var w = batch.Skin[v];
    var sum = Vector3.Zero;
    var nsum = Vector3.Zero;
    var total = 0f;
    void Add(ushort j, float weight)
    {
      if (weight <= 0 || j >= skin.Length)
        return;
      sum += Vector3.Transform(p, skin[j]) * weight;
      nsum += Vector3.TransformNormal(n, skin[j]) * weight;
      total += weight;
    }

    Add(w.J0, w.W0);
    Add(w.J1, w.W1);
    Add(w.J2, w.W2);
    Add(w.J3, w.W3);
    normal = nsum.LengthSquared() > 1e-12f ? Vector3.Normalize(nsum) : n;
    return total > 1e-6f ? sum / total : p;
  }

  /// <summary>
  ///   Bind each render vertex of a driven part to the closest sim triangle (barycentric + normal height).
  ///   Within <c>maxInfluenceDist</c> the vertex follows the sim fully; beyond it the weight falls off as
  ///   (maxInfluenceDist / distance)^distanceRemapPower and the rest follows skinning.
  /// </summary>
  private List<RenderBinding> Bind(PreviewBatch batch)
  {
    var result = new List<RenderBinding>();
    foreach (var cloth in _cloths)
    {
      var vertices = new SortedSet<int>();
      foreach (var part in cloth.Entry.DrivenParts(batch.Parts, p => p.Name))
      {
        var end = Math.Min(batch.Indices.Length, part.IndexStart + part.IndexCount);
        for (var i = Math.Max(0, part.IndexStart); i < end; i++)
          vertices.Add(batch.Indices[i]);
      }

      if (vertices.Count == 0)
        continue;
      var sim = cloth.BindPositions;
      var tris = cloth.Triangles;
      var list = vertices.ToArray();
      var triangle = new int[list.Length];
      var bary = new Vector3[list.Length];
      var height = new float[list.Length];
      var weight = new float[list.Length];
      var bindNormal = new Vector3[list.Length];
      var maxInfluence = Math.Max(1e-4f, cloth.Entry.MaxInfluenceDist);
      var power = cloth.Entry.DistanceRemapPower;
      Parallel.For(0, list.Length, k =>
      {
        var p = batch.Positions[list[k]];
        var best = float.MaxValue;
        var bestTri = 0;
        var bestBary = new Vector3(1, 0, 0);
        var bestH = 0f;
        for (var t = 0; t + 2 < tris.Length; t += 3)
        {
          var a = sim[tris[t]];
          var b = sim[tris[t + 1]];
          var c = sim[tris[t + 2]];
          var q = ClosestOnTriangle(p, a, b, c, out var bc);
          var d = (p - q).LengthSquared();
          if (d < best)
          {
            best = d;
            bestTri = t / 3;
            bestBary = bc;
            var cross = Vector3.Cross(b - a, c - a);
            var n = cross.LengthSquared() > 1e-20f ? Vector3.Normalize(cross) : Vector3.UnitY;
            bestH = Vector3.Dot(p - q, n);
          }
        }

        var distance = MathF.Sqrt(best);
        triangle[k] = bestTri;
        bary[k] = bestBary;
        height[k] = bestH;
        weight[k] = distance <= maxInfluence ? 1f : MathF.Pow(maxInfluence / distance, Math.Max(0.1f, power));
        bindNormal[k] = batch.Normals[list[k]];
      });
      var full = weight.Count(x => x >= 1f);
      BindNotes.Add($"{cloth.Name}: {list.Length} render vertices, {full} within maxInfluenceDist {maxInfluence:0.###}, mean weight {weight.Average():0.00}");
      result.Add(new RenderBinding { Cloth = cloth, Vertices = list, Triangle = triangle, Bary = bary, Height = height, Weight = weight, BindNormal = bindNormal });
    }

    return result;
  }

  /// <summary>Closest point on triangle (Ericson, Real-Time Collision Detection 5.1.5) with barycentrics.</summary>
  internal static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
  {
    var ab = b - a;
    var ac = c - a;
    var ap = p - a;
    float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
    if (d1 <= 0 && d2 <= 0) { bary = new(1, 0, 0); return a; }
    var bp = p - b;
    float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
    if (d3 >= 0 && d4 <= d3) { bary = new(0, 1, 0); return b; }
    var vc = d1 * d4 - d3 * d2;
    if (vc <= 0 && d1 >= 0 && d3 <= 0)
    {
      var v = d1 / (d1 - d3);
      bary = new(1 - v, v, 0);
      return a + ab * v;
    }

    var cp = p - c;
    float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
    if (d6 >= 0 && d5 <= d6) { bary = new(0, 0, 1); return c; }
    var vb = d5 * d2 - d1 * d6;
    if (vb <= 0 && d2 >= 0 && d6 <= 0)
    {
      var w = d2 / (d2 - d6);
      bary = new(1 - w, 0, w);
      return a + ac * w;
    }

    var va = d3 * d6 - d5 * d4;
    if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
    {
      var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
      bary = new(0, 1 - w, w);
      return b + (c - b) * w;
    }

    var denom = 1f / (va + vb + vc);
    var vv = vb * denom;
    var ww = vc * denom;
    bary = new(1 - vv - ww, vv, ww);
    return a + ab * vv + ac * ww;
  }
}
