using System.Numerics;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>
///   Expands a retail ETNA <c>.sani</c> plus its <c>.sani_data</c> into local TRS keys.
///   The channel block (0xF6, or 0xF2 when the clip has no external payload) lists which
///   bones animate and stores a per-bone range. Its index table is
///   <c>30 + declaredBones * 4</c> bytes, so a 73-bone clip ends that table at byte 322
///   and a 282-bone intercessor clip ends it at byte 1158. <c>.sani_data</c> is one 0xF2
///   block whose payload is <c>frames * (animatedRotations * 6 + animatedTranslations * 4)</c>
///   bytes: all translation keys, then all rotation keys, frame-major.
///   A rotation key is three int16 values lerped from the bone's min xyz to max xyz;
///   w is <c>sqrt(1 - x² - y² - z²)</c> with a positive sign. Those ranges are the first
///   28-byte-per-bone block in the channel tail that stays on the unit sphere. A block
///   that also swings <c>CENTRE</c> more than 100° is skipped when a later block stays
///   under that; the thunder-hammer clip's first block folds the legs, and the next one
///   is the arm swing. A translation
///   key is three signed 10-bit values in the low 30 bits, lerped through the range record
///   packed immediately before that block (when there is room) and unwrapped so a code
///   that passes 511 does not pop to -512. Bones the clip does not list keep the skeleton
///   bind pose. The 0xF4 block is a 50-byte header plus 6 int16 samples per frame. Its bone
///   and unit scale are not identified (on the unarmed run one channel is a 4096-step saw,
///   and the idle track is entirely zero), so it is not sampled. A clip whose curves live
///   only inside the 0xF2 block, with no <c>.sani_data</c> of that grid size, is left unsampled.
/// </summary>
internal static class EtnaCurves
{

  public static bool TryExpand(byte[] sani, byte[]? data, AnimationClip clip, int frames)
  {
    if (frames <= 1 || clip.Bones.Count == 0 || clip.SampleRate <= 0)
    {
      clip.Notes.Add("The ETNA header has no frame rate or bone names, so curve keys were not sampled.");
      return false;
    }

    if (!TryChannels(sani, clip.Bones.Count, out var tail, out var translations, out var rotations, out var channel))
    {
      if (rotations.Count == 0)
        clip.Notes.Add(channel);
      else
      {
        var grid = frames * (rotations.Count * 6 + translations.Count * 4);
        clip.Notes.Add(
          $"The {channel} block lists {rotations.Count} rotation and {translations.Count} translation bones, but the index lists do not partition the declared bones. "
          + (data is null
            ? $"No .sani_data key grid was loaded. The {tail.Length} bytes after the bone table are not a frame-major grid of {frames} frames ({grid} bytes), so curves were not sampled."
            : "Curve keys were not sampled."));
      }

      return false;
    }

    var expected = frames * (rotations.Count * 6 + translations.Count * 4);
    if (data is null || !TryPayload(data, out var payload))
    {
      clip.Notes.Add(
        $"No .sani_data key grid was loaded. The {channel} block lists {rotations.Count} rotation and {translations.Count} translation bones and then {tail.Length} bytes. "
        + $"A frame-major grid of {frames} frames would be {expected} bytes (translation keys, then rotation keys) and these bytes are not that grid, so curves were not sampled.");
      return false;
    }

    if (payload.Length != expected || rotations.Count == 0)
    {
      clip.Notes.Add($".sani_data is {payload.Length} bytes; {frames} frames of {rotations.Count} rotations and {translations.Count} translations would be {expected}. Keys were not sampled.");
      return false;
    }

    var transBytes = frames * translations.Count * 4;
    var rotBytes = payload.AsSpan(transBytes, frames * rotations.Count * 6);
    if (!TryRotationRanges(tail, rotBytes, frames, rotations, clip.Bones, out var rangeOffset, out var medianDegrees))
    {
      clip.Notes.Add("Rotation ranges in the 0xF6 block did not produce unit quaternions, so keys were not sampled.");
      return false;
    }

    var rate = clip.SampleRate;
    for (var bone = 0; bone < rotations.Count; bone++)
    {
      var track = new BoneTrack { Bone = -1, BoneName = clip.Bones[rotations[bone]] };
      var min = Read3(tail, rangeOffset + bone * 28);
      var max = Read3(tail, rangeOffset + bone * 28 + 12);
      var previous = Quaternion.Identity;
      var started = false;
      for (var frame = 0; frame < frames; frame++)
      {
        var sample = frame * rotations.Count + bone;
        var x = Dequant(ReadI16(rotBytes, sample * 6), min.X, max.X);
        var y = Dequant(ReadI16(rotBytes, sample * 6 + 2), min.Y, max.Y);
        var z = Dequant(ReadI16(rotBytes, sample * 6 + 4), min.Z, max.Z);
        var rotation = QuantizedQuat(x, y, z);
        if (started && Quaternion.Dot(previous, rotation) < 0)
          rotation = new Quaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);
        previous = rotation;
        started = true;
        track.Rotation.Add(new QuatKey { Time = frame / rate, Value = rotation });
      }

      clip.Tracks.Add(track);
    }

    var translated = 0;
    var transStart = rangeOffset - translations.Count * 28;
    if (translations.Count > 0 && transStart >= 0)
    {
      var transKeys = payload.AsSpan(0, transBytes);
      for (var bone = 0; bone < translations.Count; bone++)
      {
        if (!TryTranslation(tail, transStart + bone * 28, transKeys, frames, translations.Count, bone, out var samples))
          continue;
        var track = new BoneTrack { Bone = -1, BoneName = clip.Bones[translations[bone]] };
        var existing = clip.Tracks.Find(item => item.BoneName == track.BoneName);
        var target = existing ?? track;
        for (var frame = 0; frame < frames; frame++)
          target.Translation.Add(new VectorKey { Time = frame / rate, Value = samples[frame] });
        if (existing is null)
          clip.Tracks.Add(target);
        translated++;
      }
    }

    clip.Notes.Add(
      $"Sampled {rotations.Count} rotation tracks (median step {medianDegrees:0.###}°) and {translated} of {translations.Count} translation tracks. "
      + "A rotation key is three int16 components lerped through the bone's channel-block min/max, with w reconstructed. "
      + "A translation key is three 10-bit components in the same ranges, unwrapped across frames. "
      + "Other bones stay on the skeleton bind pose. The 0xF4 int16 track is not a confirmed bone, so it was left out.");
    return true;
  }

  private static bool TryPayload(byte[] data, out byte[] payload)
  {
    payload = Array.Empty<byte>();
    if (data.Length < 6 || BitConverter.ToUInt16(data, 0) != 0xF2)
      return false;
    var end = BitConverter.ToInt32(data, 2);
    if (end != data.Length || data.Length < 6)
      return false;
    payload = data.AsSpan(6).ToArray();
    return true;
  }

  private static bool TryChannels(byte[] sani, int nameCount, out byte[] tail, out List<int> translations, out List<int> rotations, out string channel)
  {
    tail = Array.Empty<byte>();
    translations = new List<int>();
    rotations = new List<int>();
    channel = "0xF6";
    if (sani.Length < 36 || sani[0] != (byte)'E')
    {
      channel = "The ETNA channel block is missing, so curve keys were not sampled.";
      return false;
    }

    var cursor = BitConverter.ToInt32(sani, 8);
    byte[]? block = null;
    ushort blockId = 0;
    while (cursor + 6 <= sani.Length)
    {
      var id = BitConverter.ToUInt16(sani, cursor);
      var end = BitConverter.ToInt32(sani, cursor + 2);
      if (end <= cursor || end > sani.Length)
        break;
      // 0xF6 is the channel table when the key grid lives in .sani_data.
      // A clip with no sibling stores that same table at the start of 0xF2.
      if (id == 0xF6 || (id == 0xF2 && blockId != 0xF6))
      {
        block = sani.AsSpan(cursor + 6, end - (cursor + 6)).ToArray();
        blockId = id;
      }

      cursor = end;
    }

    if (block is null || block.Length < 30 || BitConverter.ToUInt16(block, 0) != 2)
    {
      channel = "The ETNA channel block does not start with a version-2 bone table, so curve keys were not sampled.";
      return false;
    }

    var count0 = BitConverter.ToUInt16(block, 2);
    var count1 = BitConverter.ToUInt16(block, 6);
    var count2 = BitConverter.ToUInt16(block, 14);
    var count3 = BitConverter.ToUInt16(block, 18);
    var boneCount = count0 + count2;
    var table = 30 + boneCount * 4;
    channel = "0x" + blockId.ToString("X2");
    if (boneCount == 0 || count1 + count3 != boneCount || boneCount > nameCount || block.Length < table)
    {
      channel = $"The {channel} bone table does not partition the skeleton, so curve keys were not sampled.";
      return false;
    }

    var index = 30;
    var trans = ReadList(block, ref index, count0);
    var rot = ReadList(block, ref index, count1);
    var transRest = ReadList(block, ref index, count2);
    var rotRest = ReadList(block, ref index, count3);
    if (index != table)
    {
      channel = $"The {channel} bone table does not partition the skeleton, so curve keys were not sampled.";
      return false;
    }

    if (rot.Count == 0 || block.Length - table < rot.Count * 28)
    {
      channel = $"The {channel} bone table has no rotation ranges, so curve keys were not sampled.";
      return false;
    }

    translations = trans;
    rotations = rot;
    tail = block.AsSpan(table).ToArray();
    return Partitions(trans, transRest, boneCount) && Partitions(rot, rotRest, boneCount);
  }

  private static List<int> ReadList(byte[] block, ref int index, int count)
  {
    var list = new List<int>(count);
    for (var i = 0; i < count; i++)
    {
      list.Add(BitConverter.ToUInt16(block, index));
      index += 2;
    }

    return list;
  }

  private static bool Partitions(List<int> animated, List<int> constant, int boneCount)
  {
    var seen = new bool[boneCount];
    foreach (var list in new[] { animated, constant })
    {
      foreach (var bone in list)
      {
        if ((uint)bone >= (uint)boneCount || seen[bone])
          return false;
        seen[bone] = true;
      }
    }

    return true;
  }

  private static bool TryRotationRanges(
    byte[] tail,
    ReadOnlySpan<byte> rotations,
    int frames,
    List<int> boneIds,
    List<string> names,
    out int offset,
    out float medianDegrees)
  {
    offset = 0;
    medianDegrees = 180;
    var bones = boneIds.Count;
    var record = bones * 28;
    var centre = boneIds.FindIndex(bone => (uint)bone < (uint)names.Count && names[bone] == "CENTRE");
    // The range table is packed at the front of the tail. A later 4-byte shift can
    // look smoother because it pairs keys with a tiny interval and squashes the pose
    // toward identity, so the first block that stays on the unit sphere wins.
    // One marine clip has an earlier block that is numerically valid and swings
    // CENTRE by about 135°, which plants the feet above the head. The following
    // block keeps CENTRE small and is the one applied. Hormagaunt and the idle
    // clip are already under 100° on the first block.
    int? folded = null;
    float foldedMedian = 180;
    for (var start = 0; start + record <= tail.Length; start += 4)
    {
      if (!RangesLookReal(tail, start, bones))
        continue;
      var (maxSquared, median) = ScoreRotations(tail, start, rotations, frames, bones);
      if (maxSquared > 1.25f || median > 8f)
        continue;
      if (centre >= 0 && CentreArc(tail, start, rotations, frames, bones, centre) > 100f)
      {
        if (folded is null)
        {
          folded = start;
          foldedMedian = median;
        }

        continue;
      }

      offset = start;
      medianDegrees = median;
      return true;
    }

    if (folded is int fallback)
    {
      offset = fallback;
      medianDegrees = foldedMedian;
      return true;
    }

    return false;
  }

  private static float CentreArc(byte[] tail, int start, ReadOnlySpan<byte> rotations, int frames, int bones, int bone)
  {
    var min = Read3(tail, start + bone * 28);
    var max = Read3(tail, start + bone * 28 + 12);
    Quaternion? origin = null;
    var arc = 0f;
    for (var frame = 0; frame < frames; frame++)
    {
      var sample = frame * bones + bone;
      var x = Dequant(ReadI16(rotations, sample * 6), min.X, max.X);
      var y = Dequant(ReadI16(rotations, sample * 6 + 2), min.Y, max.Y);
      var z = Dequant(ReadI16(rotations, sample * 6 + 4), min.Z, max.Z);
      var rotation = QuantizedQuat(x, y, z);
      if (origin is not Quaternion first)
      {
        origin = rotation;
        continue;
      }

      var dot = MathF.Abs(Quaternion.Dot(first, rotation));
      arc = MathF.Max(arc, MathF.Acos(Math.Clamp(dot, -1f, 1f)) * 2f * (180f / MathF.PI));
    }

    return arc;
  }

  private static bool RangesLookReal(byte[] tail, int start, int bones)
  {
    for (var bone = 0; bone < bones; bone++)
    {
      for (var component = 0; component < 6; component++)
      {
        var value = BitConverter.ToSingle(tail, start + bone * 28 + component * 4);
        if (!float.IsFinite(value) || MathF.Abs(value) > 20f)
          return false;
      }
    }

    return true;
  }

  private static (float MaxSquared, float Median) ScoreRotations(byte[] tail, int start, ReadOnlySpan<byte> rotations, int frames, int bones)
  {
    var angles = new List<float>();
    var maxSquared = 0f;
    for (var bone = 0; bone < bones; bone++)
    {
      var min = Read3(tail, start + bone * 28);
      var max = Read3(tail, start + bone * 28 + 12);
      Quaternion? previous = null;
      for (var frame = 0; frame < frames; frame++)
      {
        var sample = frame * bones + bone;
        var x = Dequant(ReadI16(rotations, sample * 6), min.X, max.X);
        var y = Dequant(ReadI16(rotations, sample * 6 + 2), min.Y, max.Y);
        var z = Dequant(ReadI16(rotations, sample * 6 + 4), min.Z, max.Z);
        var squared = x * x + y * y + z * z;
        if (squared > maxSquared)
          maxSquared = squared;
        if (squared > 1.25f)
          return (squared, 180f);

        var rotation = QuantizedQuat(x, y, z);
        if (previous is Quaternion prior)
        {
          var dot = MathF.Abs(Quaternion.Dot(prior, rotation));
          dot = Math.Clamp(dot, -1f, 1f);
          angles.Add(MathF.Acos(dot) * 2f * (180f / MathF.PI));
        }

        previous = rotation;
      }
    }

    angles.Sort();
    var median = angles.Count == 0 ? 180f : angles[angles.Count / 2];
    return (maxSquared, median);
  }

  private static bool TryTranslation(byte[] tail, int range, ReadOnlySpan<byte> keys, int frames, int stride, int bone, out Vector3[] samples)
  {
    samples = new Vector3[frames];
    var min = Read3(tail, range);
    var max = Read3(tail, range + 12);
    if (!Finite(min) || !Finite(max))
      return false;

    var previous = new int[3];
    var started = false;
    var worst = 0f;
    for (var frame = 0; frame < frames; frame++)
    {
      var word = BitConverter.ToUInt32(keys.Slice((frame * stride + bone) * 4, 4));
      var codes = new int[3];
      for (var component = 0; component < 3; component++)
      {
        var code = (int)((word >> (component * 10)) & 1023);
        if (code >= 512)
          code -= 1024;
        if (started)
        {
          while (code - previous[component] > 512)
            code -= 1024;
          while (previous[component] - code > 512)
            code += 1024;
        }

        previous[component] = code;
        codes[component] = code;
      }

      started = true;
      var value = new Vector3(
        Dequant10(codes[0], min.X, max.X),
        Dequant10(codes[1], min.Y, max.Y),
        Dequant10(codes[2], min.Z, max.Z));
      if (frame > 0)
        worst = MathF.Max(worst, Vector3.Distance(samples[frame - 1], value));
      samples[frame] = value;
    }

    return worst < 0.5f && samples.All(sample => float.IsFinite(sample.X) && sample.Length() < 8f);
  }

  private static bool Finite(Vector3 value)
    => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z)
      && MathF.Abs(value.X) <= 20f && MathF.Abs(value.Y) <= 20f && MathF.Abs(value.Z) <= 20f;

  private static Vector3 Read3(byte[] bytes, int offset)
    => new(BitConverter.ToSingle(bytes, offset), BitConverter.ToSingle(bytes, offset + 4), BitConverter.ToSingle(bytes, offset + 8));

  private static short ReadI16(ReadOnlySpan<byte> bytes, int offset)
    => BitConverter.ToInt16(bytes.Slice(offset, 2));

  private static float Dequant(short sample, float min, float max)
  {
    var t = sample / 32767f * 0.5f + 0.5f;
    return min + t * (max - min);
  }

  private static float Dequant10(int code, float min, float max)
  {
    var t = code / 511f * 0.5f + 0.5f;
    return min + t * (max - min);
  }

  private static Quaternion QuantizedQuat(float x, float y, float z)
  {
    var squared = x * x + y * y + z * z;
    if (squared > 1f)
    {
      var scale = 1f / MathF.Sqrt(squared);
      return new Quaternion(x * scale, y * scale, z * scale, 0);
    }

    return new Quaternion(x, y, z, MathF.Sqrt(MathF.Max(0, 1f - squared)));
  }

}
