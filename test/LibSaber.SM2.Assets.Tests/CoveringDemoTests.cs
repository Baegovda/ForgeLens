using System.Numerics;
using LibSaber.SM2.Assets.Effects;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

public class CoveringDemoTests
{
  private static EffectDefinition Def()
  {
    var def = new EffectDefinition { Name = "t" };
    def.Coverings.Add(new CoveringDef("p/em_covering_blood", "blood", 2f, true, ""));
    return def;
  }

  [Theory]
  [InlineData(CoveringHostKind.Plane)]
  [InlineData(CoveringHostKind.Sphere)]
  public void ProjectInvertsSurfacePoint(CoveringHostKind kind)
  {
    var demo = new CoveringDemo(kind, Def(), Matrix4x4.Identity);
    var p = demo.SurfacePoint(0.3f, 0.4f);
    var (d, uv) = demo.Project(p);
    Assert.Equal(0f, d, 1e-4f);
    Assert.Equal(0.3f, uv.X, 1e-3f);
    Assert.Equal(0.4f, uv.Y, 1e-3f);
  }

  [Fact]
  public void SplatPaintsAroundHitScaledByRadiusScaleAndClearsOnRewind()
  {
    var demo = new CoveringDemo(CoveringHostKind.Plane, Def(), Matrix4x4.Identity);
    Assert.Equal(2f, demo.RadiusScale);
    demo.Splat(new Vector3(0.5f, 0, 0.5f), CoveringDemo.HitRadius * demo.RadiusScale, 1f);
    var (_, at) = demo.Project(new Vector3(0.5f, 0, 0.5f));
    var (_, far) = demo.Project(new Vector3(-0.5f, 0, -0.5f));
    float Sample(Vector2 uv) => demo.Mask[(int)(uv.Y * CoveringDemo.Res) * CoveringDemo.Res + (int)(uv.X * CoveringDemo.Res)];
    Assert.True(Sample(at) > 0.8f);
    Assert.Equal(0f, Sample(far));
    var v = demo.Version;
    demo.Clear();
    Assert.True(demo.Version > v);
    Assert.All(demo.Mask, m => Assert.Equal(0f, m));
  }

  [Fact]
  public void StyleByKind()
  {
    Assert.True(CoveringDemo.Style("blood").Albedo.X > CoveringDemo.Style("blood").Albedo.Y * 10);
    Assert.True(CoveringDemo.Style("water").Gloss > CoveringDemo.Style("mud").Gloss);
  }
}
