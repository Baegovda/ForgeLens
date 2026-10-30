using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>World-space point light for the level preview (colour RGB 0..n, radius metres).</summary>
public readonly record struct PreviewPointLight(Vector3 Position, Vector3 Colour, float Radius);

public readonly struct OrbitCamera
{
  public float Yaw { get; init; }
  public float Pitch { get; init; }
  public float Distance { get; init; }
  /// <summary>Orbit pivot. Null orbits the centre of the drawn bounds.</summary>
  public Vector3? Target { get; init; }
}

public sealed class PreviewImage
{
  public int Width { get; init; }
  public int Height { get; init; }
  public byte[] Bgra { get; init; } = Array.Empty<byte>();
  /// <summary><c>.td</c> format_descr when this image is a packed mask.</summary>
  public string Channels { get; init; } = "";
}


public sealed record PreviewSettings
{
  public int Lod { get; init; }
  /// <summary>Index into the active LOD's parts. Negative draws every part.</summary>
  public int Part { get; init; } = -1;
  public bool Wireframe { get; init; }
  public IReadOnlyDictionary<string, PreviewImage>? Albedo { get; init; }
  /// <summary>Normal maps keyed by texture name. Sampled in tangent space and lit with the albedo.</summary>
  public IReadOnlyDictionary<string, PreviewImage>? Normals { get; init; }
  /// <summary>Packed specular maps. <see cref="PreviewImage.Channels"/> selects metal, roughness, and AO.</summary>
  public IReadOnlyDictionary<string, PreviewImage>? Specular { get; init; }
  public int MaxEdge { get; init; } = 1280;
  /// <summary>Optional world-space point lights (level preview, R54). Null keeps the default head/fill lighting unchanged.</summary>
  public IReadOnlyList<PreviewPointLight>? PointLights { get; init; }
  /// <summary>Ambient term used with <see cref="PointLights"/> (replaces the 0.16 base).</summary>
  public float Ambient { get; init; } = 0.16f;
  /// <summary>Headlight weight used with <see cref="PointLights"/>.</summary>
  public float HeadLight { get; init; } = 1f;
  /// <summary>Final multiplier used with <see cref="PointLights"/>.</summary>
  public float Exposure { get; init; } = 1f;
  /// <summary>Supersampling factor per axis (1 = off, 2 = 4 samples per pixel). Box-filtered down.</summary>
  public int Supersample { get; init; } = 1;
  /// <summary>Rebase the clip so its first frame sits where the bind pose stands (cinematic clips stay in view).</summary>
  public bool InPlace { get; init; }
  /// <summary>Upper bound on worker threads; 0 uses every core.</summary>
  public int Threads { get; init; }
  /// <summary>Run the class's cloth and jiggle physics (<see cref="ExportedMesh.Physics"/>) on the posed skeleton.</summary>
  public bool Physics { get; init; }
  /// <summary>
  ///   R75 (t157u) orbit physics: the model's world frame for the simulation (row-vector, model -> sim world). The
  ///   viewer passes the inverse orbit yaw so turning the model gives cloth / jiggle inertial lag; drawing stays in
  ///   model space (results are mapped back with the inverse). Null = identity (old behaviour).
  /// </summary>
  public System.Numerics.Matrix4x4? PhysicsFrame { get; init; }
  /// <summary>R75: wall-clock seconds for the physics step (sim keeps running while the clip is paused). Null = clip time.</summary>
  public float? PhysicsClock { get; init; }
  /// <summary>R83 (UE "View Mode") on the CPU path: 0 Lit, 1 Unlit, 2 Clay, 3 Normals (world, after the normal map), 4 UV checker. Same meaning as GlPreview.ViewMode.</summary>
  public int ViewMode { get; init; }
}
/// <summary>
///   CPU preview renderer. Every frame it skins (only when a clip is active), projects each vertex once,
///   bins triangles into horizontal bands and rasterises the bands in parallel. Shading is per pixel with
///   interpolated vertex normals (smooth, as the game's vertex shader passes them), perspective-correct UVs,
///   bilinear sampling from a cached box-filtered mip chain chosen per triangle, Toksvig specular AA from the
///   normal-map mip length, and optional supersampling that is box-filtered down.
/// </summary>
public static partial class MeshPreview
{
  public static double LastRenderMs { get; private set; }

  /// <summary>Per-stage milliseconds of the last frame, for the bench and the status line.</summary>
  public static string LastStages { get; private set; } = "";

  public static byte[] Render(
    ExportedMesh mesh,
    int width,
    int height,
    OrbitCamera camera,
    bool showSkeleton,
    float timeSeconds,
    out string note,
    PreviewSettings? settings = null)
  {
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var maxEdge = Math.Clamp(settings?.MaxEdge ?? 1280, 8, 4096);
    width = Math.Clamp(width, 8, maxEdge);
    height = Math.Clamp(height, 8, maxEdge);
    var ss = Math.Clamp(settings?.Supersample ?? 1, 1, 3);
    var w = width * ss;
    var h = height * ss;
    var target = new Target(w, h);
    note = mesh.Message;
    var lod = Math.Max(0, settings?.Lod ?? 0);
    var partFilter = settings?.Part ?? -1;
    var honorVisibility = partFilter < 0 && lod <= 0;
    var slices = CollectSlices(mesh, lod, partFilter, honorVisibility);
    var parallel = new ParallelOptions { MaxDegreeOfParallelism = settings?.Threads > 0 ? settings.Threads : Environment.ProcessorCount };
    if (slices.Count == 0 && mesh.Bones.Count == 0)
    {
      note = string.IsNullOrEmpty(note) ? "Nothing to draw." : note;
      return target.Resolve(width, height, ss);
    }

    var clipActive = mesh.Bones.Count > 0 && mesh.Animated;
    Matrix4x4[]? worlds = clipActive || showSkeleton ? BoneWorlds(mesh, timeSeconds) : null;
    var rebase = Matrix4x4.Identity;
    if (clipActive && settings?.InPlace == true && mesh.Clip is { HasCurves: true })
    {
      rebase = InPlaceRebase(mesh);
      for (var i = 0; i < worlds!.Length; i++)
        worlds[i] *= rebase;
      RemoveRootMotion(mesh, worlds, rebase);
    }

    var offset = Vector3.Zero;

    var tBounds = watch.Elapsed.TotalMilliseconds;
    if (!FastBounds(slices, partFilter, honorVisibility, out var center, out var extent))
    {
      if (mesh.Bones.Count == 0)
      {
        note = string.IsNullOrEmpty(note) ? "Nothing to draw." : note;
        return target.Resolve(width, height, ss);
      }

      var min = new Vector3(float.MaxValue);
      var max = new Vector3(float.MinValue);
      foreach (var world in worlds ?? BoneWorlds(mesh, timeSeconds))
      {
        var point = Translation(world) + offset;
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
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
    var viewProjection = view * projection;
    var points = settings?.PointLights is { } pl ? PointGrid.Build(pl, settings.Ambient, settings.HeadLight, settings.Exposure) : null;
    var light = new Lights
    {
      Key = Vector3.Normalize((eye - center) + Vector3.UnitY * distance * 0.45f),
      Fill = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, eye - center) + Vector3.UnitY * 0.25f),
      Eye = eye,
    };

    Matrix4x4[]? skinMatrices = null;
    if (clipActive)
    {
      skinMatrices = new Matrix4x4[mesh.Bones.Count];
      for (var i = 0; i < skinMatrices.Length; i++)
        skinMatrices[i] = mesh.Bones[i].InverseBind * worlds![i];
    }

    double msSetup = watch.Elapsed.TotalMilliseconds, msSkin = 0, msProject = 0, msBin = 0, msRaster = 0;
    foreach (var slice in slices)
    {
      var stage = watch.Elapsed.TotalMilliseconds;
      var prepared = Prepare(slice);
      var positions = prepared.Positions;
      var normals = prepared.Normals;
      if (skinMatrices is not null && prepared.Skin is not null)
        Skin(prepared, skinMatrices, offset, parallel, out positions, out normals);

      msSkin += watch.Elapsed.TotalMilliseconds - stage;
      stage = watch.Elapsed.TotalMilliseconds;
      var vertexCount = positions.Length;
      var sx = new float[vertexCount];
      var sy = new float[vertexCount];
      var sz = new float[vertexCount];
      var iw = new float[vertexCount];
      Parallel.For(0, (vertexCount + 4095) / 4096, parallel, chunk =>
      {
        var end = Math.Min(vertexCount, (chunk + 1) * 4096);
        for (var i = chunk * 4096; i < end; i++)
        {
          var clip = Vector4.Transform(new Vector4(positions[i], 1), viewProjection);
          if (clip.W <= 0.0001f)
          {
            iw[i] = 0;
            continue;
          }

          var inv = 1f / clip.W;
          sx[i] = (clip.X * inv * 0.5f + 0.5f) * w;
          sy[i] = (1f - (clip.Y * inv * 0.5f + 0.5f)) * h;
          sz[i] = clip.Z * inv;
          iw[i] = inv;
        }
      });

      msProject += watch.Elapsed.TotalMilliseconds - stage;
      stage = watch.Elapsed.TotalMilliseconds;
      // Part state for this frame: visibility and resolved textures.
      var partCount = slice.Parts.Count;
      var partDraw = new bool[Math.Max(1, partCount)];
      var partMat = new PartMaterial[Math.Max(1, partCount)];
      for (var p = 0; p < partCount; p++)
      {
        var part = slice.Parts[p];
        partDraw[p] = (partFilter < 0 || p == partFilter) && (!honorVisibility || part.Visible);
        var shift = p * 17 % 36 - 12;
        partMat[p] = new PartMaterial
        {
          Albedo = Lookup(settings?.Albedo, part.Texture),
          Normal = Lookup(settings?.Normals, part.Normal),
          Specular = Lookup(settings?.Specular, part.Specular),
          SpecChannels = settings?.Specular is not null && part.Specular is not null && settings.Specular.TryGetValue(part.Specular, out var specImage) ? specImage.Channels : "",
          Solid = new Vector3(Math.Clamp(156 + shift, 48, 230), Math.Clamp(118 + shift / 2, 48, 210), Math.Clamp(96 + shift / 3, 48, 200)),
        }.Resolve();
      }

      if (partCount == 0)
      {
        partDraw[0] = true;
        partMat[0] = new PartMaterial { Solid = new Vector3(156, 118, 96) }.Resolve();
      }

      var triCount = prepared.Indices.Length / 3;
      var bandHeight = Math.Max(4, h / (parallel.MaxDegreeOfParallelism * 3));
      var bandCount = (h + bandHeight - 1) / bandHeight;
      var indices = prepared.Indices;
      var triPart = prepared.TriPart;
      // Bin triangles into bands in parallel: each chunk keeps its own band lists.
      var chunkCount = Math.Max(1, Math.Min(parallel.MaxDegreeOfParallelism * 2, (triCount + 8191) / 8192));
      var bins = new List<int>[chunkCount, bandCount];
      Parallel.For(0, chunkCount, parallel, chunk =>
      {
        var start = (int)((long)triCount * chunk / chunkCount);
        var end = (int)((long)triCount * (chunk + 1) / chunkCount);
        for (var t = start; t < end; t++)
        {
          var part = triPart[t];
          if (part < 0 || !partDraw[partCount == 0 ? 0 : part])
            continue;
          int a = indices[t * 3], b = indices[t * 3 + 1], c = indices[t * 3 + 2];
          if ((uint)a >= (uint)vertexCount || (uint)b >= (uint)vertexCount || (uint)c >= (uint)vertexCount)
            continue;
          if (iw[a] == 0 || iw[b] == 0 || iw[c] == 0)
            continue;
          var minY = MathF.Min(sy[a], MathF.Min(sy[b], sy[c]));
          var maxY = MathF.Max(sy[a], MathF.Max(sy[b], sy[c]));
          var minX = MathF.Min(sx[a], MathF.Min(sx[b], sx[c]));
          var maxX = MathF.Max(sx[a], MathF.Max(sx[b], sx[c]));
          if (maxY < 0 || minY >= h || maxX < 0 || minX >= w)
            continue;
          // Skip triangles that cover no pixel centre (sub-pixel slivers are the bulk of a 300k-triangle body).
          if ((int)MathF.Floor(maxX - 0.5f) < (int)MathF.Ceiling(minX - 0.5f) || (int)MathF.Floor(maxY - 0.5f) < (int)MathF.Ceiling(minY - 0.5f))
            continue;
          var b0 = Math.Clamp((int)minY / bandHeight, 0, bandCount - 1);
          var b1 = Math.Clamp((int)maxY / bandHeight, 0, bandCount - 1);
          for (var band = b0; band <= b1; band++)
            (bins[chunk, band] ??= new List<int>(64)).Add(t);
        }
      });

      msBin += watch.Elapsed.TotalMilliseconds - stage;
      stage = watch.Elapsed.TotalMilliseconds;
      var frame = new Frame
      {
        Target = target, Sx = sx, Sy = sy, Sz = sz, Iw = iw,
        Positions = positions, Normals = normals, Uvs = prepared.Uvs, Indices = indices, TriPart = triPart,
        Materials = partMat, Light = light, PartCountZero = partCount == 0, Points = points, ViewMode = settings?.ViewMode ?? 0,
      };
      Parallel.For(0, bandCount, parallel, band =>
      {
        var y0 = band * bandHeight;
        var y1 = Math.Min(h, y0 + bandHeight);
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
          var list = bins[chunk, band];
          if (list is null)
            continue;
          foreach (var t in list)
            RasterTriangle(frame, t, y0, y1);
        }
      });
      msRaster += watch.Elapsed.TotalMilliseconds - stage;

      if (settings?.Wireframe == true)
      {
        var thick = ss;
        for (var t = 0; t < triCount; t++)
        {
          var part = triPart[t];
          if (part < 0 || !partDraw[partCount == 0 ? 0 : part])
            continue;
          int a = indices[t * 3], b = indices[t * 3 + 1], c = indices[t * 3 + 2];
          if ((uint)a >= (uint)vertexCount || (uint)b >= (uint)vertexCount || (uint)c >= (uint)vertexCount)
            continue;
          if (iw[a] == 0 || iw[b] == 0 || iw[c] == 0)
            continue;
          Line(target.Pixels, w, h, (int)sx[a], (int)sy[a], (int)sx[b], (int)sy[b], 210, 220, 230);
          Line(target.Pixels, w, h, (int)sx[b], (int)sy[b], (int)sx[c], (int)sy[c], 210, 220, 230);
          Line(target.Pixels, w, h, (int)sx[c], (int)sy[c], (int)sx[a], (int)sy[a], 210, 220, 230);
        }
      }
    }

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
        if (!Project(Translation(worlds[parent]) + offset, viewProjection, w, h, out var x0, out var y0, out _))
          continue;
        if (!Project(Translation(worlds[i]) + offset, viewProjection, w, h, out var x1, out var y1, out _))
          continue;
        for (var k = 0; k < ss; k++)
          Line(target.Pixels, w, h, (int)x0 + k, (int)y0, (int)x1 + k, (int)y1, 80, 190, 230);
      }
    }

    var beforeResolve = watch.Elapsed.TotalMilliseconds;
    var result = target.Resolve(width, height, ss);
    LastRenderMs = watch.Elapsed.TotalMilliseconds;
    LastStages = $"clear+bounds {msSetup:0.0} (bounds {msSetup - tBounds:0.0}) prep/skin {msSkin:0.0} project {msProject:0.0} bin {msBin:0.0} raster {msRaster:0.0} resolve {LastRenderMs - beforeResolve:0.0}";
    return result;
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AnimationClip, Tuple<ExportedMesh, Matrix4x4>> RebaseCache = new();

  /// <summary>
  ///   Rigid transform that puts the clip's first frame where the bind pose stands, measured on the topmost
  ///   deforming bone (pelvis/root). Cinematic clips carry the scene placement in that bone, which otherwise
  ///   moves the model out of the orbit camera. Applied to every bone world for every frame of the clip.
  /// </summary>
  public static Matrix4x4 InPlaceRebase(ExportedMesh mesh)
  {
    if (mesh.Bones.Count == 0 || mesh.Clip is not { HasCurves: true } clip)
      return Matrix4x4.Identity;
    lock (RebaseCache)
    {
      if (RebaseCache.TryGetValue(clip, out var cached) && ReferenceEquals(cached.Item1, mesh))
        return cached.Item2;
    }

    var first = BoneWorlds(mesh, 0f);
    mesh.Clip = null;
    Matrix4x4[] bind;
    try
    {
      bind = BoneWorlds(mesh, 0f);
    }
    finally
    {
      mesh.Clip = clip;
    }

    // Reference: the topmost bone among those the clip displaces most. Bones the clip does not name stay
    // on the bind pose, so the scene placement lives on whichever animated bone carries the whole body.
    var displacement = new float[first.Length];
    var maxDisplacement = 0f;
    for (var i = 0; i < first.Length; i++)
    {
      displacement[i] = Vector3.Distance(Translation(first[i]), Translation(bind[i]));
      if (float.IsFinite(displacement[i]))
        maxDisplacement = MathF.Max(maxDisplacement, displacement[i]);
    }

    var reference = -1;
    var bestDepth = int.MaxValue;
    // Only bones that carry deforming bones count: cam_anim / Gun_pose style helpers can sit metres away.
    var carries = new bool[mesh.Bones.Count];
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      if (!mesh.Bones[i].Influences)
        continue;
      for (var p = i; p >= 0 && p < mesh.Bones.Count && !carries[p]; p = mesh.Bones[p].Parent)
        carries[p] = true;
    }

    if (carries.Any(c => c))
    {
      maxDisplacement = 0f;
      for (var i = 0; i < first.Length; i++)
        if (carries[i] && float.IsFinite(displacement[i]))
          maxDisplacement = MathF.Max(maxDisplacement, displacement[i]);
    }
    else
      Array.Fill(carries, true);

    // Gameplay clips are authored in model space: ROOT (and its parents) sit on the bind pose at frame 0 and
    // only the body below moves. Re-basing on the pelvis there would tilt and shift the whole model.
    var rootBone = RootMotionBone(mesh);
    if (rootBone >= 0 && carries[rootBone])
    {
      var placed = false;
      for (var p = rootBone; p >= 0 && p < mesh.Bones.Count; p = mesh.Bones[p].Parent)
        placed |= displacement[p] > 0.05f;
      // Cinematic corpses carry the scene placement one level below ROOT (km away): keep re-basing those.
      var near = 0f;
      for (var i = 0; i < mesh.Bones.Count; i++)
        if (carries[i] && mesh.Bones[i].Parent == rootBone && float.IsFinite(displacement[i]))
          near = MathF.Max(near, displacement[i]);
      if (!placed && near < 1f)
        maxDisplacement = 0f;
    }

    if (maxDisplacement > 0.05f)
    {
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        if (!carries[i] || !(displacement[i] >= maxDisplacement * 0.5f))
          continue;
        var depth = 0;
        for (var p = mesh.Bones[i].Parent; p >= 0 && p < mesh.Bones.Count && depth < 1024; p = mesh.Bones[p].Parent)
          depth++;
        if (depth < bestDepth || (depth == bestDepth && mesh.Bones[i].Influences && reference >= 0 && !mesh.Bones[reference].Influences))
        {
          bestDepth = depth;
          reference = i;
        }
      }
    }

    var result = Matrix4x4.Identity;
    if (reference >= 0 && Matrix4x4.Invert(first[reference], out var inverse))
    {
      // Keep only rotation and translation so a scaled scene root cannot shrink the model.
      var candidate = inverse * bind[reference];
      if (Matrix4x4.Decompose(candidate, out _, out var rotation, out var translation))
        result = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation)) * Matrix4x4.CreateTranslation(translation);
      else
        result = candidate;
    }

    if (!float.IsFinite(result.M41) || !float.IsFinite(result.M11))
      result = Matrix4x4.Identity;
    lock (RebaseCache)
      RebaseCache.AddOrUpdate(clip, Tuple.Create(mesh, result));
    return result;
  }

  /// <summary>
  ///   In place, part 2: removes the clip's root motion. The horizontal (XZ) travel of the <c>ROOT</c> bone since
  ///   frame 0 is subtracted from every (already rebased) bone world, so dashes and walks stay on the spot; height
  ///   (jumps) is kept. <paramref name="worlds"/> must be the rebased worlds of <paramref name="timeSeconds"/>.
  /// </summary>
  public static void RemoveRootMotion(ExportedMesh mesh, Matrix4x4[] worlds, Matrix4x4 rebase)
  {
    if (mesh.Clip is not { HasCurves: true } clip)
      return;
    var root = RootMotionBone(mesh);
    if (root < 0 || root >= worlds.Length)
      return;
    Vector3 start;
    lock (RootStartCache)
    {
      if (!RootStartCache.TryGetValue(clip, out var cached) || !ReferenceEquals(cached.Item1, mesh))
      {
        cached = Tuple.Create(mesh, Translation(BoneWorlds(mesh, 0f)[root] * rebase));
        RootStartCache.AddOrUpdate(clip, cached);
      }

      start = cached.Item2;
    }

    var delta = Translation(worlds[root]) - start;
    delta.Y = 0;
    if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Z) || delta.LengthSquared() < 1e-8f)
      return;
    var shift = Matrix4x4.CreateTranslation(-delta);
    for (var i = 0; i < worlds.Length; i++)
      worlds[i] *= shift;
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AnimationClip, Tuple<ExportedMesh, Vector3>> RootStartCache = new();

  /// <summary>The <c>ROOT</c> bone (SM2 rigs carry locomotion there), or -1.</summary>
  public static int RootMotionBone(ExportedMesh mesh)
  {
    for (var i = 0; i < mesh.Bones.Count; i++)
      if (string.Equals(mesh.Bones[i].Name, "ROOT", StringComparison.OrdinalIgnoreCase))
        return i;
    return -1;
  }

  /// <summary>Bounds of the drawn triangles from the cached arrays (bind pose, so the camera does not chase a clip).</summary>
  private static bool FastBounds(List<DrawSlice> slices, int partFilter, bool honorVisibility, out Vector3 center, out Vector3 extent)
  {
    var min = new Vector3(float.MaxValue);
    var max = new Vector3(float.MinValue);
    var any = false;
    foreach (var slice in slices)
    {
      var prepared = Prepare(slice);
      var parts = slice.Parts;
      var draw = new bool[Math.Max(1, parts.Count)];
      for (var p = 0; p < parts.Count; p++)
        draw[p] = (partFilter < 0 || p == partFilter) && (!honorVisibility || parts[p].Visible);
      if (parts.Count == 0)
        draw[0] = true;
      var positions = prepared.Positions;
      var indices = prepared.Indices;
      var triPart = prepared.TriPart;
      for (var t = 0; t < triPart.Length; t++)
      {
        var part = triPart[t];
        if (part < 0 || !draw[part])
          continue;
        for (var k = 0; k < 3; k++)
        {
          var index = indices[t * 3 + k];
          if ((uint)index >= (uint)positions.Length)
            continue;
          var point = positions[index];
          min = Vector3.Min(min, point);
          max = Vector3.Max(max, point);
          any = true;
        }
      }
    }

    if (!any)
    {
      center = Vector3.Zero;
      extent = Vector3.One;
      return false;
    }

    center = (min + max) * 0.5f;
    extent = Vector3.Max(max - min, new Vector3(0.05f));
    return true;
  }

  private static PreviewImage? Lookup(IReadOnlyDictionary<string, PreviewImage>? map, string? name)
    => map is not null && name is not null && map.TryGetValue(name, out var image) && image.Width > 0 && image.Height > 0
      && image.Bgra.Length >= image.Width * image.Height * 4 ? image : null;

  private sealed class Target
  {
    public readonly int Width;
    public readonly int Height;
    public readonly byte[] Pixels;
    public readonly float[] Depth;

    public Target(int width, int height)
    {
      Width = width;
      Height = height;
      Pixels = new byte[width * height * 4];
      Depth = new float[width * height];
      Array.Fill(Depth, float.PositiveInfinity);
      var bg = BitConverter.ToUInt32(new byte[] { 22, 24, 28, 255 });
      MemoryMarshalFill(Pixels, bg);
    }

    private static void MemoryMarshalFill(byte[] pixels, uint value)
      => System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(pixels.AsSpan()).Fill(value);

    public byte[] Resolve(int width, int height, int ss)
    {
      if (ss == 1)
        return Pixels;
      var output = new byte[width * height * 4];
      var inv = 1f / (ss * ss);
      Parallel.For(0, height, y =>
      {
        for (var x = 0; x < width; x++)
        {
          int b = 0, g = 0, r = 0;
          for (var j = 0; j < ss; j++)
          {
            var row = ((y * ss + j) * Width + x * ss) * 4;
            for (var i = 0; i < ss; i++)
            {
              b += Pixels[row + i * 4];
              g += Pixels[row + i * 4 + 1];
              r += Pixels[row + i * 4 + 2];
            }
          }

          var o = (y * width + x) * 4;
          output[o] = (byte)(b * inv + 0.5f);
          output[o + 1] = (byte)(g * inv + 0.5f);
          output[o + 2] = (byte)(r * inv + 0.5f);
          output[o + 3] = 255;
        }
      });
      return output;
    }
  }

  /// <summary>Uniform-grid index of point lights for per-pixel lookup.</summary>
  private sealed class PointGrid
  {
    public float Cell;
    public Dictionary<(int, int, int), List<int>> Cells = new();
    public PreviewPointLight[] Lights = Array.Empty<PreviewPointLight>();
    public float Ambient, Head, Exposure;
    public static PointGrid Build(IReadOnlyList<PreviewPointLight> lights, float ambient, float head, float exposure, float cell = 6f)
    {
      var g = new PointGrid { Cell = cell, Lights = lights.ToArray(), Ambient = ambient, Head = head, Exposure = exposure };
      for (var i = 0; i < g.Lights.Length; i++)
      {
        var l = g.Lights[i]; var r = l.Radius;
        var lo = Key(l.Position - new Vector3(r), cell); var hi = Key(l.Position + new Vector3(r), cell);
        if ((long)(hi.Item1 - lo.Item1 + 1) * (hi.Item2 - lo.Item2 + 1) * (hi.Item3 - lo.Item3 + 1) > 4096) continue;
        for (var x = lo.Item1; x <= hi.Item1; x++) for (var y = lo.Item2; y <= hi.Item2; y++) for (var z = lo.Item3; z <= hi.Item3; z++)
        {
          if (!g.Cells.TryGetValue((x, y, z), out var list)) g.Cells[(x, y, z)] = list = new List<int>();
          list.Add(i);
        }
      }
      return g;
    }
    public static (int, int, int) Key(Vector3 p, float cell) => ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Y / cell), (int)MathF.Floor(p.Z / cell));
    public Vector3 Shade(Vector3 pos, Vector3 n)
    {
      var sum = Vector3.Zero;
      if (!Cells.TryGetValue(Key(pos, Cell), out var list)) return sum;
      foreach (var i in list)
      {
        var l = Lights[i]; var d = l.Position - pos; var dist = d.Length();
        if (dist >= l.Radius) continue;
        var att = 1f - dist / l.Radius; att *= att;
        var ndl = dist > 1e-4f ? Math.Clamp(Vector3.Dot(n, d / dist), 0f, 1f) * 0.75f + 0.25f : 1f;
        sum += l.Colour * (att * ndl);
      }
      return sum;
    }
  }

  private struct Lights
  {
    public Vector3 Key;
    public Vector3 Fill;
    public Vector3 Eye;
  }

  private sealed class PartMaterial
  {
    public PreviewImage? Albedo;
    public PreviewImage? Normal;
    public PreviewImage? Specular;
    public string SpecChannels = "";
    public Vector3 Solid;
    public MipChain? AlbedoMips;
    public MipChain? NormalMips;
    public MipChain? SpecMips;
    public bool RoughInAlpha;
    public bool AoInBlue;

    public PartMaterial Resolve()
    {
      AlbedoMips = Albedo is null ? null : MipChain.For(Albedo);
      NormalMips = Normal is null ? null : MipChain.For(Normal);
      SpecMips = Specular is null ? null : MipChain.For(Specular);
      RoughInAlpha = SpecChannels.Contains("Roughness(a)", StringComparison.OrdinalIgnoreCase);
      AoInBlue = SpecChannels.Contains("AO(b)", StringComparison.OrdinalIgnoreCase) || SpecChannels.Length == 0;
      return this;
    }
  }

  private sealed class Frame
  {
    public required Target Target;
    public required float[] Sx, Sy, Sz, Iw;
    public required Vector3[] Positions;
    public required Vector3[] Normals;
    public required Vector2[] Uvs;
    public required int[] Indices;
    public required int[] TriPart;
    public required PartMaterial[] Materials;
    public required Lights Light;
    public bool PartCountZero;
    public PointGrid? Points;
    public int ViewMode;
  }

  private sealed class Prepared
  {
    public required Vector3[] Positions;
    public required Vector3[] Normals;
    public required Vector2[] Uvs;
    public required int[] Indices;
    public required int[] TriPart;
    public SkinWeights[]? Skin;
  }

  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Prepared> PreparedCache = new();

  private static Prepared Prepare(DrawSlice slice)
    => PreparedCache.GetValue(slice.Indices, _ =>
    {
      var positions = slice.Positions.ToArray();
      var normals = slice.Normals.Count == positions.Length ? slice.Normals.ToArray() : ComputeNormals(positions, slice.Indices);
      var uvs = slice.Uvs.Count == positions.Length ? slice.Uvs.ToArray() : new Vector2[positions.Length];
      var indices = slice.Indices.ToArray();
      var triPart = new int[indices.Length / 3];
      var parts = slice.Parts;
      if (parts.Count == 0)
        Array.Fill(triPart, 0);
      else
      {
        Array.Fill(triPart, -1);
        for (var p = 0; p < parts.Count; p++)
        {
          var start = Math.Max(0, parts[p].IndexStart);
          var end = Math.Min(indices.Length, parts[p].IndexStart + parts[p].IndexCount);
          for (var i = start - start % 3; i < end && i / 3 < triPart.Length; i += 3)
            triPart[i / 3] = p;
        }
      }

      return new Prepared
      {
        Positions = positions,
        Normals = normals,
        Uvs = uvs,
        Indices = indices,
        TriPart = triPart,
        Skin = slice.Skin.Count == positions.Length ? slice.Skin.ToArray() : null,
      };
    });

  private static Vector3[] ComputeNormals(Vector3[] positions, IReadOnlyList<int> indices)
  {
    var normals = new Vector3[positions.Length];
    for (var i = 0; i + 2 < indices.Count; i += 3)
    {
      int a = indices[i], b = indices[i + 1], c = indices[i + 2];
      if ((uint)a >= (uint)positions.Length || (uint)b >= (uint)positions.Length || (uint)c >= (uint)positions.Length)
        continue;
      var n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
      normals[a] += n;
      normals[b] += n;
      normals[c] += n;
    }

    for (var i = 0; i < normals.Length; i++)
      normals[i] = normals[i].LengthSquared() > 1e-12f ? Vector3.Normalize(normals[i]) : Vector3.UnitY;
    return normals;
  }

  private static void Skin(Prepared prepared, Matrix4x4[] matrices, Vector3 offset, ParallelOptions parallel, out Vector3[] positions, out Vector3[] normals)
  {
    var count = prepared.Positions.Length;
    var outPos = new Vector3[count];
    var outNrm = new Vector3[count];
    var src = prepared.Positions;
    var srcN = prepared.Normals;
    var skin = prepared.Skin!;
    Parallel.For(0, (count + 4095) / 4096, parallel, chunk =>
    {
      var end = Math.Min(count, (chunk + 1) * 4096);
      for (var i = chunk * 4096; i < end; i++)
      {
        var weights = skin[i];
        var total = weights.W0 + weights.W1 + weights.W2 + weights.W3;
        if (total <= 0)
        {
          outPos[i] = src[i] + offset;
          outNrm[i] = srcN[i];
          continue;
        }

        var p = Vector3.Zero;
        var n = Vector3.Zero;
        Accumulate(src[i], srcN[i], matrices, weights.J0, weights.W0, ref p, ref n);
        Accumulate(src[i], srcN[i], matrices, weights.J1, weights.W1, ref p, ref n);
        Accumulate(src[i], srcN[i], matrices, weights.J2, weights.W2, ref p, ref n);
        Accumulate(src[i], srcN[i], matrices, weights.J3, weights.W3, ref p, ref n);
        outPos[i] = p + offset;
        outNrm[i] = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : srcN[i];
      }
    });
    positions = outPos;
    normals = outNrm;
  }

  private static void Accumulate(Vector3 position, Vector3 normal, Matrix4x4[] matrices, int bone, float weight, ref Vector3 p, ref Vector3 n)
  {
    if (weight == 0)
      return;
    if ((uint)bone >= (uint)matrices.Length)
    {
      p += position * weight;
      n += normal * weight;
      return;
    }

    p += Vector3.Transform(position, matrices[bone]) * weight;
    n += Vector3.TransformNormal(normal, matrices[bone]) * weight;
  }

  private static bool Project(Vector3 position, Matrix4x4 viewProjection, int width, int height, out float x, out float y, out float z)
  {
    var clip = Vector4.Transform(new Vector4(position, 1), viewProjection);
    if (clip.W <= 0.0001f)
    {
      x = y = z = 0;
      return false;
    }

    x = (clip.X / clip.W * 0.5f + 0.5f) * width;
    y = (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * height;
    z = clip.Z / clip.W;
    return true;
  }

  private static void RasterTriangle(Frame f, int t, int bandY0, int bandY1)
  {
    var idx = f.Indices;
    int a = idx[t * 3], b = idx[t * 3 + 1], c = idx[t * 3 + 2];
    float ax = f.Sx[a], ay = f.Sy[a], bx = f.Sx[b], by = f.Sy[b], cx = f.Sx[c], cy = f.Sy[c];
    var area = Edge(ax, ay, bx, by, cx, cy);
    if (MathF.Abs(area) < 1e-6f)
      return;
    var target = f.Target;
    var width = target.Width;
    var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))));
    var maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))));
    var minY = Math.Max(bandY0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))));
    var maxY = Math.Min(bandY1 - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))));
    if (minX > maxX || minY > maxY)
      return;

    var material = f.Materials[f.PartCountZero ? 0 : f.TriPart[t]];
    var invArea = 1f / area;
    float az = f.Sz[a], bz = f.Sz[b], cz = f.Sz[c];
    float aw = f.Iw[a], bw = f.Iw[b], cw = f.Iw[c];
    var na = f.Normals[a];
    var nb = f.Normals[b];
    var nc = f.Normals[c];
    var ua = f.Uvs[a];
    var ub = f.Uvs[b];
    var uc = f.Uvs[c];
    var pa = f.Positions[a];
    var pb = f.Positions[b];
    var pc = f.Positions[c];

    // Per-triangle tangent frame from UV derivatives.
    var e1 = pb - pa;
    var e2 = pc - pa;
    var d1 = ub - ua;
    var d2 = uc - ua;
    var det = d1.X * d2.Y - d1.Y * d2.X;
    var tangent = Vector3.UnitX;
    var bitangent = Vector3.UnitZ;
    var hasFrame = false;
    if (MathF.Abs(det) > 1e-12f)
    {
      var invDet = 1f / det;
      tangent = (e1 * d2.Y - e2 * d1.Y) * invDet;
      bitangent = (e2 * d1.X - e1 * d2.X) * invDet;
      hasFrame = tangent.LengthSquared() > 1e-16f && bitangent.LengthSquared() > 1e-16f;
    }

    // Mip level: texels covered per screen pixel, constant across the triangle.
    var uvArea = MathF.Abs(det) * 0.5f;
    var screenArea = MathF.Abs(area) * 0.5f;
    var albedo = material.AlbedoMips;
    var normalMip = material.NormalMips;
    var specMip = material.SpecMips;
    var albedoLevel = albedo?.Level(uvArea, screenArea) ?? 0;
    var normalLevel = normalMip?.Level(uvArea, screenArea) ?? 0;
    var specLevel = specMip?.Level(uvArea, screenArea) ?? 0;
    var roughInAlpha = material.RoughInAlpha;
    var aoInBlue = material.AoInBlue;
    var light = f.Light;
    var view = light.Eye - (pa + pb + pc) / 3f;
    view = view.LengthSquared() < 1e-12f ? Vector3.UnitZ : Vector3.Normalize(view);
    var half = Vector3.Normalize(light.Key + view);
    var pixels = target.Pixels;
    var depth = target.Depth;

    for (var y = minY; y <= maxY; y++)
    {
      var py = y + 0.5f;
      var row = y * width;
      for (var x = minX; x <= maxX; x++)
      {
        var px = x + 0.5f;
        var w0 = Edge(bx, by, cx, cy, px, py) * invArea;
        var w1 = Edge(cx, cy, ax, ay, px, py) * invArea;
        var w2 = 1f - w0 - w1;
        if (w0 < 0 || w1 < 0 || w2 < 0)
          continue;
        var z = w0 * az + w1 * bz + w2 * cz;
        var index = row + x;
        if (z >= depth[index])
          continue;
        depth[index] = z;

        // Perspective-correct attribute weights.
        var q0 = w0 * aw;
        var q1 = w1 * bw;
        var q2 = w2 * cw;
        var qs = 1f / (q0 + q1 + q2);
        q0 *= qs;
        q1 *= qs;
        q2 *= qs;
        var normal = na * q0 + nb * q1 + nc * q2;
        normal = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;
        var u = ua.X * q0 + ub.X * q1 + uc.X * q2;
        var v = ua.Y * q0 + ub.Y * q1 + uc.Y * q2;

        var vm = f.ViewMode;
        if (vm == 4)
        {
          // R83 UV checker (same pattern as the GPU shader): 8x8 checker per UV tile, tinted by the UV.
          var fu = u - MathF.Floor(u); var fv = v - MathF.Floor(v);
          var ck = ((int)(fu * 8f) + (int)(fv * 8f)) % 2 == 0 ? 0.85f : 0.25f;
          var pxo = index * 4;
          pixels[pxo] = (byte)Math.Clamp(ck * (0.65f + 0.35f * 0.5f) * 255f, 0, 255);
          pixels[pxo + 1] = (byte)Math.Clamp(ck * (0.65f + 0.35f * fv) * 255f, 0, 255);
          pixels[pxo + 2] = (byte)Math.Clamp(ck * (0.65f + 0.35f * fu) * 255f, 0, 255);
          pixels[pxo + 3] = 255;
          continue;
        }
        Vector3 baseColor;
        if (vm == 2)
          baseColor = new Vector3(173f); // clay: linear 0.42 grey in sRGB bytes (GPU Clay)
        else if (albedo is not null)
          baseColor = albedo.Sample(albedoLevel, u, v, out _);
        else
          baseColor = material.Solid;
        if (vm == 1)
        {
          var pxo = index * 4;
          pixels[pxo] = (byte)Math.Clamp(baseColor.X, 0, 255);
          pixels[pxo + 1] = (byte)Math.Clamp(baseColor.Y, 0, 255);
          pixels[pxo + 2] = (byte)Math.Clamp(baseColor.Z, 0, 255);
          pixels[pxo + 3] = 255;
          continue;
        }

        var lit = normal;
        var gloss = 1f;
        if (normalMip is not null && hasFrame)
        {
          var bgr = normalMip.Sample(normalLevel, u, v, out _);
          var raw = new Vector3(bgr.Z, bgr.Y, bgr.X) * (2f / 255f) - Vector3.One;
          var len = raw.Length();
          if (len > 1e-4f)
          {
            // Gram-Schmidt the triangle tangent against the smooth normal.
            var tn = tangent - normal * Vector3.Dot(tangent, normal);
            var bn = bitangent - normal * Vector3.Dot(bitangent, normal);
            if (tn.LengthSquared() > 1e-16f && bn.LengthSquared() > 1e-16f)
            {
              var mapped = Vector3.Normalize(tn) * raw.X + Vector3.Normalize(bn) * raw.Y + normal * raw.Z;
              if (mapped.LengthSquared() > 1e-8f)
                lit = Vector3.Normalize(mapped);
            }

            gloss = Math.Clamp(len, 0.05f, 1f);
          }
        }

        if (vm == 3)
        {
          var pxo = index * 4;
          pixels[pxo] = (byte)Math.Clamp((lit.Z * 0.5f + 0.5f) * 255f, 0, 255);
          pixels[pxo + 1] = (byte)Math.Clamp((lit.Y * 0.5f + 0.5f) * 255f, 0, 255);
          pixels[pxo + 2] = (byte)Math.Clamp((lit.X * 0.5f + 0.5f) * 255f, 0, 255);
          pixels[pxo + 3] = 255;
          continue;
        }
        var metal = 0f;
        var rough = vm == 2 ? 0.6f : 0.45f;
        var ao = 1f;
        if (specMip is not null && vm != 2)
        {
          var s = specMip.Sample(specLevel, u, v, out var sa);
          metal = s.Z / 255f;
          rough = roughInAlpha ? sa / 255f : s.Y / 255f;
          ao = aoInBlue ? s.X / 255f : 1f;
        }

        var ndotl = Math.Clamp(Vector3.Dot(lit, light.Key), 0f, 1f);
        var ndotf = Math.Clamp(Vector3.Dot(lit, light.Fill), 0f, 1f);
        var ndoth = Math.Clamp(Vector3.Dot(lit, half), 0f, 1f);
        var exponent = 8f + (1f - Math.Clamp(rough, 0f, 1f)) * 56f;
        // Toksvig: a shortened averaged normal widens the lobe so mip-filtered bumps do not sparkle.
        exponent = gloss * exponent / (gloss + exponent * (1f - gloss));
        var spec = MathF.Pow(ndoth, exponent) * (0.04f + metal * 0.85f) * ndotl * ((exponent + 8f) / 64f);
        var diffuse = (0.16f + 0.72f * ndotl + 0.18f * ndotf) * (0.25f + 0.75f * ao);
        var pixel = index * 4;
        var specByte = spec * 255f;
        if (f.Points is { } pg)
        {
          // Level preview: ambient + weak headlight + nearby point lights (BGR order like baseColor), then exposure.
          var pos = pa * q0 + pb * q1 + pc * q2;
          var pc3 = pg.Shade(pos, lit);
          var head = (pg.Ambient + pg.Head * (0.5f * ndotl + 0.12f * ndotf)) * (0.25f + 0.75f * ao);
          var e = pg.Exposure;
          pixels[pixel] = (byte)Math.Clamp(baseColor.X * (head + pc3.Z) * e + specByte, 0, 255);
          pixels[pixel + 1] = (byte)Math.Clamp(baseColor.Y * (head + pc3.Y) * e + specByte, 0, 255);
          pixels[pixel + 2] = (byte)Math.Clamp(baseColor.Z * (head + pc3.X) * e + specByte, 0, 255);
          pixels[pixel + 3] = 255;
          continue;
        }
        pixels[pixel] = (byte)Math.Clamp(baseColor.X * diffuse + specByte, 0, 255);
        pixels[pixel + 1] = (byte)Math.Clamp(baseColor.Y * diffuse + specByte, 0, 255);
        pixels[pixel + 2] = (byte)Math.Clamp(baseColor.Z * diffuse + specByte, 0, 255);
        pixels[pixel + 3] = 255;
      }
    }
  }

  /// <summary>Box-filtered mip chain of a BGRA image, cached per image. Samples are bilinear with wrap.</summary>
  private sealed class MipChain
  {
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PreviewImage, MipChain> Cache = new();
    private readonly int[] _w;
    private readonly int[] _h;
    private readonly byte[][] _data;

    private MipChain(PreviewImage image)
    {
      var w = new List<int> { image.Width };
      var h = new List<int> { image.Height };
      var data = new List<byte[]> { image.Bgra };
      while (w[^1] > 1 || h[^1] > 1)
      {
        int pw = w[^1], ph = h[^1];
        int nw = Math.Max(1, pw / 2), nh = Math.Max(1, ph / 2);
        var src = data[^1];
        var dst = new byte[nw * nh * 4];
        for (var y = 0; y < nh; y++)
        {
          var y0 = Math.Min(ph - 1, y * 2);
          var y1 = Math.Min(ph - 1, y * 2 + 1);
          for (var x = 0; x < nw; x++)
          {
            var x0 = Math.Min(pw - 1, x * 2);
            var x1 = Math.Min(pw - 1, x * 2 + 1);
            for (var ch = 0; ch < 4; ch++)
            {
              var sum = src[(y0 * pw + x0) * 4 + ch] + src[(y0 * pw + x1) * 4 + ch] + src[(y1 * pw + x0) * 4 + ch] + src[(y1 * pw + x1) * 4 + ch];
              dst[(y * nw + x) * 4 + ch] = (byte)((sum + 2) >> 2);
            }
          }
        }

        w.Add(nw);
        h.Add(nh);
        data.Add(dst);
      }

      _w = w.ToArray();
      _h = h.ToArray();
      _data = data.ToArray();
    }

    public static MipChain For(PreviewImage image)
    {
      lock (Cache)
        return Cache.GetValue(image, img => new MipChain(img));
    }

    public int Level(float uvArea, float screenArea)
    {
      if (screenArea <= 1e-6f)
        return _w.Length - 1;
      var texels = uvArea * _w[0] * _h[0];
      var level = 0.5f * MathF.Log2(Math.Max(texels / screenArea, 1e-6f));
      return Math.Clamp((int)MathF.Round(level), 0, _w.Length - 1);
    }

    /// <summary>Returns (b, g, r) in 0..255 and alpha.</summary>
    public Vector3 Sample(int level, float u, float v, out float alpha)
    {
      var w = _w[level];
      var h = _h[level];
      var data = _data[level];
      u -= MathF.Floor(u);
      v -= MathF.Floor(v);
      var fx = u * w - 0.5f;
      var fy = v * h - 0.5f;
      var x0 = (int)MathF.Floor(fx);
      var y0 = (int)MathF.Floor(fy);
      var tx = fx - x0;
      var ty = fy - y0;
      var x1 = x0 + 1;
      var y1 = y0 + 1;
      x0 = ((x0 % w) + w) % w;
      x1 = ((x1 % w) + w) % w;
      y0 = ((y0 % h) + h) % h;
      y1 = ((y1 % h) + h) % h;
      var i00 = (y0 * w + x0) * 4;
      var i10 = (y0 * w + x1) * 4;
      var i01 = (y1 * w + x0) * 4;
      var i11 = (y1 * w + x1) * 4;
      float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
      var b = data[i00] * w00 + data[i10] * w10 + data[i01] * w01 + data[i11] * w11;
      var g = data[i00 + 1] * w00 + data[i10 + 1] * w10 + data[i01 + 1] * w01 + data[i11 + 1] * w11;
      var r = data[i00 + 2] * w00 + data[i10 + 2] * w10 + data[i01 + 2] * w01 + data[i11 + 2] * w11;
      alpha = data[i00 + 3] * w00 + data[i10 + 3] * w10 + data[i01 + 3] * w01 + data[i11 + 3] * w11;
      return new Vector3(b, g, r);
    }
  }

  public static float FitDistance(ExportedMesh mesh, float aspect, int lod = 0, int part = -1)
  {
    if (!TrySelect(mesh, Math.Max(0, lod), out var positions, out _, out _, out var indices, out var parts, out _))
      return 4f;
    return TryBounds(positions, indices, parts, part, out _, out var extent)
      ? FitDistance(extent, aspect)
      : 4f;
  }

  /// <summary>Centre of the drawn bounds (the pivot the renderer orbits when <see cref="OrbitCamera.Target"/> is null).</summary>
  public static Vector3 FitCentre(ExportedMesh mesh, int lod = 0, int part = -1)
  {
    if (!TrySelect(mesh, Math.Max(0, lod), out var positions, out _, out _, out var indices, out var parts, out _))
      return Vector3.Zero;
    return TryBounds(positions, indices, parts, part, out var c, out _) ? c : Vector3.Zero;
  }

  public static float FitDistance(Vector3 extent, float aspect)
  {
    var height = Math.Max(extent.Y, 0.05f);
    var width = Math.Max(Math.Max(extent.X, extent.Z), 0.05f);
    var tan = MathF.Tan(MathF.PI / 8f);
    var distY = (height * 0.5f) / tan;
    var distX = (width * 0.5f) / (tan * Math.Max(aspect, 0.05f));
    return Math.Max(distY, distX) * 1.22f;
  }

  private sealed class DrawSlice
  {
    public required IReadOnlyList<Vector3> Positions { get; init; }
    public required IReadOnlyList<Vector3> Normals { get; init; }
    public required IReadOnlyList<Vector2> Uvs { get; init; }
    public required IReadOnlyList<int> Indices { get; init; }
    public required IReadOnlyList<MeshPart> Parts { get; init; }
    public required IReadOnlyList<SkinWeights> Skin { get; init; }
  }

  /// <summary>
  ///   LOD 0 with every part selected draws each checked shell on every vertex pool:
  ///   Index's scene (LOD0 plus a checked sim shell, without _LOD or cdt). A lower LOD
  ///   draws that buffer so the dropdown can still inspect it. One part draws that shell
  ///   even when its checkbox is clear.
  /// </summary>
  private static List<DrawSlice> CollectSlices(ExportedMesh mesh, int lod, int partFilter, bool honorVisibility)
  {
    var slices = new List<DrawSlice>();
    void Add(int level)
    {
      if (!TrySelect(mesh, level, out var positions, out var normals, out var uvs, out var indices, out var parts, out var skin))
        return;
      if (honorVisibility && parts.Count > 0 && !parts.Any(part => part.Visible && part.IndexCount >= 3))
        return;
      slices.Add(new DrawSlice
      {
        Positions = positions,
        Normals = normals,
        Uvs = uvs,
        Indices = indices,
        Parts = parts,
        Skin = skin,
      });
    }

    if (!honorVisibility)
      Add(lod);
    else
    {
      Add(0);
      for (var level = 0; level < mesh.Levels.Count; level++)
        Add(level + 1);
    }

    return slices;
  }

  private static bool TrySliceBounds(IReadOnlyList<DrawSlice> slices, int partFilter, bool honorVisibility, out Vector3 center, out Vector3 extent)
  {
    var any = false;
    var min = new Vector3(float.MaxValue);
    var max = new Vector3(float.MinValue);
    void Include(Vector3 point)
    {
      any = true;
      min = Vector3.Min(min, point);
      max = Vector3.Max(max, point);
    }

    foreach (var slice in slices)
    {
      if (partFilter >= 0 && (uint)partFilter < (uint)slice.Parts.Count)
      {
        var part = slice.Parts[partFilter];
        var end = Math.Min(slice.Indices.Count, part.IndexStart + part.IndexCount);
        for (var i = Math.Max(0, part.IndexStart); i < end; i++)
        {
          var index = slice.Indices[i];
          if ((uint)index < (uint)slice.Positions.Count)
            Include(slice.Positions[index]);
        }

        continue;
      }

      if (!honorVisibility || slice.Parts.Count == 0)
      {
        foreach (var position in slice.Positions)
          Include(position);
        continue;
      }

      foreach (var part in slice.Parts)
      {
        if (!part.Visible || part.IndexCount <= 0)
          continue;
        var end = Math.Min(slice.Indices.Count, part.IndexStart + part.IndexCount);
        for (var i = Math.Max(0, part.IndexStart); i < end; i++)
        {
          var index = slice.Indices[i];
          if ((uint)index < (uint)slice.Positions.Count)
            Include(slice.Positions[index]);
        }
      }
    }

    if (!any)
    {
      center = Vector3.Zero;
      extent = Vector3.One;
      return false;
    }

    center = (min + max) * 0.5f;
    extent = Vector3.Max(max - min, new Vector3(0.05f));
    return true;
  }

  private static bool TrySelect(
    ExportedMesh mesh,
    int lod,
    out IReadOnlyList<Vector3> positions,
    out IReadOnlyList<Vector3> normals,
    out IReadOnlyList<Vector2> uvs,
    out IReadOnlyList<int> indices,
    out IReadOnlyList<MeshPart> parts,
    out IReadOnlyList<SkinWeights> skin)
  {
    if (lod <= 0)
    {
      positions = mesh.Positions;
      normals = mesh.Normals;
      uvs = mesh.Uvs;
      indices = mesh.Indices;
      parts = mesh.Parts;
      skin = mesh.Skin;
      return positions.Count > 0 || mesh.Bones.Count > 0;
    }

    if (lod - 1 >= mesh.Levels.Count)
    {
      positions = Array.Empty<Vector3>();
      normals = Array.Empty<Vector3>();
      uvs = Array.Empty<Vector2>();
      indices = Array.Empty<int>();
      parts = Array.Empty<MeshPart>();
      skin = Array.Empty<SkinWeights>();
      return false;
    }

    var level = mesh.Levels[lod - 1];
    positions = level.Positions;
    normals = level.Normals;
    uvs = level.Uvs;
    indices = level.Indices;
    parts = level.Parts;
    skin = level.Skin;
    return true;
  }
  public static Matrix4x4[] BoneWorlds(ExportedMesh mesh, float time)
  {
    var worlds = new Matrix4x4[mesh.Bones.Count];
    var local = new Matrix4x4[mesh.Bones.Count];
    if (mesh.Clip is { HasCurves: true } clip)
      ClipPose.Apply(mesh.Bones, clip, time, local);
    else
    {
      for (var i = 0; i < mesh.Bones.Count; i++)
        local[i] = mesh.Bones[i].Local;
    }

    // propanim: a held prop's own sequences over the character clip (explicit bone indices).
    if (mesh.Overlay is { HasCurves: true } overlay)
      ClipPose.ApplyOverlay(mesh.Bones, overlay, overlay.Duration > 0 && time > overlay.Duration ? time % overlay.Duration : time, local);

    var pending = new bool[local.Length];
    Array.Fill(pending, true);
    for (var guard = 0; guard < local.Length; guard++)
    {
      var progress = false;
      for (var i = 0; i < local.Length; i++)
      {
        if (!pending[i])
          continue;
        var parent = mesh.Bones[i].Parent;
        if (parent >= 0 && parent < local.Length && pending[parent])
          continue;
        worlds[i] = parent < 0 ? local[i] : local[i] * worlds[parent];
        pending[i] = false;
        progress = true;
      }

      if (!progress)
        break;
    }

    return worlds;
  }

  private static bool TryBounds(
    IReadOnlyList<Vector3> positions,
    IReadOnlyList<int> indices,
    IReadOnlyList<MeshPart> parts,
    int partFilter,
    out Vector3 center,
    out Vector3 extent)
  {
    var any = false;
    var min = new Vector3(float.MaxValue);
    var max = new Vector3(float.MinValue);
    void Include(Vector3 point)
    {
      any = true;
      min = Vector3.Min(min, point);
      max = Vector3.Max(max, point);
    }

    if (partFilter >= 0 && (uint)partFilter < (uint)parts.Count)
    {
      var part = parts[partFilter];
      var end = Math.Min(indices.Count, part.IndexStart + part.IndexCount);
      for (var i = Math.Max(0, part.IndexStart); i < end; i++)
      {
        var index = indices[i];
        if ((uint)index < (uint)positions.Count)
          Include(positions[index]);
      }
    }
    else
    {
      foreach (var position in positions)
        Include(position);
    }

    if (!any)
    {
      center = Vector3.Zero;
      extent = Vector3.One;
      return false;
    }

    center = (min + max) * 0.5f;
    extent = Vector3.Max(max - min, new Vector3(0.05f));
    return true;
  }

  private static Vector3 Translation(Matrix4x4 matrix)
    => new(matrix.M41, matrix.M42, matrix.M43);

  private static float Edge(float ax, float ay, float bx, float by, float cx, float cy)
    => (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

  private static void Line(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, byte b, byte g, byte r)
  {
    var dx = Math.Abs(x1 - x0);
    var dy = Math.Abs(y1 - y0);
    var sx = x0 < x1 ? 1 : -1;
    var sy = y0 < y1 ? 1 : -1;
    var err = dx - dy;
    while (true)
    {
      if ((uint)x0 < (uint)width && (uint)y0 < (uint)height)
      {
        var pixel = (y0 * width + x0) * 4;
        pixels[pixel] = b;
        pixels[pixel + 1] = g;
        pixels[pixel + 2] = r;
        pixels[pixel + 3] = 255;
      }

      if (x0 == x1 && y0 == y1)
        break;
      var e2 = 2 * err;
      if (e2 > -dy)
      {
        err -= dy;
        x0 += sx;
      }

      if (e2 < dx)
      {
        err += dx;
        y0 += sy;
      }
    }
  }

}
