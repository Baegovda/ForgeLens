using System.Numerics;
using System.Runtime.CompilerServices;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>R75: result of <see cref="MeshPick.Pick"/>: the nearest drawn part under a screen point.</summary>
public sealed record PickHit(int Batch, int PartIndex, PreviewPartDraw Part, float Distance, Vector3 Point, int Triangle, double Ms, int TrianglesTested);

/// <summary>
///   R75: CPU ray pick against the exact frame the viewer drew (<see cref="PreviewScene"/>: same camera, part visibility,
///   skin palette). Unskinned batches keep cached 128-triangle chunk boxes (levels: hundreds of thousands of instances,
///   a ray only touches a few chunks); skinned batches are posed once per pick. Cloth vertex overrides are ignored
///   (the bind/skinned position is tested) - INFERRED small error on cloth only.
/// </summary>
public static class MeshPick
{
  private const int Chunk = 128;
  private sealed class Boxes { public Vector3[] Min = Array.Empty<Vector3>(); public Vector3[] Max = Array.Empty<Vector3>(); public int[] Part = Array.Empty<int>(); public int[] Start = Array.Empty<int>(); public int[] Count = Array.Empty<int>(); }
  private static readonly ConditionalWeakTable<object, Boxes> Cache = new();

  /// <summary><paramref name="ndc"/>: x,y in -1..1 (y up).</summary>
  public static PickHit? Pick(PreviewScene scene, Vector2 ndc)
  {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    if (!Matrix4x4.Invert(scene.View * scene.Projection, out var inv))
      return null;
    var near = Vector4.Transform(new Vector4(ndc.X, ndc.Y, 0f, 1f), inv);
    var far = Vector4.Transform(new Vector4(ndc.X, ndc.Y, 1f, 1f), inv);
    var o = new Vector3(near.X, near.Y, near.Z) / near.W;
    var d = Vector3.Normalize(new Vector3(far.X, far.Y, far.Z) / far.W - o);
    var invD = new Vector3(1f / d.X, 1f / d.Y, 1f / d.Z);
    PickHit? best = null;
    var tested = 0;
    var skin = scene.SkinMatrices;
    for (var b = 0; b < scene.Batches.Count; b++)
    {
      var batch = scene.Batches[b];
      var drawn = new bool[batch.Parts.Count];
      var any = false;
      for (var p = 0; p < drawn.Length; p++) any |= drawn[p] = batch.Parts[p].Draw && batch.Parts[p].IndexCount >= 3;
      if (!any) continue;
      var posed = batch.Skin is not null && skin is { Length: > 0 } ? Pose(batch, skin) : batch.Positions;
      var boxes = posed == batch.Positions ? Cache.GetValue(batch.Key, _ => Build(batch, batch.Positions)) : Build(batch, posed);
      var bestT = best?.Distance ?? float.MaxValue;
      int bestTri = -1, bestPart = -1;
      var lockObj = new object();
      var idx = batch.Indices;
      Parallel.For(0, boxes.Min.Length, () => (t: float.MaxValue, tri: -1, part: -1, n: 0), (c, _, local) =>
      {
        if (!drawn[boxes.Part[c]] || !RayBox(o, invD, boxes.Min[c], boxes.Max[c], out var tBox) || tBox > Math.Min(local.t, Volatile.Read(ref bestT)))
          return local;
        var end = boxes.Start[c] + boxes.Count[c];
        for (var i = boxes.Start[c]; i + 2 < end; i += 3)
        {
          local.n++;
          if (Tri(o, d, posed[idx[i]], posed[idx[i + 1]], posed[idx[i + 2]], out var t) && t < local.t)
          { local.t = t; local.tri = i / 3; local.part = boxes.Part[c]; }
        }
        return local;
      }, local =>
      {
        lock (lockObj)
        {
          tested += local.n;
          if (local.tri >= 0 && local.t < bestT) { bestT = local.t; bestTri = local.tri; bestPart = local.part; }
        }
      });
      if (bestTri >= 0)
        best = new PickHit(b, bestPart, batch.Parts[bestPart], bestT, o + d * bestT, bestTri, 0, 0);
    }
    return best is null ? null : best with { Ms = sw.Elapsed.TotalMilliseconds, TrianglesTested = tested };
  }

  private static Vector3[] Pose(PreviewBatch batch, Matrix4x4[] skin)
  {
    var src = batch.Positions;
    var w = batch.Skin!;
    var res = new Vector3[src.Length];
    Parallel.For(0, src.Length, i =>
    {
      if (i >= w.Length) { res[i] = src[i]; return; }
      var s = w[i];
      var total = s.W0 + s.W1 + s.W2 + s.W3;
      if (total <= 0) { res[i] = src[i]; return; }
      var v = Vector3.Zero;
      void Add(ushort j, float wt) { if (wt == 0) return; v += (j < skin.Length ? Vector3.Transform(src[i], skin[j]) : src[i]) * wt; }
      Add(s.J0, s.W0); Add(s.J1, s.W1); Add(s.J2, s.W2); Add(s.J3, s.W3);
      res[i] = v;
    });
    return res;
  }

  private static Boxes Build(PreviewBatch batch, Vector3[] pos)
  {
    var min = new List<Vector3>(); var max = new List<Vector3>(); var part = new List<int>(); var start = new List<int>(); var count = new List<int>();
    var idx = batch.Indices;
    for (var p = 0; p < batch.Parts.Count; p++)
    {
      var pd = batch.Parts[p];
      var end = Math.Min(idx.Length, pd.IndexStart + pd.IndexCount);
      for (var s = pd.IndexStart; s + 2 < end; s += Chunk * 3)
      {
        var e = Math.Min(end, s + Chunk * 3);
        var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
        for (var i = s; i < e; i++) { var v = pos[(uint)idx[i] < (uint)pos.Length ? idx[i] : 0]; lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
        min.Add(lo); max.Add(hi); part.Add(p); start.Add(s); count.Add(e - s);
      }
    }
    return new Boxes { Min = min.ToArray(), Max = max.ToArray(), Part = part.ToArray(), Start = start.ToArray(), Count = count.ToArray() };
  }

  private static bool RayBox(Vector3 o, Vector3 invD, Vector3 lo, Vector3 hi, out float tNear)
  {
    var t1 = (lo - o) * invD; var t2 = (hi - o) * invD;
    var tmin = Vector3.Min(t1, t2); var tmax = Vector3.Max(t1, t2);
    tNear = MathF.Max(MathF.Max(tmin.X, tmin.Y), MathF.Max(tmin.Z, 0f));
    var tFar = MathF.Min(MathF.Min(tmax.X, tmax.Y), tmax.Z);
    return tFar >= tNear;
  }

  /// <summary>Möller–Trumbore, two-sided.</summary>
  private static bool Tri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
  {
    t = 0;
    var e1 = b - a; var e2 = c - a;
    var p = Vector3.Cross(d, e2);
    var det = Vector3.Dot(e1, p);
    if (MathF.Abs(det) < 1e-12f) return false;
    var f = 1f / det;
    var s = o - a;
    var u = f * Vector3.Dot(s, p);
    if (u < 0 || u > 1) return false;
    var q = Vector3.Cross(s, e1);
    var v = f * Vector3.Dot(d, q);
    if (v < 0 || u + v > 1) return false;
    t = f * Vector3.Dot(e2, q);
    return t > 1e-6f;
  }
}
