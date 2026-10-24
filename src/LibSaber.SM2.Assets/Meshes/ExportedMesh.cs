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

}
