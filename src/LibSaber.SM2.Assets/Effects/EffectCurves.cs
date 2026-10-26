using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Piecewise-linear curve stored flat as <c>[t0, v0, t1, v1, ...]</c> (the .sfx spline layout).</summary>
public sealed class Curve1
{
  public static readonly Curve1 Zero = new([0f, 0f]);
  public static readonly Curve1 One = new([0f, 1f]);
  private readonly float[] _t;
  private readonly float[] _v;

  public Curve1(float[] flat)
  {
    var n = Math.Max(1, flat.Length / 2);
    _t = new float[n];
    _v = new float[n];
    for (var i = 0; i < n; i++)
    {
      _t[i] = flat.Length > i * 2 ? flat[i * 2] : 0;
      _v[i] = flat.Length > i * 2 + 1 ? flat[i * 2 + 1] : 0;
    }
  }

  public int Count => _t.Length;
  public float StartTime => _t[0];
  public float EndTime => _t[^1];
  public float Max => _v.Max();
  public Curve1 Scaled(float k)
  {
    var flat = new float[_t.Length * 2];
    for (var i = 0; i < _t.Length; i++) { flat[i * 2] = _t[i]; flat[i * 2 + 1] = _v[i] * k; }
    return new Curve1(flat);
  }

  public float Evaluate(float t)
  {
    if (t <= _t[0]) return _v[0];
    for (var i = 1; i < _t.Length; i++)
    {
      if (t <= _t[i])
      {
        var span = _t[i] - _t[i - 1];
        return span <= 1e-6f ? _v[i] : _v[i - 1] + (_v[i] - _v[i - 1]) * (t - _t[i - 1]) / span;
      }
    }

    return _v[^1];
  }
}

/// <summary>
///   Colour key list stored flat as <c>[t, A, R, G, B, ...]</c>, 0..255 per channel. The order is A,R,G,B:
///   retail tracers keep RGB constant while only the 2nd value fades (trail_bolter_tracer_autority 88→255→0),
///   smoke puffs fade the 2nd value 0→255→72 on white, and sfx_light <c>ColorCtrl.value</c> is
///   <c>[255, 255, 237, 197]</c> (opaque warm white) in lght_mzl_bolter.
/// </summary>
public sealed class ColorCurve
{
  public static readonly ColorCurve White = new([0f, 255f, 255f, 255f, 255f]);
  private readonly float[] _t;
  private readonly Vector4[] _c; // RGBA 0..1

  public ColorCurve(float[] flat)
  {
    var n = Math.Max(1, flat.Length / 5);
    _t = new float[n];
    _c = new Vector4[n];
    for (var i = 0; i < n; i++)
    {
      float At(int k) => flat.Length > i * 5 + k ? flat[i * 5 + k] : 255f;
      _t[i] = flat.Length > i * 5 ? flat[i * 5] : 0f;
      _c[i] = new Vector4(At(2), At(3), At(4), At(1)) / 255f;
    }
  }

  public static ColorCurve Constant(Vector4 rgba) => new([0f, rgba.W * 255f, rgba.X * 255f, rgba.Y * 255f, rgba.Z * 255f]);

  public Vector4 Evaluate(float t)
  {
    if (t <= _t[0]) return _c[0];
    for (var i = 1; i < _t.Length; i++)
    {
      if (t <= _t[i])
      {
        var span = _t[i] - _t[i - 1];
        return span <= 1e-6f ? _c[i] : Vector4.Lerp(_c[i - 1], _c[i], (t - _t[i - 1]) / span);
      }
    }

    return _c[^1];
  }
}

/// <summary>
///   A scalar parameter after its <c>local__X</c> union is resolved. <see cref="Curve"/> is sampled over normalized
///   age (or emitter time) and a per-particle random <c>r</c> in [0,1] picks inside <see cref="Disp"/>
///   (<c>dispRange</c>) or the <c>[min, max]</c> of a constant range.
/// </summary>
public sealed record ScalarParam(Curve1 Curve, Vector2 Disp, bool Cycled = false)
{
  public static ScalarParam Constant(float value) => new(new Curve1([0f, value]), Vector2.Zero);
  public static ScalarParam Range(float min, float max) => new(new Curve1([0f, 0f]), new Vector2(min, max));
  public bool IsConstant => Curve.Count == 1;
  public ScalarParam Scaled(float k) => this with { Curve = Curve.Scaled(k), Disp = Disp * k };
  public float Sample(float t, float r) => Curve.Evaluate(Cycled && Curve.EndTime > 0 ? t % Curve.EndTime : t) + Disp.X + (Disp.Y - Disp.X) * r;
}

/// <summary>Helpers for reading the JsonNode tree that <see cref="SslText"/> produces.</summary>
public static class SslRead
{
  public static JsonObject? Obj(this JsonNode? node, string key) => (node as JsonObject)?[key] as JsonObject;
  public static JsonNode? Get(this JsonNode? node, string key) => (node as JsonObject)?[key];

  public static string? Str(this JsonNode? node, string key)
    => (node as JsonObject)?[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

  public static float? Num(JsonNode? node)
  {
    if (node is not JsonValue v) return null;
    if (v.TryGetValue<double>(out var d)) return (float)d;
    if (v.TryGetValue<long>(out var l)) return l;
    if (v.TryGetValue<int>(out var i)) return i;
    if (v.TryGetValue<bool>(out var b)) return b ? 1 : 0;
    return null;
  }

  public static float Float(this JsonNode? node, string key, float fallback) => Num(node.Get(key)) ?? fallback;

  public static bool Bool(this JsonNode? node, string key, bool fallback)
    => (node as JsonObject)?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

  public static float[] Floats(JsonNode? node)
    => node is JsonArray a ? a.Select(x => Num(x) ?? 0f).ToArray() : Num(node) is { } f ? [f] : [];

  public static float[] Floats(this JsonNode? node, string key) => Floats(node.Get(key));

  /// <summary><c>[min,max]</c>, <c>{x,y}</c> or a scalar as a range.</summary>
  public static Vector2 Range(JsonNode? node, Vector2 fallback)
  {
    switch (node)
    {
      case JsonArray a when a.Count >= 2: return new Vector2(Num(a[0]) ?? 0, Num(a[1]) ?? 0);
      case JsonArray a when a.Count == 1: return new Vector2(Num(a[0]) ?? 0);
      case JsonObject o when o.ContainsKey("x") || o.ContainsKey("y"):
        var x = Num(o["x"]) ?? 0;
        return new Vector2(x, Num(o["y"]) ?? x);
      case JsonValue:
        return Num(node) is { } f ? new Vector2(f) : fallback;
      default: return fallback;
    }
  }

  public static Vector3 Vec3(JsonNode? node, Vector3 fallback)
  {
    var f = Floats(node);
    return f.Length >= 3 ? new Vector3(f[0], f[1], f[2]) : fallback;
  }

  /// <summary>Live member name of a <c>local__X</c> union, or null when the node is not one.</summary>
  public static string? UnionKind(JsonNode? node)
    => node.Str("__type") is { } t && t.StartsWith("local__", StringComparison.Ordinal) ? t[7..] : null;

  /// <summary>
  ///   Scalar union: <c>constExpr</c> (number, <c>[min,max]</c> or <c>{x,y}</c>), <c>constExprRange</c>,
  ///   <c>value</c>, <c>ageSpline</c>/<c>spline</c> (<c>{spline, dispRange, isCycled}</c> or a flat list).
  ///   Plain numbers and ranges are accepted too.
  /// </summary>
  public static ScalarParam Scalar(JsonNode? node, float fallback)
  {
    if (node is null) return ScalarParam.Constant(fallback);
    if (node is JsonValue) return ScalarParam.Constant(Num(node) ?? fallback);
    if (node is JsonArray) { var r = Range(node, new Vector2(fallback)); return ScalarParam.Range(r.X, r.Y); }
    var kind = UnionKind(node);
    if (kind is null)
    {
      if (node.Get("spline") is { } sp) return Spline(node, sp);
      if (node.Get("value") is { } val) return Scalar(val, fallback);
      var r = Range(node, new Vector2(fallback));
      return ScalarParam.Range(r.X, r.Y);
    }

    var body = node.Get(kind);
    return kind switch
    {
      "ageSpline" or "spline" or "timeSpline" or "ageSize" => body is JsonArray ? new ScalarParam(new Curve1(Floats(body)), Vector2.Zero) : Spline(body, body.Get("spline") ?? body.Get(kind)), // sdr.power: { ageSpline = { ageSpline = [..] } }
      _ => Scalar(body, fallback),
    };
  }

  private static ScalarParam Spline(JsonNode? holder, JsonNode? spline)
  {
    var flat = Floats(spline);
    return new ScalarParam(flat.Length >= 2 ? new Curve1(flat) : Curve1.Zero, Range(holder.Get("dispRange"), Vector2.Zero), holder.Bool("isCycled", false));
  }

  /// <summary>Colour union: <c>ageSpline</c> (flat t,A,R,G,B) or <c>constExpr</c>/<c>value</c> (A,R,G,B).</summary>
  public static ColorCurve Color(JsonNode? node)
  {
    if (node is null) return ColorCurve.White;
    var kind = UnionKind(node);
    var body = kind is null ? node : node.Get(kind);
    if (body is JsonObject o && o["spline"] is { } sp) body = sp;
    // ageSplineNew: [{type, offset, value:[A,R,G,B]}, ...].
    if (body is JsonArray keys && keys.Count > 0 && keys[0] is JsonObject)
    {
      var flat = new List<float>();
      foreach (var k in keys.OfType<JsonObject>())
        if (Floats(k["value"]) is { Length: 4 } v)
          flat.AddRange([Num(k["offset"]) ?? 0f, v[0], v[1], v[2], v[3]]);
      return flat.Count >= 5 ? new ColorCurve(flat.ToArray()) : ColorCurve.White;
    }

    var f = Floats(body);
    if (f.Length >= 5) return new ColorCurve(f);
    if (f.Length == 4) return new ColorCurve([0f, f[0], f[1], f[2], f[3]]);
    return ColorCurve.White;
  }

  /// <summary>16-float row-major transform (translation at 12..14) as a System.Numerics matrix.</summary>
  public static Matrix4x4 Matrix(JsonNode? node)
  {
    var f = Floats(node);
    if (f.Length < 16) return Matrix4x4.Identity;
    return new Matrix4x4(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]);
  }
}
