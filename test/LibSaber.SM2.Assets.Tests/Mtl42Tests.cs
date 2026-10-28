using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Export;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-42: spark alpha fallback, cloth sim shells, export In place, previewable file list.</summary>
public class Mtl42Tests
{
  [Fact]
  public void AlphaCoverage_OpaqueGlowHasNone_CutoutHasSome()
  {
    var opaque = new byte[4 * 100];
    for (var i = 0; i < 100; i++) opaque[i * 4 + 3] = 254; // part_rs_glow_01: alpha 254..255 everywhere
    Assert.False(VfxDrawKinds.HasAlphaCoverage(opaque));
    var cut = (byte[])opaque.Clone();
    cut[3] = 0;
    cut[7] = 10;
    Assert.True(VfxDrawKinds.HasAlphaCoverage(cut));
  }

  [Fact]
  public void Resolve_AlphaLessSparkDrawsAsParticle_OthersUnchanged()
  {
    Assert.Equal(VfxDrawKind.CpuParticle, VfxDrawKinds.Resolve(VfxDrawKind.Spark, false));
    Assert.Equal(VfxDrawKind.Spark, VfxDrawKinds.Resolve(VfxDrawKind.Spark, true));
    Assert.Equal(VfxDrawKind.Spark, VfxDrawKinds.Resolve(VfxDrawKind.Spark, null)); // texture unknown yet
    Assert.Equal(VfxDrawKind.Trail, VfxDrawKinds.Resolve(VfxDrawKind.Trail, false));
  }

  [Theory]
  [InlineData("tbrd_01_frnt_SIM_02", true)]
  [InlineData("cloth_stamp_leg_SIM", true)]
  [InlineData("tbrd_01_frnt_REND", false)]
  [InlineData("SIMPLE_belt", false)]
  public void ClothSimNames(string name, bool sim) => Assert.Equal(sim, AssetExporter.IsClothSim(name));

  [Theory]
  [InlineData(".tpl", true)]
  [InlineData(".pct.resource", true)]
  [InlineData(".sani", true)]
  [InlineData(".sfx", true)]
  [InlineData(".tpl_data", false)]
  [InlineData(".tpl_markup", false)]
  [InlineData(".lods_base", false)]
  [InlineData(".tpl.resource", false)]
  [InlineData(".geom_dbg", false)]
  [InlineData(".pct_mip", false)]
  public void PreviewableExtensions(string ext, bool shown) => Assert.Equal(shown, AssetClassification.IsPreviewable(ext));

  private static List<Bone> Skeleton()
    => new()
    {
      new Bone { Name = "ROOT", Parent = -1 },
      new Bone { Name = "CENTRE", Parent = 0, Local = Matrix4x4.CreateTranslation(0, 1, 0) },
    };

  private static AnimationClip Walk()
  {
    // ROOT walks 3 m along +Z and rises 0.5 m.
    var clip = new AnimationClip { Name = "walk", Duration = 1 };
    var track = new BoneTrack { BoneName = "ROOT" };
    track.Translation.Add(new VectorKey { Time = 0, Value = Vector3.Zero });
    track.Translation.Add(new VectorKey { Time = 1, Value = new Vector3(0, 0.5f, 3) });
    clip.Tracks.Add(track);
    return clip;
  }

  [Fact]
  public void InPlace_RemovesHorizontalTravel_KeepsHeight_RootMotionKeepsAll()
  {
    var bones = Skeleton();
    var local = new Matrix4x4[bones.Count];
    var clip = Walk();
    ClipPose.Apply(bones, clip, 1, local);
    Assert.Equal(3f, local[0].M43, 1e-3f);

    clip = Walk();
    clip.InPlace = true;
    ClipPose.Apply(bones, clip, 1, local);
    Assert.Equal(0f, local[0].M43, 1e-3f);
    Assert.Equal(0f, local[0].M41, 1e-3f);
    Assert.Equal(0.5f, local[0].M42, 1e-3f);
  }
}
