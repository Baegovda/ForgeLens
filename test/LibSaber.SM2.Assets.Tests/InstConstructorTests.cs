using LibSaber.SM2.Assets.Customization;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>ui-45 enemy / NPC Kitbash (inst_constructor templates) and melee region skins.</summary>
public class InstConstructorTests
{
  // Shapes of ssl/inst_constructor/inst_constructor_templates.sso and inst_constructor_mesh_templates.ssolib (text SSL).
  private const string Templates = """
templates   =   {
   Cadian_Shock_Trooper   =   {
      bodyLayers   =   {
         helmet   =   {
            templateVariants   =   [
               {
                  template   =   { __type   =   "InstConstructorMeshTemplatesCadianShockTrooperHelmet01" }
                  __type   =   "InstConstructorMeshTemplateVariant"
               },
               {
                  probability   =   0.2
                  template   =   { __type   =   "InstConstructorMeshTemplatesEmpty" }
                  __type   =   "InstConstructorMeshTemplateVariant"
               }
            ]
         }
         head   =   {
            requiredLayer   =   "helmet"
            templateVariants   =   [
               { template   =   { __type   =   "InstConstructorMeshTemplatesCadianShockTrooperHead01" } }
            ]
         }
         headWithHair   =   {
            disabledByLayer   =   "head"
            templateVariants   =   [
               { template   =   { __type   =   "InstConstructorMeshTemplatesCadianShockTrooperHead02" } }
            ]
         }
         body   =   {
            templateVariants   =   [
               { template   =   { __type   =   "InstConstructorMeshTemplatesCadianShockTrooperBody01" } },
               { template   =   { __type   =   "InstConstructorMeshTemplatesCadianShockTrooperBody02" } }
            ]
         }
      }
   }
   Cultist   =   {
      bodyLayers   =   {
         Head   =   {
            templateVariants   =   [
               { template   =   { __type   =   "InstConstructorMeshTemplatesCultistHead01" } }
            ]
         }
      }
   }
   Termagaunt_01   =   {
      bodyLayers   =   {
         Body   =   {
            templateVariants   =   [
               { template   =   { __type   =   "InstConstructorMeshTemplatesNoSuchThing" } }
            ]
         }
      }
   }
}
""";

  private const string MeshTemplates = """
classes   =   {
   EMPTY   =   { sslModule   =   "x" }
   Cadian_Shock_Trooper_Helmet_01   =   { geomObjNames   =   [ "helmet_01" ] }
   Cadian_Shock_Trooper_Head_01   =   { geomObjNames   =   [ "head_01" ] }
   Cadian_Shock_Trooper_Head_02   =   { geomObjNames   =   [ "head_02" ] }
   Cadian_Shock_Trooper_Body_01   =   { geomObjNames   =   [ "torso_01" ] }
   Cadian_Shock_Trooper_Body_02   =   { geomObjNames   =   [ "torso_02" ] }
   Cultist_Head_01   =   { geomObjNames   =   [ "cultist_head_01" ] }
}
""";

  private const string CadianCls = """
properties   =   {
   geom   =   { nameTpl   =   "cc_cadian_shock_trooper" }
   prop_inst_constructor_project   =   { fixedTemplateId   =   "Cadian_Shock_Trooper" }
   prop_inst_constructor_custom_preset   =   {
      customPreset   =   {
         bodyLayers   =   {
            InstConstructorMeshPreset   =   { geomObjNames   =   [ "helmet_grp", "torso_01" ] }
         }
      }
   }
}
""";

  private static InstConstructorLibrary Lib()
  {
    var lib = InstConstructorLibrary.FromText(Templates, MeshTemplates);
    lib.AddClass("ssl/x/librarium_character_cadian.cls", CadianCls);
    var tpls = new[] { "cc_cadian_shock_trooper", "cc_constructor_cultist", "cc_termagant" };
    lib.MapModels(t => tpls.Contains(t), tpls);
    return lib;
  }

  /// <summary>helmet_grp → helmet_01 (+LOD1 sibling), head_01, head_02, torso_01, torso_02, caps (no template).</summary>
  private static ExportedMesh Cadian()
  {
    var mesh = new ExportedMesh { Name = "cc_cadian_shock_trooper" };
    void Add(string name, int parent, bool part = true)
    {
      mesh.Bones.Add(new Bone { Name = name, SourceName = name, Parent = parent });
      if (part)
        mesh.Parts.Add(new MeshPart { Name = name, Node = mesh.Bones.Count - 1, IndexCount = 3 });
    }

    Add("root", -1, part: false);   // 0
    Add("helmet_grp", 0, false);    // 1
    Add("helmet_01", 1);            // 2
    Add("helmet_01_LOD1", 1);       // 3 sibling LOD
    Add("head_01", 0);              // 4
    Add("head_02", 0);              // 5
    Add("torso_01", 0);             // 6
    Add("torso_02", 0);             // 7
    Add("cadian_caps", 0);          // 8
    return mesh;
  }

  private static HashSet<string> Visible(ExportedMesh mesh) => mesh.Parts.Where(p => p.Visible).Select(p => p.Name).ToHashSet();

  [Fact]
  public void Links_resolve_case_and_underscore_insensitive()
  {
    var lib = Lib();
    var cadian = lib.Template("Cadian_Shock_Trooper")!;
    Assert.Equal(new[] { "helmet", "head", "headWithHair", "body" }, cadian.Layers.Select(l => l.Name));
    Assert.Equal("Cadian_Shock_Trooper_Helmet_01", cadian.Layers[0].Variants[0].Key);
    Assert.True(cadian.Layers[0].Variants[1].IsEmpty); // EMPTY has no geomObjNames
    Assert.Null(lib.Template("Termagaunt_01")!.Layers[0].Variants[0].Mesh);
    Assert.Contains(lib.Notes, n => n.Contains("1 template link"));
  }

  [Fact]
  public void Probabilities_default_to_weight_one()
  {
    var helmet = Lib().Template("Cadian_Shock_Trooper")!.Layers[0];
    Assert.Null(helmet.Variants[0].Probability);
    Assert.Equal(1.0, helmet.Variants[0].Weight);
    Assert.Equal(1 / 1.2, helmet.Chance(helmet.Variants[0]), 6);
    Assert.Equal(0.2 / 1.2, helmet.Chance(helmet.Variants[1]), 6);
    var t = Lib().Template("Cadian_Shock_Trooper")!;
    var random = new Random(45);
    var empty = Enumerable.Range(0, 6000).Count(_ => InstConstructorLibrary.RandomPicks(t, random)["helmet"] == "EMPTY");
    Assert.InRange(empty / 6000.0, 0.14, 0.195);
    Assert.Equal("Cadian_Shock_Trooper_Helmet_01", InstConstructorLibrary.DefaultPicks(t)["helmet"]);
  }

  [Fact]
  public void Models_come_from_fixedTemplateId_then_name()
  {
    var lib = Lib();
    Assert.StartsWith("fixedTemplateId", lib.Template("Cadian_Shock_Trooper")!.Models[0].Source);
    Assert.Equal("cc_constructor_cultist", lib.Template("Cultist")!.Models.Single().Tpl);
    Assert.Equal("cc_termagant", lib.Template("Termagaunt_01")!.Models.Single().Tpl); // gaunt -> gant, _01 dropped
    var preset = Assert.Single(lib.Presets);
    Assert.Equal("librarium_character_cadian", preset.Name);
    Assert.Equal("cc_cadian_shock_trooper", preset.Tpl);
    Assert.Equal(new[] { "helmet_grp", "torso_01" }, preset.Objects);
    Assert.Single(lib.PresetsFor("cc_cadian_shock_trooper"));
  }

  [Fact]
  public void Layer_rules_follow_required_and_disabled_by()
  {
    var lib = Lib();
    var t = lib.Template("Cadian_Shock_Trooper")!;
    var picks = InstConstructorLibrary.DefaultPicks(t);
    Assert.Equal(new[] { "headWithHair" }, lib.DisabledLayers(t, picks));
    picks["helmet"] = "EMPTY";
    Assert.Equal(new[] { "head" }, lib.DisabledLayers(t, picks)); // head needs a helmet; headWithHair comes back
  }

  [Fact]
  public void Npc_filter_shows_only_the_picked_meshes()
  {
    var lib = Lib();
    var mesh = Cadian();
    var r = NpcVariantFilter.Apply(mesh, lib, VariantRequest.Parse("npc=Cadian_Shock_Trooper,helmet=Cadian_Shock_Trooper_Helmet_01,head=Cadian_Shock_Trooper_Head_01,headWithHair=Cadian_Shock_Trooper_Head_02,body=Cadian_Shock_Trooper_Body_02"));
    Assert.Equal(new[] { "helmet_01", "helmet_01_LOD1", "head_01", "torso_02" }.ToHashSet(), Visible(mesh));
    Assert.StartsWith("(off", r.Regions["headWithHair"]);
    Assert.Contains(r.Notes, n => n.Contains("cadian_caps"));

    mesh = Cadian();
    NpcVariantFilter.Apply(mesh, lib, VariantRequest.Parse("npc=Cadian_Shock_Trooper,helmet=none,body=Cadian_Shock_Trooper_Body_01"));
    Assert.Equal(new[] { "head_02", "torso_01" }.ToHashSet(), Visible(mesh)); // head off without helmet, headWithHair on

    mesh = Cadian();
    NpcVariantFilter.Apply(mesh, lib, VariantRequest.Parse("npc=Cadian_Shock_Trooper,npcpreset=librarium_character_cadian"));
    Assert.Equal(new[] { "helmet_01", "helmet_01_LOD1", "torso_01" }.ToHashSet(), Visible(mesh));
    var match = NpcVariantFilter.MatchPreset(Cadian(), lib.Template("Cadian_Shock_Trooper")!, lib.Presets[0]);
    Assert.Equal("Cadian_Shock_Trooper_Helmet_01", match["helmet"]);
    Assert.Equal("Cadian_Shock_Trooper_Body_01", match["body"]);
    Assert.Null(match["head"]);
  }

  [Fact]
  public void Npc_filter_on_the_wrong_model_hides_nothing()
  {
    var mesh = Cadian();
    var r = NpcVariantFilter.Apply(mesh, Lib(), VariantRequest.Parse("npc=Cultist"));
    Assert.Equal(0, r.HiddenParts);
    Assert.Contains(r.Notes, n => n.Contains("wrong model"));
  }

  [Fact]
  public void Spec_round_trips_through_VariantRequest()
  {
    var spec = InstConstructorLibrary.Spec("Cultist", new Dictionary<string, string?> { ["Head"] = "Cultist_Head_01", ["Wing"] = null }, null);
    Assert.Equal("npc=Cultist,Head=Cultist_Head_01,Wing=none", spec);
    var r = VariantRequest.Parse(spec + ",powerfist=03");
    Assert.Equal("Cultist", r.Npc);
    Assert.Equal("none", r.Regions["wing"]);
    Assert.Equal("03", r.PowerFist);
    Assert.Equal("x", VariantRequest.Parse("npcpreset=x").NpcPreset);
  }

  private const string MeleeSkins = """
meleeActors   =   {
   tzaangor_sword   =   {
      skins   =   {
         01   =   { geomCustomizerRegions   =   [ "sword_1" ] }
         02   =   { geomCustomizerRegions   =   [ "sword_2" ] material   =   "gold" }
      }
      defaultMeleeActor   =   "tzaangor_sword"
   }
   power_fist_fake   =   {
      skins   =   {
         01   =   { geomCustomizerRegions   =   [ "powerfist_1" ] meleeActorOverride   =   "power_fist_fake" }
      }
   }
}
""";

  private const string MeleePreset = """
regions   =   {
   sword_1   =   { meshes   =   [ "sword_1" ] }
   sword_2   =   { meshes   =   [ "sword_2" ] }
   body   =   { meshes   =   [ "body" ] }
   powerfist_1   =   { meshes   =   [ "powerfist_01_grp", "ch_powerfist_01_grp" ] }
}
""";

  private const string Factory = """
actorDescs   =   {
   tzaangor_sword   =   { type   =   { __type   =   "wpn_melee_tzaangor_sword" } }
   power_fist_fake   =   { type   =   { __type   =   "wpn_melee_power_fist_fake" } }
}
""";

  [Fact]
  public void Melee_skins_pick_regions()
  {
    var lib = MeleeCustomization.FromText(MeleeSkins, MeleePreset, Factory);
    lib.ResolveTpls(cls => cls == "wpn_melee_tzaangor_sword" ? "wpn_tzaangor_sword_01" : "sfx_power_fist_inc");
    Assert.Equal(new[] { "tzaangor_sword" }, lib.ActorsFor("wpn_tzaangor_sword_01"));
    var mesh = new ExportedMesh { Name = "wpn_tzaangor_sword_01" };
    foreach (var n in new[] { "sword_1", "sword_2", "body" })
    {
      mesh.Bones.Add(new Bone { Name = n, SourceName = n, Parent = -1 });
      mesh.Parts.Add(new MeshPart { Name = n, Node = mesh.Bones.Count - 1, IndexCount = 3 });
    }

    var r = MeleeVariant.Apply(mesh, lib, "wpn_tzaangor_sword_01", VariantRequest.Parse("skin=02"));
    Assert.Equal(new[] { "sword_2", "body" }.ToHashSet(), Visible(mesh)); // body: region of other tpls, untouched here
    Assert.Equal(new[] { "01", "02" }, r.Presets);
    Assert.Contains(r.Notes, n => n.Contains("gold"));
  }

  [Fact]
  public void Power_fist_skin_shows_its_region_on_a_constructor()
  {
    var melee = MeleeCustomization.FromText(MeleeSkins, MeleePreset, Factory);
    var request = VariantRequest.Parse("preset=1,powerfist=01");
    Assert.Null(melee.AddPowerFist(request, "cc_sm_constructor_tank"));
    Assert.Contains("powerfist_01_grp", request.Show);
    Assert.DoesNotContain("ch_powerfist_01_grp", request.Show);
    Assert.NotNull(melee.AddPowerFist(VariantRequest.Parse("powerfist=99"), "cc_sm_constructor_tank"));
  }

  /// <summary>Retail ssl text when LIBSABER_SM2_SSL points at an extracted ssl/ folder (retail bytes stay out of the repo).</summary>
  [Fact]
  public void Retail_templates_resolve_every_link()
  {
    var root = Environment.GetEnvironmentVariable("LIBSABER_SM2_SSL");
    if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "inst_constructor/inst_constructor_templates.sso")))
      return;
    var lib = InstConstructorLibrary.FromText(File.ReadAllText(Path.Combine(root, "inst_constructor/inst_constructor_templates.sso")),
      File.ReadAllText(Path.Combine(root, "inst_constructor/inst_constructor_mesh_templates.ssolib")));
    Assert.Equal(75, lib.Templates.Count);
    Assert.Equal(413, lib.MeshTemplates.Count);
    Assert.Equal(835, lib.Templates.Sum(t => t.Layers.Sum(l => l.Variants.Count)));
    Assert.Empty(lib.Notes);
    foreach (var f in Directory.EnumerateFiles(root, "*.cls", SearchOption.AllDirectories))
      lib.AddClass(f, File.ReadAllText(f));
    Assert.Equal(61, lib.Presets.Count);
  }
}
