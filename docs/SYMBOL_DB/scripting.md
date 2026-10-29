# symbol DB — scripting and data links (SSL classes, blueprints, resources)

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
| `prop_power_fist_customizer_base_release.sslbin` | `SetRightHandVisibility` → `GetMeshesByBodySection(int 6)` → `FilterRightHandMeshes` (minus `visibilityUnchangableMeshes`) → Show/Hide | hand region RIGHT_GAUNTLET | PROVEN (datapars, bytecode) |
| `inst_constructor_template_release.sslbin` | `GenerateRandomPreset` | requiredLayer / disabledByLayer single pass | PROVEN (datapars, bytecode) |
| `inst_constructor_mesh_template_variant_release.sslbin` | class field-default table | `probability` default 1.0 | PROVEN (datapars) |

**SSLB (compiled ssl, `*_release.sslbin`; datapars, layout INFERRED, reader `A/Scripting/Sslb.cs`, disassembler `tools/re/sslb.py`):** 16-byte header `SSLB` u16 7 u16 3 + 8-byte hash, then protobuf. Top: 6 = string table (0 = module), 8 = class {1 name, 4 member {1 name, 2 slot, 3 kind 2 method / 6 field / 7 const / 3 event, 5 code index, 6 const VALUE, 9 type, 10 attrs}, 10 field default {1 slot, 2 VALUE}}, 9 = code {1 id, 2 sig {1 name, 2 arg}, 4 ret, 5 {repeated 1 instruction}}. VALUE {1 type: 1 int (2), 2 bool (3), 3 float (4 fixed32), 4 string (5 idx), 8 null}. Instruction varint: op = low byte, arg = bits 8..31 − 0x800000. Ops: 00 locals, 01 pop, 02 push null, 03 int, 04 bool, 05 const pool, 06 load arg/local (0 this, <0 locals), 07 n + 03 n argc, 0a get member slot, 0e call method slot, 0f base call, 10 native call, 15/16 return, 1a jump-if-false, 1c and / 1d or (short-circuit), 1e jump, 1f not, 25 add, 3a 7 == / 3a 8 !=, 3b compare, 44 new, 46 store local, 47 store member. Slots are per class; inherited slots keep the base numbers.

**Exe natives:** ApplyMaterialSuffix registration FUN_14143e030 (with SetMaterialByRegion, ChangeMaterial, GetAppliedMaterialSuffix); geom script HideObj/ShowObj (customization.md).

**Code:** `A/Context/ContextIndex.cs`, `ContextResolver.cs`, `A/Effects/AnimEventVfxMap.cs`, `A/Customization/SslText.cs`, `A/Animation/CharacterAnimations.cs`.

