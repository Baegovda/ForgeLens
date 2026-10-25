using System.Buffers.Binary;
using System.Numerics;

namespace LibSaber.SM2.Assets.Animation;

/// <summary>
///   Expands a retail ETNA <c>.sani</c> (class A, file version 2, codec subversion 2).
///   <c>0xF2</c> is the codec header plus every frame. <c>0xF6</c> is the same header plus a
///   few preview frames; the sibling <c>.sani_data</c> is one <c>0xF2</c> block whose payload
///   is the full frame section. Counts are six int32 values (animated T/R/S, then constant
///   T/R/S), followed by six uint16 index lists. Range records are 24 bytes (min xyz, scale xyz),
///   one shared record per constant channel and one per animated bone. Codes are unsigned and
///   decode as <c>code * scale + min</c>. A translation word is 11/11/10 bits. A rotation is
///   three uint16 values through the modified Rodrigues map. Constant tracks are written for
///   every listed bone, then animated samples overwrite the same bone. Scalar channels are not
///   bones and are not applied as scale. <c>0xF4</c> is root motion; its translation is stored
///   on the clip and applied to the skeleton root unless root motion is turned off.
/// </summary>
internal static class EtnaCurves
{

  public static bool TryExpand(byte[] sani, byte[]? data, AnimationClip clip, int frames)
  {
    if (frames < 1 || clip.Bones.Count == 0 || clip.SampleRate <= 0 || sani.Length < 36)
    {
      clip.Notes.Add("The ETNA header has no frame rate or bone names, so curve keys were not sampled.");
      return false;
    }

    var version = BinaryPrimitives.ReadUInt16LittleEndian(sani.AsSpan(4, 2));
    if (version is not (1 or 2))
    {
      clip.Notes.Add($"ETNA version {version} is not the class-A codec (version 2, subversion 2), so curve keys were not sampled.");
      return false;
    }

    byte[]? codec = null;
    var external = false;
    byte[]? root = null;
    var cursor = BinaryPrimitives.ReadInt32LittleEndian(sani.AsSpan(8, 4));
    while (cursor + 6 <= sani.Length)
    {
      var tag = BinaryPrimitives.ReadUInt16LittleEndian(sani.AsSpan(cursor, 2));
      var end = BinaryPrimitives.ReadInt32LittleEndian(sani.AsSpan(cursor + 2, 4));
      if (end <= cursor || end > sani.Length)
        break;
      var payload = sani.AsSpan(cursor + 6, end - (cursor + 6)).ToArray();
      if (tag == 0xF2)
      {
        codec = payload;
        external = false;
      }
      else if (tag == 0xF6)
      {
        codec = payload;
        external = true;
      }
      else if (tag == 0xF4)
        root = payload;

      cursor = end;
    }

    if (codec is null)
    {
      clip.Notes.Add("The ETNA file has no 0xF2 or 0xF6 codec block, so curve keys were not sampled.");
      return false;
    }

    // Factory FUN_142234c00(version, sub): the u16 sub-version is only read when version > 1
    // (FUN_14222fe40 case F2/F6); version 1 defaults to sub 1. Sub 1 is the raw float class
    // (vtbl 0x1437e71c0), sub 2 with version < 3 the quantized class A (vtbl 0x1437e73e0).
    var codecSub = version > 1 && codec.Length >= 2 ? BinaryPrimitives.ReadUInt16LittleEndian(codec.AsSpan(0, 2)) : 1;
    int sampledFrames, scalars;
    if (codecSub == 1)
    {
      var headerBones = BinaryPrimitives.ReadInt32LittleEndian(sani.AsSpan(24, 4));
      var headerScalars = BinaryPrimitives.ReadInt32LittleEndian(sani.AsSpan(28, 4));
      if (!TryCodecRaw(codec, version > 1 ? 2 : 0, external, data, frames, headerBones, headerScalars, clip, out sampledFrames, out scalars))
        return false;
    }
    else if (!TryCodec(codec, external, data, frames, clip, out sampledFrames, out scalars))
      return false;

    var rootKeys = 0;
    if (root is not null)
    {
      if (TryRoot(root, frames, clip))
        rootKeys = clip.RootTranslation.Count;
      else
        clip.Notes.Add("The 0xF4 root track did not match the frame count, so root translation was left out.");
    }

    var rotations = clip.Tracks.Count(track => track.Rotation.Count > 0);
    var translations = clip.Tracks.Count(track => track.Translation.Count > 0);
    var scalarNote = scalars == 0
      ? ""
      : $" {scalars} scalar channels are not bones and were not applied as scale.";
    var rootNote = rootKeys == 0
      ? " No root translation was stored."
      : $" Root translation has {rootKeys} keys (applied to the skeleton root unless --no-root-motion).";
    clip.Notes.Add(
      $"Sampled {rotations} rotation and {translations} translation tracks across {sampledFrames} frames. "
      + "Codes are unsigned (value = code * scale + min). Translation is 11/11/10. "
      + "Rotation is a modified Rodrigues quaternion and replaces the bind rotation. "
      + "Constant tracks cover every listed bone."
      + scalarNote
      + rootNote);
    return true;
  }

  private static bool TryCodec(
    byte[] payload,
    bool external,
    byte[]? data,
    int frames,
    AnimationClip clip,
    out int sampledFrames,
    out int scalars)
  {
    sampledFrames = 0;
    scalars = 0;
    var reader = new Reader(payload);
    var sub = reader.U16();
    if (!reader.Ok || sub != 2)
    {
      clip.Notes.Add("The codec block does not start with subversion 2, so curve keys were not sampled.");
      return false;
    }

    var counts = new int[6];
    for (var i = 0; i < 6; i++)
      counts[i] = reader.I32();
    var preview = external ? reader.I32() : 0;
    if (!reader.Ok || counts.Any(count => count < 0) || preview < 0)
    {
      clip.Notes.Add("The codec counts are not usable, so curve keys were not sampled.");
      return false;
    }

    var n0 = counts[0];
    var n1 = counts[1];
    var n2 = counts[2];
    var n3 = counts[3];
    var n4 = counts[4];
    var n5 = counts[5];
    var idxT = reader.U16List(n0);
    var idxR = reader.U16List(n1);
    reader.U16List(n2);
    var idxCt = reader.U16List(n3);
    var idxCr = reader.U16List(n4);
    reader.U16List(n5);
    var constT = reader.Range3();
    var constR = reader.Range3();
    reader.F32();
    reader.F32();
    var animT = new Range3[n0];
    var animR = new Range3[n1];
    for (var i = 0; i < n0; i++)
      animT[i] = reader.Range3();
    for (var i = 0; i < n1; i++)
      animR[i] = reader.Range3();
    for (var i = 0; i < n2; i++)
    {
      reader.F32();
      reader.F32();
    }

    var constTCodes = new ushort[n3 * 3];
    var constRCodes = new ushort[n4 * 3];
    for (var i = 0; i < constTCodes.Length; i++)
      constTCodes[i] = reader.U16();
    for (var i = 0; i < constRCodes.Length; i++)
      constRCodes[i] = reader.U16();
    for (var i = 0; i < n5; i++)
      reader.U16();
    if (!reader.Ok)
    {
      clip.Notes.Add("The codec header ended before the constant values, so curve keys were not sampled.");
      return false;
    }

    ReadOnlySpan<byte> frameData;
    if (!external)
    {
      sampledFrames = frames;
      frameData = payload.AsSpan(reader.Offset);
    }
    else if (data is not null)
    {
      if (data.Length < 6
        || BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2)) != 0xF2
        || BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(2, 4)) is var mainEnd && (mainEnd < 6 || mainEnd > data.Length))
      {
        clip.Notes.Add(".sani_data does not start with a 0xF2 block, so curve keys were not sampled.");
        return false;
      }

      // Clips with a second codec slot (0xF7 in the .sani = external 0xF3) append a 0xF3
      // block after the 0xF2 one: {F2 main frames}{F3 second-slot frames}. The main slot is
      // the 0xF2 block; the second slot is not sampled.
      if (mainEnd < data.Length)
        clip.Notes.Add(".sani_data also carries a 0xF3 second-slot block, which is not applied.");
      sampledFrames = frames;
      frameData = data.AsSpan(6, mainEnd - 6);
    }
    else
    {
      sampledFrames = preview;
      frameData = payload.AsSpan(reader.Offset);
      clip.Notes.Add($"Only {preview} preview frames are in the 0xF6 block. The full clip is {frames} frames and needs the sibling .sani_data.");
    }

    if (sampledFrames < 1)
    {
      clip.Notes.Add("The codec block has no frames, so curve keys were not sampled.");
      return false;
    }

    var frameSize = n0 * 4 + n1 * 6 + n2 * 2;
    if (frameData.Length < sampledFrames * frameSize)
    {
      clip.Notes.Add(
        $"Frame data is {frameData.Length} bytes; {sampledFrames} frames of {n0} translations, {n1} rotations, and {n2} scalars would be {sampledFrames * frameSize}. Keys were not sampled.");
      return false;
    }

    var bones = clip.Bones.Count;
    var animTSlot = new int[bones];
    var animRSlot = new int[bones];
    Array.Fill(animTSlot, -1);
    Array.Fill(animRSlot, -1);
    var constVec = new Vector3?[bones];
    var constQuat = new Quaternion?[bones];
    var skipped = 0;
    for (var i = 0; i < n3; i++)
    {
      var bone = idxCt[i];
      if ((uint)bone >= (uint)bones)
      {
        skipped++;
        continue;
      }

      constVec[bone] = constT.Decode(constTCodes[i * 3], constTCodes[i * 3 + 1], constTCodes[i * 3 + 2]);
    }

    for (var i = 0; i < n4; i++)
    {
      var bone = idxCr[i];
      if ((uint)bone >= (uint)bones)
      {
        skipped++;
        continue;
      }

      constQuat[bone] = constR.DecodeQuat(constRCodes[i * 3], constRCodes[i * 3 + 1], constRCodes[i * 3 + 2]);
    }

    for (var i = 0; i < n0; i++)
    {
      var bone = idxT[i];
      if ((uint)bone >= (uint)bones)
      {
        skipped++;
        continue;
      }

      animTSlot[bone] = i;
    }

    for (var i = 0; i < n1; i++)
    {
      var bone = idxR[i];
      if ((uint)bone >= (uint)bones)
      {
        skipped++;
        continue;
      }

      animRSlot[bone] = i;
    }

    var tracks = new BoneTrack?[bones];
    for (var bone = 0; bone < bones; bone++)
    {
      if (animTSlot[bone] < 0 && constVec[bone] is null && animRSlot[bone] < 0 && constQuat[bone] is null)
        continue;
      tracks[bone] = new BoneTrack { Bone = -1, BoneName = clip.Bones[bone] };
    }

    var translationBytes = sampledFrames * n0 * 4;
    var rotationBytes = sampledFrames * n1 * 6;
    var translations = frameData.Slice(0, translationBytes);
    var rotations = frameData.Slice(translationBytes, rotationBytes);
    var rate = clip.SampleRate;
    for (var frame = 0; frame < sampledFrames; frame++)
    {
      var time = frame / rate;
      for (var bone = 0; bone < bones; bone++)
      {
        var track = tracks[bone];
        if (track is null)
          continue;
        if (animTSlot[bone] is int slot and >= 0)
        {
          var word = BinaryPrimitives.ReadUInt32LittleEndian(translations.Slice((frame * n0 + slot) * 4, 4));
          track.Translation.Add(new VectorKey { Time = time, Value = animT[slot].Decode(word >> 21, (word >> 10) & 0x7FF, word & 0x3FF) });
        }
        else if (constVec[bone] is Vector3 constantTranslation)
          track.Translation.Add(new VectorKey { Time = time, Value = constantTranslation });

        if (animRSlot[bone] is int rotSlot and >= 0)
        {
          var offset = (frame * n1 + rotSlot) * 6;
          var x = BinaryPrimitives.ReadUInt16LittleEndian(rotations.Slice(offset, 2));
          var y = BinaryPrimitives.ReadUInt16LittleEndian(rotations.Slice(offset + 2, 2));
          var z = BinaryPrimitives.ReadUInt16LittleEndian(rotations.Slice(offset + 4, 2));
          track.Rotation.Add(new QuatKey { Time = time, Value = animR[rotSlot].DecodeQuat(x, y, z) });
        }
        else if (constQuat[bone] is Quaternion constantRotation)
          track.Rotation.Add(new QuatKey { Time = time, Value = constantRotation });
      }
    }

    foreach (var track in tracks)
    {
      if (track is not null)
        clip.Tracks.Add(track);
    }

    if (clip.Tracks.Count == 0)
    {
      clip.Notes.Add("The codec lists no bone tracks inside the name table, so curve keys were not sampled.");
      return false;
    }

    if (skipped > 0)
      clip.Notes.Add($"{skipped} codec indices fell outside the name table and were skipped.");
    scalars = n2 + n5;
    return true;
  }

  /// <summary>
  ///   Codec sub-version 1 (and every version-1 file): uncompressed floats, vtbl 0x1437e71c0.
  ///   Header FUN_142228a70: i32 nT, nR, nS (+ i32 previewFrames for 0xF6). Carve FUN_142228dd0:
  ///   u16 idxT[nT], idxR[nR], idxS[nS]; float4 defaultT[bones]; float4 defaultR[bones] (w,x,y,z);
  ///   float defaultS[scalars] (bones/scalars = ETNA header +24/+28, clip+0x34/+0x38). Frames
  ///   FUN_142228e30, sizes FUN_142228ed0: all frames of float4 T[pad4(nT)], then all frames of
  ///   float4 R[pad4(nR)], then all frames of float S[nS]. Sampler FUN_142227680 copies the defaults
  ///   for every bone, then lerps T and quat-blends R at the listed indices.
  /// </summary>
  private static bool TryCodecRaw(byte[] payload, int start, bool external, byte[]? data, int frames, int headerBones, int headerScalars,
    AnimationClip clip, out int sampledFrames, out int scalars)
  {
    sampledFrames = 0;
    scalars = 0;
    var span = payload.AsSpan();
    if (span.Length < start + 12) return false;
    int I32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o, 4));
    float F(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadSingleLittleEndian(b.Slice(o, 4));
    var o = start;
    var nT = I32(span, o); var nR = I32(span, o + 4); var nS = I32(span, o + 8); o += 12;
    var preview = 0;
    if (external) { preview = I32(span, o); o += 4; }
    if (nT < 0 || nR < 0 || nS < 0 || headerBones < 0 || headerScalars < 0 || preview < 0)
    {
      clip.Notes.Add("The raw codec counts are not usable, so curve keys were not sampled.");
      return false;
    }

    var header = (nT + nR + nS) * 2 + (headerScalars + headerBones * 8) * 4;
    int Pad(int n) => (n + 3) / 4 * 4;
    var pT = Pad(nT); var pR = Pad(nR);
    var perFrame = (nS + (pT + pR) * 4) * 4;
    if (span.Length < o + header)
    {
      clip.Notes.Add("The raw codec header is truncated, so curve keys were not sampled.");
      return false;
    }

    var idx = new int[nT + nR];
    for (var i = 0; i < nT + nR; i++) idx[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(o + i * 2, 2));
    var defaults = o + (nT + nR + nS) * 2;
    var defaultR = defaults + headerBones * 16;

    ReadOnlySpan<byte> frameData;
    if (!external) { sampledFrames = frames; frameData = span.Slice(o + header); }
    else if (data is not null && data.Length >= 6 && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2)) == 0xF2
      && BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(2, 4)) is var end && end >= 6 && end <= data.Length)
    { sampledFrames = frames; frameData = data.AsSpan(6, end - 6); }
    else
    {
      sampledFrames = preview;
      frameData = span.Slice(o + header);
      clip.Notes.Add($"Only {preview} preview frames are in the 0xF6 block. The full clip is {frames} frames and needs the sibling .sani_data.");
    }

    if (sampledFrames < 1 || frameData.Length < sampledFrames * perFrame)
    {
      clip.Notes.Add($"Raw frame data is {frameData.Length} bytes; {sampledFrames} frames need {sampledFrames * perFrame}. Keys were not sampled.");
      return false;
    }

    var bones = clip.Bones.Count;
    var tracks = new BoneTrack[bones];
    var tSlot = new int[bones]; var rSlot = new int[bones];
    Array.Fill(tSlot, -1); Array.Fill(rSlot, -1);
    for (var i = 0; i < nT; i++) if (idx[i] < bones) tSlot[idx[i]] = i;
    for (var i = 0; i < nR; i++) if (idx[nT + i] < bones) rSlot[idx[nT + i]] = i;
    var rBase = sampledFrames * pT * 16;
    var rate = clip.SampleRate;
    for (var bone = 0; bone < bones; bone++)
      tracks[bone] = new BoneTrack { Bone = -1, BoneName = clip.Bones[bone] };
    for (var frame = 0; frame < sampledFrames; frame++)
    {
      var time = frame / rate;
      for (var bone = 0; bone < bones; bone++)
      {
        Vector3 t; Quaternion q;
        if (tSlot[bone] >= 0) { var b = (frame * pT + tSlot[bone]) * 16; t = new(F(frameData, b), F(frameData, b + 4), F(frameData, b + 8)); }
        else if (bone < headerBones) { var b = defaults + bone * 16; t = new(F(span, b), F(span, b + 4), F(span, b + 8)); }
        else continue;
        if (rSlot[bone] >= 0) { var b = rBase + (frame * pR + rSlot[bone]) * 16; q = new(F(frameData, b + 4), F(frameData, b + 8), F(frameData, b + 12), F(frameData, b)); }
        else { var b = defaultR + bone * 16; q = new(F(span, b + 4), F(span, b + 8), F(span, b + 12), F(span, b)); }
        tracks[bone].Translation.Add(new VectorKey { Time = time, Value = t });
        tracks[bone].Rotation.Add(new QuatKey { Time = time, Value = Quaternion.Normalize(q) });
      }
    }

    foreach (var track in tracks)
      if (track.Translation.Count + track.Rotation.Count > 0)
        clip.Tracks.Add(track);
    scalars = nS;
    clip.Notes.Add($"Raw float codec (sub-version 1): {nT} animated translations, {nR} animated rotations, defaults for {headerBones} bones.");
    return clip.Tracks.Count > 0;
  }

  private static bool TryRoot(byte[] payload, int frames, AnimationClip clip)
  {
    var reader = new Reader(payload);
    var sub = reader.U16();
    var translation = reader.Range3();
    var rotation = reader.Range3();
    if (!reader.Ok || sub != 2)
      return false;
    var needed = frames * 12;
    if (payload.Length < reader.Offset + needed)
      return false;

    var rate = clip.SampleRate;
    var codes = payload.AsSpan(reader.Offset);
    for (var frame = 0; frame < frames; frame++)
    {
      var offset = frame * 6;
      var x = BinaryPrimitives.ReadUInt16LittleEndian(codes.Slice(offset, 2));
      var y = BinaryPrimitives.ReadUInt16LittleEndian(codes.Slice(offset + 2, 2));
      var z = BinaryPrimitives.ReadUInt16LittleEndian(codes.Slice(offset + 4, 2));
      clip.RootTranslation.Add(new VectorKey { Time = frame / rate, Value = translation.Decode(x, y, z) });
    }

    var rotationOffset = frames * 6;
    for (var frame = 0; frame < frames; frame++)
    {
      var offset = rotationOffset + frame * 6;
      var x = BinaryPrimitives.ReadUInt16LittleEndian(codes.Slice(offset, 2));
      var y = BinaryPrimitives.ReadUInt16LittleEndian(codes.Slice(offset + 2, 2));
      var z = BinaryPrimitives.ReadUInt16LittleEndian(codes.Slice(offset + 4, 2));
      clip.RootRotation.Add(new QuatKey { Time = frame / rate, Value = rotation.DecodeQuat(x, y, z) });
    }

    return true;
  }

  private static Quaternion Mrp(double x, double y, double z)
  {
    var squared = x * x + y * y + z * z;
    var scale = 1.0 / (squared + 1.0);
    return new Quaternion(
      (float)(2 * scale * x),
      (float)(2 * scale * y),
      (float)(2 * scale * z),
      (float)((1.0 - squared) * scale));
  }

  private readonly struct Range3
  {
    public float MinX { get; init; }
    public float MinY { get; init; }
    public float MinZ { get; init; }
    public float ScaleX { get; init; }
    public float ScaleY { get; init; }
    public float ScaleZ { get; init; }

    public Vector3 Decode(uint x, uint y, uint z)
      => new(
        (float)(x * (double)ScaleX + MinX),
        (float)(y * (double)ScaleY + MinY),
        (float)(z * (double)ScaleZ + MinZ));

    public Quaternion DecodeQuat(uint x, uint y, uint z)
      => Mrp(x * (double)ScaleX + MinX, y * (double)ScaleY + MinY, z * (double)ScaleZ + MinZ);
  }

  private ref struct Reader
  {
    private readonly ReadOnlySpan<byte> _span;
    public int Offset;
    public bool Ok;

    public Reader(ReadOnlySpan<byte> span)
    {
      _span = span;
      Offset = 0;
      Ok = true;
    }

    public ushort U16()
    {
      if (!Ok || Offset + 2 > _span.Length)
      {
        Ok = false;
        return 0;
      }

      var value = BinaryPrimitives.ReadUInt16LittleEndian(_span.Slice(Offset, 2));
      Offset += 2;
      return value;
    }

    public int I32()
    {
      if (!Ok || Offset + 4 > _span.Length)
      {
        Ok = false;
        return 0;
      }

      var value = BinaryPrimitives.ReadInt32LittleEndian(_span.Slice(Offset, 4));
      Offset += 4;
      return value;
    }

    public float F32()
    {
      if (!Ok || Offset + 4 > _span.Length)
      {
        Ok = false;
        return 0;
      }

      var value = BinaryPrimitives.ReadSingleLittleEndian(_span.Slice(Offset, 4));
      Offset += 4;
      return value;
    }

    public ushort[] U16List(int count)
    {
      var list = new ushort[count];
      for (var i = 0; i < count; i++)
        list[i] = U16();
      return list;
    }

    public Range3 Range3()
      => new()
      {
        MinX = F32(),
        MinY = F32(),
        MinZ = F32(),
        ScaleX = F32(),
        ScaleY = F32(),
        ScaleZ = F32(),
      };
  }

}
