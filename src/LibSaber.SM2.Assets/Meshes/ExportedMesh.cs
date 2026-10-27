using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

public enum ExportSpace
{
  /// <summary>FBX is Z-up, +Y forward, centimeters (source meters times 100). glTF is Y-up meters.</summary>
  Unreal = 0,
  /// <summary>FBX is Z-up, +Y forward, meters. glTF is Y-up meters. UVs stay as sampled, because Blender does not flip V.</summary>
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
  /// <summary>The template object's <c>animNmb</c> (-1 = not animated). See <see cref="ClipBinding"/>.</summary>
  public int AnimIndex { get; set; } = -1;
  /// <summary>The template object's own name before the <c>_N</c> dedupe suffix (empty for hand-built meshes).</summary>
  public string SourceName { get; set; } = "";
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
  /// <summary>Normal map named by a material layer, a <c>.td</c> shader, or the <c>_nm</c> sibling Index assigns.</summary>
  public string? Normal { get; set; }
  /// <summary>Packed specular map (<c>_spec</c>). Channel layout is <see cref="SpecularChannels"/>.</summary>
  public string? Specular { get; set; }
  public string? Emissive { get; set; }
  /// <summary><c>.td</c> <c>format_descr</c> of <see cref="Specular"/>, for example <c>Metalness(r)+Roughness(g)+AO(b)</c>.</summary>
  public string SpecularChannels { get; set; } = "";
  /// <summary>Shader variant inside the texture definition. Empty shading materials are <c>default</c>.</summary>
  public string Variant { get; set; } = "default";
  /// <summary><c>opaque</c>, <c>masked</c> (alpha kill), or <c>translucent</c>.</summary>
  public string Blend { get; set; } = "opaque";
  /// <summary>
  ///   <c>glt.albedo_tint</c> of the texture definition variant, as R, G, B, A in 0..1 (the .td stores A, R, G, B bytes).
  ///   Null when the variant does not set one (white).
  /// </summary>
  public float[]? AlbedoTint { get; set; }
  /// <summary>Raw <c>glt.blendMode</c> of the variant (<c>opaque</c> when unset).</summary>
  public string BlendMode { get; set; } = "opaque";
  /// <summary><c>glt.second_spec</c> when the variant enables it (null keeps the single-lobe look).</summary>
  public SecondSpec? SecondSpec { get; set; }
  /// <summary><c>glt.anisotropic_spec</c> when the variant enables it with anisotropy above 0.01.</summary>
  public AnisoSpec? AnisoSpec { get; set; }
  /// <summary><c>glt.chameleon</c> when the variant enables it.</summary>
  public ChameleonSpec? Chameleon { get; set; }
  /// <summary>Glt-level metallicTint, reflectance, metalness and roughness scale/bias (engine defaults when unset).</summary>
  public BaseLayer BaseLayer { get; set; } = new();
  /// <summary>Index leaves LOD and collision shells unchecked. Defaults on so hand-built meshes still export whole.</summary>
  public bool Visible { get; set; } = true;
  /// <summary>"LOD" or "volume" when <see cref="Visible"/> was cleared by Index's default filter.</summary>
  public string? HideReason { get; set; }
  /// <summary>Template object (geometry graph node) the submesh belongs to, or -1. Indexes <see cref="ExportedMesh.Bones"/>.</summary>
  public int Node { get; set; } = -1;
  public List<MaterialMap> Maps { get; } = new();

  public MeshPart Copy(int indexStart, int indexCount)
  {
    var copy = new MeshPart
    {
      Name = Name,
      IndexStart = indexStart,
      IndexCount = indexCount,
      Texture = Texture,
      Normal = Normal,
      Specular = Specular,
      Emissive = Emissive,
      SpecularChannels = SpecularChannels,
      Variant = Variant,
      Blend = Blend,
      AlbedoTint = AlbedoTint,
      BlendMode = BlendMode,
      SecondSpec = SecondSpec,
      AnisoSpec = AnisoSpec,
      Chameleon = Chameleon,
      BaseLayer = BaseLayer,
      Visible = Visible,
      HideReason = HideReason,
      Node = Node,
    };
    copy.Maps.AddRange(Maps);
    return copy;
  }
}

/// <summary>Checkbox state for one catalog file, in surface order: LOD0, then each lower level.</summary>
public sealed class MeshVisibility
{
  public List<bool[]> Surfaces { get; } = new();

  public static MeshVisibility Capture(ExportedMesh mesh)
  {
    var visibility = new MeshVisibility();
    visibility.Surfaces.Add(Flags(mesh.Parts));
    foreach (var level in mesh.Levels)
      visibility.Surfaces.Add(Flags(level.Parts));
    return visibility;
  }

  public void Apply(ExportedMesh mesh)
  {
    if (Surfaces.Count > 0)
      Apply(mesh.Parts, Surfaces[0]);
    for (var i = 0; i < mesh.Levels.Count && i + 1 < Surfaces.Count; i++)
      Apply(mesh.Levels[i].Parts, Surfaces[i + 1]);
  }

  private static bool[] Flags(IReadOnlyList<MeshPart> parts)
  {
    var flags = new bool[parts.Count];
    for (var i = 0; i < parts.Count; i++)
      flags[i] = parts[i].Visible;
    return flags;
  }

  private static void Apply(IList<MeshPart> parts, bool[] flags)
  {
    var count = Math.Min(parts.Count, flags.Length);
    for (var i = 0; i < count; i++)
      parts[i].Visible = flags[i];
  }
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

/// <summary>
///   A cloth simulation shell (node name contains <c>_SIM</c>) captured at decode time, before any part
///   compaction, so the physics code sees the game's sim particles with their paint (COLOR0).
///   The engine requires OBJ_FVF_VERT | OBJ_FVF_NORM | OBJ_FVF_COLOR0 | OBJ_FVF_TEX0 on a sim mesh
///   (sm2_retail.exe string near 0x1435f1790).
/// </summary>
public sealed class ClothSimShell
{
  public string Name { get; set; } = "";
  /// <summary>Bind positions in model space (the same space as <see cref="ExportedMesh.Positions"/>).</summary>
  public List<Vector3> Positions { get; } = new();
  public List<Vector3> Normals { get; } = new();
  public List<SkinWeights> Skin { get; } = new();
  /// <summary>COLOR0 per particle (0..1). Empty when the buffer carries no colour.</summary>
  public List<Vector4> Colors { get; } = new();
  /// <summary>Triangles, local to this shell.</summary>
  public List<int> Indices { get; } = new();
  /// <summary>Surface (0 = LOD0) and first vertex of the shell in that surface.</summary>
  public int Surface { get; set; }
  public int VertexStart { get; set; }
}

public sealed class ExportedMesh
{
  public string Name { get; set; } = "mesh";
  public List<Vector3> Positions { get; } = new();
  public List<Vector3> Normals { get; } = new();
  public List<Vector2> Uvs { get; } = new();
  /// <summary>Raw UV set 1 (times its submesh UvScaling, no flip); used by the blood-splash VAT lookup. May be empty.</summary>
  public List<Vector2> Uvs1 { get; } = new();
  public List<int> Indices { get; } = new();
  public List<Bone> Bones { get; } = new();
  public List<SkinWeights> Skin { get; } = new();
  public List<MeshPart> Parts { get; } = new();
  public List<MeshLod> Levels { get; } = new();
  /// <summary>Cloth sim shells (<c>*_SIM</c>) with COLOR0, in decode order.</summary>
  public List<ClothSimShell> ClothSims { get; } = new();
  public MaterialDocument? Material { get; set; }
  public AnimationClip? Clip { get; set; }
  /// <summary>Cloth and jiggle setup from the character class (<c>prop_cloth_sim</c>, <c>prop_phys_jiggle</c>).</summary>
  public LibSaber.SM2.Assets.Physics.PhysicsSetup? Physics { get; set; }
  public string Message { get; set; } = "";

  public bool HasTriangles => Indices.Count >= 3;
  public bool HasSkin => Skin.Count == Positions.Count && Bones.Count > 0 && Skin.Count > 0;

  /// <summary>
  ///   Triangles whose parts are checked. Lower levels contribute only their checked parts,
  ///   so a visible sim shell is kept and an unchecked LOD or collision shell is not.
  ///   Returns this instance when LOD0 is fully checked and no other level has a checked part.
  /// </summary>
  /// <summary>Copy with the same vertex data and shared bone, part and level objects (lists are new).</summary>
  public ExportedMesh ShallowCopy()
  {
    var copy = new ExportedMesh { Name = Name, Material = Material, Clip = Clip, Physics = Physics, Message = Message };
    copy.Positions.AddRange(Positions);
    copy.Normals.AddRange(Normals);
    copy.Uvs.AddRange(Uvs);
    copy.Indices.AddRange(Indices);
    copy.Bones.AddRange(Bones);
    copy.Skin.AddRange(Skin);
    copy.Parts.AddRange(Parts);
    copy.Levels.AddRange(Levels);
    copy.ClothSims.AddRange(ClothSims);
    return copy;
  }

  public ExportedMesh VisibleGeometry()
  {
    var levelVisible = false;
    foreach (var level in Levels)
    {
      if (level.Parts.Any(part => part.Visible && part.IndexCount >= 3))
      {
        levelVisible = true;
        break;
      }
    }

    var lodHidden = Parts.Any(part => !part.Visible && part.IndexCount >= 3);
    if (!lodHidden && !levelVisible)
      return this;

    var copy = new ExportedMesh
    {
      Name = Name,
      Material = Material,
      Clip = Clip,
      Physics = Physics,
      Message = Message,
    };
    copy.Bones.AddRange(Bones);
    copy.ClothSims.AddRange(ClothSims);
    var keepNormals = true;
    var keepUvs = true;
    var keepSkin = true;
    Take(copy, Positions, Normals, Uvs, Indices, Skin, Parts, ref keepNormals, ref keepUvs, ref keepSkin);
    foreach (var level in Levels)
      Take(copy, level.Positions, level.Normals, level.Uvs, level.Indices, level.Skin, level.Parts, ref keepNormals, ref keepUvs, ref keepSkin);
    if (!keepNormals)
      copy.Normals.Clear();
    if (!keepUvs)
      copy.Uvs.Clear();
    if (!keepSkin)
      copy.Skin.Clear();
    return copy;
  }

  private static void Take(
    ExportedMesh copy,
    IReadOnlyList<Vector3> positions,
    IReadOnlyList<Vector3> normals,
    IReadOnlyList<Vector2> uvs,
    IReadOnlyList<int> indices,
    IReadOnlyList<SkinWeights> skin,
    IReadOnlyList<MeshPart> parts,
    ref bool keepNormals,
    ref bool keepUvs,
    ref bool keepSkin)
  {
    if (!parts.Any(part => part.Visible && part.IndexCount >= 3))
      return;
    if (normals.Count != positions.Count)
      keepNormals = false;
    if (uvs.Count != positions.Count)
      keepUvs = false;
    if (skin.Count != positions.Count)
      keepSkin = false;

    var baseVertex = copy.Positions.Count;
    foreach (var position in positions)
      copy.Positions.Add(position);
    if (keepNormals)
    {
      foreach (var normal in normals)
        copy.Normals.Add(normal);
    }

    if (keepUvs)
    {
      foreach (var uv in uvs)
        copy.Uvs.Add(uv);
    }

    if (keepSkin)
    {
      foreach (var weights in skin)
        copy.Skin.Add(weights);
    }

    foreach (var part in parts)
    {
      if (!part.Visible || part.IndexCount < 3)
        continue;
      var start = Math.Max(0, part.IndexStart);
      var end = Math.Min(indices.Count, start + part.IndexCount);
      var indexStart = copy.Indices.Count;
      for (var i = start; i < end; i++)
        copy.Indices.Add(indices[i] + baseVertex);
      var copied = part.Copy(indexStart, copy.Indices.Count - indexStart);
      copied.Visible = true;
      copy.Parts.Add(copied);
    }
  }
}

public static class AxisConvert
{

  public const string Assumption =
    "Source units are meters, Y-up, +Z in front. A retail character is about 2.6 units on Y. glTF stays Y-up meters. Unreal FBX is Z-up, +Y forward, centimeters (times 100), which is the facing Unreal's mannequin uses after a default import. Blender FBX is the same facing in meters.";

  public static float Scale(ExportSpace space, bool gltf)
    => !gltf && space == ExportSpace.Unreal ? 100f : 1f;

  public static Vector3 Point(Vector3 value, ExportSpace space, bool gltf)
  {
    var scale = Scale(space, gltf);
    return Direction(value, gltf) * scale;
  }

  public static Vector3 Direction(Vector3 value, bool gltf)
    => gltf ? value : new Vector3(-value.X, value.Z, value.Y);

  /// <summary>
  ///   UV stored on the mesh is the coordinate MeshPreview samples, with V = 0 at the top of the image.
  ///   Unreal's FBX importer then sets <c>v = 1 - v</c>. Writing <c>1 - v</c> here lands on that same texel.
  ///   The value is not wrapped: a seam that crosses an integer would split if each vertex were reduced mod 1.
  ///   Blender's importer does not do that flip, so a Blender-space FBX keeps the stored coordinate.
  /// </summary>
  public static Vector2 FbxUv(Vector2 sampled, ExportSpace space)
    => space == ExportSpace.Unreal ? new Vector2(sampled.X, 1f - sampled.Y) : sampled;

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
  ///   Z-up and +Y forward (Unreal's mannequin, and this file's FrontAxis): (x, y, z)
  ///   maps to (-x, z, y). A 180° yaw from the previous -Y facing, so it is a rotation
  ///   and does not mirror. The same matrix converts bone worlds.
  /// </summary>
  private static Matrix4x4 Basis()
    => new(
      -1, 0, 0, 0,
      0, 0, 1, 0,
      0, 1, 0, 0,
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
