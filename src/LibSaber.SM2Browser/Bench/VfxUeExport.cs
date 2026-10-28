using System.Globalization;
using System.Numerics;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SM2Browser.Effects;
using LibSaber.SM2Browser.Gpu;

namespace LibSaber.SM2Browser.Bench;

/// <summary>
///   mtl1-34: headless (no GL) frozen-frame export for Unreal, see <see cref="VfxFrameExport"/>.
///   <c>--vfxue &lt;sfx root&gt; &lt;part_aliases.ps&gt; &lt;texture root&gt; &lt;out&gt; name@seconds[@distance] ...</c> writes
///   <c>&lt;out&gt;/&lt;name&gt;_&lt;ms&gt;.vfxframe.json</c> (camera = the vfxbench Fit camera) and
///   <c>&lt;out&gt;/textures/&lt;texture&gt;.png</c>; <c>--vfxue-rules &lt;out&gt;</c> writes the rule check scene
///   (<see cref="VfxRuleScene"/>) and its synthetic textures.
/// </summary>
internal static class VfxUeExport
{

  public static int Rules(string[] args)
  {
    var output = args[1];
    var textures = Path.Combine(output, "textures");
    Directory.CreateDirectory(textures);
    foreach (var (name, (w, h, bgra)) in VfxRuleScene.Textures())
      ImageWriters.WritePng(Path.Combine(textures, name + ".png"), bgra, w, h);
    File.WriteAllText(Path.Combine(output, "sm2_vfx_rules.vfxframe.json"), VfxRuleScene.Build().ToJsonString());
    Console.WriteLine($"vfxue-rules: wrote {output}");
    return 0;
  }

  public static int Run(string[] args)
  {
    var library = new EffectLibrary(new DirectoryEffectSource(args[1]));
    var aliases = PartAliases.Load(args[2]);
    var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var f in Directory.EnumerateFiles(args[3], "*.pct.resource", SearchOption.AllDirectories))
      files.TryAdd(Path.GetFileName(f)[..^".pct.resource".Length], f);
    var output = args[4];
    var texDir = Path.Combine(output, "textures");
    Directory.CreateDirectory(texDir);
    var meshDir = Environment.GetEnvironmentVariable("SM2_VFX_MESHES") is { Length: > 0 } md ? md
      : Directory.Exists(Path.Combine(args[3], "mesh1")) ? Path.Combine(args[3], "mesh1")
      : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[3]).TrimEnd('/')) ?? ".", "mesh");
    var meshes = EffectMeshes.FromDirectory(meshDir, args[3]);
    // mtl1-35 grading: SM2_VFX_GRADING = content LUT name (e.g. story_tower_lut) or "identity" (default; the runtime
    // COLOR_GRADING volumes of the smoke/plasma captures were not dumped, so identity is HEURISTIC there).
    var gradingName = Environment.GetEnvironmentVariable("SM2_VFX_GRADING") is { Length: > 0 } gn ? gn : "identity";
    var whitePoint = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_WHITEPOINT"), CultureInfo.InvariantCulture, out var wp) ? wp : CaptureTonemap.WhitePointScale;
    GradingLut? grading = null;
    if (gradingName != "identity")
    {
      if (!files.TryGetValue(gradingName, out var lutPath)) { Console.WriteLine($"grading LUT {gradingName}: not found under {args[3]}"); return 1; }
      grading = GradingLut.FromTexture(TextureLoader.LoadLoose(lutPath));
    }
    var baked = CaptureLut.Bake(grading, null, 1f, whitePoint);
    var lutName = $"sm2lut_{gradingName}_{(int)MathF.Round(whitePoint * 1e6f)}".ToLowerInvariant();
    ImageWriters.WritePng(Path.Combine(texDir, lutName + ".png"), CaptureLut.Strip(baked), CaptureLut.Size * CaptureLut.Size, CaptureLut.Size);
    var exposure = float.TryParse(Environment.GetEnvironmentVariable("SM2_VFX_EXPOSURE"), CultureInfo.InvariantCulture, out var ex) ? ex : 1f / 250f;
    const int size = 512;
    var exit = 0;
    foreach (var spec in args.Skip(5))
    {
      var parts = spec.Split('@');
      var name = parts[0];
      var tree = library.Resolve(name);
      if (tree is null) { Console.WriteLine($"{name}: not found"); exit = 1; continue; }
      var def = EffectDefinition.Build(name, tree);
      var seconds = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : Math.Min(def.Duration, 3f) * 0.5f;
      var instance = EffectInstance.Standalone(def, 1);
      // Same framing as the vfxbench sheet (Fit over the effect's capture window, min(duration, 3 s)).
      var camera = EffectCamera.Fit(instance, Math.Min(def.Duration, 3f));
      if (parts.Length > 2 && float.TryParse(parts[2], CultureInfo.InvariantCulture, out var distance)) camera = camera with { Distance = distance };
      var (view, _, eye) = camera.Matrices(size, size);
      instance.Seek(seconds);
      var batches = EffectSprites.Build(instance, aliases, view);
      var written = new List<System.Text.Json.Nodes.JsonObject>();
      var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var b in batches)
      {
        var j = VfxFrameExport.Batch(b);
        written.Add(j);
        foreach (var t in new[] { (string?)j["texture"], (string?)j["alphaMask"] })
        {
          if (string.IsNullOrEmpty(t)) continue;
          var png = Path.Combine(texDir, t.ToLowerInvariant() + ".png");
          if (File.Exists(png)) continue;
          try
          {
            if (!files.TryGetValue(t, out var path)) { missing.Add(t); continue; }
            var doc = TextureLoader.LoadLoose(path);
            var bgra = doc.Decode(0, 0, out var w, out var h, out _);
            ImageWriters.WritePng(png, bgra, w, h);
          }
          catch (Exception e) { missing.Add($"{t} ({e.Message})"); }
        }
      }

      var lights = instance.Lights().ToList();
      var meshBatches = MeshBatches(instance, view, eye, meshes, lights, exposure, seconds, out var meshNote);
      foreach (var j in meshBatches)
      {
        var t = (string?)j["texture"];
        if (string.IsNullOrEmpty(t)) continue;
        var png = Path.Combine(texDir, t.ToLowerInvariant() + ".png");
        if (File.Exists(png)) continue;
        try
        {
          if (!files.TryGetValue(t, out var path)) { missing.Add(t); continue; }
          var bgra = TextureLoader.LoadLoose(path).Decode(0, 0, out var w, out var h, out _);
          ImageWriters.WritePng(png, bgra, w, h);
        }
        catch (Exception e) { missing.Add($"{t} ({e.Message})"); }
      }
      // Draw order: opaque lit meshes, sfx meshes (One/One, order-free), then the far-to-near sprite batches.
      written.InsertRange(0, meshBatches.OrderBy(j => (string)j["ueBlend"]! == "Solid" ? 0 : 1));
      var frame = VfxFrameExport.Frame(name, seconds, VfxFrameExport.Camera(eye, camera.Center, camera.Fov), exposure, written,
        "browser bench exposure (SM2_VFX_EXPOSURE, default 1/250; HEURISTIC). Game exposure = key/avgLum (CaptureTonemap.Exposure).");
      frame["pixels"] = size;
      frame["lights"] = VfxFrameExport.Lights(lights);
      frame["groundY"] = 0f;
      frame["grading"] = new System.Text.Json.Nodes.JsonObject
      {
        ["lut"] = gradingName, ["strip"] = lutName, ["whitePointScale"] = whitePoint, ["weight"] = 1f,
        ["layout"] = "strip 1024x32: x = r + 32*b, y = g; texel i <-> LUT coordinate i/31 (s1996 thread id)",
        ["note"] = gradingName == "identity" ? "identity grading (HEURISTIC: runtime COLOR_GRADING volumes not dumped)" : "content LUT texture (outdoor 137266 NEXT input, exact)",
      };
      frame["meshNote"] = meshNote;
      frame["missingTextures"] = new System.Text.Json.Nodes.JsonArray(missing.Select(m => (System.Text.Json.Nodes.JsonNode)m).ToArray());
      var file = Path.Combine(output, $"{name}_{(int)MathF.Round(seconds * 1000)}ms.vfxframe.json");
      File.WriteAllText(file, frame.ToJsonString());
      Console.WriteLine($"{name}@{seconds:0.000}: meshes {meshBatches.Count} ({meshBatches.Sum(m => (int)m["triangles"]!)} tris) lights {lights.Count} batches {batches.Count} quads {batches.Sum(b => b.QuadCount)} kinds [{string.Join(", ", batches.GroupBy(b => (string)VfxFrameExport.Batch(b)["ueBlend"]!).Select(g => $"{g.Key} {g.Count()}"))}] missing [{string.Join(", ", missing)}] -> {Path.GetFileName(file)}");
    }

    return exit;
  }

  /// <summary>
  ///   mtl1-35: mesh particles (<see cref="EffectMeshParticles"/>) flattened to world-space triangle batches in the sprite
  ///   vertex stride, one batch per (emitter, template part material). Per-vertex HDR colour is the browser mesh shading
  ///   (EffectRenderer.MeshFs) evaluated at the vertex: base = sRGB->linear(override colour) * material tint;
  ///   sfx: base * (200 * key(n) + effect lights) + base * emissive * 25, times the SfxExposure compensation;
  ///   lit: base * (SurfLight * key(n) + effect lights). Alpha = override alpha * transparency * soft fresnel (per vertex).
  ///   VAT deformation is not applied (rest pose). HEURISTIC: the browser shading itself (light units, key light).
  /// </summary>
  internal static List<System.Text.Json.Nodes.JsonObject> MeshBatches(EffectInstance instance, Matrix4x4 view, Vector3 eye, EffectMeshes meshes,
    List<LightSample> lights, float exposure, float time, out string note)
  {
    var result = new List<System.Text.Json.Nodes.JsonObject>();
    var list = EffectMeshParticles.Build(instance, view);
    var missing = new List<string>();
    var vat = 0;
    if (list.Count > 0)
    {
      meshes.Prefetch(list.Select(b => b.Geom.Template));
      meshes.WaitAll(TimeSpan.FromSeconds(60));
    }
    var sfxScale = SfxExposure.Exposure(exposure, EffectRenderer.ReferenceExposure, EffectRenderer.SfxLumK) / MathF.Max(exposure, 1e-12f);
    var key = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.3f));
    foreach (var batch in list)
    {
      var mesh = meshes.Get(batch.Geom.Template);
      if (mesh is null || mesh.IsMissing || mesh.Variants.Count == 0) { missing.Add(batch.Geom.Template); continue; }
      var groups = new Dictionary<EffectMeshMaterial, (List<float> V, List<int> I)>();
      foreach (var inst in batch.Instances)
      {
        var parts = mesh.Variants[Math.Clamp(inst.Variant, 0, mesh.Variants.Count - 1)];
        var normalM = inst.World with { M41 = 0, M42 = 0, M43 = 0, M44 = 1 };
        foreach (var (start, count, material) in parts)
        {
          if (material.Vat is not null && LibSaber.SM2.Assets.Effects.VatDecode.Enabled) vat++;
          if (!groups.TryGetValue(material, out var g)) groups[material] = g = (new List<float>(), new List<int>());
          var c = inst.Color * material.Tint;
          var baseC = new Vector3(MathF.Pow(MathF.Max(c.X, 0), 2.2f), MathF.Pow(MathF.Max(c.Y, 0), 2.2f), MathF.Pow(MathF.Max(c.Z, 0), 2.2f));
          var scroll = EffectRenderer.ScrollEnabled ? material.Scroll * time : Vector2.Zero;
          var remap = new Dictionary<uint, int>();
          for (var k = start; k < start + count; k++)
          {
            var src = mesh.Indices[k];
            if (!remap.TryGetValue(src, out var dst))
            {
              dst = g.V.Count / SpriteBatch.Stride; remap[src] = dst;
              var o = (int)src * 10;
              var p = Vector3.Transform(new Vector3(mesh.Vertices[o], mesh.Vertices[o + 1], mesh.Vertices[o + 2]), inst.World);
              var nv = Vector3.TransformNormal(new Vector3(mesh.Vertices[o + 3], mesh.Vertices[o + 4], mesh.Vertices[o + 5]), normalM);
              var n = nv.LengthSquared() > 1e-12f ? Vector3.Normalize(nv) : Vector3.UnitY;
              var toEye = eye - p;
              if (Vector3.Dot(n, toEye) < 0) n = -n; // two-sided: the browser flips back faces (gl_FrontFacing)
              var light = (material.Translucent ? 200f : EffectRenderer.SurfLight) * (0.35f + 0.65f * MathF.Max(Vector3.Dot(n, key), 0f));
              var lc = Vector3.Zero;
              foreach (var l in lights.Take(8))
              {
                var d = l.Position - p; var r = MathF.Max(0.01f, l.Radius);
                var fall = Math.Clamp(1f - d.LengthSquared() / (r * r), 0f, 1f);
                if (d.LengthSquared() > 1e-12f) lc += l.Color * l.Intensity * fall * fall * MathF.Max(Vector3.Dot(n, Vector3.Normalize(d)), 0f);
              }
              var hdr = baseC * (new Vector3(light) + lc) + (material.Translucent ? baseC * inst.Emissive * 25f : Vector3.Zero);
              if (material.Translucent) hdr *= sfxScale;
              var a = material.Translucent ? c.W : 1f;
              if (material.Translucent && material.SoftFresnel > 0 && toEye.LengthSquared() > 1e-12f)
                a *= MathF.Pow(MathF.Abs(Vector3.Dot(n, Vector3.Normalize(toEye))), material.SoftFresnel);
              g.V.AddRange([p.X, p.Y, p.Z, mesh.Vertices[o + 6] + scroll.X, mesh.Vertices[o + 7] + scroll.Y, hdr.X, hdr.Y, hdr.Z, a,
                material.Translucent ? material.Clip : 0f, 0f, 0f, 0f]);
            }
            g.I.Add(dst);
          }
        }
      }
      foreach (var (material, g) in groups)
        if (g.I.Count >= 3)
          result.Add(VfxFrameExport.TriangleBatch(batch.Emitter.Name, batch.Emitter.Path, material.Translucent, material.Texture, material.Translucent ? material.Clip : 0f, g.V, g.I,
            $"mesh {batch.Geom.Template}, {batch.Instances.Count} instances"));
    }
    note = $"mesh particles: {list.Count} emitters, {result.Count} batches; missing templates [{string.Join(", ", missing.Distinct())}]{(vat > 0 ? $"; {vat} VAT parts drawn in rest pose" : "")}";
    return result;
  }
}
