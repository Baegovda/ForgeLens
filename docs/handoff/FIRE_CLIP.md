# Player fire clip selection (R38, 2026-10-30 KST)

> **2026-11-02:** `docs/re/REA_DEEP.md` §6 assigns `c4` = jump-if-falsey, `98` = jump-if-truthy, `95` = prep/coerce (ALU INFERRED). Exe dispatch table still missing. **`tools/re/nmb_reader.py` and `PlayerFireClips.cs` still treat 95/98/c4 as undecoded** (both branches / HEURISTIC rows for wpnType 1.5 and 1.7). Apply §6 before trusting those rows. Muzzle node `shootTip` is parsed from cls but `SM2_MUZZLE_SHOOTTIP` defaults off (`ContextResolver`).

Source: `morpheme/primaris/primaris.nmb` (Primaris actor Morpheme network). Read-only extraction on the PC: `SM2_Work\Raw\fireclip\`.
Reader: `tools/re/nmb_reader.py <primaris.nmb>` dumps the states. `--map wpn.tsv` evaluates the mapping (TSV columns: name, wpnType).
Code: `PlayerFireClips` (src/LibSaber.SM2.Assets/Animation/PlayerFireClips.cs), used by `PropAnimStates.AutoPlays` step 3b.

## Data chain
- The ssl/cls data names no character fire clip (PROVEN, DATA). `prop_firearm_user_animation` holds only aim and recoil controllers.
- Shot trigger: `weapons/common/firearm/recoil_animation_controller.sso` → MorphemeFloat `ho_shoot` (deactivation 20 ms). This is DATA.
- Weapon key: firearm cls `prop_firearm_type_settings.wpnType` (DATA). The class also has wpnHandType, wpnGripType and wpnMagazineType. The network control parameter `ControlParameters|wpn_type` is fed from it (INFERRED: same name and value ranges; the ssl copy was not decompiled).
- No exe strings for wpnType or shootAnim. Selection is network data, not exe code.

## Binary layout (derived from data)
| Item | Layout | Label |
|---|---|---|
| String table | IDMappedStringTable `{u32 n, u32 dataLen, u64 ids, u64 offsets, u64 hashes, u64 data}` | PROVEN (every name resolves) |
| NodeDef | `u32 type, u32 flags, u16 id, u16 parent, u16 nChildren, u16 nInputCP`, then rel u64 children at +0x28, input CPs (u16 node, u16 pin) at +0x30, attrib handles at +0x48 | INFERRED (ids/parents/children agree with the name paths) |
| Switch (type 0x83) | attr0 = weights f32[n]; attr1 = (u32 mode, u32 selection) | weights PROVEN-consistent; selection 1 = floor, 0 = closest INFERRED (wpn_type switches use 1, boolean ones use 0) |
| Expression (type 0xcb) | constants `{u32 type=4, i16 id, f32 value}` × 24 B; instructions `(b0, b1, b2, op)`; b1 ≥ 0x80 = constant, b2 = variable (inputs, then OUT); target = pc + 1 + b0 | INFERRED |
| Ops | `04` OUT = c; `a4` if !(v == c) jump; `a5`/`bd` if (v == c) jump (OR-chains); `c0` jump; `95`/`98`/`c4` not decoded | INFERRED / HEURISTIC |

## States
- Aim: `LOCO|minizoom|shoot|shoot_state` (and `shoot_state_1`, identical). It contains:
  - Switch_1(character_type == 13 → npc sniper clip)
  - Switch(wpn_type, floor over [0, 1]):
    - 0 → Switch_9(Expression_1) → sht6 / sht7 / sht14
    - 1 → Switch_8(Expression_2) → sht1 / 2 / 3 / 5 / 15 / Switch_2(isInAuxVision) → sht16 / sht16_melta
- Hip (blind): `LOCO|loco2|blind_shoot|blind_whole_anim|shoot|shoot_state` contains Switch(wpn_type, floor over [0, 1, 2]):
  - 0 → pistols, same as aim
  - 1 → rifles: sht1 / 2 / 3 / SubtractiveBlend(sht5) / sht15 / sht16. Its Expression_2 has an explicit else → 5 = sht16.
  - 2 → Switch_10(wpn_type floor over [2, 2.1, 2.2, 2.3]) → sht9 / (plasma_charged ? sht12 : sht11) / sht10 / sht13
- Variants (wham, asWas, whole_anim_1, shield) use the same structure with fewer cases.

## Mapping (aim / blind)
| wpnType | weapons | aim | blind | label |
|---|---|---|---|---|
| 0 | heavy bolt pistol, absolver, inferno combi | sht7 | sht7 | INFERRED |
| 0.1 | bolt pistol, hgun bolt carbine | sht6 | sht6 | INFERRED |
| 0.2 | plasma pistol | sht6 (charged: sht7) | same | INFERRED |
| 0.3 / 0.4 | assault bolter / volkite | sht14 | sht14 | INFERRED (0.4 → index 3 clamps) |
| 0.5 | inferno pistol | sht6 | sht6 | HEURISTIC (unassigned → default 0) |
| 1, 1.01, 1.02, 1.04, 1.52 | bolt rifle family | sht1 | sht16 | blind INFERRED; aim HEURISTIC (unassigned) |
| 1.03, 1.11, 1.2, 1.21 | DW bolt rifle, marksman, bolt carbine, occulus | sht1 | sht1 | INFERRED |
| 1.1, 1.13, 1.6 | stalker, bolt sniper, las fusil | sht2 | sht2 | INFERRED |
| 1.3 | plasma exterminator | sht1 (charged: sht2) | same | INFERRED |
| 1.12 | instigator | sht3 | sht3 | INFERRED |
| 1.4 | melta shotgun | sht5 | SubtractiveBlend(sht5) | HEURISTIC (R40: SubtractiveBlend not indexed, child assumed sht5) |
| 1.41 | melta shotgun (chaos) | sht1 | sht5 | aim HEURISTIC |
| 1.25 | pyroblaster | sht15 | sht15 | INFERRED |
| 1.51 | combi melta | sht16 (aux: sht16_melta) | sht16 | HEURISTIC (R40: Switch_2 input SimpleTypeConvert not indexed; isInAuxVision assumed) |
| 1.5 | aux GL / aux melta | sht2? | sht16 | HEURISTIC (undecoded op c4 on isInAuxVision) |
| 1.7 | smg heroic_05_a (authority only) | sht15? | sht16 | HEURISTIC (undecoded ops 95/98) |
| 2 | heavy bolter, grav cannon | sht1? | sht9 | blind INFERRED; aim HEURISTIC (no heavy branch) |
| 2.1 | plasma incinerator | sht1? | sht11 (charged: sht12) | blind INFERRED |
| 2.2, 2.21 | heavy flamer, pyrecannon | sht1? | sht10 | blind INFERRED |
| 2.3 | heavy melta | sht1? | sht13 | blind INFERRED |

## Open
- The default value of an unassigned Expression OUT (assumed 0).
- The meaning of ops 95/98/c4.
- The SubtractiveBlend node record (not indexed; its children are assumed to be sht5).
- The ssl code that copies wpnType → wpn_type.
- Heavies in aim: there is probably no minizoom state for them. Check which state heavies use when aiming.
- In game, no visual check of the result has been done.

## Verification (R40, `tools/re/nmb_verify.py`)
`nmb_verify.py primaris.nmb --all-states` → `FIRE_CLIP_VERIFY.tsv` (31 weapons × plasma × aux = 124 rows; weapon list `fire_clip_weapons.tsv`).
- Table vs network: 0 mismatches; 60 rows INFERRED (no assumption), 64 HEURISTIC. Two rows were relabelled INFERRED → HEURISTIC (1.4, 1.51), code + tests updated.
- Assumptions it lists: unassigned Expression OUT → 0; undecoded ops 95/98/c4 (1.5, 1.7: first outcome taken); heavies (≥2) floor into the rifle aim branch; SubtractiveBlend / SimpleTypeConvert records not indexed.
- Variant states (wham, shield, asWas, shoot_state_1) diverge for the else-branch weapons (blind gives sht1 instead of sht16, pistols 0.3/0.4 differ). Which variant the game picks is not known.
- Lead for heavy aim (not followed): states `LOCO|heavy_bolter_turret|shoot|shoot_anim` (sht11/sht12 aim clips) and `anim|heavy_flammer_shoot`.
