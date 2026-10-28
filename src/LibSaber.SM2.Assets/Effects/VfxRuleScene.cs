using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   mtl1-34: synthetic frame (same <see cref="VfxFrameExport"/> format) whose expected Unreal output follows from
///   <see cref="CaptureRules"/> alone: a tonemap ramp, each capture blend state over a known backdrop, the spark alpha
///   test and the DUDV distortion shift (incl. the +-0.02 clamp). Orthographic, <see cref="Pixels"/> square, the board
///   lies in the SM2 x/y plane; screen u = 0.5 + x / <see cref="Width"/>, v = 0.5 - y / <see cref="Width"/>.
///   Textures are 8-bit, so expectations use the quantised texel values.
/// </summary>
public static class VfxRuleScene
{
  public const float Width = 12f;
  public const int Pixels = 1200;
  public const float Exposure = 1f / 250f;
  /// <summary>Backdrop HDR value: exposed 0.5.</summary>
  public const float Backdrop = 0.5f / Exposure;
  public const float Patch = 0.8f;

  /// <summary>Synthetic textures: name -> (width, height, BGRA pixels). <c>_lin</c>, ramp and nrm textures are linear (not sRGB).</summary>
  public static Dictionary<string, (int W, int H, byte[] Bgra)> Textures()
  {
    var t = new Dictionary<string, (int, int, byte[])>();
    (int, int, byte[]) Solid(byte r, byte g, byte b, byte a)
    {
      var p = new byte[16 * 16 * 4];
      for (var i = 0; i < 256; i++) { p[i * 4] = b; p[i * 4 + 1] = g; p[i * 4 + 2] = r; p[i * 4 + 3] = a; }
      return (16, 16, p);
    }

    t["sm2r_white"] = Solid(255, 255, 255, 255);
    t["sm2r_white_lin"] = Solid(255, 255, 255, 255);
    t["sm2r_half"] = Solid(255, 255, 255, 128);
    t["sm2r_quarter"] = Solid(255, 255, 255, 64);
    t["sm2r_nrm_r191"] = Solid(191, 128, 0, 255);
    // Ramp backdrop for the distortion shift: 256 texels, value = x (linear texture, 1 texel per output pixel).
    var ramp = new byte[256 * 4 * 4];
    for (var y = 0; y < 4; y++)
      for (var x = 0; x < 256; x++)
      {
        var o = (y * 256 + x) * 4;
        ramp[o] = ramp[o + 1] = ramp[o + 2] = (byte)x; ramp[o + 3] = 255;
      }
    t["sm2r_ramp"] = (256, 4, ramp);
    return t;
  }

  public static float Texel(byte v) => v / 255f;

  private static EmitterDef Def(string name, EffectBlend blend = EffectBlend.Blend, bool alphaMul = true, string? tex = null) =>
    new() { Name = name, Path = "rules/" + name, Blend = blend, BlendAlphaMultiply = alphaMul, Texture = tex, DistortTexture = tex };

  private static SpriteBatch Quad(EmitterDef def, VfxDrawKind kind, bool distortion, Vector2 center, Vector2 half, Vector3 rgb, float alpha, float z = 0f)
  {
    var v = new float[4 * SpriteBatch.Stride];
    Span<(float X, float Y, float U, float V)> c = [(-1, -1, 0, 1), (1, -1, 1, 1), (1, 1, 1, 0), (-1, 1, 0, 0)];
    var o = 0;
    foreach (var (x, y, u, w) in c)
    {
      v[o++] = center.X + x * half.X; v[o++] = center.Y + y * half.Y; v[o++] = z;
      v[o++] = u; v[o++] = w;
      v[o++] = rgb.X; v[o++] = rgb.Y; v[o++] = rgb.Z; v[o++] = alpha;
      v[o++] = 0f; v[o++] = 0f;
      v[o++] = u; v[o++] = w;
    }

    return new SpriteBatch { Emitter = def, Frames = TextureFrames.Single, Vertices = v, QuadCount = 1, Kind = kind, Distortion = distortion };
  }

  private static JsonObject Expect(string id, Vector2 at, string what, float value, string formula) => new()
  {
    ["id"] = id, ["u"] = 0.5f + at.X / Width, ["v"] = 0.5f - at.Y / Width, ["what"] = what, ["value"] = value, ["formula"] = formula,
  };

  /// <summary>The frame JSON plus the expectations (display 0..255 values or a shift in pixels).</summary>
  public static JsonObject Build()
  {
    var batches = new List<JsonObject>();
    var expect = new JsonArray();
    var backdrop = new EmitterDef { Name = "backdrop", Path = "rules/backdrop", Texture = "sm2r_white" };
    void Add(SpriteBatch b, string? tex = null, bool isBackdrop = false)
    {
      var j = VfxFrameExport.Batch(b, tex);
      if (isBackdrop) { j["ueBlend"] = "Opaque"; j["captureBlend"] = "opaque backdrop (test scene)"; }
      batches.Add(j);
    }

    // Backdrop: upper area uniform (exposed 0.5), lower strip a linear ramp for the distortion check.
    Add(Quad(backdrop, VfxDrawKind.CpuParticle, false, new Vector2(0, 1.5f), new Vector2(Width / 2, 4.5f), new Vector3(Backdrop), 1f, -0.2f), "sm2r_white_lin", true);
    // Ramp: 256 texels over 2.56 m = 1 texel per pixel at 100 px/m; HDR = texel * RampScale (exposed 0..1).
    const float rampScale = 1f / Exposure;
    for (var i = 0; i < 3; i++)
      Add(Quad(backdrop, VfxDrawKind.CpuParticle, false, new Vector2(-3.5f + i * 3.5f, -4.0f), new Vector2(1.28f, 1.0f), new Vector3(rampScale), 1f, -0.2f), "sm2r_ramp", true);

    // Row 1 (y = 4.5): tonemap ramp, opaque premultiplied quads (ta = 1, va = 1) -> display = sRGB(Apply(E * exposure)).
    float[] exposed = [0.05f, 0.1f, 0.2f, 0.4f, 0.8f, 1.6f, 3.2f, 6.4f];
    for (var i = 0; i < exposed.Length; i++)
    {
      var at = new Vector2(-4.9f + i * 1.4f, 4.5f);
      var e = exposed[i] / Exposure;
      Add(Quad(Def($"tone{i}", tex: "sm2r_white"), VfxDrawKind.CpuParticle, false, at, new Vector2(Patch / 2), new Vector3(e), 1f));
      expect.Add(Expect($"tone{i}", at, "display", VfxFrameExport.Display(e, Exposure), $"sRGB(sat(Hable({exposed[i]})*WPS))"));
    }

    var half = Texel(128);
    var quarter = Texel(64);
    var c = 1.0f / Exposure;
    // Row 2 (y = 2.8): particle blend states over the backdrop.
    (string Id, EffectBlend Blend, bool Mul, float Va, string Tex, float Ta)[] parts =
    [
      ("part_blend_mul", EffectBlend.Blend, true, 1f, "sm2r_half", half),
      ("part_blend_mul_va05", EffectBlend.Blend, true, 0.5f, "sm2r_half", half),
      ("part_blend_nomul", EffectBlend.Blend, false, 1f, "sm2r_half", half),
      ("part_add", EffectBlend.Add, true, 1f, "sm2r_half", half),
      ("part_add_va05", EffectBlend.Add, true, 0.5f, "sm2r_quarter", quarter),
      ("part_blendadd", EffectBlend.BlendAdd, true, 1f, "sm2r_half", half),
    ];
    for (var i = 0; i < parts.Length; i++)
    {
      var p = parts[i];
      var at = new Vector2(-4.9f + i * 1.4f, 2.8f);
      var def = Def(p.Id, p.Blend, p.Mul, p.Tex);
      Add(Quad(def, VfxDrawKind.CpuParticle, false, at, new Vector2(Patch / 2), new Vector3(c), p.Va));
      var flags = EffectBlendRea.Flags(p.Blend, p.Mul);
      expect.Add(Expect(p.Id, at, "display", VfxFrameExport.Display(VfxFrameExport.Composite(VfxDrawKind.CpuParticle, c, p.Va, p.Ta, Backdrop, flags), Exposure),
        $"One/InvSrcAlpha premul, flags 0x{flags:x}"));
    }

    // Row 3 (y = 1.1): trails and sparks.
    (string Id, VfxDrawKind Kind, float Va, string Tex, float Ta)[] others =
    [
      ("trail_ta05", VfxDrawKind.Trail, 1f, "sm2r_half", half),
      ("trail_va05", VfxDrawKind.Trail, 0.5f, "sm2r_white", 1f),
      ("spark_pass", VfxDrawKind.Spark, 1f, "sm2r_half", half),
      ("spark_va05", VfxDrawKind.Spark, 0.5f, "sm2r_white", 1f),
      ("spark_cull", VfxDrawKind.Spark, 1f, "sm2r_quarter", quarter),
    ];
    for (var i = 0; i < others.Length; i++)
    {
      var p = others[i];
      var at = new Vector2(-4.9f + i * 1.4f, 1.1f);
      Add(Quad(Def(p.Id, tex: p.Tex), p.Kind, false, at, new Vector2(Patch / 2), new Vector3(c * 2f), p.Va));
      expect.Add(Expect(p.Id, at, "display", VfxFrameExport.Display(VfxFrameExport.Composite(p.Kind, c * 2f, p.Va, p.Ta, Backdrop, 0), Exposure),
        p.Kind == VfxDrawKind.Trail ? "SrcAlpha/InvSrcAlpha" : "blending off, alpha test 0.5"));
    }

    // Row 3b (y = -0.9, mtl1-35): soft depth. Premultiplied particles in front of the backdrop (z = -0.2) by gap g with
    // softDepth 1 m -> fade = sat(g / 1) multiplies rgb and alpha (PS 101818), i.e. va * fade with the alpha-multiply flag.
    float[] gaps = [0.25f, 0.5f, 1.5f];
    for (var i = 0; i < gaps.Length; i++)
    {
      var at = new Vector2(-4.9f + i * 1.4f, -0.9f);
      var def = new EmitterDef { Name = $"soft{i}", Path = $"rules/soft{i}", Texture = "sm2r_white", BlendAlphaMultiply = true, SoftDepth = 1f };
      Add(Quad(def, VfxDrawKind.CpuParticle, false, at, new Vector2(Patch / 2), new Vector3(c), 1f, -0.2f + gaps[i]));
      var fade = Math.Clamp(gaps[i] / 1f, 0f, 1f);
      expect.Add(Expect($"soft{i}", at, "display", VfxFrameExport.Display(VfxFrameExport.Composite(VfxDrawKind.CpuParticle, c, fade, 1f, Backdrop, EffectBlendRea.Flags(EffectBlend.Blend, true)), Exposure),
        $"soft depth: va *= sat({gaps[i]} / 1)"));
    }

    // Row 4 (y = -4): distortion over the ramps. Normal texel r = 191, g = 128 -> o = (tex * 2.007874 - 1.007874) * power.
    float[] powers = [0.125f, 0.25f, 1.0f];
    for (var i = 0; i < powers.Length; i++)
    {
      var at = new Vector2(-3.5f + i * 3.5f, -4.3f);
      var def = Def($"distort{i}", tex: "sm2r_nrm_r191");
      Add(Quad(def, VfxDrawKind.CpuParticle, true, at, new Vector2(0.6f, 0.4f), new Vector3(powers[i], 0, 0), 1f));
      var ox = (Texel(191) * VfxFrameExport.NormalScale + VfxFrameExport.NormalBias) * powers[i];
      var oy = (Texel(128) * VfxFrameExport.NormalScale + VfxFrameExport.NormalBias) * powers[i];
      var mask = DistortionRules.Mask(ox, oy);
      var shift = Math.Clamp(DistortionRules.UvPerOffset * mask * ox, -DistortionRules.MaxShift, DistortionRules.MaxShift);
      expect.Add(Expect($"distort{i}", at, "shiftPx", shift * Pixels, $"clamp(0.08*mask*o, 0.02) * {Pixels}px, o={ox:0.0000}, mask={mask:0.000}"));
    }

    var frame = VfxFrameExport.Frame("sm2_vfx_rules", 0f, VfxFrameExport.Camera(new Vector3(0, 0, 10), Vector3.Zero, 0f, Width), Exposure, batches,
      "rule scene: exposure 1/250 (browser bench default)");
    frame["pixels"] = Pixels;
    frame["expect"] = expect;
    return frame;
  }
}
