using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   mtl1-34: "frozen frame" export of an effect for Unreal (no Niagara). One JSON per effect and time holds every sprite
///   batch the browser would draw (<see cref="EffectSprites.Build"/>: camera-facing quads with HDR colour, alpha, clip,
///   flipbook UVs), the camera, and the capture-measured render rules (<see cref="CaptureRules"/>): blend state per draw
///   kind (<see cref="VfxBlendRules"/>) mapped to a UE blend mode, the CB_PASS_HDR tonemap (<see cref="CaptureTonemap"/>)
///   and the DUDV distortion formula (<see cref="DistortionRules"/>). tools/ue/ue_vfx_frame.py rebuilds it with
///   ProceduralMeshComponents and capture-rule masters. Space: SM2 metres, Y up (the importer maps (x, y, z) to UE
///   (x, z, y) * 100 cm, a reflection that turns the right-handed view into UE's left-handed one without mirroring).
/// </summary>
public static class VfxFrameExport
{
  /// <summary>
  ///   UE blend mode that reproduces a capture blend state exactly:
  ///   premultiplied One/InvSrcAlpha = BLEND_AlphaComposite (dst = emissive + (1 - opacity) * dst);
  ///   straight SrcAlpha/InvSrcAlpha = BLEND_Translucent; One/One = BLEND_Additive; blending off = BLEND_Masked.
  ///   Distortion batches write the DUDV buffer; UE rebuilds it in two passes (SceneColor / refraction are constant inside a
  ///   SceneCapture2D in 5.7): an additive shift pass into an RGBA16F target, then the tonemap post-process material samples
  ///   PostProcessInput0 at ViewportUV + shift (DistortionRules.Post).
  /// </summary>
  public static string UeBlend(VfxDrawKind kind, bool distortion) => distortion ? "Distortion" : kind switch
  {
    VfxDrawKind.CpuParticle => "AlphaComposite",
    VfxDrawKind.Trail => "Translucent",
    VfxDrawKind.Spark => "Masked",
    VfxDrawKind.SfxMesh => "Additive",
    _ => throw new ArgumentOutOfRangeException(nameof(kind)),
  };

  /// <summary>Normal-map decode of the particle distortion PS (particles.sdc 00005_1): o = rg * 2.007874 - 1.007874.</summary>
  public const float NormalScale = 2.007874f, NormalBias = -1.007874f;

  private static JsonArray Vec(Vector3 v) => [R(v.X), R(v.Y), R(v.Z)];
  private static float R(float v) => float.IsFinite(v) ? float.Parse(v.ToString("G6", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) : 0f;

  /// <summary>Capture constants shared by every frame file (the UE masters and the rule check read them from here).</summary>
  public static JsonObject Rules() => new()
  {
    ["tonemap"] = new JsonObject
    {
      ["A"] = CaptureTonemap.A, ["B"] = CaptureTonemap.B, ["C"] = CaptureTonemap.C, ["D"] = CaptureTonemap.D,
      ["E"] = CaptureTonemap.E, ["F"] = CaptureTonemap.F, ["whitePointScale"] = CaptureTonemap.WhitePointScale,
      ["key"] = CaptureTonemap.Key, ["lut"] = "not applied (grading LUT contents not dumped)",
      ["evidence"] = "CB_PASS_HDR, LUT bake CS s1996, all five RenderDoc captures (CaptureRules.CaptureTonemap)",
    },
    ["distortion"] = new JsonObject
    {
      ["uvPerOffset"] = DistortionRules.UvPerOffset, ["maxShift"] = DistortionRules.MaxShift,
      ["normalScale"] = NormalScale, ["normalBias"] = NormalBias,
      ["evidence"] = "plasma 101917 PS 4996 (DUDV write) + PS 5160 (uv += dudv*0.04-0.02) (CaptureRules.DistortionRules)",
    },
    ["blend"] = new JsonObject
    {
      ["CpuParticle"] = VfxBlendRules.For(VfxDrawKind.CpuParticle).ToString(),
      ["Trail"] = VfxBlendRules.For(VfxDrawKind.Trail).ToString(),
      ["Spark"] = VfxBlendRules.For(VfxDrawKind.Spark).ToString(),
      ["SfxMesh"] = VfxBlendRules.For(VfxDrawKind.SfxMesh).ToString(),
      ["sparkAlphaTest"] = VfxDrawKinds.SparkAlphaTest,
    },
  };

  /// <summary>Camera of the frame: perspective (eye/target/fovY radians) or orthographic when <paramref name="orthoWidth"/> &gt; 0.</summary>
  public static JsonObject Camera(Vector3 eye, Vector3 target, float fovY, float orthoWidth = 0f)
  {
    var o = new JsonObject { ["eye"] = Vec(eye), ["target"] = Vec(target), ["up"] = Vec(Vector3.UnitY), ["fovY"] = R(fovY), ["fovYDeg"] = R(fovY * 180f / MathF.PI) };
    if (orthoWidth > 0) o["orthoWidth"] = R(orthoWidth);
    return o;
  }

  /// <summary>One batch: material description plus its quads (4 vertices of <see cref="SpriteBatch.Stride"/> floats each).</summary>
  public static JsonObject Batch(SpriteBatch b, string? texture = null)
  {
    var e = b.Emitter;
    var flags = EffectBlendRea.Flags(e.Blend, e.BlendAlphaMultiply);
    var tex = texture ?? (b.Distortion ? e.DistortTexture : e.Texture);
    var verts = new JsonArray();
    for (var i = 0; i < b.QuadCount * 4 * SpriteBatch.Stride; i++) verts.Add(R(b.Vertices[i]));
    return new JsonObject
    {
      ["emitter"] = e.Name,
      ["path"] = e.Path,
      ["kind"] = b.Kind.ToString(),
      ["distortion"] = b.Distortion,
      ["ueBlend"] = UeBlend(b.Kind, b.Distortion),
      ["captureBlend"] = b.Distortion ? "DUDV One/InvSrcAlpha (refraction buffer)" : VfxBlendRules.For(b.Kind).ToString(),
      ["texture"] = tex ?? "",
      ["alphaMask"] = b.Distortion ? "" : e.AlphaMask ?? "",
      ["frames"] = new JsonArray(b.Frames.FramesX, b.Frames.FramesY),
      ["flags"] = flags,
      // EffectBlendRea.Output: rgb factor = va * (0x400 ? 1 : texAlpha), output alpha = 0x20 ? 0 : va * texAlpha.
      ["rgbTexAlpha"] = (flags & 0x400u) == 0 ? 1 : 0,
      ["outAlpha"] = (flags & 0x20u) == 0 ? 1 : 0,
      ["textureTint"] = R(e.TextureTint),
      ["contrast"] = R(e.AdditionalContrast),
      ["contrastKey"] = Vec(e.ContrastColorKey / 255f),
      ["quads"] = b.QuadCount,
      ["stride"] = SpriteBatch.Stride,
      ["vertices"] = verts,
    };
  }

  public static JsonObject Frame(string effect, float time, JsonObject camera, float exposure, IEnumerable<JsonObject> batches, string exposureNote)
  {
    var list = new JsonArray();
    foreach (var b in batches) list.Add(b);
    var o = new JsonObject
    {
      ["format"] = "sm2-vfx-frame/1",
      ["effect"] = effect,
      ["time"] = R(time),
      ["camera"] = camera,
      ["exposure"] = R(exposure),
      ["exposureNote"] = exposureNote,
      ["rules"] = Rules(),
      ["batches"] = list,
    };
    return o;
  }

  // ---- rule check scene: synthetic batches whose expected UE output follows from CaptureRules -----------------------

  /// <summary>sRGB encode of a linear 0..1 value (the display transfer after the tonemap).</summary>
  public static float Srgb(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

  /// <summary>Expected 8-bit display value of an HDR value under the capture tonemap at <paramref name="exposure"/>.</summary>
  public static float Display(float hdr, float exposure) => 255f * Srgb(CaptureTonemap.Apply(hdr * exposure));

  /// <summary>
  ///   Composite of one synthetic quad over an opaque backdrop <paramref name="dst"/> (HDR), per capture blend state:
  ///   premultiplied particle (rgb = c*va*(rgbTexAlpha ? ta : 1), a = outAlpha ? va*ta : 0; One/InvSrcAlpha),
  ///   trail (SrcAlpha/InvSrcAlpha on c, va*ta), spark (blending off, alpha test 0.5 on ta, rgb = c*va).
  /// </summary>
  public static float Composite(VfxDrawKind kind, float c, float va, float ta, float dst, uint flags)
  {
    switch (kind)
    {
      case VfxDrawKind.CpuParticle:
        var (k, a) = EffectBlendRea.Output(flags, va, ta);
        return c * k + dst * (1f - a);
      case VfxDrawKind.Trail:
        var at = va * ta;
        return c * at + dst * (1f - at);
      case VfxDrawKind.Spark:
        return ta >= VfxDrawKinds.SparkAlphaTest ? c * va : dst;
      case VfxDrawKind.SfxMesh:
        return c + dst;
      default:
        throw new ArgumentOutOfRangeException(nameof(kind));
    }
  }
}
