using System.Numerics;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>
///   Expands a retail ETNA <c>.sani</c> plus its <c>.sani_data</c> into local TRS keys.
///   The <c>.sani</c> 0xF6 block lists which bones animate and stores a per-bone range.
///   <c>.sani_data</c> is one 0xF2 block whose payload is
///   <c>frames * (animatedRotations * 6 + animatedTranslations * 4)</c> bytes:
///   all translation keys, then all rotation keys, frame-major.
///   A rotation key is three int16 values lerped from the bone's min xyz to max xyz;
///   w is <c>sqrt(1 - x² - y² - z²)</c> with a positive sign. Those ranges are the first
///   28-byte-per-bone block in the 0xF6 tail that stays on the unit sphere. A translation
///   key is three signed 10-bit values in the low 30 bits, lerped through the range record
///   packed immediately before that block (when there is room) and unwrapped so a code
///   that passes 511 does not pop to -512. Bones the clip does not list keep the skeleton
///   bind pose. The 0xF4 block (6 int16 per frame, middle component always 0) is a separate
///   track whose bone and unit scale are not identified, so it is not sampled.
/// </summary>
internal static class EtnaCurves
{

  public static bool TryExpand(byte[] sani, byte[]? data, AnimationClip clip, int frames)
  {
    if (data is null || frames <= 1 || clip.Bones.Count == 0 || clip.SampleRate <= 0)
    {
      clip.Notes.Add(data is null
        ? "Paired .sani_data was not loaded, so curve keys were not sampled."
        : "The ETNA header has no frame rate or bone names, so curve keys were not sampled.");
      return false;
    }

    if (!TryPayload(data, out var payload))
    {
      clip.Notes.Add(".sani_data does not start with the 0xF2 block, so curve keys were not sampled.");
      return false;
    }

    if (!TryChannels(sani, clip.Bones.Count, out var tail, out var translations, out var rotations))
    {
      clip.Notes.Add("The ETNA 0xF6 bone table does not partition the skeleton, so curve keys were not sampled.");
      return false;
    }

    var expected = frames * (rotations.Count * 6 + translations.Count * 4);
    if (payload.Length != expected || rotations.Count == 0)
    {
      clip.Notes.Add($".sani_data is {payload.Length} bytes; {frames} frames of {rotations.Count} rotations and {translations.Count} translations would be {expected}. Keys were not sampled.");
      return false;
    }

    var transBytes = frames * translations.Count * 4;
    var rotBytes = payload.AsSpan(transBytes, frames * rotations.Count * 6);
    if (!TryRotationRanges(tail, rotBytes, frames, rotations.Count, out var rangeOffset, out var medianDegrees))
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
      + "A rotation key is three int16 components lerped through the bone's 0xF6 min/max, with w reconstructed. "
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

  private static bool TryChannels(byte[] sani, int boneCount, out byte[] tail, out List<int> translations, out List<int> rotations)
  {
    tail = Array.Empty<byte>();
    translations = new List<int>();
    rotations = new List<int>();
    if (sani.Length < 36 || sani[0] != (byte)'E')
      return false;
    var cursor = BitConverter.ToInt32(sani, 8);
    byte[]? block = null;
    while (cursor + 6 <= sani.Length)
    {
      var id = BitConverter.ToUInt16(sani, cursor);
      var end = BitConverter.ToInt32(sani, cursor + 2);
      if (end <= cursor || end > sani.Length)
        break;
      if (id == 0xF6)
        block = sani.AsSpan(cursor + 6, end - (cursor + 6)).ToArray();
      cursor = end;
    }

    if (block is null || block.Length < 322)
      return false;

    var count0 = BitConverter.ToUInt16(block, 2);
    var count1 = BitConverter.ToUInt16(block, 6);
    var count2 = BitConverter.ToUInt16(block, 14);
    var count3 = BitConverter.ToUInt16(block, 18);
    if (2 + 2 + (count0 + count1 + count2 + count3) * 2 > 322)
      return false;
    if (count0 + count2 != boneCount || count1 + count3 != boneCount)
      return false;

    var index = 30;
    var trans = ReadList(block, ref index, count0);
    var rot = ReadList(block, ref index, count1);
    var transRest = ReadList(block, ref index, count2);
    var rotRest = ReadList(block, ref index, count3);
    if (index != 322)
      return false;
    if (!Partitions(trans, transRest, boneCount) || !Partitions(rot, rotRest, boneCount))
      return false;

    translations = trans;
    rotations = rot;
    tail = block.AsSpan(322).ToArray();
    return tail.Length >= rot.Count * 28;
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

  private static bool TryRotationRanges(byte[] tail, ReadOnlySpan<byte> rotations, int frames, int bones, out int offset, out float medianDegrees)
  {
    offset = 0;
    medianDegrees = 180;
    var record = bones * 28;
    // The range table is packed at the front of the tail. A later 4-byte shift can
    // look smoother because it pairs keys with a tiny interval and squashes the pose
    // toward identity, so the first block that stays on the unit sphere wins.
    for (var start = 0; start + record <= tail.Length; start += 4)
    {
      if (!RangesLookReal(tail, start, bones))
        continue;
      var (maxSquared, median) = ScoreRotations(tail, start, rotations, frames, bones);
      if (maxSquared > 1.25f || median > 8f)
        continue;
      offset = start;
      medianDegrees = median;
      return true;
    }

    return false;
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
