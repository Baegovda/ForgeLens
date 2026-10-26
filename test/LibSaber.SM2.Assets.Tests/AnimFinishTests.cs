using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

public class AnimFinishTests
{
  private static List<Bone> Skeleton()
    => new()
    {
      new Bone { Name = "Object00C9", Parent = -1 },
      new Bone { Name = "ROOT", Parent = 0 },
      new Bone { Name = "CENTRE", Parent = 1, Local = Matrix4x4.CreateTranslation(0, 1, 0) },
    };

  private static AnimationClip LevelSpaceClip()
  {
    // ROOT starts 4706 m away, 420 m up, turned 90 deg, then moves 2 m along its own heading (world -X after the turn).
    var clip = new AnimationClip { Name = "shot", Duration = 1 };
    clip.Bones.AddRange(new[] { "ROOT" });
    var track = new BoneTrack { BoneName = "ROOT" };
    var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
    track.Rotation.Add(new QuatKey { Time = 0, Value = turn });
    track.Rotation.Add(new QuatKey { Time = 1, Value = turn });
    track.Translation.Add(new VectorKey { Time = 0, Value = new Vector3(-4706, 420, -1662) });
    track.Translation.Add(new VectorKey { Time = 1, Value = new Vector3(-4706, 420, -1662) + Vector3.Transform(new Vector3(0, 0, 2), turn) });
    clip.Tracks.Add(track);
    return clip;
  }

  private static Matrix4x4 World(IReadOnlyList<Bone> bones, Matrix4x4[] local, int i)
    => bones[i].Parent < 0 ? local[i] : local[i] * World(bones, local, bones[i].Parent);

  [Fact]
  public void Rebase_MovesFirstFrameToBind_AndKeepsTheMotion()
  {
    var bones = Skeleton();
    var clip = LevelSpaceClip();
    clip.RebaseFirstFrame = true;
    var local = new Matrix4x4[bones.Count];

    ClipPose.Apply(bones, clip, 0, local);
    var w0 = World(bones, local, 1);
    Assert.True(w0.Translation.Length() < 1e-2f, w0.Translation.ToString());
    Assert.True(Math.Abs(w0.M33 - 1) < 1e-4f && Math.Abs(w0.M31) < 1e-4f, "heading back to +Z");
    Assert.InRange(clip.RebaseYawDegrees, 89.9f, 90.1f);
    Assert.InRange(clip.RebaseOffset.X, -4706.01f, -4705.99f);

    ClipPose.Apply(bones, clip, 1, local);
    var w1 = World(bones, local, 1);
    Assert.True(Vector3.Distance(w1.Translation, new Vector3(0, 0, 2)) < 1e-2f, w1.Translation.ToString()); // forward 2 m
  }

  [Fact]
  public void Rebase_IsANoOpForClipsThatStartAtBind()
  {
    var bones = Skeleton();
    var clip = new AnimationClip { Name = "run", Duration = 1 };
    var track = new BoneTrack { BoneName = "ROOT" };
    track.Translation.Add(new VectorKey { Time = 0, Value = Vector3.Zero });
    track.Translation.Add(new VectorKey { Time = 1, Value = new Vector3(0, 0, 4.86f) });
    clip.Tracks.Add(track);
    clip.RebaseFirstFrame = true;
    var local = new Matrix4x4[bones.Count];
    ClipPose.Apply(bones, clip, 1, local);
    Assert.Equal(Vector3.Zero, clip.RebaseOffset);
    Assert.Equal(0f, clip.RebaseYawDegrees);
    Assert.Equal(new Vector3(0, 0, 4.86f), World(bones, local, 1).Translation);
  }

  [Fact]
  public void Rebase_UsesCentreWhenTheShotPositionIsOnThePelvis()
  {
    var bones = Skeleton();
    var clip = new AnimationClip { Name = "cinematic", Duration = 1 };
    var track = new BoneTrack { BoneName = "CENTRE" };
    track.Translation.Add(new VectorKey { Time = 0, Value = new Vector3(-4706, 421, -1662) });
    track.Translation.Add(new VectorKey { Time = 1, Value = new Vector3(-4706, 421, -1660) });
    clip.Tracks.Add(track);
    clip.RebaseFirstFrame = true;
    var local = new Matrix4x4[bones.Count];
    ClipPose.Apply(bones, clip, 0, local);
    Assert.True(Vector3.Distance(World(bones, local, 2).Translation, new Vector3(0, 1, 0)) < 1e-2f);
    ClipPose.Apply(bones, clip, 1, local);
    Assert.True(Vector3.Distance(World(bones, local, 2).Translation, new Vector3(0, 1, 2)) < 1e-2f);
    Assert.InRange(clip.RebaseOffset.Y, 419.99f, 420.01f);
    Assert.True(World(bones, local, 1).Translation.Length() < 1e-3f, "ROOT stays at its bind; only its children move");

    // A crouch (pelvis 0.5 m down) is a pose, not a placement.
    var crouch = new AnimationClip { Name = "crouch", Duration = 1, RebaseFirstFrame = true };
    var c = new BoneTrack { BoneName = "CENTRE" };
    c.Translation.Add(new VectorKey { Time = 0, Value = new Vector3(0, 0.5f, 0) });
    crouch.Tracks.Add(c);
    ClipPose.Apply(bones, crouch, 0, local);
    Assert.Equal(Vector3.Zero, crouch.RebaseOffset);
  }

  [Fact]
  public void AnimTokens_SplitListsAndGlob()
  {
    Assert.Equal(new[] { "a", "b c", "d" }, ClipResolver.SplitTokens("a; b c ,d"));
    var headers = new List<SaniIndex.Header>
    {
      new(1, "animation/primaris/assault/assault_run_fwd.sani", 1, 30, Array.Empty<string>()),
      new(2, "animation/primaris/assault/assault_walk_fwd.sani", 1, 30, Array.Empty<string>()),
      new(3, "animation/primaris/tacticus/tacticus_run_fwd.sani", 1, 30, Array.Empty<string>()),
    };
    Assert.Equal(new[] { 1, 3 }, SaniIndex.Match(headers, "animation/primaris/*_run_*").Select(h => h.Index));
    Assert.Equal(new[] { 1 }, SaniIndex.Match(headers, "*/assault/*run*").Select(h => h.Index));
    Assert.Equal(new[] { 2 }, SaniIndex.Match(headers, "walk").Select(h => h.Index));
  }
}
