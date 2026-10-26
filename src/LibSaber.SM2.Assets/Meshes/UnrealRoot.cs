using System.Numerics;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Unreal takes root motion from skeleton bone 0 only. A SM2 tpl hierarchy is
///   <c>ObjectNNNN</c> (tpl root object, identity MatrixLT) → <c>ROOT</c> → <c>CENTRE</c> …, and the FBX writer used to put
///   a <c>{mesh}_Skeleton</c> Null above it, so Interchange made that Null bone 0 and the 0xF4 root motion
///   (applied by <see cref="ClipPose"/> to parentless bones) landed on bone 2 (ObjectNNNN), where UE ignores it.
///   In the game the 0xF4 track is not a bone track at all: the loader FUN_14222fe40 stores it apart from the
///   bone slots (slot 0 at clip+0x60, slot 1 at clip+0x68, root at clip+0x70; sampler FUN_14222f1b0 / codec
///   FUN_142234f10), i.e. it moves the entity. <see cref="Promote"/> makes <c>ROOT</c> the single hierarchy root
///   (it then carries that entity motion) and hangs the old tpl root object under it with the same world bind.
/// </summary>
public static class UnrealRoot
{
  public const string RootName = "ROOT";

  /// <summary>Index of the bone that becomes the hierarchy root, or -1 when the hierarchy does not have the SM2 shape.</summary>
  public static int FindPromotable(IReadOnlyList<Bone> bones, out int oldRoot)
  {
    oldRoot = -1;
    for (var i = 0; i < bones.Count; i++)
    {
      if (bones[i].Parent >= 0)
        continue;
      if (oldRoot >= 0)
        return -1; // more than one root: leave it alone
      oldRoot = i;
    }

    if (oldRoot < 0 || string.Equals(bones[oldRoot].Name, RootName, StringComparison.Ordinal))
      return -1;
    for (var i = 0; i < bones.Count; i++)
    {
      if (bones[i].Parent == oldRoot && string.Equals(bones[i].Name, RootName, StringComparison.Ordinal))
        return i;
    }

    return -1;
  }

  /// <summary>
  ///   Returns a copy of <paramref name="mesh"/> whose <c>ROOT</c> bone is the only parentless bone. World binds,
  ///   bone order and skin indices are unchanged. Returns the same instance when the shape does not match.
  /// </summary>
  public static ExportedMesh Promote(ExportedMesh mesh)
  {
    var root = FindPromotable(mesh.Bones, out var old);
    if (root < 0)
      return mesh;

    var copy = mesh.ShallowCopy();
    copy.Bones.Clear();
    foreach (var bone in mesh.Bones)
    {
      copy.Bones.Add(new Bone
      {
        Name = bone.Name,
        Parent = bone.Parent,
        Local = bone.Local,
        InverseBind = bone.InverseBind,
        FileBind = bone.FileBind,
        Influences = bone.Influences,
        AnimIndex = bone.AnimIndex,
      });
    }

    // Source convention is row vectors: world = local * parentWorld.
    var oldWorld = mesh.Bones[old].Local;
    var rootWorld = mesh.Bones[root].Local * oldWorld;
    copy.Bones[root].Parent = -1;
    copy.Bones[root].Local = rootWorld;
    copy.Bones[old].Parent = root;
    copy.Bones[old].Local = Matrix4x4.Invert(rootWorld, out var inverse) ? oldWorld * inverse : Matrix4x4.Identity;
    return copy;
  }
}
