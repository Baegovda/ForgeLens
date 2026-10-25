using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
/// Binary FBX 7.4. The footer, FileId, and CreationTime match the values Blender writes so the
/// FBX SDK checksum used by Unreal Engine accepts the file. ASCII FBX is not written: a character
/// mesh is one line of millions of characters, and Interchange then reports an empty scene.
/// </summary>
public static class FbxWriter
{

  private const int Version = 7400;
  private static readonly byte[] HeadMagic = "Kaydara FBX Binary  \0\x1a\0"u8.ToArray();
  private static readonly byte[] FileId = { 0x28, 0xB3, 0x2A, 0xEB, 0xB6, 0x24, 0xCC, 0xC2, 0xBF, 0xC8, 0xB0, 0x2A, 0xA9, 0x2B, 0xFC, 0xF1 };
  private static readonly byte[] FootId = { 0xFA, 0xBC, 0xAB, 0x09, 0xD0, 0xC8, 0xD4, 0x66, 0xB1, 0x76, 0xFB, 0x83, 0x1C, 0xF7, 0x26, 0x7E };
  private static readonly byte[] FootMagic = { 0xF8, 0x5A, 0x8C, 0x6A, 0xDE, 0xF5, 0xD9, 0x7E, 0xEC, 0xE9, 0x0C, 0xE3, 0x75, 0x8F, 0x29, 0x0B };
  private const string CreationStamp = "1970-01-01 10:00:00:000";

  public static void Write(string path, ExportedMesh mesh, ExportSpace space, IReadOnlyDictionary<string, string>? textureFiles = null, bool animationOnly = false)
  {
    // Unreal import uses Index's checked set. A caller that still holds the full
    // template (LOD shells and collision) must not ship those as extra meshes.
    if (space == ExportSpace.Unreal)
      mesh = mesh.VisibleGeometry();

    var root = new Node("");
    var ids = new IdSource();
    var bones = BuildBones(mesh, space);
    var take = BuildTake(mesh, space, bones);
    ids.Bind(mesh, bones);
    take?.Allocate(ids);

    WriteHeader(root);
    WriteGlobalSettings(root, space, take);
    WriteDocuments(root, ids.Next(), take?.Name ?? "");
    root.Add("References");
    WriteDefinitions(root, mesh, bones, take, animationOnly);
    WriteObjects(root, mesh, space, ids, bones, textureFiles, take, animationOnly);
    if (take is not null)
      WriteAnimationObjects(root, take, mesh);
    WriteConnections(root, mesh, ids, bones, animationOnly);
    if (take is not null)
      WriteAnimationConnections(root, take, ids);
    var takes = root.Add("Takes");
    takes.Add("Current").String(take?.Name ?? "");
    if (take is not null)
      WriteTake(takes, take);

    using var stream = File.Create(path);
    stream.Write(HeadMagic);
    Span<byte> version = stackalloc byte[4];
    BinaryPrimitives.WriteInt32LittleEndian(version, Version);
    stream.Write(version);
    root.MeasureChildren((int)stream.Position, isLast: false);
    root.WriteChildren(stream, isLast: false);
    stream.Write(FootId);
    stream.Write(new byte[4]);
    var pad = (int)((stream.Position + 15) & ~15) - (int)stream.Position;
    if (pad == 0)
      pad = 16;
    stream.Write(new byte[pad]);
    stream.Write(version);
    stream.Write(new byte[120]);
    stream.Write(FootMagic);
  }

  internal static Vector3 EulerXYZDegrees(Matrix4x4 matrix)
  {
    if (!Matrix4x4.Decompose(matrix, out _, out var rotation, out _))
      return Vector3.Zero;
    var pure = Matrix4x4.CreateFromQuaternion(rotation);
    var sy = Math.Clamp(-pure.M13, -1d, 1d);
    var y = Math.Asin(sy);
    double x;
    double z;
    if (Math.Abs(pure.M13) < 0.999999)
    {
      x = Math.Atan2(pure.M23, pure.M33);
      z = Math.Atan2(pure.M12, pure.M11);
    }
    else
    {
      z = 0;
      x = Math.Atan2(-pure.M32, pure.M22);
    }

    return new Vector3((float)(x * 180 / Math.PI), (float)(y * 180 / Math.PI), (float)(z * 180 / Math.PI));
  }

  private static void WriteHeader(Node root)
  {
    var header = root.Add("FBXHeaderExtension");
    header.Add("FBXHeaderVersion").Int32(1003);
    header.Add("FBXVersion").Int32(Version);
    header.Add("EncryptionType").Int32(0);
    var stamp = header.Add("CreationTimeStamp");
    stamp.Add("Version").Int32(1000);
    stamp.Add("Year").Int32(1970);
    stamp.Add("Month").Int32(1);
    stamp.Add("Day").Int32(1);
    stamp.Add("Hour").Int32(10);
    stamp.Add("Minute").Int32(0);
    stamp.Add("Second").Int32(0);
    stamp.Add("Millisecond").Int32(0);
    header.Add("Creator").String("LibSaber.SM2Browser");

    var scene = header.Add("SceneInfo");
    scene.String(NameClass("GlobalInfo", "SceneInfo"));
    scene.String("UserData");
    scene.Add("Type").String("UserData");
    scene.Add("Version").Int32(100);
    var meta = scene.Add("MetaData");
    meta.Add("Version").Int32(100);
    meta.Add("Title").String("");
    meta.Add("Subject").String("");
    meta.Add("Author").String("");
    meta.Add("Keywords").String("");
    meta.Add("Revision").String("");
    meta.Add("Comment").String("");
    var props = scene.Add("Properties70");
    PropStringUrl(props, "DocumentUrl", "scene.fbx");
    PropStringUrl(props, "SrcDocumentUrl", "scene.fbx");
    PropCompound(props, "Original");
    PropString(props, "Original|ApplicationVendor", "LibSaber");
    PropString(props, "Original|ApplicationName", "LibSaber.SM2Browser");
    PropString(props, "Original|ApplicationVersion", "1");
    PropDateTime(props, "Original|DateTime_GMT", "01/01/1970 00:00:00.000");
    PropString(props, "Original|FileName", "scene.fbx");
    PropCompound(props, "LastSaved");
    PropString(props, "LastSaved|ApplicationVendor", "LibSaber");
    PropString(props, "LastSaved|ApplicationName", "LibSaber.SM2Browser");
    PropString(props, "LastSaved|ApplicationVersion", "1");
    PropDateTime(props, "LastSaved|DateTime_GMT", "01/01/1970 00:00:00.000");

    root.Add("FileId").Raw(FileId);
    root.Add("CreationTime").String(CreationStamp);
    root.Add("Creator").String("LibSaber.SM2Browser");
  }

  private static void WriteGlobalSettings(Node root, ExportSpace space, AnimTake? take)
  {
    var settings = root.Add("GlobalSettings");
    settings.Add("Version").Int32(1000);
    var props = settings.Add("Properties70");
    PropInt(props, "UpAxis", 2);
    PropInt(props, "UpAxisSign", 1);
    PropInt(props, "FrontAxis", 1);
    PropInt(props, "FrontAxisSign", 1);
    // Z-up, front along -Y in Blender's names, which is FrontAxis +Y. CoordAxisSign -1
    // keeps the basis right-handed. +1 on the coord axis mirrors the mesh. Vertex data
    // faces +Y so a default Unreal import matches the mannequin.
    PropInt(props, "CoordAxis", 0);
    PropInt(props, "CoordAxisSign", -1);
    PropInt(props, "OriginalUpAxis", 2);
    PropInt(props, "OriginalUpAxisSign", 1);
    var unit = space == ExportSpace.Blender ? 100d : 1d;
    PropDouble(props, "UnitScaleFactor", unit);
    PropDouble(props, "OriginalUnitScaleFactor", unit);
    PropColorRgb(props, "AmbientColor", 0, 0, 0);
    PropString(props, "DefaultCamera", "Producer Perspective");
    var rate = take is { SampleRate: > 1f } ? take.SampleRate : 30d;
    PropEnum(props, "TimeMode", TimeMode(rate));
    PropTime(props, "TimeSpanStart", take?.StartTick ?? 0);
    // A static mesh keeps the one-second span Blender writes. A clip must cover its
    // last key: the SDK evaluator clamps to this span, and a 40s idle was cut at 1s.
    PropTime(props, "TimeSpanStop", take?.StopTick ?? 46186158000);
    PropDouble(props, "CustomFrameRate", rate);
  }

  private static int TimeMode(double rate)
  {
    if (Math.Abs(rate - 24d) < 0.05)
      return 11;
    if (Math.Abs(rate - 30d) < 0.05)
      return 6;
    if (Math.Abs(rate - 60d) < 0.05)
      return 3;
    return 14;
  }

  private static void WriteTake(Node takes, AnimTake take)
  {
    var node = takes.Add("Take");
    node.String(take.Name);
    node.Add("FileName").String(take.Name + ".tak");
    var local = node.Add("LocalTime");
    local.Int64(take.StartTick);
    local.Int64(take.StopTick);
    var reference = node.Add("ReferenceTime");
    reference.Int64(take.StartTick);
    reference.Int64(take.StopTick);
  }

  private static void WriteDocuments(Node root, long documentId, string stackName)
  {
    var docs = root.Add("Documents");
    docs.Add("Count").Int32(1);
    var doc = docs.Add("Document");
    doc.Int64(documentId);
    doc.String("Scene");
    doc.String("Scene");
    var props = doc.Add("Properties70");
    PropObject(props, "SourceObject");
    PropString(props, "ActiveAnimStackName", stackName);
    doc.Add("RootNode").Int64(0);
  }

  private static void WriteDefinitions(Node root, ExportedMesh mesh, BoneTables bones, AnimTake? take, bool animationOnly)
  {
    var materials = animationOnly ? new List<MaterialRef>() : CollectMaterials(mesh);
    var textures = animationOnly ? new List<TextureRef>() : CollectTextures(mesh);
    var geometry = !animationOnly && mesh.HasTriangles ? 1 : 0;
    var models = (animationOnly ? 0 : 1) + mesh.Bones.Count + (mesh.Bones.Count > 0 ? 1 : 0);
    // Blender's armature FBX connects a Null NodeAttribute to the Null model. LimbNode
    // attributes alone leave that root without the attribute its DefaultAttributeIndex names.
    var attributes = mesh.Bones.Count + (mesh.Bones.Count > 0 ? 1 : 0);
    var deformers = !animationOnly && mesh.HasSkin && mesh.HasTriangles ? 1 + bones.Clusters.Count : 0;
    var poses = 1;
    var curves = take?.CurveCount ?? 0;
    var curveNodes = take?.NodeCount ?? 0;
    var stacks = take is null ? 0 : 1;
    var count = 1 + models + geometry + attributes + materials.Count + textures.Count + textures.Count + deformers + poses + stacks + stacks + curveNodes + curves;

    var definitions = root.Add("Definitions");
    definitions.Add("Version").Int32(100);
    definitions.Add("Count").Int32(count);
    ObjectType(definitions, "GlobalSettings", 1, null, null);
    ObjectType(definitions, "Model", models, "FbxNode", ModelTemplate);
    if (geometry != 0)
      ObjectType(definitions, "Geometry", geometry, "FbxMesh", GeometryTemplate);
    if (attributes != 0)
    {
      var attributeType = ObjectType(definitions, "NodeAttribute", attributes, null, null);
      PropertyTemplate(attributeType, "FbxNull", NullAttributeTemplate);
      PropertyTemplate(attributeType, "LimbNode", AttributeTemplate);
    }
    if (materials.Count != 0)
      ObjectType(definitions, "Material", materials.Count, "FbxSurfacePhong", MaterialTemplate);
    if (textures.Count != 0)
    {
      ObjectType(definitions, "Texture", textures.Count, "FbxFileTexture", TextureTemplate);
      ObjectType(definitions, "Video", textures.Count, "FbxVideo", VideoTemplate);
    }

    if (deformers != 0)
      ObjectType(definitions, "Deformer", deformers, null, null);
    ObjectType(definitions, "Pose", poses, null, null);
    if (take is not null)
    {
      ObjectType(definitions, "AnimationStack", 1, "FbxAnimStack", AnimationStackTemplate);
      ObjectType(definitions, "AnimationLayer", 1, "FbxAnimLayer", AnimationLayerTemplate);
      ObjectType(definitions, "AnimationCurveNode", curveNodes, "FbxAnimCurveNode", AnimationCurveNodeTemplate);
      ObjectType(definitions, "AnimationCurve", curves, null, null);
    }
  }

  private static void WriteObjects(Node root, ExportedMesh mesh, ExportSpace space, IdSource ids, BoneTables bones, IReadOnlyDictionary<string, string>? textureFiles, AnimTake? take, bool animationOnly)
  {
    var objects = root.Add("Objects");
    var materials = animationOnly ? new List<MaterialRef>() : CollectMaterials(mesh);
    var textures = animationOnly ? new List<TextureRef>() : CollectTextures(mesh);
    if (!animationOnly && mesh.HasTriangles)
      WriteGeometry(objects, mesh, space, ids, materials);
    if (mesh.Bones.Count > 0)
      WriteArmature(objects, mesh, ids);
    if (!animationOnly)
      WriteMeshModel(objects, mesh, ids);
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      AnimBone? animated = null;
      if (take is not null)
      {
        foreach (var bone in take.Bones)
        {
          if (bone.Index != i)
            continue;
          animated = bone;
          break;
        }
      }

      WriteBone(objects, mesh.Bones[i], bones.Local[i], ids.BoneModels[i], ids.BoneAttributes[i], animated);
    }

    foreach (var material in materials)
      WriteMaterial(objects, material, ids);
    foreach (var texture in textures)
      WriteTexture(objects, texture, ids, textureFiles);

    if (!animationOnly && mesh.HasSkin && mesh.HasTriangles)
      WriteSkin(objects, mesh, bones, ids);

    WriteBindPose(objects, mesh, bones, ids, animationOnly);
  }

  private static void WriteGeometry(Node objects, ExportedMesh mesh, ExportSpace space, IdSource ids, List<MaterialRef> materials)
  {
    var geom = objects.Add("Geometry");
    geom.Int64(ids.Geometry);
    geom.String(NameClass(mesh.Name, "Geometry"));
    geom.String("Mesh");
    geom.Add("Properties70");
    geom.Add("GeometryVersion").Int32(124);

    var vertices = new double[mesh.Positions.Count * 3];
    for (var i = 0; i < mesh.Positions.Count; i++)
    {
      var point = AxisConvert.Point(mesh.Positions[i], space, gltf: false);
      vertices[i * 3] = point.X;
      vertices[i * 3 + 1] = point.Y;
      vertices[i * 3 + 2] = point.Z;
    }

    geom.Add("Vertices").DoubleArray(vertices);

    var polygons = new int[mesh.Indices.Count];
    for (var i = 0; i < mesh.Indices.Count; i++)
    {
      var index = mesh.Indices[i];
      polygons[i] = (i + 1) % 3 == 0 ? ~index : index;
    }

    geom.Add("PolygonVertexIndex").Int32Array(polygons);

    // ByPolygonVertex + IndexToDirect is the mapping Blender's importer and the FBX SDK
    // both keep. ByControlPoint is valid FBX, but Blender 4 drops those layers.
    var corners = mesh.Indices.ToArray();
    var hasNormals = mesh.Normals.Count == mesh.Positions.Count && mesh.Positions.Count > 0;
    if (hasNormals)
    {
      var layer = geom.Add("LayerElementNormal");
      layer.Int32(0);
      layer.Add("Version").Int32(101);
      layer.Add("Name").String("");
      layer.Add("MappingInformationType").String("ByPolygonVertex");
      layer.Add("ReferenceInformationType").String("IndexToDirect");
      var normals = new double[mesh.Normals.Count * 3];
      for (var i = 0; i < mesh.Normals.Count; i++)
      {
        var normal = Vector3.Normalize(AxisConvert.Direction(mesh.Normals[i], gltf: false));
        if (float.IsNaN(normal.X))
          normal = Vector3.UnitZ;
        normals[i * 3] = normal.X;
        normals[i * 3 + 1] = normal.Y;
        normals[i * 3 + 2] = normal.Z;
      }

      layer.Add("Normals").DoubleArray(normals);
      layer.Add("NormalsIndex").Int32Array(corners);
    }

    var hasUvs = mesh.Uvs.Count == mesh.Positions.Count && mesh.Positions.Count > 0;
    if (hasUvs)
    {
      var layer = geom.Add("LayerElementUV");
      layer.Int32(0);
      layer.Add("Version").Int32(101);
      layer.Add("Name").String("UVMap");
      layer.Add("MappingInformationType").String("ByPolygonVertex");
      layer.Add("ReferenceInformationType").String("IndexToDirect");
      var uvs = new double[mesh.Uvs.Count * 2];
      for (var i = 0; i < mesh.Uvs.Count; i++)
      {
        var uv = AxisConvert.FbxUv(mesh.Uvs[i], space);
        uvs[i * 2] = uv.X;
        uvs[i * 2 + 1] = uv.Y;
      }

      layer.Add("UV").DoubleArray(uvs);
      layer.Add("UVIndex").Int32Array(corners);
    }

    if (materials.Count > 0)
    {
      var layer = geom.Add("LayerElementMaterial");
      layer.Int32(0);
      layer.Add("Version").Int32(101);
      layer.Add("Name").String("");
      var triangles = mesh.Indices.Count / 3;
      if (materials.Count == 1)
      {
        layer.Add("MappingInformationType").String("AllSame");
        layer.Add("ReferenceInformationType").String("IndexToDirect");
        layer.Add("Materials").Int32Array(new[] { 0 });
      }
      else
      {
        var assigned = new int[triangles];
        var ordinal = 0;
        foreach (var part in mesh.Parts)
        {
          if (part.IndexCount < 3)
            continue;
          var material = ordinal++;
          if (material >= materials.Count)
            break;
          var start = Math.Max(0, part.IndexStart / 3);
          var end = Math.Min(triangles, start + Math.Max(0, part.IndexCount / 3));
          for (var triangle = start; triangle < end; triangle++)
            assigned[triangle] = material;
        }

        layer.Add("MappingInformationType").String("ByPolygon");
        layer.Add("ReferenceInformationType").String("IndexToDirect");
        layer.Add("Materials").Int32Array(assigned);
      }
    }

    var toc = geom.Add("Layer");
    toc.Int32(0);
    toc.Add("Version").Int32(100);
    if (hasNormals)
      LayerEntry(toc, "LayerElementNormal");
    if (hasUvs)
      LayerEntry(toc, "LayerElementUV");
    if (materials.Count > 0)
      LayerEntry(toc, "LayerElementMaterial");
  }

  private static void LayerEntry(Node layer, string type)
  {
    var entry = layer.Add("LayerElement");
    entry.Add("Type").String(type);
    entry.Add("TypedIndex").Int32(0);
  }

  private static void WriteArmature(Node objects, ExportedMesh mesh, IdSource ids)
  {
    var attribute = objects.Add("NodeAttribute");
    attribute.Int64(ids.ArmatureAttribute);
    attribute.String(NameClass(mesh.Name + "_Skeleton", "NodeAttribute"));
    attribute.String("Null");
    attribute.Add("TypeFlags").String("Null");
    var attributeProps = attribute.Add("Properties70");
    PropDouble(attributeProps, "Size", 100);

    var model = objects.Add("Model");
    model.Int64(ids.Armature);
    model.String(NameClass(mesh.Name + "_Skeleton", "Model"));
    model.String("Null");
    model.Add("Version").Int32(232);
    var props = model.Add("Properties70");
    PropLcl(props, "Lcl Translation", 0, 0, 0);
    PropLcl(props, "Lcl Rotation", 0, 0, 0);
    PropLcl(props, "Lcl Scaling", 1, 1, 1);
    PropDouble(props, "Visibility", 1, "Visibility", "");
    PropInt(props, "DefaultAttributeIndex", 0);
    PropEnum(props, "RotationOrder", 0);
    PropEnum(props, "InheritType", 1);
    model.Add("MultiLayer").Int32(0);
    model.Add("MultiTake").Int32(0);
    model.Add("Shading").Char(1);
    model.Add("Culling").String("CullingOff");
  }

  private static void WriteMeshModel(Node objects, ExportedMesh mesh, IdSource ids)
  {
    var model = objects.Add("Model");
    model.Int64(ids.MeshModel);
    model.String(NameClass(mesh.Name, "Model"));
    model.String("Mesh");
    model.Add("Version").Int32(232);
    var props = model.Add("Properties70");
    PropLcl(props, "Lcl Translation", 0, 0, 0);
    PropLcl(props, "Lcl Rotation", 0, 0, 0);
    PropLcl(props, "Lcl Scaling", 1, 1, 1);
    PropDouble(props, "Visibility", 1, "Visibility", "");
    PropInt(props, "DefaultAttributeIndex", 0);
    PropEnum(props, "RotationOrder", 0);
    PropEnum(props, "InheritType", 1);
    model.Add("MultiLayer").Int32(0);
    model.Add("MultiTake").Int32(0);
    model.Add("Shading").Char(1);
    model.Add("Culling").String("CullingOff");
  }

  private static void WriteBone(Node objects, Bone bone, Matrix4x4 local, long modelId, long attributeId, AnimBone? animated)
  {
    var attribute = objects.Add("NodeAttribute");
    attribute.Int64(attributeId);
    attribute.String(NameClass(bone.Name, "NodeAttribute"));
    attribute.String("LimbNode");
    attribute.Add("TypeFlags").String("Skeleton");
    var attributeProps = attribute.Add("Properties70");
    PropDouble(attributeProps, "Size", 1);

    var translation = new Vector3(local.M41, local.M42, local.M43);
    var euler = EulerXYZDegrees(local);
    var scale = Vector3.One;
    if (Matrix4x4.Decompose(local, out var decomposed, out _, out _))
      scale = decomposed;

    var model = objects.Add("Model");
    model.Int64(modelId);
    model.String(NameClass(bone.Name, "Model"));
    model.String("LimbNode");
    model.Add("Version").Int32(232);
    var props = model.Add("Properties70");
    PropLcl(props, "Lcl Translation", translation.X, translation.Y, translation.Z, animated?.Translation is not null);
    PropLcl(props, "Lcl Rotation", euler.X, euler.Y, euler.Z, animated?.Rotation is not null);
    PropLcl(props, "Lcl Scaling", scale.X, scale.Y, scale.Z);
    PropDouble(props, "Visibility", 1, "Visibility", "");
    PropInt(props, "DefaultAttributeIndex", 0);
    PropEnum(props, "RotationOrder", 0);
    PropEnum(props, "InheritType", 1);
    model.Add("MultiLayer").Int32(0);
    model.Add("MultiTake").Int32(0);
    model.Add("Shading").Char(1);
    model.Add("Culling").String("CullingOff");
  }

  private static void WriteMaterial(Node objects, MaterialRef material, IdSource ids)
  {
    var node = objects.Add("Material");
    node.Int64(ids.Materials[material.Key]);
    node.String(NameClass(material.Name, "Material"));
    node.String("");
    node.Add("Version").Int32(102);
    node.Add("ShadingModel").String("Phong");
    node.Add("MultiLayer").Int32(0);
    var props = node.Add("Properties70");
    PropString(props, "ShadingModel", "Phong");
    PropColor(props, "DiffuseColor", 0.8, 0.8, 0.8);
    PropNumber(props, "DiffuseFactor", 1);
    PropColor(props, "EmissiveColor", 0, 0, 0);
    PropNumber(props, "EmissiveFactor", 1);
    PropColor(props, "AmbientColor", 0.2, 0.2, 0.2);
    PropNumber(props, "AmbientFactor", 1);
    PropColor(props, "SpecularColor", 0.2, 0.2, 0.2);
    PropNumber(props, "SpecularFactor", 1);
    PropNumber(props, "Shininess", 20);
    PropNumber(props, "ShininessExponent", 20);
    PropNumber(props, "Opacity", 1);
    PropNumber(props, "TransparencyFactor", 0);
  }

  private static void WriteTexture(Node objects, TextureRef texture, IdSource ids, IReadOnlyDictionary<string, string>? textureFiles)
  {
    var file = TextureFile(texture.Name, textureFiles);
    var tex = objects.Add("Texture");
    tex.Int64(ids.Textures[texture.Name]);
    tex.String(NameClass(texture.Name, "Texture"));
    tex.String("");
    tex.Add("Type").String("TextureVideoClip");
    tex.Add("Version").Int32(202);
    tex.Add("TextureName").String(NameClass(texture.Name, "Texture"));
    tex.Add("Media").String(NameClass(texture.Name, "Video"));
    tex.Add("FileName").String(file);
    tex.Add("RelativeFilename").String(file);
    var props = tex.Add("Properties70");
    PropEnum(props, "CurrentMappingType", 0);
    PropString(props, "UVSet", "UVMap");
    PropEnum(props, "WrapModeU", 0);
    PropEnum(props, "WrapModeV", 0);
    PropVector(props, "Translation", 0, 0, 0);
    PropVector(props, "Rotation", 0, 0, 0);
    PropVector(props, "Scaling", 1, 1, 1);
    PropBool(props, "UseMaterial", true);
    PropBool(props, "UseMipMap", false);

    var video = objects.Add("Video");
    video.Int64(ids.Videos[texture.Name]);
    video.String(NameClass(texture.Name, "Video"));
    video.String("Clip");
    video.Add("Type").String("Clip");
    var videoProps = video.Add("Properties70");
    PropStringUrl(videoProps, "Path", file);
    video.Add("UseMipMap").Int32(0);
    video.Add("Filename").String(file);
    video.Add("RelativeFilename").String(file);
  }

  private static void WriteSkin(Node objects, ExportedMesh mesh, BoneTables bones, IdSource ids)
  {
    var skin = objects.Add("Deformer");
    skin.Int64(ids.Skin);
    skin.String(NameClass(mesh.Name, "Deformer"));
    skin.String("Skin");
    skin.Add("Version").Int32(101);
    skin.Add("Link_DeformAcuracy").Double(50);

    var weights = new List<(int Index, float Weight)>[mesh.Bones.Count];
    for (var i = 0; i < weights.Length; i++)
      weights[i] = new List<(int, float)>();
    for (var vertex = 0; vertex < mesh.Skin.Count; vertex++)
    {
      var skinVertex = mesh.Skin[vertex];
      AddWeight(weights, vertex, skinVertex.J0, skinVertex.W0);
      AddWeight(weights, vertex, skinVertex.J1, skinVertex.W1);
      AddWeight(weights, vertex, skinVertex.J2, skinVertex.W2);
      AddWeight(weights, vertex, skinVertex.J3, skinVertex.W3);
    }

    var meshWorld = Matrix4x4.Identity;
    for (var bone = 0; bone < mesh.Bones.Count; bone++)
    {
      if (weights[bone].Count == 0)
        continue;
      var world = bones.World[bone];
      Matrix4x4.Invert(world, out var inverse);
      var cluster = objects.Add("Deformer");
      cluster.Int64(ids.Clusters[bone]);
      cluster.String(NameClass(mesh.Bones[bone].Name, "SubDeformer"));
      cluster.String("Cluster");
      cluster.Add("Version").Int32(100);
      var user = cluster.Add("UserData");
      user.String("");
      user.String("");
      var indexes = new int[weights[bone].Count];
      var values = new double[weights[bone].Count];
      for (var i = 0; i < weights[bone].Count; i++)
      {
        indexes[i] = weights[bone][i].Index;
        values[i] = weights[bone][i].Weight;
      }

      cluster.Add("Indexes").Int32Array(indexes);
      cluster.Add("Weights").DoubleArray(values);
      cluster.Add("Transform").DoubleArray(MatrixArray(meshWorld * inverse));
      cluster.Add("TransformLink").DoubleArray(MatrixArray(world));
      cluster.Add("TransformAssociateModel").DoubleArray(MatrixArray(meshWorld));
    }
  }

  private static void WriteBindPose(Node objects, ExportedMesh mesh, BoneTables bones, IdSource ids, bool animationOnly)
  {
    var pose = objects.Add("Pose");
    pose.Int64(ids.Pose);
    pose.String(NameClass(mesh.Name, "Pose"));
    pose.String("BindPose");
    pose.Add("Type").String("BindPose");
    pose.Add("Version").Int32(100);
    var poseNodes = (animationOnly ? 0 : 1) + mesh.Bones.Count + (mesh.Bones.Count > 0 ? 1 : 0);
    pose.Add("NbPoseNodes").Int32(poseNodes);
    if (mesh.Bones.Count > 0)
    {
      var armNode = pose.Add("PoseNode");
      armNode.Add("Node").Int64(ids.Armature);
      armNode.Add("Matrix").DoubleArray(MatrixArray(Matrix4x4.Identity));
    }

    if (!animationOnly)
    {
      var meshNode = pose.Add("PoseNode");
      meshNode.Add("Node").Int64(ids.MeshModel);
      meshNode.Add("Matrix").DoubleArray(MatrixArray(Matrix4x4.Identity));
    }
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var node = pose.Add("PoseNode");
      node.Add("Node").Int64(ids.BoneModels[i]);
      node.Add("Matrix").DoubleArray(MatrixArray(bones.World[i]));
    }
  }

  private static void WriteConnections(Node root, ExportedMesh mesh, IdSource ids, BoneTables bones, bool animationOnly)
  {
    var connections = root.Add("Connections");
    var sceneParent = mesh.Bones.Count > 0 ? ids.Armature : 0;
    if (mesh.Bones.Count > 0)
    {
      Connect(connections, "OO", ids.Armature, 0);
      Connect(connections, "OO", ids.ArmatureAttribute, ids.Armature);
    }
    if (!animationOnly)
      Connect(connections, "OO", ids.MeshModel, sceneParent);
    if (!animationOnly && mesh.HasTriangles)
      Connect(connections, "OO", ids.Geometry, ids.MeshModel);
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var parent = mesh.Bones[i].Parent;
      var parentId = parent >= 0 && parent < ids.BoneModels.Length ? ids.BoneModels[parent] : sceneParent;
      Connect(connections, "OO", ids.BoneModels[i], parentId);
      Connect(connections, "OO", ids.BoneAttributes[i], ids.BoneModels[i]);
    }

    var materials = CollectMaterials(mesh);
    if (animationOnly)
      return;

    foreach (var material in materials)
    {
      var materialId = ids.Materials[material.Key];
      Connect(connections, "OO", materialId, ids.MeshModel);
      if (!string.IsNullOrWhiteSpace(material.Diffuse) && ids.Textures.TryGetValue(material.Diffuse, out var diffuseId))
        Connect(connections, "OP", diffuseId, materialId, "DiffuseColor");
      if (!string.IsNullOrWhiteSpace(material.Normal) && ids.Textures.TryGetValue(material.Normal, out var normalId))
        Connect(connections, "OP", normalId, materialId, "NormalMap");
      if (!string.IsNullOrWhiteSpace(material.Specular) && ids.Textures.TryGetValue(material.Specular, out var specularId))
        Connect(connections, "OP", specularId, materialId, "SpecularColor");
      if (!string.IsNullOrWhiteSpace(material.Emissive) && ids.Textures.TryGetValue(material.Emissive, out var emissiveId))
        Connect(connections, "OP", emissiveId, materialId, "EmissiveColor");
    }

    foreach (var texture in CollectTextures(mesh))
      Connect(connections, "OO", ids.Videos[texture.Name], ids.Textures[texture.Name]);

    if (!mesh.HasSkin || !mesh.HasTriangles)
      return;

    Connect(connections, "OO", ids.Skin, ids.Geometry);
    for (var i = 0; i < bones.Clusters.Count; i++)
    {
      var bone = bones.Clusters[i];
      Connect(connections, "OO", ids.Clusters[bone], ids.Skin);
      Connect(connections, "OO", ids.BoneModels[bone], ids.Clusters[bone]);
    }
  }

  private static void Connect(Node connections, string kind, long child, long parent, string? property = null)
  {
    var link = connections.Add("C");
    link.String(kind);
    link.Int64(child);
    link.Int64(parent);
    if (property is not null)
      link.String(property);
  }

  private static BoneTables BuildBones(ExportedMesh mesh, ExportSpace space)
  {
    var local = new Matrix4x4[mesh.Bones.Count];
    var world = new Matrix4x4[mesh.Bones.Count];
    var source = new Matrix4x4[mesh.Bones.Count];
    var parents = new int[mesh.Bones.Count];
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      source[i] = mesh.Bones[i].Local;
      parents[i] = mesh.Bones[i].Parent;
    }

    // Convert composed worlds, then derive locals. Converting each local matrix stacks the
    // Y-up to Z-up rotation once per bone, which is the fan of long bones Unreal draws.
    AxisConvert.ConvertHierarchy(source, parents, space, local, world);

    var clusters = new List<int>();
    if (mesh.HasSkin)
    {
      var used = new bool[mesh.Bones.Count];
      for (var vertex = 0; vertex < mesh.Skin.Count; vertex++)
      {
        var skin = mesh.Skin[vertex];
        Mark(used, skin.J0, skin.W0);
        Mark(used, skin.J1, skin.W1);
        Mark(used, skin.J2, skin.W2);
        Mark(used, skin.J3, skin.W3);
      }

      for (var i = 0; i < used.Length; i++)
      {
        if (used[i])
          clusters.Add(i);
      }
    }

    return new BoneTables(local, world, clusters);
  }

  private static void Mark(bool[] used, int joint, float weight)
  {
    if (weight > 0 && (uint)joint < (uint)used.Length)
      used[joint] = true;
  }

  private static void AddWeight(List<(int Index, float Weight)>[] weights, int vertex, int joint, float weight)
  {
    if (weight > 0 && (uint)joint < (uint)weights.Length)
      weights[joint].Add((vertex, weight));
  }

  private static List<MaterialRef> CollectMaterials(ExportedMesh mesh)
  {
    var list = new List<MaterialRef>();
    foreach (var slot in MaterialMaps.Slots(mesh))
    {
      var part = slot.Part;
      list.Add(new MaterialRef(
        slot.Name,
        slot.Name,
        string.IsNullOrWhiteSpace(part.Texture) ? null : part.Texture,
        string.IsNullOrWhiteSpace(part.Normal) ? null : part.Normal,
        string.IsNullOrWhiteSpace(part.Specular) ? null : part.Specular,
        string.IsNullOrWhiteSpace(part.Emissive) ? null : part.Emissive));
    }

    if (list.Count == 0 && mesh.HasTriangles)
      list.Add(new MaterialRef(string.IsNullOrWhiteSpace(mesh.Name) ? "mesh" : mesh.Name, mesh.Name, null, null, null, null));
    return list;
  }

  private static List<TextureRef> CollectTextures(ExportedMesh mesh)
  {
    var list = new List<TextureRef>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    void Add(string? name)
    {
      if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
        return;
      list.Add(new TextureRef(name));
    }

    foreach (var material in CollectMaterials(mesh))
    {
      Add(material.Diffuse);
      Add(material.Normal);
      Add(material.Specular);
      Add(material.Emissive);
    }

    return list;
  }

  private static string TextureFile(string name, IReadOnlyDictionary<string, string>? textureFiles)
  {
    if (textureFiles is not null && textureFiles.TryGetValue(name, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
      return mapped.Replace('\\', '/');

    var file = name.Replace('\\', '/');
    var slash = file.LastIndexOf('/');
    if (slash >= 0)
      file = file[(slash + 1)..];
    if (file.EndsWith(".pct.resource", StringComparison.OrdinalIgnoreCase))
      file = file[..^".pct.resource".Length];
    else if (file.EndsWith(".pct", StringComparison.OrdinalIgnoreCase))
      file = file[..^".pct".Length];
    else if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
      || file.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
      || file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
      file = Path.GetFileNameWithoutExtension(file);
    if (file.Length == 0)
      file = "texture";
    return Safe(file) + ".png";
  }

  private static double[] MatrixArray(Matrix4x4 matrix)
    => new[]
    {
      (double)matrix.M11, matrix.M12, matrix.M13, matrix.M14,
      matrix.M21, matrix.M22, matrix.M23, matrix.M24,
      matrix.M31, matrix.M32, matrix.M33, matrix.M34,
      matrix.M41, matrix.M42, matrix.M43, matrix.M44,
    };

  private static Node ObjectType(Node definitions, string name, int count, string? template, Action<Node>? fill)
  {
    var type = definitions.Add("ObjectType");
    type.String(name);
    type.Add("Count").Int32(count);
    if (template is not null && fill is not null)
      PropertyTemplate(type, template, fill);
    return type;
  }

  private static void PropertyTemplate(Node type, string template, Action<Node> fill)
  {
    var props = type.Add("PropertyTemplate");
    props.String(template);
    fill(props.Add("Properties70"));
  }

  private static void ModelTemplate(Node props)
  {
    PropLcl(props, "Lcl Translation", 0, 0, 0);
    PropLcl(props, "Lcl Rotation", 0, 0, 0);
    PropLcl(props, "Lcl Scaling", 1, 1, 1);
    PropEnum(props, "RotationOrder", 0);
    PropEnum(props, "InheritType", 1);
    PropInt(props, "DefaultAttributeIndex", 0);
    PropDouble(props, "Visibility", 1, "Visibility", "");
  }

  private static void GeometryTemplate(Node props)
  {
    PropColorRgb(props, "Color", 0.8, 0.8, 0.8);
    PropBool(props, "Primary Visibility", true);
    PropBool(props, "Casts Shadows", true);
    PropBool(props, "Receive Shadows", true);
  }

  private static void NullAttributeTemplate(Node props)
    => PropDouble(props, "Size", 100);

  private static void AttributeTemplate(Node props)
    => PropDouble(props, "Size", 1);

  private static void MaterialTemplate(Node props)
  {
    PropString(props, "ShadingModel", "Phong");
    PropColor(props, "DiffuseColor", 0.8, 0.8, 0.8);
    PropNumber(props, "DiffuseFactor", 1);
  }

  private static void TextureTemplate(Node props)
  {
    PropString(props, "UVSet", "UVMap");
    PropBool(props, "UseMaterial", true);
  }

  private static void VideoTemplate(Node props)
    => PropStringUrl(props, "Path", "");

  private static void AnimationStackTemplate(Node props)
  {
    PropString(props, "Description", "");
    PropTime(props, "LocalStart", 0);
    PropTime(props, "LocalStop", 0);
    PropTime(props, "ReferenceStart", 0);
    PropTime(props, "ReferenceStop", 0);
  }

  private static void AnimationLayerTemplate(Node props)
  {
    PropNumber(props, "Weight", 100);
    PropBool(props, "Mute", false);
    PropBool(props, "Solo", false);
    PropBool(props, "Lock", false);
    PropColorRgb(props, "Color", 0.8, 0.8, 0.8);
    PropEnum(props, "BlendMode", 0);
    PropEnum(props, "RotationAccumulationMode", 0);
    PropEnum(props, "ScaleAccumulationMode", 0);
    PropULong(props, "BlendModeBypass", 0);
  }

  private static void AnimationCurveNodeTemplate(Node props)
    => PropCompound(props, "d");

  private static void PropInt(Node props, string name, int value)
    => Prop(props, name, "int", "Integer", "", value);

  private static void PropEnum(Node props, string name, int value)
    => Prop(props, name, "enum", "", "", value);

  private static void PropBool(Node props, string name, bool value)
    => Prop(props, name, "bool", "", "", value ? 1 : 0);

  private static void PropDouble(Node props, string name, double value, string type = "double", string label = "Number")
  {
    var node = props.Add("P");
    node.String(name);
    node.String(type);
    node.String(label);
    node.String("");
    node.Double(value);
  }

  private static void PropNumber(Node props, string name, double value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("Number");
    node.String("");
    node.String("A");
    node.Double(value);
  }

  private static void PropTime(Node props, string name, long value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("KTime");
    node.String("Time");
    node.String("");
    node.Int64(value);
  }

  private static void PropString(Node props, string name, string value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("KString");
    node.String("");
    node.String("");
    node.String(value);
  }

  private static void PropStringUrl(Node props, string name, string value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("KString");
    node.String("Url");
    node.String("");
    node.String(value);
  }

  private static void PropDateTime(Node props, string name, string value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("DateTime");
    node.String("");
    node.String("");
    node.String(value);
  }

  private static void PropObject(Node props, string name)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("object");
    node.String("");
    node.String("");
  }

  private static void PropCompound(Node props, string name)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("Compound");
    node.String("");
    node.String("");
  }

  private static void Prop(Node props, string name, string type, string label, string flags, int value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String(type);
    node.String(label);
    node.String(flags);
    node.Int32(value);
  }

  private static void PropVector(Node props, string name, double x, double y, double z)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("Vector3D");
    node.String("Vector");
    node.String("");
    node.Double(x);
    node.Double(y);
    node.Double(z);
  }

  private static void PropLcl(Node props, string name, double x, double y, double z, bool animated = false)
  {
    var node = props.Add("P");
    node.String(name);
    node.String(name);
    node.String("");
    node.String(animated ? "A+" : "A");
    node.Double(x);
    node.Double(y);
    node.Double(z);
  }

  private static void PropULong(Node props, string name, long value)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("ULongLong");
    node.String("");
    node.String("");
    node.Int64(value);
  }

  private static void PropColor(Node props, string name, double r, double g, double b)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("Color");
    node.String("");
    node.String("A");
    node.Double(r);
    node.Double(g);
    node.Double(b);
  }

  private static void PropColorRgb(Node props, string name, double r, double g, double b)
  {
    var node = props.Add("P");
    node.String(name);
    node.String("ColorRGB");
    node.String("Color");
    node.String("");
    node.Double(r);
    node.Double(g);
    node.Double(b);
  }

  private static string NameClass(string name, string cls)
    => Safe(name) + "\0\u0001" + cls;

  private static string Safe(string name)
  {
    var builder = new StringBuilder(name.Length);
    foreach (var c in name)
    {
      if (c is '\0' or '\u0001' or ':' or '"' or '\\')
        builder.Append('_');
      else if (c < 32)
        builder.Append('_');
      else
        builder.Append(c);
    }

    return builder.Length == 0 ? "object" : builder.ToString();
  }

  private const double FbxTicksPerSecond = 46186158000d;
  private const int LinearKeyFlags = (1 << 2) | (1 << 8) | (1 << 13) | (1 << 14);
  private static readonly float[] LinearKeyData = { 0f, 0f, BitConverter.Int32BitsToSingle(unchecked((int)0x0F3F0F3F)), 0f };

  private static AnimTake? BuildTake(ExportedMesh mesh, ExportSpace space, BoneTables bind)
  {
    if (mesh.Clip is not { HasCurves: true } clip || mesh.Bones.Count == 0)
      return null;

    var times = new SortedSet<float>();
    foreach (var track in clip.Tracks)
    {
      foreach (var key in track.Rotation)
        times.Add(key.Time);
      foreach (var key in track.Translation)
        times.Add(key.Time);
    }

    if (clip.RootMotion)
    {
      foreach (var key in clip.RootTranslation)
        times.Add(key.Time);
    }

    if (times.Count == 0)
      return null;

    var samples = times.ToArray();
    var count = mesh.Bones.Count;
    var translation = new Vector3[count][];
    var rotation = new Vector3[count][];
    for (var bone = 0; bone < count; bone++)
    {
      translation[bone] = new Vector3[samples.Length];
      rotation[bone] = new Vector3[samples.Length];
    }

    var source = new Matrix4x4[count];
    var parents = new int[count];
    for (var i = 0; i < count; i++)
      parents[i] = mesh.Bones[i].Parent;
    var local = new Matrix4x4[count];
    var world = new Matrix4x4[count];
    for (var frame = 0; frame < samples.Length; frame++)
    {
      ClipPose.Apply(mesh.Bones, clip, samples[frame], source);
      AxisConvert.ConvertHierarchy(source, parents, space, local, world);
      for (var bone = 0; bone < count; bone++)
      {
        translation[bone][frame] = new Vector3(local[bone].M41, local[bone].M42, local[bone].M43);
        var euler = EulerXYZDegrees(local[bone]);
        if (frame > 0)
          euler = UnwrapEuler(rotation[bone][frame - 1], euler);
        rotation[bone][frame] = euler;
      }
    }

    var take = new AnimTake(Safe(clip.Name), samples, clip.SampleRate);
    for (var bone = 0; bone < count; bone++)
    {
      var restT = new Vector3(bind.Local[bone].M41, bind.Local[bone].M42, bind.Local[bone].M43);
      var restR = EulerXYZDegrees(bind.Local[bone]);
      var moveT = false;
      var moveR = false;
      for (var frame = 0; frame < samples.Length; frame++)
      {
        if (Vector3.Distance(translation[bone][frame], restT) > 0.0005f)
          moveT = true;
        if (EulerDistance(rotation[bone][frame], restR) > 0.05f)
          moveR = true;
      }

      if (!moveT && !moveR)
        continue;
      take.Bones.Add(new AnimBone(bone, moveT ? Flatten(translation[bone]) : null, moveR ? Flatten(rotation[bone]) : null, restT, restR));
    }

    return take.Bones.Count == 0 ? null : take;
  }

  private static float[] Flatten(Vector3[] values)
  {
    var flat = new float[values.Length * 3];
    for (var i = 0; i < values.Length; i++)
    {
      flat[i * 3] = values[i].X;
      flat[i * 3 + 1] = values[i].Y;
      flat[i * 3 + 2] = values[i].Z;
    }

    return flat;
  }

  private static Vector3 UnwrapEuler(Vector3 previous, Vector3 current)
    => new(NearestAngle(previous.X, current.X), NearestAngle(previous.Y, current.Y), NearestAngle(previous.Z, current.Z));

  private static float NearestAngle(float previous, float current)
  {
    while (current - previous > 180f)
      current -= 360f;
    while (previous - current > 180f)
      current += 360f;
    return current;
  }

  private static float EulerDistance(Vector3 a, Vector3 b)
    => MathF.Abs(NearestAngle(a.X, b.X) - a.X) + MathF.Abs(NearestAngle(a.Y, b.Y) - a.Y) + MathF.Abs(NearestAngle(a.Z, b.Z) - a.Z);

  private static void WriteAnimationObjects(Node root, AnimTake take, ExportedMesh mesh)
  {
    var objects = root.Children.Last(node => Encoding.UTF8.GetString(node.Name) == "Objects");
    var stack = objects.Add("AnimationStack");
    stack.Int64(take.StackId);
    stack.String(NameClass(take.Name, "AnimStack"));
    stack.String("");
    var stackProps = stack.Add("Properties70");
    PropTime(stackProps, "LocalStart", take.StartTick);
    PropTime(stackProps, "LocalStop", take.StopTick);
    PropTime(stackProps, "ReferenceStart", take.StartTick);
    PropTime(stackProps, "ReferenceStop", take.StopTick);

    var layer = objects.Add("AnimationLayer");
    layer.Int64(take.LayerId);
    layer.String(NameClass(take.Name, "AnimLayer"));
    layer.String("");
    var layerProps = layer.Add("Properties70");
    PropNumber(layerProps, "Weight", 100);
    PropBool(layerProps, "Mute", false);

    foreach (var bone in take.Bones)
    {
      var boneName = mesh.Bones[bone.Index].Name;
      if (bone.Translation is not null)
        WriteCurveNode(objects, take, bone.TranslationId, bone.TranslationCurves, boneName, "T", bone.Translation, bone.RestTranslation);
      if (bone.Rotation is not null)
        WriteCurveNode(objects, take, bone.RotationId, bone.RotationCurves, boneName, "R", bone.Rotation, bone.RestRotation);
    }
  }

  private static void WriteCurveNode(Node objects, AnimTake take, long id, long[] curves, string boneName, string channel, float[] values, Vector3 rest)
  {
    var node = objects.Add("AnimationCurveNode");
    node.Int64(id);
    node.String(NameClass(boneName + " " + channel, "AnimCurveNode"));
    node.String("");
    var props = node.Add("Properties70");
    // The compound has to exist before d|X/Y/Z. Without it the SDK stores those as
    // user properties and GetCurve(layer, "X") does not see the keys.
    PropCompound(props, "d");
    PropNumber(props, "d|X", rest.X);
    PropNumber(props, "d|Y", rest.Y);
    PropNumber(props, "d|Z", rest.Z);
    for (var axis = 0; axis < 3; axis++)
      WriteCurve(objects, curves[axis], boneName + channel + "XYZ"[axis], take.Samples, values, axis, axis switch { 0 => rest.X, 1 => rest.Y, _ => rest.Z });
  }

  private static void WriteCurve(Node objects, long id, string name, float[] samples, float[] values, int axis, double rest)
  {
    var curve = objects.Add("AnimationCurve");
    curve.Int64(id);
    curve.String(NameClass(name, "AnimCurve"));
    curve.String("");
    curve.Add("Default").Double(rest);
    curve.Add("KeyVer").Int32(4008);
    var times = new long[samples.Length];
    var axisValues = new float[samples.Length];
    for (var i = 0; i < samples.Length; i++)
    {
      times[i] = (long)Math.Round(samples[i] * FbxTicksPerSecond);
      axisValues[i] = values[i * 3 + axis];
    }

    curve.Add("KeyTime").Int64Array(times);
    curve.Add("KeyValueFloat").FloatArray(axisValues);
    // Same linear-plus-tangent flags Blender writes. Unreal's FBX SDK accepts that
    // combination; a bare linear flag is not what those files contain.
    curve.Add("KeyAttrFlags").Int32Array(new[] { LinearKeyFlags });
    curve.Add("KeyAttrDataFloat").FloatArray(LinearKeyData);
    curve.Add("KeyAttrRefCount").Int32Array(new[] { samples.Length });
  }

  private static void WriteAnimationConnections(Node root, AnimTake take, IdSource ids)
  {
    var connections = root.Children.Last(node => Encoding.UTF8.GetString(node.Name) == "Connections");
    Connect(connections, "OO", take.LayerId, take.StackId);
    foreach (var bone in take.Bones)
    {
      var model = ids.BoneModels[bone.Index];
      if (bone.Translation is not null)
        ConnectCurves(connections, take.LayerId, bone.TranslationId, bone.TranslationCurves, model, "Lcl Translation");
      if (bone.Rotation is not null)
        ConnectCurves(connections, take.LayerId, bone.RotationId, bone.RotationCurves, model, "Lcl Rotation");
    }
  }

  private static void ConnectCurves(Node connections, long layerId, long nodeId, long[] curveIds, long model, string property)
  {
    Connect(connections, "OO", nodeId, layerId);
    Connect(connections, "OP", nodeId, model, property);
    for (var axis = 0; axis < 3; axis++)
      Connect(connections, "OP", curveIds[axis], nodeId, axis switch { 0 => "d|X", 1 => "d|Y", _ => "d|Z" });
  }

  private readonly record struct MaterialRef(string Key, string Name, string? Diffuse, string? Normal, string? Specular, string? Emissive);
  private readonly record struct TextureRef(string Name);

  private sealed class AnimTake
  {
    public AnimTake(string name, float[] samples, float sampleRate)
    {
      Name = name.Length == 0 ? "clip" : name;
      Samples = samples;
      SampleRate = sampleRate;
      StartTick = samples.Length == 0 ? 0 : (long)Math.Round(samples[0] * FbxTicksPerSecond);
      StopTick = samples.Length == 0 ? 0 : (long)Math.Round(samples[^1] * FbxTicksPerSecond);
      if (StopTick <= StartTick)
        StopTick = StartTick + (long)Math.Round(FbxTicksPerSecond / 30d);
    }

    public string Name { get; }
    public float[] Samples { get; }
    public float SampleRate { get; }
    public long StartTick { get; }
    public long StopTick { get; }
    public long StackId { get; private set; }
    public long LayerId { get; private set; }
    public List<AnimBone> Bones { get; } = new();
    public int NodeCount => Bones.Sum(bone => (bone.Translation is null ? 0 : 1) + (bone.Rotation is null ? 0 : 1));
    public int CurveCount => NodeCount * 3;

    public void Allocate(IdSource ids)
    {
      StackId = ids.Next();
      LayerId = ids.Next();
      foreach (var bone in Bones)
      {
        if (bone.Translation is not null)
        {
          bone.TranslationId = ids.Next();
          bone.TranslationCurves = new[] { ids.Next(), ids.Next(), ids.Next() };
        }

        if (bone.Rotation is not null)
        {
          bone.RotationId = ids.Next();
          bone.RotationCurves = new[] { ids.Next(), ids.Next(), ids.Next() };
        }
      }
    }
  }

  private sealed class AnimBone
  {
    public AnimBone(int index, float[]? translation, float[]? rotation, Vector3 restTranslation, Vector3 restRotation)
    {
      Index = index;
      Translation = translation;
      Rotation = rotation;
      RestTranslation = restTranslation;
      RestRotation = restRotation;
    }

    public int Index { get; }
    public float[]? Translation { get; }
    public float[]? Rotation { get; }
    public Vector3 RestTranslation { get; }
    public Vector3 RestRotation { get; }
    public long TranslationId { get; set; }
    public long RotationId { get; set; }
    public long[] TranslationCurves { get; set; } = Array.Empty<long>();
    public long[] RotationCurves { get; set; } = Array.Empty<long>();
  }

  private sealed class BoneTables
  {
    public BoneTables(Matrix4x4[] local, Matrix4x4[] world, List<int> clusters)
    {
      Local = local;
      World = world;
      Clusters = clusters;
    }

    public Matrix4x4[] Local { get; }
    public Matrix4x4[] World { get; }
    public List<int> Clusters { get; }
  }

  private sealed class IdSource
  {
    private long _next = 10;
    private readonly Dictionary<string, long> _materials = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _videos = new(StringComparer.Ordinal);

    public long Next() => _next++;
    public long Geometry { get; } = 1;
    public long MeshModel { get; } = 2;
    public long Skin { get; } = 3;
    public long Pose { get; } = 4;
    public long Armature { get; } = 5;
    public long ArmatureAttribute { get; } = 6;
    public long[] BoneModels { get; private set; } = Array.Empty<long>();
    public long[] BoneAttributes { get; private set; } = Array.Empty<long>();
    public long[] Clusters { get; private set; } = Array.Empty<long>();
    public Dictionary<string, long> Materials => _materials;
    public Dictionary<string, long> Textures => _textures;
    public Dictionary<string, long> Videos => _videos;

    public IdSource()
    {
    }

    public void Bind(ExportedMesh mesh, BoneTables bones)
    {
      BoneModels = new long[mesh.Bones.Count];
      BoneAttributes = new long[mesh.Bones.Count];
      Clusters = new long[mesh.Bones.Count];
      for (var i = 0; i < mesh.Bones.Count; i++)
      {
        BoneModels[i] = Next();
        BoneAttributes[i] = Next();
        Clusters[i] = Next();
      }

      foreach (var material in CollectMaterials(mesh))
        _materials[material.Key] = Next();
      foreach (var texture in CollectTextures(mesh))
      {
        _textures[texture.Name] = Next();
        _videos[texture.Name] = Next();
      }
    }
  }

  private sealed class Node
  {
    private const int Sentinel = 13;
    // The FBX SDK reader expects a null child-list terminator on these two nodes even
    // when they have no children. Without it the layer is dropped, the stack grows a
    // default "Base Layer" with no curves, and Unreal samples the bind pose.
    private static readonly HashSet<string> AlwaysSentinel = new(StringComparer.Ordinal)
    {
      "AnimationStack",
      "AnimationLayer",
    };

    public Node(string name) => Name = Encoding.UTF8.GetBytes(name);
    public byte[] Name { get; }
    public List<(char Type, byte[] Data)> Props { get; } = new();
    public List<Node> Children { get; } = new();
    public int End { get; private set; }
    public int PropsLength { get; private set; }

    public Node Add(string name)
    {
      var child = new Node(name);
      Children.Add(child);
      return child;
    }

    public void Int32(int value) => Props.Add(('I', BitConverter.GetBytes(value)));
    public void Int64(long value) => Props.Add(('L', BitConverter.GetBytes(value)));
    public void Double(double value) => Props.Add(('D', BitConverter.GetBytes(value)));
    public void Char(byte value) => Props.Add(('C', new[] { value }));

    public void String(string value)
    {
      var text = Encoding.UTF8.GetBytes(value);
      var data = new byte[4 + text.Length];
      BinaryPrimitives.WriteInt32LittleEndian(data, text.Length);
      text.CopyTo(data.AsSpan(4));
      Props.Add(('S', data));
    }

    public void Raw(byte[] value)
    {
      var data = new byte[4 + value.Length];
      BinaryPrimitives.WriteInt32LittleEndian(data, value.Length);
      value.CopyTo(data.AsSpan(4));
      Props.Add(('R', data));
    }

    public void Int32Array(int[] values) => Props.Add(('i', PackArray(values, sizeof(int), static (buffer, source) => Buffer.BlockCopy(source, 0, buffer, 0, source.Length * sizeof(int)))));
    public void Int64Array(long[] values) => Props.Add(('l', PackArray(values, sizeof(long), static (buffer, source) => Buffer.BlockCopy(source, 0, buffer, 0, source.Length * sizeof(long)))));
    public void FloatArray(float[] values) => Props.Add(('f', PackArray(values, sizeof(float), static (buffer, source) => Buffer.BlockCopy(source, 0, buffer, 0, source.Length * sizeof(float)))));
    public void DoubleArray(double[] values) => Props.Add(('d', PackArray(values, sizeof(double), static (buffer, source) => Buffer.BlockCopy(source, 0, buffer, 0, source.Length * sizeof(double)))));

    public int Measure(int offset, bool isLast)
    {
      offset += 13 + Name.Length;
      var props = 0;
      foreach (var prop in Props)
        props += 1 + prop.Data.Length;
      PropsLength = props;
      offset += props;
      if (Children.Count > 0)
      {
        for (var i = 0; i < Children.Count; i++)
          offset = Children[i].Measure(offset, i == Children.Count - 1);
        offset += Sentinel;
      }
      else if ((Props.Count == 0 && !isLast) || AlwaysSentinel.Contains(Encoding.UTF8.GetString(Name)))
      {
        offset += Sentinel;
      }

      End = offset;
      return offset;
    }

    public void MeasureChildren(int offset, bool isLast)
    {
      if (Children.Count == 0)
        return;
      for (var i = 0; i < Children.Count; i++)
        offset = Children[i].Measure(offset, i == Children.Count - 1);
      End = offset + Sentinel;
    }

    public void Write(Stream stream, bool isLast)
    {
      WriteInt(stream, End);
      WriteInt(stream, Props.Count);
      WriteInt(stream, PropsLength);
      stream.WriteByte((byte)Name.Length);
      stream.Write(Name);
      foreach (var prop in Props)
      {
        stream.WriteByte((byte)prop.Type);
        stream.Write(prop.Data);
      }

      WriteChildren(stream, isLast);
      if (stream.Position != End)
        throw new InvalidDataException($"FBX node '{Encoding.UTF8.GetString(Name)}' ended at {stream.Position}, expected {End}.");
    }

    public void WriteChildren(Stream stream, bool isLast)
    {
      if (Children.Count > 0)
      {
        for (var i = 0; i < Children.Count; i++)
          Children[i].Write(stream, i == Children.Count - 1);
        stream.Write(new byte[Sentinel]);
      }
      else if ((Props.Count == 0 && !isLast) || AlwaysSentinel.Contains(Encoding.UTF8.GetString(Name)))
      {
        stream.Write(new byte[Sentinel]);
      }
    }

    private static void WriteInt(Stream stream, int value)
    {
      Span<byte> bytes = stackalloc byte[4];
      BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
      stream.Write(bytes);
    }

    private static byte[] PackArray<T>(T[] values, int size, Action<byte[], T[]> copy)
    {
      var raw = new byte[values.Length * size];
      copy(raw, values);
      if (raw.Length <= 128)
      {
        var plain = new byte[12 + raw.Length];
        BinaryPrimitives.WriteInt32LittleEndian(plain, values.Length);
        BinaryPrimitives.WriteInt32LittleEndian(plain.AsSpan(4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(plain.AsSpan(8), raw.Length);
        raw.CopyTo(plain.AsSpan(12));
        return plain;
      }

      var compressed = Zlib(raw);
      var packed = new byte[12 + compressed.Length];
      BinaryPrimitives.WriteInt32LittleEndian(packed, values.Length);
      BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(4), 1);
      BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(8), compressed.Length);
      compressed.CopyTo(packed.AsSpan(12));
      return packed;
    }

    private static byte[] Zlib(byte[] raw)
    {
      using var output = new MemoryStream();
      output.WriteByte(0x78);
      output.WriteByte(0x9C);
      using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
        deflate.Write(raw);
      var checksum = Adler(raw);
      output.WriteByte((byte)(checksum >> 24));
      output.WriteByte((byte)(checksum >> 16));
      output.WriteByte((byte)(checksum >> 8));
      output.WriteByte((byte)checksum);
      return output.ToArray();
    }

    private static uint Adler(byte[] data)
    {
      const uint mod = 65521;
      uint a = 1;
      uint b = 0;
      foreach (var value in data)
      {
        a = (a + value) % mod;
        b = (b + a) % mod;
      }

      return (b << 16) | a;
    }
  }
}
