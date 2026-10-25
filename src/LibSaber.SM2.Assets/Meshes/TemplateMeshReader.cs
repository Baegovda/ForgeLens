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

    var usedNames = new HashSet<string>(StringComparer.Ordinal);
    for (var i = 0; i < objects.Count; i++)
    {
      var obj = objects[i];
      var name = obj.GetName();
      if (string.IsNullOrWhiteSpace(name))
        name = "bone" + i;
      if (!usedNames.Add(name))
      {
        var suffix = 2;
        while (!usedNames.Add(name + "_" + suffix))
          suffix++;
        name = name + "_" + suffix;
      }

      var parent = obj.parentId;
      mesh.Bones.Add(new Bone
      {
        Name = name,
        Parent = parent >= 0 && parent < objects.Count && parent != i ? parent : -1,
        Local = obj.MatrixModel,
      });
    }

    // Retail inverse binds match Inverse(world) when MatrixModel is the local
    // matrix and a child is applied before its parent (local * parentWorld).
    var worlds = new Matrix4x4[objects.Count];
    var done = new bool[objects.Count];
    for (var guard = 0; guard < objects.Count; guard++)
    {
      var progress = false;
      for (var i = 0; i < objects.Count; i++)
      {
        if (done[i])
          continue;
        var parent = mesh.Bones[i].Parent;
        if (parent >= 0 && !done[parent])
          continue;
        var local = mesh.Bones[i].Local;
        worlds[i] = parent >= 0 ? local * worlds[parent] : local;
        done[i] = true;
        progress = true;
      }

      if (!progress)
        break;
    }

    var inverse = template?.skin?.boneInvBindMatrList;
    var paired = 0;
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      var computed = Matrix4x4.Identity;
      var haveComputed = done[i] && Matrix4x4.Invert(worlds[i], out computed);
      if (inverse is not null && i < inverse.Length && haveComputed && MatrixDiff(computed, inverse[i]) < 0.05f)
      {
        mesh.Bones[i].InverseBind = inverse[i];
        mesh.Bones[i].FileBind = true;
        paired++;
      }
      else
      {
        mesh.Bones[i].InverseBind = computed;
      }
    }

    if (inverse is { Length: > 0 })
    {
      notes.Add(paired == inverse.Length
        ? $"Paired all {paired} inverse binds to the first {paired} of {mesh.Bones.Count} nodes (MatrixModel, child before parent)."
        : $"Paired {paired} of {inverse.Length} inverse binds by matrix match against {mesh.Bones.Count} nodes. Unmatched nodes use the inverse of their composed world so the bind pose stays put.");
    }
  }

  private static void MarkInfluences(ExportedMesh mesh)
  {
    void Mark(IReadOnlyList<SkinWeights> skin)
    {
      foreach (var weights in skin)
      {
        void One(ushort joint, float weight)
        {
          if (weight > 0.02f && joint < mesh.Bones.Count)
            mesh.Bones[joint].Influences = true;
        }

        One(weights.J0, weights.W0);
        One(weights.J1, weights.W1);
        One(weights.J2, weights.W2);
        One(weights.J3, weights.W3);
      }
    }

    Mark(mesh.Skin);
    foreach (var level in mesh.Levels)
      Mark(level.Skin);

    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      if (!mesh.Bones[i].Influences)
        continue;
      var parent = mesh.Bones[i].Parent;
      var guard = 0;
      while (parent >= 0 && parent < mesh.Bones.Count && guard++ < mesh.Bones.Count)
      {
        if (mesh.Bones[parent].Influences)
          break;
        mesh.Bones[parent].Influences = true;
        parent = mesh.Bones[parent].Parent;
      }
    }
  }

  private static float MatrixDiff(Matrix4x4 a, Matrix4x4 b)
  {
    float d = 0;
    d += MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13) + MathF.Abs(a.M14 - b.M14);
    d += MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) + MathF.Abs(a.M23 - b.M23) + MathF.Abs(a.M24 - b.M24);
    d += MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33) + MathF.Abs(a.M34 - b.M34);
    d += MathF.Abs(a.M41 - b.M41) + MathF.Abs(a.M42 - b.M42) + MathF.Abs(a.M43 - b.M43) + MathF.Abs(a.M44 - b.M44);
    return d;
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

    var groups = GroupByVertexBuffer(graph);
    if (groups.Count == 0)
    {
      notes.Add("The .tpl has buffers but no mesh that points at them.");
      return;
    }

    var rejected = 0;
    try
    {
      for (var lod = 0; lod < groups.Count; lod++)
      {
        var group = groups[lod];
        var target = new Surface(lod == 0 ? "LOD0" : "LOD" + lod);
        var skinned = (group.Vertex.Flags & (FVFFlags.WEIGHT4 | FVFFlags.WEIGHT8)) != 0;
        foreach (var slice in group.Slices)
          DecodeSlice(mesh, graph, reader, slice, target, skinned, ref rejected);
        if (skinned && target.Skin.Count != target.Positions.Count)
          target.Skin.Clear();
        if (lod == 0)
          target.CopyTo(mesh);
        else if (target.Indices.Count >= 3)
          mesh.Levels.Add(target.ToLod());
      }
    }
    catch (Exception ex)
    {
      notes.Add("Vertex buffer was present but could not be decoded (" + ex.Message + ").");
      return;
    }

    if (mesh.Skin.Count == 0)
      notes.Add("This mesh has no per-vertex skin weights.");
    else
      MarkInfluences(mesh);
    notes.Add($"LOD0 is every part on the largest vertex pool: {mesh.Positions.Count:N0} vertices, {mesh.Indices.Count / 3:N0} triangles, {mesh.Parts.Count} parts.");
    if (mesh.Levels.Count > 0)
      notes.Add("Other LODs: " + string.Join(", ", mesh.Levels.Select(level => level.Positions.Count.ToString("N0"))) + " vertices.");
    if (rejected > 0)
      notes.Add($"{rejected:N0} triangles sat outside their submesh range and were dropped.");

    var textureNames = mesh.Parts.Concat(mesh.Levels.SelectMany(level => level.Parts))
      .Select(part => part.Texture)
      .Where(name => !string.IsNullOrWhiteSpace(name))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
    if (textureNames.Count > 0)
    {
      var resolved = textureNames.Count(name => ResolveTexture(catalog, fileIndex, name!) >= 0);
      notes.Add(resolved == 0
        ? "Part materials name diffuse textures, and none of them are in this folder, so the preview stays a lit solid color."
        : $"Resolved {resolved} of {textureNames.Count} diffuse textures for the preview.");
    }

    var box = template.bbox;
    notes.Add($"File bounds {box.Min.X:0.###},{box.Min.Y:0.###},{box.Min.Z:0.###} .. {box.Max.X:0.###},{box.Max.Y:0.###},{box.Max.Z:0.###}.");
  }

  private readonly struct MeshSlice
  {
    public int MeshIndex { get; init; }
    public int VertBase { get; init; }
    public int FaceBase { get; init; }
    public int UvBase { get; init; }
    public GeometryBuffer Vertex { get; init; }
    public GeometryBuffer Face { get; init; }
    public GeometryBuffer? Uv { get; init; }
  }

  private sealed class MeshGroup
  {
    public GeometryBuffer Vertex { get; init; } = null!;
    public List<MeshSlice> Slices { get; } = new();
  }

  private sealed class Surface
  {
    public Surface(string name) => Name = name;
    public string Name { get; }
    public List<Vector3> Positions { get; } = new();
    public List<Vector3> Normals { get; } = new();
    public List<Vector2> Uvs { get; } = new();
    public List<int> Indices { get; } = new();
    public List<SkinWeights> Skin { get; } = new();
    public List<MeshPart> Parts { get; } = new();
    public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void CopyTo(ExportedMesh mesh)
    {
      mesh.Positions.AddRange(Positions);
      mesh.Normals.AddRange(Normals);
      mesh.Uvs.AddRange(Uvs);
      mesh.Indices.AddRange(Indices);
      mesh.Skin.AddRange(Skin);
      mesh.Parts.AddRange(Parts);
    }

    public MeshLod ToLod()
    {
      var lod = new MeshLod { Name = Name };
      lod.Positions.AddRange(Positions);
      lod.Normals.AddRange(Normals);
      lod.Uvs.AddRange(Uvs);
      lod.Indices.AddRange(Indices);
      lod.Skin.AddRange(Skin);
      lod.Parts.AddRange(Parts);
      return lod;
    }
  }

  private static List<MeshGroup> GroupByVertexBuffer(objGEOM_MNG graph)
  {
    var groups = new List<MeshGroup>();
    if (graph.Meshes is not { Count: > 0 } || graph.Buffers is null)
      return groups;

    var byBuffer = new Dictionary<int, MeshGroup>();
    for (var meshIndex = 0; meshIndex < graph.Meshes.Count; meshIndex++)
    {
      if (!TrySlice(graph, meshIndex, out var slice))
        continue;
      if (!byBuffer.TryGetValue(BufferId(graph, graph.Meshes[meshIndex]), out var group))
      {
        group = new MeshGroup { Vertex = slice.Vertex };
        byBuffer.Add(BufferId(graph, graph.Meshes[meshIndex]), group);
        groups.Add(group);
      }

      group.Slices.Add(slice);
    }

    groups.Sort((a, b) => b.Vertex.Count.CompareTo(a.Vertex.Count));
    return groups;
  }

  private static int BufferId(objGEOM_MNG graph, GeometryMesh mesh)
  {
    if (mesh.Buffers is null || graph.Buffers is null)
      return -1;
    foreach (var info in mesh.Buffers)
    {
      if ((uint)info.BufferId >= (uint)graph.Buffers.Count)
        continue;
      if (graph.Buffers[info.BufferId].ElementType == GeometryElementType.Vertex)
        return info.BufferId;
    }

    return -1;
  }

  private static bool TrySlice(objGEOM_MNG graph, int meshIndex, out MeshSlice slice)
  {
    slice = default;
    var source = graph.Meshes![meshIndex];
    if (source.Buffers is null || graph.Buffers is null)
      return false;

    GeometryMeshBufferInfo vertexInfo = default;
    GeometryMeshBufferInfo faceInfo = default;
    GeometryMeshBufferInfo uvInfo = default;
    GeometryBuffer? vertex = null;
    GeometryBuffer? face = null;
    GeometryBuffer? uv = null;
    foreach (var info in source.Buffers)
    {
      if ((uint)info.BufferId >= (uint)graph.Buffers.Count)
        continue;
      var buffer = graph.Buffers[info.BufferId];
      if (buffer.ElementSize <= 0 || buffer.Count <= 0)
        continue;
      if (buffer.ElementType == GeometryElementType.Vertex && vertex is null)
      {
        vertex = buffer;
        vertexInfo = info;
      }
      else if (buffer.ElementType == GeometryElementType.Face && face is null)
      {
        face = buffer;
        faceInfo = info;
      }
      else if (buffer.ElementType == GeometryElementType.Interleaved && uv is null)
      {
        uv = buffer;
        uvInfo = info;
      }
    }

    if (vertex is null || face is null)
      return false;

    slice = new MeshSlice
    {
      MeshIndex = meshIndex,
      VertBase = vertexInfo.SubBufferOffset / vertex.ElementSize,
      FaceBase = faceInfo.SubBufferOffset / face.ElementSize,
      UvBase = uv is null ? 0 : uvInfo.SubBufferOffset / uv.ElementSize,
      Vertex = vertex,
      Face = face,
      Uv = uv,
    };
    return slice.VertBase >= 0 && slice.VertBase <= vertex.Count && slice.FaceBase >= 0 && slice.FaceBase <= face.Count;
  }

  private static void DecodeSlice(ExportedMesh mesh, objGEOM_MNG graph, NativeReader reader, MeshSlice slice, Surface target, bool skinned, ref int rejected)
  {
    var subs = graph.SubMeshes?.Where(sub => sub.MeshId == slice.MeshIndex).ToList() ?? new List<GeometrySubMesh>();
    if (subs.Count == 0)
    {
      var vertCount = Math.Min(ushort.MaxValue, Math.Max(0, slice.Vertex.Count - slice.VertBase));
      var faceCount = Math.Min(ushort.MaxValue, Math.Max(0, slice.Face.Count - slice.FaceBase));
      if (vertCount == 0 || faceCount == 0)
        return;
      subs.Add(new GeometrySubMesh
      {
        MeshId = slice.MeshIndex,
        BufferInfo = new GeometrySubMeshBufferInfo
        {
          VertexOffset = 0,
          VertexCount = (ushort)vertCount,
          FaceOffset = 0,
          FaceCount = (ushort)faceCount,
        },
      });
    }

    foreach (var sub in subs)
    {
      var info = sub.BufferInfo;
      if (info.VertexCount == 0 || info.FaceCount == 0)
        continue;
      var vertexStart = slice.VertBase + info.VertexOffset;
      var vertexEnd = vertexStart + info.VertexCount;
      if (vertexStart < 0 || vertexEnd > slice.Vertex.Count)
        continue;
      var faceStart = slice.FaceBase + info.FaceOffset;
      var faceEnd = faceStart + info.FaceCount;
      if (faceStart < 0 || faceEnd > slice.Face.Count)
        continue;

      var vertices = new VertexSerializer(slice.Vertex, sub.Position, sub.Scale)
        .DeserializeRange(reader, vertexStart, vertexEnd)
        .ToArray();
      if (!skinned)
      {
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
      }

      var uvs = ReadUvs(reader, slice, sub, info.VertexOffset, vertices.Length);
      var faces = new FaceSerializer(slice.Face).DeserializeRange(reader, faceStart, faceEnd).ToArray();
      var partName = PartName(graph, sub, slice.MeshIndex);
      AppendRange(mesh, target, vertices, uvs, faces, info.VertexOffset, skinned, sub.BoneIds, partName, sub.Material, ref rejected);
    }
  }

  private static Vector2[] ReadUvs(NativeReader reader, MeshSlice slice, GeometrySubMesh sub, int vertexOffset, int count)
  {
    var uvs = new Vector2[count];
    if (slice.Uv is null || count == 0)
      return uvs;
    var start = slice.UvBase + vertexOffset;
    var end = start + count;
    if (start < 0 || end > slice.Uv.Count)
      return uvs;

    var layer = DiffuseLayer(sub.Material);
    var set = layer is { UvSetIndex: >= 0 } ? layer.UvSetIndex : 0;
    var scale = UvScale(slice.Uv.Flags, sub, set);
    var tileU = layer is { TilingU: > 0 and < 64 } ? layer.TilingU : 1f;
    var tileV = layer is { TilingV: > 0 and < 64 } ? layer.TilingV : 1f;
    var index = 0;
    foreach (var data in new InterleavedDataSerializer(slice.Uv).DeserializeRange(reader, start, end))
    {
      if (index >= uvs.Length)
        break;
      // The serializer stores V already flipped (1 - snorm). The submesh scale was divided out
      // before packing, so a scale of 2 leaves V in the top half of the texture until it is put back.
      var source = UvChannel(data, set);
      var u = source.X * scale * tileU;
      var snormV = 1f - source.Y;
      var v = 1f - snormV * scale * tileV;
      uvs[index++] = new Vector2(u, v);
    }

    return uvs;
  }

  private static float UvScale(FVFFlags flags, GeometrySubMesh sub, int set)
  {
    var compressed = set switch
    {
      1 => (flags & FVFFlags.TEX1_COMPR) != 0,
      2 => (flags & FVFFlags.TEX2_COMPR) != 0,
      3 => (flags & FVFFlags.TEX3_COMPR) != 0,
      _ => (flags & FVFFlags.TEX0_COMPR) != 0,
    };
    if (!compressed || !sub.UvScaling.TryGetValue((byte)set, out var raw) || raw == 0)
      return 1f;
    return raw;
  }

  private static Vector4 UvChannel(InterleavedData data, int set)
  {
    if (set == 1 && data.HasUV1)
      return data.UV1;
    if (set == 2 && data.HasUV2)
      return data.UV2;
    if (set == 3 && data.HasUV3)
      return data.UV3;
    return data.UV0;
  }

  private static MaterialLayer? DiffuseLayer(SaberMaterial? material)
  {
    if (material is null)
      return null;
    foreach (var layer in new[] { material.Layer0, material.Layer1, material.Layer2, material.Layer3 })
    {
      if (layer is not null && !string.IsNullOrWhiteSpace(layer.TextureName))
        return layer;
    }

    return material.Layer0;
  }

  private static string PartName(objGEOM_MNG graph, GeometrySubMesh sub, int meshIndex)
  {
    var texture = FirstTexture(sub.Material);
    if (!string.IsNullOrWhiteSpace(texture))
      return texture!;
    if (graph.objects is not null && (uint)sub.NodeId < (uint)graph.objects.Count)
    {
      var name = graph.objects[sub.NodeId].GetName();
      if (!string.IsNullOrWhiteSpace(name))
        return name;
    }

    return "part" + meshIndex + "_" + sub.NodeId;
  }

  public static int ResolveTexture(GameCatalog catalog, int hint, string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return -1;
    var candidates = new[]
    {
      name,
      name + ".pct.resource",
      name + ".pct",
      "pct/" + name + ".pct.resource",
      "pct/" + name + ".pct",
    };
    foreach (var candidate in candidates)
    {
      var index = catalog.ResolveReference(candidate, hint);
      if (index >= 0)
        return index;
    }

    return -1;
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
    Surface target,
    Vertex[] vertices,
    Vector2[] uvs,
    Face[] faces,
    int indexBase,
    bool anySkin,
    short[]? boneIds,
    string partName,
    SaberMaterial? material,
    ref int rejected)
  {
    if (vertices.Length == 0 || faces.Length == 0)
      return;

    var baseVertex = target.Positions.Count;
    for (var i = 0; i < vertices.Length; i++)
    {
      var uv = (uint)i < (uint)uvs.Length ? uvs[i] : Vector2.Zero;
      AddVertex(target, vertices[i], uv, anySkin, boneIds);
    }

    var indexStart = target.Indices.Count;
    foreach (var face in faces)
    {
      if (!TryLocal(face.A, indexBase, vertices.Length, out var a)
        || !TryLocal(face.B, indexBase, vertices.Length, out var b)
        || !TryLocal(face.C, indexBase, vertices.Length, out var c))
      {
        rejected++;
        continue;
      }

      target.Indices.Add(baseVertex + a);
      target.Indices.Add(baseVertex + b);
      target.Indices.Add(baseVertex + c);
    }

    if (target.Indices.Count > indexStart)
    {
      var display = partName;
      if (!target.Names.Add(display))
      {
        var suffix = 2;
        while (!target.Names.Add(partName + " (" + suffix + ")"))
          suffix++;
        display = partName + " (" + suffix + ")";
      }

      target.Parts.Add(new MeshPart
      {
        Name = display,
        IndexStart = indexStart,
        IndexCount = target.Indices.Count - indexStart,
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

  private static void AddVertex(Surface target, Vertex vertex, Vector2 uv, bool anySkin, short[]? boneIds)
  {
    target.Positions.Add(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z));
    var normal = vertex.HasNormal
      ? new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z)
      : Vector3.UnitY;
    if (normal.LengthSquared() < 1e-8f)
      normal = Vector3.UnitY;
    target.Normals.Add(Vector3.Normalize(normal));
    target.Uvs.Add(uv);
    if (!anySkin)
      return;

    var skin = new SkinWeights();
    var slot = 0;
    Take(vertex.Index1, vertex.Weight1, vertex.HasWeight1, boneIds, ref skin, ref slot);
    Take(vertex.Index2, vertex.Weight2, vertex.HasWeight2, boneIds, ref skin, ref slot);
    Take(vertex.Index3, vertex.Weight3, vertex.HasWeight3, boneIds, ref skin, ref slot);
    var fourth = vertex.HasWeight4
      && !(vertex.Weight4 > 0.99f && vertex.Weight1 + vertex.Weight2 + vertex.Weight3 > 0.97f);
    Take(vertex.Index4, vertex.Weight4, fourth, boneIds, ref skin, ref slot);
    // Byte weights such as 128+128 sum to 256/255, and a vertex can list the same
    // bone twice. glTF requires unique joints and a sum of 1.
    NormalizeSkin(ref skin);
    target.Skin.Add(skin);
  }

  private static void NormalizeSkin(ref SkinWeights skin)
  {
    Span<(ushort Joint, float Weight)> slots = stackalloc (ushort, float)[4];
    var count = 0;
    count = Accumulate(slots, count, skin.J0, skin.W0);
    count = Accumulate(slots, count, skin.J1, skin.W1);
    count = Accumulate(slots, count, skin.J2, skin.W2);
    count = Accumulate(slots, count, skin.J3, skin.W3);

    var sum = 0f;
    for (var i = 0; i < count; i++)
      sum += slots[i].Weight;

    skin = default;
    if (sum <= 0.0001f)
    {
      skin.W0 = 1;
      return;
    }

    var scale = 1f / sum;
    if (count > 0)
    {
      skin.J0 = slots[0].Joint;
      skin.W0 = slots[0].Weight * scale;
    }

    if (count > 1)
    {
      skin.J1 = slots[1].Joint;
      skin.W1 = slots[1].Weight * scale;
    }

    if (count > 2)
    {
      skin.J2 = slots[2].Joint;
      skin.W2 = slots[2].Weight * scale;
    }

    if (count > 3)
    {
      skin.J3 = slots[3].Joint;
      skin.W3 = slots[3].Weight * scale;
    }
  }

  private static int Accumulate(Span<(ushort Joint, float Weight)> slots, int count, ushort joint, float weight)
  {
    if (weight <= 0)
      return count;
    for (var i = 0; i < count; i++)
    {
      if (slots[i].Joint != joint)
        continue;
      slots[i].Weight += weight;
      return count;
    }

    slots[count] = (joint, weight);
    return count + 1;
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
