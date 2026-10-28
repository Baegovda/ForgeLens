using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary><c>blendType</c>: parsed by 0x141751cd0 / written by 0x141751dd0 as blend=0, add=1, blend_add=2.</summary>
public enum EffectBlend { Blend = 0, Add = 1, BlendAdd = 2 }

/// <summary>
///   Particle blend as REA'd (vfx3m). FUN_1417e1e50 packs per-emitter flags into PART_EMITTERS_SHARED_DATA row 3.x
///   (CPU table 0x14400c7ec, 0x60 B per emitter, uploaded by FUN_1418541b0 to t8,space2):
///   bit 0x20 = blendType==add (emitter+0x48c), bit 0x400 = !(isBlendAlphaMultiply (+0x490) &amp;&amp; blendType!=add).
///   particles.sdc PS 00000_0: rgb = col*vertexAlpha*(bit0x400 ? 1 : texAlpha'), outAlpha = bit0x20 ? 0 : vertexAlpha*texAlpha'.
///   Output alpha 0 for add implies a premultiplied ONE/INV_SRC_ALPHA PSO (capture-verified mtl1-23:
  ///   RenderDoc smoke 88302 / outdoor 137266 CPU particle draws, PS 101818 = One/InvSrcAlpha; see VfxBlendRules).
///   blend_add has no bit of its own, so it renders exactly like blend. Default ON since vfx3u (in-game frag grenade
///   reference: bright additive core/sparks only match with it); <c>SM2_VFX_BLEND_REA=0</c> restores the legacy blend.
/// </summary>
public static class EffectBlendRea
{
  public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_BLEND_REA") != "0";

  /// <summary>Flag word bits as written by FUN_1417e1e50 (only the two blend bits).</summary>
  public static uint Flags(EffectBlend blend, bool alphaMultiply)
    => (blend == EffectBlend.Add ? 0x20u : 0u) | (alphaMultiply && blend != EffectBlend.Add ? 0u : 0x400u);

  /// <summary>Premultiplied output (rgb factor on colour, output alpha) for vertex alpha <paramref name="va"/> and texture alpha <paramref name="ta"/>.</summary>
  public static (float Rgb, float Alpha) Output(uint flags, float va, float ta)
    => (va * ((flags & 0x400u) != 0 ? 1f : ta), (flags & 0x20u) != 0 ? 0f : va * ta);
}

/// <summary><c>sorting</c> @0x143645dd8: parsed by 0x1417534f0 as Camera Distance=0, Spawn Order=1, Spawn Order Reversed=2.</summary>
public enum EffectSorting { CameraDistance = 0, SpawnOrder = 1, SpawnOrderReversed = 2 }

/// <summary><c>emission.type</c> union: constantFlow @0x143644cf8, explosion @0x143644d18, dynamicFlow @0x143644d08.</summary>
public enum EmissionKind { ConstantFlow, Explosion, DynamicFlow }

public enum FrameMode { None, Constant, AgeSpline }

/// <summary>Where a sprite emitter draws. <c>texture.animIn</c> union <c>channel</c> = "DISTORTION" (490 in the corpus;
/// channel strings DIFFUSE/ALPHA_MASK/SELF_DISTORTION/GLOSS/DISTORTION near 0x1436442d0) draws only into the
/// refraction buffer; <c>drawInSimulationTex = drawInSimulationEnabled</c> draws into the wind/water simulation
/// textures (<c>simSystems.drawInWindSim/drawInWaterSim</c>), not the colour buffer.</summary>
public enum DrawTarget { Color, Distortion, Simulation }

/// <summary>When an attached (child) emitter fires: <c>genOnFly</c> @0x143644b38 (while the parent particle lives),
/// <c>genOnCDT</c> @0x143644b48 (parent particle hits collision), <c>genOnDie</c> @0x143644b58 (parent particle dies).
/// Listed under the emitter's <c>attached</c> @0x143644cd0 block as <c>{ key = { name, numParts @0x143644b18 } }</c>;
/// "Emitter `%s` can not be attached to emitter `%s` because it has LCS render mode." @0x143648810.</summary>
public enum AttachEvent { Fly, Cdt, Die }

/// <summary>
///   <c>optimization.degNear</c> / <c>degFar</c>: by camera distance, size / intensity / alpha / distortion scale go from 1
///   at <see cref="RadStart"/> to the <c>scale*</c> value at <see cref="RadEnd"/> (near: start 1.8 > end 0.9 fades
///   particles in the camera's face; far: 40 -> 60 fades them out). Linear in distance; unlisted scales stay 1.
/// </summary>
public sealed record DistanceFade(float RadStart, float RadEnd, float Size = 1, float Intens = 1, float Alpha = 1, float Disp = 1)
{
  public float Amount(float distance)
  {
    var span = RadEnd - RadStart;
    return MathF.Abs(span) < 1e-5f ? (span >= 0 ? (distance >= RadEnd ? 1 : 0) : (distance <= RadEnd ? 1 : 0)) : Math.Clamp((distance - RadStart) / span, 0f, 1f);
  }

  public static DistanceFade? Parse(System.Text.Json.Nodes.JsonObject? o)
  {
    if (o is null || o["radStart"] is null || o["radEnd"] is null) return null;
    return new DistanceFade(o.Float("radStart", 0), o.Float("radEnd", 0), o.Float("scaleSize", 1), o.Float("scaleIntens", 1), o.Float("scaleAlpha", 1), o.Float("scaleDisp", 1));
  }
}

/// <summary>A child emitter fired per parent particle.</summary>
public sealed record AttachedEmitter(AttachEvent Event, string Name, float NumParts)
{
  /// <summary>Index into <see cref="EffectDefinition.Emitters"/> (resolved within the same part), -1 if missing.</summary>
  public int Index { get; set; } = -1;
}

/// <summary>One CPU_EMITTER / GPU_EMITTER after union resolution, with the transforms of its part and groups folded in.</summary>
public sealed class EmitterDef
{
  public required string Name { get; init; }
  /// <summary>Path through the effect graph, e.g. <c>part_mzl_bolter/flame_circle</c>.</summary>
  public required string Path { get; init; }
  public bool IsGpu { get; init; }
  /// <summary><c>typeParams</c> union member: <c>default</c> (sprites), <c>lwiGeom</c> (mesh particles), ...</summary>
  public string ParticleType { get; init; } = "default";
  public bool IsSprite => ParticleType is "default" or "distortion";
  /// <summary>
  ///   Screen-space refraction source: a normal map (RG = offset) scaled by <see cref="DistortPower"/> over age.
  ///   From <c>typeParams.distortion</c> (<c>visuals.texture</c> + <c>visuals.power</c>, refraction only), a texture whose
  ///   <c>animIn.channel = DISTORTION</c> (refraction only), or <c>visuals.sdr</c> with its texture on channel
  ///   <c>DISTORTION</c> (the sprite draws colour and also refracts). <c>SELF_DISTORTION</c> sdr warps the sprite's own UVs instead.
  /// </summary>
  public string? DistortTexture { get; init; }
  public ScalarParam DistortPower { get; init; } = ScalarParam.Constant(1);
  /// <summary><c>sdr</c> on channel <c>SELF_DISTORTION</c>: normal map that warps the sprite's own texture lookup.</summary>
  public string? SelfDistortTexture { get; init; }
  public ScalarParam SelfDistortPower { get; init; } = ScalarParam.Constant(0);
  public Vector2 SelfDistortSpeed { get; init; }
  public DistanceFade? DegNear { get; init; }
  public DistanceFade? DegFar { get; init; }
  /// <summary>Mesh particles (<c>typeParams.lwiGeom</c>).</summary>
  public MeshParticleDef? Geom { get; init; }
  public bool Visible { get; init; } = true;
  public string NameParent { get; init; } = "";
  public Matrix4x4 Transform { get; init; } = Matrix4x4.Identity;
  /// <summary><c>emitScale</c> @0x143645da0 times the part's <c>scale</c>: scales sizes, speeds and offsets.</summary>
  public float Scale { get; init; } = 1f;
  public float TimeScale { get; init; } = 1f;
  public EffectSorting Sorting { get; init; }
  public float PrewarmTime { get; init; }
  /// <summary>Emission window in emitter seconds (the part's <c>timeLife</c>); 0 = endless.</summary>
  public float EmitDuration { get; init; }

  public EmissionKind Emission { get; init; }
  public Vector2 Rate { get; init; }
  public Vector2 BurstCount { get; init; }
  public float BurstDelay { get; init; }
  public Curve1 DynamicRate { get; init; } = Curve1.Zero;
  public bool DynamicCycled { get; init; }
  public int MaxParticles { get; init; }
  /// <summary><c>angleEmission</c>: half-angle of the emission cone in degrees (180 = sphere; sparks use 180).</summary>
  public float AngleEmission { get; init; }
  public Vector3 EmitDir { get; init; } = Vector3.UnitY;
  /// <summary>
  ///   No <c>emitDir</c> (point source, <c>parentArea.direction = isEmitByNormal</c>): emit along the parent's normal.
  ///   On a bone socket that is the bone's +Z (wpn tip: barrel axis; part_mzl_bolter's flame_front_01 has no emitDir
  ///   while its sibling flame_circle_2 says z+ and flame_cross_6/8 sit 0.25 behind the tip on the brake vents, x+/x-);
  ///   unparented it stays the ground normal +Y.
  /// </summary>
  public bool EmitByNormal { get; init; }
  public ScalarParam SpeedNorm { get; init; } = ScalarParam.Constant(0);
  public ScalarParam SpeedTang { get; init; } = ScalarParam.Constant(0);
  public float InitialOffset { get; init; }
  public float InitialOffsetDisp { get; init; }
  public float RandomOffsetRadius { get; init; }
  public Vector3 RandomOffsetWeights { get; init; } = Vector3.One;
  public string Source { get; init; } = "point";

  public Vector2 Life { get; init; } = new(1, 1);
  /// <summary>Particle radius (half extent) over normalized age; <c>Mesh.Vertex*</c> outlines live in [-1,1].</summary>
  public ScalarParam Size { get; init; } = ScalarParam.Constant(0.1f);
  public bool AlignSpeed { get; init; }
  public DrawTarget Target { get; init; }
  /// <summary>Children fired per particle (<c>attached.genOnFly/genOnCDT/genOnDie</c>).</summary>
  public List<AttachedEmitter> Attached { get; } = [];
  /// <summary>True when another emitter lists this one under <c>attached</c>: it then emits only from parent particles.</summary>
  public bool IsAttachedChild { get; set; }
  /// <summary><c>particleParams.inherit.speedInherit {x,y}</c> @0x143644878: random fraction of the parent particle's velocity.</summary>
  public Vector2 SpeedInherit { get; init; }
  public Vector2 RatioXY { get; init; } = Vector2.One;
  public string RatioDependency { get; init; } = "none";
  /// <summary>
  ///   GPU <c>visuals.transforms.streaks</c> {bool isEnabled @+0x178, float ratio @+0x17c}. The GPU visuals constructor
  ///   FUN_1417c6290 sets isEnabled=true, ratio=0.3 and the reader (FUN_1417c7720 -> FUN_1417c0eb0) only overwrites keys
  ///   that are present (Mod Tools meta_desc partEMITTER_GPU_STREAKS agrees: True / 0.3). CPU emitters have no streaks.
  /// </summary>
  public float StreakRatio { get; init; }

  public string? Texture { get; init; }
  public EffectBlend Blend { get; init; }
  /// <summary>
  ///   <c>visuals.isBlendAlphaMultiply</c> (emitter desc +0x490, read by FUN_1417e1e50). Absent means true: the visuals
  ///   constructor FUN_14178cbf0 writes 1 at +0xe8 and the reader FUN_14178f3b0 only overwrites present keys
  ///   (Mod Tools meta_desc __defaultValue = True).
  /// </summary>
  public bool BlendAlphaMultiply { get; init; } = true;
  public ColorCurve Color { get; init; } = ColorCurve.White;
  public ScalarParam ColorIntensity { get; init; } = ScalarParam.Constant(1);
  public float AlphaIntensity { get; init; } = 1f;
  /// <summary>
  ///   <c>visuals.textureTintControl</c> (0..1, @0x143645278, typeParams+0x140): how much of the texture's own RGB tints
  ///   the particle. 0 uses the texture as intensity only (luminance), so channel-packed flipbooks such as
  ///   <c>seq_fin_expl_g</c> (blue fire mask in B) draw grey/white smoke tinted by <see cref="Color"/>, not blue.
  ///   Absent means 0: the visuals constructor FUN_14178cbf0 zeroes +0x140 and the reader FUN_14178f3b0 only overwrites
  ///   present keys (Mod Tools meta_desc __defaultValue = 0). The luminance/colour mix itself is still a shader heuristic.
  /// </summary>
  public float TextureTint { get; init; }
  /// <summary>
  ///   <c>visuals.additionalContrast</c> (string @0x143645248, float at typeParams+0x12C, serialized at 0x14178d437):
  ///   extra brightness on texels close to <see cref="ContrastColorKey"/>. The particle PS (particles.sdc 00114_2) does
  ///   <c>boost = 1 + additionalContrast * saturate(1 - length(key/255 - tex.rgb))</c>. 0 = off (absent).
  /// </summary>
  public float AdditionalContrast { get; init; }
  /// <summary>
  ///   <c>visuals.contrastColorKey</c> [A,R,G,B] (string @0x143645260, colour at typeParams+0x130, serializer 0x1427456a0
  ///   called at 0x14178d4da): key colour as raw 0..255 RGB (alpha dropped; the shader reads shared[1].rgb only).
  /// </summary>
  public Vector3 ContrastColorKey { get; init; }

  /// <summary>[A,R,G,B] (or a constExpr/value union of it) to raw RGB 0..255; anything else is black.</summary>
  public static Vector3 ContrastKey(System.Text.Json.Nodes.JsonNode? node)
  {
    if (node is null) return Vector3.Zero;
    var body = SslRead.UnionKind(node) is { } k ? node.Get(k) : node;
    var f = SslRead.Floats(body);
    return f.Length >= 4 ? new Vector3(f[1], f[2], f[3]) : f.Length == 3 ? new Vector3(f[0], f[1], f[2]) : Vector3.Zero;
  }
  public ScalarParam AlphaClip { get; init; } = ScalarParam.Constant(0);
  /// <summary><c>visuals.alphaMask.clip.power</c> (mask desc +0x88; softClip +0xa0, clipEdgeRadius +0xa8; parser FUN_141782a60).
  /// Age-driven dissolve threshold of the mask, e.g. chainsaw blood cpu_drops_3 0→0.6 over life. Null when absent/disabled.</summary>
  public ScalarParam? MaskClip { get; init; }
  /// <summary>SM2_VFX_MASK_CLIP=1 enables using <see cref="MaskClip"/> as the sprite clip threshold.
  /// HEURISTIC: the consumer/shader math was not traced; we reuse the alphaClip path (clip applied after the mask multiply).</summary>
  public static bool MaskClipEnabled { get; set; } = Environment.GetEnvironmentVariable("SM2_VFX_MASK_CLIP") == "1";
  public float SoftDepth { get; init; }
  /// <summary><c>visuals.alphaMask</c> (enabled): second texture whose alpha multiplies the sprite, sampled over the whole quad
  /// (not the flipbook cell) and rotated by a random angle from <c>tcIn.transform.rotation{x,y}</c> degrees.</summary>
  public string? AlphaMask { get; init; }
  public Vector2 AlphaMaskRotation { get; init; }
  public string Lighting { get; init; } = "None";
  public float AmbientLight { get; init; } = 1f;
  public Vector2 LocalOffset { get; init; }
  public FrameMode FrameMode { get; init; }
  public Vector2 FrameRate { get; init; }
  public Vector2 FrameOffset { get; init; }
  public ScalarParam FrameSpline { get; init; } = ScalarParam.Constant(0);
  public bool FrameCycle { get; init; }
  public bool KillAfterSequence { get; init; }
  public bool RandomFlip { get; init; }
  public Vector2 RotationAngle { get; init; }
  public Vector2 RotationSpeed { get; init; }

  public Vector3 Gravity { get; init; }
  public float Friction { get; init; }
  public bool Collide { get; init; }
  public float Resilience { get; init; }
  public bool KillOnCollide { get; init; }
}

/// <summary><c>sfx_light</c>: a point light animated over <c>AnimationParams.animationTime</c>.</summary>
public sealed class LightDef
{
  public required string Name { get; init; }
  public required string Path { get; init; }
  public string NameParent { get; init; } = "";
  public Matrix4x4 Transform { get; init; } = Matrix4x4.Identity;
  public float Duration { get; init; } = 0.1f;
  public Curve1 Radius { get; init; } = Curve1.One;
  public Curve1 Intensity { get; init; } = Curve1.One;
  public ColorCurve Color { get; init; } = ColorCurve.White;
}

/// <summary>Effect ready to simulate: every emitter and light, plus what was parsed but is not drawn yet.</summary>
public sealed class EffectDefinition
{
  public required string Name { get; init; }
  public List<EmitterDef> Emitters { get; } = [];
  public List<LightDef> Lights { get; } = [];
  public List<TrailDef> Trails { get; } = [];
  /// <summary>Every drawable render state: sprite emitters and trail ribbons.</summary>
  public IEnumerable<EmitterDef> Drawables => Emitters.Where(e => e.IsSprite).Concat(Trails.Select(t => t.Render));
  public IEnumerable<string?> Textures => Emitters.Select(e => e.Texture).Concat(Emitters.Select(e => e.AlphaMask)).Concat(Trails.Select(t => t.Render.Texture));
  /// <summary>Nodes kept out of the simulation (trails, mesh particles, ...): path and type.</summary>
  public List<(string Path, string Type)> Unsupported { get; } = [];
  /// <summary>
  ///   <c>em_covering_*</c> stubs: no particle type or visuals, only <c>physics.cdt.coveringInteraction.radiusScale</c>.
  ///   They tell the engine to paint the covering layer (blood, water, ...: the <c>coveringMask</c> material channel) of
  ///   the surfaces around the effect. Not a particle; the preview lists them instead of drawing.
  /// </summary>
  public List<CoveringDef> Coverings { get; } = [];
  public bool Loops => Trails.Count > 0 || Emitters.Any(e => e.Emission != EmissionKind.Explosion && e.EmitDuration <= 0);

  /// <summary>Seconds until the last particle of a non-looping effect dies (or a preview window for loops).</summary>
  public float Duration
  {
    get
    {
      var d = 0.25f;
      foreach (var e in Emitters)
      {
        var ts = Math.Max(0.01f, e.TimeScale);
        var emit = e.Emission switch
        {
          EmissionKind.Explosion => e.BurstDelay,
          EmissionKind.DynamicFlow => e.EmitDuration > 0 ? e.EmitDuration : e.DynamicRate.EndTime,
          _ => e.EmitDuration > 0 ? e.EmitDuration : 3f,
        };
        d = Math.Max(d, (emit + e.Life.Y) / ts);
      }

      foreach (var l in Lights)
        d = Math.Max(d, l.Duration);
      if (Trails.Count > 0) d = Math.Max(d, 2f);
      return Math.Min(d, 30f);
    }
  }

  public static EffectDefinition Build(string name, JsonObject resolved)
  {
    var def = new EffectDefinition { Name = name };
    Walk(def, resolved, name, Matrix4x4.Identity, "", 1f);
    return def;
  }

  private static void Walk(EffectDefinition def, JsonObject node, string path, Matrix4x4 parent, string parentName, float scale)
  {
    var type = node.Str("__type") ?? "";
    var local = node.ContainsKey("transform") ? SslRead.Matrix(node["transform"]) : Matrix4x4.Identity;
    var world = local * parent;
    var nameParent = node.Str("nameParent") is { Length: > 0 } np ? np : parentName;
    switch (type)
    {
      case "sfx_group":
        if (node["list"] is JsonObject list)
          foreach (var (key, child) in list)
            if (child is JsonObject c)
              Walk(def, c, path + "/" + key, world, nameParent, scale * node.Float("scale", 1f));
        break;
      case "sfx_part":
      {
        var partScale = scale * node.Float("scale", 1f);
        var duration = node.Float("timeLife", 0f);
        var partTimeScale = node.Float("emitTimeScale", 1f);
        var first = def.Emitters.Count;
        foreach (var key in new[] { "emitters2", "emitters" })
          if (node[key] is JsonObject emitters)
            foreach (var (en, e) in emitters)
              if (e is JsonObject eo)
              {
                var et = eo.Str("__type") ?? "";
                // Untyped entries (5499 in the corpus, 89% isVisible=False) are CPU emitters when they are still in the
                // editor graph (<c>__node</c>); untyped entries without a node are orphans and are not emitted.
                if (et.Length == 0 && eo.ContainsKey("emission")) et = eo.ContainsKey("__node") ? "CPU_EMITTER" : "orphan";
                if (CoveringDef.TryParse(eo, en, path + "/" + en, nameParent) is { } covering)
                  def.Coverings.Add(covering);
                else if (et is "CPU_EMITTER" or "GPU_EMITTER")
                  def.Emitters.Add(Emitter(eo, en, path + "/" + en, world, nameParent, partScale, duration, partTimeScale));
                else if (eo.Count > 0)
                  def.Unsupported.Add((path + "/" + en, et.Length > 0 ? et : "emitter?"));
              }

        for (var i = first; i < def.Emitters.Count; i++)
          foreach (var a in def.Emitters[i].Attached)
          {
            a.Index = def.Emitters.FindIndex(first, x => x.Name == a.Name);
            if (a.Index >= 0 && a.Index != i) def.Emitters[a.Index].IsAttachedChild = true;
            else a.Index = -1;
          }

        break;
      }
      case "sfx_light":
      case "sfx_light_base":
      {
        var anim = node.Obj("AnimationParams");
        var color = node.Obj("ColorCtrl");
        var colorCurve = color?.Str("__type") == "anim_const_color_ctrl" && SslRead.Floats(color["value"]) is { Length: 4 } v
          ? new ColorCurve([0f, v[0], v[1], v[2], v[3]])
          : SslRead.Color(color?["color"] ?? color?["spline"]);
        def.Lights.Add(new LightDef
        {
          Name = path.Split('/')[^1],
          Path = path,
          NameParent = nameParent,
          Transform = world,
          Duration = Math.Max(0.016f, anim.Float("animationTime", 0.1f)),
          Radius = Spline(node.Obj("RadiusCtrl"), 1f),
          Intensity = Spline(node.Obj("Intensity"), 1f),
          Color = colorCurve,
        });
        break;
      }
      case "sfx_trail":
        def.Trails.Add(TrailDef.Parse(node, path, world, nameParent, scale * node.Float("scale", 1f)));
        break;
      default:
        def.Unsupported.Add((path, type.Length > 0 ? type : "?"));
        break;
    }
  }

  private static Curve1 Spline(JsonObject? ctrl, float fallback)
  {
    var f = SslRead.Floats(ctrl?["spline"]);
    if (f.Length >= 2) return new Curve1(f);
    return SslRead.Num(ctrl?["value"]) is { } v ? new Curve1([0f, v]) : new Curve1([0f, fallback]);
  }

  private static Vector3 Axis(string? dir, JsonNode? custom) => dir switch
  {
    "x+" => Vector3.UnitX, "x-" => -Vector3.UnitX,
    "y-" => -Vector3.UnitY, "z+" => Vector3.UnitZ, "z-" => -Vector3.UnitZ,
    "custom" when SslRead.Vec3(custom, Vector3.UnitY) is { } c && c.LengthSquared() > 1e-8f => Vector3.Normalize(c),
    _ => Vector3.UnitY,
  };

  private static EmitterDef Emitter(JsonObject e, string name, string path, Matrix4x4 parent, string parentName, float partScale, float duration, float partTimeScale)
  {
    var gpu = e.Str("__type") == "GPU_EMITTER";
    var emission = e.Obj("emission");
    var typeNode = emission.Obj("type");
    var kind = SslRead.UnionKind(typeNode) ?? "constantFlow";
    var typeBody = typeNode.Get(kind);
    var source = emission.Obj("source");
    var sourceKind = SslRead.UnionKind(source) ?? "point";
    var sourceBody = source.Obj(sourceKind);
    var dirNode = sourceBody.Get("emitDir");
    string? dirName = dirNode is JsonObject ? dirNode.Obj("emitDir").Str("emitDir") ?? dirNode.Str("emitDir") : sourceBody.Str("emitDir");
    if (sourceKind == "parentArea")
      dirName = sourceBody.Obj("direction")?.Obj("emitDir")?.Str("emitDir") ?? dirName;
    var custom = sourceBody.Obj("emitType")?["customDir"];

    JsonObject? tp;
    string particleType;
    JsonObject? visuals, transforms;
    float softDepth;
    if (gpu)
    {
      particleType = "default";
      tp = e;
      visuals = e.Obj("visuals");
      transforms = visuals.Obj("transforms");
      softDepth = visuals.Float("softDepth", 0f);
    }
    else
    {
      var typeParams = e.Obj("typeParams");
      particleType = SslRead.UnionKind(typeParams) ?? "default";
      tp = typeParams.Obj(particleType);
      visuals = tp.Obj("visuals");
      transforms = tp.Obj("transforms");
      softDepth = tp.Float("softDepth", 0f);
    }

    var texture = visuals.Obj("texture");
    var anim = texture.Obj("animIn");
    var sdr = visuals.Obj("sdr") is { } so && so.Bool("isEnabled", false) ? so : null;
    var sdrChannel = sdr?.Obj("texture")?.Obj("animIn")?.Str("channel");
    var channelDistort = anim is not null && SslRead.UnionKind(anim) == "channel" && anim.Obj("channel").Str("channel") == "DISTORTION";
    string? distortTex = particleType == "distortion" || channelDistort ? texture.Str("nameTex")
      : sdrChannel == "DISTORTION" ? sdr!.Obj("texture").Str("nameTex") : null;
    var distortPower = particleType == "distortion" ? SslRead.Scalar(visuals.Get("power"), 1f)
      : sdrChannel == "DISTORTION" ? SslRead.Scalar(sdr!.Get("power"), 1f) : ScalarParam.Constant(1f);
    var animBody = anim.Obj(SslRead.UnionKind(anim) ?? "params");
    var speedFrame = animBody.Obj("speedFrame");
    var frameKind = SslRead.UnionKind(speedFrame);
    var frameBody = frameKind is null ? null : speedFrame.Get(frameKind);
    var particle = e.Obj("particleParams");
    var sizeNode = particle.Obj("size");
    var sizeKind = SslRead.UnionKind(sizeNode) ?? "ageSize";
    var size = sizeKind == "constExprRange"
      ? ScalarParam.Range(SslRead.Range(sizeNode.Get("constExprRange"), new Vector2(0.1f)).X, SslRead.Range(sizeNode.Get("constExprRange"), new Vector2(0.1f)).Y)
      : SslRead.Scalar(sizeNode, 0.1f);
    var physics = e.Obj("physics");
    var cdt = physics.Obj("cdt");
    var ratio = transforms.Obj("ratioDependency");
    var rotation = transforms.Obj("rotation");
    var blend = visuals.Str("blendType") switch { "add" => EffectBlend.Add, "blend_add" => EffectBlend.BlendAdd, _ => EffectBlend.Blend };
    var lighting = visuals.Get("lighting") is JsonObject lo ? (lo.Bool("isEnabled", false) ? "Lit" : "None") : visuals.Str("lighting") ?? "None";
    var local = e.ContainsKey("transform") ? SslRead.Matrix(e["transform"]) : Matrix4x4.Identity;
    var maxParticles = (int)emission.Float("maxParticles", 0);

    var result = new EmitterDef
    {
      Name = name,
      Path = path,
      IsGpu = gpu,
      ParticleType = particleType,
      Geom = particleType == "lwiGeom" ? MeshParticleDef.Parse(tp) : null,
      Visible = e.Bool("isVisible", true),
      NameParent = e.Str("nameParent") is { Length: > 0 } np ? np : parentName,
      Transform = local * parent,
      Scale = partScale * e.Float("emitScale", 1f),
      TimeScale = Math.Max(0f, e.Float("timeScale", 1f) * partTimeScale),
      Sorting = e.Str("sorting") switch { "Spawn Order" => EffectSorting.SpawnOrder, "Spawn Order Reversed" => EffectSorting.SpawnOrderReversed, _ => EffectSorting.CameraDistance },
      PrewarmTime = e.Float("prewarmTime", 0f),
      EmitDuration = duration,
      Emission = kind switch { "explosion" => EmissionKind.Explosion, "dynamicFlow" => EmissionKind.DynamicFlow, _ => EmissionKind.ConstantFlow },
      Rate = SslRead.Range(typeBody, Vector2.Zero),
      BurstCount = SslRead.Range(typeBody.Get("numPartsToEmit"), Vector2.One),
      BurstDelay = typeBody.Float("emitDelay", 0f),
      DynamicRate = SslRead.Floats(typeBody.Get("intensitySpline")) is { Length: >= 2 } ds ? new Curve1(ds) : Curve1.Zero,
      DynamicCycled = typeBody.Bool("isCycled", false),
      MaxParticles = maxParticles,
      AngleEmission = emission.Float("angleEmission", 0f),
      EmitDir = Axis(dirName, custom),
      EmitByNormal = dirName is null,
      SpeedNorm = SslRead.Scalar(emission.Get("speedNorm"), 0f),
      SpeedTang = SslRead.Scalar(emission.Get("speedTang"), 0f),
      InitialOffset = emission.Float("initialOffset", 0f),
      InitialOffsetDisp = emission.Float("initialOffsetDisp", 0f),
      RandomOffsetRadius = emission.Float("randomOffsetRadius", 0f),
      RandomOffsetWeights = SslRead.Vec3(emission.Get("randomOffsetWeights"), Vector3.One),
      Source = sourceKind,
      Life = SslRead.Range(particle.Get("timeLife"), Vector2.One),
      Size = size,
      AlignSpeed = particle.Bool("isAlignSpeed", false),
      RatioXY = ratio is null ? Vector2.One : SslRead.Range(ratio.Get("ratioXY") is JsonObject ro && SslRead.UnionKind(ro) is { } rk ? ro.Get(rk) : ratio.Get("ratioXY"), Vector2.One),
      RatioDependency = ratio.Str("dependencyType") ?? "none",
      StreakRatio = !gpu ? 0f : transforms.Obj("streaks") is not { } st ? 0.3f : st.Bool("isEnabled", true) ? st.Float("ratio", 0.3f) : 0f,
      Texture = texture.Str("nameTex"),
      Blend = blend,
      BlendAlphaMultiply = visuals.Bool("isBlendAlphaMultiply", true),
      Color = SslRead.Color(visuals.Get("color")),
      ColorIntensity = SslRead.Scalar(visuals.Get("colorIntensity"), 1f),
      AlphaIntensity = SslRead.Scalar(visuals.Get("alphaIntensity"), 1f).Sample(0, 0.5f),
      TextureTint = Math.Clamp(visuals.Float("textureTintControl", 0f), 0f, 1f),
      AdditionalContrast = Math.Max(0f, visuals.Float("additionalContrast", 0f)),
      ContrastColorKey = EmitterDef.ContrastKey(visuals.Get("contrastColorKey")),
      AlphaClip = SslRead.Scalar(visuals.Get("alphaClip"), 0f),
      MaskClip = visuals.Obj("alphaMask") is { } amc && (amc.Bool("isEnabled", false) || amc.Str("mode") == "Enabled") && amc.Obj("clip")?.Get("power") is { } mcp ? SslRead.Scalar(mcp, 0f) : null,
      SpeedInherit = SslRead.Range(particle.Obj("inherit")?.Get("speedInherit"), Vector2.Zero),
      SoftDepth = softDepth,
      Target = SslRead.UnionKind(tp.Obj("drawInSimulationTex")) == "drawInSimulationEnabled" ? DrawTarget.Simulation
        : particleType == "distortion" || channelDistort ? DrawTarget.Distortion
        : DrawTarget.Color,
      DistortTexture = distortTex,
      DistortPower = distortPower,
      SelfDistortTexture = sdrChannel == "SELF_DISTORTION" ? sdr!.Obj("texture").Str("nameTex") : null,
      SelfDistortPower = sdrChannel == "SELF_DISTORTION" ? SslRead.Scalar(sdr!.Get("power"), 0.1f) : ScalarParam.Constant(0),
      DegNear = DistanceFade.Parse(e.Obj("optimization")?.Obj("degNear")),
      DegFar = DistanceFade.Parse(e.Obj("optimization")?.Obj("degFar")),
      SelfDistortSpeed = sdrChannel == "SELF_DISTORTION" && sdr!.Obj("texture")?.Obj("tcIn")?.Obj("transform")?.Get("speed") is { } sps ? SslRead.Range(sps, Vector2.Zero) : Vector2.Zero,
      AlphaMask = visuals.Obj("alphaMask") is { } am && (am.Bool("isEnabled", false) || am.Str("mode") == "Enabled") ? am.Obj("texture").Str("nameTex") : null,
      AlphaMaskRotation = visuals.Obj("alphaMask")?.Obj("texture")?.Obj("tcIn") is { } tci && SslRead.UnionKind(tci) is { } tk && tci.Obj(tk)?.Get("rotation") is { } rot ? SslRead.Range(rot, Vector2.Zero) : Vector2.Zero,
      Lighting = lighting,
      AmbientLight = visuals.Float("ambientLight", 1f),
      LocalOffset = new Vector2(visuals.Float("localOffsetX", 0f), visuals.Float("localOffsetY", 0f)),
      FrameMode = frameKind switch { "constExpr" => FrameMode.Constant, "ageSpline" => FrameMode.AgeSpline, _ => FrameMode.None },
      FrameRate = SslRead.Range(frameBody.Get("framerate"), Vector2.Zero),
      FrameOffset = SslRead.Range(frameBody.Get("frameOffset"), Vector2.Zero),
      FrameSpline = frameKind == "ageSpline" ? new ScalarParam(new Curve1(SslRead.Floats(frameBody.Get("spline")) is { Length: >= 2 } fs ? fs : [0f, 0f]), SslRead.Range(frameBody.Get("disp"), Vector2.Zero)) : ScalarParam.Constant(0),
      FrameCycle = animBody.Bool("isFrameCycle", false),
      KillAfterSequence = frameBody.Bool("killAfterPlayingSequence", false),
      RandomFlip = transforms.Bool("isRandomFlip", false),
      RotationAngle = SslRead.Range(rotation.Get("angle"), Vector2.Zero),
      RotationSpeed = SslRead.Range(rotation.Get("speed"), Vector2.Zero),
      Gravity = SslRead.Vec3(physics.Get("gravity"), Vector3.Zero),
      Friction = physics.Float("friction", 0f),
      Collide = cdt.Bool("enable", false),
      Resilience = cdt.Float("resilience", 0.2f),
      KillOnCollide = cdt.Bool("killOnCDT", false),
    };
    if (e.Obj("attached") is { } attached)
      foreach (var (key, ev) in new[] { ("genOnFly", AttachEvent.Fly), ("genOnCDT", AttachEvent.Cdt), ("genOnDie", AttachEvent.Die) })
        if (attached.Obj(key) is { } entries)
          foreach (var (_, entry) in entries)
            if (entry is JsonObject eo && eo.Str("name") is { Length: > 0 } target)
              result.Attached.Add(new AttachedEmitter(ev, target, eo.Float("numParts", 1f)));
    return result;
  }
}

/// <summary>Covering interaction stub (see <see cref="EffectDefinition.Coverings"/>).</summary>
public sealed record CoveringDef(string Path, string Kind, float RadiusScale, bool Visible, string NameParent)
{
  public static CoveringDef? TryParse(JsonObject node, string name, string path, string nameParent)
  {
    if (node.ContainsKey("typeParams") || node.ContainsKey("visuals") || node.ContainsKey("emission"))
      return null;
    var interaction = node.Obj("physics").Obj("cdt").Obj("coveringInteraction");
    if (interaction is null && !name.StartsWith("em_covering", StringComparison.OrdinalIgnoreCase))
      return null;
    var kind = name.StartsWith("em_covering_", StringComparison.OrdinalIgnoreCase) ? name[12..] : "covering";
    return new CoveringDef(path, kind, interaction.Float("radiusScale", 1f), node.Bool("isVisible", true), nameParent);
  }
}
