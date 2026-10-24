using System.Numerics;
using System.Text;
using LibSaber.IO;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Textures;
using LibSaber.SpaceMarine2.Serialization;
using LibSaber.SpaceMarine2.Structures;

namespace LibSaber.SM2.Assets.Animation;

public static class SaniReader
{

  public static AnimationClip Load(GameCatalog catalog, ArchiveStore store, int fileIndex)
  {
    var file = catalog.GetFile(fileIndex);
    var clip = new AnimationClip { Name = AssetClassification.BaseName(file.Name) };
    if (file.Extension is ".sani_data")
    {
      clip.Notes.Add(".sani_data is the external animation payload. Open the paired .sani or .sani.resource. Retail ETNA curve blocks are not expanded into keys.");
      return clip;
    }

    var bytes = store.Read(fileIndex);
    if (TextureLoader.LooksText(bytes))
    {
      if (TryFollowResource(catalog, store, fileIndex, bytes, clip))
        return clip;
      clip.Notes.Add("This animation file is text (often .sani.resource). It is shown as text. Curve export needs the binary .sani.");
      clip.Notes.Add(Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 2000)));
      return clip;
    }

    if (TryReadEtna(bytes, clip, siblingBytes: null))
      return clip;

    try
    {
      using var stream = new MemoryStream(bytes, writable: false);
      var reader = new NativeReader(stream, Endianness.LittleEndian);
      var track = Serializer<animTRACK>.Deserialize(reader);
      if (track.seqList is { Count: > 0 })
      {
        foreach (var sequence in track.seqList)
        {
          var duration = sequence.timeSec > 0 ? sequence.timeSec : 0;
          clip.Sequences.Add($"{sequence.name}  frames {sequence.startFrame:0}-{sequence.endFrame:0}  {duration:0.###}s");
          clip.Duration = Math.Max(clip.Duration, duration);
        }
      }

      if (track.objAnimList is { Count: > 0 })
      {
        for (var i = 0; i < track.objAnimList.Count; i++)
        {
          var bone = track.objMapList is not null && i < track.objMapList.Count ? track.objMapList[i] : i;
          var anim = track.objAnimList[i];
          var boneTrack = new BoneTrack { Bone = bone, BoneName = "bone" + bone };
          var duration = clip.Duration > 0 ? clip.Duration : 1f;
          AddVectors(boneTrack.Translation, anim.pTranslation, anim.iniTranslation, duration, clip);
          AddQuats(boneTrack.Rotation, anim.pRotation, anim.iniRotation, duration, clip);
          AddVectors(boneTrack.Scale, anim.pScale, anim.iniScale, duration, clip);
          if (boneTrack.Translation.Count + boneTrack.Rotation.Count + boneTrack.Scale.Count > 0)
            clip.Tracks.Add(boneTrack);
        }
      }

      if (clip.Sequences.Count == 0 && clip.Tracks.Count == 0)
        clip.Notes.Add("animTRACK parsed, but it had no sequences or curves.");
      else
        clip.Notes.Add("Spline keys are copied with a best-effort layout (xyz or txyz). Retail .sani packing is unverified, and the library's spline accessors are still stubs.");
    }
    catch (Exception ex)
    {
      clip.Notes.Add("This .sani did not parse as animTRACK (" + ex.Message + "). Retail animation files are unverified. The raw bytes can still be exported.");
    }

    return clip;
  }

  private static bool TryFollowResource(GameCatalog catalog, ArchiveStore store, int fileIndex, byte[] bytes, AnimationClip clip)
  {
    var text = Encoding.UTF8.GetString(bytes);
    if (text.IndexOf("res_desc_animation", StringComparison.OrdinalIgnoreCase) < 0)
      return false;

    var animation = QuotedEnding(text, ".sani");
    if (animation is null)
      return false;

    var index = catalog.ResolveReference(animation, fileIndex);
    if (index < 0)
    {
      clip.Notes.Add("The animation resource names " + animation + ", which is not in the catalog.");
      return true;
    }

    var dataName = QuotedEnding(text, ".sani_data");
    byte[]? data = null;
    if (dataName is not null)
    {
      var dataIndex = catalog.ResolveReference(dataName, fileIndex);
      if (dataIndex >= 0)
        data = store.Read(dataIndex);
    }

    if (!TryReadEtna(store.Read(index), clip, data))
      clip.Notes.Add(animation + " is not an ETNA clip and did not yield curves.");
    return true;
  }

  private static string? QuotedEnding(string text, string suffix)
  {
    for (var i = 0; i < text.Length; i++)
    {
      if (text[i] != '"' && text[i] != '\'')
        continue;
      var end = text.IndexOf(text[i], i + 1);
      if (end < 0)
        return null;
      var token = text[(i + 1)..end];
      i = end;
      if (token.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
        && !token.EndsWith(suffix + "_data", StringComparison.OrdinalIgnoreCase)
        && !token.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
        return token;
    }

    return null;
  }

  /// <summary>
  ///   Retail .sani files start with ETNA. The header carries duration, rate, frame count,
  ///   and the bone name table. Curve payloads stay packed; this does not invent keys.
  /// </summary>
  private static bool TryReadEtna(byte[] bytes, AnimationClip clip, byte[]? siblingBytes)
  {
    if (bytes.Length < 36 || bytes[0] != (byte)'E' || bytes[1] != (byte)'T' || bytes[2] != (byte)'N' || bytes[3] != (byte)'A')
      return false;

    var stringEnd = BitConverter.ToInt32(bytes, 8);
    var duration = BitConverter.ToSingle(bytes, 12);
    var rate = BitConverter.ToSingle(bytes, 16);
    var frames = BitConverter.ToInt32(bytes, 20);
    var declaredBones = BitConverter.ToInt32(bytes, 24);
    if (stringEnd < 36 || stringEnd > bytes.Length || declaredBones < 0 || declaredBones > 4096)
    {
      clip.Notes.Add("The file starts with ETNA, but the header counts are not usable.");
      return true;
    }

    var offset = 32;
    while (offset + 4 <= stringEnd && clip.Bones.Count < declaredBones + 8)
    {
      var length = BitConverter.ToInt32(bytes, offset);
      offset += 4;
      if (length <= 0 || length > 200 || offset + length > stringEnd)
        break;
      var name = Encoding.ASCII.GetString(bytes, offset, length);
      if (name.Any(ch => ch < 32 || ch > 126))
        break;
      clip.Bones.Add(name);
      offset += length;
    }

    clip.Duration = duration > 0 && !float.IsNaN(duration) ? duration : 0;
    clip.SampleRate = rate > 0 && rate < 1000 ? rate : 0;
    var events = new List<string>();
    var cursor = stringEnd;
    var packed = 0;
    while (cursor + 6 <= bytes.Length)
    {
      var id = BitConverter.ToUInt16(bytes, cursor);
      var end = BitConverter.ToInt32(bytes, cursor + 2);
      if (end <= cursor || end > bytes.Length)
        break;
      var payload = bytes.AsSpan(cursor + 6, end - (cursor + 6));
      packed += payload.Length;
      if (id == 245)
        CollectNames(payload, events);
      cursor = end;
    }

    foreach (var name in events.Distinct(StringComparer.Ordinal))
      clip.Sequences.Add(name);

    var dataNote = siblingBytes is null ? "" : $" Paired .sani_data is {siblingBytes.Length:N0} bytes and is not expanded.";
    clip.Notes.Add($"ETNA clip, {clip.Bones.Count} bones, {frames} frames, {clip.SampleRate:0.###} fps, {clip.Duration:0.###}s. Packed curve blocks total {packed:N0} bytes and were not expanded into keys.{dataNote}");
    if (clip.Bones.Count != declaredBones)
      clip.Notes.Add($"The header declared {declaredBones} bones and the name table has {clip.Bones.Count}.");
    return true;
  }

  private static void CollectNames(ReadOnlySpan<byte> payload, List<string> names)
  {
    var index = 0;
    while (index + 4 <= payload.Length)
    {
      var length = BitConverter.ToInt32(payload.Slice(index, 4));
      if (length is >= 3 and <= 64 && index + 4 + length <= payload.Length && IsName(payload.Slice(index + 4, length)))
      {
        names.Add(Encoding.ASCII.GetString(payload.Slice(index + 4, length)));
        index += 4 + length;
        continue;
      }

      index++;
    }

    var run = new List<byte>();
    for (var i = 0; i <= payload.Length; i++)
    {
      var printable = i < payload.Length && payload[i] is >= 32 and < 127;
      if (printable)
      {
        run.Add(payload[i]);
        continue;
      }

      if (i < payload.Length && payload[i] == 0 && run.Count is >= 4 and <= 64 && IsName(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(run)))
        names.Add(Encoding.ASCII.GetString(run.ToArray()));
      run.Clear();
    }
  }

  private static bool IsName(ReadOnlySpan<byte> bytes)
  {
    var letters = 0;
    foreach (var value in bytes)
    {
      if (value is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (byte)'_')
        letters++;
      else if (value is not ((>= (byte)'0' and <= (byte)'9') or (byte)'+' or (byte)'-'))
        return false;
    }

    return letters >= 3;
  }

  private static void AddVectors(List<VectorKey> keys, m3dSPL? spline, Vector3 initial, float duration, AnimationClip clip)
  {
    var raw = spline?.RawData;
    var data = raw?.Data;
    var count = (int)(raw?.Count ?? 0);
    if (data is null || count <= 0)
    {
      if (initial != Vector3.Zero)
        keys.Add(new VectorKey { Time = 0, Value = initial });
      return;
    }

    if (data.Length == count * 3)
    {
      for (var i = 0; i < count; i++)
      {
        keys.Add(new VectorKey
        {
          Time = count == 1 ? 0 : duration * i / (count - 1),
          Value = new Vector3(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]),
        });
      }

      return;
    }

    if (data.Length == count * 4)
    {
      for (var i = 0; i < count; i++)
      {
        keys.Add(new VectorKey
        {
          Time = data[i * 4],
          Value = new Vector3(data[i * 4 + 1], data[i * 4 + 2], data[i * 4 + 3]),
        });
      }

      return;
    }

    clip.Notes.Add($"A spline had {count} keys and {data.Length} floats, which is not 3 or 4 floats per key, so those keys were left raw.");
    if (initial != Vector3.Zero)
      keys.Add(new VectorKey { Time = 0, Value = initial });
  }

  private static void AddQuats(List<QuatKey> keys, m3dSPL? spline, Vector4 initial, float duration, AnimationClip clip)
  {
    var raw = spline?.RawData;
    var data = raw?.Data;
    var count = (int)(raw?.Count ?? 0);
    if (data is not null && count > 0 && data.Length == count * 4)
    {
      for (var i = 0; i < count; i++)
      {
        keys.Add(new QuatKey
        {
          Time = count == 1 ? 0 : duration * i / (count - 1),
          Value = Quaternion.Normalize(new Quaternion(data[i * 4], data[i * 4 + 1], data[i * 4 + 2], data[i * 4 + 3])),
        });
      }

      return;
    }

    if (initial != Vector4.Zero)
      keys.Add(new QuatKey { Time = 0, Value = Quaternion.Normalize(new Quaternion(initial.X, initial.Y, initial.Z, initial.W)) });
    else if (data is { Length: > 0 })
      clip.Notes.Add("A rotation spline was not 4 floats per key, so it was not sampled.");
  }

}
