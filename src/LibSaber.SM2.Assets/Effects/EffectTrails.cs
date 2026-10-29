using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Cross-section of a trail ribbon (<c>trailParams.typeParams</c> union: local__billboard @0x1436459f0,
/// local__tape @0x1436459c0, local__cross @0x1436459d0, local__star @0x1436459e0, cylinder).
/// mtl1-53: <c>local__triangle</c> @0x143646c18 is union member 6 in the typeParams reader FUN_1418296f0, which reads
/// only <c>triangle.name</c> for it (billboard/tape blocks next to it in the data are stale and unread, PROVEN).</summary>
public enum TrailShape { Billboard, Tape, Cylinder, Cross, Star, Triangle }

/// <summary>
///   <c>sfx_trail</c> (node type string @0x14363c280): a ribbon left behind by its parent (projectile, bone, socket).
///   trailParams is serialised field by field at 0x1417b1031 (struct +0x8a..): <c>maxTrailLength</c> @0x143645e08,
///   <c>maxSegLength</c> @0x143645e18, <c>timeLifeSeg</c> @0x143645e28, <c>scaleFreq</c>/<c>scaleRange</c>/
///   <c>displacement</c>/<c>scaleArgDep</c> @0x143645e38.., <c>addFirstPointOnStart</c> @0x143645e88; visuals at
///   0x1417b06cd (<c>colorIntensityMultiplier</c> @0x143645cf8, <c>alphaMult</c> @0x143645d30, <c>fixateTex</c>
///   @0x143645c00). The runtime ribbon builder was not traced: point emission (a new point every
///   <c>maxSegLength</c> metres, points die after <c>timeLifeSeg</c>, length clipped to <c>maxTrailLength</c>) and
///   width sampled along the ribbon (0 = head, 1 = tail) follow the field names and corpus values. rea-batch 7: colour is
///   baked per point by FUN_141ab9b60 over point age (PROVEN), see <see cref="TrailDef.BakePointColor"/>.
/// </summary>
/// <summary>
///   fix54: texture usage from the name. In the retail td set (18,690 <c>.td</c>) a name ending in <c>_nm</c> has
///   <c>usage = "MNM"</c> ("Normalmap(rgb)") in 3,619 of 3,619 files and no other usage has that suffix (PROVEN, data), so the
///   suffix stands for the td usage without reading the td.
/// </summary>
public static class SfxTextureUsage
{
  /// <summary>Whether <paramref name="nameTex"/> is a normal map (td usage MNM).</summary>
  public static bool IsNormalMap(string? nameTex)
    => nameTex is { Length: > 3 } n && n.TrimEnd().EndsWith("_nm", StringComparison.OrdinalIgnoreCase);
}

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
  /// <summary>Node <c>timeLife</c> (s, 0 = none). mtl1-53: a swept trail emits until its stop event or this, whichever is first (HEURISTIC).</summary>
  public float TimeLife { get; init; }
  /// <summary><c>trailParams.angleMin</c> (deg). Read for completeness; the swept trail samples on a fixed clock instead (HEURISTIC).</summary>
  public float AngleMin { get; init; }
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
  /// <summary>
  ///   <c>visuals.fadeParams</c>. Serializer FUN_141834320 stores distTranspFront/_useMeters/distTranspMax/_useMeters at
  ///   visuals+0x1d0/+0x1d4/+0x1d8/+0x1dc, ctor FUN_141834200 defaults 0/False/0/False (PROVEN, rea-batch 3). It is NOT
  ///   TRAIL_DEGR (that is optimization.degNear/degFar).
  ///   rea-batch 7 (PROVEN): ribbon trails bake the fade on the CPU per point. FUN_141909810 (SfxTrail update) calls the
  ///   per-point updater FUN_141847640, which writes trlPOINT+0x10 = FUN_141ab9b60(visuals = desc+0x90, base rgba pt+0x14,
  ///   age t, pos pt+0x28, oldest pos, newest pos, inherited alpha pt+0x1c, alpha mult owner+0x144). In FUN_141ab9b60:
  ///   front = flag(+0x1d4) ? F : F*len, back = flag(+0x1dc) ? M : M*len (len = newest - oldest),
  ///   f = sat((newest - pos)/front) * sat((pos - oldest)/back); blendType (visuals+0x78) == 1 (add) -> rgb *= f, else a *= f;
  ///   then isAlphaInherited (visuals+0x54) -> a = inherited alpha; then a *= alpha mult; clamp 0..255. The points are then
  ///   uploaded unchanged by FUN_14184c8d0 ("Trail points data", 80-byte trlPOINT). On by default; <c>SM2_VFX_TRAIL_FADE=0</c> opts out.
  ///   Zero front/back gives factor 1 (same guard as the lightning VS; INFERRED for the CPU body).
  /// </summary>
  public float FadeFront { get; init; }
  public bool FadeFrontMeters { get; init; }
  public float FadeMax { get; init; }
  public bool FadeMaxMeters { get; init; }
  /// <summary><c>visuals.isAlphaInherited</c> (visuals+0x54, PROVEN offset). When set, FUN_141ab9b60 replaces alpha with the
  /// point's inherited alpha (pt+0x1c). FUN_1418bd8d0 (add point) stores 255 there on the normal emitter path
  /// (FUN_1418bbd20 / FUN_141909810 pass NULL, PROVEN), so alpha becomes 1 (gradient alpha and alpha fade dropped).</summary>
  public bool AlphaInherited { get; init; }
  /// <summary>Along-trail fade (FUN_141ab9b60, PROVEN). On by default; <c>SM2_VFX_TRAIL_FADE=0</c> turns it off.</summary>
  public static bool ApplyAlongFade { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_TRAIL_FADE") != "0";

  /// <summary>Along-trail fade factor at distance <paramref name="fromHead"/> from the newest point (head, emitter end) of a
  /// trail of <paramref name="length"/>: sat((newest-pos)/front) * sat((pos-oldest)/back) (FUN_141ab9b60, PROVEN).</summary>
  public float AlongFade(float fromHead, float length)
  {
    var front = FadeFrontMeters ? FadeFront : FadeFront * length;
    var max = FadeMaxMeters ? FadeMax : FadeMax * length;
    var k = front > 1e-6f ? Math.Clamp(fromHead / front, 0f, 1f) : 1f;
    if (max > 1e-6f) k *= Math.Clamp((length - fromHead) / max, 0f, 1f);
    return k;
  }

  /// <summary>
  ///   Per-point colour as FUN_141ab9b60 bakes it (PROVEN order), in 0..1: base (white, FUN_1418bd8d0 normal path) times the
  ///   visuals gradient sampled at normalised AGE <paramref name="ageT"/> (not position), then the along fade on rgb (add) or
  ///   alpha, then inherited alpha (1 = 255 on the normal path), then <paramref name="ownerAlpha"/> (owner+0x144; its source
  ///   is not traced, callers pass 1).
  /// </summary>
  public Vector4 BakePointColor(float ageT, float fromHead, float length, float ownerAlpha = 1f)
  {
    var c = Render.Color.Evaluate(Math.Clamp(ageT, 0f, 1f));
    var f = ApplyAlongFade ? AlongFade(fromHead, length) : 1f;
    if (Render.Blend == EffectBlend.Add) { c.X *= f; c.Y *= f; c.Z *= f; } else c.W *= f;
    if (AlphaInherited) c.W = 1f;
    c.W *= ownerAlpha;
    return Vector4.Clamp(c, Vector4.Zero, Vector4.One);
  }

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
    var triangle = node.Str("__type") == "sfx_trail_triangle";
    // mtl1-53: a swept trail without blendType draws additive, the sfx material default (mtl1-50: mtl_template_sfx.td ->
    // sfx_sdr_preset_default.sd). Every retail sfx_trail_triangle sets blendType, so this only covers edits. sfx_trail keeps blend.
    var blend = visuals.Str("blendType") switch
    {
      "add" => EffectBlend.Add, "blend_add" => EffectBlend.BlendAdd, "blend" => EffectBlend.Blend,
      _ => triangle ? EffectBlend.Add : EffectBlend.Blend,
    };
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
      // fix54 (HEURISTIC rule, data INFERRED): a trail whose visuals texture is a normal map (td usage MNM) is a distortion /
      // refraction ribbon, never colour. Of the 48 retail trail nodes with an MNM texture, 46 set isDistortion; the other two
      // (trail_melee_dark, trail_sword_melee_warrior_03) are clones of their weapon's distortion trail (same nm texture, sdr,
      // width, spline) with isDistortion False, and drawn as colour they made a dark fan the owner does not see in game.
      Target = visuals.Bool("isDistortion", false) || SfxTextureUsage.IsNormalMap(texture.Str("nameTex") ?? node.Str("nameTex"))
        ? DrawTarget.Distortion : DrawTarget.Color,
    };
    var width = SslRead.Floats(tp.Get("width"));
    return new TrailDef
    {
      Name = name,
      Path = path,
      NameParent = node.Str("nameParent") is { Length: > 0 } np ? np : nameParent,
      Transform = world,
      Scale = scale,
      Shape = triangle ? TrailShape.Triangle : kind switch { "tape" => TrailShape.Tape, "cylinder" => TrailShape.Cylinder, "cross" => TrailShape.Cross, "star" => TrailShape.Star, _ => TrailShape.Billboard },
      Vertical = body.Str("topology") == "vertical",
      Subdivs = Math.Max(1, (int)body.Float("segSubdivs", 1f)),
      MaxTrailLength = tp.Float("maxTrailLength", 0f),
      TimeLife = node.Float("timeLife", 0f),
      AngleMin = tp.Float("angleMin", 0f),
      MaxSegLength = Math.Max(0.01f, tp.Float("maxSegLength", 0.5f)),
      TimeLifeSeg = Math.Max(0.005f, tp.Float("timeLifeSeg", 0.5f)),
      Width = width.Length >= 2 ? new Curve1(width) : new Curve1([0f, 0.1f]),
      TexSpeed = transform.Str("wrapMode") == "Clamp" ? Vector2.Zero : SslRead.Range(transform?.Get("speed"), Vector2.Zero),
      Render = render,
      TexScale = SslRead.Range(transform?.Get("scale"), Vector2.One),
      DegNear = DistanceFade.Parse((node.Obj("optimization") ?? tp.Obj("optimization"))?.Obj("degNear")),
      DegFar = DistanceFade.Parse((node.Obj("optimization") ?? tp.Obj("optimization"))?.Obj("degFar")),
      FadeFront = visuals?.Obj("fadeParams")?.Float("distTranspFront", 0f) ?? 0f,
      FadeFrontMeters = visuals?.Obj("fadeParams")?.Bool("distTranspFront_useMeters", false) ?? false,
      FadeMax = visuals?.Obj("fadeParams")?.Float("distTranspMax", 0f) ?? 0f,
      FadeMaxMeters = visuals?.Obj("fadeParams")?.Bool("distTranspMax_useMeters", false) ?? false,
      AlphaInherited = visuals?.Bool("isAlphaInherited", false) ?? false,
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
        pts[k] = (Vector3.Lerp(pts[k - 1].P, pts[k].P, f), pts[k - 1].Age + (pts[k].Age - pts[k - 1].Age) * f);
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
          // rea-batch 7 (PROVEN): the gradient is sampled at the point's normalised age (FUN_141847640 -> FUN_141ab9b60,
          // trlPOINT half2 age at +0x34). Normalising by timeLifeSeg is INFERRED (the half2 holds age and life).
          var life = Math.Max(1e-4f, def.TimeLifeSeg);
          var ta = pts[i].Age / life;
          var tb = pts[i + 1].Age / life;
          void Emit(Vector3 p, float n, float tAge, float side, Vector3 sideDir)
          {
            var fade = def.Fade(Vector3.Distance(p, eye)); // per vertex, as the trail VS (TRAIL_DEGR)
            var w = 0.5f * def.Width.Evaluate(n) * def.Scale * fade.Size;
            var c = def.BakePointColor(tAge, n * length, length);
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
          Emit(a, na, ta, -1, sa); Emit(b, nb, tb, -1, sb); Emit(b, nb, tb, 1, sb); Emit(a, na, ta, 1, sa);
          depth += Vector3.Distance(a, eye);
          quads++;
        }

      if (quads == 0) continue;
      yield return new SpriteBatch { Emitter = r, Frames = TextureFrames.Single, Vertices = verts.ToArray(), QuadCount = quads, Depth = depth / quads, Kind = VfxDrawKind.Trail };
    }
  }
}
