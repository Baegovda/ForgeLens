using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   mtl1-40: <c>--vfxemitters &lt;sfx root&gt; &lt;out.json&gt;</c> lists every parsed emitter's GPU-constant inputs
///   (textureTintControl, additionalContrast, contrastColorKey, blendType, isBlendAlphaMultiply, texture). Used to match a
///   capture's PART_EMITTERS_SHARED_DATA (FUN_1417e1e50: shared[0].z tint, shared[1] = key rgb + additionalContrast,
///   shared[3].x flags 0x20/0x400) against our parser (tools/vfx/tint_check.py).
/// </summary>
internal static class VfxEmitterDump
{
  public static int Run(string[] args)
  {
    var library = new EffectLibrary(new DirectoryEffectSource(args[1]));
    var all = new JsonArray();
    foreach (var name in library.Names)
    {
      EffectDefinition def;
      try { if (library.Resolve(name) is not { } tree) continue; def = EffectDefinition.Build(name, tree); }
      catch { continue; }
      foreach (var e in def.Emitters)
        all.Add(new JsonObject
        {
          ["effect"] = name, ["emitter"] = e.Name, ["gpu"] = e.IsGpu, ["type"] = e.ParticleType, ["target"] = e.Target.ToString(),
          ["tint"] = e.TextureTint, ["contrast"] = e.AdditionalContrast,
          ["key"] = new JsonArray(e.ContrastColorKey.X, e.ContrastColorKey.Y, e.ContrastColorKey.Z),
          ["blend"] = e.Blend.ToString(), ["alphaMul"] = e.BlendAlphaMultiply, ["texture"] = e.Texture,
        });
    }
    File.WriteAllText(args[2], all.ToJsonString());
    Console.WriteLine($"vfxemitters: {all.Count} emitters -> {args[2]}");
    return 0;
  }
}
