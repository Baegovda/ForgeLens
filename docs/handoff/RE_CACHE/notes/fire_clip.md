# Player fire clip selection (REA/data item 3), 2026-10-30

Status: the mechanism and where it is defined are answered. The exact per-weapon clip table is NOT decoded yet: it needs the binary Switch/Expression node data in primaris.nmb.

## Findings
1. PROVEN (DATA). The ssl/cls data does not name any character fire clip.
   - `prop_firearm_user_animation` (cls + sslbin) holds only ctrlAim, recoilAnimationController, isNeedParentByOwner and debug floats.
   - The player classes (`pc_marine_base_client/authority.cls`) name no shoot clip.
   - `prop_firearm_animation.shootAnimation` (SHOOT_1) is the WEAPON prop sequence, not the character clip.
2. PROVEN (DATA). Shot trigger: `weapons/common/firearm/recoil_animation_controller.sso` sets MorphemeFloat `mrRecoil = "ho_shoot"` with `mrDeactivationDelayMs = 20`. NPC overrides exist, e.g. cultist uses 50.
   - The network has the control parameters `ControlParameters|ho_shoot` and `ho_shoot_smooth`.
   - So each shot pulses the `ho_shoot` float. Nothing names a clip.
3. PROVEN (DATA). Per-weapon key: every firearm cls has `prop_firearm_type_settings.wpnType` (float). The class also has wpnHandType, wpnGripType and wpnMagazineType (from the sslbin field list). Examples:
   - bolt_rifle 1, auto_bolt_rifle 1.01, heavy_bolt_rifle 1.02, deathwatch 1.03
   - stalker 1.1, marksman 1.11, instigator 1.12, bolt_sniper 1.13
   - bolt_carbine (smg) 1.2, occulus 1.21, pyroblaster 1.25, plasma_exterminator 1.3, melta shotgun 1.4
   - aux GL/melta 1.5, combimelta 1.51, las_fusil 1.6
   - bolt_pistol 0.1, plasma_pistol 0.2, assault_bolter 0.3 (client), volkite 0.4, inferno 0.5
   - heavy_bolter / grav 2, plasma_incinerator 2.1, heavy_flamer 2.2, pyrecannon 2.21, heavy_melta 2.3
   - Some versions differ between client and authority (smg heroic_05_a: 1.7 vs 1.2).
   - The source is `Raw/cls17/ssl/weapons/common/firearm/**`.
4. PROVEN (DATA). The selection lives in the Morpheme network `morpheme/primaris/primaris.nmb`, actor "Primaris". It was extracted read-only to `SM2_Work/Raw/fireclip/`.
   - Control parameters: `wpn_type`, `new_wpn_type`, `heavy_wpn_type`, `wpn_type_shoulder`, `melee_wpn_type`. The ssl side sets `heavy_wpn_type` from `prop_weapon_slot_system.holsteredNonPistolFirearmType`.
   - Aimed fire: state `anim|LOCO|minizoom|shoot|shoot_state` / `shoot_state_1`. It holds clip nodes `tacticus_shtN_shoot_aim` plus `Switch_1..9`, `Expression*`, `OperatorArithmetic` and `SimpleTypeConvert`.
   - Hip/blind fire: `anim|LOCO|loco2|blind_shoot|{blind_whole_anim,blind_whole_anim_1,blind_asWas,blind_wham}|shoot|shoot_state` holds `tacticus_shtN_shoot_blind`. The same set exists under `LOCO|shield|...`. These states also contain RandomFloat, ReRange and SmoothFloat nodes, so some variants may be chosen at random.
   - Transitions use ControlParameterInRange / ControlParamBoolTest conditions.
   - Other states: heavy bolter turret (`LOCO|heavy_bolter_turret|shoot`), heavy flamer (`anim|heavy_flammer_shoot`), Techmarine shoulder gun, and last stand (`*_last_stand_shoot`).
   - Clip set: sht1, 2, 3, 5, 6, 7, 9, 10 to 16 (aim + blind), and sht16_aim_melta.
5. INFERRED (~85%). The game copies `wpnType` into Morpheme `wpn_type`, and Switch nodes driven by Expressions on `wpn_type` pick one shtN clip. `ho_shoot` gates or blends the shot. Neither "wpnType" nor "shootAnim" exists as an exe string, which fits script plus network driving, not exe code. The copy itself was not seen, because sslbin bytecode was not decompiled.
6. NOT DONE. The exact wpn_type → shtN table: Switch thresholds and Expression bodies are binary fields of the nmb node data.

## Recommended repo change (not applied)
- Replace the name regex in `PropAnimStates` step 3b: pick the clip from the player clip set `tacticus_shtN_shoot_{aim|blind}` keyed by the firearm `wpnType` (DATA).
- Keep the wpnType→N mapping HEURISTIC until the nmb Switch nodes are decoded. Next step: a small nmb node reader in `tools/re/` (Switch node: input CP + thresholds + child clip ids) that dumps the `minizoom|shoot|shoot_state` and `blind_shoot` switches. That would make the mapping PROVEN.
- Doc: the shot trigger for player clips = `ho_shoot` pulse (RecoilAnimationController, DATA). Player clips have no Shoot track, so the rate stays firearm_library shotsPerMin (DATA).

# Round 2 (2026-10-30 00:20 KST): nmb decoded, pushed 51947fa (R38)
Tool outputs: out/states.txt (state dumps), out/mapping.tsv (per weapon × plasma_charged × isInAuxVision).

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
| 1.4 | melta shotgun | sht5 | SubtractiveBlend(sht5) | INFERRED |
| 1.41 | melta shotgun (chaos) | sht1 | sht5 | aim HEURISTIC |
| 1.25 | pyroblaster | sht15 | sht15 | INFERRED |
| 1.51 | combi melta | sht16 (aux: sht16_melta) | sht16 | INFERRED |
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
