using System.Numerics;
using LibSaber.IO;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SpaceMarine2.Enumerations;
using LibSaber.SpaceMarine2.Serialization;
using LibSaber.SpaceMarine2.Serialization.Geometry;
using LibSaber.SpaceMarine2.Structures;
using LibSaber.SpaceMarine2.Structures.Geometry;
using LibSaber.SpaceMarine2.Structures.Materials;
using LibSaber.SpaceMarine2.Structures.Resources;

namespace LibSaber.SM2.Assets.Meshes;

public static class TemplateMeshReader
{

  public static ExportedMesh Load(GameCatalog catalog, ArchiveStore store, int fileIndex)
  {
    var file = catalog.GetFile(fileIndex);
    var mesh = new ExportedMesh { Name = AssetClassification.BaseName(file.Name) };
    var notes = new List<string>();

    if (file.Extension is ".tpl_data" or ".lods_base" or ".geom_dbg" or ".tpl_markup")
    {
      mesh.Message = $"{file.Extension} is an external model buffer. Retail meshes are split across .tpl, .tpl_data and .lods_base (debug data is .geom_dbg). Open the .tpl or .tpl.resource. This buffer's layout is not decoded on its own.";
      return mesh;
    }

    byte[] bytes;
    resDESC_TPL? description = null;
    if (file.Extension == ".tpl.resource")
    {
      bytes = store.Read(fileIndex);
      description = ReadDescription(bytes, notes);
      if (description is null)
      {
        mesh.Message = string.Join(" ", notes);
        return mesh;
      }

      mesh.Material = MaterialFromDescription(description);
      var tpl = FirstLinked(catalog, fileIndex, description.tpl);
      if (tpl < 0)
      {
        mesh.Message = "The .tpl.resource parsed, but its tpl file is not in the catalog. " + string.Join(" ", notes);
        return mesh;
      }

      notes.Add(DescribeLinks(description));
      bytes = store.Read(tpl);
    }
    else
    {
      bytes = store.Read(fileIndex);
    }

    if (TextureLoaderLooksText(bytes))
    {
      mesh.Message = "This model file is text. The binary .tpl is the one that can carry a skeleton. " + string.Join(" ", notes);
      return mesh;
    }

    animTPL? template = null;
    MemoryStream? stream = null;
    try
    {
      stream = new MemoryStream(bytes, writable: false);
      var reader = new NativeReader(stream, Endianness.LittleEndian);
      template = Serializer<animTPL>.Deserialize(reader);
      if (template?.GeometryGraph is { } graph)
        ReadGraph(mesh, graph, reader, notes);
      ReadSkeleton(mesh, template, notes);
    }
    catch (Exception ex)
    {
      notes.Add("The .tpl reader did not recognize this file (" + ex.Message + "). Retail geometry usually lives in .tpl_data / .lods_base and that packing is unverified.");
    }
    finally
    {
      stream?.Dispose();
    }

    if (template?.TexList is { Count: > 0 })
    {
      mesh.Material ??= new MaterialDocument { Name = mesh.Name };
      var slot = 0;
      foreach (var texture in template.TexList)
      {
        if (!string.IsNullOrWhiteSpace(texture))
          mesh.Material.Slots.Add(new MaterialSlot { Slot = "tex" + slot, Texture = texture });
        slot++;
      }
    }

    if (mesh.Positions.Count == 0)
      notes.Add("No vertex buffer was found in this .tpl. Retail files usually keep positions in .tpl_data or .lods_base; those are not decoded yet.");
    else
      notes.Add($"Decoded {mesh.Positions.Count} vertices and {mesh.Indices.Count / 3} triangles.");

    if (mesh.Bones.Count > 0)
      notes.Add($"{mesh.Bones.Count} bones.");

    mesh.Message = string.Join(" ", notes.Where(note => note.Length > 0));
    return mesh;
  }

  private static void ReadSkeleton(ExportedMesh mesh, animTPL? template, List<string> notes)
  {
    var objects = template?.GeometryGraph?.objects;
    if (objects is null || objects.Count == 0)
      return;

    for (var i = 0; i < objects.Count; i++)
    {
      var obj = objects[i];
      var name = obj.GetBoneName();
      if (string.IsNullOrWhiteSpace(name))
        name = obj.GetName();
      var parent = obj.parentId;
      mesh.Bones.Add(new Bone
      {
        Name = string.IsNullOrWhiteSpace(name) ? "bone" + i : name,
        Parent = parent >= 0 && parent < objects.Count && parent != i ? parent : -1,
        Local = obj.MatrixLT,
      });
    }

    var inverse = template?.skin?.boneInvBindMatrList;
    if (inverse is not null && inverse.Length == mesh.Bones.Count)
    {
      for (var i = 0; i < inverse.Length; i++)
        mesh.Bones[i].InverseBind = inverse[i];
    }
    else if (inverse is { Length: > 0 })
    {
      notes.Add($"Skin has {inverse.Length} inverse binds and the object list has {mesh.Bones.Count} nodes; they were not paired.");
    }
  }

  private static void ReadGraph(ExportedMesh mesh, objGEOM_MNG graph, NativeReader reader, List<string> notes)
  {
    if (!graph.ContainsVertexBuffers || graph.Buffers is null)
      return;

    var vertices = new List<Vertex>();
    var faces = new List<Face>();
    GeometryBuffer? vertexBuffer = null;
    GeometryBuffer? faceBuffer = null;
    foreach (var buffer in graph.Buffers)
    {
      if (buffer.ElementSize <= 0 || buffer.Count <= 0)
        continue;
      if (buffer.ElementType == GeometryElementType.Vertex && vertexBuffer is null)
        vertexBuffer = buffer;
      else if (buffer.ElementType == GeometryElementType.Face && faceBuffer is null)
        faceBuffer = buffer;
    }

    if (vertexBuffer is null)
      return;

    try
    {
      foreach (var vertex in new VertexSerializer(vertexBuffer).DeserializeRange(reader, 0, vertexBuffer.Count))
        vertices.Add(vertex);
      if (faceBuffer is not null)
      {
        foreach (var face in new FaceSerializer(faceBuffer).DeserializeRange(reader, 0, faceBuffer.Count))
          faces.Add(face);
      }
    }
    catch (Exception ex)
    {
      notes.Add("Vertex buffer was present but could not be decoded (" + ex.Message + ").");
      return;
    }

    var anySkin = vertices.Exists(vertex => vertex.HasSkinningData);
    if (graph.SubMeshes is { Count: > 0 })
    {
      foreach (var sub in graph.SubMeshes)
        AppendSubMesh(mesh, vertices, faces, sub, anySkin);
    }

    if (mesh.Positions.Count == 0)
    {
      foreach (var vertex in vertices)
        AddVertex(mesh, vertex, anySkin, boneIds: null);
      foreach (var face in faces)
      {
        mesh.Indices.Add(face.A);
        mesh.Indices.Add(face.B);
        mesh.Indices.Add(face.C);
      }

      if (mesh.Indices.Count > 0)
        mesh.Parts.Add(new MeshPart { Name = mesh.Name, IndexStart = 0, IndexCount = mesh.Indices.Count });
    }

    if (anySkin && mesh.Skin.Count != mesh.Positions.Count)
      mesh.Skin.Clear();
  }

  private static void AppendSubMesh(ExportedMesh mesh, List<Vertex> vertices, List<Face> faces, GeometrySubMesh sub, bool anySkin)
  {
    var info = sub.BufferInfo;
    if (info.VertexCount == 0 || info.VertexOffset + info.VertexCount > vertices.Count)
      return;

    var baseVertex = mesh.Positions.Count;
    for (var i = 0; i < info.VertexCount; i++)
      AddVertex(mesh, vertices[info.VertexOffset + i], anySkin, sub.BoneIds);

    var indexStart = mesh.Indices.Count;
    var faceEnd = Math.Min(faces.Count, info.FaceOffset + info.FaceCount);
    for (var i = info.FaceOffset; i < faceEnd; i++)
    {
      mesh.Indices.Add(baseVertex + LocalIndex(faces[i].A, info.VertexCount));
      mesh.Indices.Add(baseVertex + LocalIndex(faces[i].B, info.VertexCount));
      mesh.Indices.Add(baseVertex + LocalIndex(faces[i].C, info.VertexCount));
    }

    if (mesh.Indices.Count > indexStart)
    {
      mesh.Parts.Add(new MeshPart
      {
        Name = "submesh" + sub.NodeId,
        IndexStart = indexStart,
        IndexCount = mesh.Indices.Count - indexStart,
        Texture = FirstTexture(sub.Material),
      });
    }

    if (sub.Material is not null)
      AddMaterial(mesh, sub.Material, "node" + sub.NodeId);
  }

  private static int LocalIndex(int index, int vertexCount)
  {
    if (index >= 0 && index < vertexCount)
      return index;
    return 0;
  }

  private static void AddVertex(ExportedMesh mesh, Vertex vertex, bool anySkin, short[]? boneIds)
  {
    mesh.Positions.Add(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
    mesh.Normals.Add(vertex.HasNormal
      ? Vector3.Normalize(new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z))
      : Vector3.UnitZ);
    if (!anySkin)
      return;

    var skin = new SkinWeights();
    var slot = 0;
    Take(vertex.Index1, vertex.Weight1, vertex.HasWeight1, boneIds, ref skin, ref slot);
    Take(vertex.Index2, vertex.Weight2, vertex.HasWeight2, boneIds, ref skin, ref slot);
    Take(vertex.Index3, vertex.Weight3, vertex.HasWeight3, boneIds, ref skin, ref slot);
    Take(vertex.Index4, vertex.Weight4, vertex.HasWeight4, boneIds, ref skin, ref slot);
    if (slot == 0)
      skin.W0 = 1;
    mesh.Skin.Add(skin);
  }

  private static void Take(short index, float weight, bool present, short[]? boneIds, ref SkinWeights skin, ref int slot)
  {
    if (!present || weight <= 0 || slot > 3)
      return;
    var bone = index;
    if (boneIds is not null && (uint)index < (uint)boneIds.Length)
      bone = boneIds[index];
    if (bone < 0)
      return;

    var joint = (ushort)Math.Min(bone, ushort.MaxValue);
    switch (slot)
    {
      case 0: skin.J0 = joint; skin.W0 = weight; break;
      case 1: skin.J1 = joint; skin.W1 = weight; break;
      case 2: skin.J2 = joint; skin.W2 = weight; break;
      default: skin.J3 = joint; skin.W3 = weight; break;
    }

    slot++;
  }

  private static void AddMaterial(ExportedMesh mesh, SaberMaterial material, string name)
  {
    mesh.Material ??= new MaterialDocument { Name = mesh.Name };
    void Layer(string slot, MaterialLayer? layer)
    {
      if (layer is null || string.IsNullOrWhiteSpace(layer.TextureName))
        return;
      mesh.Material.Slots.Add(new MaterialSlot { Slot = name + "." + slot, Texture = layer.TextureName, Tint = layer.Tint });
    }

    Layer("layer0", material.Layer0);
    Layer("layer1", material.Layer1);
    Layer("layer2", material.Layer2);
    Layer("layer3", material.Layer3);
  }

  private static string? FirstTexture(SaberMaterial? material)
  {
    if (material is null)
      return null;
    return new[] { material.Layer0, material.Layer1, material.Layer2, material.Layer3 }
      .Select(layer => layer?.TextureName)
      .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
  }

  private static resDESC_TPL? ReadDescription(byte[] bytes, List<string> notes)
  {
    try
    {
      using var stream = new MemoryStream(bytes, writable: false);
      var reader = new NativeReader(stream, Endianness.LittleEndian);
      var description = Serializer<resDESC>.Deserialize(reader);
      if (description is resDESC_TPL tpl)
        return tpl;
      notes.Add(description.__type is null
        ? "Resource is not a model (res_desc_tpl)."
        : $"Resource is {description.__type}, not a model.");
      return null;
    }
    catch (Exception ex)
    {
      notes.Add("Could not read the model resource (" + ex.Message + ").");
      return null;
    }
  }

  private static MaterialDocument MaterialFromDescription(resDESC_TPL description)
  {
    var material = new MaterialDocument { Name = description.tpl ?? "model" };
    void Links(string slot, string[]? values)
    {
      if (values is null)
        return;
      var index = 0;
      foreach (var value in values)
      {
        if (!string.IsNullOrWhiteSpace(value))
          material.Slots.Add(new MaterialSlot { Slot = slot + index, Texture = value });
        index++;
      }
    }

    Links("pct", description.linksPct);
    Links("td", description.linksTd);
    Links("tpl", description.linksTpl);
    Links("anim", description.linksAnim);
    if (!string.IsNullOrWhiteSpace(description.tplData))
      material.Notes.Add("tplData: " + description.tplData);
    if (!string.IsNullOrWhiteSpace(description.tplLodsBase))
      material.Notes.Add("tplLodsBase: " + description.tplLodsBase);
    material.Notes.Add("Particle systems are not converted. Linked .ps / .sfx files, if any, stay raw.");
    return material;
  }

  private static string DescribeLinks(resDESC_TPL description)
  {
    var parts = new List<string>();
    if (!string.IsNullOrWhiteSpace(description.tpl))
      parts.Add("tpl=" + description.tpl);
    if (!string.IsNullOrWhiteSpace(description.tplData))
      parts.Add("tplData=" + description.tplData);
    if (!string.IsNullOrWhiteSpace(description.tplLodsBase))
      parts.Add("lods=" + description.tplLodsBase);
    return parts.Count == 0 ? "" : string.Join(", ", parts) + ".";
  }

  private static int FirstLinked(GameCatalog catalog, int hint, string? reference)
  {
    if (string.IsNullOrWhiteSpace(reference))
      return -1;
    return catalog.ResolveReference(reference, hint);
  }

  private static bool TextureLoaderLooksText(byte[] bytes)
    => TextureLoader.LooksText(bytes);

}
