using System.Numerics;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Writes one clip sample over skeleton bind locals. A rotation key is an offset from
///   identity, so it is applied on top of the bind rotation (<c>bind * clip</c>). An
///   identity bind leaves the key unchanged. Missing channels keep the bind value.
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
      {
        var bindRotation = Matrix4x4.Decompose(bind, out _, out var decomposed, out _)
          ? decomposed
          : Quaternion.CreateFromRotationMatrix(bind);
        rotation = Quaternion.Normalize(bindRotation * AnimationClip.Sample(track.Rotation, Quaternion.Identity, time));
      }

      if (track.Scale.Count == 0)
        scale = Matrix4x4.Decompose(bind, out var bindScale, out _, out _) ? bindScale : Vector3.One;
      local[index] = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
    }
  }

}
