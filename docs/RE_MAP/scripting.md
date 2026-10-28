# RE map — scripting and data links (SSL classes, blueprints, resources)

**Scope:** how game data links classes, models, animations, weapons and effects. Mostly data-level RE (text SSL + resource YAML), which is why few exe addresses appear here.

**Links (data, verified on retail files):**
| From | Field | To | Status |
|---|---|---|---|
| `.cls` | `properties.geom.nameTpl` | model `.tpl` | data |
| `.cls` | `prop_animation…MorphemeController.nameBundle` | clip bundle (`<action>_<bundle>.sani`) | data |
| `.cls` | trailing `__type` | parent class | data |
| `.cls.resource` | `linksDesc` | owned `.sso` blueprints | data (mtl1-49) |
| `.cls.resource` | `linksDescVisual` | owned `.sfx` effects | data (mtl1-49) |
| `.cls.resource` | `parents` | parent classes | data |
| `.cls` | `sfx.list.<key>.__type` | effect for blueprint key `<key>` | data |
| `.cls` | `prop_inventory_client.localCreatedActorsDescs.*.type.__type` | held weapon class | data (PROVEN on the Sorcerer stave) |
| `.cls` | `prop_model_weapon_holds` | hold locators (GUN_R / GUN_L) | data |
| `.sso` | `BlueprintNode(EquipmentUser)Morpheme(Duration)Event` → VfxRestart / StartSfx / stop nodes | `.sani` event tags → effects | data |
| `.sani` | `Shoot` track | weapon `muzzle*` sfx slot | HEURISTIC |
| customization `.sslbin` | `SetActivePresetToDefault` + PREDEFINED_PRESET_1 | default look | data |

**Exe natives:** ApplyMaterialSuffix registration FUN_14143e030 (with SetMaterialByRegion, ChangeMaterial, GetAppliedMaterialSuffix); geom script HideObj/ShowObj (customization.md).

**Code:** `A/Context/ContextIndex.cs`, `ContextResolver.cs`, `A/Effects/AnimEventVfxMap.cs`, `A/Customization/SslText.cs`, `A/Animation/CharacterAnimations.cs`.

