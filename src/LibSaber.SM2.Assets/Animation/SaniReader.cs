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
      clip.Notes.Add(".sani_data is the external animation payload. Curves are read from the paired .sani when that file matches animTRACK. This pairing is unverified on retail files.");
      return clip;
    }

    var bytes = store.Read(fileIndex);
    if (TextureLoader.LooksText(bytes))
    {
      clip.Notes.Add("This animation file is text (often .sani.resource). It is shown as text. Curve export needs the binary .sani.");
      clip.Notes.Add(Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 2000)));
      return clip;
    }

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
