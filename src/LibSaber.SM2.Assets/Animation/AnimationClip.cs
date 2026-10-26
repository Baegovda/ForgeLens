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

  /// <summary>0xF4 root rotation. Stored so it can be checked against the codec. Export applies translation only.</summary>
  public List<QuatKey> RootRotation { get; } = new();

  /// <summary>When true, <see cref="RootTranslation"/> is added to every skeleton bone whose parent is the scene.</summary>
  public bool RootMotion { get; set; } = true;

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
