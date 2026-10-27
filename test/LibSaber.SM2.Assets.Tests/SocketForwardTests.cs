using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

public class SocketForwardTests
{
  private const string Part = """
    emitters2   =   {
       flame   =   {
          nameParent   =   "tip"
          isVisible   =   True
          emission   =   {
             angleEmission   =   0
             speedNorm   =   {
                constExpr   =   [
                   2,
                   2
                ]
                __type   =   "local__constExpr"
             }
             type   =   {
                explosion   =   {
                   numPartsToEmit   =   [
                      3,
                      3
                   ]
                }
                __type   =   "local__explosion"
             }
             source   =   {
                parentArea   =   {
                   direction   =   {
                      __type   =   "local__isEmitByNormal"
                   }
                }
                __type   =   "local__point"
             }
          }
          particleParams   =   {
             timeLife   =   [
                1,
                1
             ]
          }
          physics   =   {
             gravity   =   [
                0,
                0,
                0
             ]
          }
          __type   =   "CPU_EMITTER"
       }
    }
    __type   =   "sfx_part"
    """;

  [Fact]
  public void NoHostSocketEmitsAlongLocalZ()
  {
    var def = EffectDefinition.Build("fx_s", new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_s"] = Part })).Resolve("fx_s")!);
    var prev = EffectInstance.SocketForwardFallback;
    try
    {
      EffectInstance.SocketForwardFallback = true;
      var instance = new EffectInstance(def, 1) { Ground = null };
      instance.Seek(0.1f);
      var p = Assert.Single(instance.Emitters).Particles;
      Assert.NotEmpty(p);
      Assert.All(p, q => Assert.True(MathF.Abs(Vector3.Normalize(q.Velocity).Z) > 0.99f));
    }
    finally { EffectInstance.SocketForwardFallback = prev; }
  }
}
