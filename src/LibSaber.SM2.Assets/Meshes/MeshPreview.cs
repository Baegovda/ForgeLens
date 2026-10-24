using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

public readonly struct OrbitCamera
{
  public float Yaw { get; init; }
  public float Pitch { get; init; }
  public float Distance { get; init; }
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
    out string note)
  {
    width = Math.Clamp(width, 8, 1024);
    height = Math.Clamp(height, 8, 1024);
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

    if (!TryBounds(mesh, out var center, out var radius))
    {
      note = string.IsNullOrEmpty(note) ? "Nothing to draw." : note;
      return pixels;
    }

    var distance = camera.Distance > 0 ? camera.Distance : Math.Max(1f, radius * 2.8f);
    var pitch = Math.Clamp(camera.Pitch, -1.2f, 1.2f);
    var planar = new Vector3(
      MathF.Cos(pitch) * MathF.Sin(camera.Yaw),
      MathF.Cos(pitch) * MathF.Cos(camera.Yaw),
      MathF.Sin(pitch));
    var eye = center + planar * distance;
    var view = Matrix4x4.CreateLookAt(eye, center, Vector3.UnitZ);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, width / (float)height, Math.Max(0.01f, distance * 0.01f), distance * 20f);
    var viewProjection = view * projection;
    var light = Vector3.Normalize(eye - center);

    if (mesh.HasTriangles)
    {
      var positions = PosedPositions(mesh, timeSeconds);
      for (var i = 0; i + 2 < mesh.Indices.Count; i += 3)
      {
        var a = mesh.Indices[i];
        var b = mesh.Indices[i + 1];
        var c = mesh.Indices[i + 2];
        if ((uint)a >= (uint)positions.Count || (uint)b >= (uint)positions.Count || (uint)c >= (uint)positions.Count)
          continue;

        if (!Project(positions[a], viewProjection, width, height, out var ax, out var ay, out var az))
          continue;
        if (!Project(positions[b], viewProjection, width, height, out var bx, out var by, out var bz))
          continue;
        if (!Project(positions[c], viewProjection, width, height, out var cx, out var cy, out var cz))
          continue;

        var normal = Vector3.Normalize(Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]));
        if (float.IsNaN(normal.X))
          normal = Vector3.UnitZ;
        var shade = Math.Clamp(MathF.Abs(Vector3.Dot(normal, light)), 0.25f, 1f);
        var color = new byte[] { (byte)(70 * shade), (byte)(140 * shade), (byte)(210 * shade), 255 };
        Fill(pixels, depth, width, height, ax, ay, az, bx, by, bz, cx, cy, cz, color);
      }
    }

    if (showSkeleton && mesh.Bones.Count > 0)
    {
      var worlds = BoneWorlds(mesh, timeSeconds);
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        var parent = mesh.Bones[i].Parent;
        if (parent < 0 || parent >= mesh.Bones.Count)
          continue;
        var from = Translation(worlds[parent]);
        var to = Translation(worlds[i]);
        if (!Project(from, viewProjection, width, height, out var x0, out var y0, out _))
          continue;
        if (!Project(to, viewProjection, width, height, out var x1, out var y1, out _))
          continue;
        Line(pixels, width, height, (int)x0, (int)y0, (int)x1, (int)y1, new byte[] { 80, 220, 180, 255 });
      }
    }

    return pixels;
  }

  private static List<Vector3> PosedPositions(ExportedMesh mesh, float time)
  {
    if (!mesh.HasSkin || mesh.Clip is null || !mesh.Clip.HasCurves)
      return mesh.Positions;

    var worlds = BoneWorlds(mesh, time);
    var binds = new Matrix4x4[mesh.Bones.Count];
    for (var i = 0; i < binds.Length; i++)
      binds[i] = mesh.Bones[i].InverseBind * worlds[i];

    var posed = new List<Vector3>(mesh.Positions.Count);
    for (var i = 0; i < mesh.Positions.Count; i++)
    {
      var skin = mesh.Skin[i];
      var position = mesh.Positions[i];
      var accumulated = Vector3.Zero;
      accumulated += Transform(position, binds, skin.J0) * skin.W0;
      accumulated += Transform(position, binds, skin.J1) * skin.W1;
      accumulated += Transform(position, binds, skin.J2) * skin.W2;
      accumulated += Transform(position, binds, skin.J3) * skin.W3;
      var weight = skin.W0 + skin.W1 + skin.W2 + skin.W3;
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

  private static bool TryBounds(ExportedMesh mesh, out Vector3 center, out float radius)
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

    foreach (var position in mesh.Positions)
      Include(position);
    if (mesh.Positions.Count == 0)
    {
      var worlds = BoneWorlds(mesh, 0);
      foreach (var world in worlds)
        Include(Translation(world));
    }

    if (!any)
    {
      center = Vector3.Zero;
      radius = 1;
      return false;
    }

    center = (min + max) * 0.5f;
    radius = Math.Max(0.05f, (max - min).Length() * 0.5f);
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
    byte[] color)
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

        var inv = 1f / MathF.Abs(area);
        var z = (w0 * az + w1 * bz + w2 * cz) * inv;
        var index = y * width + x;
        if (z >= depth[index])
          continue;
        depth[index] = z;
        var pixel = index * 4;
        pixels[pixel] = color[0];
        pixels[pixel + 1] = color[1];
        pixels[pixel + 2] = color[2];
        pixels[pixel + 3] = color[3];
      }
    }
  }

  private static float Edge(float ax, float ay, float bx, float by, float cx, float cy)
    => (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

  private static void Line(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, byte[] color)
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
        pixels[pixel] = color[0];
        pixels[pixel + 1] = color[1];
        pixels[pixel + 2] = color[2];
        pixels[pixel + 3] = color[3];
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
