using System.Text;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>One morpheme event: a point (duration 0) or a span on a track, tagged with event names.</summary>
public sealed record AnimEvent(float Start, float Duration, uint UserData, IReadOnlyList<string> Tags);


/// <summary>A named event track of a clip (<c>Footsteps</c>, <c>sound_foley</c>, <c>vfx_events</c>, <c>shake</c>, ...).</summary>
public sealed record AnimEventTrack(string Name, IReadOnlyList<AnimEvent> Events);

/// <summary>
///   ETNA block 245 (morpheme event tracks). Layout read from retail clips:
///   <c>u32 trackCount</c>; per track <c>u32 len, name, 16-byte hash, u32 0, u8 0, u32 eventCount</c>, per event
///   <c>f32 start, f32 duration, u32 userData, u32 tagCount, u32 tag[tagCount]</c>; then the tag table
///   <c>u32 count</c> x (<c>u32 len, kind ("Sound"), u32 3, u32 offset</c>), <c>u32 blobSize</c> and a blob of
///   NUL-terminated names that <c>offset</c> points into. Start and duration are normalised to the clip (0..1:
///   every track of a clip ends at 1, e.g. footsteps 0+0.163, 0.163+0.805, 0.969+0.031); multiply by the clip duration.
/// </summary>
public static class AnimEvents
{
  public static List<AnimEventTrack> Parse(ReadOnlySpan<byte> p)
  {
    var raw = new List<(string Name, List<(float, float, uint, int[])> Events)>();
    var c = 0;
    if (!U32(p, ref c, out var trackCount) || trackCount > 256) return [];
    for (var t = 0; t < trackCount; t++)
    {
      if (!Str(p, ref c, out var name) || c + 16 + 5 + 4 > p.Length) return [];
      c += 16 + 5;
      U32(p, ref c, out var count);
      if (count > 4096) return [];
      var events = new List<(float, float, uint, int[])>();
      for (var e = 0; e < count; e++)
      {
        if (c + 16 > p.Length) return [];
        var start = BitConverter.ToSingle(p.Slice(c, 4));
        var duration = BitConverter.ToSingle(p.Slice(c + 4, 4));
        var user = BitConverter.ToUInt32(p.Slice(c + 8, 4));
        var tags = BitConverter.ToInt32(p.Slice(c + 12, 4));
        c += 16;
        if (tags < 0 || tags > 64 || c + tags * 4 > p.Length) return [];
        var idx = new int[tags];
        for (var k = 0; k < tags; k++) idx[k] = BitConverter.ToInt32(p.Slice(c + k * 4, 4));
        c += tags * 4;
        events.Add((start, duration, user, idx));
      }
      raw.Add((name, events));
    }

    // Tag table: offsets into a trailing blob of NUL-terminated names.
    var offsets = new List<int>();
    if (U32(p, ref c, out var tagCount) && tagCount <= 4096)
    {
      for (var k = 0; k < tagCount; k++)
      {
        if (!Str(p, ref c, out _) || c + 8 > p.Length) { offsets.Clear(); break; }
        offsets.Add(BitConverter.ToInt32(p.Slice(c + 4, 4)));
        c += 8;
      }
    }

    var blob = U32(p, ref c, out var size) && c + size <= p.Length ? p.Slice(c, (int)size) : ReadOnlySpan<byte>.Empty;
    var tagNames = new List<string>();
    foreach (var off in offsets)
    {
      if (off < 0 || off >= blob.Length) { tagNames.Add(""); continue; }
      var end = blob[off..].IndexOf((byte)0);
      tagNames.Add(Encoding.ASCII.GetString(end < 0 ? blob[off..] : blob.Slice(off, end)));
    }

    return raw.Select(r => new AnimEventTrack(r.Name, r.Events.Select(e => new AnimEvent(e.Item1, e.Item2, e.Item3,
      e.Item4.Select(i => i >= 0 && i < tagNames.Count ? tagNames[i] : $"#{i}").ToArray())).ToList())).ToList();
  }

  private static bool U32(ReadOnlySpan<byte> p, ref int c, out uint value)
  {
    value = 0;
    if (c + 4 > p.Length) return false;
    value = BitConverter.ToUInt32(p.Slice(c, 4));
    c += 4;
    return true;
  }

  private static bool Str(ReadOnlySpan<byte> p, ref int c, out string value)
  {
    value = "";
    if (!U32(p, ref c, out var len) || len > 256 || c + len > p.Length) return false;
    value = Encoding.ASCII.GetString(p.Slice(c, (int)len));
    c += (int)len;
    return true;
  }
}
