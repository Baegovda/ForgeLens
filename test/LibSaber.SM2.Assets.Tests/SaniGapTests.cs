using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>
///   Synthetic ETNA files for the .sani gaps: version 3 (scale channel, empty names), the
///   0xF3/0xF7 additive slot, and version-1 raw root motion.
/// </summary>
public class SaniGapTests
{

  private sealed class Bytes
  {
    public readonly List<byte> Data = new();
    public Bytes U16(int v) { Data.AddRange(BitConverter.GetBytes((ushort)v)); return this; }
    public Bytes I32(int v) { Data.AddRange(BitConverter.GetBytes(v)); return this; }
    public Bytes F32(float v) { Data.AddRange(BitConverter.GetBytes(v)); return this; }
    public Bytes U32(uint v) { Data.AddRange(BitConverter.GetBytes(v)); return this; }
    public Bytes Range(float min, float scale) { for (var i = 0; i < 3; i++) F32(min); for (var i = 0; i < 3; i++) F32(scale); return this; }
    public Bytes Name(string s) { I32(s.Length); Data.AddRange(Encoding.ASCII.GetBytes(s)); return this; }
    public byte[] ToArray() => Data.ToArray();
  }

  private static byte[] Etna(int version, int frames, float rate, string[] bones, string[] scalars, params (int Tag, byte[] Payload)[] blocks)
  {
    var f1 = new Bytes();
    f1.F32((frames - 1) / rate).F32(rate).I32(frames).I32(bones.Length).I32(scalars.Length);
    foreach (var n in bones) f1.Name(n);
    foreach (var n in scalars) f1.Name(n);
    var file = new Bytes();
    file.Data.AddRange(Encoding.ASCII.GetBytes("ETNA"));
    file.U16(version);
    file.U16(0xF1).I32(6 + 6 + f1.Data.Count);
    file.Data.AddRange(f1.Data);
    foreach (var (tag, payload) in blocks)
    {
      var end = file.Data.Count + 6 + payload.Length;
      file.U16(tag).I32(end);
      file.Data.AddRange(payload);
    }

    return file.ToArray();
  }

  /// <summary>
  ///   v3 codec (FUN_14222d0d0 / FUN_14222d550): bone 0 animated T/R/S, bone 1 constant T/R/S,
  ///   one constant scalar. Two frames.
  /// </summary>
  private static byte[] V3Codec(float scaleCode0, float scaleCode1)
  {
    var c = new Bytes();
    c.U16(2);
    c.I32(1).I32(1).I32(1).I32(0); // nT nR nS nF
    c.I32(1).I32(1).I32(1).I32(1); // cT cR cS cF
    c.U16(0).U16(0).U16(0); // idxT idxR idxS
    c.U16(1).U16(1).U16(1).U16(0); // idxCT idxCR idxCS idxCF
    c.Range(0, 0.5f); // const T
    c.Range(0, 0); // const R (identity)
    c.Range(1, 0.25f); // const S
    c.F32(0).F32(0); // const F
    c.Range(0, 1); // anim T
    c.Range(0, 0); // anim R
    c.Range(1, 0.5f); // anim S
    c.U16(2).U16(4).U16(6); // const T codes -> (1,2,3)
    c.U16(0).U16(0).U16(0); // const R
    c.U16(4).U16(4).U16(4); // const S -> 2
    c.U16(0); // const F
    // frames: T plane (2 frames), R plane, S plane
    c.U32((1u << 21) | (2u << 10) | 3u).U32((4u << 21) | (5u << 10) | 6u);
    c.U16(0).U16(0).U16(0).U16(0).U16(0).U16(0);
    c.U16((int)scaleCode0).U16((int)scaleCode0).U16((int)scaleCode0);
    c.U16((int)scaleCode1).U16((int)scaleCode1).U16((int)scaleCode1);
    return c.ToArray();
  }

  [Fact]
  public void Version3_DecodesScaleAndEmptyNames()
  {
    var sani = Etna(3, 2, 30, new[] { "", "lid" }, new[] { "" }, (0xF2, V3Codec(0, 2)), (0xF3, V3Codec(0, 0)));
    var clip = SaniReader.FromBytes(sani, null);

    Assert.Equal(new[] { "unnamed_000", "lid" }, clip.Bones);
    Assert.Single(clip.Scalars);
    Assert.True(clip.HasAdditive);
    Assert.Equal(2, clip.Tracks.Count);
    var animated = clip.Tracks.Single(t => t.BoneName == "unnamed_000");
    Assert.Equal(new Vector3(1, 2, 3), animated.Translation[0].Value);
    Assert.Equal(new Vector3(4, 5, 6), animated.Translation[1].Value);
    Assert.Equal(Vector3.One, animated.Scale[0].Value);
    Assert.Equal(new Vector3(2), animated.Scale[1].Value);
    var constant = clip.Tracks.Single(t => t.BoneName == "lid");
    Assert.Equal(new Vector3(1, 2, 3), constant.Translation[1].Value);
    Assert.Equal(new Vector3(2), constant.Scale[0].Value);
    Assert.Equal(Quaternion.Identity, constant.Rotation[0].Value);
  }

  [Fact]
  public void Version3_External_UsesFirstSaniDataBlock()
  {
    var full = V3Codec(0, 2);
    // Header part of the codec = everything before the 2 frames (2*4 + 2*6 + 2*6 = 32 bytes).
    var header = full.AsSpan(0, full.Length - 32).ToArray();
    var frames = full.AsSpan(full.Length - 32).ToArray();
    var ext = new List<byte>();
    ext.AddRange(header.AsSpan(0, 2 + 8 * 4).ToArray());
    ext.AddRange(BitConverter.GetBytes(0)); // preview frames
    ext.AddRange(header.AsSpan(2 + 8 * 4).ToArray());
    var data = new List<byte>();
    data.AddRange(BitConverter.GetBytes((ushort)0xF2)); data.AddRange(BitConverter.GetBytes(6 + frames.Length)); data.AddRange(frames);
    var garbage = new byte[32];
    var f3End = data.Count + 6 + garbage.Length;
    data.AddRange(BitConverter.GetBytes((ushort)0xF3)); data.AddRange(BitConverter.GetBytes(f3End)); data.AddRange(garbage);
    var sani = Etna(3, 2, 30, new[] { "a", "b" }, Array.Empty<string>(), (0xF6, ext.ToArray()), (0xF7, ext.ToArray()));

    var clip = SaniReader.FromBytes(sani, data.ToArray());

    Assert.True(clip.HasAdditive);
    Assert.Equal(new Vector3(4, 5, 6), clip.Tracks.Single(t => t.BoneName == "a").Translation[1].Value);
    Assert.Equal(new Vector3(2), clip.Tracks.Single(t => t.BoneName == "a").Scale[1].Value);
  }

  [Fact]
  public void Version1_RootMotion_IsRawFloats()
  {
    // v1 codec sub 1 (no sub u16): nT=0 nR=0 nS=0, defaults for 1 bone.
    var codec = new Bytes();
    codec.I32(0).I32(0).I32(0);
    codec.F32(0).F32(1).F32(0).F32(0); // default T
    codec.F32(1).F32(0).F32(0).F32(0); // default R (w,x,y,z)
    // F4 v1: float4 T[frames], then float4 R[frames] (w,x,y,z). FUN_14222f140 / FUN_14222ef50.
    var root = new Bytes();
    root.F32(0).F32(0).F32(0).F32(0).F32(0).F32(0).F32(2).F32(0);
    var s = MathF.Sqrt(0.5f);
    root.F32(1).F32(0).F32(0).F32(0).F32(s).F32(0).F32(s).F32(0);
    var sani = Etna(1, 2, 30, new[] { "ROOT" }, Array.Empty<string>(), (0xF2, codec.ToArray()), (0xF4, root.ToArray()));

    var clip = SaniReader.FromBytes(sani, null);

    Assert.Equal(2, clip.RootTranslation.Count);
    Assert.Equal(new Vector3(0, 0, 2), clip.RootTranslation[1].Value);
    Assert.Equal(Quaternion.Identity, clip.RootRotation[0].Value);
    Assert.True(Math.Abs(clip.RootRotation[1].Value.Y - s) < 1e-6);
  }

  [Fact]
  public void Names_ReadsBonesThenScalarsAndKeepsScalarsOutOfBones()
  {
    var codec = new Bytes();
    codec.I32(0).I32(0).I32(0);
    codec.F32(0).F32(0).F32(0).F32(0).F32(1).F32(0).F32(0).F32(0);
    codec.F32(0);
    var sani = Etna(1, 1, 30, new[] { "hip" }, new[] { "blink" }, (0xF2, codec.ToArray()));
    var clip = SaniReader.FromBytes(sani, null);
    Assert.Equal(new[] { "hip" }, clip.Bones);
    Assert.Equal(new[] { "blink" }, clip.Scalars);
  }

}
