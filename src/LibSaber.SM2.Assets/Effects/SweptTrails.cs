using System.Numerics;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   mtl1-53: one weapon swing trail (<c>sfx_trail_triangle</c>) started by a morpheme event in context playback.
///   <paramref name="Start"/>..<paramref name="End"/> is the emission window in clip seconds: the start event, to the first later
///   stop event that names the effect (<c>BlueprintNodeStopPackOfVfx.vfxList</c>, <c>BlueprintNodeVfxStop</c>, a duration
///   event's <c>OnDeactivate</c>), a restart of the same trail, the node's <c>timeLife</c>, or the clip end, whichever is first.
///   Event and stop wiring are PROVEN from the blueprints; "first of" is HEURISTIC.
/// </summary>
/// <param name="Scope">Bone prefix tried first for the locators (<c>wpn</c> → <c>wpn_sfx_trail3</c>), as for other weapon effects.</param>
public sealed record SweptTrailCue(string Event, string Effect, string Source, TrailDef Def, TrailEdge Edge, string? Scope, float Start, float End);

/// <summary>
///   mtl1-53: ribbon for a <c>sfx_trail_triangle</c>. The trail desc is shared with sfx_trail (subclass of sfx_trail_base,
///   FUN_141848c40) and its trailParams fields are PROVEN (FUN_141839b50: maxTrailLength, maxSegLength, angleMin, timeLifeSeg,
///   scaleFreq, scaleRange, displacement, scaleArgDep, inheritedParams, width, addFirstPointOnStart, typeParams; the trailParams
///   <c>color</c>/<c>texture</c>/<c>fadeParams</c> blocks some files carry are not in that list, so they are not read). The
///   runtime builder was not traced; the rule here is HEURISTIC and follows the field names:
///   <list type="bullet">
///     <item>every <see cref="SampleRate"/> Hz of clip time the edge locator1 → locator2 (with offsets) is one cross-section;</item>
///     <item>a cross-section lives <c>timeLifeSeg</c> seconds; normalised age n = age / timeLifeSeg is 0 at the blade, 1 at the tail;</item>
///     <item>colour and alpha = <c>visuals.color</c> (t,A,R,G,B) at n times <c>colorIntensity x colorIntensityMultiplier</c>, so the
///       authored gradient (trail_melee_01_yellow: yellow → black by n 0.68) is the age fade; additive per <c>blendType</c>;</item>
///     <item>texture <c>visuals.texture.nameTex</c> with U along n and V across (0 at locator1, 1 at locator2);</item>
///     <item><c>optimization.degNear/degFar</c> camera fades per vertex, as sfx_trail; <c>maxTrailLength</c> &gt; 0 clips the length.</item>
///   </list>
/// </summary>
public static class SweptTrails
{
  /// <summary>Cross-sections per second of clip time.</summary>
  public static float SampleRate { get; set; } = 120f;

  /// <summary>Whether <paramref name="cue"/> draws anything at clip time <paramref name="t"/>.</summary>
  public static bool Alive(SweptTrailCue cue, float t) => t >= cue.Start && t <= cue.End + cue.Def.TimeLifeSeg && cue.End > cue.Start;

  /// <summary>Sample times (newest first) of the cross-sections alive at <paramref name="t"/>: the head, the grid, the tail.</summary>
  public static List<float> SampleTimes(SweptTrailCue cue, float t)
  {
    var times = new List<float>();
    if (!Alive(cue, t)) return times;
    var s1 = Math.Min(t, cue.End);
    var s0 = Math.Max(cue.Start, t - cue.Def.TimeLifeSeg);
    if (s1 - s0 < 1e-5f) return times;
    times.Add(s1);
    var rate = SampleRate;
    for (var k = (int)MathF.Ceiling(s1 * rate) - 1; k / rate > s0 + 1e-5f; k--)
      if (times[^1] - k / rate > 1e-5f) times.Add(k / rate);
    if (times[^1] - s0 > 1e-5f) times.Add(s0);
    return times;
  }

  /// <summary>
  ///   Ribbon quads for one cue at clip time <paramref name="t"/>. <paramref name="edgeAt"/> gives the world edge
  ///   (locator1 point, locator2 point) at a clip time, or null when the host lacks a locator.
  /// </summary>
  public static SpriteBatch? Build(SweptTrailCue cue, float t, Func<float, (Vector3 A, Vector3 B)?> edgeAt, Vector3 eye)
  {
    var def = cue.Def;
    var r = def.Render;
    if (r.Target != DrawTarget.Color) return null;
    var times = SampleTimes(cue, t);
    if (times.Count < 2) return null;
    var life = Math.Max(1e-4f, def.TimeLifeSeg);
    var edges = new List<(Vector3 A, Vector3 B, float N)>(times.Count);
    var length = 0f;
    var maxLength = def.MaxTrailLength > 0 ? def.MaxTrailLength * def.Scale : float.PositiveInfinity;
    foreach (var s in times)
    {
      if (edgeAt(s) is not { } e) return null;
      var n = Math.Clamp((t - s) / life, 0f, 1f);
      if (edges.Count > 0)
      {
        var step = Vector3.Distance((e.A + e.B) * 0.5f, (edges[^1].A + edges[^1].B) * 0.5f);
        if (length + step > maxLength)
        {
          var f = (maxLength - length) / Math.Max(1e-6f, step);
          var p = edges[^1];
          edges.Add((Vector3.Lerp(p.A, e.A, f), Vector3.Lerp(p.B, e.B, f), p.N + (n - p.N) * f));
          break;
        }

        length += step;
      }

      edges.Add((e.A, e.B, n));
    }

    if (edges.Count < 2) return null;
    var verts = new List<float>((edges.Count - 1) * 4 * SpriteBatch.Stride);
    var scroll = (t - cue.Start) * (def.TexSpeed.X + def.TexSpeed.Y) * 0.5f;
    var depth = 0f;
    var quads = 0;
    void Emit(Vector3 p, Vector3 mid, float n, float across)
    {
      var fade = def.Fade(Vector3.Distance(p, eye));
      var pos = mid + (p - mid) * fade.Size;
      var c = r.Color.Evaluate(n);
      var rgb = new Vector3(c.X, c.Y, c.Z) * r.ColorIntensity.Sample(n, 0.5f) * fade.Intens;
      if (r.Lighting is not ("None" or "")) rgb *= EffectSprites.LitScale * Math.Clamp(r.AmbientLight + 0.55f, 0f, 1f);
      var (tu, tv) = (n - scroll, across);
      if (TrailDef.ApplyTexScale) { tu *= def.TexScale.X; tv *= def.TexScale.Y; }
      verts.Add(pos.X); verts.Add(pos.Y); verts.Add(pos.Z);
      verts.Add(tu); verts.Add(tv);
      verts.Add(rgb.X); verts.Add(rgb.Y); verts.Add(rgb.Z); verts.Add(c.W * r.AlphaIntensity * fade.Alpha);
      verts.Add(0f); verts.Add(r.SoftDepth); verts.Add(0.5f); verts.Add(0.5f);
    }

    for (var i = 0; i + 1 < edges.Count; i++)
    {
      var (a0, b0, n0) = edges[i];
      var (a1, b1, n1) = edges[i + 1];
      if ((a0 - a1).LengthSquared() + (b0 - b1).LengthSquared() < 1e-12f) continue;
      var m0 = (a0 + b0) * 0.5f;
      var m1 = (a1 + b1) * 0.5f;
      Emit(a0, m0, n0, 0f); Emit(a1, m1, n1, 0f); Emit(b1, m1, n1, 1f); Emit(b0, m0, n0, 1f);
      depth += Vector3.Distance((m0 + m1) * 0.5f, eye);
      quads++;
    }

    if (quads == 0) return null;
    return new SpriteBatch { Emitter = r, Frames = TextureFrames.Single, Vertices = verts.ToArray(), QuadCount = quads, Depth = depth / quads, Kind = VfxDrawKind.Trail };
  }

  /// <summary>Edge points from posed bone worlds: locator worlds (scale stripped) with the starter's offsets in the locator frame.</summary>
  public static (Vector3 A, Vector3 B)? Edge(EffectHost host, Matrix4x4[] worlds, TrailEdge edge, string? scope)
  {
    if (host.WorldIn(worlds, edge.Locator1, scope) is not { } w1 || host.WorldIn(worlds, edge.Locator2, scope) is not { } w2) return null;
    return (Vector3.Transform(edge.Offset1, w1), Vector3.Transform(edge.Offset2, w2));
  }
}

/// <summary>
///   mtl1-53: posed bone worlds on a fixed clip-time grid (k / <see cref="SweptTrails.SampleRate"/>), cached so a trail only
///   poses the skeleton once per new cross-section. Off-grid times blend the two neighbouring grid poses' edge points.
/// </summary>
public sealed class SweptPoseCache(Func<float, Matrix4x4[]> pose)
{
  private readonly Dictionary<int, Matrix4x4[]> _grid = [];
  public int Count => _grid.Count;

  public Matrix4x4[] At(int k)
  {
    if (!_grid.TryGetValue(k, out var w)) _grid[k] = w = pose(k / SweptTrails.SampleRate);
    return w;
  }

  /// <summary>Drops grid poses outside [<paramref name="from"/>, <paramref name="to"/>] seconds.</summary>
  public void Trim(float from, float to)
  {
    var lo = (int)MathF.Floor(from * SweptTrails.SampleRate) - 1;
    var hi = (int)MathF.Ceiling(to * SweptTrails.SampleRate) + 1;
    foreach (var k in _grid.Keys.Where(k => k < lo || k > hi).ToList()) _grid.Remove(k);
  }

  public void Clear() => _grid.Clear();

  /// <summary>Edge at clip time <paramref name="time"/> (grid pose, or a blend of the two around it).</summary>
  public (Vector3 A, Vector3 B)? Edge(EffectHost host, TrailEdge edge, string? scope, float time)
  {
    var x = time * SweptTrails.SampleRate;
    var k = (int)MathF.Floor(x + 1e-4f);
    var f = x - k;
    if (SweptTrails.Edge(host, At(k), edge, scope) is not { } e0) return null;
    if (f < 1e-3f) return e0;
    if (SweptTrails.Edge(host, At(k + 1), edge, scope) is not { } e1) return e0;
    return (Vector3.Lerp(e0.A, e1.A, f), Vector3.Lerp(e0.B, e1.B, f));
  }
}
