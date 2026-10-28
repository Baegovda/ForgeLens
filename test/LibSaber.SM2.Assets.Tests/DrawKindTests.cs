using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public class DrawKindTests
{
  [Fact]
  public void GpuStreakEmittersAreSparks()
  {
    Assert.Equal(VfxDrawKind.Spark, VfxDrawKinds.Classify(true, 0.2f));
    Assert.Equal(VfxDrawKind.CpuParticle, VfxDrawKinds.Classify(true, 0f));
    Assert.Equal(VfxDrawKind.CpuParticle, VfxDrawKinds.Classify(false, 0.2f));
    Assert.Equal(VfxDrawKind.Spark, VfxDrawKinds.ForEmitter(new EmitterDef { Name = "g", Path = "g", IsGpu = true, StreakRatio = 0.1f }));
  }

  [Fact]
  public void DistortionEffectsGetANonPaintingBackdrop()
  {
    var def = new EffectDefinition { Name = "d" };
    Assert.False(CoveringDemo.WantsHost(def));
    def.Emitters.Add(new EmitterDef { Name = "e", Path = "e", DistortTexture = "fx_dudv" });
    Assert.True(CoveringDemo.WantsHost(def));
    var demo = new CoveringDemo(CoveringHostKind.Plane, def, System.Numerics.Matrix4x4.Identity);
    Assert.False(demo.Paints);
    Assert.Equal(0.6f, demo.CheckerContrast);
    var v = demo.Version;
    demo.Update(EffectInstance.Standalone(def, 1));
    Assert.Equal(v, demo.Version);
    Assert.All(demo.Mask, m => Assert.Equal(0f, m));
  }
}

public class BackdropTests
{
  [Fact]
  public void DistortionBackdropFacesEyeBehindOrigin()
  {
    var def = new EffectDefinition { Name = "d" };
    def.Emitters.Add(new EmitterDef { Name = "e", Path = "e", DistortTexture = "fx_dudv" });
    var eye = new System.Numerics.Vector3(0, 1, 5);
    var plane = CoveringDemo.For(CoveringHostKind.Plane, def, System.Numerics.Matrix4x4.Identity, eye);
    var dir = System.Numerics.Vector3.Normalize(eye);
    Assert.True(System.Numerics.Vector3.Dot(plane.Axis, dir) > 0.999f);
    Assert.True(System.Numerics.Vector3.Dot(plane.Center, dir) < -0.4f);
    var sphere = CoveringDemo.For(CoveringHostKind.Sphere, def, System.Numerics.Matrix4x4.Identity, eye);
    Assert.True(System.Numerics.Vector3.Dot(sphere.Center, dir) < -CoveringDemo.SphereRadius);
    def.Coverings.Add(new CoveringDef("p/em_covering_blood", "blood", 1f, true, ""));
    Assert.Null(CoveringDemo.For(CoveringHostKind.Plane, def, System.Numerics.Matrix4x4.Identity, eye).BackdropEye);
  }
}
