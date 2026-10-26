using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>Track to bone binding by template animNmb (FUN_142232230), with the by-name fallback.</summary>
public class ClipBindingTests
{

  private static AnimationClip Clip(params string[] names)
  {
    var clip = new AnimationClip { Duration = 1, SampleRate = 30 };
    foreach (var name in names)
    {
      var track = new BoneTrack { BoneName = name };
      track.Translation.Add(new VectorKey { Time = 0, Value = new Vector3(1, 2, 3) });
      clip.Tracks.Add(track);
    }

    return clip;
  }

  [Fact]
  public void EmptyNamedTracksBindByAnimNmbRank()
  {
    // Objects: root (not animated), door (animNmb 1), unnamed object (animNmb 0), lid (animNmb 2).
    var bones = new List<Bone>
    {
      new() { Name = "root", AnimIndex = -1 },
      new() { Name = "door", AnimIndex = 1, Parent = 0 },
      new() { Name = "bone2", AnimIndex = 0, Parent = 0 },
      new() { Name = "lid", AnimIndex = 2, Parent = 0 },
    };
    var clip = Clip("unnamed_000", "door", "lid");

    Assert.True(ClipBinding.UsesAnimIndex(bones, clip));
    Assert.Equal(new[] { 2, 1, 3 }, ClipBinding.Resolve(bones, clip));

    var local = new Matrix4x4[bones.Count];
    ClipPose.Apply(bones, clip, 0, local);
    Assert.Equal(new Vector3(1, 2, 3), local[2].Translation);
    Assert.Equal(Vector3.Zero, local[0].Translation);
  }

  [Fact]
  public void CountMismatchFallsBackToNames()
  {
    // A character skeleton: the animated-object count differs from the clip's tracks, so names decide.
    var bones = new List<Bone>
    {
      new() { Name = "pelvis", AnimIndex = 0 },
      new() { Name = "spine", AnimIndex = 1, Parent = 0 },
      new() { Name = "head", AnimIndex = 2, Parent = 1 },
    };
    var clip = Clip("head", "pelvis");

    Assert.False(ClipBinding.UsesAnimIndex(bones, clip));
    Assert.Equal(new[] { 2, 0 }, ClipBinding.Resolve(bones, clip));
  }

  [Fact]
  public void ExplicitBoneIndexIsKept()
  {
    var bones = new List<Bone> { new() { Name = "a", AnimIndex = 1 }, new() { Name = "b", AnimIndex = 0 } };
    var clip = Clip("x", "y");
    clip.Tracks[0].Bone = 0;
    // Track 1 is rank 1 = animNmb 1 = bone "a"; track 0 keeps its explicit index.
    Assert.Equal(new[] { 0, 0 }, ClipBinding.Resolve(bones, clip));
  }

  [Fact]
  public void RootRotationRotatesAboutOriginThenTranslates()
  {
    // Root bone at (1,0,0) with no track; root motion R = 90 deg about Y, T = (0,0,5): p' = R p + T.
    var bones = new List<Bone> { new() { Name = "root", Local = Matrix4x4.CreateTranslation(1, 0, 0) }, new() { Name = "child", Parent = 0 } };
    var clip = Clip();
    clip.Tracks.Add(new BoneTrack { BoneName = "child" });
    clip.Tracks[0].Rotation.Add(new QuatKey { Time = 0, Value = Quaternion.Identity });
    var r = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    clip.RootRotation.Add(new QuatKey { Time = 0, Value = r });
    clip.RootTranslation.Add(new VectorKey { Time = 0, Value = new Vector3(0, 0, 5) });
    Assert.True(clip.HasRootRotation);

    var local = new Matrix4x4[bones.Count];
    ClipPose.Apply(bones, clip, 0, local);
    var expected = Vector3.Transform(new Vector3(1, 0, 0), r) + new Vector3(0, 0, 5);
    Assert.True(Vector3.Distance(expected, local[0].Translation) < 1e-5f, local[0].Translation.ToString());
    Assert.True(Quaternion.Dot(Quaternion.CreateFromRotationMatrix(local[0]), r) > 0.99999f);
    Assert.Equal(Vector3.Zero, local[1].Translation);
  }

  [Fact]
  public void IdentityRootRotationKeepsTranslationOnlyPath()
  {
    var clip = Clip();
    clip.RootRotation.Add(new QuatKey { Time = 0, Value = Quaternion.Identity });
    Assert.False(clip.HasRootRotation);
  }
}
