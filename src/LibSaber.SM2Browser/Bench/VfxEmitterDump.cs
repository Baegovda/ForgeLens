using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   mtl1-40: <c>--vfxemitters &lt;sfx root&gt; &lt;out.json&gt;</c> lists every parsed emitter's GPU-constant inputs
///   (textureTintControl, additionalContrast, contrastColorKey, blendType, isBlendAlphaMultiply, texture, isAlphaKill / isScreenSpace and the predicted
///   FUN_1417e1e50 blend flags 0x20/0x400; mtl1-41 also lists effects without emitters with a 4th argument). Used to match a
///   capture's PART_EMITTERS_SHARED_DATA (FUN_1417e1e50: shared[0].z tint, shared[1] = key rgb + additionalContrast,
///   shared[3].x flags 0x20/0x400) against our parser (tools/vfx/tint_check.py).
/// </summary>
internal static class VfxEmitterDump
{
  public static int Run(string[] args)
  {
    var library = new EffectLibrary(new DirectoryEffectSource(args[1]));
    var all = new JsonArray();
    // mtl1-42: SM2_VFX_TEXROOT=<raw root> adds the texture's alpha coverage and the resolved draw kind (spark fallback).
    var texFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (Environment.GetEnvironmentVariable("SM2_VFX_TEXROOT") is { Length: > 0 } texRoot)
      foreach (var f in Directory.EnumerateFiles(texRoot, "*.pct.resource", SearchOption.AllDirectories))
        texFiles.TryAdd(Path.GetFileName(f)[..^".pct.resource".Length], f);
    var coverage = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
    bool? Coverage(string? t)
    {
      if (t is null || texFiles.Count == 0) return null;
      if (coverage.TryGetValue(t, out var c)) return c;
      try { c = texFiles.TryGetValue(t, out var p) ? VfxDrawKinds.HasAlphaCoverage(LibSaber.SM2.Assets.Textures.TextureLoader.LoadLoose(p).Decode(0, 0, out _, out _, out _)) : null; }
      catch { c = null; }
      return coverage[t] = c;
    }
    var unresolved = new List<string>(); var failed = new List<string>(); var empty = new List<string>();
    foreach (var name in library.Names)
    {
      EffectDefinition def;
      try
      {
        if (library.Resolve(name) is not { } tree) { unresolved.Add(name); continue; }
        def = EffectDefinition.Build(name, tree);
      }
      catch (Exception ex) { failed.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); continue; }
      if (def.Emitters.Count == 0) empty.Add(name);
      // mtl1-42: trails too (kind Trail, texture alpha coverage, particle-attached flag).
      if (texFiles.Count > 0)
        foreach (var t in def.Trails)
          all.Add(new JsonObject
          {
            ["effect"] = name, ["emitter"] = t.Name, ["gpu"] = false, ["type"] = "trail", ["blend"] = t.Render.Blend.ToString(),
            ["texture"] = t.Render.Texture, ["kind"] = "Trail", ["texAlpha"] = Coverage(t.Render.Texture),
            ["particleAttached"] = def.ParticleAttachedTrails.Contains(t.Name),
          });
      foreach (var e in def.Emitters)
        all.Add(new JsonObject
        {
          ["effect"] = name, ["emitter"] = e.Name, ["gpu"] = e.IsGpu, ["type"] = e.ParticleType, ["target"] = e.Target.ToString(),
          ["tint"] = e.TextureTint, ["contrast"] = e.AdditionalContrast,
          ["key"] = new JsonArray(e.ContrastColorKey.X, e.ContrastColorKey.Y, e.ContrastColorKey.Z),
          ["blend"] = e.Blend.ToString(), ["alphaMul"] = e.BlendAlphaMultiply, ["texture"] = e.Texture,
          ["alphaKill"] = e.AlphaKill, ["screenSpace"] = e.ScreenSpace, ["flags"] = EffectBlendRea.Flags(e),
          ["kind"] = VfxDrawKinds.ForEmitter(e).ToString(), ["texAlpha"] = Coverage(e.Texture),
          ["resolvedKind"] = VfxDrawKinds.Resolve(VfxDrawKinds.ForEmitter(e), Coverage(e.Texture)).ToString(),
        });
    }
    File.WriteAllText(args[2], all.ToJsonString());
    Console.WriteLine($"vfxemitters: {all.Count} emitters, {library.Names.Count()} effects, {empty.Count} without particle emitters, {unresolved.Count} unresolved, {failed.Count} failed -> {args[2]}");
    foreach (var f in failed.Take(40)) Console.WriteLine("  failed " + f);
    foreach (var u in unresolved.Take(20)) Console.WriteLine("  unresolved " + u);
    if (args.Length > 3) File.WriteAllLines(args[3], failed.Select(f => "failed\t" + f).Concat(unresolved.Select(u => "unresolved\t" + u)).Concat(empty.Select(e => "empty\t" + e)));
    return 0;
  }
}
