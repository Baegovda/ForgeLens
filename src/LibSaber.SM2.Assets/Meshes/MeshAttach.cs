using System.Numerics;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   mtl1-46: merge an attached actor's template (weapon skin attachingActors, e.g. purity seals) into a host mesh under a
///   locator node. The game attaches the actor to the locator with the actor's own geom.scale. Here the attachment's root
///   nodes become children of the locator (root Local times the scale), its LOD0 vertices move into host model space,
///   and inverse binds are rebased so the skinned bind pose stays put. Only <see cref="ExportedMesh.Parts"/> is merged: pass
///   <see cref="ExportedMesh.VisibleGeometry"/> so visible parts on other vertex pools (the seal's paper_rend) come along.
/// </summary>
public static class MeshAttach
{
  /// <summary>Host bone whose Name or SourceName is <paramref name="locator"/>, or -1.</summary>
  public static int FindNode(ExportedMesh mesh, string locator)
  {
    for (var i = 0; i < mesh.Bones.Count; i++)
    {
      if (mesh.Bones[i].SourceName.Equals(locator, StringComparison.OrdinalIgnoreCase) || mesh.Bones[i].Name.Equals(locator, StringComparison.OrdinalIgnoreCase))
        return i;
    }

    return -1;
  }

  public static Matrix4x4[] Worlds(IReadOnlyList<Bone> bones)
  {
    var worlds = new Matrix4x4[bones.Count];
    var state = new byte[bones.Count];
    Matrix4x4 Get(int i)
    {
      if (state[i] == 2)
        return worlds[i];
      var parent = bones[i].Parent;
      if (state[i] == 1 || parent < 0 || parent >= bones.Count)
        worlds[i] = bones[i].Local;
      else
      {
        state[i] = 1;
        worlds[i] = bones[i].Local * Get(parent);
      }

      state[i] = 2;
      return worlds[i];
    }

    for (var i = 0; i < bones.Count; i++)
      Get(i);
    return worlds;
  }

  /// <summary>Returns the number of parts merged (0 when the locator is missing or the attachment has no triangles).</summary>
  public static int Merge(ExportedMesh host, ExportedMesh attachment, int locatorNode, float scale, string prefix)
  {
    if (locatorNode < 0 || locatorNode >= host.Bones.Count || attachment.Indices.Count < 3)
      return 0;

    var hostVertices = host.Positions.Count;
    var hostHasNormals = host.Normals.Count == hostVertices;
    var hostHasUvs = host.Uvs.Count == hostVertices;
    var hostHasUvs1 = host.Uvs1.Count == hostVertices && hostVertices > 0;
    var hostHasSkin = host.Skin.Count == hostVertices;

    var place = Matrix4x4.CreateScale(scale) * Worlds(host.Bones)[locatorNode]; // attachment model space -> host model space
    Matrix4x4.Invert(place, out var unplace);
    var boneOffset = host.Bones.Count;
    var names = new HashSet<string>(host.Bones.Select(b => b.Name), StringComparer.OrdinalIgnoreCase);
    foreach (var b in attachment.Bones)
    {
      var name = prefix + "_" + b.Name;
      for (var k = 2; !names.Add(name); k++)
        name = prefix + "_" + b.Name + "_" + k;
      var root = b.Parent < 0 || b.Parent >= attachment.Bones.Count;
      host.Bones.Add(new Bone
      {
        Name = name,
        Parent = root ? locatorNode : b.Parent + boneOffset,
        Local = root ? b.Local * Matrix4x4.CreateScale(scale) : b.Local,
        // v_host * IB' * W' must equal (v * IB * W) * place with v_host = v * place and W' = W * place.
        InverseBind = unplace * b.InverseBind,
        FileBind = b.FileBind,
        Influences = b.Influences,
        AnimIndex = -1,
        SourceName = b.SourceName.Length > 0 ? b.SourceName : b.Name,
      });
    }

    // Vertices without skin are bound rigidly to the node of the part that uses them (else the attachment root).
    var rigid = new int[attachment.Positions.Count];
    Array.Fill(rigid, attachment.Bones.FindIndex(b => b.Parent < 0));
    foreach (var part in attachment.Parts)
    {
      if (part.Node < 0)
        continue;
      for (var i = part.IndexStart; i < part.IndexStart + part.IndexCount && i < attachment.Indices.Count; i++)
        rigid[attachment.Indices[i]] = part.Node;
    }

    var attachmentSkin = attachment.Skin.Count == attachment.Positions.Count;
    for (var v = 0; v < attachment.Positions.Count; v++)
    {
      host.Positions.Add(Vector3.Transform(attachment.Positions[v], place));
      if (hostHasNormals)
      {
        var n = v < attachment.Normals.Count ? Vector3.TransformNormal(attachment.Normals[v], place) : Vector3.UnitZ;
        host.Normals.Add(n.LengthSquared() > 0 ? Vector3.Normalize(n) : n);
      }

      if (hostHasUvs)
        host.Uvs.Add(v < attachment.Uvs.Count ? attachment.Uvs[v] : Vector2.Zero);
      if (hostHasUvs1)
        host.Uvs1.Add(attachment.Uvs1.Count == attachment.Positions.Count ? attachment.Uvs1[v] : Vector2.Zero);
      if (!hostHasSkin)
        continue;
      if (attachmentSkin)
      {
        var s = attachment.Skin[v];
        host.Skin.Add(new SkinWeights
        {
          J0 = (ushort)(s.J0 + boneOffset), J1 = (ushort)(s.J1 + boneOffset), J2 = (ushort)(s.J2 + boneOffset), J3 = (ushort)(s.J3 + boneOffset),
          W0 = s.W0, W1 = s.W1, W2 = s.W2, W3 = s.W3,
        });
      }
      else
      {
        host.Skin.Add(new SkinWeights { J0 = (ushort)((rigid[v] < 0 ? locatorNode - boneOffset : rigid[v]) + boneOffset), W0 = 1 });
      }
    }

    var indexOffset = host.Indices.Count;
    foreach (var i in attachment.Indices)
      host.Indices.Add(i + hostVertices);
    var merged = 0;
    foreach (var part in attachment.Parts)
    {
      var copy = part.Copy(part.IndexStart + indexOffset, part.IndexCount);
      copy.Node = part.Node >= 0 ? part.Node + boneOffset : locatorNode;
      host.Parts.Add(copy);
      if (part.IndexCount >= 3)
        merged++;
    }

    return merged;
  }
}
