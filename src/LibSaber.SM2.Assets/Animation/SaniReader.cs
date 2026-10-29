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

    if (TryReadEtna(bytes, clip, SiblingData(catalog, store, fileIndex)))
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

      // propanim: the same decoder as a template's embedded track ([frame, values] keys, w-first conjugated
      // rotations; DATA-PROVEN), replacing the old guess (values only, xyzw, frames spread over the clip).
      if (TplAnimation.From(track) is { Objects.Count: > 0 } decoded)
      {
        var fps = decoded.Sequences.FirstOrDefault(s => s.Seconds > 0)?.Fps ?? TplAnimation.DefaultFps;
        var whole = decoded.Timeline(clip.Name, fps);
        clip.Duration = Math.Max(clip.Duration, whole.Duration);
        for (var i = 0; i < whole.Tracks.Count; i++)
        {
          var boneTrack = whole.Tracks[i];
          if (track.objMapList is not null && i < track.objMapList.Count)
          {
            boneTrack.Bone = track.objMapList[i];
            boneTrack.BoneName = "bone" + boneTrack.Bone;
          }

          if (boneTrack.Translation.Count + boneTrack.Rotation.Count + boneTrack.Scale.Count > 0)
            clip.Tracks.Add(boneTrack);
        }

        clip.Notes.AddRange(decoded.Notes);
      }

      if (clip.Sequences.Count == 0 && clip.Tracks.Count == 0)
        clip.Notes.Add("animTRACK parsed, but it had no sequences or curves.");
      else
        clip.Notes.Add("animTRACK keys decoded as [frame, values] (DATA-PROVEN on the tpl-embedded tracks; loose animTRACK .sani are rare). Without objMapList, tracks bind by animNmb rank.");
    }
    catch (Exception ex)
    {
      clip.Notes.Add("This .sani did not parse as animTRACK (" + ex.Message + "). Retail animation files are unverified. The raw bytes can still be exported.");
    }

    return clip;
  }

  /// <summary>Decode an ETNA .sani from raw bytes (and its .sani_data, when the clip is 0xF6). Used by bulk validation.</summary>
  /// <summary>
  ///   mtl1-49: only the morpheme event tracks (section 245) and the duration of an ETNA clip, without decoding curves
  ///   (context playback scans a character's whole bundle for the event that starts an effect). Null when not ETNA.
  /// </summary>
  public static (float Duration, List<AnimEventTrack> Tracks)? ReadEvents(byte[] bytes)
  {
    if (bytes.Length < 36 || bytes[0] != (byte)'E' || bytes[1] != (byte)'T' || bytes[2] != (byte)'N' || bytes[3] != (byte)'A')
      return null;
    var stringEnd = BitConverter.ToInt32(bytes, 8);
    var duration = BitConverter.ToSingle(bytes, 12);
    if (stringEnd < 36 || stringEnd > bytes.Length) return null;
    var tracks = new List<AnimEventTrack>();
    var cursor = stringEnd;
    while (cursor + 6 <= bytes.Length)
    {
      var id = BitConverter.ToUInt16(bytes, cursor);
      var end = BitConverter.ToInt32(bytes, cursor + 2);
      if (end <= cursor || end > bytes.Length) break;
      if (id == 245) tracks.AddRange(AnimEvents.Parse(bytes.AsSpan(cursor + 6, end - (cursor + 6))));
      cursor = end;
    }

    return (duration > 0 && !float.IsNaN(duration) ? duration : 0, tracks);
  }

  public static AnimationClip FromBytes(byte[] sani, byte[]? data, string name = "clip")
  {
    var clip = new AnimationClip { Name = name };
    if (!TryReadEtna(sani, clip, data))
      clip.Notes.Add("Not an ETNA clip.");
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
  ///   and the bone name table. <see cref="EtnaCurves"/> samples the class-A codec
  ///   (inline 0xF2, or 0xF6 plus the paired .sani_data) and the 0xF4 root translation.
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

    // F1 header FUN_1422336c0: bones (+24) names, then scalars (+28) names, each hashed by
    // FUN_141c3fa90. A name may be empty (version-3 tpl clips leave unnamed objects blank).
    var declaredScalars = BitConverter.ToInt32(bytes, 28);
    if (declaredScalars < 0 || declaredScalars > 4096)
      declaredScalars = 0;
    var names = ReadNames(bytes, 32, stringEnd, declaredBones + declaredScalars);
    for (var i = 0; i < names.Count; i++)
    {
      // Empty names cannot bind by name; give each a unique placeholder so the skeleton
      // written for the clip has distinct bones (see HANDOFF: binding v3 tpl tracks).
      if (i < declaredBones)
        clip.Bones.Add(names[i].Length > 0 ? names[i] : $"unnamed_{i:D3}");
      else
        clip.Scalars.Add(names[i]);
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
      {
        CollectNames(payload, events);
        clip.EventTracks.AddRange(AnimEvents.Parse(payload));
      }
      cursor = end;
    }

    foreach (var name in events.Distinct(StringComparer.Ordinal))
      clip.Sequences.Add(name);

    var expanded = EtnaCurves.TryExpand(bytes, siblingBytes, clip, frames);
    var dataNote = siblingBytes is null ? "" : expanded ? "" : $" Paired .sani_data is {siblingBytes.Length:N0} bytes.";
    if (!expanded && siblingBytes is not null && clip.Notes.Count == 0)
      clip.Notes.Add("Paired .sani_data did not yield keys.");
    clip.Notes.Insert(0, $"ETNA clip, {clip.Bones.Count} bones, {frames} frames, {clip.SampleRate:0.###} fps, {clip.Duration:0.###}s. Packed blocks total {packed:N0} bytes.{dataNote}");
    if (clip.Bones.Count != declaredBones || clip.Scalars.Count != declaredScalars)
      clip.Notes.Add($"The header declared {declaredBones} bones and {declaredScalars} scalars; the name table has {clip.Bones.Count} and {clip.Scalars.Count}.");
    return true;
  }

  private static byte[]? SiblingData(GameCatalog catalog, ArchiveStore store, int fileIndex)
  {
    var path = catalog.GetFile(fileIndex).Path.Replace('\\', '/');
    if (!path.EndsWith(".sani", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sani_data", StringComparison.OrdinalIgnoreCase))
      return null;
    var dataPath = path + "_data";
    var index = catalog.ResolveReference(dataPath, fileIndex);
    if (index < 0)
      index = catalog.ResolveReference(System.IO.Path.GetFileName(dataPath), fileIndex);
    return index >= 0 ? store.Read(index) : null;
  }

  /// <summary>Bone names and timing from an ETNA header, without reading curve bytes.</summary>
  public static bool TryPeek(ReadOnlySpan<byte> bytes, out float duration, out float rate, out List<string> bones)
  {
    duration = 0;
    rate = 0;
    bones = new List<string>();
    if (bytes.Length < 36 || bytes[0] != (byte)'E' || bytes[1] != (byte)'T' || bytes[2] != (byte)'N' || bytes[3] != (byte)'A')
      return false;
    var stringEnd = BitConverter.ToInt32(bytes.Slice(8, 4));
    duration = BitConverter.ToSingle(bytes.Slice(12, 4));
    rate = BitConverter.ToSingle(bytes.Slice(16, 4));
    if (stringEnd < 36 || stringEnd > bytes.Length)
      return false;
    var declared = BitConverter.ToInt32(bytes.Slice(24, 4));
    if (declared < 0 || declared > 4096)
      return false;
    bones = ReadNames(bytes, 32, stringEnd, declared);

    return bones.Count > 0;
  }

  /// <summary>Length-prefixed ASCII names (u32 length, may be 0). Stops at a bad length or the F1 end.</summary>
  private static List<string> ReadNames(ReadOnlySpan<byte> bytes, int offset, int end, int count)
  {
    var names = new List<string>();
    while (names.Count < count && offset + 4 <= end)
    {
      var length = BitConverter.ToInt32(bytes.Slice(offset, 4));
      offset += 4;
      if (length < 0 || length > 256 || offset + length > end)
        break;
      var raw = bytes.Slice(offset, length);
      var ok = true;
      foreach (var ch in raw)
        ok &= ch is >= 32 and <= 126;
      if (!ok)
        break;
      names.Add(Encoding.ASCII.GetString(raw));
      offset += length;
    }

    return names;
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

}
