using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

public enum ExportSpace
{
  /// <summary>FBX is Z-up centimeters (source meters times 100). glTF is Y-up meters.</summary>
  Unreal = 0,
  /// <summary>FBX is Z-up meters. glTF is Y-up meters.</summary>
  Blender = 1,
}

public sealed class Bone
{
  public string Name { get; set; } = "bone";
  public int Parent { get; set; } = -1;
  public Matrix4x4 Local { get; set; } = Matrix4x4.Identity;
  public Matrix4x4 InverseBind { get; set; } = Matrix4x4.Identity;
  public bool FileBind { get; set; }
  public bool Influences { get; set; }
}

public struct SkinWeights
{
  public ushort J0, J1, J2, J3;
  public float W0, W1, W2, W3;
}

public sealed class MeshPart
{
  public string Name { get; set; } = "part";
  public int IndexStart { get; set; }
  public int IndexCount { get; set; }
  public string? Texture { get; set; }
}

/// <summary>A lower detail level. LOD0 lives on <see cref="ExportedMesh"/> itself.</summary>
public sealed class MeshLod
{
  public string Name { get; set; } = "LOD1";
  public List<Vector3> Positions { get; } = new();
  public List<Vector3> Normals { get; } = new();
  public List<Vector2> Uvs { get; } = new();
  public List<int> Indices { get; } = new();
  public List<SkinWeights> Skin { get; } = new();
  public List<MeshPart> Parts { get; } = new();
}

public sealed class MaterialSlot
{
  public string Slot { get; set; } = "";
  public string Texture { get; set; } = "";
  public float[]? Tint { get; set; }
}

public sealed class MaterialDocument
{
  public string Name { get; set; } = "material";
  public List<MaterialSlot> Slots { get; } = new();
  public List<string> Notes { get; } = new();
}

public sealed class ExportedMesh
{
  public string Name { get; set; } = "mesh";
  public List<Vector3> Positions { get; } = new();
  public List<Vector3> Normals { get; } = new();
  public List<Vector2> Uvs { get; } = new();
  public List<int> Indices { get; } = new();
  public List<Bone> Bones { get; } = new();
  public List<SkinWeights> Skin { get; } = new();
  public List<MeshPart> Parts { get; } = new();
  public List<MeshLod> Levels { get; } = new();
  public MaterialDocument? Material { get; set; }
  public AnimationClip? Clip { get; set; }
  public string Message { get; set; } = "";

  public bool HasTriangles => Indices.Count >= 3;
  public bool HasSkin => Skin.Count == Positions.Count && Bones.Count > 0 && Skin.Count > 0;
}

public static class AxisConvert
{

  public const string Assumption =
    "Source units are meters, Y-up. A retail character is about 2.6 units on Y, which is a standing height, and a helmet is about 0.3 units. glTF stays Y-up meters. Unreal FBX is Z-up centimeters (times 100). Blender FBX is Z-up meters.";

  public static float Scale(ExportSpace space, bool gltf)
    => !gltf && space == ExportSpace.Unreal ? 100f : 1f;

  public static Vector3 Point(Vector3 value, ExportSpace space, bool gltf)
  {
    var scale = Scale(space, gltf);
    return Direction(value, gltf) * scale;
  }

  public static Vector3 Direction(Vector3 value, bool gltf)
    => gltf ? value : new Vector3(value.X, -value.Z, value.Y);

  public static Matrix4x4 Matrix(Matrix4x4 source, ExportSpace space, bool gltf)
  {
    var x = Direction(new Vector3(source.M11, source.M12, source.M13), gltf);
    var y = Direction(new Vector3(source.M21, source.M22, source.M23), gltf);
    var z = Direction(new Vector3(source.M31, source.M32, source.M33), gltf);
    var translation = Point(new Vector3(source.M41, source.M42, source.M43), space, gltf);
    return new Matrix4x4(
      x.X, x.Y, x.Z, 0,
      y.X, y.Y, y.Z, 0,
      z.X, z.Y, z.Z, 0,
      translation.X, translation.Y, translation.Z, source.M44);
  }

  /// <summary>
  ///   Source is Y-up, X right, +Z toward the character's front. Destination FBX is
  ///   Z-up and -Y forward: (x, y, z) maps to (x, -z, y). This is that change of basis.
  /// </summary>
  private static Matrix4x4 Basis()
    => new(
      1, 0, 0, 0,
      0, 0, 1, 0,
      0, -1, 0, 0,
      0, 0, 0, 1);

  /// <summary>
  ///   Convert one world matrix. Conjugating the rotation keeps a parent and child in the
  ///   same space; converting each local matrix on its own stacks an extra rotation per bone.
  /// </summary>
  public static Matrix4x4 ConvertWorld(Matrix4x4 source, ExportSpace space, bool gltf)
  {
    if (gltf)
    {
      source.M14 = 0;
      source.M24 = 0;
      source.M34 = 0;
      if (source.M44 == 0)
        source.M44 = 1;
      return source;
    }

    var basis = Basis();
    Matrix4x4.Invert(basis, out var inverse);
    var linear = source;
    linear.M14 = linear.M24 = linear.M34 = 0;
    linear.M41 = linear.M42 = linear.M43 = 0;
    linear.M44 = 1;
    var converted = inverse * linear * basis;
    var translation = Point(new Vector3(source.M41, source.M42, source.M43), space, false);
    converted.M41 = translation.X;
    converted.M42 = translation.Y;
    converted.M43 = translation.Z;
    converted.M14 = converted.M24 = converted.M34 = 0;
    converted.M44 = 1;
    return converted;
  }

  /// <summary>
  ///   Source locals are parent-relative. Convert the composed worlds, then rebuild locals so
  ///   a child translation stays a bone length instead of another copy of the world position.
  /// </summary>
  public static void ConvertHierarchy(ReadOnlySpan<Matrix4x4> sourceLocals, ReadOnlySpan<int> parents, ExportSpace space, Span<Matrix4x4> locals, Span<Matrix4x4> worlds)
  {
    var count = sourceLocals.Length;
    var sourceWorld = new Matrix4x4[count];
    var done = new bool[count];
    for (var guard = 0; guard < count; guard++)
    {
      var progress = false;
      for (var i = 0; i < count; i++)
      {
        if (done[i])
          continue;
        var parent = parents[i];
        if (parent >= 0 && parent < count && parent != i && !done[parent])
          continue;
        var local = sourceLocals[i];
        sourceWorld[i] = parent >= 0 && parent < count && parent != i
          ? local * sourceWorld[parent]
          : local;
        done[i] = true;
        progress = true;
      }

      if (!progress)
        break;
    }

    for (var i = 0; i < count; i++)
    {
      if (!done[i])
        sourceWorld[i] = sourceLocals[i];
      worlds[i] = ConvertWorld(sourceWorld[i], space, false);
    }

    for (var i = 0; i < count; i++)
    {
      var parent = parents[i];
      if (parent < 0 || parent >= count || parent == i || !done[parent] || !Matrix4x4.Invert(worlds[parent], out var inverse))
        locals[i] = worlds[i];
      else
        locals[i] = worlds[i] * inverse;
    }
  }

}
