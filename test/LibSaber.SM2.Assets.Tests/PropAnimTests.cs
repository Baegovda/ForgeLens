using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Effects;
using LibSaber.SM2.Assets.Meshes;
using LibSaber.Shared.Structures;
using LibSaber.SpaceMarine2.Structures;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>propanim: tpl-embedded track decode ([frame, values] keys), sequence clips, composition, blueprint anim triggers, Auto.</summary>
public class PropAnimTests
{
  private static SplineData Data(SplineType type, int dim, params float[] keys) => new()
  {
    SplineType = type, Unk_02 = (byte)dim, Unk_03 = (byte)dim, Count = (uint)(keys.Length / (dim + 1)), SizeInBytes = (uint)(keys.Length * 4), Data = keys,
  };

  /// <summary>Object 0 slides on X over frames 0..4 (SHOOT_1) and hides at frame 11 (HIDE); object 1 turns about Z over 20..30 (chain_anim).</summary>
  internal static animTRACK Track()
  {
    // File order w, x, y, z of the conjugate: a +90° turn about Z is stored as (cos45, 0, 0, -sin45).
    var s = MathF.Sqrt(0.5f);
    return new animTRACK
    {
      seqList =
      [
        new animSEQ { name = "SHOOT_1", startFrame = 0, endFrame = 4, timeSec = 4 / 30f },
        new animSEQ { name = "HIDE", startFrame = 10, endFrame = 12, timeSec = 2 / 30f },
        new animSEQ { name = "chain_anim", startFrame = 20, endFrame = 30, timeSec = 0.5f },
      ],
      objAnimList =
      [
        new animOBJ_ANIM
        {
          pTranslation = new m3dSPL_Linear3D(Data(SplineType.Linear3D, 3, 0, 0, 0, 0, 2, -1, 0, 0, 4, 0, 0, 0)),
          pVisibility = new m3dSPL_Linear1D(Data(SplineType.Linear1D, 1, 0, 1, 10, 1, 11, 0, 12, 1)),
        },
        new animOBJ_ANIM
        {
          pRotation = new m3dSPL_Quat(Data(SplineType.Quat, 4, 20, 1, 0, 0, 0, 30, s, 0, 0, -s)),
          iniTranslation = new Vector3(0, 0, 5), HasIniTranslation = true,
        },
      ],
    };
  }

  private static List<Bone> Bones() =>
  [
    new() { Name = "root", Parent = -1 },
    new() { Name = "slide", Parent = 0, AnimIndex = 0 },
    new() { Name = "teeth_root", Parent = 0 },
    new() { Name = "tooth", Parent = 2, AnimIndex = 1, Local = Matrix4x4.CreateTranslation(0, 0, 5) },
  ];

  [Fact]
  public void SplineKeysAreFramePlusValues()
  {
    var spl = new m3dSPL_Linear3D(Data(SplineType.Linear3D, 3, 0, 1, 2, 3, 5, 4, 5, 6));
    Assert.True(spl.HasFrameKeys);
    Assert.Equal(4, spl.Stride);
    Assert.Equal(2, spl.KeyCount);
    Assert.Equal(5f, spl.KeyFrame(1));
    Assert.Equal(6f, spl.KeyValue(1, 2));
    Assert.False(new m3dSPL_Linear3D(Data(SplineType.Linear3D, 3, 0, 1, 2)).HasFrameKeys);
  }

  [Fact]
  public void DecodesChannelsSequencesAndConjugateQuaternion()
  {
    var anim = TplAnimation.From(Track())!;
    Assert.Equal(3, anim.Sequences.Count);
    Assert.Equal(30f, anim.Find("SHOOT_1")!.Fps, 0.001f);
    Assert.Equal(20f, anim.Find("chain_anim")!.Fps, 0.001f);
    var slide = anim.Objects[0];
    Assert.Equal(-0.5f, slide.TranslationAt(1)!.Value.X, 0.0001f);
    Assert.Equal(0f, slide.VisibilityAt(11)!.Value);
    var q = anim.Objects[1].RotationAt(30)!.Value;
    Assert.Equal(1f, Vector3.Transform(Vector3.UnitX, q).Y, 0.0001f); // conjugated: +90° about Z
    Assert.Equal(new Vector3(0, 0, 5), anim.Objects[1].TranslationAt(25));
    Assert.True(anim.Moves(slide, anim.Find("SHOOT_1")!));
    Assert.False(anim.Moves(anim.Objects[1], anim.Find("SHOOT_1")!));
    Assert.True(anim.Moves(anim.Objects[1], anim.Find("chain_anim")!));
  }

  [Fact]
  public void EnglishLabelsAndConditionOrder()
  {
    Assert.Equal("Shoot 1", PropAnimStates.Label("SHOOT_1"));
    Assert.Equal("Teeth spin 3", PropAnimStates.Label("chain_anim3"));
    Assert.Equal("Aux shot (augmented aim)", PropAnimStates.Label("SHOOT_AUX"));
    Assert.Equal("", PropAnimStates.Label("whatever"));
    var conds = PropAnimStates.Conditions(TplAnimation.From(Track()));
    Assert.True(conds[0].IsAuto);
    Assert.Equal("SHOOT_1", conds[1].Name);
    Assert.Equal("SHOOT_1 · Shoot 1", conds[1].Display);
    Assert.Equal("HIDE", conds.Single(c => c.Name == "HIDE").Display);
  }

  [Fact]
  public void SequenceClipBindsBonesAndHidesWithZeroScale()
  {
    var anim = TplAnimation.From(Track())!;
    var bones = Bones();
    var map = anim.BoneMap(bones);
    Assert.Equal(1, map[0]);
    Assert.Equal(3, map[1]);
    var clip = anim.ToClip(anim.Find("HIDE")!, bones);
    var slide = clip.Tracks.Single(t => t.Bone == 1);
    Assert.Contains(slide.Scale, k => k.Value == Vector3.Zero);
    Assert.Equal(2 / 30f, clip.Duration, 0.0001f);
    var shot = anim.ToClip(anim.Find("SHOOT_1")!, bones, boneOffset: 10);
    Assert.Contains(shot.Tracks, t => t.Bone == 11);
  }

  [Fact]
  public void ComposeLatestMovingPlayWinsAndMaskLimits()
  {
    var anim = TplAnimation.From(Track())!;
    var bones = Bones();
    var plays = new List<TplPlay>
    {
      new(anim.Find("chain_anim")!, 0, Loop: true),
      new(anim.Find("SHOOT_1")!, 0.5f),
    };
    var clip = anim.Compose(plays, 1f, bones);
    var slide = clip.Tracks.Single(t => t.Bone == 1);
    var tooth = clip.Tracks.Single(t => t.Bone == 3);
    var mid = slide.Translation.OrderBy(k => MathF.Abs(k.Time - (0.5f + 2 / 30f))).First();
    Assert.Equal(-1f, mid.Value.X, 0.01f);
    Assert.True(tooth.Rotation.Count > 2);

    var masks = PropAnimStates.Masks(anim, bones, [new PropAnimLayer("teeth", "MainMultiStitch", ["chain_anim"], ["teeth_root"])]);
    Assert.Equal(new[] { 1 }, masks["chain_anim"].ToArray());
    var masked = anim.Compose([PropAnimStates.Single(anim.Find("chain_anim")!, masks)], 0.5f, bones);
    Assert.DoesNotContain(masked.Tracks, t => t.Bone == 1 && t.Translation.Count > 0);
  }

  private const string Blueprint = """
    nodes   =   {
       OnChangeHolding   =   {
          OnHoldInArms   =   [
             "StartTeeth/Input"
          ]
          __type   =   "BlueprintNodeMeleeWeaponOnChangeHolding"
       }
       StartTeeth   =   {
          animLink   =   {
             def   =   "chain_anim"
             __type   =   "AnimStringRefParam"
          }
          typePlayback   =   {
             def   =   "cycle"
          }
          __type   =   "BlueprintAnimationLibraryStartanim"
       }
       Hit   =   {
          mrEvent   =   {
             name   =   "hit1_start_anim"
             __type   =   "MorphemeEvent"
          }
          OnEmitEvent   =   [
             "Pipe/Input"
          ]
          __type   =   "BlueprintNodeEquipmentUserMorphemeEvent"
       }
       Pipe   =   {
          Output   =   [
             "Shot/Input"
          ]
          __type   =   "BlueprintLibraryPipe"
       }
       Shot   =   {
          animLink   =   {
             def   =   "SHOOT_1"
             __type   =   "AnimStringRefParam"
          }
          __type   =   "BlueprintAnimationLibraryStartanim"
       }
    }
    """;

  [Fact]
  public void BlueprintStartanimTriggersDriveAuto()
  {
    Assert.True(AnimEventVfxMap.Relevant(Blueprint));
    var map = new AnimEventVfxMap();
    map.AddBlueprint("bp_test", Blueprint);
    Assert.Contains(map.Anims, a => a.Trigger.EndsWith(".OnHoldInArms") && a.Anim == "chain_anim" && a.Loops);
    Assert.Contains(map.Anims, a => a.Trigger == "hit1_start_anim" && a.Anim == "SHOOT_1" && !a.Stop);

    var anim = TplAnimation.From(Track())!;
    var swing = new AnimationClip { Name = "swing", Duration = 2f };
    swing.EventTracks.Add(new AnimEventTrack("vfx", [new AnimEvent(0.25f, 0, 0, ["hit1_start_anim"])]));
    var plan = PropAnimStates.AutoPlays(anim, swing, map.Anims);
    Assert.Contains(plan.Plays, p => p.Seq.Name == "chain_anim" && p.Loop);
    Assert.Contains(plan.Plays, p => p.Seq.Name == "SHOOT_1" && MathF.Abs(p.Start - 0.5f) < 1e-3f);
  }

  [Fact]
  public void FirearmShootTrackFiresShootAnimation()
  {
    var anim = TplAnimation.From(Track())!;
    var gun = new AnimationClip { Name = "bolter_shoot", Duration = 1f };
    gun.EventTracks.Add(new AnimEventTrack("Shoot", [new AnimEvent(0.1f, 0, 0, []), new AnimEvent(0.6f, 0, 0, [])]));
    var plan = PropAnimStates.AutoPlays(anim, gun, []);
    Assert.Equal(2, plan.Plays.Count(p => p.Seq.Name == "SHOOT_1"));
  }

  [Fact]
  public void LegacyTimelineUsesSameDecoder()
  {
    var anim = TplAnimation.From(Track())!;
    Assert.Equal(30f, anim.LastFrame);
    var clip = anim.Timeline("legacy");
    Assert.Equal(1f, clip.Duration, 0.001f);
    Assert.NotEmpty(clip.Tracks);
  }

  [Fact]
  public void SequenceClipsForExport()
  {
    var anim = TplAnimation.From(Track())!;
    var missing = new List<string>();
    var clips = PropAnimStates.SequenceClips(anim, "seq:SHOOT_1,nope", "wpn_test", missing);
    Assert.Equal("SHOOT_1", Assert.Single(clips).Name);
    Assert.Equal(["nope"], missing);
    Assert.Equal(3, PropAnimStates.SequenceClips(anim, "seq:all", "wpn_test").Count);
  }

  [Fact]
  public void FiringClipWithoutShootTrackLoopsShootAnimation()
  {
    var anim = TplAnimation.From(Track())!;
    var plan = PropAnimStates.AutoPlays(anim, new AnimationClip { Name = "tacticus_wpn1_blind_shooting_run", Duration = 1f }, []);
    Assert.Contains(plan.Plays, p => p.Seq.Name == "SHOOT_1" && p.Loop);
    var reload = PropAnimStates.AutoPlays(anim, new AnimationClip { Name = "tacticus_wpn10_reload", Duration = 1f }, []);
    Assert.DoesNotContain(reload.Plays, p => p.Seq.Name == "SHOOT_1");
  }

  private static animTRACK ReloadTrack()
  {
    var t = Track();
    t.seqList.Add(new animSEQ { name = "RELOAD_START", startFrame = 0, endFrame = 4, timeSec = 4 / 30f });
    t.seqList.Add(new animSEQ { name = "RELOAD_END", startFrame = 10, endFrame = 12, timeSec = 2 / 30f });
    return t;
  }

  [Fact]
  public void ReloadIsTheReloadSoundSeqTrackNotTheClipName()
  {
    var anim = TplAnimation.From(ReloadTrack())!;
    var clip = new AnimationClip { Name = "some_clip", Duration = 2f };
    clip.EventTracks.Add(new AnimEventTrack("reload_sound_seq",
      [new AnimEvent(0.2f, 0.3f, 0, ["snd_reload_mag_out"]), new AnimEvent(0.5f, 0.2f, 0, ["snd_reload_mag_in"])]));
    var plan = PropAnimStates.AutoPlays(anim, clip, []);
    Assert.Contains(plan.Plays, p => p.Seq.Name == "RELOAD_START" && p.Start == 0f);
    Assert.Contains(plan.Plays, p => p.Seq.Name == "RELOAD_END" && MathF.Abs(p.Start - 1f) < 1e-4f);
    var named = PropAnimStates.AutoPlays(anim, new AnimationClip { Name = "tacticus_wpn10_reload", Duration = 2f }, []);
    Assert.DoesNotContain(named.Plays, p => p.Seq.Name.StartsWith("RELOAD_"));
  }

  [Fact]
  public void FiringClipUsesShotsPerMin()
  {
    var anim = TplAnimation.From(Track())!;
    var rates = new[] { new FirearmFireRate("arifle_bolt_rifle", "auto", 300, false, "autofire", 0.9, null) };
    var plan = PropAnimStates.AutoPlays(anim, new AnimationClip { Name = "x_shooting", Duration = 1f }, [], null, null, rates);
    var shots = plan.Plays.Where(p => p.Seq.Name == "SHOOT_1").ToList();
    Assert.Equal(5, shots.Count);
    Assert.All(shots, p => Assert.False(p.Loop));
    Assert.Equal(0.6f, shots[3].Start, 1e-4f);
    Assert.Equal(0.8f, shots[3].End, 1e-4f);
  }

  [Fact]
  public void FirearmLibraryReadsBarrelFireRates()
  {
    const string text = """
firearms   =   {
   arifle_bolt_rifle   =   {
      barrelDescriptions   =   {
         auto   =   {
            shootSettings   =   {
               fireRate   =   {
                  shotsPerMin   =   280
                  __type   =   "FixedFireRate"
               }
               fireRateAffectShootAnim   =   False
               shootLogicUid   =   "autofire"
               __type   =   "ShootSettings"
            }
         }
      }
      reloadSettings   =   {
         reloadAnimSpeedScale   =   0.9
         __type   =   "ReloadSettings"
      }
   }
}
""";
    var r = Assert.Single(FirearmLibrary.Parse(text)["arifle_bolt_rifle"]);
    Assert.Equal(280, r.ShotsPerMin);
    Assert.False(r.AffectsShootAnim);
    Assert.Equal("autofire", r.ShootLogic);
    Assert.Equal(0.9, r.ReloadAnimSpeedScale!.Value, 1e-9);
    Assert.Equal(60.0 / 280, r.Interval, 1e-9);
    Assert.Equal("arifle_bolt_rifle", FirearmLibrary.UidOf("arifle_bolt_rifle_client"));
  }
}
