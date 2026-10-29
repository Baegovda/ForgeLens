using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>Trail tcIn scale + degNear (trail.sdc VS: TRAIL_MAPPING, TRAIL_DEGR).</summary>
public class TrailShaderTests
{
  private const string Trail = """
    nameParent   =   "RotCenter"
    visuals   =   {
       blendType   =   "add"
       texture   =   {
          nameTex   =   "trail_smoke_hard"
          tcIn   =   {
             transform   =   {
                scale   =   [
                   0.025,
                   1
                ]
                wrapMode   =   "Clamp"
             }
          }
       }
       fadeParams   =   {
          distTranspFront   =   0.2
          distTranspMax   =   1
          distTranspMax_useMeters   =   True
       }
    }
    optimization   =   {
       degNear   =   {
          radStart   =   2
          radEnd   =   20
          scaleAlpha   =   0
       }
    }
    trailParams   =   {
       maxSegLength   =   0.5
       timeLifeSeg   =   1
       typeParams   =   {
          billboard   =   {
          }
          __type   =   "local__billboard"
       }
    }
    __type   =   "sfx_trail"
    """;

  private static TrailDef Def() =>
    Assert.Single(EffectDefinition.Build("t", new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["t"] = Trail })).Resolve("t")!).Trails);

  [Fact]
  public void ParsesTexScaleAndDegNear()
  {
    var t = Def();
    Assert.Equal(new Vector2(0.025f, 1f), t.TexScale);
    Assert.NotNull(t.DegNear);
    Assert.Equal(2f, t.DegNear!.RadStart);
    Assert.Equal(20f, t.DegNear.RadEnd);
    Assert.Null(t.DegFar);
  }

  [Fact]
  public void DegNearRampsAlphaPerDistance()
  {
    var t = Def();
    Assert.Equal(0f, t.Fade(1f).Alpha, 0.001f);   // closer than radStart: scaleAlpha
    Assert.Equal(0.5f, t.Fade(11f).Alpha, 0.001f); // halfway
    Assert.Equal(1f, t.Fade(30f).Alpha, 0.001f);   // beyond radEnd: unchanged
    Assert.Equal(1f, t.Fade(1f).Size, 0.001f);
  }

  [Fact]
  public void FadeParamsAlongTrail()
  {
    var t = Def();
    Assert.Equal(0.2f, t.FadeFront);
    Assert.False(t.FadeFrontMeters);
    Assert.Equal(1f, t.FadeMax);
    Assert.True(t.FadeMaxMeters);
    // length 10: front ramp over 2 m from the head, max ramp over 1 m from the tail.
    Assert.Equal(0f, t.AlongFade(0f, 10f), 1e-4f);
    Assert.Equal(0.5f, t.AlongFade(1f, 10f), 1e-4f);
    Assert.Equal(1f, t.AlongFade(5f, 10f), 1e-4f);
    Assert.Equal(0.5f, t.AlongFade(9.5f, 10f), 1e-4f);
    Assert.Equal(1f, new TrailDef { Name = "x", Path = "x", Render = t.Render }.AlongFade(3f, 10f), 1e-4f);
  }

  [Fact]
  public void FadeDefaultOnAndBakeFollowsFun141ab9b60()
  {
    Assert.True(TrailDef.ApplyAlongFade || Environment.GetEnvironmentVariable("SM2_VFX_TRAIL_FADE") == "0");
    var prev = TrailDef.ApplyAlongFade;
    TrailDef.ApplyAlongFade = true;
    try
    {
      var grad = new ColorCurve([0f, 255f, 255f, 255f, 255f, 1f, 0f, 0f, 0f, 0f]); // white -> black/transparent over age
      var add = new TrailDef { Name = "x", Path = "x", FadeFront = 0.2f, Render = new EmitterDef { Name = "x", Path = "x", Blend = EffectBlend.Add, Color = grad } };
      var blend = new TrailDef { Name = "x", Path = "x", FadeFront = 0.2f, Render = new EmitterDef { Name = "x", Path = "x", Blend = EffectBlend.Blend, Color = grad } };
      var inh = new TrailDef { Name = "x", Path = "x", FadeFront = 0.2f, AlphaInherited = true, Render = blend.Render };
      // Gradient follows age, not position: same position, different age.
      Assert.Equal(1f, add.BakePointColor(0f, 5f, 10f).X, 1e-4f);
      Assert.Equal(0.5f, add.BakePointColor(0.5f, 5f, 10f).X, 1e-4f);
      // add: fade on rgb, alpha kept; blend: fade on alpha, rgb kept.
      var ca = add.BakePointColor(0f, 1f, 10f);
      Assert.Equal(0.5f, ca.X, 1e-4f); Assert.Equal(1f, ca.W, 1e-4f);
      var cb = blend.BakePointColor(0f, 1f, 10f);
      Assert.Equal(1f, cb.X, 1e-4f); Assert.Equal(0.5f, cb.W, 1e-4f);
      // isAlphaInherited: alpha = inherited 255 after the fade, then owner alpha.
      Assert.Equal(0.25f, inh.BakePointColor(0.5f, 1f, 10f, 0.25f).W, 1e-4f);
    }
    finally { TrailDef.ApplyAlongFade = prev; }
  }
}
