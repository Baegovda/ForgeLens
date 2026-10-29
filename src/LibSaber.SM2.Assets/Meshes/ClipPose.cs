using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Writes one clip sample over skeleton bind locals. A rotation key is the bone's local
///   rotation, so it replaces the bind rotation. A translation key is the local translation.
///   Missing channels keep the bind value. When <see cref="AnimationClip.RootMotion"/> is set,
///   the 0xF4 root transform (rotation, then translation) is applied to every bone with no parent.
/// </summary>
internal static class ClipPose
{

  public static void Apply(IReadOnlyList<Bone> bones, AnimationClip clip, float time, Matrix4x4[] local)
  {
    ApplyBase(bones, clip, time, local);
    if (clip.InPlace)
      RemoveTravel(bones, clip, local);

    // Baked secondary motion (jiggle bones, cloth helper bones) replaces those locals. Baked bones are never
    // parentless, so the rebase above does not touch them.
    if (clip.Baked is { } baked)
    {
      var indices = baked.Indices(bones, b => b.Name);
      var k = 0;
      foreach (var name in baked.Local.Keys)
      {
        var bone = indices[k++];
        if (bone >= 0 && bone < local.Length && baked.TrySample(name, time, out var value))
          local[bone] = value;
      }
    }
  }

  private static void ApplyBase(IReadOnlyList<Bone> bones, AnimationClip clip, float time, Matrix4x4[] local)
  {
    ApplyRaw(bones, clip, time, local);
    if (!clip.RebaseFirstFrame)
      return;
    if (!ReferenceEquals(clip.RebaseKey, bones))
    {
      Rebase(bones, clip);
      clip.RebaseKey = bones;
    }

    ApplyRebase(bones, clip, local);
  }

  /// <summary>The bone whose travel In place removes: <c>ROOT</c>, else the first parentless bone, else -1.</summary>
  internal static int TravelBone(IReadOnlyList<Bone> bones)
  {
    for (var i = 0; i < bones.Count; i++)
      if (string.Equals(bones[i].Name, "ROOT", StringComparison.OrdinalIgnoreCase))
        return i;
    for (var i = 0; i < bones.Count; i++)
      if (bones[i].Parent < 0)
        return i;
    return -1;
  }

  /// <summary>
  ///   <see cref="AnimationClip.InPlace"/>: subtract the travel bone's horizontal (source XZ, Y-up) world offset from frame 0.
  ///   Same rule as <see cref="MeshPreview.RemoveRootMotion"/>; a parentless local is a world, so a post-translation of every
  ///   parentless local shifts the whole skeleton.
  /// </summary>
  private static void RemoveTravel(IReadOnlyList<Bone> bones, AnimationClip clip, Matrix4x4[] local)
  {
    var root = TravelBone(bones);
    if (root < 0 || root >= local.Length)
      return;
    if (!ReferenceEquals(clip.InPlaceKey, bones))
    {
      var first = new Matrix4x4[bones.Count];
      ApplyBase(bones, clip, 0f, first);
      clip.InPlaceStart = World(bones, first, root).Translation;
      clip.InPlaceKey = bones;
    }

    var delta = World(bones, local, root).Translation - clip.InPlaceStart;
    delta.Y = 0;
    if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Z) || delta.LengthSquared() < 1e-10f)
      return;
    var shift = Matrix4x4.CreateTranslation(-delta);
    for (var i = 0; i < bones.Count; i++)
      if (bones[i].Parent < 0)
        local[i] *= shift;
  }

  private static void ApplyRebase(IReadOnlyList<Bone> bones, AnimationClip clip, Matrix4x4[] local)
  {
    if (!clip.RebaseFix.IsIdentity)
      for (var i = 0; i < bones.Count; i++)
        if (bones[i].Parent < 0)
          local[i] *= clip.RebaseFix;

    if (clip.RebaseBodyFix.IsIdentity)
      return;
    var under = clip.RebaseBodyUnder;
    if (under < 0)
    {
      for (var i = 0; i < bones.Count; i++)
        if (bones[i].Parent < 0)
          local[i] *= clip.RebaseBodyFix;
      return;
    }

    // world_child' = world_child * B  =>  local_child' = local_child * W_under * B * inv(W_under). ROOT itself stays put.
    var w = World(bones, local, under);
    Matrix4x4.Invert(w, out var wi);
    var m = w * clip.RebaseBodyFix * wi;
    for (var i = 0; i < bones.Count; i++)
      if (bones[i].Parent == under)
        local[i] *= m;
  }

  /// <summary>Pivot candidates: <c>ROOT</c> (0xF4 root motion lands here) and <c>CENTRE</c> (the pelvis, which carries the level-space translation in v1 cinematic shots).</summary>
  internal static readonly string[] RebasePivots = { "ROOT", "CENTRE" };

  /// <summary>A pelvis offset at frame 0 below this (source meters) is a pose, not a level placement.</summary>
  internal const float PelvisRebaseMinimum = 2f;

  /// <summary>
  ///   Two stages, each C = T(-p0) * Yaw(-h) * T(pb) so the pivot's frame-0 world times C has its bind translation and heading:
  ///   1. ROOT, when it is off its bind at frame 0: C on the right of every parentless local (row vectors).
  ///   2. CENTRE, when after stage 1 it is still at least <see cref="PelvisRebaseMinimum"/> away (story_tower jetpack and
  ///      pve_relay boss_intro keep ROOT at the origin and put the shot position on CENTRE): C is applied to ROOT's children
  ///      in world space, so ROOT keeps its own (rebased) track instead of being thrown to minus the shot position.
  ///   The reported offset/yaw belong to the stage that moved the body (CENTRE when stage 2 ran, else ROOT).
  /// </summary>
  internal static void Rebase(IReadOnlyList<Bone> bones, AnimationClip clip)
  {
    clip.RebaseFix = Matrix4x4.Identity;
    clip.RebaseBodyFix = Matrix4x4.Identity;
    clip.RebaseBodyUnder = -1;
    clip.RebaseOffset = Vector3.Zero;
    clip.RebaseYawDegrees = 0;
    var bind = new Matrix4x4[bones.Count];
    for (var i = 0; i < bones.Count; i++)
      bind[i] = bones[i].Local;
    var root = Find(bones, "ROOT");
    var centre = Find(bones, "CENTRE");

    var local = new Matrix4x4[bones.Count];
    ApplyRaw(bones, clip, 0, local);
    if (root >= 0 && Fix(bones, local, bind, root, out var c1, out var off1, out var yaw1) is var moved1 && (moved1 >= 1e-4f || MathF.Abs(yaw1) >= 1e-4f))
    {
      clip.RebaseFix = c1;
      clip.RebaseOffset = off1;
      clip.RebaseYawDegrees = yaw1 * 180 / MathF.PI;
    }

    if (centre < 0)
      return;
    ApplyRebase(bones, clip, local);
    if (Fix(bones, local, bind, centre, out var c2, out var off2, out var yaw2) < PelvisRebaseMinimum)
      return;
    var under = -1;
    for (var p = bones[centre].Parent; p >= 0; p = bones[p].Parent)
      if (p == root)
        under = root;
    clip.RebaseBodyFix = c2;
    clip.RebaseBodyUnder = under;
    clip.RebaseOffset = off2 + clip.RebaseOffset;
    clip.RebaseYawDegrees += yaw2 * 180 / MathF.PI;
  }

  private static int Find(IReadOnlyList<Bone> bones, string name)
  {
    for (var i = 0; i < bones.Count; i++)
      if (string.Equals(bones[i].Name, name, StringComparison.OrdinalIgnoreCase))
        return i;
    return -1;
  }

  private static float Fix(IReadOnlyList<Bone> bones, Matrix4x4[] local, Matrix4x4[] bind, int pivot, out Matrix4x4 fix, out Vector3 offset, out float yaw)
  {
    var frame0 = World(bones, local, pivot);
    var rest = World(bones, bind, pivot);
    // Heading change about up: where world +Z goes under the world-space delta (rest * delta = frame0), projected on XZ.
    Matrix4x4.Invert(rest, out var restInverse);
    var delta = restInverse * frame0;
    yaw = MathF.Atan2(delta.M31, delta.M33);
    if (delta.M31 * delta.M31 + delta.M33 * delta.M33 < 0.25f)
      yaw = MathF.Atan2(-delta.M13, delta.M11); // +Z near vertical: use +X instead
    var p0 = frame0.Translation;
    var pb = rest.Translation;
    offset = p0 - pb;
    fix = Matrix4x4.CreateTranslation(-p0) * Matrix4x4.CreateRotationY(-yaw) * Matrix4x4.CreateTranslation(pb);
    return offset.Length();
  }

  private static Matrix4x4 World(IReadOnlyList<Bone> bones, Matrix4x4[] local, int index)
  {
    var world = local[index];
    for (var parent = bones[index].Parent; parent >= 0; parent = bones[parent].Parent)
      world *= local[parent];
    return world;
  }

  /// <summary>propanim: <paramref name="overlay" />'s tracks written over the current locals (no reset, no root motion).</summary>
  public static void ApplyOverlay(IReadOnlyList<Bone> bones, AnimationClip overlay, float time, Matrix4x4[] local) =>
    ApplyTracks(bones, overlay, time, local);

  private static void ApplyRaw(IReadOnlyList<Bone> bones, AnimationClip clip, float time, Matrix4x4[] local)
  {
    for (var i = 0; i < bones.Count; i++)
      local[i] = bones[i].Local;

    ApplyTracks(bones, clip, time, local);
    ApplyRoot(bones, clip, time, local);
  }

  private static void ApplyTracks(IReadOnlyList<Bone> bones, AnimationClip clip, float time, Matrix4x4[] local)
  {
    var binding = ClipBinding.Resolve(bones, clip);
    for (var t = 0; t < clip.Tracks.Count; t++)
    {
      var track = clip.Tracks[t];
      var index = binding[t];

      if ((uint)index >= (uint)local.Length)
        continue;

      var bind = local[index];
      var translation = AnimationClip.Sample(track.Translation, new Vector3(bind.M41, bind.M42, bind.M43), time);
      var scale = AnimationClip.Sample(track.Scale, Vector3.One, time);
      Quaternion rotation;
      if (track.Rotation.Count == 0)
        rotation = Quaternion.CreateFromRotationMatrix(bind);
      else
        rotation = Quaternion.Normalize(AnimationClip.Sample(track.Rotation, Quaternion.Identity, time));

      if (track.Scale.Count == 0)
        scale = Matrix4x4.Decompose(bind, out var bindScale, out _, out _) ? bindScale : Vector3.One;
      local[index] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }
  }

  private static void ApplyRoot(IReadOnlyList<Bone> bones, AnimationClip clip, float time, Matrix4x4[] local)
  {
    if (!clip.RootMotion || (clip.RootTranslation.Count == 0 && !clip.HasRootRotation))
      return;

    var root = AnimationClip.Sample(clip.RootTranslation, Vector3.Zero, time);
    var turn = clip.HasRootRotation;
    var rootMatrix = turn
      ? Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(AnimationClip.Sample(clip.RootRotation, Quaternion.Identity, time))) * Matrix4x4.CreateTranslation(root)
      : Matrix4x4.Identity;
    for (var i = 0; i < bones.Count; i++)
    {
      if (bones[i].Parent >= 0)
        continue;
      if (turn)
      {
        // p' = R_root * p + T_root (see AnimationClip.RootMotion).
        local[i] *= rootMatrix;
        continue;
      }
      if (!Matrix4x4.Decompose(local[i], out var scale, out var rotation, out var translation))
      {
        local[i].M41 += root.X;
        local[i].M42 += root.Y;
        local[i].M43 += root.Z;
        continue;
      }

      local[i] = Matrix4x4.CreateScale(scale)
        * Matrix4x4.CreateFromQuaternion(rotation)
        * Matrix4x4.CreateTranslation(translation + root);
    }
  }

}
