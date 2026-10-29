using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>R38: player fire clip per wpnType, decoded from primaris.nmb (tools/re/nmb_reader.py).</summary>
public class PlayerFireClipsTests
{
  [Theory]
  [InlineData(1.0, "tacticus_sht1_shoot_aim", "tacticus_sht16_shoot_blind", "HEURISTIC")]      // bolt rifle: aim default, blind else-branch
  [InlineData(1.03, "tacticus_sht1_shoot_aim", "tacticus_sht1_shoot_blind", "INFERRED")]
  [InlineData(1.1, "tacticus_sht2_shoot_aim", "tacticus_sht2_shoot_blind", "INFERRED")]      // stalker
  [InlineData(1.12, "tacticus_sht3_shoot_aim", "tacticus_sht3_shoot_blind", "INFERRED")]     // instigator
  [InlineData(1.4, "tacticus_sht5_shoot_aim", "tacticus_sht5_shoot_blind", "HEURISTIC")]     // melta shotgun (SubtractiveBlend unindexed, R40)
  [InlineData(1.25, "tacticus_sht15_shoot_aim", "tacticus_sht15_shoot_blind", "INFERRED")]   // pyroblaster
  [InlineData(1.51, "tacticus_sht16_shoot_aim", "tacticus_sht16_shoot_blind", "HEURISTIC")]  // combi melta (SimpleTypeConvert unindexed, R40)
  [InlineData(1.6, "tacticus_sht2_shoot_aim", "tacticus_sht2_shoot_blind", "INFERRED")]      // las fusil
  [InlineData(0.1, "tacticus_sht6_shoot_aim", "tacticus_sht6_shoot_blind", "INFERRED")]      // bolt pistol
  [InlineData(0.0, "tacticus_sht7_shoot_aim", "tacticus_sht7_shoot_blind", "INFERRED")]      // heavy bolt pistol
  [InlineData(0.4, "tacticus_sht14_shoot_aim", "tacticus_sht14_shoot_blind", "INFERRED")]    // volkite (index 3 clamps)
  [InlineData(2.0, "tacticus_sht1_shoot_aim", "tacticus_sht9_shoot_blind", "HEURISTIC")]     // heavy bolter
  [InlineData(2.21, "tacticus_sht1_shoot_aim", "tacticus_sht10_shoot_blind", "HEURISTIC")]   // pyrecannon floors to 2.2
  [InlineData(2.3, "tacticus_sht1_shoot_aim", "tacticus_sht13_shoot_blind", "HEURISTIC")]    // heavy melta
  public void ClipPerWpnType(double wpnType, string aim, string blind, string evidence)
  {
    var c = PlayerFireClips.For(wpnType);
    Assert.Equal(aim, c.Aim);
    Assert.Equal(blind, c.Blind);
    Assert.Equal(evidence, c.Evidence);
  }

  [Fact]
  public void PlasmaChargedAndAuxVision()
  {
    Assert.Equal("tacticus_sht7_shoot_aim", PlayerFireClips.For(0.2, plasmaCharged: true).Aim);
    Assert.Equal("tacticus_sht6_shoot_aim", PlayerFireClips.For(0.2).Aim);
    Assert.Equal("tacticus_sht2_shoot_blind", PlayerFireClips.For(1.3, plasmaCharged: true).Blind);
    Assert.Equal("tacticus_sht12_shoot_blind", PlayerFireClips.For(2.1, plasmaCharged: true).Blind);
    Assert.Equal("tacticus_sht11_shoot_blind", PlayerFireClips.For(2.1).Blind);
    Assert.Equal("tacticus_sht16_shoot_aim_melta", PlayerFireClips.For(1.51, auxVision: true).Aim);
  }

  [Fact]
  public void FireClipNames()
  {
    Assert.True(PlayerFireClips.IsFireClip("animation/primaris/tacticus/weapons/shoot/tacticus_sht16_shoot_aim_melta_intercessor.sani"));
    Assert.True(PlayerFireClips.IsFireClip("tacticus_sht9_shoot_blind"));
    Assert.Equal(13, PlayerFireClips.ShtIndex("tacticus_sht13_shoot_blind_intercessor"));
    Assert.False(PlayerFireClips.IsFireClip("tacticus_wpn1_blind_shooting_run"));
    Assert.False(PlayerFireClips.IsFireClip(null));
  }

  [Fact]
  public void AutoPlaysFiresOnPlayerClipAndFlagsWrongWeapon()
  {
    var anim = TplAnimation.From(PropAnimTests.Track())!;
    var plan = PropAnimStates.AutoPlays(anim, new AnimationClip { Name = "tacticus_sht2_shoot_aim_intercessor", Duration = 1f }, [], wpnType: 1.0);
    var shot = Assert.Single(plan.Plays, p => p.Seq.Name == "SHOOT_1");
    Assert.Contains("INFERRED", shot.Why);
    Assert.Contains(plan.Notes, n => n.Contains("tacticus_sht1_shoot_aim"));
    var ok = PropAnimStates.AutoPlays(anim, new AnimationClip { Name = "tacticus_sht16_shoot_blind_intercessor", Duration = 1f }, [], wpnType: 1.0);
    Assert.DoesNotContain(ok.Notes, n => n.Contains("the network picks"));
  }
}
