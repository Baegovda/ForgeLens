using System.Numerics;
using LibSaber.SM2.Assets.Animation;
using LibSaber.SM2.Assets.Context;
using LibSaber.SM2.Assets.Effects;
using Xunit;

namespace LibSaber.SM2.Assets.Tests;

/// <summary>mtl1-49 context playback: effect → character / weapon / event, clip → weapon / muzzle, weapon socket scope.</summary>
public partial class HostEffectTests
{
  private static readonly Dictionary<string, string> CtxFiles = new()
  {
    ["characters/npc_sorc_client_base.cls"] = """
      properties   =   {
         geom   =   {
            nameTpl   =   "cc_sorc"
         }
         prop_animation   =   {
            MorphemeController   =   {
               nameBundle   =   "sorc"
            }
         }
         prop_inventory_client   =   {
            localCreatedActorsDescs   =   {
               stave   =   {
                  type   =   {
                     __type   =   "wpn_stave"
                  }
               }
            }
         }
         prop_model_weapon_holds   =   {
            rightHandHold   =   "GUN_R"
            leftHandHold   =   "GUN_L"
         }
      }
      __type   =   "npc_client"
      """,
    ["characters/npc_sorc_client.cls"] = """
      properties   =   {
         sfx   =   {
            list   =   {
               fx_aura   =   {
               }
            }
         }
      }
      __type   =   "npc_sorc_client_base"
      """,
    ["characters/npc_sorc_client.cls.resource"] = """
      __type: res_desc_cls
      linksDesc:
      - res://ssl/characters/bp_sorc_main.sso.resource
      linksDescVisual:
      - res://ssl/sfx/fx_aura.sfx.resource
      parents:
      - res://ssl/characters/npc_sorc_client_base.cls.resource
      """,
    ["weapons/melee/weapon_actors/wpn_stave.cls"] = """
      properties   =   {
         geom   =   {
            nameTpl   =   "wpn_stave_01"
         }
         sfx   =   {
            list   =   {
               cast_ray   =   {
                  __type   =   "fx_ray_prep"
               }
            }
         }
         prop_equipment   =   {
            loadoutItemCategoryUid   =   "melee"
         }
      }
      __type   =   "wpn_melee_client"
      """,
    ["weapons/melee/weapon_actors/wpn_stave.cls.resource"] = """
      __type: res_desc_cls
      linksDesc:
      - res://ssl/characters/bp_stave.sso.resource
      linksDescVisual:
      - res://ssl/sfx/fx_ray_prep.sfx.resource
      """,
    ["weapons/common/firearm/mgs/gun/rubric_bolter_client.cls"] = """
      properties   =   {
         geom   =   {
            nameTpl   =   "wpn_rubric_bolter_01"
         }
         sfx   =   {
            list   =   {
               muzzle   =   {
                  __type   =   "fx_bolter_muzzle"
               }
               muzzle_alt   =   {
                  __type   =   "fx_bolter_muzzle_alt"
               }
            }
         }
      }
      __type   =   "firearm_client"
      """,
  };

  private const string StaveBlueprint = """
    nodes   =   {
       ev   =   {
          __type   =   "BlueprintNodeEquipmentUserMorphemeDurationEvent"
          mrDurationEvent   =   {
             name   =   "vfx_ray_prepare"
          }
          OnActivate   =   [
             "start/Input"
          ]
       }
       start   =   {
          __type   =   "BlueprintNodeVfxRestart"
          vfxUidRef   =   {
             def   =   "cast_ray"
          }
       }
    }
    """;

  private static (ContextResolver Resolver, AnimEventVfxMap Map) Ctx()
  {
    var index = ContextIndex.Build(CtxFiles.Keys, p => CtxFiles.GetValueOrDefault(p));
    var map = new AnimEventVfxMap();
    map.AddBlueprint("bp_stave", StaveBlueprint);
    map.AddBlueprint("bp_other", StaveBlueprint.Replace("cast_ray", "fx_ray_prep")); // not owned: must not count
    return (new ContextResolver(index), map);
  }

  [Fact]
  public void EffectContextWalksWeaponToCharacterAndEvent()
  {
    var (resolver, map) = Ctx();
    var contexts = resolver.ForEffect("fx_ray_prep", map);
    var best = contexts[0];
    Assert.Equal("npc_sorc_client", best.Character); // derived class before _base
    Assert.Equal("wpn_stave", best.Weapon);
    Assert.Equal("wpn_stave_01", best.WeaponTpl);
    Assert.Equal("cc_sorc", best.Tpl);
    Assert.Equal("sorc", best.Bundle);
    Assert.Equal(["vfx_ray_prepare"], best.Events);
    Assert.True(best.Proven);
    Assert.Contains(contexts, c => c.Character == "npc_sorc_client_base");

    var aura = resolver.ForEffect("fx_aura", map)[0];
    Assert.Equal("npc_sorc_client", aura.Character);
    Assert.Null(aura.Weapon);
    Assert.False(aura.Proven); // no blueprint event starts it: HEURISTIC clip pick
  }

  [Fact]
  public void ClipContextFindsWeaponHoldAndMuzzle()
  {
    var (resolver, _) = Ctx();
    var (chars, proven) = resolver.CharactersFor("cc_sorc", null);
    Assert.True(proven);
    Assert.Equal("npc_sorc_client", chars[0]);
    var stave = resolver.WeaponsFor(chars, "anim/sorc_cast_ray_sorc.sani", shoots: false)[0];
    Assert.Equal("wpn_stave", stave.Class);
    Assert.True(stave.Proven);
    Assert.Equal("GUN_R", stave.Hold);

    var gun = resolver.WeaponsFor([], "animation/chaos/rubric_marine/rubric_infernobolt_shoot_rubric_marine.sani", shoots: true);
    var bolter = Assert.Single(gun);
    Assert.Equal("rubric_bolter_client", bolter.Class);
    Assert.False(bolter.Proven);
    var cues = resolver.ShootCues(bolter.Class);
    Assert.Equal(["muzzle", "muzzle_alt"], cues.Select(c => c.SlotKey));
    Assert.Equal("fx_bolter_muzzle", cues[0].Effect);
  }

  [Fact]
  public void ResourceListsAreParsed()
  {
    var (resolver, _) = Ctx();
    var c = resolver.Index.Get("npc_sorc_client")!;
    Assert.Contains("bp_sorc_main", c.Blueprints);
    Assert.Contains("fx_aura", c.Effects);
    Assert.Equal(["npc_sorc_client_base"], c.ResourceParents);
    Assert.Equal("fx_ray_prep", resolver.Resolver("npc_sorc_client", "wpn_stave", null)("cast_ray"));
  }

  [Fact]
  public void ShootTrackCueGoesOnTheWeaponSocket()
  {
    var clip = new AnimationClip { Duration = 2f };
    clip.EventTracks.Add(new AnimEventTrack("Shoot", [new AnimEvent(0.25f, 0.5f, 0, [])]));
    var lib = new EffectLibrary(new MemoryEffectSource(new Dictionary<string, string> { ["fx_flash_part"] = Part, ["fx_muzzle"] = Group }));
    // Character has its own "tip"; the merged weapon's wpn_tip (template name "tip") must win for a weapon cue.
    var host = new EffectHost(["root", "tip", "wpn_root", "wpn_tip"], ["root", "tip", "root", "tip"]);
    var weaponTip = Matrix4x4.CreateTranslation(5, 0, 0);
    host.SetPose([Matrix4x4.Identity, Matrix4x4.Identity, Matrix4x4.Identity, weaponTip]);
    var events = new AnimEventEffects(clip, new AnimEventVfxMap(), n => lib.Resolve(n) is { } t ? EffectDefinition.Build(n, t) : null, host,
      scope: (_, _) => ("wpn", "wpn_root"), trackCues: [new TrackCue("Shoot", "muzzle", "fx_muzzle", "gun", false)]);
    var cue = Assert.Single(events.Cues);
    Assert.Equal(0.5f, cue.Time, 0.001f);
    Assert.Equal("fx_muzzle", cue.Effect);
    var inst = Assert.Single(events.At(0.6f));
    Assert.Equal(new Vector3(5, 0, 0), inst.Socket!("tip", 0)!.Value.Translation);
    Assert.Equal(1, host.IndexOf("tip")); // host names win over aliases
  }
}
