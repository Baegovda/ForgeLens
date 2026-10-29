using System.Runtime.CompilerServices;
using System.Numerics;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>One drawable part range of a <see cref="PreviewBatch"/> with its resolved preview material.</summary>
/// <summary>How a part composites, from the material: <c>MeshPart.Blend</c> (td <c>glt.blendMode</c> and the diffuse AKill channel).</summary>
public enum PreviewBlend
{
  Opaque = 0,
  /// <summary>Alpha test (AKill): cut at 0.5, alpha-to-coverage under MSAA, two-sided.</summary>
  Masked = 1,
  /// <summary><c>glt.blendMode</c> other than opaque: alpha blended after the opaque pass, no depth write, sorted.</summary>
  Translucent = 2,
  /// <summary>
  ///   mtl1-51: sfx shader card (<see cref="MeshPart.Sfx"/>), drawn after the translucent pass, unlit, no depth write:
  ///   colour = albedo.rgb * sfx tint, alpha = albedo.a * tint.a; additive for blendMode add / unspecified (PROVEN mtl1-50),
  ///   premultiplied over for blend / premul_a. dynamicAlphaMask is taken at alphaVal_max (HEURISTIC: the runtime value).
  /// </summary>
  Sfx = 3,
}

public sealed class PreviewPartDraw
{
  public string Name { get; init; } = "";
  public PreviewBlend Blend { get; init; }
  /// <summary>The albedo's alpha is coverage (<c>AKill(a)</c> or <c>Transp(a)</c> in the diffuse format).</summary>
  public bool AlphaFromAlbedo { get; init; }
  /// <summary><c>glt.albedo_tint</c> RGB (white when unset).</summary>
  public Vector3 Tint { get; init; } = Vector3.One;
  /// <summary><c>glt.anisotropic_spec</c> (hair): strand highlight along the tangent-space angle.</summary>
  public AnisoSpec? Aniso { get; init; }
  public bool SecondSpec { get; init; }
  /// <summary><c>glt.chameleon</c> view-dependent albedo ramp (null when disabled); see <see cref="ChameleonSpec"/>.</summary>
  public ChameleonSpec? Chameleon { get; init; }
  /// <summary>
  ///   glt base metalness/roughness {scale, bias}: metal = sat(spec.r * x + y), rough = sat(rough * z + w). mtl1-23 capture
  ///   (hellbrute 68141, CB_OBJ_MATERIAL_DATA OBJ_MTLBLEND_LAYER_CONST rows 2/3 = td scale/bias, e.g. eid 2979 armor_02 default
  ///   .988/0 and 1/.102); the glt PS feeds this metal into lerp(chameleon ramp, 1, metal) and the tint lerp.
  /// </summary>
  public Vector4 MetalRough { get; init; } = new(1f, 0f, 1f, 0f);
  /// <summary>glt.metallicTint (sRGB 0..1): albedo *= lerp(albedoTint, metallicTint, metal) (PS 29540 MTLBLEND rows 0/1).</summary>
  public Vector3 MetalTint { get; init; } = Vector3.One;
  /// <summary>Bind-pose centre of the part's triangles, for back-to-front sorting of translucent parts.</summary>
  public Vector3 Center { get; init; }
  public int IndexStart { get; init; }
  public int IndexCount { get; init; }
  public bool Draw { get; init; }
  public PreviewImage? Albedo { get; init; }
  public PreviewImage? Normal { get; init; }
  public PreviewImage? Specular { get; init; }
  public bool RoughInAlpha { get; init; }
  public bool AoInBlue { get; init; }
  /// <summary>Lit solid colour (sRGB 0..1) for parts without an albedo map, same tint as the CPU renderer.</summary>
  public Vector3 Solid { get; init; }
  /// <summary><see cref="PreviewBlend.Sfx"/>: sfx tint alpha (0..1).</summary>
  public float SfxAlpha { get; init; } = 1f;
  /// <summary><see cref="PreviewBlend.Sfx"/>: additive (blendMode add or unspecified) rather than premultiplied over.</summary>
  public bool SfxAdditive { get; init; } = true;
}

/// <summary>
///   One vertex pool (LOD buffer) as flat arrays. <see cref="Key"/> is stable for the life of the mesh, so a
///   GPU renderer can upload the buffers once and reuse them every frame.
/// </summary>
public sealed class PreviewBatch
{
  public required object Key { get; init; }
  public required Vector3[] Positions { get; init; }
  public required Vector3[] Normals { get; init; }
  public required Vector2[] Uvs { get; init; }
  public required int[] Indices { get; init; }
  public SkinWeights[]? Skin { get; init; }
  public required IReadOnlyList<PreviewPartDraw> Parts { get; init; }
}

/// <summary>Everything a renderer needs for one frame: geometry handles, camera, lights, skin palette, bone lines.</summary>
public sealed class PreviewScene
{
  public required IReadOnlyList<PreviewBatch> Batches { get; init; }
  /// <summary>Row-vector (System.Numerics) view and projection; projection depth is 0..1.</summary>
  public Matrix4x4 View { get; init; }
  public Matrix4x4 Projection { get; init; }
  public Vector3 Eye { get; init; }
  public Vector3 Center { get; init; }
  public Vector3 KeyLight { get; init; }
  public Vector3 FillLight { get; init; }
  /// <summary>InverseBind * world per bone while a clip plays; null draws the bind pose.</summary>
  public Matrix4x4[]? SkinMatrices { get; init; }
  /// <summary>Posed bone worlds (after the in-place rebase) when a clip plays or the skeleton shows; null otherwise.</summary>
  public Matrix4x4[]? BoneWorlds { get; init; }
  public IReadOnlyList<(Vector3 From, Vector3 To)> BoneLines { get; init; } = Array.Empty<(Vector3, Vector3)>();
  /// <summary>
  ///   Cloth-driven vertices for this frame, already posed in model space (draw them unskinned). Empty when
  ///   physics is off.
  /// </summary>
  public IReadOnlyList<LibSaber.SM2.Assets.Physics.VertexOverride> VertexOverrides { get; init; } = Array.Empty<LibSaber.SM2.Assets.Physics.VertexOverride>();
  /// <summary>Milliseconds the physics step took this frame (0 when off).</summary>
  public double PhysicsMs { get; init; }
  public string Note { get; init; } = "";
}

public static partial class MeshPreview
{
  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Dictionary<string, (Vector3 Center, Vector3 Extent, bool Ok)>> BoundsCache = new();

  /// <summary>
  ///   Same camera, lights, part selection, materials, in-place rebase and skin palette as <see cref="Render"/>,
  ///   without rasterising. Used by the GPU preview.
  /// </summary>
  public static PreviewScene BuildScene(ExportedMesh mesh, int width, int height, OrbitCamera camera, bool showSkeleton, float timeSeconds, PreviewSettings? settings = null)
  {
    width = Math.Max(1, width);
    height = Math.Max(1, height);
    var lod = Math.Max(0, settings?.Lod ?? 0);
    var partFilter = settings?.Part ?? -1;
    var honorVisibility = partFilter < 0 && lod <= 0;
    var slices = CollectSlices(mesh, lod, partFilter, honorVisibility);
    var physics = settings?.Physics == true ? PhysicsFor(mesh) : null;
    var clipActive = mesh.Bones.Count > 0 && (mesh.Clip is { HasCurves: true } || physics is not null);
    Matrix4x4[]? worlds = clipActive || showSkeleton ? BoneWorlds(mesh, timeSeconds) : null;
    var rebase = Matrix4x4.Identity;
    if (clipActive && settings?.InPlace == true && mesh.Clip is { HasCurves: true })
    {
      rebase = InPlaceRebase(mesh);
      for (var i = 0; i < worlds!.Length; i++)
        worlds[i] *= rebase;
      RemoveRootMotion(mesh, worlds, rebase);
    }
    // mtl1-42: physics runs AFTER the in-place rebase / root-motion removal, in the same space the body is drawn in.
    // It used to step on the raw clip worlds: jiggle bones were rebased afterwards with the body, but cloth particles
    // (ClothInstance positions, drawn as-is) stayed in root-motion space, so with In place on the tabard and purity seals
    // slid away from the body as the clip's ROOT travelled (cc_titus + titus_radar_intro_intercessor). The sim also no
    // longer reacts to locomotion the preview has removed.
    physics?.Step(worlds!, timeSeconds, mesh.Clip?.Duration ?? 0);

    var batches = new List<PreviewBatch>(slices.Count);
    var visibilityKey = new System.Text.StringBuilder();
    visibilityKey.Append(partFilter).Append('|').Append(honorVisibility ? 1 : 0).Append('|');
    foreach (var slice in slices)
    {
      var prepared = Prepare(slice);
      var parts = new List<PreviewPartDraw>();
      if (slice.Parts.Count == 0)
        parts.Add(new PreviewPartDraw { IndexStart = 0, IndexCount = prepared.Indices.Length, Draw = true, Solid = new Vector3(156, 118, 96) / 255f });
      for (var p = 0; p < slice.Parts.Count; p++)
      {
        var part = slice.Parts[p];
        var draw = (partFilter < 0 || p == partFilter) && (!honorVisibility || part.Visible);
        visibilityKey.Append(draw ? '1' : '0');
        var shift = p * 17 % 36 - 12;
        var channels = settings?.Specular is not null && part.Specular is not null && settings.Specular.TryGetValue(part.Specular, out var specImage) ? specImage.Channels : "";
        var start = Math.Clamp(part.IndexStart - part.IndexStart % 3, 0, prepared.Indices.Length);
        var end = Math.Clamp(part.IndexStart + part.IndexCount, start, prepared.Indices.Length);
        parts.Add(new PreviewPartDraw
        {
          IndexStart = start,
          IndexCount = (end - start) / 3 * 3,
          Draw = draw,
          Albedo = Lookup(settings?.Albedo, part.Texture),
          Normal = Lookup(settings?.Normals, part.Normal),
          Specular = Lookup(settings?.Specular, part.Specular),
          RoughInAlpha = channels.Contains("Roughness(a)", StringComparison.OrdinalIgnoreCase),
          AoInBlue = channels.Contains("AO(b)", StringComparison.OrdinalIgnoreCase) || channels.Length == 0,
          Solid = new Vector3(Math.Clamp(156 + shift, 48, 230), Math.Clamp(118 + shift / 2, 48, 210), Math.Clamp(96 + shift / 3, 48, 200)) / 255f,
          Name = part.Name,
          Blend = BlendOf(part),
          AlphaFromAlbedo = AlphaChannel(part),
          Tint = part.Sfx ? (part.SfxTint is { Length: >= 3 } st ? new Vector3(st[0], st[1], st[2]) : Vector3.One)
            : part.AlbedoTint is { Length: >= 3 } t ? new Vector3(t[0], t[1], t[2]) : Vector3.One,
          SfxAlpha = part.SfxTint is { Length: >= 4 } sa ? sa[3] : 1f,
          SfxAdditive = Effects.SfxBlend.IsAdditive(part.SfxBlend),
          Aniso = part.AnisoSpec,
          SecondSpec = part.SecondSpec is not null,
          Chameleon = part.Chameleon,
          MetalRough = part.BaseLayer is { } bl ? new Vector4(bl.MetalnessScale, bl.MetalnessBias, bl.RoughnessScale, bl.RoughnessBias) : new Vector4(1f, 0f, 1f, 0f),
          MetalTint = part.BaseLayer?.MetallicTint is { Length: >= 3 } mt ? new Vector3(mt[0], mt[1], mt[2]) : Vector3.One,
          Center = PartCenter(prepared, start, end),
        });
      }

      visibilityKey.Append(';');
      batches.Add(new PreviewBatch
      {
        Key = prepared,
        Positions = prepared.Positions,
        Normals = prepared.Normals,
        Uvs = prepared.Uvs,
        Indices = prepared.Indices,
        Skin = prepared.Skin,
        Parts = parts,
      });
    }

    // Bind-pose bounds of the drawn parts, cached per mesh and visibility so a frame costs no vertex walk.
    var cacheKey = visibilityKey.ToString() + lod;
    var cache = BoundsCache.GetOrCreateValue(mesh);
    (Vector3 Center, Vector3 Extent, bool Ok) bounds;
    lock (cache)
    {
      if (!cache.TryGetValue(cacheKey, out bounds))
      {
        var ok = FastBounds(slices, partFilter, honorVisibility, out var c, out var e);
        bounds = (c, e, ok);
        cache[cacheKey] = bounds;
      }
    }

    var center = bounds.Center;
    var extent = bounds.Extent;
    if (!bounds.Ok && mesh.Bones.Count > 0)
    {
      var min = new Vector3(float.MaxValue);
      var max = new Vector3(float.MinValue);
      foreach (var world in worlds ?? BoneWorlds(mesh, timeSeconds))
      {
        min = Vector3.Min(min, Translation(world));
        max = Vector3.Max(max, Translation(world));
      }

      center = (min + max) * 0.5f;
      extent = Vector3.Max(max - min, new Vector3(0.05f));
    }

    if (camera.Target is { } pivot)
      center = pivot;
    var distance = camera.Distance > 0 ? camera.Distance : FitDistance(extent, width / (float)height);
    var pitch = Math.Clamp(camera.Pitch, -1.2f, 1.2f);
    var planar = MathF.Cos(pitch);
    var eye = center + new Vector3(planar * MathF.Sin(camera.Yaw), MathF.Sin(pitch), planar * MathF.Cos(camera.Yaw)) * distance;
    var view = Matrix4x4.CreateLookAt(eye, center, Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, width / (float)height, Math.Max(0.01f, distance * 0.01f), distance * 40f);

    Matrix4x4[]? skinMatrices = null;
    if (clipActive)
    {
      skinMatrices = new Matrix4x4[mesh.Bones.Count];
      for (var i = 0; i < skinMatrices.Length; i++)
        skinMatrices[i] = mesh.Bones[i].InverseBind * worlds![i];
    }

    var overrides = new List<LibSaber.SM2.Assets.Physics.VertexOverride>();
    if (physics is not null && skinMatrices is not null)
    {
      foreach (var batch in batches)
        if (physics.Override(batch, skinMatrices, rebase) is { } over)
          overrides.Add(over);
    }

    var lines = new List<(Vector3, Vector3)>();
    if (showSkeleton && mesh.Bones.Count > 0 && worlds is not null)
    {
      var deformOnly = false;
      var fileOnly = false;
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        deformOnly |= mesh.Bones[i].Influences;
        fileOnly |= mesh.Bones[i].FileBind;
      }

      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        if (deformOnly && !mesh.Bones[i].Influences)
          continue;
        if (!deformOnly && fileOnly && !mesh.Bones[i].FileBind)
          continue;
        var parent = mesh.Bones[i].Parent;
        if (parent < 0 || parent >= mesh.Bones.Count)
          continue;
        lines.Add((Translation(worlds[parent]), Translation(worlds[i])));
      }
    }

    return new PreviewScene
    {
      Batches = batches,
      View = view,
      Projection = projection,
      Eye = eye,
      Center = center,
      KeyLight = Vector3.Normalize((eye - center) + Vector3.UnitY * distance * 0.45f),
      FillLight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, eye - center) + Vector3.UnitY * 0.25f),
      SkinMatrices = skinMatrices,
      BoneWorlds = worlds,
      BoneLines = lines,
      VertexOverrides = overrides,
      PhysicsMs = physics?.LastStepMs ?? 0,
      Note = slices.Count == 0 && mesh.Bones.Count == 0 ? (string.IsNullOrEmpty(mesh.Message) ? "Nothing to draw." : mesh.Message) : mesh.Message,
    };
  }

  private static readonly ConditionalWeakTable<ExportedMesh, LibSaber.SM2.Assets.Physics.PhysicsRuntime> PhysicsCache = new();

  /// <summary>The mesh's physics state (one per mesh object; a new clip on the same mesh resets it).</summary>
  public static LibSaber.SM2.Assets.Physics.PhysicsRuntime? PhysicsFor(ExportedMesh mesh)
  {
    if (mesh.Physics is not { IsEmpty: false } setup || mesh.Bones.Count == 0)
      return null;
    var runtime = PhysicsCache.GetValue(mesh, m => new LibSaber.SM2.Assets.Physics.PhysicsRuntime(m, setup));
    if (!ReferenceEquals(runtime.Setup, setup))
    {
      PhysicsCache.AddOrUpdate(mesh, runtime = new LibSaber.SM2.Assets.Physics.PhysicsRuntime(mesh, setup));
    }

    runtime.Track(mesh.Clip);
    return runtime.IsEmpty ? null : runtime;
  }

  private static PreviewBlend BlendOf(MeshPart part)
    => part.Sfx ? PreviewBlend.Sfx : part.Blend.ToLowerInvariant() switch
    {
      "translucent" => PreviewBlend.Translucent,
      "masked" => PreviewBlend.Masked,
      _ => PreviewBlend.Opaque,
    };

  private static bool AlphaChannel(MeshPart part)
  {
    var diffuse = part.Maps.FirstOrDefault(map => map.Role == "diffuse");
    var channels = diffuse?.Channels ?? "";
    return channels.Contains("AKill", StringComparison.OrdinalIgnoreCase) || channels.Contains("Transp", StringComparison.OrdinalIgnoreCase);
  }

  private static readonly ConditionalWeakTable<object, Dictionary<int, Vector3>> Centers = new();

  private static Vector3 PartCenter(Prepared prepared, int start, int end)
  {
    var cache = Centers.GetOrCreateValue(prepared);
    lock (cache)
    {
      if (cache.TryGetValue(start, out var known))
        return known;
      var sum = Vector3.Zero;
      var n = 0;
      var positions = prepared.Positions;
      for (var i = start; i < end; i += 7)
      {
        var index = prepared.Indices[i];
        if ((uint)index >= (uint)positions.Length)
          continue;
        sum += positions[index];
        n++;
      }

      var center = n == 0 ? Vector3.Zero : sum / n;
      cache[start] = center;
      return center;
    }
  }
}
