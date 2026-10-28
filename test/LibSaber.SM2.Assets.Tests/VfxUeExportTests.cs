using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-34: frozen-frame VFX export for UE (blend mapping, composite rules, rule-scene expectations, camera).</summary>
public class VfxUeExportTests
{
  [Theory]
  [InlineData(VfxDrawKind.CpuParticle, false, "AlphaComposite")]
  [InlineData(VfxDrawKind.Trail, false, "Translucent")]
  [InlineData(VfxDrawKind.Spark, false, "Masked")]
  [InlineData(VfxDrawKind.SfxMesh, false, "Additive")]
  [InlineData(VfxDrawKind.CpuParticle, true, "Distortion")]
  public void UeBlendFollowsCaptureBlendState(VfxDrawKind kind, bool distortion, string ue) =>
    Assert.Equal(ue, VfxFrameExport.UeBlend(kind, distortion));

  [Fact]
  public void ParticleCompositeIsPremultipliedOneInvSrcAlpha()
  {
    foreach (var (blend, mul) in new[] { (EffectBlend.Blend, true), (EffectBlend.Blend, false), (EffectBlend.Add, true), (EffectBlend.BlendAdd, true) })
    {
      var flags = EffectBlendRea.Flags(blend, mul);
      var (k, a) = EffectBlendRea.Output(flags, 0.7f, 0.4f);
      Assert.Equal(2f * k + 0.3f * (1f - a), VfxFrameExport.Composite(VfxDrawKind.CpuParticle, 2f, 0.7f, 0.4f, 0.3f, flags), 1e-6f);
    }
  }

  [Fact]
  public void TrailSparkAndSfxComposite()
  {
    Assert.Equal(2f * 0.25f + 0.3f * 0.75f, VfxFrameExport.Composite(VfxDrawKind.Trail, 2f, 0.5f, 0.5f, 0.3f, 0), 1e-6f);
    Assert.Equal(1f, VfxFrameExport.Composite(VfxDrawKind.Spark, 2f, 0.5f, 0.6f, 0.3f, 0), 1e-6f);
    Assert.Equal(0.3f, VfxFrameExport.Composite(VfxDrawKind.Spark, 2f, 0.5f, 0.4f, 0.3f, 0), 1e-6f);
    Assert.Equal(2.3f, VfxFrameExport.Composite(VfxDrawKind.SfxMesh, 2f, 0.5f, 0.4f, 0.3f, 0), 1e-6f);
  }

  [Fact]
  public void RuleSceneExpectationsMatchUeMeasurement()
  {
    // UE 5.7 render of the rule scene (mtl1-34) matched every display patch within 0.41 / 255.
    var expect = VfxRuleScene.Build()["expect"]!.AsArray().ToDictionary(e => (string)e!["id"]!, e => (float)e!["value"]!);
    var want = new Dictionary<string, float>
    {
      ["tone0"] = 65.1f, ["tone3"] = 180.4f, ["tone7"] = 253.0f, ["part_blend_mul"] = 212.4f, ["part_blend_nomul"] = 230.0f,
      ["part_add"] = 234.7f, ["trail_ta05"] = 230.1f, ["spark_pass"] = 240.7f, ["spark_cull"] = 192.9f,
    };
    foreach (var (id, v) in want) Assert.Equal(v, expect[id], 0.15f);
    Assert.Equal(24f, expect["distort2"], 0.01f);
  }

  [Fact]
  public void CameraWritesRadiansAndDegrees()
  {
    var c = VfxFrameExport.Camera(new Vector3(0, 0, 5), Vector3.Zero, MathF.PI / 4);
    Assert.Equal(MathF.PI / 4, (float)c["fovY"]!, 1e-4f);
    Assert.Equal(45f, (float)c["fovYDeg"]!, 1e-3f);
  }
}
