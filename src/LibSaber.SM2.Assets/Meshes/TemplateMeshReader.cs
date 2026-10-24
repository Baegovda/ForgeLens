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
      ReadSkeleton(mesh, template, notes);
      if (template?.GeometryGraph is { } graph)
        ReadGraph(mesh, graph, template, reader, catalog, store, fileIndex, description, notes);
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
      notes.Add("No triangles were decoded. The .tpl was read, and a sibling .tpl_data is required when the vertex bytes are not inside the .tpl.");
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

  private static void ReadGraph(
    ExportedMesh mesh,
    objGEOM_MNG graph,
    animTPL template,
    NativeReader inline,
    GameCatalog catalog,
    ArchiveStore store,
    int fileIndex,
    resDESC_TPL? description,
    List<string> notes)
  {
    if (graph.Buffers is null || graph.Buffers.Count == 0)
      return;

    var external = graph.ContainsVertexBuffers ? null : FindGeometryPayload(catalog, store, fileIndex, description);
    if (!graph.ContainsVertexBuffers && external is null)
    {
      notes.Add("The .tpl describes geometry buffers, but the paired .tpl_data is not in this folder.");
      return;
    }

    NativeReader reader;
    if (external is null)
      reader = inline;
    else
    {
      notes.Add($"Positions, normals, UVs, and indices were read from .tpl_data ({external.Length:N0} bytes).");
      reader = new NativeReader(new MemoryStream(external, writable: false), Endianness.LittleEndian);
    }

    var chosen = ChooseMesh(graph);
    if (chosen < 0)
    {
      notes.Add("The .tpl has buffers but no mesh that points at them.");
      return;
    }

    var source = graph.Meshes![chosen];
    GeometryBuffer? vertexBuffer = null;
    GeometryBuffer? faceBuffer = null;
    GeometryBuffer? uvBuffer = null;
    if (source.Buffers is not null)
    {
      foreach (var info in source.Buffers)
      {
        if ((uint)info.BufferId >= (uint)graph.Buffers.Count)
          continue;
        var buffer = graph.Buffers[info.BufferId];
        if (buffer.ElementSize <= 0 || buffer.Count <= 0)
          continue;
        if (buffer.ElementType == GeometryElementType.Vertex && vertexBuffer is null)
          vertexBuffer = buffer;
        else if (buffer.ElementType == GeometryElementType.Face && faceBuffer is null)
          faceBuffer = buffer;
        else if (buffer.ElementType == GeometryElementType.Interleaved && uvBuffer is null)
          uvBuffer = buffer;
      }
    }

    if (vertexBuffer is null || faceBuffer is null)
    {
      notes.Add("The chosen mesh has no vertex or index buffer.");
      return;
    }

    Face[] faces;
    Vector2[] uvs;
    try
    {
      faces = new FaceSerializer(faceBuffer).DeserializeRange(reader, 0, faceBuffer.Count).ToArray();
      uvs = uvBuffer is null
        ? Array.Empty<Vector2>()
        : new InterleavedDataSerializer(uvBuffer).DeserializeRange(reader, 0, uvBuffer.Count)
          .Select(data => new Vector2(data.UV0.X, data.UV0.Y))
          .ToArray();
    }
    catch (Exception ex)
    {
      notes.Add("Index or UV buffer could not be decoded (" + ex.Message + ").");
      return;
    }

    var subs = graph.SubMeshes?.Where(sub => sub.MeshId == chosen).ToList() ?? new List<GeometrySubMesh>();
    if (subs.Count == 0)
    {
      subs.Add(new GeometrySubMesh
      {
        MeshId = chosen,
        BufferInfo = new GeometrySubMeshBufferInfo
        {
          VertexOffset = 0,
          VertexCount = (ushort)Math.Min(ushort.MaxValue, vertexBuffer.Count),
          FaceOffset = 0,
          FaceCount = (ushort)Math.Min(ushort.MaxValue, faces.Length),
        },
      });
    }

    var anySkin = false;
    var decodedVertices = 0;
    try
    {
      foreach (var sub in subs)
      {
        var info = sub.BufferInfo;
        if (info.VertexCount == 0 || info.VertexOffset + info.VertexCount > vertexBuffer.Count)
          continue;
        var vertices = new VertexSerializer(vertexBuffer, sub.Position, sub.Scale)
          .DeserializeRange(reader, info.VertexOffset, info.VertexOffset + info.VertexCount)
          .ToArray();
        if (vertices.Any(vertex => vertex.HasSkinningData))
          anySkin = true;
        var matrix = NodeMatrix(graph, sub.NodeId);
        if (matrix is { } transform)
        {
          for (var i = 0; i < vertices.Length; i++)
          {
            var vertex = vertices[i];
            var position = TransformPoint(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), transform);
            vertex.Position = new Vector4(position, 1);
            if (vertex.HasNormal)
            {
              var normal = TransformDirection(new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z), transform);
              vertex.Normal = new Vector4(normal, vertex.Normal.W);
            }

            vertices[i] = vertex;
          }
        }

        var sliceUvs = new Vector2[vertices.Length];
        for (var i = 0; i < sliceUvs.Length; i++)
        {
          var sourceIndex = info.VertexOffset + i;
          sliceUvs[i] = (uint)sourceIndex < (uint)uvs.Length ? uvs[sourceIndex] : Vector2.Zero;
        }

        AppendRange(mesh, vertices, sliceUvs, faces, info.VertexOffset, info.FaceOffset, info.FaceCount, anySkin, sub.BoneIds, "part" + sub.MeshId + "_" + sub.NodeId, sub.Material);
        decodedVertices += vertices.Length;
      }
    }
    catch (Exception ex)
    {
      notes.Add("Vertex buffer was present but could not be decoded (" + ex.Message + ").");
      return;
    }

    if (anySkin && mesh.Skin.Count != mesh.Positions.Count)
      mesh.Skin.Clear();
    else if (!anySkin)
      notes.Add("This mesh has no per-vertex skin weights.");

    if (graph.Meshes.Count > 1)
    {
      var lods = graph.Meshes.Select(item => VertexCount(graph, item)).ToArray();
      notes.Add($"Exported the densest mesh ({decodedVertices} vertices). Other meshes in the file: {string.Join(", ", lods)}.");
    }

    var box = template.bbox;
    notes.Add($"File bounds {box.Min.X:0.###},{box.Min.Y:0.###},{box.Min.Z:0.###} .. {box.Max.X:0.###},{box.Max.Y:0.###},{box.Max.Z:0.###}.");
  }

  private static int ChooseMesh(objGEOM_MNG graph)
  {
    if (graph.Meshes is not { Count: > 0 })
      return -1;

    var best = 0;
    var count = -1;
    for (var i = 0; i < graph.Meshes.Count; i++)
    {
      var vertices = VertexCount(graph, graph.Meshes[i]);
      if (vertices > count)
      {
        count = vertices;
        best = i;
      }
    }

    return count > 0 ? best : -1;
  }

  private static int VertexCount(objGEOM_MNG graph, GeometryMesh mesh)
  {
    if (mesh.Buffers is null || graph.Buffers is null)
      return 0;
    foreach (var info in mesh.Buffers)
    {
      if ((uint)info.BufferId >= (uint)graph.Buffers.Count)
        continue;
      var buffer = graph.Buffers[info.BufferId];
      if (buffer.ElementType == GeometryElementType.Vertex)
        return buffer.Count;
    }

    return 0;
  }

  private static byte[]? FindGeometryPayload(GameCatalog catalog, ArchiveStore store, int fileIndex, resDESC_TPL? description)
  {
    var file = catalog.GetFile(fileIndex);
    var names = new List<string>();
    if (!string.IsNullOrWhiteSpace(description?.tplData))
      names.Add(description.tplData);
    if (file.Extension.Equals(".tpl", StringComparison.OrdinalIgnoreCase))
      names.Add(file.Path + "_data");
    if (!string.IsNullOrWhiteSpace(description?.tpl))
      names.Add(description.tpl + "_data");

    foreach (var name in names)
    {
      var index = catalog.ResolveReference(name, fileIndex);
      if (index >= 0)
        return store.Read(index);
    }

    return null;
  }

  private static void AppendRange(
    ExportedMesh mesh,
    Vertex[] vertices,
    Vector2[] uvs,
    Face[] faces,
    int indexBase,
    int faceOffset,
    int faceCount,
    bool anySkin,
    short[]? boneIds,
    string partName,
    SaberMaterial? material)
  {
    if (vertices.Length == 0 || faceCount <= 0)
      return;

    var baseVertex = mesh.Positions.Count;
    for (var i = 0; i < vertices.Length; i++)
    {
      var uv = (uint)i < (uint)uvs.Length ? uvs[i] : Vector2.Zero;
      AddVertex(mesh, vertices[i], uv, anySkin, boneIds);
    }

    var indexStart = mesh.Indices.Count;
    var faceEnd = Math.Min(faces.Length, faceOffset + faceCount);
    for (var i = Math.Max(0, faceOffset); i < faceEnd; i++)
    {
      if (!TryLocal(faces[i].A, indexBase, vertices.Length, out var a)
        || !TryLocal(faces[i].B, indexBase, vertices.Length, out var b)
        || !TryLocal(faces[i].C, indexBase, vertices.Length, out var c))
        continue;
      mesh.Indices.Add(baseVertex + a);
      mesh.Indices.Add(baseVertex + b);
      mesh.Indices.Add(baseVertex + c);
    }

    if (mesh.Indices.Count > indexStart)
    {
      mesh.Parts.Add(new MeshPart
      {
        Name = partName,
        IndexStart = indexStart,
        IndexCount = mesh.Indices.Count - indexStart,
        Texture = FirstTexture(material),
      });
    }

    if (material is not null)
      AddMaterial(mesh, material, partName);
  }

  private static Matrix4x4? NodeMatrix(objGEOM_MNG graph, int nodeId)
  {
    if (graph.objects is null || (uint)nodeId >= (uint)graph.objects.Count)
      return null;
    var matrix = graph.objects[nodeId].MatrixModel;
    var weight = MathF.Abs(matrix.M11) + MathF.Abs(matrix.M22) + MathF.Abs(matrix.M33) + MathF.Abs(matrix.M44);
    return weight > 0.5f ? matrix : null;
  }

  private static Vector3 TransformPoint(Vector3 point, Matrix4x4 matrix)
    => new(
      point.X * matrix.M11 + point.Y * matrix.M21 + point.Z * matrix.M31 + matrix.M41,
      point.X * matrix.M12 + point.Y * matrix.M22 + point.Z * matrix.M32 + matrix.M42,
      point.X * matrix.M13 + point.Y * matrix.M23 + point.Z * matrix.M33 + matrix.M43);

  private static Vector3 TransformDirection(Vector3 direction, Matrix4x4 matrix)
    => new(
      direction.X * matrix.M11 + direction.Y * matrix.M21 + direction.Z * matrix.M31,
      direction.X * matrix.M12 + direction.Y * matrix.M22 + direction.Z * matrix.M32,
      direction.X * matrix.M13 + direction.Y * matrix.M23 + direction.Z * matrix.M33);

  private static bool TryLocal(int index, int vertexOffset, int vertexCount, out int local)
  {
    local = index - vertexOffset;
    return local >= 0 && local < vertexCount;
  }

  private static void AddVertex(ExportedMesh mesh, Vertex vertex, Vector2 uv, bool anySkin, short[]? boneIds)
  {
    mesh.Positions.Add(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
    var normal = vertex.HasNormal
      ? new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z)
      : Vector3.UnitZ;
    if (normal.LengthSquared() < 1e-8f)
      normal = Vector3.UnitZ;
    mesh.Normals.Add(Vector3.Normalize(normal));
    mesh.Uvs.Add(uv);
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
