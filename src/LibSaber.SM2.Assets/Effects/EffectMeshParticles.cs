using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   <c>typeParams.lwiGeom</c> (@0x143644870, block parsed 0x141760a45..0x141762f4d): particles drawn as instances of
///   a geometry template. <c>nameGeomTpl</c> @0x1436447b0 names the .tpl, <c>typeInst</c> @0x1436447e8 picks which
///   of its sub-objects (in node order) each particle uses (<c>enumeration.typeInstEnum</c> or a random
///   <c>range.typeInstRange</c>; <c>default</c> = 0), <c>transforms.initRotAxises.random{x,y,z}</c> spins the
///   instance randomly about those local axes, and <c>sfxMtlOverrides</c> @0x143644810 animate the material over
///   particle age: <c>color</c>, <c>emissiveIntensity</c>, <c>colorTransparency</c> @0x143644700 (spline or const).
/// </summary>
public sealed class MeshParticleDef
{
  public required string Template { get; init; }
  public string? SubmaterialOverride { get; init; }
  public int InstanceMin { get; init; }
  public int InstanceMax { get; init; }
  public Vector3 RandomAxes { get; init; }
  public bool RotationFromEmitter { get; init; }
  /// <summary>Raw key values in file order. Unlike sprite splines (A,R,G,B) these are R,G,B,A: the bolter flesh
  /// splash <c>constExpr [255,44,3,155]</c> is blood red, and read as A,R,G,B it would be blue.</summary>
  public ColorCurve? Color { get; init; }
  public Vector4 ColorAt(float n)
  {
    if (Color is null) return Vector4.One;
    var v = Color.Evaluate(n); // (src1, src2, src3, src0)
    return new Vector4(v.W, v.X, v.Y, v.Z);
  }
  public Curve1? Emissive { get; init; }
  public Curve1? Transparency { get; init; }

  public static MeshParticleDef? Parse(JsonObject? body)
  {
    if (body.Str("nameGeomTpl") is not { Length: > 0 } tpl) return null;
    var inst = body.Obj("typeInst");
    var instKind = SslRead.UnionKind(inst) ?? "default";
    int min = 0, max = 0;
    if (instKind == "enumeration" && int.TryParse(inst.Obj("enumeration").Str("typeInstEnum") ?? SslRead.Num(inst.Obj("enumeration").Get("typeInstEnum"))?.ToString(), out var en))
      min = max = en;
    else if (instKind == "range")
    {
      var r = SslRead.Range(inst.Obj("range").Get("typeInstRange"), Vector2.Zero);
      min = (int)r.X; max = (int)r.Y;
    }

    var transforms = body.Obj("transforms");
    var axes = transforms.Obj("initRotAxises");
    var axesBody = SslRead.UnionKind(axes) == "random" ? axes.Obj("random") : null;
    var ov = body.Obj("sfxMtlOverrides");
    return new MeshParticleDef
    {
      Template = tpl,
      SubmaterialOverride = body.Str("nameSubmaterialOverride") is { Length: > 0 } sm ? sm : null,
      InstanceMin = Math.Min(min, max),
      InstanceMax = Math.Max(min, max),
      RandomAxes = axesBody is null ? Vector3.Zero : new Vector3(axesBody.Bool("x", false) ? 1 : 0, axesBody.Bool("y", false) ? 1 : 0, axesBody.Bool("z", false) ? 1 : 0),
      RotationFromEmitter = transforms.Bool("applyRotationFromEmitter", false),
      Color = ov.Obj("color") is { } c && SslRead.UnionKind(c) == "color" ? SslRead.Color(c.Obj("color")) : null,
      Emissive = Override(ov.Obj("emissiveIntensity")),
      Transparency = Override(ov.Obj("colorTransparency")),
    };
  }

  private static Curve1? Override(JsonObject? node) => SslRead.UnionKind(node) switch
  {
    "splineOverride" when SslRead.Floats(node.Obj("splineOverride").Get("spline")) is { Length: >= 2 } f => new Curve1(f),
    "constOverride" when SslRead.Num(node.Obj("constOverride")?.FirstOrDefault().Value) is { } v => new Curve1([0f, v]),
    _ => null,
  };
}

/// <summary>One mesh-particle instance ready to draw.</summary>
/// <param name="Stage">Normalised particle age, fed to the VAT shader as combineMtlAnimControl.animStage (HEURISTIC).</param>
public readonly record struct MeshInstance(Matrix4x4 World, Vector4 Color, float Emissive, int Variant, float Stage = 0);

public sealed class MeshBatch
{
  public required EmitterDef Emitter { get; init; }
  public required MeshParticleDef Geom { get; init; }
  public List<MeshInstance> Instances { get; } = [];
}

public static class EffectMeshParticles
{
  /// <summary>
  ///   Instances per lwiGeom emitter. Scale is the particle size (template units, times <c>emitScale</c>); with
  ///   <c>isAlignSpeed</c> the template's +Z follows the velocity, after the random initial spin.
  /// </summary>
  public static List<MeshBatch> Build(EffectInstance instance)
  {
    var batches = new List<MeshBatch>();
    foreach (var state in instance.Emitters)
    {
      var def = state.Def;
      if (def.Geom is not { } geom || !def.Visible || state.Particles.Count == 0) continue;
      var batch = new MeshBatch { Emitter = def, Geom = geom };
      foreach (var p in state.Particles)
      {
        var n = Math.Clamp(p.Age / p.Life, 0f, 1f);
        var size = def.Size.Sample(n, p.RandSize) * def.Scale;
        if (size <= 0) continue;
        var rot = Matrix4x4.CreateFromQuaternion(p.Orientation);
        if (def.AlignSpeed && p.Velocity.LengthSquared() > 1e-8f)
          rot *= AlignZ(Vector3.Normalize(p.Velocity));
        else if (geom.RotationFromEmitter)
          rot *= state.World with { M41 = 0, M42 = 0, M43 = 0, M44 = 1 };
        var c = geom.ColorAt(n);
        var alpha = c.W * (geom.Transparency?.Evaluate(n) ?? 1f);
        var world = Matrix4x4.CreateScale(size) * rot * Matrix4x4.CreateTranslation(p.Position);
        batch.Instances.Add(new MeshInstance(world, new Vector4(c.X, c.Y, c.Z, alpha), geom.Emissive?.Evaluate(n) ?? 0f, p.Variant, n));
      }

      if (batch.Instances.Count > 0) batches.Add(batch);
    }

    return batches;
  }

  private static Matrix4x4 AlignZ(Vector3 dir)
  {
    var up = MathF.Abs(dir.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
    var x = Vector3.Normalize(Vector3.Cross(up, dir));
    var y = Vector3.Cross(dir, x);
    return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, dir.X, dir.Y, dir.Z, 0, 0, 0, 0, 1);
  }
}
