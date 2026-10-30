using System.Numerics;
using System.Text.Json;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Games.Poe2;

/// <summary>One particle/trail event of an effect animation (from the .ao ParticleEffects / TrailsEffects structs).</summary>
public sealed record Poe2EffectEvent(string Kind, string Animation, float Time, string File, string BoneGroup, IReadOnlyList<string> Bones);

/// <summary>
///   PoE2 skill effect preview (M1 skills step). An effect .ao names a skeleton (.ast), a default animation, bone groups and,
///   per animation, the .pet particle / .trl trail files spawned on a bone group (PROVEN on fire_fireball rig.ao).
///   Preview = the effect rig animated by its .ast clip; each .pet GPU emitter that has a <c>particle_mesh</c> (.fmt) is drawn
///   as that mesh rigidly bound to the first bone of its group; sprite emitters (no mesh) get a 2-quad cross proxy with the
///   first texture of the render material. Both are drawn in the viewer's additive sfx pass.
///   NOT simulated: emission rate, lifetime, velocity, size/colour curves (they live in the update .mat fxgraphs).
/// </summary>
public sealed partial class Poe2Reader
{
  public sealed class EffectInfo
  {
    public string Ao { get; init; } = "";
    public string? Skeleton { get; set; }
    public string? DefaultAnimation { get; set; }
    public Dictionary<string, List<string>> BoneGroups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Poe2EffectEvent> Events { get; } = new();
  }

  public EffectInfo ReadAo(string ao)
  {
    var info = new EffectInfo { Ao = ao };
    using var doc = Json(ao);
    void Structs(string prop)
    {
      if (!doc.RootElement.TryGetProperty(prop, out var arr)) return;
      foreach (var s in arr.EnumerateArray())
      {
        var name = s.GetProperty("name").GetString();
        foreach (var e in s.GetProperty("entries").EnumerateArray())
        {
          var key = e.GetProperty("key").GetString();
          var value = e.GetProperty("value").GetString() ?? "";
          if (key == "skeleton") info.Skeleton = value;
          else if (key == "default_animation") info.DefaultAnimation = value;
          else if (key == "bone_group")
          {
            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3) info.BoneGroups[parts[0]] = parts.Skip(2).ToList();
          }
          else if (key == "animations" && name is "ParticleEffects" or "TrailsEffects")
          {
            try
            {
              using var anims = JsonDocument.Parse(value, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
              foreach (var a in anims.RootElement.EnumerateArray())
                foreach (var ev in a.GetProperty("events").EnumerateArray())
                {
                  var group = ev.TryGetProperty("bone_group", out var g) ? g.GetString() ?? "" : "";
                  info.Events.Add(new Poe2EffectEvent(name == "ParticleEffects" ? "pet" : "trl", a.GetProperty("name").GetString() ?? "",
                    ev.TryGetProperty("time", out var t) ? t.GetSingle() : 0, ev.GetProperty("filename").GetString() ?? "", group,
                    Array.Empty<string>()));
                }
            }
            catch (JsonException x) { Log.Add($"ao {ao} {name}: {x.Message}"); }
          }
        }
      }
    }
    Structs("structs");
    Structs("client_structs");
    for (var i = 0; i < info.Events.Count; i++)
    {
      var ev = info.Events[i];
      info.Events[i] = ev with { Bones = info.BoneGroups.TryGetValue(ev.BoneGroup, out var b) ? b : new List<string>() };
    }
    return info;
  }

  /// <summary>Effect preview mesh: rig + clip from the .ao skeleton, emitter meshes / sprite proxies bound to bone-group bones.</summary>
  public ExportedMesh LoadEffect(string ao, string? animation = null)
  {
    var info = ReadAo(ao);
    animation ??= info.DefaultAnimation;
    var mesh = new ExportedMesh { Name = Path.GetFileNameWithoutExtension(ao) + "_" + animation };
    if (info.Skeleton != null)
      ReadSkeleton(mesh, info.Skeleton, animation);
    if (mesh.Bones.Count == 0)
      mesh.Bones.Add(new Bone { Name = "root", FileBind = true, Influences = true });
    var worlds = new Matrix4x4[mesh.Bones.Count];
    for (var i = 0; i < worlds.Length; i++)
      worlds[i] = mesh.Bones[i].Parent < 0 ? mesh.Bones[i].Local : mesh.Bones[i].Local * worlds[mesh.Bones[i].Parent];

    foreach (var ev in info.Events.Where(e => string.Equals(e.Animation, animation, StringComparison.OrdinalIgnoreCase)))
    {
      var boneName = ev.Bones.FirstOrDefault();
      var bone = boneName == null ? 0 : Math.Max(0, mesh.Bones.FindIndex(b => string.Equals(b.Name, boneName, StringComparison.OrdinalIgnoreCase)));
      Log.Add($"event {ev.Kind} t={ev.Time} {ev.File} group {ev.BoneGroup} -> bone {mesh.Bones[bone].Name}");
      if (ev.Kind != "pet") continue;
      JsonDocument pet;
      try { pet = Json(ev.File); } catch (IOException) { Log.Add($"pet missing {ev.File}"); continue; }
      using (pet)
      {
        if (!pet.RootElement.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("gpu_emitters", out var gpu))
          continue;
        var n = 0;
        foreach (var em in gpu.EnumerateArray())
        {
          n++;
          var meshFile = em.TryGetProperty("particle_mesh", out var pm) ? pm.GetString() : null;
          var render = em.TryGetProperty("render_passes", out var rp) ? rp.EnumerateArray().Select(r => r.TryGetProperty("render_material", out var m) ? m.GetString() : null).FirstOrDefault(s => !string.IsNullOrEmpty(s)) : null;
          var tex = render == null ? null : FirstTexture(render);
          var name = $"{Path.GetFileNameWithoutExtension(ev.File)}_e{n}";
          var start = mesh.Indices.Count;
          if (!string.IsNullOrEmpty(meshFile))
          {
            ExportedMesh pmesh;
            try { pmesh = Load(meshFile); }
            catch (Exception x) when (x is IOException or KeyNotFoundException) { Log.Add($"particle mesh missing {meshFile}"); continue; }
            Append(mesh, pmesh.Positions, pmesh.Normals, pmesh.Uvs, pmesh.Indices, worlds[bone], bone);
            name += "_mesh";
          }
          else
          {
            // Sprite proxy: two crossed 40 cm quads (size NOT from data; GPU particle size lives in the update fxgraph).
            var h = 0.2f;
            var pos = new List<Vector3> { new(-h, -h, 0), new(h, -h, 0), new(h, h, 0), new(-h, h, 0), new(0, -h, -h), new(0, -h, h), new(0, h, h), new(0, h, -h) };
            var nor = Enumerable.Repeat(Vector3.UnitZ, 8).ToList();
            var uv = new List<Vector2> { new(0, 1), new(1, 1), new(1, 0), new(0, 0), new(0, 1), new(1, 1), new(1, 0), new(0, 0) };
            var idx = new List<int> { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            Append(mesh, pos, nor, uv, idx, worlds[bone], bone);
            name += "_sprite";
          }
          var part = new MeshPart { Name = name, IndexStart = start, IndexCount = mesh.Indices.Count - start, Texture = tex, Variant = render ?? "", Sfx = true, SfxBlend = LibSaber.SM2.Assets.Effects.SfxBlendMode.Add, Blend = "translucent" };
          if (tex != null) part.Maps.Add(new MaterialMap { Role = "render", Name = tex });
          mesh.Parts.Add(part);
        }
      }
    }
    Log.Add($"effect {ao} anim {animation}: {mesh.Parts.Count} emitter parts, {mesh.Positions.Count} verts, {mesh.Bones.Count} bones, clip {mesh.Clip?.Duration:0.00}s");
    return mesh;
  }

  private static void Append(ExportedMesh mesh, IReadOnlyList<Vector3> pos, IReadOnlyList<Vector3> nor, IReadOnlyList<Vector2> uv, IReadOnlyList<int> idx, Matrix4x4 world, int bone)
  {
    var b = mesh.Positions.Count;
    for (var i = 0; i < pos.Count; i++)
    {
      mesh.Positions.Add(Vector3.Transform(pos[i], world));
      mesh.Normals.Add(i < nor.Count ? Vector3.Normalize(Vector3.TransformNormal(nor[i], world)) : Vector3.UnitY);
      mesh.Uvs.Add(i < uv.Count ? uv[i] : Vector2.Zero);
      mesh.Skin.Add(new SkinWeights { J0 = (ushort)bone, W0 = 1 });
    }
    foreach (var i in idx) mesh.Indices.Add(i + b);
  }

  /// <summary>First texture path of a .mat (any graph instance).</summary>
  public string? FirstTexture(string mat)
  {
    try
    {
      using var doc = Json(mat);
      foreach (var gi in doc.RootElement.GetProperty("graphinstances").EnumerateArray())
        if (gi.TryGetProperty("custom_parameters", out var cps))
          foreach (var cp in cps.EnumerateArray())
            foreach (var p in cp.GetProperty("parameters").EnumerateArray())
              if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("path", out var path) && path.GetString() is { } s && s.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
                  && !s.Contains("noise", StringComparison.OrdinalIgnoreCase) && !s.Contains("distortion", StringComparison.OrdinalIgnoreCase))
                return s;
    }
    catch (Exception x) when (x is IOException or JsonException or KeyNotFoundException) { Log.Add($"mat {mat}: {x.Message}"); }
    return null;
  }
}
