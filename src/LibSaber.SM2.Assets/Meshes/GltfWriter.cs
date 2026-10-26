using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

public static class GltfWriter
{

  public static void Write(string path, ExportedMesh mesh, ExportSpace space)
  {
    var bin = new MemoryStream();
    var views = new List<string>();
    var accessors = new List<string>();

    int Add(ReadOnlySpan<byte> bytes, int componentSize, int target)
    {
      while (bin.Length % componentSize != 0)
        bin.WriteByte(0);
      var offset = (int)bin.Length;
      bin.Write(bytes);
      views.Add(target == 0
        ? $"{{\"buffer\":0,\"byteOffset\":{offset},\"byteLength\":{bytes.Length}}}"
        : $"{{\"buffer\":0,\"byteOffset\":{offset},\"byteLength\":{bytes.Length},\"target\":{target}}}");
      return views.Count - 1;
    }

    int Accessor(int view, string type, int count, string component, string? extra = null)
    {
      var json = $"{{\"bufferView\":{view},\"componentType\":{component},\"count\":{count},\"type\":\"{type}\"{extra ?? ""}}}";
      accessors.Add(json);
      return accessors.Count - 1;
    }

    int? positionAccessor = null;
    int? normalAccessor = null;
    int? uvAccessor = null;
    int? jointAccessor = null;
    int? weightAccessor = null;
    int? indexAccessor = null;
    int? inverseAccessor = null;

    if (mesh.Positions.Count > 0)
    {
      var floats = new float[mesh.Positions.Count * 3];
      var min = new Vector3(float.MaxValue);
      var max = new Vector3(float.MinValue);
      for (var i = 0; i < mesh.Positions.Count; i++)
      {
        var point = AxisConvert.Point(mesh.Positions[i], space, gltf: true);
        floats[i * 3] = point.X;
        floats[i * 3 + 1] = point.Y;
        floats[i * 3 + 2] = point.Z;
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
      }

      var view = Add(AsBytes(floats), 4, 34962);
      positionAccessor = Accessor(view, "VEC3", mesh.Positions.Count, "5126",
        $",\"min\":[{F(min.X)},{F(min.Y)},{F(min.Z)}],\"max\":[{F(max.X)},{F(max.Y)},{F(max.Z)}]");
    }

    if (mesh.Normals.Count == mesh.Positions.Count && mesh.Positions.Count > 0)
    {
      var floats = new float[mesh.Normals.Count * 3];
      for (var i = 0; i < mesh.Normals.Count; i++)
      {
        var normal = Vector3.Normalize(AxisConvert.Direction(mesh.Normals[i], gltf: true));
        if (float.IsNaN(normal.X))
          normal = Vector3.UnitY;
        floats[i * 3] = normal.X;
        floats[i * 3 + 1] = normal.Y;
        floats[i * 3 + 2] = normal.Z;
      }

      normalAccessor = Accessor(Add(AsBytes(floats), 4, 34962), "VEC3", mesh.Normals.Count, "5126");
    }

    if (mesh.Uvs.Count == mesh.Positions.Count && mesh.Positions.Count > 0)
    {
      var floats = new float[mesh.Uvs.Count * 2];
      for (var i = 0; i < mesh.Uvs.Count; i++)
      {
        floats[i * 2] = mesh.Uvs[i].X;
        floats[i * 2 + 1] = mesh.Uvs[i].Y;
      }

      uvAccessor = Accessor(Add(AsBytes(floats), 4, 34962), "VEC2", mesh.Uvs.Count, "5126");
    }

    if (mesh.HasSkin)
    {
      var joints = new ushort[mesh.Skin.Count * 4];
      var weights = new float[mesh.Skin.Count * 4];
      for (var i = 0; i < mesh.Skin.Count; i++)
      {
        var skin = mesh.Skin[i];
        joints[i * 4] = skin.J0;
        joints[i * 4 + 1] = skin.J1;
        joints[i * 4 + 2] = skin.J2;
        joints[i * 4 + 3] = skin.J3;
        weights[i * 4] = skin.W0;
        weights[i * 4 + 1] = skin.W1;
        weights[i * 4 + 2] = skin.W2;
        weights[i * 4 + 3] = skin.W3;
      }

      jointAccessor = Accessor(Add(MemoryMarshalBytes(joints), 4, 34962), "VEC4", mesh.Skin.Count, "5123");
      weightAccessor = Accessor(Add(AsBytes(weights), 4, 34962), "VEC4", mesh.Skin.Count, "5126");

      var matrices = new float[mesh.Bones.Count * 16];
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        // Inverse binds come from a float inverse, so M44 drifts off 1 by ~1e-7.
        // glTF requires an affine matrix: perspective row 0,0,0 and M44 exactly 1.
        var bind = AxisConvert.Matrix(mesh.Bones[i].InverseBind, space, gltf: true);
        bind.M14 = 0;
        bind.M24 = 0;
        bind.M34 = 0;
        bind.M44 = 1;
        WriteColumnMajor(matrices, i * 16, bind);
      }
      inverseAccessor = Accessor(Add(AsBytes(matrices), 4, 0), "MAT4", mesh.Bones.Count, "5126");
    }

    if (mesh.Indices.Count > 0)
    {
      var indices = new uint[mesh.Indices.Count];
      for (var i = 0; i < indices.Length; i++)
        indices[i] = (uint)mesh.Indices[i];
      indexAccessor = Accessor(Add(MemoryMarshalBytes(indices), 4, 34963), "SCALAR", indices.Length, "5125");
    }

    var nodes = new List<string>();
    var roots = new List<int>();
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var bone = mesh.Bones[i];
      var matrix = AxisConvert.Matrix(bone.Local, space, gltf: true);
      var children = new List<int>();
      for (var child = 0; child < mesh.Bones.Count; child++)
      {
        if (mesh.Bones[child].Parent == i)
          children.Add(child);
      }

      Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation);
      var nodeJson = new StringBuilder();
      nodeJson.Append("{\"name\":").Append(Quote(bone.Name));
      nodeJson.Append(",\"translation\":[").Append(F(translation.X)).Append(',').Append(F(translation.Y)).Append(',').Append(F(translation.Z)).Append(']');
      nodeJson.Append(",\"rotation\":[").Append(F(rotation.X)).Append(',').Append(F(rotation.Y)).Append(',').Append(F(rotation.Z)).Append(',').Append(F(rotation.W)).Append(']');
      nodeJson.Append(",\"scale\":[").Append(F(scale.X)).Append(',').Append(F(scale.Y)).Append(',').Append(F(scale.Z)).Append(']');
      if (children.Count > 0)
        nodeJson.Append(",\"children\":[").Append(string.Join(',', children)).Append(']');
      nodeJson.Append('}');
      nodes.Add(nodeJson.ToString());
      if (bone.Parent < 0)
        roots.Add(i);
    }

    var meshNode = -1;
    string? meshesJson = null;
    if (positionAccessor is int positions)
    {
      var attributes = new StringBuilder();
      attributes.Append("{\"POSITION\":").Append(positions);
      if (normalAccessor is int normals)
        attributes.Append(",\"NORMAL\":").Append(normals);
      if (uvAccessor is int uvs)
        attributes.Append(",\"TEXCOORD_0\":").Append(uvs);
      if (jointAccessor is int joints && weightAccessor is int weights)
        attributes.Append(",\"JOINTS_0\":").Append(joints).Append(",\"WEIGHTS_0\":").Append(weights);
      attributes.Append('}');

      var primitives = new List<string>();
      if (indexAccessor is int indices)
        primitives.Add($"{{\"attributes\":{attributes},\"indices\":{indices},\"mode\":4}}");
      else
        primitives.Add($"{{\"attributes\":{attributes},\"mode\":0}}");

      if (primitives.Count > 0)
      {
        meshesJson = $"[{{\"name\":{Quote(mesh.Name)},\"primitives\":[{string.Join(',', primitives)}]}}]";
        meshNode = nodes.Count;
        var skin = mesh.HasSkin ? ",\"skin\":0" : "";
        nodes.Add($"{{\"name\":{Quote(mesh.Name)},\"mesh\":0{skin}}}");
        roots.Add(meshNode);
      }
    }

    string? skins = null;
    if (mesh.HasSkin && inverseAccessor is int inverse)
    {
      var joints = string.Join(',', Enumerable.Range(0, mesh.Bones.Count));
      skins = $"[{{\"inverseBindMatrices\":{inverse},\"joints\":[{joints}]}}]";
    }

    string? animations = null;
    if (mesh.Clip is { HasCurves: true } clip && mesh.Bones.Count > 0)
      animations = WriteAnimation(clip, mesh, space, bin, views, accessors);

    var json = new StringBuilder();
    json.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"LibSaber.SM2Browser\"},");
    json.Append("\"scene\":0,\"scenes\":[{\"nodes\":[").Append(string.Join(',', roots)).Append("]}],");
    if (meshesJson is not null)
      json.Append("\"meshes\":").Append(meshesJson).Append(',');
    json.Append("\"nodes\":[").Append(string.Join(',', nodes)).Append(']');
    if (skins is not null)
      json.Append(",\"skins\":").Append(skins);
    if (animations is not null)
      json.Append(",\"animations\":").Append(animations);
    json.Append(",\"buffers\":[{\"byteLength\":").Append(bin.Length).Append("}]");
    json.Append(",\"bufferViews\":[").Append(string.Join(',', views)).Append(']');
    json.Append(",\"accessors\":[").Append(string.Join(',', accessors)).Append("]}");

    var jsonBytes = Encoding.UTF8.GetBytes(json.ToString());
    var jsonPad = (4 - (jsonBytes.Length % 4)) % 4;
    var binBytes = bin.ToArray();
    var binPad = (4 - (binBytes.Length % 4)) % 4;
    var total = 12 + 8 + jsonBytes.Length + jsonPad + 8 + binBytes.Length + binPad;
    using var output = File.Create(path);
    using var writer = new BinaryWriter(output);
    writer.Write(0x46546C67);
    writer.Write(2);
    writer.Write(total);
    writer.Write(jsonBytes.Length + jsonPad);
    writer.Write(0x4E4F534A);
    writer.Write(jsonBytes);
    for (var i = 0; i < jsonPad; i++)
      writer.Write((byte)0x20);
    writer.Write(binBytes.Length + binPad);
    writer.Write(0x004E4942);
    writer.Write(binBytes);
    for (var i = 0; i < binPad; i++)
      writer.Write((byte)0);
  }

  private static string WriteAnimation(
    AnimationClip clip,
    ExportedMesh mesh,
    ExportSpace space,
    MemoryStream bin,
    List<string> views,
    List<string> accessors)
  {
    var channels = new List<string>();
    var samplers = new List<string>();
    var translated = new HashSet<int>();
    var binding = ClipBinding.Resolve(mesh.Bones, clip);
    var bakeRoots = clip.RootMotion && clip.HasRootRotation;
    for (var t = 0; t < clip.Tracks.Count; t++)
    {
      var track = clip.Tracks[t];
      var node = binding[t];
      if (node < 0 || node >= mesh.Bones.Count)
        continue;
      if (bakeRoots && mesh.Bones[node].Parent < 0)
        continue;

      void Channel(string path, int input, int output)
      {
        var sampler = samplers.Count;
        samplers.Add($"{{\"input\":{input},\"output\":{output},\"interpolation\":\"LINEAR\"}}");
        channels.Add($"{{\"sampler\":{sampler},\"target\":{{\"node\":{node},\"path\":\"{path}\"}}}}");
      }

      if (track.Translation.Count > 0)
      {
        var times = track.Translation.Select(key => key.Time).ToArray();
        var values = new float[track.Translation.Count * 3];
        var addRoot = clip.RootMotion && clip.RootTranslation.Count > 0 && mesh.Bones[node].Parent < 0;
        for (var i = 0; i < track.Translation.Count; i++)
        {
          var value = track.Translation[i].Value;
          if (addRoot)
            value += AnimationClip.Sample(clip.RootTranslation, Vector3.Zero, times[i]);
          var point = AxisConvert.Point(value, space, gltf: true);
          values[i * 3] = point.X;
          values[i * 3 + 1] = point.Y;
          values[i * 3 + 2] = point.Z;
        }

        translated.Add(node);
        Channel("translation", TimeAccessor(times, bin, views, accessors), VectorAccessor(values, bin, views, accessors));
      }

      if (track.Rotation.Count > 0)
      {
        var times = track.Rotation.Select(key => key.Time).ToArray();
        var values = new float[track.Rotation.Count * 4];
        for (var i = 0; i < track.Rotation.Count; i++)
        {
          var rotation = TurnQuaternion(Quaternion.Normalize(track.Rotation[i].Value));

          values[i * 4] = rotation.X;
          values[i * 4 + 1] = rotation.Y;
          values[i * 4 + 2] = rotation.Z;
          values[i * 4 + 3] = rotation.W;
        }

        Channel("rotation", TimeAccessor(times, bin, views, accessors), VectorAccessor(values, bin, views, accessors, "VEC4"));
      }

      if (track.Scale.Count > 0)
      {
        var times = track.Scale.Select(key => key.Time).ToArray();
        var values = new float[track.Scale.Count * 3];
        for (var i = 0; i < track.Scale.Count; i++)
        {
          values[i * 3] = track.Scale[i].Value.X;
          values[i * 3 + 1] = track.Scale[i].Value.Y;
          values[i * 3 + 2] = track.Scale[i].Value.Z;
        }

        Channel("scale", TimeAccessor(times, bin, views, accessors), VectorAccessor(values, bin, views, accessors));
      }
    }

    if (bakeRoots)
    {
      // Root rotation: sample the scene-root bones with ClipPose (local * R_root * T_root) on every key time.
      var keyTimes = new SortedSet<float>(clip.RootRotation.Select(key => key.Time).Concat(clip.RootTranslation.Select(key => key.Time)));
      for (var t = 0; t < clip.Tracks.Count; t++)
      {
        if (binding[t] < 0 || binding[t] >= mesh.Bones.Count || mesh.Bones[binding[t]].Parent >= 0)
          continue;
        foreach (var key in clip.Tracks[t].Rotation) keyTimes.Add(key.Time);
        foreach (var key in clip.Tracks[t].Translation) keyTimes.Add(key.Time);
      }

      var times = keyTimes.ToArray();
      var pose = new Matrix4x4[mesh.Bones.Count];
      var poses = new Matrix4x4[times.Length][];
      for (var i = 0; i < times.Length; i++)
      {
        ClipPose.Apply(mesh.Bones, clip, times[i], pose);
        poses[i] = (Matrix4x4[])pose.Clone();
      }

      var timeAccessor = TimeAccessor(times, bin, views, accessors);
      for (var node = 0; node < mesh.Bones.Count; node++)
      {
        if (mesh.Bones[node].Parent >= 0)
          continue;
        var tValues = new float[times.Length * 3];
        var rValues = new float[times.Length * 4];
        for (var i = 0; i < times.Length; i++)
        {
          Matrix4x4.Decompose(poses[i][node], out _, out var rotation, out var translation);
          var point = AxisConvert.Point(translation, space, gltf: true);
          tValues[i * 3] = point.X;
          tValues[i * 3 + 1] = point.Y;
          tValues[i * 3 + 2] = point.Z;
          var turned = TurnQuaternion(Quaternion.Normalize(rotation));
          rValues[i * 4] = turned.X;
          rValues[i * 4 + 1] = turned.Y;
          rValues[i * 4 + 2] = turned.Z;
          rValues[i * 4 + 3] = turned.W;
        }

        var sampler = samplers.Count;
        samplers.Add($"{{\"input\":{timeAccessor},\"output\":{VectorAccessor(tValues, bin, views, accessors)},\"interpolation\":\"LINEAR\"}}");
        channels.Add($"{{\"sampler\":{sampler},\"target\":{{\"node\":{node},\"path\":\"translation\"}}}}");
        sampler = samplers.Count;
        samplers.Add($"{{\"input\":{timeAccessor},\"output\":{VectorAccessor(rValues, bin, views, accessors, "VEC4")},\"interpolation\":\"LINEAR\"}}");
        channels.Add($"{{\"sampler\":{sampler},\"target\":{{\"node\":{node},\"path\":\"rotation\"}}}}");
      }
    }
    else if (clip.RootMotion && clip.RootTranslation.Count > 0)
    {
      for (var node = 0; node < mesh.Bones.Count; node++)
      {
        if (mesh.Bones[node].Parent >= 0 || translated.Contains(node))
          continue;
        var bind = mesh.Bones[node].Local;
        var origin = new Vector3(bind.M41, bind.M42, bind.M43);
        var times = clip.RootTranslation.Select(key => key.Time).ToArray();
        var values = new float[times.Length * 3];
        for (var i = 0; i < clip.RootTranslation.Count; i++)
        {
          var point = AxisConvert.Point(origin + clip.RootTranslation[i].Value, space, gltf: true);
          values[i * 3] = point.X;
          values[i * 3 + 1] = point.Y;
          values[i * 3 + 2] = point.Z;
        }

        var sampler = samplers.Count;
        samplers.Add($"{{\"input\":{TimeAccessor(times, bin, views, accessors)},\"output\":{VectorAccessor(values, bin, views, accessors)},\"interpolation\":\"LINEAR\"}}");
        channels.Add($"{{\"sampler\":{sampler},\"target\":{{\"node\":{node},\"path\":\"translation\"}}}}");
      }
    }

    if (channels.Count == 0)
      return "[]";

    return $"[{{\"name\":{Quote(clip.Name)},\"channels\":[{string.Join(',', channels)}],\"samplers\":[{string.Join(',', samplers)}]}}]";
  }

  private static Quaternion TurnQuaternion(Quaternion rotation)
  {
    var matrix = Matrix4x4.CreateFromQuaternion(rotation);
    var turned = AxisConvert.Matrix(matrix, ExportSpace.Unreal, gltf: true);
    return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(turned));
  }

  private static int TimeAccessor(float[] times, MemoryStream bin, List<string> views, List<string> accessors)
  {
    var min = times.Min();
    var max = times.Max();
    return Push(times, "SCALAR", times.Length, bin, views, accessors, $",\"min\":[{F(min)}],\"max\":[{F(max)}]");
  }

  private static int VectorAccessor(float[] values, MemoryStream bin, List<string> views, List<string> accessors, string type = "VEC3")
  {
    var count = values.Length / (type == "VEC4" ? 4 : 3);
    return Push(values, type, count, bin, views, accessors, null);
  }

  private static int Push(float[] values, string type, int count, MemoryStream bin, List<string> views, List<string> accessors, string? extra)
  {
    while (bin.Length % 4 != 0)
      bin.WriteByte(0);
    var offset = (int)bin.Length;
    var bytes = AsBytes(values);
    bin.Write(bytes);
    views.Add($"{{\"buffer\":0,\"byteOffset\":{offset},\"byteLength\":{bytes.Length}}}");
    accessors.Add($"{{\"bufferView\":{views.Count - 1},\"componentType\":5126,\"count\":{count},\"type\":\"{type}\"{extra ?? ""}}}");
    return accessors.Count - 1;
  }

  private static string ColumnMajorText(Matrix4x4 matrix)
  {
    var values = new float[16];
    WriteColumnMajor(values, 0, matrix);
    return string.Join(',', values.Select(F));
  }

  private static void WriteColumnMajor(float[] destination, int offset, Matrix4x4 matrix)
  {
    // glTF stores column vectors. System.Numerics is a row-vector matrix, so the
    // matching column-major array is the row-major dump: translation sits in the last column.
    destination[offset] = matrix.M11;
    destination[offset + 1] = matrix.M12;
    destination[offset + 2] = matrix.M13;
    destination[offset + 3] = matrix.M14;
    destination[offset + 4] = matrix.M21;
    destination[offset + 5] = matrix.M22;
    destination[offset + 6] = matrix.M23;
    destination[offset + 7] = matrix.M24;
    destination[offset + 8] = matrix.M31;
    destination[offset + 9] = matrix.M32;
    destination[offset + 10] = matrix.M33;
    destination[offset + 11] = matrix.M34;
    destination[offset + 12] = matrix.M41;
    destination[offset + 13] = matrix.M42;
    destination[offset + 14] = matrix.M43;
    destination[offset + 15] = matrix.M44;
  }

  private static byte[] AsBytes(float[] values)
  {
    var bytes = new byte[values.Length * 4];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
  }

  private static byte[] MemoryMarshalBytes<T>(T[] values) where T : unmanaged
  {
    var bytes = new byte[values.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>()];
    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
    return bytes;
  }

  private static string Quote(string value)
  {
    var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    return "\"" + escaped + "\"";
  }

  private static string F(float value)
    => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

}
