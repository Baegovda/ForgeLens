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
}

public sealed class PreviewSettings
{
  public int Lod { get; init; }
  /// <summary>Index into the active LOD's parts. Negative draws every part.</summary>
  public int Part { get; init; } = -1;
  public bool Wireframe { get; init; }
  public IReadOnlyDictionary<string, PreviewImage>? Albedo { get; init; }
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
    if (!TrySelect(mesh, lod, out var positions, out var normals, out var uvs, out var indices, out var parts, out var skin))
    {
      note = string.IsNullOrEmpty(note) ? "Nothing to draw." : note;
      return pixels;
    }

    if (!TryBounds(positions, indices, parts, partFilter, out var center, out var extent))
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

    var posed = PosedPositions(mesh, positions, skin, timeSeconds);
    if (indices.Count >= 3)
    {
      var partIndex = 0;
      for (var i = 0; i + 2 < indices.Count; i += 3)
      {
        while (partIndex < parts.Count && i >= parts[partIndex].IndexStart + parts[partIndex].IndexCount)
          partIndex++;
        if (partFilter >= 0 && partIndex != partFilter)
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
        if (partIndex < parts.Count
          && settings?.Albedo is { } textures
          && parts[partIndex].Texture is { } textureName
          && textures.TryGetValue(textureName, out var image))
          albedo = image;

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
          FillTextured(pixels, depth, width, height, ax, ay, az, bx, by, bz, cx, cy, cz,
            au, av, bu, bv, cu, cv, albedo, shade);
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
    for (var i = 0; i < mesh.Bones.Count; i++)
      local[i] = mesh.Bones[i].Local;

    if (mesh.Clip is not null)
    {
      foreach (var track in mesh.Clip.Tracks)
      {
        var index = track.Bone;
        if (index < 0)
          index = mesh.Bones.FindIndex(bone => bone.Name == track.BoneName);
        if ((uint)index >= (uint)local.Length)
          continue;

        var translation = AnimationClip.Sample(track.Translation, Translation(local[index]), time);
        var rotation = AnimationClip.Sample(track.Rotation, Quaternion.Identity, time);
        var scale = AnimationClip.Sample(track.Scale, Vector3.One, time);
        if (track.Rotation.Count == 0)
          rotation = QuaternionFrom(local[index]);
        local[index] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
      }
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

  private static Quaternion QuaternionFrom(Matrix4x4 matrix)
    => Quaternion.CreateFromRotationMatrix(matrix);

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
