using LibSaber.SM2.Assets.Animation;

namespace LibSaber.SM2.Assets.Tests;

public class CharacterAnimationsTests
{
  [Theory]
  [InlineData("animation/cinematics/titus/cin_titus_intro.sani", true)]
  [InlineData("animation/story/xscene_titus_01.sani", true)]
  [InlineData("animation/primaris/tacticus/lipsync/titus_story_01.sani", false)]
  [InlineData("animation/interplay/story_titus_door.sani", false)]
  [InlineData("animation/primaris/tacticus/run_fwd.sani", false)]
  public void Cinematic_marks(string path, bool expected)
    => Assert.Equal(expected, CharacterAnimations.IsCinematic(path));

  [Fact]
  public void Names_character_by_whole_token()
  {
    var tokens = new[] { "titus" };
    Assert.True(CharacterAnimations.NamesCharacter("animation/cinematics/cin_titus_intro.sani", tokens));
    Assert.False(CharacterAnimations.NamesCharacter("animation/cinematics/cin_titusx_intro.sani", tokens));
  }

  [Fact]
  public void Ranks_main_then_cinematic_then_other()
  {
    var main = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "animation/primaris/tacticus/run_fwd.sani" };
    var result = new CharacterAnimations.Result(main, new[] { "titus" }, new[] { "primaris" }, Array.Empty<string>(), "test");
    Assert.Equal(ClipRank.Main, result.Rank("animation/primaris/tacticus/run_fwd.sani"));
    Assert.Equal(ClipRank.Cinematic, result.Rank("animation/cinematics/cin_titus_intro.sani"));
    Assert.Equal(ClipRank.Other, result.Rank("animation/primaris/tacticus/lipsync/titus_01.sani"));
    Assert.Equal(ClipRank.Other, result.Rank("animation/chaos/marine/run.sani"));
  }

  [Fact]
  public void Falls_back_to_name_tokens_without_a_bundle()
  {
    var result = new CharacterAnimations.Result(new HashSet<string>(), new[] { "calgar" }, Array.Empty<string>(), Array.Empty<string>(), "names");
    Assert.Equal(ClipRank.Main, result.Rank("animation/calgar/calgar_idle.sani"));
    Assert.Equal(ClipRank.Other, result.Rank("animation/calgar/lipsync/calgar_vo.sani"));
  }
}
