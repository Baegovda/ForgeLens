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
}
