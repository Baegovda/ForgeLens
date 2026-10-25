using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

public readonly struct OrbitCamera
{
  public float Yaw { get; init; }
  public float Pitch { get; init; }
  public float Distance { get; init; }
}

public sealed class PreviewImage
{
  public int Width { get; init; }
  public int Height { get; init; }
  public byte[] Bgra { get; init; } = Array.Empty<byte>();
  /// <summary><c>.td</c> format_descr when this image is a packed mask.</summary>
  public string Channels { get; init; } = "";
}

public sealed class PreviewSettings
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
}

public static class MeshPreview
{

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
    var maxEdge = Math.Clamp(settings?.MaxEdge ?? 1280, 8, 2048);
    width = Math.Clamp(width, 8, maxEdge);
    height = Math.Clamp(height, 8, maxEdge);
    var pixels = new byte[width * height * 4];
    var depth = new float[width * height];
    for (var i = 0; i < pixels.Length; i += 4)
    {
      pixels[i] = 22;
      pixels[i + 1] = 24;
      pixels[i + 2] = 28;
      pixels[i + 3] = 255;
    }

    Array.Fill(depth, float.PositiveInfinity);
    note = mesh.Message;
    var lod = Math.Max(0, settings?.Lod ?? 0);
    var partFilter = settings?.Part ?? -1;
    var honorVisibility = partFilter < 0 && lod <= 0;
    var slices = CollectSlices(mesh, lod, partFilter, honorVisibility);
    if (slices.Count == 0 && mesh.Bones.Count == 0)
    {
      note = string.IsNullOrEmpty(note) ? "Nothing to draw." : note;
      return pixels;
    }

    if (!TrySliceBounds(slices, partFilter, honorVisibility, out var center, out var extent))
    {
      if (mesh.Bones.Count == 0)
      {
        note = string.IsNullOrEmpty(note) ? "Nothing to draw." : note;
        return pixels;
      }

      center = Vector3.Zero;
      extent = Vector3.One;
      var worldsOnly = BoneWorlds(mesh, timeSeconds);
      var min = new Vector3(float.MaxValue);
      var max = new Vector3(float.MinValue);
      foreach (var world in worldsOnly)
      {
        var point = Translation(world);
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
      }

      center = (min + max) * 0.5f;
      extent = Vector3.Max(max - min, new Vector3(0.05f));
    }

    var distance = camera.Distance > 0
      ? camera.Distance
      : FitDistance(extent, width / (float)height);
    var pitch = Math.Clamp(camera.Pitch, -1.2f, 1.2f);
    var planar = MathF.Cos(pitch);
    var eye = center + new Vector3(
      planar * MathF.Sin(camera.Yaw),
      MathF.Sin(pitch),
      planar * MathF.Cos(camera.Yaw)) * distance;
    var view = Matrix4x4.CreateLookAt(eye, center, Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, width / (float)height, Math.Max(0.01f, distance * 0.01f), distance * 40f);
    var viewProjection = view * projection;
    var key = Vector3.Normalize((eye - center) + Vector3.UnitY * distance * 0.45f);
    var fill = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, eye - center) + Vector3.UnitY * 0.25f);

    foreach (var slice in slices)
    {
      var posed = PosedPositions(mesh, slice.Positions, slice.Skin, timeSeconds);
      var indices = slice.Indices;
      var parts = slice.Parts;
      var normals = slice.Normals;
      var uvs = slice.Uvs;
      if (indices.Count >= 3)
      {
        var partIndex = 0;
        for (var i = 0; i + 2 < indices.Count; i += 3)
        {
          while (partIndex < parts.Count && i >= parts[partIndex].IndexStart + parts[partIndex].IndexCount)
            partIndex++;
          if (partFilter >= 0 && partIndex != partFilter)
            continue;
          if (honorVisibility && partIndex < parts.Count && !parts[partIndex].Visible)
            continue;
          if (partIndex < parts.Count && i < parts[partIndex].IndexStart)
            continue;

          var a = indices[i];
          var b = indices[i + 1];
          var c = indices[i + 2];
          if ((uint)a >= (uint)posed.Count || (uint)b >= (uint)posed.Count || (uint)c >= (uint)posed.Count)
            continue;
          if (!Project(posed[a], viewProjection, width, height, out var ax, out var ay, out var az))
            continue;
          if (!Project(posed[b], viewProjection, width, height, out var bx, out var by, out var bz))
            continue;
          if (!Project(posed[c], viewProjection, width, height, out var cx, out var cy, out var cz))
            continue;

          var normal = normals.Count == posed.Count
            ? normals[a] + normals[b] + normals[c]
            : Vector3.Cross(posed[b] - posed[a], posed[c] - posed[a]);
          if (normal.LengthSquared() < 1e-8f)
            normal = Vector3.UnitY;
          else
            normal = Vector3.Normalize(normal);
          var shade = Math.Clamp(
            0.24f + 0.68f * Math.Clamp(Vector3.Dot(normal, key), 0f, 1f) + 0.22f * Math.Clamp(Vector3.Dot(normal, fill), 0f, 1f),
            0f, 1f);
          var shift = partIndex * 17 % 36 - 12;
          byte br = (byte)Math.Clamp(96 + shift / 3, 48, 200);
          byte bg = (byte)Math.Clamp(118 + shift / 2, 48, 210);
          byte bb = (byte)Math.Clamp(156 + shift, 48, 230);
          PreviewImage? albedo = null;
          PreviewImage? normalMap = null;
          PreviewImage? specularMap = null;
          if (partIndex < parts.Count)
          {
            var part = parts[partIndex];
            if (settings?.Albedo is { } textures && part.Texture is { } textureName && textures.TryGetValue(textureName, out var image))
              albedo = image;
            if (settings?.Normals is { } normalTextures && part.Normal is { } normalName && normalTextures.TryGetValue(normalName, out var normalImage))
              normalMap = normalImage;
            if (settings?.Specular is { } specTextures && part.Specular is { } specName && specTextures.TryGetValue(specName, out var specImage))
              specularMap = specImage;
          }

          if (albedo is null)
          {
            Fill(pixels, depth, width, height, ax, ay, az, bx, by, bz, cx, cy, cz,
              (byte)(bb * shade), (byte)(bg * shade), (byte)(br * shade));
          }
          else
          {
            var au = (uint)a < (uint)uvs.Count ? uvs[a].X : 0;
            var av = (uint)a < (uint)uvs.Count ? uvs[a].Y : 0;
            var bu = (uint)b < (uint)uvs.Count ? uvs[b].X : 0;
            var bv = (uint)b < (uint)uvs.Count ? uvs[b].Y : 0;
            var cu = (uint)c < (uint)uvs.Count ? uvs[c].X : 0;
            var cv = (uint)c < (uint)uvs.Count ? uvs[c].Y : 0;
            if (normalMap is null && specularMap is null)
            {
              FillTextured(pixels, depth, width, height, ax, ay, az, bx, by, bz, cx, cy, cz,
                au, av, bu, bv, cu, cv, albedo, shade);
            }
            else
            {
              var geometric = normals.Count == posed.Count ? normals[a] : normal;
              FillLit(pixels, depth, width, height, ax, ay, az, bx, by, bz, cx, cy, cz,
                au, av, bu, bv, cu, cv, albedo, normalMap, specularMap,
                posed[a], posed[b], posed[c], geometric, key, eye);
            }
          }
        }
      }

      if (settings?.Wireframe == true && indices.Count >= 3)
      {
        var partIndex = 0;
        for (var i = 0; i + 2 < indices.Count; i += 3)
        {
          while (partIndex < parts.Count && i >= parts[partIndex].IndexStart + parts[partIndex].IndexCount)
            partIndex++;
          if (partFilter >= 0 && partIndex != partFilter)
            continue;
          if (honorVisibility && partIndex < parts.Count && !parts[partIndex].Visible)
            continue;
          var a = indices[i];
          var b = indices[i + 1];
          var c = indices[i + 2];
          if ((uint)a >= (uint)posed.Count || (uint)b >= (uint)posed.Count || (uint)c >= (uint)posed.Count)
            continue;
          if (!Project(posed[a], viewProjection, width, height, out var ax, out var ay, out _))
            continue;
          if (!Project(posed[b], viewProjection, width, height, out var bx, out var by, out _))
            continue;
          if (!Project(posed[c], viewProjection, width, height, out var cx, out var cy, out _))
            continue;
          Line(pixels, width, height, (int)ax, (int)ay, (int)bx, (int)by, 210, 220, 230);
          Line(pixels, width, height, (int)bx, (int)by, (int)cx, (int)cy, 210, 220, 230);
          Line(pixels, width, height, (int)cx, (int)cy, (int)ax, (int)ay, 210, 220, 230);
        }
      }
    }

    if (showSkeleton && mesh.Bones.Count > 0)
    {
      var deformOnly = false;
      var fileOnly = false;
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        deformOnly |= mesh.Bones[i].Influences;
        fileOnly |= mesh.Bones[i].FileBind;
      }

      var worlds = BoneWorlds(mesh, timeSeconds);
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        if (deformOnly && !mesh.Bones[i].Influences)
          continue;
        if (!deformOnly && fileOnly && !mesh.Bones[i].FileBind)
          continue;
        var parent = mesh.Bones[i].Parent;
        if (parent < 0 || parent >= mesh.Bones.Count)
          continue;
        if (!Project(Translation(worlds[parent]), viewProjection, width, height, out var x0, out var y0, out _))
          continue;
        if (!Project(Translation(worlds[i]), viewProjection, width, height, out var x1, out var y1, out _))
          continue;
        Line(pixels, width, height, (int)x0, (int)y0, (int)x1, (int)y1, 80, 190, 230);
      }
    }

    return pixels;
  }

  public static float FitDistance(ExportedMesh mesh, float aspect, int lod = 0, int part = -1)
  {
    if (!TrySelect(mesh, Math.Max(0, lod), out var positions, out _, out _, out var indices, out var parts, out _))
      return 4f;
    return TryBounds(positions, indices, parts, part, out _, out var extent)
      ? FitDistance(extent, aspect)
      : 4f;
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

  private static IReadOnlyList<Vector3> PosedPositions(
    ExportedMesh mesh,
    IReadOnlyList<Vector3> positions,
    IReadOnlyList<SkinWeights> skin,
    float time)
  {
    if (skin.Count != positions.Count || mesh.Bones.Count == 0 || mesh.Clip is null || !mesh.Clip.HasCurves)
      return positions;

    var worlds = BoneWorlds(mesh, time);
    var binds = new Matrix4x4[mesh.Bones.Count];
    for (var i = 0; i < binds.Length; i++)
      binds[i] = mesh.Bones[i].InverseBind * worlds[i];

    var posed = new List<Vector3>(positions.Count);
    for (var i = 0; i < positions.Count; i++)
    {
      var weights = skin[i];
      var position = positions[i];
      var accumulated = Transform(position, binds, weights.J0) * weights.W0;
      accumulated += Transform(position, binds, weights.J1) * weights.W1;
      accumulated += Transform(position, binds, weights.J2) * weights.W2;
      accumulated += Transform(position, binds, weights.J3) * weights.W3;
      var weight = weights.W0 + weights.W1 + weights.W2 + weights.W3;
      posed.Add(weight > 0 ? accumulated : position);
    }

    return posed;
  }

  private static Vector3 Transform(Vector3 position, Matrix4x4[] binds, int bone)
  {
    if ((uint)bone >= (uint)binds.Length)
      return position;
    return Vector3.Transform(position, binds[bone]);
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

  private static bool Project(Vector3 position, Matrix4x4 viewProjection, int width, int height, out float x, out float y, out float z)
  {
    var clip = Vector4.Transform(new Vector4(position, 1), viewProjection);
    if (clip.W <= 0.0001f)
    {
      x = y = z = 0;
      return false;
    }

    var ndcX = clip.X / clip.W;
    var ndcY = clip.Y / clip.W;
    x = (ndcX * 0.5f + 0.5f) * (width - 1);
    y = (1f - (ndcY * 0.5f + 0.5f)) * (height - 1);
    z = clip.Z / clip.W;
    return true;
  }

  private static void Fill(
    byte[] pixels, float[] depth, int width, int height,
    float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz,
    byte b, byte g, byte r)
  {
    var minX = Math.Clamp((int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))), 0, width - 1);
    var maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))), 0, width - 1);
    var minY = Math.Clamp((int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))), 0, height - 1);
    var maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))), 0, height - 1);
    var area = Edge(ax, ay, bx, by, cx, cy);
    if (MathF.Abs(area) < 0.0001f)
      return;

    for (var y = minY; y <= maxY; y++)
    {
      var row = y * width;
      for (var x = minX; x <= maxX; x++)
      {
        var px = x + 0.5f;
        var py = y + 0.5f;
        var w0 = Edge(bx, by, cx, cy, px, py);
        var w1 = Edge(cx, cy, ax, ay, px, py);
        var w2 = Edge(ax, ay, bx, by, px, py);
        if (area < 0)
        {
          w0 = -w0;
          w1 = -w1;
          w2 = -w2;
        }

        if (w0 < 0 || w1 < 0 || w2 < 0)
          continue;

        var z = (w0 * az + w1 * bz + w2 * cz) / MathF.Abs(area);
        var index = row + x;
        if (z >= depth[index])
          continue;
        depth[index] = z;
        var pixel = index * 4;
        pixels[pixel] = b;
        pixels[pixel + 1] = g;
        pixels[pixel + 2] = r;
        pixels[pixel + 3] = 255;
      }
    }
  }

  private static void FillTextured(
    byte[] pixels, float[] depth, int width, int height,
    float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz,
    float au, float av, float bu, float bv, float cu, float cv,
    PreviewImage image, float shade)
  {
    var minX = Math.Clamp((int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))), 0, width - 1);
    var maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))), 0, width - 1);
    var minY = Math.Clamp((int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))), 0, height - 1);
    var maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))), 0, height - 1);
    var area = Edge(ax, ay, bx, by, cx, cy);
    var absArea = MathF.Abs(area);
    if (absArea < 0.0001f || image.Width <= 0 || image.Height <= 0 || image.Bgra.Length < image.Width * image.Height * 4)
      return;

    for (var y = minY; y <= maxY; y++)
    {
      var row = y * width;
      for (var x = minX; x <= maxX; x++)
      {
        var px = x + 0.5f;
        var py = y + 0.5f;
        var w0 = Edge(bx, by, cx, cy, px, py);
        var w1 = Edge(cx, cy, ax, ay, px, py);
        var w2 = Edge(ax, ay, bx, by, px, py);
        if (area < 0)
        {
          w0 = -w0;
          w1 = -w1;
          w2 = -w2;
        }

        if (w0 < 0 || w1 < 0 || w2 < 0)
          continue;

        var inv = 1f / absArea;
        var z = (w0 * az + w1 * bz + w2 * cz) * inv;
        var index = row + x;
        if (z >= depth[index])
          continue;
        depth[index] = z;
        var u = (w0 * au + w1 * bu + w2 * cu) * inv;
        var v = (w0 * av + w1 * bv + w2 * cv) * inv;
        Sample(image, u, v, out var tb, out var tg, out var tr);
        var pixel = index * 4;
        pixels[pixel] = (byte)(tb * shade);
        pixels[pixel + 1] = (byte)(tg * shade);
        pixels[pixel + 2] = (byte)(tr * shade);
        pixels[pixel + 3] = 255;
      }
    }
  }

  private static void FillLit(
    byte[] pixels, float[] depth, int width, int height,
    float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz,
    float au, float av, float bu, float bv, float cu, float cv,
    PreviewImage albedo, PreviewImage? normalMap, PreviewImage? specular,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 geometric, Vector3 key, Vector3 eye)
  {
    var minX = Math.Clamp((int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))), 0, width - 1);
    var maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))), 0, width - 1);
    var minY = Math.Clamp((int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))), 0, height - 1);
    var maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))), 0, height - 1);
    var area = Edge(ax, ay, bx, by, cx, cy);
    var absArea = MathF.Abs(area);
    if (absArea < 0.0001f || albedo.Width <= 0 || albedo.Height <= 0 || albedo.Bgra.Length < albedo.Width * albedo.Height * 4)
      return;

    geometric = geometric.LengthSquared() < 1e-8f ? Vector3.UnitY : Vector3.Normalize(geometric);
    var tangent = Vector3.UnitX;
    var bitangent = Vector3.UnitZ;
    var e1 = p1 - p0;
    var e2 = p2 - p0;
    var d1 = new Vector2(bu - au, bv - av);
    var d2 = new Vector2(cu - au, cv - av);
    var det = d1.X * d2.Y - d1.Y * d2.X;
    if (MathF.Abs(det) > 1e-8f)
    {
      var invDet = 1f / det;
      var rawT = (e1 * d2.Y - e2 * d1.Y) * invDet;
      var rawB = (e2 * d1.X - e1 * d2.X) * invDet;
      rawT -= geometric * Vector3.Dot(rawT, geometric);
      if (rawT.LengthSquared() > 1e-8f)
        tangent = Vector3.Normalize(rawT);
      rawB -= geometric * Vector3.Dot(rawB, geometric);
      bitangent = rawB.LengthSquared() > 1e-8f ? Vector3.Normalize(rawB) : Vector3.Normalize(Vector3.Cross(geometric, tangent));
    }

    var view = eye - (p0 + p1 + p2) / 3f;
    view = view.LengthSquared() < 1e-8f ? Vector3.UnitZ : Vector3.Normalize(view);
    var half = Vector3.Normalize(key + view);
    var channels = specular?.Channels ?? "";
    var roughInAlpha = channels.Contains("Roughness(a)", StringComparison.OrdinalIgnoreCase);
    var aoInBlue = channels.Contains("AO(b)", StringComparison.OrdinalIgnoreCase) || channels.Length == 0;

    for (var y = minY; y <= maxY; y++)
    {
      var row = y * width;
      for (var x = minX; x <= maxX; x++)
      {
        var px = x + 0.5f;
        var py = y + 0.5f;
        var w0 = Edge(bx, by, cx, cy, px, py);
        var w1 = Edge(cx, cy, ax, ay, px, py);
        var w2 = Edge(ax, ay, bx, by, px, py);
        if (area < 0)
        {
          w0 = -w0;
          w1 = -w1;
          w2 = -w2;
        }

        if (w0 < 0 || w1 < 0 || w2 < 0)
          continue;

        var inv = 1f / absArea;
        var z = (w0 * az + w1 * bz + w2 * cz) * inv;
        var index = row + x;
        if (z >= depth[index])
          continue;
        depth[index] = z;
        var u = (w0 * au + w1 * bu + w2 * cu) * inv;
        var v = (w0 * av + w1 * bv + w2 * cv) * inv;
        Sample(albedo, u, v, out var tb, out var tg, out var tr);
        var lit = geometric;
        if (normalMap is not null && normalMap.Width > 0)
        {
          Sample(normalMap, u, v, out var nb, out var ng, out var nr);
          var nx = nr / 255f * 2f - 1f;
          var ny = ng / 255f * 2f - 1f;
          var nz = nb / 255f * 2f - 1f;
          var mapped = tangent * nx + bitangent * ny + geometric * nz;
          if (mapped.LengthSquared() > 1e-6f)
            lit = Vector3.Normalize(mapped);
        }

        var metal = 0f;
        var rough = 0.45f;
        var ao = 1f;
        if (specular is not null && specular.Width > 0)
        {
          Sample(specular, u, v, out var sb, out var sg, out var sr);
          var sa = Alpha(specular, u, v);
          metal = sr / 255f;
          rough = roughInAlpha ? sa / 255f : sg / 255f;
          ao = aoInBlue ? sb / 255f : 1f;
        }

        var ndotl = Math.Clamp(Vector3.Dot(lit, key), 0f, 1f);
        var ndoth = Math.Clamp(Vector3.Dot(lit, half), 0f, 1f);
        var shine = MathF.Pow(ndoth, 8f + (1f - Math.Clamp(rough, 0f, 1f)) * 56f);
        var spec = shine * (0.04f + metal * 0.85f) * ndotl;
        var diffuse = (0.18f + 0.82f * ndotl) * (0.25f + 0.75f * ao);
        var pixel = index * 4;
        pixels[pixel] = (byte)Math.Clamp(tb * diffuse + spec * 255f, 0, 255);
        pixels[pixel + 1] = (byte)Math.Clamp(tg * diffuse + spec * 255f, 0, 255);
        pixels[pixel + 2] = (byte)Math.Clamp(tr * diffuse + spec * 255f, 0, 255);
        pixels[pixel + 3] = 255;
      }
    }
  }

  private static void Sample(PreviewImage image, float u, float v, out byte b, out byte g, out byte r)
  {
    u -= MathF.Floor(u);
    v -= MathF.Floor(v);
    var x = Math.Clamp((int)(u * image.Width), 0, image.Width - 1);
    var y = Math.Clamp((int)(v * image.Height), 0, image.Height - 1);
    var texel = (y * image.Width + x) * 4;
    b = image.Bgra[texel];
    g = image.Bgra[texel + 1];
    r = image.Bgra[texel + 2];
  }

  private static byte Alpha(PreviewImage image, float u, float v)
  {
    u -= MathF.Floor(u);
    v -= MathF.Floor(v);
    var x = Math.Clamp((int)(u * image.Width), 0, image.Width - 1);
    var y = Math.Clamp((int)(v * image.Height), 0, image.Height - 1);
    var texel = (y * image.Width + x) * 4;
    return image.Bgra.Length > texel + 3 ? image.Bgra[texel + 3] : (byte)255;
  }

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
