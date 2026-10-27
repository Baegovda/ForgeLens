using System.Numerics;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>visuals.additionalContrast (@0x143645248, +0x12C) / contrastColorKey (@0x143645260, +0x130) parsing.</summary>
public class ContrastTests
{
  private static string Emitter(string name, string visualsExtra) => $$"""
       {{name}}   =   {
          isVisible   =   True
          emission   =   {
             type   =   {
                explosion   =   {
                   numPartsToEmit   =   [
                      1,
                      1
                   ]
                }
                __type   =   "local__explosion"
             }
          }
          typeParams   =   {
             default   =   {
                visuals   =   {
                   texture   =   {
                      nameTex   =   "seq_fin_expl_g"
                   }
    {{visualsExtra}}
                }
             }
             __type   =   "local__default"
          }
          __type   =   "CPU_EMITTER"
       }
    """;

  private static EffectDefinition Def()
  {
    var part = "timeLife   =   1\nemitters2   =   {\n"
      + Emitter("hot", """
                   additionalContrast   =   37.53
                   contrastColorKey   =   [
                      255,
                      246,
                      102,
                      14
                   ]
    """)
      + Emitter("plain", "")
      + "}\n__type   =   \"sfx_part\"\n";
    return EffectDefinition.Build("fx_k", new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_k"] = part })).Resolve("fx_k")!);
  }

  [Fact]
  public void ParsesContrastAndKeyArgb()
  {
    var hot = Def().Emitters.Single(e => e.Name == "hot");
    Assert.Equal(37.53f, hot.AdditionalContrast, 0.001f);
    Assert.Equal(new Vector3(246, 102, 14), hot.ContrastColorKey);
  }

  [Fact]
  public void AbsentMeansOff()
  {
    var plain = Def().Emitters.Single(e => e.Name == "plain");
    Assert.Equal(0f, plain.AdditionalContrast);
    Assert.Equal(Vector3.Zero, plain.ContrastColorKey);
  }

  [Fact]
  public void KeyAcceptsUnionAndRgb()
  {
    Assert.Equal(new Vector3(1, 2, 3), EmitterDef.ContrastKey(System.Text.Json.Nodes.JsonNode.Parse("[1,2,3]")));
    Assert.Equal(new Vector3(2, 3, 4), EmitterDef.ContrastKey(System.Text.Json.Nodes.JsonNode.Parse("{\"__type\":\"local__constExpr\",\"constExpr\":[1,2,3,4]}")));
    Assert.Equal(Vector3.Zero, EmitterDef.ContrastKey(null));
  }
}
