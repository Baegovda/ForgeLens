using System.Numerics;

namespace LibSaber.SM2.Assets.Animation;

public readonly struct VectorKey
{
  public float Time { get; init; }
  public Vector3 Value { get; init; }
}

public readonly struct QuatKey
{
  public float Time { get; init; }
  public Quaternion Value { get; init; }
}

public sealed class BoneTrack
{
  public int Bone { get; set; } = -1;
  public string BoneName { get; set; } = "";
  public List<VectorKey> Translation { get; } = new();
  public List<QuatKey> Rotation { get; } = new();
  public List<VectorKey> Scale { get; } = new();
}

public sealed class AnimationClip
{
  public string Name { get; set; } = "clip";
  public float Duration { get; set; }
  public List<BoneTrack> Tracks { get; } = new();
  public List<string> Notes { get; } = new();
  public List<string> Sequences { get; } = new();
  /// <summary>Morpheme event tracks (ETNA block 245), see <see cref="AnimEvents"/>.</summary>
  public List<AnimEventTrack> EventTracks { get; } = new();
  public List<string> Bones { get; } = new();
  public float SampleRate { get; set; }

  /// <summary>Scalar (float) channel names from the F1 header, after the bone names. Not bones.</summary>
  public List<string> Scalars { get; } = new();

  /// <summary>
  ///   True when the clip carries a second codec slot (0xF3 inline / 0xF7 external, clip+0x68).
  ///   The stats dumper FUN_141060280 prints clip+0x68 != 0 as the "HasAdditive" column
  ///   (string 0x1435ef808). The slot holds the additive delta of slot 0 against its first frame.
  /// </summary>
  public bool HasAdditive { get; set; }

  /// <summary>0xF4 root translation in source meters, Y-up. Empty when the clip has no root track.</summary>
  public List<VectorKey> RootTranslation { get; } = new();

  /// <summary>0xF4 root rotation. Applied with <see cref="RootTranslation"/>, see <see cref="RootMotion"/>.</summary>
  public List<QuatKey> RootRotation { get; } = new();

  /// <summary>True when any <see cref="RootRotation"/> key differs from identity (vector part above 1e-5, about 0.001 degrees).</summary>
  public bool HasRootRotation => RootRotation.Exists(key => key.Value.X * key.Value.X + key.Value.Y * key.Value.Y + key.Value.Z * key.Value.Z > 1e-10f);

  /// <summary>
  ///   When true, the 0xF4 root transform is applied to every skeleton bone whose parent is the scene:
  ///   local * R_root * T_root (System.Numerics row vectors: rotate about the model origin, then translate).
  ///   The loader FUN_14222fe40 sums the root track the same way. It samples frame 0 and the last frame
  ///   (vtbl +8), inverts R0 (FUN_1427aa8a0, conjugate / |q|^2), and stores R0^-1 * (T_last - T0) at root+8
  ///   (FUN_1427ace20 rotates by the inverse) and R0^-1 * R_last at root+0x14 (FUN_1427ad8b0, product).
  ///   So T is a parent-space offset that R does not rotate, i.e. p' = R p + T.
  ///   Rotation is applied only when <see cref="HasRootRotation"/>, which keeps identity-root clips byte-identical.
  /// </summary>
  public bool RootMotion { get; set; } = true;

  /// <summary>
  ///   Cinematic rebase (<c>--anim-rebase first-frame</c>). Cinematic shots are authored in level space
  ///   (story_tower jetpack: about (-4706, 420, -1662) m), while gameplay clips start at the entity origin.
  ///   The loader FUN_14222fe40 already treats root motion as relative to frame 0 (it stores
  ///   R0^-1 * (T_last - T0) and R0^-1 * R_last), so moving the first frame back to the bind position and heading
  ///   matches how the game consumes a clip. Only the pivot (the <c>ROOT</c> bone, else the first parentless bone)
  ///   is measured: its frame-0 world translation goes back to bind and its yaw about up (source Y) is removed.
  ///   Pitch and roll stay. The same fix is applied to every parentless bone for every frame, so the motion
  ///   inside the shot is unchanged. A no-op for clips that already start at the bind (locomotion, idles).
  /// </summary>
  public bool RebaseFirstFrame { get; set; }

  /// <summary>Set by the exporter when <see cref="RebaseFirstFrame"/> moved the clip: source meters and degrees removed.</summary>
  public Vector3 RebaseOffset { get; internal set; }
  public float RebaseYawDegrees { get; internal set; }
  internal object? RebaseKey;
  internal Matrix4x4 RebaseFix = Matrix4x4.Identity;
  internal Matrix4x4 RebaseBodyFix = Matrix4x4.Identity;
  internal int RebaseBodyUnder = -1;

  /// <summary>
  ///   Simulated bone locals baked by <see cref="LibSaber.SM2.Assets.Physics.PhysicsBake"/> (jiggle bones, cloth helper bones).
  ///   <see cref="LibSaber.SM2.Assets.Meshes.ClipPose"/> writes them over the clip pose, so FBX and glTF carry the game's
  ///   secondary motion without a runtime solver. Null for an unbaked clip.
  /// </summary>
  public BakedBones? Baked { get; set; }

  public bool HasCurves => Tracks.Exists(track => track.Translation.Count + track.Rotation.Count + track.Scale.Count > 0);

  public static Vector3 Sample(IReadOnlyList<VectorKey> keys, Vector3 fallback, float time)
  {
    if (keys.Count == 0)
      return fallback;
    if (keys.Count == 1 || time <= keys[0].Time)
      return keys[0].Value;

    for (var i = 1; i < keys.Count; i++)
    {
      if (time > keys[i].Time)
        continue;
      var span = keys[i].Time - keys[i - 1].Time;
      var amount = span <= 0 ? 0 : (time - keys[i - 1].Time) / span;
      return Vector3.Lerp(keys[i - 1].Value, keys[i].Value, amount);
    }

    return keys[^1].Value;
  }

  public static Quaternion Sample(IReadOnlyList<QuatKey> keys, Quaternion fallback, float time)
  {
    if (keys.Count == 0)
      return fallback;
    if (keys.Count == 1 || time <= keys[0].Time)
      return keys[0].Value;

    for (var i = 1; i < keys.Count; i++)
    {
      if (time > keys[i].Time)
        continue;
      var span = keys[i].Time - keys[i - 1].Time;
      var amount = span <= 0 ? 0 : (time - keys[i - 1].Time) / span;
      return Quaternion.Slerp(keys[i - 1].Value, keys[i].Value, amount);
    }

    return keys[^1].Value;
  }

}

/// <summary>
///   Per-bone local transforms sampled on a fixed grid (<see cref="Times"/>), keyed by bone name, so they survive the
///   writers' skeleton copies and reordering (FbxWriter's VisibleGeometry / UnrealRoot.Promote).
/// </summary>
public sealed class BakedBones
{
  public required float[] Times { get; init; }
  public Dictionary<string, Matrix4x4[]> Local { get; } = new(StringComparer.Ordinal);
  private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, int[]> _indices = new();

  /// <summary>Skeleton index of every <see cref="Local"/> entry (same order as <see cref="Local"/>.Keys), -1 when absent.</summary>
  public int[] Indices<TBone>(IReadOnlyList<TBone> bones, Func<TBone, string> name) where TBone : class
    => _indices.GetValue(bones, _ =>
    {
      var map = new Dictionary<string, int>(StringComparer.Ordinal);
      for (var i = 0; i < bones.Count; i++)
        map.TryAdd(name(bones[i]), i);
      return Local.Keys.Select(k => map.TryGetValue(k, out var i) ? i : -1).ToArray();
    });

  public bool TrySample(string bone, float time, out Matrix4x4 local)
  {
    local = default;
    if (!Local.TryGetValue(bone, out var frames) || frames.Length == 0 || Times.Length != frames.Length)
      return false;
    if (time <= Times[0] || frames.Length == 1)
    {
      local = frames[0];
      return true;
    }

    var hi = Array.BinarySearch(Times, time);
    if (hi >= 0)
    {
      local = frames[hi];
      return true;
    }

    hi = ~hi;
    if (hi >= frames.Length)
    {
      local = frames[^1];
      return true;
    }

    var lo = hi - 1;
    var span = Times[hi] - Times[lo];
    var t = span <= 0 ? 0 : (time - Times[lo]) / span;
    Matrix4x4.Decompose(frames[lo], out var s0, out var r0, out var p0);
    Matrix4x4.Decompose(frames[hi], out var s1, out var r1, out var p1);
    local = Matrix4x4.CreateScale(Vector3.Lerp(s0, s1, t)) * Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(r0, r1, t)) * Matrix4x4.CreateTranslation(Vector3.Lerp(p0, p1, t));
    return true;
  }
}
