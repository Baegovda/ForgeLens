using System.Text;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Catalog;
using C = LibSaber.SM2.Assets.Animation.CharacterAnimations.ClassInfo;
using F = LibSaber.SM2.Assets.Catalog.AssetCategories.FileEntry;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>ui-43: Category view classifier. RULE: no visualizable file is unreachable.</summary>
public class AssetCategoriesTests
{
  private static readonly C[] Classes =
  {
    new("iactor_like", null, null, null, "iactor", "ssl/base/iactor_like.cls"),
    new("npc_client", null, null, null, "iactor", "ssl/npc/npc_client.cls"),
    new("npc_hormagaunt_client", "cc_hormagaunt", "hormagaunt", null, "npc_client", "ssl/characters/tyranids/npc_hormagaunt_client.cls"),
    new("player_character_client", null, null, null, "iactor", "ssl/pc/player_character_client.cls"),
    new("pc_marine_base_client", null, "primaris", null, "player_character_client", "ssl/pc/pc_marine_base_client.cls"),
    new("pc_titus", "cc_titus", null, "titus", "pc_marine_base_client", "ssl/characters/player/pc_titus.cls"),
    new("firearm_base", null, null, null, "iactor", "ssl/weapons/firearm_base.cls"),
    new("firearm_client", null, null, null, "firearm_base", "ssl/weapons/firearm_client.cls"),
    new("arifle_client", null, null, null, "firearm_client", "ssl/weapons/arifle_client.cls"),
    new("arifle_bolt_rifle_client", "wpn_bolt_rifle_01", null, null, "arifle_client", "ssl/weapons/arifle_bolt_rifle_client.cls"),
    new("npc_mystery_client", "cc_blob", null, null, "npc_client", "ssl/characters/misc/npc_mystery_client.cls"),
    new("door_base_client", "imp_door_01", null, null, "scene_actor_client", "ssl/level/door_base_client.cls"),
  };

  private static readonly F[] Files =
  {
    new(0, "tpl/cc_hormagaunt.tpl/cc_hormagaunt.tpl", ".tpl"),
    new(1, "tpl/cc_titus.tpl/cc_titus.tpl", ".tpl"),
    new(2, "tpl/wpn_bolt_rifle_01.tpl/wpn_bolt_rifle_01.tpl", ".tpl"),
    new(3, "tpl/refrt_crate_01.tpl/refrt_crate_01.tpl", ".tpl"),
    new(4, "tpl/zz_unknown.tpl/zz_unknown.tpl", ".tpl"),
    new(5, "tpl/cc_blob.tpl/cc_blob.tpl", ".tpl"),
    new(6, "tpl/imp_door_01.tpl/imp_door_01.tpl", ".tpl"),
    new(7, "animation/primaris/tacticus/loco/tacticus_run_fwd.sani", ".sani"),
    new(8, "animation/group_animations/tacticus_vs_hormagaunt/tacticus_vs_hormagaunt_finisher_01.sani", ".sani"),
    new(9, "animation/tyranids/hormagaunt/hormagaunt_idle.sani", ".sani"),
    new(10, "animation/cinematics/story_intro/shot_01/titus_shot.sani", ".sani"),
    new(11, "weird.sani", ".sani"),
    new(12, "ssl/sfx/hit/blood/hit_blood_01.sfx", ".sfx"),
    new(13, "fx_floating.sfx", ".sfx"),
    new(14, "pct/ch_titus_body_nm.pct.resource", ".pct.resource"),
    new(15, "textures/ui/hud/icon_bolter.pct.resource", ".pct.resource"),
    new(16, "pct/ch_titus_body.pct.resource", ".pct.resource"),
    new(17, "odd/thing.pct", ".pct"),
  };

  private static readonly Dictionary<string, IReadOnlySet<string>> Bundles = new()
  {
    ["primaris"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "animation/primaris/tacticus/loco/tacticus_run_fwd.sani" },
    ["hormagaunt"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "animation/tyranids/hormagaunt/hormagaunt_idle.sani" },
  };

  private static CategoryTree Tree() => AssetCategories.Build(Files, Classes, Bundles);

  private static bool In(CategoryTree tree, string key, int index)
    => tree.Find(key) is { } node && node.AllFiles.Contains(index);

  [Fact]
  public void Clip_owner_model_comes_from_the_bundle_class()
  {
    var tree = Tree();
    Assert.Equal(1, tree.OwnerModel(7)); // primaris bundle -> pc_marine_base_client <- pc_titus -> cc_titus
    Assert.Equal(0, tree.OwnerModel(9)); // hormagaunt bundle -> cc_hormagaunt
    Assert.Equal(-1, tree.OwnerModel(11));
  }

  [Fact]
  public void Every_file_is_in_All_and_in_a_kind_group_or_Other()
  {
    var tree = Tree();
    Assert.Equal("All", tree.Root.Children[0].Name);
    Assert.Equal(Files.Length, tree.All.TotalCount);
    var reached = new HashSet<int>();
    foreach (var top in tree.Root.Children.Where(c => c.Name != "All"))
      reached.UnionWith(top.AllFiles);
    Assert.All(Files, f => Assert.Contains(f.Index, reached));
  }

  [Fact]
  public void Reachability_holds_for_random_paths()
  {
    var rng = new Random(43);
    var exts = new[] { ".tpl", ".sani", ".sfx", ".pct", ".pct.resource" };
    var words = new[] { "a", "tpl", "animation", "ssl", "sfx", "pct", "scenes", "x_vs_y", "group_animations", "cc_", "refrt_", "lipsync", "cinematics", "interplay" };
    var files = new List<F>();
    for (var i = 0; i < 2000; i++)
    {
      var depth = rng.Next(0, 5);
      var parts = Enumerable.Range(0, depth).Select(_ => words[rng.Next(words.Length)] + rng.Next(3)).ToList();
      var ext = exts[rng.Next(exts.Length)];
      parts.Add(words[rng.Next(words.Length)] + "_" + i + ext);
      files.Add(new F(i, string.Join("/", parts), ext));
    }

    var tree = AssetCategories.Build(files, Classes, Bundles);
    var reached = new HashSet<int>();
    foreach (var top in tree.Root.Children.Where(c => c.Name != "All"))
      reached.UnionWith(top.AllFiles);
    Assert.Equal(files.Count, tree.All.TotalCount);
    Assert.Equal(files.Count, reached.Count);
  }

  [Fact]
  public void Models_follow_the_class_chain()
  {
    var tree = Tree();
    Assert.True(In(tree, "Models/Tyranids/NPC", 0));
    Assert.True(In(tree, "Models/Space Marine/Player", 1));
    Assert.False(tree.Find("Models/Space Marine/Player")!.Heuristic); // pc_marine chain = data
    Assert.True(In(tree, "Models/Weapons/Firearms/arifle", 2));
    Assert.True(In(tree, "Models/Props & environment/Scene actors", 6));
    Assert.True(In(tree, "Models/Other enemies", 5)); // npc without a faction keyword
    Assert.Contains("npc_hormagaunt_client", tree.Reason(0));
  }

  [Fact]
  public void Unclassed_templates_use_flagged_name_rules()
  {
    var tree = Tree();
    Assert.True(In(tree, "Models/Props & environment/Level geometry (refrt_)", 3));
    Assert.True(In(tree, "Models/Props & environment/Other (no class)", 4));
    Assert.True(tree.Find("Models/Props & environment/Level geometry (refrt_)")!.Heuristic);
    Assert.StartsWith("HEURISTIC", tree.Reason(3));
  }

  [Fact]
  public void Animations_group_by_bundle_sync_pair_folder_and_action()
  {
    var tree = Tree();
    Assert.True(In(tree, "Animations/By character (Morpheme bundle)/primaris · Space Marine", 7));
    Assert.True(In(tree, "Animations/By character (Morpheme bundle)/hormagaunt · Tyranids", 9));
    Assert.True(In(tree, "Animations/Synced kills/tacticus_vs_hormagaunt", 8));
    Assert.True(In(tree, "Animations/Cinematics/story_intro", 10));
    Assert.True(In(tree, "Animations/By action (heuristic)/Locomotion", 7));
    Assert.True(In(tree, "Animations/By action (heuristic)/Idles & poses", 9));
    Assert.True(tree.Find("Animations/By action (heuristic)")!.Heuristic);
    Assert.True(In(tree, "Animations", 11));
  }

  [Fact]
  public void Effects_and_textures_group()
  {
    var tree = Tree();
    Assert.True(In(tree, "Effects/hit/blood", 12));
    Assert.True(In(tree, "Effects/Other", 13));
    Assert.True(In(tree, "Textures/Normal maps", 14));
    Assert.True(In(tree, "Textures/UI", 15));
    Assert.True(In(tree, "Textures/Albedo / colour", 16));
    Assert.True(In(tree, "Textures", 17));
  }

  [Theory]
  [InlineData("tacticus_run_fwd", "Locomotion")]
  [InlineData("hormagaunt_death_back_01", "Deaths")]
  [InlineData("tacticus_reload_bolter", "Shooting")]
  [InlineData("tacticus_vs_lictor_finisher", "Synced kills & finishers")]
  [InlineData("zzz", null)]
  public void Action_groups_from_names(string stem, string? expected)
    => Assert.Equal(expected, AssetCategories.ActionGroup($"animation/x/{stem}.sani"));

  [Fact]
  public void Builds_from_a_catalog_with_classes_and_bundles()
  {
    using var dir = new TempDirectory();
    var cls = Encoding.UTF8.GetBytes("geom = { nameTpl = \"cc_hormagaunt\" }\nmorphemeController = { nameBundle = \"hormagaunt\" }\n__type = \"npc_client\"");
    var mr = Encoding.UTF8.GetBytes("{\"linksAnimation\":[\"res://animation/tyranids/hormagaunt/hormagaunt_idle.sani.resource\"]}");
    CacheFile.Create(dir.Path, "a.pak", new[]
    {
      new CacheFile.Entry("ssl/characters/tyranids/npc_hormagaunt_client.cls", cls),
      new CacheFile.Entry("morpheme/hormagaunt/hormagaunt.mr.resource", mr),
      new CacheFile.Entry("tpl/cc_hormagaunt.tpl/cc_hormagaunt.tpl", new byte[] { 1 }),
      new CacheFile.Entry("animation/tyranids/hormagaunt/hormagaunt_idle.sani", new byte[] { 1 }),
      new CacheFile.Entry("pct/rock.pct.resource", new byte[] { 1 }),
    });
    var catalog = GameCatalog.Load(dir.Path);
    using var store = new ArchiveStore(catalog);
    var tree = AssetCategories.Build(catalog, store);
    Assert.Equal(3, tree.All.TotalCount); // .cls / .mr.resource are not visualizable
    var tpl = catalog.FindByPath("tpl/cc_hormagaunt.tpl/cc_hormagaunt.tpl")[0];
    var clip = catalog.FindByPath("animation/tyranids/hormagaunt/hormagaunt_idle.sani")[0];
    Assert.True(In(tree, "Models/Tyranids/NPC", tpl));
    Assert.True(In(tree, "Animations/By character (Morpheme bundle)/hormagaunt · Tyranids", clip));
    Assert.Same(tree, AssetCategories.Build(catalog, store)); // cached per catalog
  }
}
