using LibSaber.SM2.Assets.Catalog;

namespace LibSaber.SM2.Assets.Tests;

public class SearchWordsTests
{
  [Theory]
  [InlineData("ssl/sfx/weapon/bolter/fx_bolter_muzzle_client.sfx", new[] { "bolter", "muzzle" }, true)]
  [InlineData("ssl/sfx/weapon/bolter/fx_bolter_muzzle_client.sfx", new[] { "MUZZLE", "Bolter" }, true)]
  [InlineData("ssl/sfx/weapon/bolter/fx_bolter_muzzle_client.sfx", new[] { "bolter", "grenade" }, false)]
  [InlineData("tpl/cc_titus.tpl", new string[0], true)]
  public void AllWordsMustMatch(string path, string[] words, bool expected)
    => Assert.Equal(expected, GameCatalog.MatchesAll(path, words));
}
