using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;

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

  public static void Write(string path, ExportedMesh mesh, ExportSpace space)
  {
    var root = new Node("");
    var ids = new IdSource();
    var bones = BuildBones(mesh, space);
    ids.Bind(mesh, bones);

    WriteHeader(root);
    WriteGlobalSettings(root, space);
    WriteDocuments(root, ids.Next());
    root.Add("References");
    WriteDefinitions(root, mesh, bones);
    WriteObjects(root, mesh, space, ids, bones);
    WriteConnections(root, mesh, ids, bones);
    var takes = root.Add("Takes");
    takes.Add("Current").String("");

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

  private static void WriteGlobalSettings(Node root, ExportSpace space)
  {
    var settings = root.Add("GlobalSettings");
    settings.Add("Version").Int32(1000);
    var props = settings.Add("Properties70");
    PropInt(props, "UpAxis", 2);
    PropInt(props, "UpAxisSign", 1);
    PropInt(props, "FrontAxis", 1);
    PropInt(props, "FrontAxisSign", 1);
    // Z-up, -Y forward (FBX right-handed). CoordAxisSign -1 is that system; +1 mirrors it.
    PropInt(props, "CoordAxis", 0);
    PropInt(props, "CoordAxisSign", -1);
    PropInt(props, "OriginalUpAxis", 2);
    PropInt(props, "OriginalUpAxisSign", 1);
    var unit = space == ExportSpace.Blender ? 100d : 1d;
    PropDouble(props, "UnitScaleFactor", unit);
    PropDouble(props, "OriginalUnitScaleFactor", unit);
    PropColorRgb(props, "AmbientColor", 0, 0, 0);
    PropString(props, "DefaultCamera", "Producer Perspective");
    PropEnum(props, "TimeMode", 6);
    PropTime(props, "TimeSpanStart", 0);
    PropTime(props, "TimeSpanStop", 46186158000);
    PropDouble(props, "CustomFrameRate", 30);
  }

  private static void WriteDocuments(Node root, long documentId)
  {
    var docs = root.Add("Documents");
    docs.Add("Count").Int32(1);
    var doc = docs.Add("Document");
    doc.Int64(documentId);
    doc.String("Scene");
    doc.String("Scene");
    var props = doc.Add("Properties70");
    PropObject(props, "SourceObject");
    PropString(props, "ActiveAnimStackName", "");
    doc.Add("RootNode").Int64(0);
  }

  private static void WriteDefinitions(Node root, ExportedMesh mesh, BoneTables bones)
  {
    var materials = CollectMaterials(mesh);
    var textures = CollectTextures(mesh);
    var geometry = mesh.HasTriangles ? 1 : 0;
    var models = 1 + mesh.Bones.Count;
    var attributes = mesh.Bones.Count;
    var deformers = mesh.HasSkin && mesh.HasTriangles ? 1 + bones.Clusters.Count : 0;
    var poses = 1;
    var count = 1 + models + geometry + attributes + materials.Count + textures.Count + textures.Count + deformers + poses;

    var definitions = root.Add("Definitions");
    definitions.Add("Version").Int32(100);
    definitions.Add("Count").Int32(count);
    ObjectType(definitions, "GlobalSettings", 1, null, null);
    ObjectType(definitions, "Model", models, "FbxNode", ModelTemplate);
    if (geometry != 0)
      ObjectType(definitions, "Geometry", geometry, "FbxMesh", GeometryTemplate);
    if (attributes != 0)
      ObjectType(definitions, "NodeAttribute", attributes, "LimbNode", AttributeTemplate);
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
  }

  private static void WriteObjects(Node root, ExportedMesh mesh, ExportSpace space, IdSource ids, BoneTables bones)
  {
    var objects = root.Add("Objects");
    var materials = CollectMaterials(mesh);
    var textures = CollectTextures(mesh);
    if (mesh.HasTriangles)
      WriteGeometry(objects, mesh, space, ids, materials);
    WriteMeshModel(objects, mesh, ids);
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      WriteBone(objects, mesh.Bones[i], bones.Local[i], ids.BoneModels[i], ids.BoneAttributes[i]);
    }

    foreach (var material in materials)
      WriteMaterial(objects, material, ids);
    foreach (var texture in textures)
      WriteTexture(objects, texture, ids);

    if (mesh.HasSkin && mesh.HasTriangles)
      WriteSkin(objects, mesh, bones, ids);

    WriteBindPose(objects, mesh, bones, ids);
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
        uvs[i * 2] = mesh.Uvs[i].X;
        uvs[i * 2 + 1] = mesh.Uvs[i].Y;
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
        foreach (var part in mesh.Parts)
        {
          var key = MaterialKey(part);
          var material = materials.FindIndex(item => item.Key == key);
          if (material < 0)
            continue;
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

  private static void WriteBone(Node objects, Bone bone, Matrix4x4 local, long modelId, long attributeId)
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
    PropLcl(props, "Lcl Translation", translation.X, translation.Y, translation.Z);
    PropLcl(props, "Lcl Rotation", euler.X, euler.Y, euler.Z);
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

  private static void WriteTexture(Node objects, TextureRef texture, IdSource ids)
  {
    var file = TextureFile(texture.Name);
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

  private static void WriteBindPose(Node objects, ExportedMesh mesh, BoneTables bones, IdSource ids)
  {
    var pose = objects.Add("Pose");
    pose.Int64(ids.Pose);
    pose.String(NameClass(mesh.Name, "Pose"));
    pose.String("BindPose");
    pose.Add("Type").String("BindPose");
    pose.Add("Version").Int32(100);
    pose.Add("NbPoseNodes").Int32(1 + mesh.Bones.Count);
    var meshNode = pose.Add("PoseNode");
    meshNode.Add("Node").Int64(ids.MeshModel);
    meshNode.Add("Matrix").DoubleArray(MatrixArray(Matrix4x4.Identity));
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var node = pose.Add("PoseNode");
      node.Add("Node").Int64(ids.BoneModels[i]);
      node.Add("Matrix").DoubleArray(MatrixArray(bones.World[i]));
    }
  }

  private static void WriteConnections(Node root, ExportedMesh mesh, IdSource ids, BoneTables bones)
  {
    var connections = root.Add("Connections");
    Connect(connections, "OO", ids.MeshModel, 0);
    if (mesh.HasTriangles)
      Connect(connections, "OO", ids.Geometry, ids.MeshModel);
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var parent = mesh.Bones[i].Parent;
      var parentId = parent >= 0 && parent < ids.BoneModels.Length ? ids.BoneModels[parent] : 0;
      Connect(connections, "OO", ids.BoneModels[i], parentId);
      Connect(connections, "OO", ids.BoneAttributes[i], ids.BoneModels[i]);
    }

    var materials = CollectMaterials(mesh);
    foreach (var material in materials)
      Connect(connections, "OO", ids.Materials[material.Key], ids.MeshModel);

    foreach (var texture in CollectTextures(mesh))
    {
      if (!ids.Materials.TryGetValue(texture.Name, out var materialId))
        materialId = materials.Count > 0 ? ids.Materials[materials[0].Key] : 0;
      if (materialId != 0)
        Connect(connections, "OP", ids.Textures[texture.Name], materialId, "DiffuseColor");
      Connect(connections, "OO", ids.Videos[texture.Name], ids.Textures[texture.Name]);
    }

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
    var seen = new HashSet<string>(StringComparer.Ordinal);
    void Add(string? key, string? name)
    {
      if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
        return;
      list.Add(new MaterialRef(key, string.IsNullOrWhiteSpace(name) ? key : name));
    }

    foreach (var part in mesh.Parts)
      Add(MaterialKey(part), string.IsNullOrWhiteSpace(part.Texture) ? part.Name : part.Texture);
    if (mesh.Material is not null)
    {
      foreach (var slot in mesh.Material.Slots)
        Add(slot.Texture, slot.Texture);
    }

    if (list.Count == 0 && mesh.HasTriangles)
      list.Add(new MaterialRef(mesh.Name, mesh.Name));
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

    foreach (var part in mesh.Parts)
      Add(part.Texture);
    foreach (var level in mesh.Levels)
    {
      foreach (var part in level.Parts)
        Add(part.Texture);
    }

    if (mesh.Material is not null)
    {
      foreach (var slot in mesh.Material.Slots)
        Add(slot.Texture);
    }

    return list;
  }

  private static string MaterialKey(MeshPart part)
    => !string.IsNullOrWhiteSpace(part.Texture) ? part.Texture! : part.Name;

  private static string TextureFile(string name)
  {
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

  private static void ObjectType(Node definitions, string name, int count, string? template, Action<Node>? fill)
  {
    var type = definitions.Add("ObjectType");
    type.String(name);
    type.Add("Count").Int32(count);
    if (template is null || fill is null)
      return;
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

  private static void PropLcl(Node props, string name, double x, double y, double z)
  {
    var node = props.Add("P");
    node.String(name);
    node.String(name);
    node.String("");
    node.String("A");
    node.Double(x);
    node.Double(y);
    node.Double(z);
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

  private readonly record struct MaterialRef(string Key, string Name);
  private readonly record struct TextureRef(string Name);

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
    private static readonly HashSet<string> AlwaysSentinel = new(StringComparer.Ordinal);

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
