using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Cross-section of a trail ribbon (<c>trailParams.typeParams</c> union: local__billboard @0x1436459f0,
/// local__tape @0x1436459c0, local__cross @0x1436459d0, local__star @0x1436459e0, cylinder).</summary>
public enum TrailShape { Billboard, Tape, Cylinder, Cross, Star }

/// <summary>
///   <c>sfx_trail</c> (node type string @0x14363c280): a ribbon left behind by its parent (projectile, bone, socket).
///   trailParams is serialised field by field at 0x1417b1031 (struct +0x8a..): <c>maxTrailLength</c> @0x143645e08,
///   <c>maxSegLength</c> @0x143645e18, <c>timeLifeSeg</c> @0x143645e28, <c>scaleFreq</c>/<c>scaleRange</c>/
///   <c>displacement</c>/<c>scaleArgDep</c> @0x143645e38.., <c>addFirstPointOnStart</c> @0x143645e88; visuals at
///   0x1417b06cd (<c>colorIntensityMultiplier</c> @0x143645cf8, <c>alphaMult</c> @0x143645d30, <c>fixateTex</c>
///   @0x143645c00). The runtime ribbon builder was not traced: point emission (a new point every
///   <c>maxSegLength</c> metres, points die after <c>timeLifeSeg</c>, length clipped to <c>maxTrailLength</c>) and
///   width/colour sampled along the ribbon (0 = head, 1 = tail) follow the field names and corpus values.
/// </summary>
public sealed class TrailDef
{
  public required string Name { get; init; }
  public required string Path { get; init; }
  public string NameParent { get; init; } = "";
  public Matrix4x4 Transform { get; init; } = Matrix4x4.Identity;
  public float Scale { get; init; } = 1f;
  public TrailShape Shape { get; init; }
  /// <summary>billboard/tape <c>topology</c> @0x1436458b0: "vertical" lays the texture's V along the ribbon.</summary>
  public bool Vertical { get; init; }
  public int Subdivs { get; init; } = 1;
  public float MaxTrailLength { get; init; }
  public float MaxSegLength { get; init; } = 0.5f;
  public float TimeLifeSeg { get; init; } = 0.5f;
  public Curve1 Width { get; init; } = new([0f, 0.1f]);
  public Vector2 TexSpeed { get; init; }
  /// <summary>
  ///   <c>visuals.texture.tcIn.transform.scale</c> {u,v}. The trail VS (trail.sdc 00001_1) builds UV as
  ///   <c>uv = base + TRAIL_MAPPING.zw + rot(across, along) * TRAIL_MAPPING.xy</c> (CB_PASS_TRAIL offset 0), so the scale is
  ///   applied per axis. Mapping of the sfx field into TRAIL_MAPPING is assumed (heuristic). Applied only with
  ///   <c>SM2_VFX_TRAIL_TC=1</c>.
  /// </summary>
  public Vector2 TexScale { get; init; } = Vector2.One;
  /// <summary>
  ///   <c>optimization.degNear/degFar</c> on the trail. The trail VS applies two per-vertex camera-distance ramps
  ///   <c>saturate((d - TRAIL_DEGR.y) * TRAIL_DEGR.x)</c> / <c>saturate((d - TRAIL_DEGR.z) * TRAIL_DEGR.w)</c> to width
  ///   (TRAIL_MISC.y/z) and colour/alpha (TRAIL_MISC.w, TRAIL_OFFSET.z), i.e. the same linear radStart->radEnd ramp as sprites.
  ///   Which DEGR/MISC slot holds which sfx scale is inferred (HEURISTIC). vfx3z retry: the CB member names
  ///   ("CB_PASS_TRAIL" @0x14378f170, "TRAIL_MISC" @0x14378f1d8, "TRAIL_DEGR" @0x14378f1e8, "TRAIL_OFFSET" @0x14378f230,
  ///   "TRAIL_MISC_2" @0x14378f288) have no code references in ghidra_proj3 (DataRefs: 0 refs), so the CB is packed by
  ///   fixed layout, not by name; the trail shaders (vfxsh_glsl/trail) only consume it. Mapping stays HEURISTIC.
  /// </summary>
  public DistanceFade? DegNear { get; init; }
  public DistanceFade? DegFar { get; init; }
  /// <summary>Render state shared with sprite batches (texture, blend, colour over the ribbon, HDR intensity).</summary>
  public required EmitterDef Render { get; init; }

  public static TrailDef Parse(JsonObject node, string path, Matrix4x4 world, string nameParent, float scale)
  {
    var tp = node.Obj("trailParams");
    var shapeNode = tp.Obj("typeParams");
    var kind = SslRead.UnionKind(shapeNode) ?? "billboard";
    // The union's __type is authoritative; saves keep stale data of other members (e.g. local__star with a billboard block).
    var body = shapeNode.Obj(kind) ?? shapeNode.Obj("billboard") ?? shapeNode.Obj("tape");
    var visuals = node.Obj("visuals");
    var texture = visuals.Obj("texture");
    var transform = texture.Obj("tcIn")?.Obj("transform");
    var intensity = SslRead.Scalar(visuals.Get("colorIntensity"), 1f);
    var mult = visuals.Float("colorIntensityMultiplier", 1f);
    var blend = visuals.Str("blendType") switch { "add" => EffectBlend.Add, "blend_add" => EffectBlend.BlendAdd, _ => EffectBlend.Blend };
    var name = path.Split('/')[^1];
    var render = new EmitterDef
    {
      Name = name,
      Path = path,
      ParticleType = "trail",
      Texture = texture.Str("nameTex") ?? node.Str("nameTex"),
      Blend = blend,
      Color = SslRead.Color(visuals.Get("color")),
      ColorIntensity = intensity.Scaled(mult),
      AlphaIntensity = visuals.Float("alphaMult", 1f),
      SoftDepth = visuals.Float("softDepth", 0f),
      Lighting = visuals.Get("lighting") is JsonObject lo ? (lo.Bool("isEnabled", false) ? "Lit" : "None") : visuals.Str("lighting") ?? "None",
      Target = visuals.Bool("isDistortion", false) ? DrawTarget.Distortion : DrawTarget.Color,
    };
    var width = SslRead.Floats(tp.Get("width"));
    return new TrailDef
    {
      Name = name,
      Path = path,
      NameParent = node.Str("nameParent") is { Length: > 0 } np ? np : nameParent,
      Transform = world,
      Scale = scale,
      Shape = kind switch { "tape" => TrailShape.Tape, "cylinder" => TrailShape.Cylinder, "cross" => TrailShape.Cross, "star" => TrailShape.Star, _ => TrailShape.Billboard },
      Vertical = body.Str("topology") == "vertical",
      Subdivs = Math.Max(1, (int)body.Float("segSubdivs", 1f)),
      MaxTrailLength = tp.Float("maxTrailLength", 0f),
      MaxSegLength = Math.Max(0.01f, tp.Float("maxSegLength", 0.5f)),
      TimeLifeSeg = Math.Max(0.005f, tp.Float("timeLifeSeg", 0.5f)),
      Width = width.Length >= 2 ? new Curve1(width) : new Curve1([0f, 0.1f]),
      TexSpeed = transform.Str("wrapMode") == "Clamp" ? Vector2.Zero : SslRead.Range(transform?.Get("speed"), Vector2.Zero),
      Render = render,
      TexScale = SslRead.Range(transform?.Get("scale"), Vector2.One),
      DegNear = DistanceFade.Parse((node.Obj("optimization") ?? tp.Obj("optimization"))?.Obj("degNear")),
      DegFar = DistanceFade.Parse((node.Obj("optimization") ?? tp.Obj("optimization"))?.Obj("degFar")),
    };
  }

  /// <summary><c>SM2_VFX_TRAIL_TC=1</c>: apply <see cref="TexScale"/> (off by default until checked in-game).</summary>
  public static bool ApplyTexScale { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_TRAIL_TC") == "1";

  /// <summary>Size / intensity / alpha factors at camera distance <paramref name="d"/> (1,1,1 without fades).</summary>
  public (float Size, float Intens, float Alpha) Fade(float d)
  {
    float s = 1, i = 1, a = 1;
    if (!EffectSprites.DistanceFades) return (s, i, a);
    // Trail VS: near = lerp(scale, 1, saturate((d - start) * inv)) -> scale applies at/inside radStart;
    // far = lerp(1, scale, saturate((d - start) * inv)) -> scale applies beyond radEnd.
    for (var n = 0; n < 2; n++)
    {
      var f = n == 0 ? DegNear : DegFar;
      if (f is null) continue;
      var k = n == 0 ? 1 - f.Amount(d) : f.Amount(d);
      s *= 1 + (f.Size - 1) * k; i *= 1 + (f.Intens - 1) * k; a *= 1 + (f.Alpha - 1) * k;
    }
    return (s, i, a);
  }
}

public struct TrailPoint
{
  public Vector3 Position;
  public float Age;
}

public sealed class TrailState(TrailDef def)
{
  public TrailDef Def { get; } = def;
  /// <summary>Oldest first; the live head (parent position) is appended when drawing.</summary>
  public List<TrailPoint> Points { get; } = [];
  public Vector3 Head;
  public bool Started;
  public float Time;
}

public static class EffectTrailSprites
{
  /// <summary>Ribbon quads for every trail (one batch per trail), in the sprite vertex layout.</summary>
  public static IEnumerable<SpriteBatch> Build(EffectInstance instance, Vector3 eye, Func<EmitterDef, bool>? include = null)
  {
    foreach (var state in instance.Trails)
    {
      var def = state.Def;
      if (def.Render.Target != DrawTarget.Color || !state.Started || (include is not null && !include(def.Render))) continue;
      // Head first.
      var pts = new List<(Vector3 P, float Age)> { (state.Head, 0f) };
      for (var i = state.Points.Count - 1; i >= 0; i--)
        if ((state.Points[i].Position - pts[^1].P).LengthSquared() > 1e-8f)
          pts.Add((state.Points[i].Position, state.Points[i].Age));
      if (pts.Count < 2) continue;
      var dist = new float[pts.Count];
      for (var i = 1; i < pts.Count; i++) dist[i] = dist[i - 1] + Vector3.Distance(pts[i].P, pts[i - 1].P);
      var length = dist[^1];
      if (def.MaxTrailLength > 0 && length > def.MaxTrailLength * def.Scale)
      {
        // Clip the tail to maxTrailLength.
        var max = def.MaxTrailLength * def.Scale;
        var k = Array.FindIndex(dist, d => d >= max);
        var f = (max - dist[k - 1]) / Math.Max(1e-6f, dist[k] - dist[k - 1]);
        pts[k] = (Vector3.Lerp(pts[k - 1].P, pts[k].P, f), pts[k].Age);
        dist[k] = max;
        pts.RemoveRange(k + 1, pts.Count - k - 1);
        length = max;
        Array.Resize(ref dist, k + 1);
      }

      if (length < 1e-4f) continue;
      var planes = def.Shape switch { TrailShape.Cross => 2, TrailShape.Star => 3, _ => 1 };
      var verts = new List<float>((pts.Count - 1) * 4 * SpriteBatch.Stride * planes);
      var quads = 0;
      var scroll = state.Time * (def.TexSpeed.X + def.TexSpeed.Y) * 0.5f;
      var r = def.Render;
      var depth = 0f;
      for (var plane = 0; plane < planes; plane++)
        for (var i = 0; i + 1 < pts.Count; i++)
        {
          var a = pts[i].P;
          var b = pts[i + 1].P;
          var dir = b - a;
          if (dir.LengthSquared() < 1e-10f) continue;
          dir = Vector3.Normalize(dir);
          Vector3 Side(Vector3 p)
          {
            Vector3 s;
            if (def.Shape is TrailShape.Billboard or TrailShape.Cylinder) s = Vector3.Cross(dir, p - eye);
            else
            {
              var reference = MathF.Abs(dir.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
              s = Vector3.Cross(dir, reference);
              if (plane > 0)
                s = Vector3.Transform(s, Quaternion.CreateFromAxisAngle(dir, MathF.PI * plane / planes));
            }

            return s.LengthSquared() < 1e-10f ? Vector3.UnitY : Vector3.Normalize(s);
          }

          var na = dist[i] / length;
          var nb = dist[i + 1] / length;
          void Emit(Vector3 p, float n, float side, Vector3 sideDir)
          {
            var fade = def.Fade(Vector3.Distance(p, eye)); // per vertex, as the trail VS (TRAIL_DEGR)
            var w = 0.5f * def.Width.Evaluate(n) * def.Scale * fade.Size;
            var c = r.Color.Evaluate(n);
            var rgb = new Vector3(c.X, c.Y, c.Z) * r.ColorIntensity.Sample(n, 0.5f) * fade.Intens;
            if (r.Lighting is not ("None" or "")) rgb *= EffectSprites.LitScale * Math.Clamp(r.AmbientLight + 0.55f, 0f, 1f);
            var pos = p + sideDir * (w * side);
            var along = n - scroll;
            var across = side * 0.5f + 0.5f;
            verts.Add(pos.X); verts.Add(pos.Y); verts.Add(pos.Z);
            var (tu, tv) = def.Vertical ? (across, along) : (along, across);
            if (TrailDef.ApplyTexScale) { tu *= def.TexScale.X; tv *= def.TexScale.Y; }
            verts.Add(tu); verts.Add(tv);
            verts.Add(rgb.X); verts.Add(rgb.Y); verts.Add(rgb.Z); verts.Add(c.W * r.AlphaIntensity * fade.Alpha);
            verts.Add(0f); verts.Add(r.SoftDepth); verts.Add(0.5f); verts.Add(0.5f);
          }

          var sa = Side(a);
          var sb = Side(b);
          Emit(a, na, -1, sa); Emit(b, nb, -1, sb); Emit(b, nb, 1, sb); Emit(a, na, 1, sa);
          depth += Vector3.Distance(a, eye);
          quads++;
        }

      if (quads == 0) continue;
      yield return new SpriteBatch { Emitter = r, Frames = TextureFrames.Single, Vertices = verts.ToArray(), QuadCount = quads, Depth = depth / quads };
    }
  }
}
