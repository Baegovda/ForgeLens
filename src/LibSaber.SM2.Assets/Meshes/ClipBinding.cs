using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Meshes;

/// <summary>
///   Maps each clip track to a skeleton bone.
///   <para>
///     The game binds a <c>.sani</c> to a template in FUN_142232230(clip, tpl), which is reached from
///     FUN_141db55f0 when <c>CONFIG.useSaniAnimationsInTpl</c> (bool 0x144f7c9d0, registered at 0x14017fbb0) is set.
///     It walks the template objects (0x40-byte records) and keeps those whose <c>animNmb</c> (short +0x3e) is not -1, as
///     (animNmb, object id +0x3c) pairs. It sorts the pairs by animNmb (FUN_14127d230), then writes clip+0x50[i] = object
///     id of the i-th pair for i below the clip's bone count (clip+0x34). No track name is compared, so the tracks whose
///     name is empty in the F1 table still bind.
///   </para>
///   <para>
///     Here that rule applies when the number of animated objects equals the track count, as it does for template
///     animations (door_mushroom 130/130, hologram_planet 47/47). Otherwise, for example a character skeleton driven by
///     clips from an animation set, a track binds by bone name.
///     A track that already carries a bone index (<see cref="BoneTrack.Bone"/> &gt;= 0) keeps it.
///   </para>
/// </summary>
public static class ClipBinding
{
  public static int[] Resolve(IReadOnlyList<Bone> bones, AnimationClip clip)
  {
    var map = new int[clip.Tracks.Count];
    var ranked = new List<int>();
    for (var i = 0; i < bones.Count; i++)
    {
      if (bones[i].AnimIndex != -1)
        ranked.Add(i);
    }

    ranked.Sort((a, b) => bones[a].AnimIndex != bones[b].AnimIndex ? bones[a].AnimIndex.CompareTo(bones[b].AnimIndex) : a.CompareTo(b));
    var byAnim = ranked.Count > 0 && ranked.Count == clip.Tracks.Count;

    for (var t = 0; t < clip.Tracks.Count; t++)
    {
      var track = clip.Tracks[t];
      var index = track.Bone;
      if (index < 0 && byAnim)
        index = ranked[t];
      if (index < 0 && track.BoneName.Length > 0)
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

      map[t] = index < bones.Count ? index : -1;
    }

    return map;
  }

  /// <summary>True when the clip binds by template <c>animNmb</c> rather than by name.</summary>
  public static bool UsesAnimIndex(IReadOnlyList<Bone> bones, AnimationClip clip)
  {
    var animated = 0;
    foreach (var bone in bones)
    {
      if (bone.AnimIndex != -1)
        animated++;
    }

    return animated > 0 && animated == clip.Tracks.Count;
  }
}
