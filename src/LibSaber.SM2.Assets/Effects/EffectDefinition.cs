using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary><c>blendType</c>: parsed by 0x141751cd0 / written by 0x141751dd0 as blend=0, add=1, blend_add=2.</summary>
public enum EffectBlend { Blend = 0, Add = 1, BlendAdd = 2 }

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
  public bool IsSprite => ParticleType == "default";
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
  public float StreakRatio { get; init; }

  public string? Texture { get; init; }
  public EffectBlend Blend { get; init; }
  public ColorCurve Color { get; init; } = ColorCurve.White;
  public ScalarParam ColorIntensity { get; init; } = ScalarParam.Constant(1);
  public float AlphaIntensity { get; init; } = 1f;
  public ScalarParam AlphaClip { get; init; } = ScalarParam.Constant(0);
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
  /// <summary>Nodes kept out of the simulation (trails, mesh particles, ...): path and type.</summary>
  public List<(string Path, string Type)> Unsupported { get; } = [];
  public bool Loops => Emitters.Any(e => e.Emission != EmissionKind.Explosion && e.EmitDuration <= 0);

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
                if (et is "CPU_EMITTER" or "GPU_EMITTER")
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
      StreakRatio = transforms.Obj("streaks") is { } st && st.Bool("isEnabled", false) ? st.Float("ratio", 0f) : 0f,
      Texture = texture.Str("nameTex"),
      Blend = blend,
      Color = SslRead.Color(visuals.Get("color")),
      ColorIntensity = SslRead.Scalar(visuals.Get("colorIntensity"), 1f),
      AlphaIntensity = SslRead.Scalar(visuals.Get("alphaIntensity"), 1f).Sample(0, 0.5f),
      AlphaClip = SslRead.Scalar(visuals.Get("alphaClip"), 0f),
      SpeedInherit = SslRead.Range(particle.Obj("inherit")?.Get("speedInherit"), Vector2.Zero),
      SoftDepth = softDepth,
      Target = SslRead.UnionKind(tp.Obj("drawInSimulationTex")) == "drawInSimulationEnabled" ? DrawTarget.Simulation
        : anim is not null && SslRead.UnionKind(anim) == "channel" && anim.Obj("channel").Str("channel") == "DISTORTION" ? DrawTarget.Distortion
        : DrawTarget.Color,
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
