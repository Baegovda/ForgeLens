using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Writes one clip sample over skeleton bind locals. A rotation key is the bone's local
///   rotation, so it replaces the bind rotation. A translation key is the local translation.
///   Missing channels keep the bind value. When <see cref="AnimationClip.RootMotion"/> is set,
///   the 0xF4 root translation is added to every bone with no parent.
/// </summary>
internal static class ClipPose
{

  public static void Apply(IReadOnlyList<Bone> bones, AnimationClip clip, float time, Matrix4x4[] local)
  {
    for (var i = 0; i < bones.Count; i++)
      local[i] = bones[i].Local;

    foreach (var track in clip.Tracks)
    {
      var index = track.Bone;
      if (index < 0)
      {
        for (var i = 0; i < bones.Count; i++)
        {
          if (string.Equals(bones[i].Name, track.BoneName, StringComparison.Ordinal))
          {
            index = i;
            break;
          }
        }
      }

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

    if (!clip.RootMotion || clip.RootTranslation.Count == 0)
      return;

    var root = AnimationClip.Sample(clip.RootTranslation, Vector3.Zero, time);
    for (var i = 0; i < bones.Count; i++)
    {
      if (bones[i].Parent >= 0)
        continue;
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
