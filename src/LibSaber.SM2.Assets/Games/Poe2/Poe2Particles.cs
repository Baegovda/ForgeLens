using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Games.Poe2;

/// <summary>
///   M2 step D: PoE2 .ao effect -> the shared SM2 <see cref="EffectDefinition"/>, so the SM2 particle simulation, sprite and
///   trail builders, viewer VFX view and --vfxbench render it (CORE CONCEPT: only the reader changes).
///   Sources (PROVEN layout, poe_data_tools translate JSON / UTF-16 JSON .mat):
///   - .pet <c>payload.gpu_emitters[]</c>: emitter_duration, particle_duration min/max, use_pps + particles_per_second curve,
///     emit_burst/particles_count, is_continuous, particle_mesh, update_material, render_passes[].render_material.
///   - update .mat <c>graphinstances[]</c> (fxgraph parent + custom_parameters): SphereEmitter Radius, EmitVelocityRandom,
///     EmitVelocityAlong (Velocity, Cone Size), Gravity, Air/AirResistanceMultiplier, CurlNoise_Wind Amplitude, EmitScale.
///   - render .mat graphinstances: Colour (RGBA over life), Scale/ScaleXYZ (size over life), Vibrance/COLORV tint,
///     Alpha_Multiply, Force* blend graphs, the first albedo-like .dds parameter, flipbook grid from the _NxM_ file name.
///   - .trl <c>emitters[]</c>: trail_length, max_segment_length, segment_duration, scale curve, material.
///   HEURISTIC (fxgraph bodies are not decoded, no in-game comparison): unit of Scale (x <see cref="SizeUnit"/> m),
///   gravity strength, air friction, curl noise as tangential speed, colour intensity (x <see cref="Intensity"/>),
///   trail width (scale x <see cref="TrailWidthUnit"/> m), mesh particles drawn as camera sprites, fly-by speed.
///   CPU <c>emitters</c> (key_values graphs) map to the same EmitterDef. Not modelled: event start times / intervals, distortion passes (hidden), GPU collision,
///   masks/erosion/UV scroll graphs, skill hue-shift variants.
/// </summary>
public static class Poe2Particles
{
    /// <summary>M2 G: FL_POE2_FX_LEGACYPLATE=1 restores the old untextured / alpha glow cards (before/after captures only).</summary>
    /// <summary>M2 G: terrain / PBR / model material textures used as sprites have no sprite alpha mask and draw as square plates (frost bomb, glacial bolt).</summary>
    private static bool PlateTexture(string? t) => t is not null && (t.Contains("/PBR/", StringComparison.OrdinalIgnoreCase) || t.Contains("/Terrain/", StringComparison.OrdinalIgnoreCase) || t.Contains("Art/Models/", StringComparison.OrdinalIgnoreCase));
    private static readonly bool LegacyPlates = Environment.GetEnvironmentVariable("FL_POE2_FX_LEGACYPLATE") == "1";
  public static float SizeUnit = Env("FL_POE2_FX_SIZE", 0.05f);
  public static float Intensity = Env("FL_POE2_FX_INTENSITY", 60f);
  public static float TrailWidthUnit = Env("FL_POE2_FX_TRAILW", 0.05f);
  public static float FlySpeed = Env("FL_POE2_FX_SPEED", 8f);

  private static float Env(string n, float d) => float.TryParse(Environment.GetEnvironmentVariable(n), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

  public sealed class Result
  {
    public required EffectDefinition Definition { get; init; }
    public Dictionary<string, TextureFrames> Frames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Notes { get; } = new();
    public bool Projectile { get; set; }
  }

  public static string Glow = "Art/particles/FXLib/glow/glow_star_015_BC7.dds";
  private static readonly Regex Grid = new(@"_(\d+)[xX](\d+)(_|\.|$)");

  public static Result Build(Poe2Reader reader, string ao, string? animation = null)
  {
    var info = reader.ReadAo(ao);
    animation ??= info.DefaultAnimation;
    var name = Path.GetFileNameWithoutExtension(ao) + "_" + animation;
    var res = new Result { Definition = new EffectDefinition { Name = name } };
    var rig = new ExportedMesh { Name = name };
    if (info.Skeleton != null)
      try { reader.ReadSkeleton(rig, info.Skeleton, animation); } catch (Exception x) { res.Notes.Add("skeleton: " + x.Message); }
    var worlds = new Matrix4x4[rig.Bones.Count];
    for (var i = 0; i < worlds.Length; i++)
      worlds[i] = rig.Bones[i].Parent < 0 ? rig.Bones[i].Local : rig.Bones[i].Local * worlds[rig.Bones[i].Parent];
    var events = info.Events.Where(e => string.Equals(e.Animation, animation, StringComparison.OrdinalIgnoreCase)).ToList();
    if (events.Count == 0) events = info.Events;
    res.Projectile = ao.Contains("proj", StringComparison.OrdinalIgnoreCase) || events.Any(e => e.Kind == "trl");
    foreach (var ev in events)
    {
      var bi = ev.Bones.FirstOrDefault() is { } bn ? rig.Bones.FindIndex(b => string.Equals(b.Name, bn, StringComparison.OrdinalIgnoreCase)) : -1;
      var at = bi >= 0 ? Matrix4x4.CreateTranslation(worlds[bi].Translation) : Matrix4x4.Identity;
      try
      {
        if (ev.Kind == "pet") Pet(reader, res, ev.File, at);
        else Trl(reader, res, ev.File, at);
      }
      catch (Exception x) when (x is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
      {
        res.Notes.Add($"{ev.File}: {x.GetType().Name} {x.Message}");
      }
    }
    return res;
  }

  /// <summary>Preview root motion: projectiles fly along +X at <see cref="FlySpeed"/> m/s, 1 m above ground, over [-4, 4] m.</summary>
  public static EffectInstance Instance(Result r, ulong seed = 1)
  {
    var lift = Matrix4x4.CreateTranslation(0, 1f, 0);
    if (!r.Projectile) return new EffectInstance(r.Definition, seed) { Root = lift };
    var period = 4f / FlySpeed + 0.3f;
    return new EffectInstance(r.Definition, seed) { Root = lift, RootMotion = t => Matrix4x4.CreateTranslation(Math.Min(-2f + t % period * FlySpeed, 2f), 1f, 0) };
  }

  private static void Pet(Poe2Reader reader, Result res, string pet, Matrix4x4 at)
  {
    using var doc = reader.Json(pet);
    var root = doc.RootElement;
    if (root.TryGetProperty("emitters", out var cpu) && cpu.ValueKind == JsonValueKind.Array)
    {
      var ci = 0;
      foreach (var ce in cpu.EnumerateArray()) Cpu(reader, res, pet, ce, ++ci, at);
    }
    if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("gpu_emitters", out var gpu)) return;
    var n = 0;
    foreach (var em in gpu.EnumerateArray())
    {
      n++;
      var update = Str(em, "update_material");
      var render = em.TryGetProperty("render_passes", out var rp) ? rp.EnumerateArray().Select(r => Str(r, "render_material")).FirstOrDefault(s => !string.IsNullOrEmpty(s)) : null;
      var u = Graphs(reader, update);
      var r = Graphs(reader, render);
      var life = new Vector2(F(em, "particle_duration_min", 1), F(em, "particle_duration_max", 1));
      var continuous = B(em, "is_continuous");
      var pps = B(em, "use_pps");
      var count = em.TryGetProperty("particles_count", out var pc) ? pc.GetSingle() : 1f;
      EmissionKind kind; Vector2 rate = default, burst = default;
      if (pps)
      {
        kind = EmissionKind.ConstantFlow;
        var v = em.TryGetProperty("particles_per_second", out var p) ? CurveOf(p).Max : 10f;
        rate = new Vector2(v, v);
      }
      else if (continuous)
      {
        // burst emitter on a continuous effect (e.g. fireball head: 1 mesh particle, 0.1 s life): keep `count` alive
        kind = EmissionKind.ConstantFlow;
        var v = count / Math.Max(0.02f, (life.X + life.Y) / 2);
        rate = new Vector2(v, v);
      }
      else { kind = EmissionKind.Explosion; burst = new Vector2(count, count); }

      // update graphs
      float radius = 0, speed = 0, cone = 180, curl = 0, friction = 0, emitScale = 1;
      var gravity = Vector3.Zero;
      foreach (var (g, ps) in u)
      {
        switch (g)
        {
          case "SphereEmitter": radius = Val(ps, "Radius") * Poe2Reader.Scale; break;
          case "EmitVelocityRandom": speed = Val(ps, "Emit Velocity") * Poe2Reader.Scale; cone = 180; break;
          case "EmitVelocityAlong": speed = Val(ps, "Velocity") * Poe2Reader.Scale; cone = ps.ContainsKey("Cone Size") ? Val(ps, "Cone Size") : 0; break;
          case "Gravity": gravity = new Vector3(0, -4f, 0); break;
          case "Air": friction = Math.Max(friction, 1f); break;
          case "AirResistanceMultiplier": friction = Math.Max(friction, 1f) * Math.Max(0.1f, Val(ps, "Air Resistance Multiplier", 1)); break;
          case "CurlNoise_Wind": curl = Val(ps, "Amplitude") * Poe2Reader.Scale * 0.3f; break;
          case "EmitScale": emitScale = Val(ps, "Emit Scale", 1); break;
        }
      }

      // render graphs
      var size = ScalarParam.Constant(1);
      var sizeMul = 1f;
      var color = ColorCurve.White;
      var tint = Vector3.One;
      var intensity = 1f;
      var alpha = 1f;
      var blend = EffectBlend.BlendAdd;
      Tint(r, ref tint, ref intensity);
      foreach (var (g, ps) in r)
      {
        switch (g)
        {
          case "Colour" when ps.TryGetValue("Colour", out var c): color = Colour(c); break;
          case "Scale" when ps.TryGetValue("Scale", out var s): size = Scalar(s); break;
          case "ScaleXYZ" when ps.TryGetValue("Scale X", out var s): sizeMul *= Scalar(s).Curve.Max; break;
          case "Scale_XYZ" when ps.TryGetValue("X Scale", out var s): size = Scalar(s); break;
          case "Alpha_Multiply": alpha *= Val(ps, "- Intensity", 1); break;
          case "ForceAlpha" or "ForceAlphaBlendNoGI" or "ForceDownscaledAlphaBlend": blend = EffectBlend.Blend; break;
          case "ForceAdditive": blend = EffectBlend.Add; break;
          case "ForceAddablend": blend = EffectBlend.BlendAdd; break;
        }
      }
      if (r.Any(x => x.Graph == "ForceAdditive")) blend = EffectBlend.Add; // HEURISTIC: additive wins over a later ForceAlpha (flames_render has both)
      if (size.Curve.Count == 1 && size.Curve.Max == 1 && Graph(r, "ObjectPreset") is { } op) size = ScalarParam.Constant(Val(op, "Size", 1));
      var tex = TextureOf(r);
      if (Graph(r, "SIM_Smoke2") is { } sim && sim.TryGetValue("02.NormalDensity_Tex", out var nd) && nd.TryGetProperty("path", out var ndp)) tex = ndp.GetString();
      var faceLock = em.TryGetProperty("render_passes", out var rp2) ? rp2.EnumerateArray().Select(x => Str(x, "face_lock")).FirstOrDefault() ?? "Camera" : "Camera";
      var tileable = tex != null && (tex.Contains("tileables", StringComparison.OrdinalIgnoreCase) || tex.Contains("noise", StringComparison.OrdinalIgnoreCase));
      var isMesh = !string.IsNullOrEmpty(Str(em, "particle_mesh"));
      // HEURISTIC: world-locked planes (face_lock None/X/Y/Z) and tileable-mask sprites need the mask graphs; hidden.
      // Mesh particles stay (drawn as glow sprites) unless they are ground-locked (Z: shock-wave rings such as cleaveGeo01).
      var hidden = isMesh ? faceLock is "Z" : tileable || faceLock is "None" or "Z" or "X" or "Y";
      // LuminanceAlpha graph: alpha from texture luminance (texture key suffix "#lum", applied by the viewer's texture loader)
      if (tex != null && tex != Glow && r.Any(x => x.Graph is "LuminanceAlpha" or "Data_LuminanceFromAlpha" or "Mask_Luminance")) tex += "#lum";
      var frames = TextureFrames.Single;
      var frameMode = FrameMode.None;
      var frameOffset = Vector2.Zero;
      var frameSpline = ScalarParam.Constant(0);
      var meshFile0 = Str(em, "particle_mesh");
      // HEURISTIC: mesh particles are drawn as camera sprites; their tileable/noise scroll textures read as grey squares, so
      // they use a soft glow sprite from the same effect library instead.
      if (!string.IsNullOrEmpty(meshFile0) && (tex == null || tex.Contains("tileables", StringComparison.OrdinalIgnoreCase) || tex.Contains("noise", StringComparison.OrdinalIgnoreCase)))
        tex = Glow;
      var meshExtent = 1f;
      if (!string.IsNullOrEmpty(meshFile0))
        try
        {
          var pm = reader.Load(meshFile0);
          if (pm.Positions.Count > 0)
          {
            var lo = pm.Positions.Aggregate(Vector3.Min); var hi = pm.Positions.Aggregate(Vector3.Max);
            meshExtent = MathF.Max(hi.X - lo.X, MathF.Max(hi.Y - lo.Y, hi.Z - lo.Z)) / SizeUnit; // Size is scaled by SizeUnit below
          }
        }
        catch (Exception x) when (x is IOException or KeyNotFoundException or JsonException) { res.Notes.Add($"mesh {meshFile0}: {x.Message}"); }
      if (tex != null && Grid.Match(Path.GetFileName(tex)) is { Success: true } m)
      {
        var fx = int.Parse(m.Groups[1].Value); var fy = int.Parse(m.Groups[2].Value);
        frames = new TextureFrames(fx, fy, fx * fy, 0, []);
        res.Frames[tex] = frames;
        frameMode = FrameMode.Constant;
        if (r.Any(x => x.Graph.StartsWith("FlipbookRandomPick", StringComparison.Ordinal))) frameOffset = new Vector2(0, fx * fy - 0.01f);
        else { frameMode = FrameMode.AgeSpline; frameSpline = new ScalarParam(new Curve1([0f, 0f, 1f, fx * fy - 0.01f]), Vector2.Zero); }
      }
      if (!LegacyPlates && blend == EffectBlend.Blend && (tex ?? "").Contains("/glow/", StringComparison.OrdinalIgnoreCase)) blend = EffectBlend.BlendAdd; // M2 G: alpha-blended glow card drew a grey plate (stormblast e1) // M2 G: alpha-blended glow card drew a grey plate (stormblast e1)
            var distortion = (render ?? "").Contains("distortion", StringComparison.OrdinalIgnoreCase) || (tex ?? "").Contains("/distortion/", StringComparison.OrdinalIgnoreCase) || r.Any(x => x.Graph.Contains("Distort", StringComparison.Ordinal));
      var meshFile = Str(em, "particle_mesh");
      var c0 = color;
      var def = new EmitterDef
      {
        Name = $"{Path.GetFileNameWithoutExtension(pet)}_e{n}" + (string.IsNullOrEmpty(meshFile) ? "" : "_meshsprite"),
        Path = pet + "#" + n,
        IsGpu = true,
        Visible = !distortion && !hidden && (LegacyPlates || (!string.IsNullOrEmpty(tex) && !PlateTexture(tex) && !((tex ?? "").Contains("/glow/", StringComparison.OrdinalIgnoreCase) && size.Curve.Max >= 20))) /* M2 G: untextured cards, material textures and huge glow cards drew plates behind the effect */,
        Transform = at,
        EmitDuration = continuous ? 0 : F(em, "emitter_duration_max", 1),
        Emission = kind,
        Rate = rate,
        BurstCount = burst,
        MaxParticles = 4000,
        AngleEmission = cone,
        EmitDir = Vector3.UnitY,
        SpeedNorm = ScalarParam.Range(speed * 0.5f, speed),
        SpeedTang = ScalarParam.Range(0, curl),
        RandomOffsetRadius = radius,
        Life = life,
        Size = size.Scaled(SizeUnit * emitScale * sizeMul * meshExtent),
        Texture = tex,
        Blend = blend,
        Color = Tinted(c0, tint),
        ColorIntensity = ScalarParam.Constant(Intensity * intensity),
        AlphaIntensity = alpha,
        FrameMode = frameMode,
        FrameOffset = frameOffset,
        FrameSpline = frameSpline,
        RandomFlip = true,
        RotationAngle = new Vector2(0, 360),
        Gravity = gravity,
        Friction = friction,
      };
      res.Definition.Emitters.Add(def);
      res.Notes.Add($"{def.Name}: {kind} rate {rate.X:0.#} burst {burst.X:0} life {life.X:0.00}-{life.Y:0.00}s r {radius:0.00}m v {speed:0.00}m/s size {size.Curve.Max:0.##} tex {tex ?? "-"} {blend}{(distortion ? " (distortion, hidden)" : hidden ? $" (face_lock {faceLock} / tileable mask, hidden)" : "")}");
    }
  }

  /// <summary>
  ///   CPU emitter (.pet <c>emitters[]</c>, PointEmitter): <c>key_values</c> text, "key v0 .. v6" graphs sampled at 7 even
  ///   points over particle life (PROVEN layout, arc_02/impact_midup.pet). Units cm and cm/s (x0.01). colour = 7 hex AARRGGBB
  ///   (INFERRED byte order). velocity/acceleration graphs use their mean (SM2 sim has constant speed + gravity).
  /// </summary>
  private static readonly float CpuFactor = float.TryParse(Environment.GetEnvironmentVariable("FL_POE2_FX_CPU"), System.Globalization.CultureInfo.InvariantCulture, out var cf) ? cf : 1f;
  private static void Cpu(Poe2Reader reader, Result res, string pet, JsonElement ce, int n, Matrix4x4 at)
  {
    var kv = new Dictionary<string, string[]>(StringComparer.Ordinal);
    foreach (var line in (Str(ce, "key_values") ?? "").Split('\n'))
    {
      var p = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (p.Length > 0) kv[p[0]] = p.Skip(1).ToArray();
    }
    float[] G(string k) => kv.TryGetValue(k, out var v) ? v.Select(x => float.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray() : [];
    float One(string k, float d) => G(k) is { Length: > 0 } a ? a[0] : d;
    Curve1 Life7(float[] a, float k) { if (a.Length == 0) return new Curve1([0f, k]); var flat = new List<float>(); for (var i = 0; i < a.Length; i++) { flat.Add(a.Length == 1 ? 0 : i / (float)(a.Length - 1)); flat.Add(a[i] * k); } return new Curve1(flat.ToArray()); }
    var mat = Str(ce, "material");
    var g = Graphs(reader, mat);
    var tex = TextureOf(g);
    var tint = Vector3.One; var ti = 1f;
    Tint(g, ref tint, ref ti);
    // colour + opacity over life
    var cols = kv.TryGetValue("colour", out var cv) ? cv : [];
    var op = G("opacity");
    var flatC = new List<float>();
    var m = Math.Max(cols.Length, Math.Max(op.Length, 1));
    for (var i = 0; i < m; i++)
    {
      uint argb = i < cols.Length && uint.TryParse(cols[i], System.Globalization.NumberStyles.HexNumber, null, out var u) ? u : 0xffffffff;
      var a = ((argb >> 24) & 255) / 255f * (op.Length > 0 ? op[Math.Min(i, op.Length - 1)] : 1f);
      flatC.AddRange([m == 1 ? 0 : i / (float)(m - 1), a * 255f, ((argb >> 16) & 255) * tint.X, ((argb >> 8) & 255) * tint.Y, (argb & 255) * tint.Z]);
    }
    var pps = G("particles_per_second");
    var rate = pps.Length > 0 ? pps.Average() : 10f;
    var life = One("particle_duration", 1f);
    var lv = One("particle_duration_variance", 0f);
    var up = G("velocity_up"); var along = G("velocity_along");
    var speed = (up.Length > 0 ? up.Average() : 0) * Poe2Reader.Scale;
    var speedA = (along.Length > 0 ? along.Average() : 0) * Poe2Reader.Scale;
    var acc = G("up_acceleration_graph");
    var frames = (int)One("number_of_frames", 1);
    var frameMode = FrameMode.None;
    var spline = ScalarParam.Constant(0);
    if (tex != null && (frames > 1 || Grid.IsMatch(Path.GetFileName(tex))))
    {
      var mm = Grid.Match(Path.GetFileName(tex));
      var fx = mm.Success ? int.Parse(mm.Groups[1].Value) : frames; var fy = mm.Success ? int.Parse(mm.Groups[2].Value) : 1;
      res.Frames[tex] = new TextureFrames(fx, fy, fx * fy, 0, []);
      frameMode = FrameMode.AgeSpline;
      spline = new ScalarParam(new Curve1([0f, 0f, 1f, fx * fy - 0.01f]), Vector2.Zero);
    }
    var blend = (kv.TryGetValue("blend_type", out var bt) ? bt.FirstOrDefault() : "") switch { "AdditiveBlend" => EffectBlend.BlendAdd /* M2 E: the textures carry an alpha mask (fw, lightning_bolts_vert: white RGB, alpha 2-7%); pure Add drew white squares */, "AlphaBlend" => EffectBlend.Blend, _ => EffectBlend.BlendAdd };
    var infinite = One("is_infinite", 0) != 0;
    var name = $"{Path.GetFileNameWithoutExtension(pet)}_c{n}";
    var face = kv.TryGetValue("face_type", out var ft) ? ft.FirstOrDefault() ?? "" : "";
    res.Definition.Emitters.Add(new EmitterDef
    {
      Name = name, Path = pet + "#c" + n, Transform = at,
      Visible = face != "XYAligned" && (LegacyPlates || (face != "XYLock" && !PlateTexture(tex))) /* M2 G: flat ground flash cards drew a panel (flame dash) */ && !(tex ?? "").Contains("/distortion/", StringComparison.OrdinalIgnoreCase),
      EmitDuration = infinite ? 0 : Math.Max(One("emitter_duration", 1), 1f / 60f),
      Emission = EmissionKind.ConstantFlow, Rate = new Vector2(rate, rate), MaxParticles = 4000,
      AngleEmission = One("scatter_cone_size", 0), EmitDir = Vector3.UnitY,
      SpeedNorm = ScalarParam.Range(speed - One("velocity_up_variance", 0) * Poe2Reader.Scale, speed + One("velocity_up_variance", 0) * Poe2Reader.Scale),
      SpeedTang = ScalarParam.Range(0, MathF.Abs(speedA)),
      Life = new Vector2(Math.Max(0.02f, life - lv), life + lv),
      Size = new ScalarParam(Life7(G("scale"), Poe2Reader.Scale * Math.Max(0.01f, One("emit_scale", 1))), new Vector2(-1, 1) * One("scale_variance", 0) * Poe2Reader.Scale),
      Texture = tex, Blend = blend,
      Color = new ColorCurve(flatC.ToArray()),
      // HEURISTIC: CPU emitter intensity factor (env FL_POE2_FX_CPU, default 1). It was 0.35 while AdditiveBlend ignored the alpha masks (white squares).
      ColorIntensity = ScalarParam.Constant(Intensity * ti * CpuFactor),
      FrameMode = frameMode, FrameSpline = spline,
      RandomFlip = One("random_flip_x", 0) != 0,
      RotationAngle = new Vector2(0, 360),
      RotationSpeed = new Vector2(-1, 1) * One("rotation_speed_variance", 0),
      Gravity = new Vector3(0, (acc.Length > 0 ? acc.Average() : 0) * Poe2Reader.Scale, 0),
    });
    res.Notes.Add($"{name}: CPU {Str(ce, "emitter_type")} rate {rate:0.#} life {life:0.00}s v {speed:0.00}m/s size {G("scale").DefaultIfEmpty(0).Max() * Poe2Reader.Scale:0.00}m tex {tex ?? "-"} {blend} face {face}");
  }

  /// <summary>Columns of an "_Nx1_" lightning sheet (0 when the name has no such grid).</summary>
  public static int StripColumns(string tex)
  {
    var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(tex), @"_(\d+)x1_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return m.Success ? int.Parse(m.Groups[1].Value) : 0;
  }

  private static void Trl(Poe2Reader reader, Result res, string trl, Matrix4x4 at)
  {
    using var doc = reader.Json(trl);
    if (!doc.RootElement.TryGetProperty("emitters", out var ems)) return;
    var n = 0;
    foreach (var e in ems.EnumerateArray())
    {
      n++;
      string S(string k) => e.TryGetProperty(k, out var v) ? (v.GetString() ?? "").Trim('"') : "";
      float N(string k, float d) => float.TryParse(S(k), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
      var mat = S("material");
      var g = Graphs(reader, mat);
      var tex = TextureOf(g);
      if (tex != null && StripColumns(tex) > 1 && g.Any(x => x.Graph.StartsWith("LIGHTNING_", StringComparison.Ordinal))) tex += "#strip";
      var width = TrlCurve(S("scale"));
      var name = $"{Path.GetFileNameWithoutExtension(trl)}_t{n}";
      var blend = g.Any(x => x.Graph is "ForceAlpha" or "ForceAlphaBlendNoGI") ? EffectBlend.Blend : EffectBlend.BlendAdd;
      var tint = Vector3.One;
      var ti = 1f;
      Tint(g, ref tint, ref ti);
      var render = new EmitterDef
      {
        Name = name, Path = trl + "#" + n, ParticleType = "trail", Texture = tex, Blend = blend,
        Color = Tinted(ColorCurve.White, tint), ColorIntensity = ScalarParam.Constant(Intensity * ti),
      };
      var len = N("trail_length", 300) * Poe2Reader.Scale;
      res.Definition.Trails.Add(new TrailDef
      {
        Name = name, Path = trl, Transform = at, Shape = TrailShape.Billboard,
        MaxTrailLength = len,
        MaxSegLength = Math.Max(0.02f, N("max_segment_length", 50) * Poe2Reader.Scale),
        TimeLifeSeg = Math.Max(0.02f, N("segment_duration", 0.5f) * Math.Min(1f, len / Math.Max(0.1f, FlySpeed))),
        Width = width.Scaled(TrailWidthUnit),
        Render = render,
      });
      res.Notes.Add($"{name}: trail length {len:0.00}m width {width.Max * TrailWidthUnit:0.00}m tex {tex ?? "-"}");
    }
  }

  /// <summary>Colour graphs shared by sprites and trails (HEURISTIC constant tint, see class remarks).</summary>
  private static void Tint(List<(string Graph, Dictionary<string, JsonElement> Params)> r, ref Vector3 tint, ref float intensity)
  {
    foreach (var (g, ps) in r)
      switch (g)
      {
          case "Vibrance_Albedo" or "Vibrance_Albedo_PreLight":
          if (ps.TryGetValue("- Hue", out var h) && Vec(h) is { } hv) tint *= hv;
          intensity *= Math.Max(0.1f, Val(ps, "- Intensity", 1)); break;
        case "LIGHTNING_Main" when ps.TryGetValue("02. Lightning Color", out var lc) && Vec(lc) is { } lv:
          {
            // M2 E HEURISTIC: linear bolt colour (spark: 0.023/0.032/0.11) times a sparse 4x1 strip (mean texel ~4%): the hue is
            // the colour normalised to max 1, and the strength is max*100 (the shader's decay power makes the core white-hot, not modelled).
            var mx = Math.Max(1e-4f, Math.Max(lv.X, Math.Max(lv.Y, lv.Z)));
            tint *= lv / mx; intensity *= mx * 100f; break;
          }
        case "Channel_LookUpColor" when ps.TryGetValue("Color Look Up RGB", out var lu):
          {
            // HEURISTIC: luminance -> RGB ramp sampled at 0.7 (a bright texel) as a constant tint, max channel normalised to 1
            float At(string k) => lu.TryGetProperty(k, out var x) ? CurveOf(x).Evaluate(0.7f) : 1f;
            var lt = new Vector3(At("R"), At("G"), At("B"));
            var mx = MathF.Max(lt.X, MathF.Max(lt.Y, lt.Z));
            if (mx > 1e-3f) { tint *= lt / mx; intensity *= mx * Math.Max(0.1f, Val(ps, "Final Color Multiplier", 1)); }
            break;
          }
        case "COLORV_Albedo" when ps.TryGetValue("00. Color", out var cc) && Vec(cc) is { } cv: tint *= cv; break;
        case "SIM_Smoke2" when ps.TryGetValue("04.Albedo_RGB", out var al) && Vec(al) is { } av: tint *= av; break;
      }
  }

  // ---- .mat graph helpers ----
  private static List<(string Graph, Dictionary<string, JsonElement> Params)> Graphs(Poe2Reader reader, string? mat)
  {
    var list = new List<(string, Dictionary<string, JsonElement>)>();
    if (string.IsNullOrEmpty(mat)) return list;
    JsonDocument doc;
    try { doc = reader.Json(mat); } catch (Exception x) when (x is IOException or JsonException) { return list; }
    if (!doc.RootElement.TryGetProperty("graphinstances", out var gi)) return list;
    foreach (var g in gi.EnumerateArray())
    {
      var parent = Path.GetFileNameWithoutExtension(Str(g, "parent") ?? "");
      var ps = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
      if (g.TryGetProperty("custom_parameters", out var cp))
        foreach (var p in cp.EnumerateArray())
          if (p.TryGetProperty("parameters", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
            ps[Str(p, "name") ?? ""] = arr[0].Clone();
      list.Add((parent, ps));
    }
    return list;
  }

  private static Dictionary<string, JsonElement>? Graph(List<(string Graph, Dictionary<string, JsonElement> Params)> g, string name) =>
    g.FirstOrDefault(x => x.Graph == name).Params;

  private static readonly string[] TexKeys = { "AlbedoTexture", "base_color_texture", "00. Texture", "Texture", "Mask Texture" };

  private static string? TextureOf(List<(string Graph, Dictionary<string, JsonElement> Params)> g)
  {
    string? fallback = null;
    foreach (var key in TexKeys)
      foreach (var (gn, ps) in g)
        foreach (var (k, v) in ps)
          if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("path", out var p) && p.GetString() is { Length: > 0 } path)
          {
            if (k.Contains(key, StringComparison.OrdinalIgnoreCase) && !gn.Contains("Grit", StringComparison.Ordinal) && !gn.Contains("Muddle", StringComparison.Ordinal)) return path;
            if (fallback == null && !path.Contains("noise", StringComparison.OrdinalIgnoreCase)) fallback = path;
          }
    return fallback;
  }

  private static string? Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
  private static float F(JsonElement e, string k, float d) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : d;
  private static bool B(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

  private static float Val(Dictionary<string, JsonElement> ps, string k, float d = 0) => ps.TryGetValue(k, out var v) ? Scalar(v).Curve.Max : d;

  private static Curve1 CurveOf(JsonElement e)
  {
    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("points", out var pts) && pts.GetArrayLength() > 0)
    {
      var flat = new List<float>();
      foreach (var p in pts.EnumerateArray()) { flat.Add(F(p, "time", 0)); flat.Add(F(p, "value", 0)); }
      return new Curve1(flat.ToArray());
    }
    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number) return new Curve1([0f, v.GetSingle()]);
    return new Curve1([0f, 0f]);
  }

  private static ScalarParam Scalar(JsonElement e)
  {
    var c = CurveOf(e);
    var var = e.ValueKind == JsonValueKind.Object ? F(e, "variance", 0) : 0;
    return new ScalarParam(c, new Vector2(-var, var));
  }

  private static Vector3? Vec(JsonElement e) =>
    e.ValueKind == JsonValueKind.Object && e.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() >= 3
      ? new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle()) : null;

  private static ColorCurve Colour(JsonElement c)
  {
    Curve1 Ch(string k) => c.TryGetProperty(k, out var x) ? CurveOf(x) : new Curve1([0f, 1f]);
    var r = Ch("R"); var g = Ch("G"); var b = Ch("B"); var a = Ch("A");
    var times = new SortedSet<float> { 0f, 1f };
    foreach (var k in new[] { "R", "G", "B", "A" })
      if (c.TryGetProperty(k, out var x) && x.TryGetProperty("points", out var pts))
        foreach (var p in pts.EnumerateArray()) times.Add(F(p, "time", 0));
    var flat = new List<float>();
    foreach (var t in times) flat.AddRange([t, Math.Clamp(a.Evaluate(t), 0, 1) * 255f, r.Evaluate(t) * 255f, g.Evaluate(t) * 255f, b.Evaluate(t) * 255f]);
    return new ColorCurve(flat.ToArray());
  }

  private static ColorCurve Tinted(ColorCurve c, Vector3 tint)
  {
    if (tint == Vector3.One) return c;
    var flat = new List<float>();
    for (var i = 0; i <= 16; i++)
    {
      var t = i / 16f;
      var v = c.Evaluate(t);
      flat.AddRange([t, v.W * 255f, v.X * tint.X * 255f, v.Y * tint.Y * 255f, v.Z * tint.Z * 255f]);
    }
    return new ColorCurve(flat.ToArray());
  }

  /// <summary>.trl curve string "n t0 v0 mode0 t1 v1 mode1 ..." (PROVEN format from trl_main.trl).</summary>
  private static Curve1 TrlCurve(string s)
  {
    var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var flat = new List<float>();
    for (var i = 1; i + 1 < p.Length; i += 3)
      if (float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) &&
          float.TryParse(p[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
      { flat.Add(t); flat.Add(v); }
    return flat.Count >= 2 ? new Curve1(flat.ToArray()) : new Curve1([0f, 1f]);
  }
}
