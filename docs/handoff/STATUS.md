# STATUS — SM2 pipeline (as of 2026-10-27 ~05:15 KST, tip 57c38f9)

Branch `cursor/sm2-pck-extractor-4420`, PR #1. Always confirm the current state with `git fetch && git log --oneline -30 origin/cursor/sm2-pck-extractor-4420`.
Percentages are the previous agent's estimates of how complete each track is against the "looks exactly like in-game" goal.

## mtl1-48 (2026-10-29 KST): in-game comparison done statically (no captures). ApplyMaterialSuffix PROVEN → diffuse td `materials.<suffix>` variant; skin materials now use it (blue/white = red NM/spec, yellow/violet = green spec), naming fallback HEURISTIC. VFX bench rerun vs the 6 in-game photos: muzzle/grenade closer after mtl1-44 (HANDOFF "Update mtl1-48")

## mtl1-47 (2026-10-29 KST): skin materials applied (texture _<material> siblings, melee/firearm override skins resolve to their artificer tpl, excludeObjectsToApplyMaterial honored; SM2_SKIN_MATERIAL=0 disables). powerfist=KEY hides the RIGHT_GAUNTLET hand region except visibilityUnchangableMeshes (HEURISTIC section). Preview unchanged (HANDOFF "Update mtl1-47")

## mtl1-46 (2026-10-29 KST): weapon skin attachingActors (purity seals) merged into firearm exports under their purity_seal_N locator with the purity_seal_01 class scale 0.7. Paper not simulated; preview unchanged; SM2_WPN_ATTACH=0 disables (HANDOFF "Update mtl1-46")

## mtl1-45 (2026-10-29 KST): hem spikes are rotation-fit flips. The cluster-fit rotation step is capped at 30°/frame (SM2_PHYS_FIT_MAXSTEP, APPROXIMATION), plus a collider sub-step lerp (SM2_PHYS_COLLIDER_LERP). run_fwd >90° steps 12 → 0, max 144.7° → 33.2°. Old/new bakes compared in UE via SIE captures (folded hem gone). Finger curl skips gaps < 5° (titus regression); Cadian jog residual ~6° final (HANDOFF "Update mtl1-45")

## mtl1-44 (2026-10-28 KST): SFX blend selector PROVEN. td sfx loader FUN_1425fd580 reads blendMode {blend 0, add 1, premul_a 2} (table 0x144cccd38). Key builder FUN_142451840: blend → SFX_ALPHA_BLEND (SrcAlpha/InvSrcAlpha), add → SFX_BLEND_ADD_NO_ALPHA (One/One), premul_a → none (One/InvSrcAlpha). Applied per material in the browser and UE export. A missing blendMode stays additive (HEURISTIC, final). UseSpecMap / 0x400 / auto-exposure / falloff are HEURISTIC, final (HANDOFF "Update mtl1-44")
## mtl1-43 (2026-10-28 KST): cc_calgar cloak detail re-import (stripes gone), cloak V-flip = false lead, loop-seamless cloth bakes (seam 179.6°→0.06°) + fit prior (hem spikes reduced), unresolved CDT bones PROVEN dropped by game, finger curl tool (Cadian), sfx blendMode enum + define bits PROVEN, selector still HEURISTIC (HANDOFF "Update mtl1-43")
## ui-45 (2026-10-28 KST): Forge Lens Kitbash Enemy/NPC mode — 75 inst_constructor templates (835/835 links), layer rules, weighted Randomize, 61 .cls presets, npc= --variant; melee region skins (skin=, Team MELEE) + armor power fist. Link/weight/model mapping HEURISTIC (HANDOFF "Update ui-45")
## ui-44 (2026-10-28 KST): Forge Lens Kitbasher — Kitbash tab: faction/class/preset + one part per slot from the customization .sso, live preview = export filter (ArmorVariant, SLOT=none), Export combination. Story/champion tpl mapping HEURISTIC (HANDOFF "Update ui-44")
## ui-43 (2026-10-28 KST): Forge Lens UE5-style Content Browser — Favorites / Folders|Categories / Collections, thumbnail grid + list, chips, hover-animated cached thumbnails; Anim wheel steps clips. Category name rules, effect splat and clay thumbnails are HEURISTIC (HANDOFF "Update ui-43")
## mtl1-42 physics/materials batch (2026-10-28 KST): Calgar cape stripe = detail tiling, 60 Hz Calgar clips, PA assign, cloth bone cap 4096, CDT sweep
## mtl1-42 (2026-10-28 KST): VFX black squares/fly-by fixed, cloth SIM shells hidden in preview, export In place, file list UI

## mtl1-41 (2026-10-28 KST): particle flag 0x20 isAlphaKill rule PROVEN, inline list nodes; VFX ≈95 %

## shaders-1 (2026-10-28 KST): static .sdc shader bundles — LUT apply, SFX blend, particle blend/tint PROVEN; see SHADERS.md

## Per-feature status

| Area | State | % | Notes / where |
|---|---|---|---|
| Archives `.pak`/`.pck` + `.cache` list/extract | done | 100 | 115 archives, 486,564 files on retail, 0 skipped |
| Textures (BC1-7, mips, cubemaps; DDS/PNG/TGA) | done | 100 | stored-channel remap (`swizzleTexInShader`) recorded in sidecar |
| Meshes `.tpl` + `.tpl_data` (skinned + rigid, LOD, composite LOD, cloth `_SIM` hidden) | done | 95 | merged `Object*` proxies hidden on LOD-chain weapons (d181b5c) |
| Materials → UE (M_SM2 Substrate, blendMode, albedoTint, second spec, aniso, baseLayer, DirectAo) | done, tuning open | 90 | Calgar hair still brighter/streakier than in-game; no Titus/Hormagaunt in-game reference |
| Constructor (customization library) tpls: `--variant`, `--tex-formats` | done + UE verified | 100 | see "UE verification" below |
| Animation `.sani` (v1/v2/v3, additive, root motion, rebase, anim-only, multi-clip) | done | 95 | 23,227/23,227 decode; v3 raw sub-1 codec unused by retail |
| FBX → UE root motion (ROOT bone 0, single tpl root = bone 0) | done | 100 | Tyranid/walker fix ec02444 |
| Mannequin → SM2 retarget prototype | prototype | 70 | Cadian index finger curl −14…−20° open; Anim Only (Manny) root yaw-180 fix mtl1-13 |
| Browser GPU preview (v3/v4: ANGLE, MSAA, blend passes, anim ordering, Space) | done | 95 | CPU renderer ignores blend and physics |
| Secondary physics (jiggle exact port, cloth PBD ≈ NvCloth) browser + FBX bake + **live UE (plugin v2, mtl1-7)** | done + UE live verified | 88 | 6 chars live, no NaN; tech_priest robe drop + sorcerer_exalted dash lag FLAG |
| Chaos Cloth option (opt-in) + sidecar collision PA | in progress | 70 | mtl1-13: collision wired, no NaN, 2–6% particles >1 cm inside; wind not yet applied |
| Browser VFX (`.sfx` sprites, meshes, trails, lights, distortion, sockets, anim-event cues) | in progress | ~85 | see ROADMAP "VFX3 remaining" |
| Bulk validation (37 assets) | done | 100 | PASS 31 / WARN 3 / FAIL 3 → constructors fixed afterwards |
| UE verification of constructors + physics | done (first pass) | 100 / 85 | open issues below |
| In-game side-by-side comparison | done (static, mtl1-48) | 70 | VFX vs 6 user photos + static RE per track; no motion/physics reference (no captures allowed) |
| Unreal export test (no Niagara) | done (preview materials) | 100 | `/Game/SM2_Work/VfxExport`: 20 textures, 2 meshes, 2 masters + 20 MIs; capture `Captures\ue_export\ue_export_board.png` |
| Browser viewer polish → **Forge Lens** (rename + dark theme, mtl1-6) | done | 90 | ForgeLens.exe; theme is visual-only (App.axaml); ui_before/after.png |

## Commits this session (2026-10-27, oldest first)
| Commit | What |
|---|---|
| 8292777, e0b0df4, ec02444, **d0ad14f** | Bulk validation: glt.blendMode "none" = opaque; export log names unmatched tracks; single tpl root = bone 0; manifest + export/UE/capture scripts + report |
| **b7ae735** | Constructor tpls: `--variant` (HideObj/ShowObj subtree hide, PREDEFINED_PRESET_1 default), `--tex-formats`, partsOnly textures, GC every 32 textures in the UE material import |
| **642fb56** | Secondary physics: live cloth/jiggle in the GPU preview, baked into the FBX, `{model}.physics.json`, `ue_sm2_physics.py` |
| **697c0c4** | VFX v1: `.sfx` parser, deterministic 60 Hz sim, GPU sprite renderer, `--vfxbench` |
| **00d32bd** | VFX v1 UI (play/pause/Space, restart, loop, scrub, orbit), draw targets, orphan emitters, tests |
| **5d55daa** | VFX2: lwiGeom mesh particles, sfx_trail ribbons, RGBA override colour fix (blood) |
| **b31bd95** | VFX3 part 1: effects on bones/sockets, anim-event VFX cues, emit-by-normal (+Z) on sockets |
| **69651e9** | skip sprites whose texture is missing (was the "huge jetpack flame") |
| af459c9 | distortion (refraction) pass |
| 4b31810 | anim-event stop cues |
| eaa33bd | `optimization.degNear/degFar` camera-distance fades |
| d181b5c | bolter "drum": hide merged Object* proxies |
| cc147d7 | In place keeps root-motion clips on the spot (dash 9.5 m drift) |
| **e4ceb7d** | HANDOFF: vfx3 part 2 |
| 799f9d5, fffb202, **f99b30f** | VFX3 part 3: `em_covering_*` reported as surface coverings, `textureTintControl` (grenade smoke luminance fix), HANDOFF part 3 |
| 8edbdb9, 84c00ef, c4e8050, **57c38f9** | UE verify: `ue_sm2_physics.py` Name key fix, 60 Hz baked-clip import (`custom_sample_rate`), `ue_cap_lit.py fit`, `ue_phys_probe.py`, HANDOFF |
| (next) | this handoff kit: AGENTS.md + docs/handoff/ |

HANDOFF's "vfx3 part 2/3" sections cite **local** hashes (d6b28f4, e5628ce, 1f9ffce, 28d731d, ...). The cloud agent's `git am` rewrote them; match them by subject.

## Tests
- At e4ceb7d (run by the cloud agent): **SM2.Assets 111, SM2Extractor 42, LibSaber.Tests 5, all pass.**
- vfx3c (box, 2026-10-27, not pushed yet): **SM2.Assets 116, SM2Extractor 42, LibSaber.Tests 5, all pass.**
- History: 101 at 00d32bd, 104 at 5d55daa, 107 at b31bd95.
- HaloCEA tests fail and are **out of scope**.
- Retail-data tests read env vars (`LIBSABER_SM2_SAMPLE`, `LIBSABER_SM2_TITUS`, `LIBSABER_SM2_PICK`). When unset they return early, so they pass without game files.

## Bulk validation (step 4, d0ad14f) — `/workspace/bulk/REPORT.md`, `tools/bulk/`
- 37 assets (SM, Tyranid, Chaos, Human, Vehicle, Prop/Weapon/Door). **PASS 31, WARN 3, FAIL 3.**
- WARN: captain partial tracks (face bones; expected); rubric `rubric_fwd` and lesser sorcerer `walk_fwd` have 0 root displacement (in-place clip?). That one is **not REA-verified**.
- FAIL: the 3 constructors (too big, UE RHI abort). **Fixed in b7ae735 and verified in UE** (below).

## UE verification (2026-10-27, after the user's PC became free)
Extractor build: PC `Desktop\SM2_Work\Bin\SM2Extractor_e4ceb7d`. Captures: PC `Desktop\SM2_Work\Captures\ue_verify\`, box `/workspace/browser_run_shots/ue_verify/`.
- **Constructors (tank, sniper, soldier): PASS** in `/Game/SM2_Work/Tests/bulk_fix/<a>`.
  - No D3D11 RHI crash; editor peaked at ~14 GB.
  - Slots = MIs (54/64/41), 0 material mismatches.
  - Correct default variant: one armor set each, no stacked pieces.
- **Physics bake: PASS** for Titus and Calgar in `/Game/SM2_Work/Tests/physics/<a>`.
  - All jiggle and helper bones are present (Titus 34 + 922, Calgar 3 + 1294) and move.
  - The 60 Hz baked-clip import is fixed: `Tests/physics/cc_titus/Anims60`. The plain `Anims` folders are the old 30 fps imports.
  - `PA_cc_titus_sm2` and `PA_cc_calgar_sm2` were created but not assigned.
- **AnimDynamics / Chaos Cloth live setup is manual:** UE 5.7 Python has no API for AnimGraph nodes or cloth-asset creation. The bake is what plays. The `ue` block in `physics.json` is the manual recipe.
- **Open:**
  - Calgar cape shows a red/white diagonal stripe. It is a material/texture issue, also present without physics. Needs REA of the cloak material.
  - Spiky Calgar cloak hem late in the clip: helper cap 1024 < 1133 free particles, plus the polar fit on thin strips.
  - Calgar was not re-imported at 60 Hz.
  - No in-game comparison yet.

## Open limitations (consolidated)
- **Physics:** cloth is a PBD approximation of NvCloth (no self-collision, unit-mass wind, box size = full extent); jiggle scalar defaults and the teleport threshold are unverified; baked clips are not loop-seamless (rough seam); the CPU renderer has no physics.
- **Animation:** rubric/sorcerer locomotion root motion 0 (WARN, unverified); Cadian index finger curl in the retarget; additive compose order not proven.
- **Materials:** Calgar hair brightness (UE lights the second slab with IBL, the game uses direct light only); Calgar cape stripe; eyelash metalness unverified.
- **VFX heuristics (not REA-proven):**
  - size treated as radius / half-width (the half-width part is checked);
  - ratioDependency simplified;
  - blend_add interpreted;
  - exposure 1/250 and lit ×100 tuned on corpus medians;
  - genOnFly approximated;
  - distortion resolve scale `SM2_VFX_DISTORT` 0.04 (buffer math exact since vfx3z; power→vertex alpha PROVEN in vfx4-1, FUN_1417eeed0);
  - trail DEGR slot→field mapping (desc offsets PROVEN in vfx4-1: degNear +0x3e8, degFar +0x400, degradationRadius +0x418; CB pack not traced);
  - finisher blood saturation (scene ambient/fog; no exe evidence found in vfx4-1);
  - bench bloom point 0.45, surface light 60, LitScale 400 (vfx3z);
  - degNear curve shape;
  - flesh colour assumed dark red.
- **VFX not implemented:**
  - VAT blood-splash animation (layout unknown, see HANDOFF vfx3 part 3);
  - SELF_DISTORTION render;
  - degNear on trails/meshes;
  - `applyColorGradients` (GPU-side; `additionalContrast` is now parsed and rendered behind `SM2_VFX_CONTRAST=1`, see HANDOFF vfx3c);
  - tcIn scale on trails;
  - wind/water sim draws;
  - GPU-only features (vector fields, depth collision).
- **UE:** `ue_cap_lit.py` should wait for texture compilation (grey captures otherwise).

### 2026-10-27 (KST) vfx3d
- Tests: SM2.Assets 118, Extractor 42, LibSaber.Tests 5 — all pass.
- Trail degNear/degFar + texcoord scale done (TC behind SM2_VFX_TRAIL_TC=1). Tint exact formula behind SM2_VFX_TINT_EXACT=1.
- VAT VS still unlocated. VFX estimate ≈ 80% (estimate).

### 2026-10-27 (KST) vfx3e
- Tests: SM2.Assets 120, Extractor 42, LibSaber.Tests 5 — pass.
- VAT VS found (combine_material.sdc 00000_0) + VatDecode helpers; renderer hookup pending. Sprite degNear fix behind SM2_VFX_DEGNEAR_FIX=1.
- VFX estimate ≈ 81% (estimate; VAT decode known but not rendered).

### 2026-10-27 (KST) vfx3f
- Tests: SM2.Assets 121, Extractor 42, LibSaber.Tests 5 — pass. VAT hooked into EffectRenderer behind SM2_VFX_VAT=1 (unverified on real assets).
- Ghidra 12.1.4 light re-analysis running: /workspace/rea/ghidra_proj3 (log /workspace/rea/ghidra3.log).
- VFX estimate ≈ 82% (estimate; VAT path unverified).

## vfx3g — 2026-10-27 (KST): VFX flag verification (headless `--vfxbench`, llvmpipe, box)
Captures: `/workspace/browser_run_shots/vfx3_verify/` (PC copy `Desktop\SM2_Work\Captures\vfx3_verify\`); OFF top / flag bottom.
- `SM2_VFX_VAT=1` — **was broken at 9474df0** (`splash_vat_broken_9474df0_cmp.png`: long spikes/streaks), **fixed here**
  (`splash_vat_fixed_cmp.png`: droplet spray that gathers into the splash shape; VatDraws(cum) = 59 on fx_mgs_hierophant_bomb_ee).
  Bugs fixed (all still behind the flag):
  1. Every part used the first td material (`splash_05`): solid_blood.td has one material per part (`splash_07` ↔ part
     `splashe07`, …) with its own bounds/frames/texture (`splash7_v2_anim_data`, 9, 12, 17, 18, 19). New
     `VatDecode.MaterialBlockFor(td, partName)` (HEURISTIC: name match only) + tests. Extra textures extracted to PC `Raw\vfx\tex8`.
  2. Channel order: the pct is ARGB8888 = DXGI 87 B8G8R8A8 (`TextureDecode`) and decodes to raw bytes → sample `.bgr`.
  3. Texel-exact `texelFetch` (1 column per vertex; bilinear u blended neighbours) and `fract(v)` wrap (band 0 goes below 0
     in the VS formula; wrap sampler assumed — HEURISTIC).
  Offline check (python, splashe05..18): decoded triangles stay coherent (edge p99 ≈ 1.2 cm vs rest 1.0 cm).
  Still open: frame source (animStage = age/life, heuristic); rest-pose match is approximate (median ≈ 3 cm); node-transform
  offset not observed at this camera.
- `SM2_VFX_CONTRAST=1` — frag grenade: very subtle (mean |Δ| 0.07/255), slightly crisper core/sparks; plausible, low impact.
- `SM2_VFX_TINT_EXACT=1` — frag grenade: visible change (mean |Δ| 0.46): first frames gain orange/blue tints in the fireball
  and smoke. Plausible per the REA'd tint, but the blue cast on early smoke looks suspicious — needs an in-game reference.
- `SM2_VFX_TRAIL_TC=1` / tint on `trail_stalker_bolter_smoke` — **inconclusive**: the bench emitter is stationary, the trail
  builds 6–45 quads but renders ~invisible (max pixel Δ 5). Needs a moving-emitter capture (bench has no motion path).
- `VfxBench` per-frame lines now print `vatDraws(cum)`; `GlPreview.VatDraws` exposes the counter.

## vfx3h (Ghidra REA, sm2_retail.exe, 2026-10-27)
Evidence from decompiling ghidra_proj3 (scripts /workspace/rea/scripts/DecompAt.java, DispScan.java). Docs only, no code change.
- genOnFly (PROVEN): emitter+0x9b0 is an array {ptr,count@+0x9b8} of 12-byte entries {key, emitterNameId@+4, numParts@+8} (parser FUN_1419b9a50 reads "numParts"). genOnCDT is +0x9c0 (16-byte entries), genOnDie is +0x9d0 (12-byte entries). Runtime: FUN_1417e4e10 (CPU) and FUN_1417e5060 (GPU) match each entry's nameId against child emitters (child+0x6bc CPU, +0x34 GPU) and attach the child to the parent. GPU attach is FUN_1417e5b70 with mode 0=fly/1=CDT/2=die, at most 3 slots at +600/+0x290, log "attached GPU emitter". CPU attach is FUN_1417e5a20, 12 slots at +200. So genOnFly is not a rate: it spawns child emitter `name` attached to each flying parent particle, numParts per parent (HEURISTIC that numParts is per-particle; not traced further).
- ratioDependency (PARTIAL): at emitter-desc +0xc0 (FUN_141796980 → FUN_141786e20): dependencyType at +0 ("none"=0, "Velocity"=1, "Age"=2; FUN_141787230, strings at 143567450/1436443ec), ratioXY curve at +8 (FUN_1417867c0; constExpr/ageSpline). Runtime consumer not located.
- blendType (PARTIAL): FUN_141751c70 gives blend=0, add=1, blend_add=2 (writer FUN_141752030 also has 3). The D3D12 blend-desc consumer was not located within the time box; the mapping in the existing code stays HEURISTIC.
- animStage: serialised in FUN_14179fdd0 group (FUN_14179edc0/FUN_14179f290 at desc+0x20). Runtime setter not located, so VatDecode.StageFrame age/life remains HEURISTIC.
- Exposure 1/250, light x100, distortion strength, and trail DEGR/MISC/OFFSET CPU mapping: NOT resolved (only shader define strings DISTORTION_* / EXPOSURE=%g found). These remain HEURISTIC.
- VFX estimate corrected to ≈ 72% (honest; earlier 80–82% figures were optimistic).

## vfx3i (Ghidra REA follow-up, 2026-10-27)
Scripts: /workspace/rea/scripts/Callers.java (new), DispScan.java. Outputs: /workspace/rea/vfxi_1.c, vfxi_2.c. Docs only. Nothing proven was worth a code change, so there is no env flag.
- Emitter desc layout (PROVEN): typeParams variant at emitter+0x1a8 (FUN_1417adea0 → FUN_1417a37c0). Type int at +0x1a8: 0=default (FUN_141793d40), 1=distortion (FUN_141796980), 2=lwiGeom (FUN_1417a1ef0). Payload at +0x1b0. Runtime code reads emitter desc via [inst+0x450].
- blendType (PARTIAL): default.visuals (FUN_14178ce70 at payload+0x1f8). blendType sits at emitter+0x48c, isBlendAlphaMultiply at +0x490. Only reader in 0x1417b0000–0x141a00000 is FUN_1417e1e50 (GPU emitter constants). It sets flag bit 0x20 when blendType==1 ("add") and clears bit 0x400 when isBlendAlphaMultiply && blendType!=1. blend_add (2) gets NO separate bit there, so it is handled like "blend" for these flags. The D3D12_BLEND_DESC builder is still not found; the current mapping stays HEURISTIC.
- ratioDependency (PROVEN for distortion): emitter+0x270 (payload+0xc0). FUN_1417e29b0 binds the pointer emitter+0x270 into a GPU curve slot (inst+0x240, tag 0x2000000000000), the same as the other curves at +0x508/+0x580. So ratioXY is evaluated on GPU. Emitter flag 0x1000 (inst+0x6b8) is set unless dependencyType==none, or (field[2]==0 and ratioXY ends ==1.0) (1417e3f73). HEURISTIC: it scales the sprite X/Y aspect by velocity or age.
- Distortion strength: distortion.power at payload+0x98 = emitter+0x248, curve parser FUN_14177c520. Reader FUN_1417daac0 references +0x248 but was not decoded (size 5033).
- animStage: in the lwiGeom payload, FUN_14179fdd0 at payload+0x178 = emitter+0x328 (+0x20 inside). FUN_1417e9480 writes inst+0x328 = 0 (an instance field, not proven to be animStage). The runtime setter is still unproven, so age/life remains HEURISTIC.
- Exposure 1/250, light ×100, trail DEGR/MISC/OFFSET: still NOT resolved. FUN_1417e1e50 writes 1/max(|x|,1e-6) of emitter+0x3a0 (default) or +0x1b0 into GPU constants (+4). That is a reciprocal, but its link to exposure is UNPROVEN.
- VFX estimate stays ≈ 72%; the genOnFly/ratio/blend layout knowledge does not yet change rendering.

## vfx3j (Ghidra REA follow-up, 2026-10-27)
Outputs: /workspace/rea/vfxj_1.c, vfxj_3.c. Docs only. Nothing new reached "proven + implementable", so this round has no code, env flag or tests.
- FUN_1417daac0 was a FALSE POSITIVE: its +0x248/+0x1a8 hits are RBP stack-frame slots (1417db5fa, 1417db3ed), not emitter fields.
- distortion.power has no CPU reader. FUN_1417e29b0 binds the whole typeParams payload to the GPU: for type 1 (distortion), slot inst+0x200 ← desc+0x1b8. For type 0, inst+0x200 ← desc+0x3a8 (visuals) and inst+0x220 ← desc+0x630. Plus inst+0x250 ← desc+0x310 (default only). So power, like ratioXY, is evaluated on the GPU from the uploaded curve block. The scalar meaning is still HEURISTIC.
- Blend: the particle shader permutation table FUN_142450650 defines SFX_ALPHA_BLEND (perm word +0x205 bit0) and SFX_BLEND_ADD_NO_ALPHA (bit1). Trails have the matching TRAIL_BLEND_ADD_NO_ALPHA (FUN_1424e32c0). HEURISTIC, link not traced: blend_add selects the "add, no alpha" variant (ONE/ONE ignoring src alpha) and blend selects ALPHA_BLEND. I still have not found the D3D12_BLEND_DESC builder. The full-binary +0x48c scan was skipped as too noisy; the only real reader is FUN_1417e1e50 (vfx3i).
- Trails (PROVEN wiring only): emitter+0x9e0/+0x9e8 is a list of 24-byte trail-name strings. At runtime FUN_1418bfcc0 matches a trail object's name (trail+0x10) and registers trail+0x178 into the emitter instance list inst+0x3d0. TRAIL_MISC/TRAIL_DEGR/TRAIL_OFFSET/TRAIL_MISC_2 are only shader-constant name registrations in FUN_1421ca290 (globals 145025180/1450251a8/145025220/1450252c0). The CPU packing of these constants was not reached.
- VFX estimate remains ≈ 72%.

## vfx3k (shader disassembly + Ghidra, box only, 2026-10-27)
Done on the box only: no game launch, nothing touching EAC. Tools: dxc -dumpbin (/workspace/vfxsh_dis), dxil-spirv --glsl (/workspace/vfxsh_glsl/trail), Ghidra scripts DataRefs.java/Callers.java/DispScan.java (outputs /workspace/rea/vfxk_*.c). Docs only. Nothing new reached "proven + not yet implemented", so this round has no code or env flag.
- CB_PASS_TRAIL layout (PROVEN, reflection in trail.sdc 00020_0, bound at cb4): MAPPING@0, ALPHA_MAPPING@16, ALPHA_MASK_PARAMS@32, COLOR_FADEOFF@48, MISC@64, DEGR@80, PS_REG_TRAIL_EMISSIVE@96, PS_REG_TRAIL_CONST_COLOR@112, OFFSET@128, SDR_MAPPING@144, SDR_ROTATION@160, FLATLIGHTING[4]@176, MISC_2@240, TRAIL_PROJ_PARAMS@256 (blendMaskScale/Bias, height, yOffset, normalMapScale, parallaxScale, zFadeScale, angleFadeScale/Bias, flags).
- Shader use (PROVEN, trail 00001_2 GLSL). d is the vertex→camera distance, as in our existing EffectTrails implementation.
  - The two ramps: rN = sat((d-DEGR.y)*DEGR.x), rF = sat((d-DEGR.z)*DEGR.w).
  - Width scales by lerp(MISC.y,1,rN)*lerp(1,MISC.z,rF). Colour/alpha scale by lerp(MISC.w,1,rN)*lerp(1,OFFSET.z,rF).
  - OFFSET.x is subtracted from TEXCOORD5.y, the along-trail coordinate.
  - MISC.x is a squared-distance normaliser: sat(|v|²/(MISC.x+1e-4)).
  - MISC_2.yzw is an axis vector used in cross products.
  - Most frequent component is OFFSET.w (not decoded).
- CPU side (PARTIAL): the CB_PASS_TRAIL name table is built only by FUN_1421ca290 (DataRefs found no other refs to 145025000..+0x300). So constants are packed into a struct and uploaded by layout, not written by name.
- Trail desc layout (PROVEN, FUN_14183b3a0): renderPass +0x84, isThinTrail +0x88, visuals +0x90, trailParams +0x280 (FUN_141839b50), physics +0x3b0, degNear +0x3e8, degFar +0x400 (FUN_14181ec90, 24 bytes each), degradationRadius +0x418, optimizeBranchQuality +0x41c, nTrailData +0x420.
  - Runtime: FUN_14187cf20 copies degNear/degFar fields to the render-side struct (+0x1f8..+0x218).
  - FUN_1418c09a0 computes (x-degFar[0])/(degFar[1]-degFar[0]), i.e. a linear start→end ramp. So DEGR.x/w = 1/(end-start) and DEGR.y/z = start is consistent (HEURISTIC, the final CB pack was not traced).
- Blend, distortion power, exposure/light: no progress beyond vfx3j. Variant-bit readers were not reached within the time box.
- VFX estimate remains ≈ 72%. The existing trail DEGR implementation matches the shader; only the slot→sfx-field assignment stays HEURISTIC.

## vfx3l (shader pass sfx/distort/particles + trail packing, box only, 2026-10-27)
Disassembly: /workspace/vfxsh_dis/{pc,pg,dx_pc_dx12_sfx,dx_pc_dx12_distort}, GLSL: /workspace/vfxsh_glsl/{pc,distort}. Ghidra: /workspace/rea/vfxl_1.c. Docs only; nothing new proven and implementable.
- pc_dx12_distort is the scene distortion pass (CB_PASS_DISTORTION.DISTORT_PARAMS@0; 00000_2 PS writes the constant (0.5,0.5,1,1) = neutral offset), not particle power.
- pc_dx12_sfx is the world/decal SFX material (SFX_DISTORT_PARAMS, SFX_DISTORT_*_LAYER, SFX_BLOOM_INTENSITY, SFX_TEX_DISTORT*), not particle emitters. SFX_BLEND_ADD_NO_ALPHA variants belong to it, so they do NOT prove the particle blend_add mapping.
- Particles: the CPU path has CB_PARTICLES (view/proj, FPS_PARAMS[2]@368, SHADING_DATA[4]@400) and CB_PARTICLES_DATA (PARTICLES_TEXTURE_DATA[3072], raw). The GPU path keeps emitter params in the untyped f32 buffer PART_GPU_EMITTERS_PART_SHARED_DATA (PART_GPU_EMITTER_STATE is 404 B: data[3], simulationMask, render, alive, ...). Particle per-emitter constants are unnamed, so distortion power/ratioXY slots can't be read from reflection.
- Exposure: particles read the global COMMON_HDR_EXPOSURE_DIV_AVG_LUM (CB_COMMON @336). So exposure is a runtime camera value, not a per-effect constant. The fixed 1/250 stays HEURISTIC.
- Trail packing: readers of render-side +0x1f8..+0x218 in 0x141800000–0x142400000 are FUN_14187e8b0 (lerp of degNear/degFar fields vs +0xec.., i.e. LOD/state blend) and FUN_14189db00 (defaults: +0x1fc=255.0, +0x20c=0.5), plus unrelated hits at 141a1/141ef/141f2. The CB_PASS_TRAIL pack was NOT found. The DEGR slot mapping stays HEURISTIC.
- VFX estimate remains ≈ 72%.

- vfx3m: blend flags 0x20/0x400 proven (FUN_1417e1e50 → t8 PART_EMITTERS_SHARED_DATA → particles.sdc 00000_0); opt-in SM2_VFX_BLEND_REA=1. VFX ≈73%.
- vfx3n: blend-REA bench sanity (grenade brighter, kept opt-in); 0x60 record ↔ visuals field map (FUN_14178ce70). VFX ≈73%.
- vfx3o: curve slots = texture bindings; r0.y = 1/softDepth (not exposure); r4.y/z/w + r0.w decoded; colour-gradient LUT proven (unused in data). distortion.power and trail DEGR unresolved. VFX ≈73%.
- vfx3p/q: distortion.power and trail DEGR unresolved after 2 no-progress chunks → stopped. VFX ≈73%.
- vfx3r: in-game comparison prep — bench sheets in Captures\ingame_cmp, user refs go to Captures\ingame_ref.
- vfx3s: textures added (tex9), Fit reframed (hierophant renders, VAT d=0.48); Retail.exe direct launch relaunches via start_protected_game + EAC → no capture (route disproved); steam_appid.txt.check incident (created/deleted in game root, verified gone). Open: muzzle/candle 0 quads, chainsaw blood 0 particles.
- vfx3t: muzzle/candle were culled by degNear (camera < 2 m) → Fit keeps the eye outside degNear; isVisible=+0x88 is draw-only (FUN_1417f71b0) → hidden sources spawn children (SM2_VFX_HIDDEN_SPAWN=0 off); chainsaw blood is all-hidden in data → empty is correct. VFX ≈ 73%.
- vfx3u: in-game refs compared (side_by_side_*.png); BLEND_REA default ON (grenade core/sparks); open: muzzle flash missing, blood too bright/orange, grenade fireball/smoke too weak. VFX ≈ 74%.
- vfx3v: muzzle flash & grenade fireball were sub-frame lifetimes (SM2_VFX_STEP_HZ); chainsaw blood hidden-by-default (SM2_VFX_FORCE_VISIBLE debug) matches dark red; open: muzzle mesh untextured, smoke lit scale, blood size. VFX ≈ 77%.

## vfx3w (2026-10-27 KST) — VFX ~79%
Muzzle mesh named-material (mzl_geom) fixed; hidden blood = templates, compare vs finisher copies; emitScale trace inconclusive; MaskClip opt-in; lit-smoke light heuristic. See HANDOFF.md.

## vfx3x (2026-10-27 KST) — VFX ~80%
phantom_hands extracted (rings textured but dim: no bloom); grey columns fixed via exact particles.sdc alpha-clip (SM2_VFX_CLIP_EXACT); socket +Z fallback (SM2_VFX_SOCKET_FWD). See HANDOFF.md.

## vfx3y (2026-10-27 KST) — VFX ~81%
Mesh UV scroll (overrideAffixScroll, FUN_1425fd580) on; opt-in bench bloom from material bloomIntensity (SM2_VFX_BLOOM=1, heuristic). See HANDOFF.md.

### vfx3z (2026-10-27)
Bloom includes sprites + bloom point (heuristic value, threshold design from Bloom.BloomPoint); finisher blood darker (combine_material deferred, SurfLight 60 heuristic); LitScale 400 (runtime light, heuristic value); distortion buffer math from particles.sdc 00005_1 (power mapping heuristic); trail DEGR still heuristic (0 refs). VFX ≈ 79%. See HANDOFF.md.

_Niagara conversion: off the roadmap (2026-10-27), listed under ROADMAP → Possible future development._

## vfx4-3 (2026-10-27 KST) — browser polish
- Search: several words must all match (`GameCatalog.MatchesAll`, tested). Ctrl+F focuses the box, Enter previews the first hit, Down moves into the list, Esc or ✕ clears. When nothing matches, the status bar says so.
- Export: enabled only when something is selected. Label shows "Export N files…" and changes to "Exporting…" while it runs. Ctrl+E starts it.
### mtl1 (2026-10-27)
glt.chameleon REA'd (FUN_142576f30 + glt PS 03558_0) and implemented in sidecar + M_SM2 (offset packing heuristic). Sorcerer exported with baked cloth idle clip; lictor tentacle sway confirmed in UE frames. Hellbrute tone needs review. See HANDOFF.md.
- mtl1-A: SM2PhysicsSetup UE editor plugin (tools/ue/SM2PhysicsSetup) builds + installed; wired in ue_bulk import; in-editor realtime verify pending editor restart.
- mtl1-A verified in UE: lictor + sorcerer simulate live (RigidBody post-process ABP); ue_bulk re-import-after-rename bug noted in HANDOFF.
- mtl1-1: ue_bulk re-import backup = duplicate (not rename) + in-place reimport; fixes illegal private reference. PC Scripts/ue_bulk.py == repo (bak2 kept).
- mtl1-2: chameleon ramp in GL preview (synthetic bench verified; PC install pending with mtl1-3).

## mtl1-3 (2026-10-27 KST): customization parts
- Weapon skins decoded: firearm_skins_library → geomCustomizerRegions → geom_customizer_preset_firearm meshes.
- `--variant skin=` option, plus a browser ExportWindow part picker for armor and weapons. Tested on the bolt rifle and the tank marine.
- HEURISTIC: `main` is always on; skin material is applied since mtl1-47; attachments (purity seals) are merged into the export since mtl1-46.

## mtl1-4 (2026-10-27 KST): heuristics
- Cloth joint swing limits = atan(maxDist/segment), DERIVED. Plugin built, install pending an editor restart.
- Blood, distortion 0.04 and DEGR unchanged (HEURISTIC, see vfx4-1).

## mtl1-5 (2026-10-27 KST): chars_phys batch
- Six characters (calgar, chaplain, commissar, tech_priest, sorcerer_exalted, titus_assault) are in /Game/SM2_Work/Tests/chars_phys with PA and ABP, 0 errors. The live sim check needs clips (pending).

## mtl1-8a (2026-10-27 KST): Anim Only (Manny)
- The `--anim-only-manny` CLI flag and the Forge Lens "Anim Only (Manny)" checkbox write `_Manny.fbx` clips directly on SK_Mannequin (HEURISTIC world-delta retarget). `ue_import_manny.py` imports them at yaw 0 and is wired into `ue_bulk`. On titus run/idle/attack, the bone error against SM2 is a mean of 8–10 cm. Status ~85%: only biped humanoids are mapped, fingers fall back, and nothing else has been checked yet.
- mtl1-8/9 physics: in PhysTestMap, tech_priest mean is 23 cm (was 56; robe sag FLAG remains), sorcerer dash is 41–62 cm (was 69–137; no blow-up), and the other 4 characters are unchanged or better. Physics status ~90%.
- mtl1-10 Chaos Cloth (opt-in): 6 SIM sections converted on 3 characters. tech_priest robe sag fixed with Chaos. Other sections fall back to bones (FLAG: sim/section mismatch). ~92%.
- mtl1-11: SIM sections were X-mirrored; fixed in the exporter and the plugin, so Chaos now converts 16/16 cloths on 3 characters. Bone-mode chain-depth cap brings the tech_priest sag from 64 cm to about 10 cm. ~95%.
- mtl1-12: chaplain fixed (5.7 cm), Chaos framing unified, sidecar wind/collision offsets X-mirror fixed, gravity restored on the diag PAs. ~97%.

- 2026-10-28 mtl1-14 Manny axis fix: Mirror (S R S) flipped bone-local axes -> imported pelvis X pointed down (pitch -73 vs Manny +84), so Persona (skeleton translation retarget) twisted/floated. MannequinRetarget.BoneAxisFix (env SM2_MANNY_AXISFIX=0 to disable) pre-multiplies non-root frames by local Rz(180). Verified AnimPose: pelvis pitch +73/+84, root identity, run root along +Y. Persona capture blocked (editor in PIE, window stale).

- 2026-10-28 mtl1-14b: Manny verified via fixed-camera SIE side/front captures (tools/ue/mtl_fix/mcap3.ps1; camera not following pelvis, red line = ground z=0): run/idle/attack upright, feet z~11 (ankle), run faces +Y, AnimPose root along +Y. "play mode" error earlier was our own leftover SIE; pie.py ends PIE/SIE. Persona viewport camera not scriptable (opens inside mesh). Cloth: cpp10 (pinned/free pen split, CCD, thickness>=2) + clothwind.py (ChaosClothingInteractor.set_wind / WindDirectionalSource tag SM2Wind, Sequencer-friendly) + nocoll.py committed.

- 2026-10-28 mtl1-14c cloth: sorcerer Chaos+collision EXPLODED cloak with X-mirrored Spine capsule (r38/25, offset -20) -> sidecar collision.space=ueMesh (json .bak kept) fixes explosion (cloak still flares). pen free>1cm: coll 11-38 vs NoColl 69. Wind: interactor keyed by clothing asset name (SM2Cloth_<sim>_0), must be applied AFTER sim starts; sidecar localWindCm x1 explodes cloth (also with AirDensity 1.225e-6), x0.05 stable -> clothwind default scale 0.05. skirt_SIM interactor not found (TODO). Bone row recaptured (cl5bone). cloth_cmp_*.png rebuilt (bone/coll/noColl/wind). calgar/tech_priest wind row is an old run where wind was not applied.

## mtl1-14d (generic cloth collision)
- GENERIC: sidecar collision offsets are raw `Ue(v)` bone-local (`space="boneLocal"`); plugin only un-negates when `space=="ueMesh"`. No per-character json edits (sorcerer json restored from .bak1).
- Plugin AddCollision rest-fit: capsule/sphere radii shrink (floor 0.25) so free rest particles sit >= thickness outside. Log: `unnegated/restFitted/minRadiusScale`.
- Rebuilt ChaosCloth calgar/sorcerer/tech_priest. Calgar/tech_priest OK. Sorcerer cloak still fans out without wind (open); with wind x0.05 it hangs better.
- Wind: first cloth per mesh gets no interactor (skirt_SIM, skirt_v7_SIM ...) - open. Old exports lack localWindCm -> vel 0 (re-export needed).
- Captures: cloth_cmp_<char>_14d.png (box /workspace/mtl_fix).

## mtl1-14e (cloth binding / wind re-export)
- Re-exported calgar/sorcerer/tech_priest (Bin\SM2Extractor_14e) and swapped in only the physics.json (old one kept as .bakN). Sidecars now have localWindCm, simPositionsSpace=ueMesh and collision.space=boneLocal. Non-zero localWindCm: calgar cloak (300,-200,0); sorcerer cloak (0,0,-600), skirt (0,0,-400), tabard (100,200,100), beads (0,0,-1000). Tech_priest has none.
- ROOT CAUSE of "no interactor" (tech_priest skirt_v7_SIM, sorcerer skirt_SIM): the cloth is never simulated (get_num_cloths=3 of 4). Its render section (o6) is CHUNKED into 9 sections because the 8-bit bone index (256 bones/section) limit applies (r.GPUSkin.Support16BitBoneIndex=False; SM2 has ~2600 cloth helper bones). The bind lands on the parent chunk only, and the mesh rebuild drops it. The plugin now re-checks and rebinds after the loop (bindCheck/postBind/sections logs), but the rebuild still drops chunked binds.
- Generic fix options (need approval, project-level): (a) enable "Support 16-bit Bone Index" in the project's DefaultEngine.ini, restart and re-import the meshes; (b) plugin: before clothing, re-skin the render sections to the sim section's bones so they fit in one chunk.
- Sorcerer cloak (asset idx0) now simulates after the rebind. It still fans out with collision; the wind run (vel 0,0,-30) hangs better. Not solved.
- tech_priest wind penetration: not reproduced (14e run with collision 0-27, wind 0-17, no collision 51-229). Tech_priest has no sidecar wind (vel 0), so the earlier 156-401 rise was not caused by wind; most likely the different animation sample time.
- Penetration ranges, free particles >1 cm inside:

  | char | coll | noColl | wind |
  |---|---|---|---|
  | calgar | 0 | 209-272 | 0 |
  | sorcerer | 53-171 | 447-634 | 127-178 |
  | tech_priest | 0-27 | 51-229 | 0-17 |
- cloth_cmp_<char>.png regenerated with all 4 rows (bone / coll / noColl / wind, tag e14*). Previous versions kept as _bak3.

## mtl1-14f (ChaosCloth: 16-bit bone index, one render section per cloth)
- Approved project change: `[/Script/Engine.RendererSettings] r.GPUSkin.Support16BitBoneIndex=True` in the project's DefaultEngine.ini. **Meshes must be re-imported afterwards** (the existing render data keeps the chunked 8-bit sections). After that the large cloths (calgar cloak, sorcerer skirt, tech_priest skirt_v7) are no longer chunked and they simulate; WIND gets all 4 interactors.
- Plugin (cpp15e): (1) waits for the async skeletal-mesh build (FSkinnedAssetCompilingManager::FinishCompilation) before and after every cloth edit; editing the LODModel during an in-flight build gave the sorcerer spiky garbage. (2) Render slot match: exact name first, Contains only as a fallback (beads_REND vs beads_REND1). (3) **Only one render section is bound per cloth** (the matching slot with the most vertices). BindToSkeletalMesh maps one section per asset LOD, so a 2nd bind fails and the half-bound section exploded the mesh on the next rebuild. The other matching sections stay bone-skinned (logged: `extra render slots stay bone-skinned`). A failed bind is cleared off its section.
- **Restart the editor after a ChaosCloth rebuild** (CreateChaosCloth on a mesh already open/used in the level): the live component keeps the stale clothing render data until restart.
- Penetration (free particles >1 cm inside, t=7/9/11 s; worst free depth in cm). Particle counts are higher than in 14e because all cloths now simulate (calgar 3039, sorcerer 4819, tech_priest 3253):

  | char | coll | noColl | wind x0.05 |
  |---|---|---|---|
  | calgar | 0 (0.0) | 250-295 (27.4) | 0 (0.3) |
  | sorcerer_exalted | 236-334 (9.4) | 752-981 (28.8) | 271-297 (8.6) |
  | tech_priest | 131-139 (2.8) | 954-1138 (22.3) | 123-138 (6.5) |
- Sorcerer no longer explodes; cloak/skirt simulate with collision. Remaining penetration is shallow (<10 cm) vs no-collision (~29 cm).
- cloth_cmp_<char>.png regenerated (bone e14b / coll f15c / noColl f15n / wind f15w). Previous versions kept as _bak5. Sorcerer frame is small because its bounds include the large staff/horn parts.

## mtl1-15m (materials: BC5 spec metalness was wiped on export)
- User report: Hellbrute armour plates and the cc_sorcerer helmet face/collar looked flat dark grey.
- ROOT CAUSE (exporter, generic): AssetExporter zeroed byte 2 of BC5 textures to drop the reconstructed normal Z. TextureDecode returns **BGRA**, so byte 2 is **R**. On `MetGray(r)+Roughness(a)` spec masks (BC5, DXGI 83) that wiped metalness: every exported *_spec.png had R=0 since 37f43a1 (2026-10-26). Evidence: ch_rubric_helmet_05_spec DDS R (decoded with /workspace/mtl_fix/bc5.py) has metal ~1 on the gold trims, while the PNG R was 0.
- Why it looked grey: M_SM2 chameleon does `albedo *= lerp(ramp, 1, metal)`. With metal 0, the beige/gold metal parts of the paint slots (secondSpec + aniso + chameleon) were multiplied by the cyan/teal ramp -> dark grey-green diffuse.
- Fix: zero byte 0 (B) instead. Re-export is required: all exports since 37f43a1 (bulk, bulk_fix, mtl_cham, chars_*, re14e) have metal=0. Importer/shader unchanged.
- Verified: re-exported (SM2Extractor_15m) cc_hellbrute, cc_sorcerer, cc_sorcerer_exalted into /Game/SM2_Work/Tests/mtl15/*. Captures in Captures\mtl_fix\mtl15 (before = mtl_cham/chars_cloth imports): Hellbrute plates and the sorcerer helmet are metallic gold/teal instead of flat grey. Box composites /workspace/mtl_fix/mtl15_hb_front.png, mtl15_scf.png, mtl15_face.png.
- Tools: tools/ue/mtl_fix/mtl15imp.py (FBX + exported materials import), capf.py (bone-framed capture, fixed distance), mchk.py, findmesh.py.
- Note: new MIs render as the grey default for ~2 min after import (shader compile); wait before capturing.

## mtl1-15w (Sequencer wind source test, other-character mirror check)
- WindDirectionalSource test (tools/ue/mtl_fix/liveS.ps1, env SM2WINDSRC): calgar run, Chaos+collision, source spawned in the editor world before Simulate, no interactor wind. Pixel diff vs no-wind (>30/255): repeat no-wind run 1-2k px (noise), +X 1000 cm/s 14-20k px, -Y 2000 cm/s 10-27k px; the cloak visibly streams with the wind, nan=0, pen 0. So a level WindDirectionalSource (tag SM2Wind, keyable in Sequencer: Strength/Speed/rotation) drives Chaos cloth in the game world that Sequencer/MRQ renders. Not done: an actual MRQ render with keyed wind.
- clothwind.py `src` was broken (`Vector.rotation()` does not exist) -> MathLibrary.make_rot_from_x; Speed = |v|/250 (cloth wind = dir * Speed * 250 cm/s).
- Mirror check on characters not used before: cc_sorcerer (skirt, ribbons) and cc_chaplain (Skirt_front, Scarf, Skirt_L/R), exported with SM2Extractor_15m `--include-cloth-sim` into Exports\mirr15, imported to /Game/SM2_Work/Tests/mirr15. CreateChaosCloth DIAG: all 6 cloths hit=1.00, sim/section centre and extents identical, worstMatchCm 0, no mirror fallback used. Live SIE (rest pose, no clip in that set): no NaN, no spikes; chaplain pen 4 free particles (1.8 cm). Captures box /workspace/mtl_fix/mirr15_cmp.png, wind_src_cmp.png.

## mtl1-16 (Hellbrute plates bright cyan vs in-game dark navy)
- Evidence: HANDOFF 1b/aniso. The game multiplies **D** by the aniso intensity (4 on the hellbrute paint slots), and the second lobe is added at weight 1 **for direct lights only**. M_SM2 put both gains into **F0** (`sat(F0*AnisoIntensity)`, `sat(F0_2*SecondSpecGain*ExtraIntensity)`). Substrate F0 also scales the sky/reflection term, so the blue paint (albedo sRGB 42,129,173; second-lobe F0 = 0.976*albedo) became a cyan mirror.
- Fix (importer, generic): new master scalar `SpecGainToF0` (default 0). The gain applied to F0 is `lerp(1, gain, SpecGainToF0)`, so gains no longer reach F0/F90. Set it to 1 for the old look. Limitation: Substrate cannot split direct and IBL F0, so the second lobe still reflects the sky at weight 1.
- Verified: cc_hellbrute and cc_sorcerer imported from the same mtl15 export with the new script → /Game/SM2_Work/Tests/mtl16. Mean plate colour, front view with lights (mtl15 → mtl16): (58,105,116) → (43,84,104), so the paint is less cyan and less mirror-like. Box: /workspace/mtl_fix/hb16_face.png and hb16_cmp.png (rows: env 15/16, lit 15/16).
- Still open: in-game plates are about (25,30,40), which is much darker and desaturated, and the flesh is purple rather than pink. Neither the chameleon ramp (C1..C3 remove red) nor F0 explains that hue. The likely remaining factor is the untraced per-instance RGB `_783` (instance buffer dword 0..2, which multiplies albedo in PS 03558_0) and/or the lighting of the reference. The chameleon threshold packing is still a HEURISTIC (CPU packer not traced).

## mtl1-16b (sorcerer cloth penetration: solver substeps)
- Plugin: new `SetClothConfigProps(Mesh, "Prop=Value;...")`. It sets reflected properties on every cloth config of every clothing asset, in memory only (the ChaosCloth config classes are not exposed to Python). Tools: tools/ue/mtl_fix/cfgtune.py and pen16.ps1, which runs the variants live through liveC and ends SIE.
- Live SIE results on chars_cloth `_ChaosCloth`: free particles more than 1 cm inside, 3 samples, worst depth in cm.

  | variant | sorcerer_exalted (4819) | tech_priest (3253) | calgar |
  |---|---|---|---|
  | base (Subdivision 1, Iteration 1) | 243-359 (11.2) | 132-140 (2.8) | 0 |
  | SubdivisionCount 2 | 174-272 (11.1, later samples 4.8) | 59-192 (2.9) | 0 |
  | IterationCount 4 | 210-359 (12.5) | | |
  | Subdivision 2 + Iteration 4 | 164-279 (11.0) | | |
  | Subdivision 4 + Iteration 4 | 172-260 (6.8) | | |
  | Subdivision 2 + CollisionThickness 3 | 154-238 (10.7) | | |

  A repeated base run matches within 1-9 particles, so the runs are near-deterministic.
- Generic default (HEURISTIC): CreateChaosCloth now sets SubdivisionCount = 2 on ChaosClothSharedSimConfig. The sidecar can override it with `ue.chaosCloth.solverSubsteps` (1-8). Sorcerer penetration drops about 25%, and worst depth falls to about 4.8 cm after settling. Verified on a fresh mirr15 cc_sorcerer ChaosCloth: SubdivisionCount 2, live run stable (nan 0).
- Existing `_ChaosCloth` assets keep 1 until they are recreated with ue_chaos_cloth.py (or set it with cfgtune.py and save).
- Build: `RunUAT BuildPlugin -Package=Desktop\SM2_Work\build16`, then copy Binaries; the old binaries are in Plugins\SM2PhysicsSetup\Binaries_bak1 and _bak2. The editor needs a restart for a new plugin DLL.

## mtl1-16c (MRQ render with keyed wind)
- tools/ue/mtl_fix/mrqwind.py builds /Game/SM2_Work/Tests/mrq16 with these assets:
  - L_wind16: sky, sun, floor, calgar ChaosCloth, CineCamera, and a WindDirectionalSource (tag SM2Wind).
  - LS_wind16: 30 fps, frames 0-150. The camera cut is bound to the camera. The **wind component's Speed and Strength are keyed**: 0 for frames 0-40, ramping to Speed 8 (2000 cm/s) and Strength 1 by frame 60, then held to 150.
  - CFG_wind16: PNG 960x540, engine warm-up 32, render warm-up 8.
- It restores the previously open map without saving it.
- Render: a separate CLI process, hidden and at Idle priority, with no window:
  `UnrealEditor-Cmd.exe <uproject> /Game/SM2_Work/Tests/mrq16/L_wind16 -game -LevelSequence=… -MoviePipelineConfig=… -RenderOffscreen -windowed -Unattended -NoSplash -NoSound`
  It finished in about 60 s with 150 frames in Desktop\SM2_Work\Captures\mrq16.
- Frame-to-frame pixel change (>30/255): calm frames 20→21 and 30→31 changed 165 and 39 px; windy frames 100→101, 120→121 and 140→141 changed 3491, 3830 and 2600 px. The cloak streams sideways from about frame 60. So keyed wind drives Chaos cloth in an MRQ render. Box: /workspace/mtl_fix/mrq16_cmp.png (frames 10, 40, 60 / 80, 110, 145).
- Known: the `{sequence_name}` token was empty in CLI mode, so files are named `.0000.png`. The framing is tight.

## mtl1-17 / vfx 7 (2026-10-28 KST): degNear/degFar on mesh particles
- `EffectSprites.DistanceFadeK(def, dist)` = shared size/intensity/alpha/disp factors; sprites refactored to use it (no behaviour change).
- `EffectMeshParticles.Build(instance, view)`: lwiGeom instances now get the same degNear/degFar (size, colour+emissive intensity, alpha). Callers (bench, host, main window) pass the view. `SM2_VFX_DEG=0` turns all fades off.
- Data: 195 of 1045 lwiGeom `.sfx` files carry degNear/degFar. Bench (muzzle@0.02, grenade@0.3, 480 Hz): DEG=0 vs 1 identical (those emitters are outside their fade ranges) → no regression. Trails already had it (TRAIL_DEGR). SELF_DISTORTION render still parsed only.
- Tests 143/143 (new `DistanceFadeKSharedBySpritesAndMeshes`). Shots `/workspace/browser_run_shots/vfx17mesh/`.

## mtl1-18 (2026-10-28 KST): glt.chameleon CPU side fully traced (Hellbrute navy, part 1)
- Parser FUN_142576f30 passes a 0 default to every read: an **unset colour is (0,0,0,0) black** (was white HEURISTIC), unset offset1/offset2/fresnelPwr are 0. Colour text `[a,r,g,b]` -> stored (r,g,b,a) (FUN_142820d20: 2nd,3rd,4th,1st), so the ARGB reading was right.
- CPU packer FUN_1423ba150 (glt desc +0xb8 = chameleon; eps [0x143c8435c] = 1e-6): colours rows 10..13 in order C0..C3; o1 = clamp01(offset1), o2 = offset2 < offset1 ? offset1+eps : offset2, clamp01; row14 = (0, 1/(o1+eps), o1, 1/(o2-o1+eps)); row15 = (o2, 1/(1-o2+eps), fresnelPwr, -). Same thresholds as our former heuristic, so facing (N·V=1) is C3 (teal on the Hellbrute "blue" slot).
- Code: `ChameleonSpec` defaults zero + `ChameleonSpec.Ramp` (exact packer) used by GlPreview; import_sm2_materials.chameleon_ramp is the same packer. Tests: Chameleon_UnsetValuesAreZeroLikeTheEngineParser, ChameleonRamp_MatchesCpuPacker (145 pass).
- UE check: SM2Extractor_18 (Desktop\SM2_Work\Bin\SM2Extractor_18) -> Exports\mtl18\cc_hellbrute -> /Game/SM2_Work/Tests/mtl18/cc_hellbrute. Sidecar now has color0 [0,0,0,0]. Env capture plate mean mtl16 -> mtl18: front (52,118,139) -> (51,117,138), q3 (57,107,129) -> (55,104,126): only grazing rims darken. Box /workspace/mtl_fix/hb18_cmp.png (rows: lit16, lit18, env16, env18; the lit rows rendered grey = capture artefact, not material).
- Conclusion: every CPU material constant of the chameleon path now matches the exe, so the in-game navy (35,39,50) is **not** a material-constant problem. Remaining: the per-instance RGB `_783` (set=2 binding0 buffer, 108-dword record, likely TPL customization data; the Librarium "Hellbrute" inst_constructor template has no materialModifier and cc_hellbrute.tpl tints are all white, so it is probably white there) or the reference lighting (user screenshot = Librarium model viewer, librarium_character_helbrute.cls). /workspace/ingame_ref has VFX shots only, no Hellbrute. Next lead if needed: capture a RenderDoc/PIX frame of the Librarium viewer and read the bound _783 values.

## mtl1-19 — Browser VFX: SELF_DISTORTION + ROADMAP 1-6 time-box (2026-10-28 KST)
- SELF_DISTORTION render (EffectRenderer sprite FS): `uv = vUv + (texture(selfTex, vMask + frac(speed*t)).rg*2-1) * power * K * k`, with k = |dFdx(vUv)|/|dFdx(vMask)| clamped to [0,4] (maps the mask-space offset into atlas UV). power = SelfDistortPower.Sample(0.5,0.5). **HEURISTIC:** K = 0.04 (env `SM2_VFX_SELF_DISTORT_K`), because data power is 0.5..1 in quad units and K=1 warps the whole sprite. Off with `SM2_VFX_SELF_DISTORT=0`. The REA clip branch reads `texture(uTex, uv).a` (no double mask multiply).
- Bug fixed during the work: `LinearTexture`'s first upload bound the new texture on active unit 0, replacing the sprite texture (full-screen blue rectangle). The self texture is now resolved before any binds and bound on unit 3.
- Bench (fx_mgs_hub_enemy_spawn_med, /workspace/browser_run_shots/vfx19self): K=0 identical to off; K=0.04 small shimmer; K=0.3 visibly warps the glow.
- ROADMAP 1 (contrast): `additionalContrast` only acts with TINT_EXACT; grenade mean delta 0.001-0.007. Default grey smoke matches in-game grey (ingame_ref frame 2); TINT_EXACT adds a blue cast → both stay opt-in.
- ROADMAP 2 (trail tcIn): already opt-in `SM2_VFX_TRAIL_TC=1`; in-game shots show no visible bolter trail either → consistent with "faint by data".
- ROADMAP 4 (genOnFly REA): parsed into emitter desc +0x9b0/+0x9b8 (genOnCDT +0x9c0, genOnDie +0x9d0; FUN_1419b6360/9a50/bfdd0). Runtime FUN_1417e4e10: genOnCDT/genOnDie call FUN_14197fc70 once (one-shot spawn, flag 0x6c0|1, Die also |0x80); genOnFly calls FUN_1417e5a20(mode 0), which registers the child in the parent's attached list (parent+200, 16-byte slots, count +0x188, up to 12). Per step FUN_1417ecc30 walks that list and, per parent particle, spawns `FUN_1417f3770(child, dt)` = the child's own emission count (mode +0xe8: 3 → 0xfff = burst all, 1 → rate curve integrated over [t-dt,t] on emitter time +0x650). This matches the current model (burst for explosion children, own flow rate per living parent). Open: whether the dynamic-rate curve uses child emitter time (+0x650) or parent age (we use parent age).
- ROADMAP 3, 5, 6: skipped (time-box; stuck before).
- Hellbrute navy (part 1 of this brief): CPU chameleon fully REA'd and fixed in mtl1-18 (zero defaults + exact packer); env-plate means barely change (front (52,118,139)→(51,117,138)) → navy is not from material constants. Remaining suspects: per-instance RGB `_783` or Librarium-viewer lighting; next lead is a RenderDoc/PIX frame of the viewer.

## mtl1-20 — ROADMAP VFX 3/5/6 retry (2026-10-28 KST, box only; PC offline)
- **6 VAT animStage (FOUND, implemented):** lwiGeom serialiser FUN_1417a1ef0 puts `combineMtlAnimControl` (str @0x143645a20) at lwiGeom desc +0x178. Its group serialiser FUN_14179fdd0 has animStage (str @0x143645878) at +0x00, worldOffsetX/Y/Z +0x20/+0x40/+0x60, translucencyMultByMask +0x80 (all the same curve reader FUN_14179ea80), startColor/endColor +0xa0/+0xc0. Data: animStage is `splineOverride` 190x, `withoutOverride` 8x (corpus vfx3_raw/sfx). Code: `MeshParticleDef.AnimStage` + `StageAt(n)` = spline(age/life), clamped to [0,1]; fallback age/life when not overridden. The x-domain = normalised age is by analogy with sfxMtlOverrides (same union type); the evaluator is GPU-side, so that part stays labelled. Test `AnimStageCurveDrivesVatStage` (Assets tests 146 pass). worldOffset/translucencyMultByMask/start/endColor are parsed by the exe but not used by us yet.
- **5 Flesh covering (classified, not drawn):** FUN_141e08d30 (`coveringParams` @0x1436715a0, next to waterParams/slimeParams/mudParams/footprintTex) and FUN_141778460 (`coveringLayerNumber` @0x143644c48, intensitySplineMin/Max) show `em_covering_*` paint an intensity layer of the hit surface (`covering_simulation`, `coveringMask`, "covering intensity buffer!" @0x1436d7529). No particle geometry exists. Missing for a render: a host character with its covering-layer mask + the covering_simulation pass; the standalone effect bench has no host surface. Left as a note.
- **3 Guessed values (still PARTIAL):** new leads checked: shader-define tables FUN_1424e32c0 (TRAIL_BLEND_ADD_NO_ALPHA), FUN_142450650 (SFX_ALPHA_BLEND / SFX_BLEND_ADD_NO_ALPHA, mesh-sfx variants) and FUN_1424eca70 (BLEND_MODE_ADD / PREMULTIPLIED_SOURCE = UI/Scaleform shader). None is the particle PSO. Global scan for emitter+0x48c/+0x490 readers (0x141000000-0x142f00000) found only FUN_1417e1e50 (already known: add -> flag 0x20) and unrelated writers. So the blend_add D3D12 blend desc, ratioXY meaning (GPU curve slot), exposure (runtime COMMON_HDR_EXPOSURE_DIV_AVG_LUM) and distortion power scale stay HEURISTIC. **Unblock:** a RenderDoc/PIX capture of an in-game frame with a blend_add sprite (e.g. grenade smoke) to read the OM blend state, the emitter CB values and the exposure CB.

## mtl1-21 — Covering demo host in the browser VFX preview (2026-10-28 KST) — DEMO / HEURISTIC
- What: effects with `em_covering_*` stubs (blood 19, water 6, mud 3 in the corpus) get a built-in host surface in the VFX view: ComboBox "Covering demo: off / plane / sphere (heuristic)" (visible only when the effect has coverings; default plane) plus a PreviewNote saying it is a demo, not game-accurate. Bench: `SM2_VFX_COVERING=plane|sphere` (paints at 60 Hz between sheet frames; log prints coverHits/coverMax).
- `CoveringDemo` (Assets): 128x128 intensity mask on a plane (3 m, through the effect origin, normal = effect +Y) or sphere (r 0.6 m, effect origin on its pole). Paint: one splat 0.5 m x radiusScale at the origin at t=0 (the stub), and every particle closer than 5 cm to (or up to 30 cm behind) the surface splats 0.12 m x radiusScale, gain dt*20; scrubbing back clears. radiusScale = max `physics.cdt.coveringInteraction.radiusScale` of the effect's coverings (data, parsed since 1f9ffce).
- Material (EffectRenderer `CoverFs`): grey checker host; covered texels lerp to a per-kind albedo and gloss (blood dark red 0.7, water dark glossy 0.9, mud brown 0.3, slime green 0.8), Blinn-Phong spec exponent 2^(2+9 gloss), key light + effect lights.
- HEURISTIC (everything except radiusScale and the kind name): host shapes/sizes, contact distance, splat radii/gain/falloff, origin splat, kind styles, shading. The real path (host material `coveringParams` @0x1436715a0 / `coveringLayerNumber` @0x143644c48, `covering_simulation` / `coveringMask` shaders) is not reproduced.
- Test: CoveringDemoTests (project/surface-point round trip for both hosts, splat locality + radiusScale + clear-on-rewind, kind styles). Assets tests 150 pass, Extractor tests 42 pass. Screenshot: /workspace/mtl_fix/covering_demo.png (part_mag_ee_bolter_flesh, fx_mgs_cultist_blood_expl on plane and sphere; sheets in /workspace/mtl_fix/cov21/).

## mtl1-22 (2026-10-28 KST): cleanup, BC5 re-export, cloth backstop, MRQ names
- Cleanup (clearly unneeded test leftovers only; log Desktop\SM2_Work\Scripts\mtl_fix\cleanup_del2.log): PC files 32.3 GB (test exports, old Bin/Build outputs, old bench captures, crash dumps, test autosaves), UE 2489 test packages (Tests/*_bakN etc.) + 84 empty dirs about 18.6 GB, box 0.6 GB. Kept: uncertain items, 577 packages with external referencers, Original/, Work/, user .bakN.
- Item 1 (BC5 metal fix): new verb `LibSaber.SM2Extractor redecode-bc5 <dir>` (Bc5PreviewFix: decodes the exported DX10 .dds with the exporter's path, rewrites existing non-normal BC5 .png/.tga previews; tested). Bin\SM2Extractor_22 run over 25 export sets since 10-26: 449 previews fixed. tools/ue/mtl_fix/texreimp.py (scan/run, keeps texture settings, _bakN for non-Tests assets): 266 UE textures reimported, all under Tests (0 user-area). Box: /workspace/mtl_fix/mtl22/torso_spec_before_after.png (mean R 0 -> 73, B 25 -> 0). ChaosCloth recreated (SubdivisionCount 2, backstop): chars_cloth calgar/sorcerer_exalted/tech_priest, mirr15 chaplain/sorcerer; old meshes kept as _bakN. FLAG: mtl15 cc_sorcerer converts 0 cloths (same json as mirr15; mesh-side, open), bone fallback only. ue_chaos_cloth.py now deletes a leftover ObjectRedirector at the target path (L_wind16 referenced calgar), so referencers load the new mesh.
- Item 2 (plugin, generic, HEURISTIC): CreateChaosCloth paints BackstopDistance/BackstopRadius on free particles (may sink at most 1 cm behind the skinned position; R = clamp(2 x MaxDistance, 5, 50) cm; sidecar ue.chaosCloth.backstopCm overrides, <0 off). tech_priest same session, free >1 cm inside (t=7/9/11 s): new 102-150 (worst 2.79 cm) vs previous mesh 115-152 (2.86). Small gain, within run-to-run noise. Remaining penetration probably comes from skinned poses already inside the capsules (not proven). Captures: /workspace/mtl_fix/mtl22/tp_before_side.png, tp_after_side.png.
- Item 3 (MRQ): `{sequence_name}` was empty because the CLI got bare package paths. tools/ue/mtl_fix/mrqrender.ps1 passes full object paths (/Game/X/LS_a.LS_a); verified: 150 frames LS_wind16.0000.png ... (old empty-name frames moved to Captures\mrq16_before).
- Item 4: ROADMAP possible-future list gains "UE multi-game extraction".

## mtl1-23 (2026-10-28 KST): RenderDoc ground truth (nightly 1.47) applied
Evidence: box /workspace/rdc/NOTES.md and /workspace/rdc/<scene>/{nightly,deep} (frames hellbrute 68141, smoke 88302, plasma 101917, armory 128197, outdoor 137266). Code: `src/LibSaber.SM2.Assets/Effects/CaptureRules.cs` (pure, tested by CaptureRulesTests) cites each value.
- Blend (VfxBlendRules): CPU particles premultiplied One/InvSrcAlpha (PS 101818, smoke/outdoor; the vfx3m REA guess is now capture-verified); trails SrcAlpha/InvSrcAlpha, alpha Zero/One (CB_PASS_TRAIL); sparks blending off, write VELOCITY (eid 2086 (NOTES 12:10 blend list)); SFX meshes One/One, alpha Zero/One (plasma). Browser: sfx (translucent) meshes now One/One (env SM2_VFX_SFX_ADD=0 = old premul). UE test exporter header documents the mapping (sfx -> ADDITIVE, particles/trails -> TRANSLUCENT, sparks -> MASKED; ALPHACOMPOSITE would be the exact premul mode).
- SFX exposure compensation (plasma PS s33060 l.514-517): intensity *= lerp(1, COMMON_HDR_AVERAGE_LUMINANCE_CLAMPED, sfx[560].w). With exposure = key/avgLum this makes emissive brightness scene-exposure independent. Browser: sfx meshes use SfxExposure.Exposure(layer, 1/250, k); k = SM2_VFX_SFX_LUM_K (default 1, HEURISTIC: the td field behind CB_DYNAMIC_SFX byte 560 .w is not traced).
- Distortion (DistortionRules): particle PS 4996 writes mask·(0.5+2·off), mask = sat(10|off|)·depthFade, off = normal.xy·TEXCOORD0.w (strength x fade); post PS 5160 uv += dudv·0.04-0.02 -> shift 0.08·mask·off, max ±0.02. Browser: DistortionStrength default 0.04 -> 0.08 with ±0.02 clamp and mask-weighted shift; SELF_DISTORTION K 0.04 -> same formula (applying it in quad space is HEURISTIC). No depth fade in the browser (no scene depth).
- Tonemap (CaptureTonemap, LUT bake CS s1996): Hable F(x) = (x(Ax+CB)+DE)/(x(Ax+B)+DF)-E/F with [A,B,C,D]=[0.12,0.05,0.45,0.2], [E,F]=[0.02,0.08], x WHITEPOINT_SCALE 1.375385 (outdoor 1.343645), then sRGB + 32³ grading LUT (RANGE_LOG 14, GREY 0.43). Exposure: EXPOSURE_DIV_AVG_LUM x avgLum = 0.112-0.117 in all scenes -> key ~0.11 (HEURISTIC fit). Browser VFX sprites/meshes use it (SM2_VFX_TONEMAP=exp = old 1-exp); model preview opt-in SM2_PREVIEW_TONEMAP=1. UE: use a manual-exposure post process with the same curve (Filmic UE tonemapper differs; not wired, documented only).
- Hellbrute (item 3): the game armour colour is the ch_hellbrute_armor_02 texture: eid 2979 (PS 29540) reads no colorization/tintbymask/chameleon; CB_OBJ_MATERIAL_DATA MTLBLEND rows 2/3 = (.988,0)/(1,.102) = td variant **default**. GBUFFER0 armour bins (16-31,112-127,144-159) match the texture blue (34,115,150); the #02617F median mixed in the chameleon draws 2945/2956 (legs_01/arm_02, mean ~(13,91,116), CB colours = our td parse exactly, C0 black). Ours: colorization 0.502 and tintbymask are never applied (identity, no change needed); default slot = texture (matches). The brighter cyan in UE comes from the **blue** variant (chameleon + aniso intensity 4 + second lobe metal .99) on slots the frame draws with the default variant (armor_02/armor_01/arm_01/legs_02 blue are not drawn in 68141) - section mapping needs the .pak (HEURISTIC). **Refuted in mtl1-24:** all 31 sections are drawn and every blue section uses a chameleon PS. Generic browser fix: GlPreview now applies the variant base metal/rough {scale,bias} and lerp(albedoTint, metallicTint, metal) like PS 29540 / chameleon PS 03558_0 (metal feeds lerp(ramp,1,metal)). Images: /workspace/mtl_fix/mtl23/hellbrute_before_after.png.
- Bench (box, llvmpipe): /workspace/mtl_fix/mtl23/vfx_before_after.png (before = SM2_VFX_TONEMAP=exp SM2_VFX_SFX_ADD=0 SM2_VFX_DISTORT=0.04 SM2_VFX_SELF_DISTORT_K=0.04). Shaders compile (vfxbench + glbench synthetic). Mean diffs small (0-6/255); muzzle sfx mesh 5.2.
- Armory customization lead: see HANDOFF mtl1-23.

## mtl1-24 (2026-10-28 KST): Hellbrute sections mapped to capture draws — variant assignment already correct
- Method: PC `SM2Extractor_22 extract --filter *cc_hellbrute.tpl*` -> box; `tools/sections_dump` (TemplateMeshReader.LoadRaw) lists every section (pool, texture, variant, index count, verts, bounds); `tools/rdc/map_sections_to_draws.py sections.tsv nightly/draws.json --min-eid 2800 --max-eid 3900` matches by exact index count.
- Evidence (capture 68141, GBUFFER eids 2870-3464): **31/31** exported sections match a draw with a unique index count (2979 = armor_02 default 102480, 2945 = legs_01 blue 1062, 2956 = arm_02 blue 1332, 2870 = inner_01 default 37998, 3027 = body_01 default 15387, ...). The game draws the "hellbrute"/"hellbrute_pilot" nodes, the same set our LOD0 export writes (VisibleGeometry), in the same order as the sidecar slots hellbrute..hellbrute_26.
- Pixel shader per variant: every `blue` section -> chameleon PS (19112: legs_01/arm_02/arm_01 blue; 22961: armor_01/armor_02/legs_02/inner_01 blue); `default` -> 29540 (inner_01 default 18688, cables/arm_02 default 19264); skin 21380; eyes 23521; ruby 33973 (blended). The mtl1-23 note "armor_01/arm_01/legs_02 and armor_02 blue are not drawn" was wrong (they are at eids 3130-3452; the earlier list stopped at 3027).
- Rule (exe/capture evidence): variant = the section's own shading material name (`ShadingMaterialMaterial`, else default), per section, per texture's td — exactly what TemplateMeshReader already does. **No exporter change.** UE check: /Game/SM2_Work/Tests/mtl18/cc_hellbrute has UseChameleon on exactly slots 6, 8, 10, 17, 19, 26 (pilot_5), 30 = the blue sections.
- So the remaining colour difference (ours brighter/cyan on the blue panels) is shading/lighting, not section->variant: candidates are the chameleon/aniso/second-spec response in M_SM2 under UE lights and the per-instance DYNAMIC_GLT_BUFFER rgb (not dumped). HEURISTIC until a capture with that buffer and the chameleon draw CBs (3419-3452) is dumped.
- Image: box /workspace/mtl_fix/mtl23/hellbrute_ue_after.png (capture final | UE lit 3/4 front | UE base colour, mtl18 import re-rendered with ue_cap_lit fit view=ref; before = after) + the 31-row mapping table. PC captures: Captures\mtl_fix\mtl24\{lit,base,ref,refbase}.

## mtl1-25 (2026-10-28 KST): official Mod Tools — install blocked, public-docs comparison
- Install: Steam appid 3856330 (free sub 1356912, FreeToDownload=No). `steam://install/3856330` opened the dialog but no appmanifest/download after 13:08 KST → needs a user click in Steam. saber.games toolset page has no direct download (images only); Nexus mods/280 needs login. No DLL/ILSpy analysis yet.
- Public docs (spacemarine2-modding.prismray.io, 76 pages, full-text grep): **no** mention of chameleon, anisotropic, second spec, colorization, DYNAMIC_GLT, .sani or sfx internals. Those still need the tool DLLs.
- Confirmed by docs (first-party text): submaterial/texture override defaults = material name minus `_matN`, submaterial `default` → matches our variant rule. colorSets→glTF COLOR0, extraVertexColorData→COLOR1, ARGB, one byte per channel, set n = channels 4n..4n+3.
- USF material schema (mesh_metadata): `layers[]{albedo_tint, subMaterial, textureName, type, isVisible, blending{weights{colorSetIdx,multiplier,useFromLayerAlpha,useFromMaskChannel}, heightmap{colorSetIdx,invert,softnessMultiplier,useHeightmapBlending}, upVector{angle,enabled,falloff}, normalAdditiveBlend}}`. Maps onto our binary `MaterialBlending` (weightMultiplier, useLayerAlpha, texChannelBlendMask, heightmapSoftness, useHeightmap, blendNormalAdd) — HEURISTIC name mapping. **Gap:** exporter uses only the first non-empty layer (`DiffuseLayer`); per-layer subMaterial/tint and weight blending are not exported (relevant to Calgar cape stripe lead).
- Cloth (docs): "if SIM mesh does not cover the REND mesh completely (influenceDistance), the cloth is skipped from simulation entirely and appears rigid". Our preview binds per vertex with (maxInfluenceDist/d)^distanceRemapPower falloff. Mismatch noted; not changed (docs do not define "completely"; no exe evidence).

## mtl1-42 physics/materials batch (2026-10-28 KST): Calgar cape stripe = detail tiling, 60 Hz Calgar clips, PA assign, cloth bone cap 4096, CDT sweep
- **Cape red/white stripe (data; tiling rule HEURISTIC):** `ch_calgar_coat_01.td` glt has `detail = { density = 16, scale = 0.75 }` with `tex.det = "cloth_04_det"`. M_SM2 sampled the weave detail normal at UV×1 and lerp-replaced the normal, which magnified the weave into diagonal stripes over the whole cape. New `SpecLayers.Detail` → `MeshPart.Detail` → sidecar `slots[].detail {density, scale}`; M_SM2 gains `DetailDensity` (Detail UV = UV0 × density) and `DetailScale` (blend = saturate(mask.g × scale)), defaults 1/1. Retail survey (18690 .td): 1535 glt.detail blocks, density 5 (589×) / 1 (326×) / up to 20, scale absent in 666 (default 1 assumed). The CPU packer/shader use of density was not located (shader GLSL has no symbol names) → HEURISTIC. UE A/B (scratch `/Game/SM2_Work/Tests/mtl42_detail`, `<MI>_off` swaps, no project asset changed): stripes gone at 16/0.75; cape red-channel high-frequency energy 15.9 → 12.5 (side view). Image: box /workspace/mtl_fix/mtl42/m42_detail_ab.png (top density 1, bottom .td values), PC Captures\mtl_fix\mtl42\det_{d1,td}\*.png. Existing imports pick it up on the next re-export + material import.
- Second cape lead (not shipped, HEURISTIC): cloak_REND_01 as decoded samples texture padding at the lower corners; V-flipped it maps cleanly (alpha-cut 3.9 % → 0.4 %), cloak_REND_02 is better as decoded. UvScaling 0:1, tiling 1 — cause unknown. Sheet: /workspace/mtl_fix/mtl42/m42_cape_sheet.png.
- **Calgar 60 Hz:** `/Game/SM2_Work/Tests/physics/cc_calgar/Anims60/…start_run_calgar` 398 frames / 6.633 s (Anims: 199 frames). `ue_bulk.py` already imports at `physics.json` `baked_rate`, so new imports are 60 Hz automatically. Capture: /workspace/mtl_fix/mtl42/m42_ue_calgar60.png.
- **PA assign:** `ue_sm2_physics.py <json> <mesh> <folder> assign` sets `PA_<mesh>_sm2[_N]` only when the mesh slot is empty or already ours (a user PA is kept); `physasset` now also assigns. cc_calgar/cc_titus: None → PA_*_sm2 (meshes backed up as .uasset.bak1); verify: 0 missing jiggle/helper bones. The plugin's `SetupFromJson` already assigned the PA at runtime.
- **Spiky hem:** `PhysicsBake.MaxBonesPerCloth` 1024 → 4096 (calgar cloak_SIM has 1133 free particles): helpers 1294 → 1403, cloak fit error 3.9 → 0.0 cm. Edge-stretch > 2× unchanged (baked 0.157 % vs live 0.191 %): the late-clip spikes are in the sim itself (ribbon_R stretch 8.3, ribbon_02_L 7.9, cloak 2.2; no self-collision/drag, HEURISTIC).
- **Jetpack/CDT sweep** (62 tpls with cls physics): 15243 shape bone refs, 14820 resolved (97.2 %), 1 via locator, 423 missing. Raider jetpack bones resolve as template objects. Missing: suffix/typo variants (`CENTREs`, `BACKupS`, `knee_LS`, `ARM2Rs`…; already handled by the name remap/reject) and names in no tpl/markup (`LEG_L_Gun`, `Hip_gun_CDT`, `Backpack_cl_cdt`, `weapon_box`, `wpn_grapling_hook_01_chaos` ×36, cine_calgar `Backpack_CDT_L`) — likely on attached weapon/equipment templates (HEURISTIC, unresolved). Output /workspace/mtl_fix/mtl42/cdt_sweep.txt.
- **Krak textures:** plasma_clouds_menu_01 + scorch_meltagun_sml1 fetched (8 pct + 2 td) into /workspace/vfx3_raw/texall/tex; CPU reference not re-rendered yet.
- **`ue_cap_lit.py`** waits via `AutomationLibrary.finish_loading_before_screenshot()` (shaders + textures compiled and streamed; `nowait` skips).
- Tests: SM2.Assets 203 pass (+1 large-cloth bone cap, +1 detail parse), Extractor 42, LibSaber 5, python.

## mtl1-42 (2026-10-28 KST): VFX black squares + sideways fly-by, Fit camera, cloth SIM shells in preview, export In place, file list columns / Show all files
- **Black squares (HEURISTIC):** GPU streak emitters classify as `Spark` (opaque + alpha test). When the texture has no alpha coverage (e.g. `part_rs_glow_01`, `flare_common`: alpha ≈255 everywhere), the whole quad passed the alpha test and drew as a black/dark square (fx_mgs_zoanthrope_charging at 1.55 s). `VfxDrawKinds.Resolve(kind, textureHasAlpha)` now draws such sparks as `CpuParticle` (normal blend, colour-driven). Used by the browser (`EffectRenderer.ResolveKind`), `VfxFrameExport` and `VfxUeExport`. Disable with `SM2_VFX_SPARK_ALPHA=0`. Scan (all .sfx with textures on the box): **59 emitters in 48 effects** affected (229 more spark emitters have textures missing on the box, not judged). Dark-tile bench: **19 → 2** effects (the 2 left are legitimate: hierophant_bomb_ee splatter, exalted_sorcerer_ground `smoke_dark` trails); control set 6 → 6. Image: box /workspace/fix42b_shots/cmp_charging.png (top before, bottom after).
- **Sideways motion (root cause):** `EffectInstance.StandaloneMotion` (the preview fly-by that moves an effect so its trails draw) ran for every effect with trails and snapped back every 0.4 s, so particle-attached trails (`trails { partATTACHED_TRAIL { name } }`) made the whole effect slide left/right (fx_mgs_zoanthrope_lightnings). It now flies only when the effect has root trails (`EffectDefinition.RootTrails`, i.e. trails not attached to a particle). **108 of 289** trail effects now stay still. Disable the fly-by entirely with `SM2_VFX_FLYBY=0`. Image: /workspace/fix42b_shots/cmp_lightnings.png.
- **VFX camera auto-frame:** `EffectCamera.FitAll/FitBox` frames the simulated bounds (sampled over the clip) when an effect opens; **Fit** button + **F** key re-frame.
- **cc_titus white tabard (LOD0 only):** cloth `*_SIM` shells (`_SIM(_|$)`, carry `__default_material__`, the game never draws them) were visible because the LOD0 "all parts" path adds every level with visible parts and the SIM shells live in the LOD5 (titus/calgar) / LOD8 (sorcerer) pool. `TemplateMeshReader` now hides them (reason `clothSim`, toggle tooltip explains). Before: titus 6, calgar 8, sorcerer_exalted 4 visible SIM shells → 0. The UE export already hid them (`--include-cloth-sim` re-shows).
- **Physics drift with In place:** `MeshPreviewScene` now steps physics after the In-place rebase / RemoveRootMotion, so cloth/seals simulate in display space. UE bake samples `ClipPose`, unaffected unless the export In place option is chosen (then it bakes in place consistently).
- **Export In place vs Root motion:** `AnimationClip.InPlace` → `ClipPose.Apply` removes the ROOT (else first parentless bone) horizontal XZ world travel since frame 0 from parentless locals (height + turns kept; same as the preview's In place). Browser Export window "Root" combo (Root motion (default) / In place), CLI `--anim-root <motion|in-place>` (export only), applies to normal, multi-clip and Anim Only (Manny) exports. Default unchanged (root motion).
- **UI:** mouse wheel over a closed Anim/LOD/Part combo no longer changes the selection. File list: resizable columns (header splitters; TYPE/SIZE/ARCHIVE widths persisted, PATH takes the rest), full-path tooltip, middle ellipsis (`PrefixCharacterEllipsis`; Avalonia 11.2 has no PathSegmentEllipsis — it throws at startup), pane split persisted, tab underline, section labels, dimmed secondary text. **Show all files** (persisted, `BrowserSettings.ShowAllFiles`): off = only previewable types (`AssetClassification.IsPreviewable`: .tpl, .pct/.pct.resource, .sani, .sfx) in folder view and search when the type filter is All; status bar reports "N auxiliary files hidden". Screenshot: /workspace/fix42b_shots/ui_after.png.
- **Browser state memory:** settings.json now also keeps the expanded folder-tree nodes, selected folder and file (with archive), file list scroll, search text, type filter and Include subfolders (plus the column/pane widths above); restored when the last game folder reopens (saved scroll kept while the selected file stays on screen; the restored row gets keyboard focus). Clicking a row or moving with Up/Down previews it immediately (Ctrl/Shift-adding rows previews the newest one). Default column widths TYPE 72 / SIZE 64 / ARCHIVE 112, file pane 1.35*. Checked under Xvfb with a synthetic pak: /workspace/fix42b_shots/ui_restore.png, ui_arrow.png.
- Tests: SM2.Assets 218 pass (+17 Mtl42Tests), Extractor 43 (+1 `--anim-root`).

## mtl1-41 (2026-10-28 KST): sparks_05 flag outlier PROVEN (isAlphaKill), 342 effects with 0 emitters fixed
- **Blend flag 0x20 rule (PROVEN, exe):** FUN_1417e1e50 (m20_b2.c l.395) sets 0x20 only if blendType == add (desc+0x48c) AND (particle type != default (desc+0x1a8) OR isScreenSpace (desc+0x1b0) OR !isAlphaKill (desc+0x1fa)). Offsets from the typeParams.default field serializer 0x141793d40 (isScreenSpace +0x0, isAlphaKill +0x4a, visuals +0x1f8 = desc+0x3a8, so struct base desc+0x1b0). Other bits read there: 0x8000 = default && isScreenSpace, 0x100 = desc+0x6e9, 0x2000 = desc+0x77c, 0x200 = softDepth sign (desc+0x3a0); 0x400 unchanged. isAlphaKill default False from Mod Tools meta_desc.ps (`__defaultValue = False`). So the outlier krak/frag sparks_05 (add, `isAlphaKill = True`) correctly reads 0x2500: additive colour but output alpha kept (premultiplied blend), not alpha 0. New `EmitterDef.AlphaKill/ScreenSpace` + `EffectBlendRea.Flags(EmitterDef)` (CPU emitters only; GPU emitters keep add → 0x20, they do not go through FUN_1417e1e50); used by the browser (alphaMul + uMode) and `VfxFrameExport` (`flags`/`outAlpha`, so UE and the CPU reference follow). isScreenSpace default False is assumed (no capture slot has 0x8000).
- **Check:** `tint_check.py` now also compares the predicted 0x20/0x400 bits: plasma 101917 (both shared buffers 4996/5085) **123/123 consistent, 0 contradictions** (was 122/123), slot 56 sparks_05 predicted 0x400 by all 3 candidates; 68 slots have a single predicted value. Output /workspace/mtl_fix/mtl41/tint_check.json.
- **Parser gap (INFERRED from data, list loader not traced):** an untyped `sfx_group.list` entry keyed by a node type name with no file of that name (`sfx_part` 360×, `sfx_trail` 54×, …) is now an inline node of that type (`EffectLibrary.Expand`), and an untyped emitter keyed exactly `CPU_EMITTER`/`GPU_EMITTER` takes that type (`EffectDefinition.Walk`; 211 GPU_EMITTER were simulated as CPU). `--vfxemitters`: 18189 → **21343** emitters (GPU 2478), 0 unresolved / 0 failed, 312 effects without particle emitters (lights/meshes/trails only; list via the new 4th arg). Example fx_krak_grenade_explosion: 0 → 59+ emitters.
- Images (box CPU reference): /workspace/mtl_fix/mtl41/vfx/m41_sheet.png (krak 150 ms, 400 ms new vs old flags, |diff|×4: mean 0.014/255, max 36, 701 px > 2; the 2 batches with missing textures plasma_clouds_menu_01/scorch_meltagun_sml1 omitted, not yet fetched). No UE re-render (UE consumes the frame json flags, so the change carries over).
- Tests: SM2.Assets 201 pass (+3 InlineNodeTests), Extractor 42, LibSaber 5, python 15.

## mtl1-40 (2026-10-28 KST): exposure from avgLum, textureTint verified, gated bilinear distortion, UE frame sequences
- **Exposure (PROVEN data, HEURISTIC scale):** particle VS 390 (GLSL l.490-492 unlit, l.788-790 emissive) and VS 248 multiply by COMMON_HDR_AVERAGE_LUMINANCE (CB_COMMON[22].y) and the final tonemap by EXPOSURE_DIV_AVG_LUM, so a particle displays c·I·Key, Key = E·avgLum. Key per capture: hellbrute 0.1124, smoke 0.1146, plasma 0.1134, outdoor **0.0737**, armory **0.0245**. The old claim "0.11 in every scene" was wrong (corrected in CaptureRules and box /workspace/rdc/NOTES.md). New `CaptureScenes` / `CaptureExposure` (CaptureRules.cs): frame exposure = Key(scene) × BenchScale, BenchScale = (1/250)/Key(smoke) (HEURISTIC: the CPU colour → vertex scale is untraced; smoke stays exactly 1/250). `--vfxue` gets `SM2_VFX_SCENE` (default smoke; plasma/hellbrute/outdoor/armory; `bench` = the old convention). Whitepoint follows the scene, non-adaptive lights use the scene avgLum (previously 1 → /4.82 at default), SFX emissives use lerp(1, avgLumC, k)/avgLum (k = 1 → unchanged). `SM2_VFX_EXPOSURE` still overrides. Frame JSON gains `"scene"`. Outdoor frag 150 ms: exposure 0.002574 (×0.643), wp 1.343645; mean display 67.8 vs 71.0 (smoke). The histogram auto-exposure CPU mapping is still untraced.
- **textureTint=0 mix VERIFIED** against the plasma 101917 PART_EMITTERS_SHARED_DATA buffer (6 float4/emitter: [0].z tint, [1].rgb key, [1].w contrast, [3].x flags). New `ForgeLens --vfxemitters <sfx> <out.json>` (18189 emitters) + `tools/vfx/tint_check.py`: 123 keyed slots ok 123 / mismatch 0 / missing 0, 58 unambiguous (47 tint 0). An absent textureTintControl reads 0 in the capture (part_mg_aux sparks_long_A_01, no key in part_mg_aux_grenade_expl.sfx l.1732, slots 30/31/37 = 0). Blend flag 0x20 consistent 122/123; open outlier part_mg_frag_grenade_expl/sparks_05 (parsed Add, flags 0x2500). Output /workspace/mtl_fix/mtl40/tint_check.json.
- **Distortion resample (gate PROVEN, bilinear INFERRED):** PS 5160: uv += dudv·0.0400391 − 0.0200391, applied only if the DUDV is non-neutral AND the DUDV alpha at the shifted uv is 0 (cleared 1, distortion particles write alpha 0; plasma dudv.png: all 4231 shifted px inside the 21726 alpha-0 px). Both samples use PS_SAMPLERS[4], which is used with textureGrad and for the light atlas, so it filters (descriptor not read → INFERRED). `DistortionRules.Covered/Neutral/PostScale/PostBias`, CPU ref `vfxframe_scene.distort_resample`, UE master writes footprint B = 1 and the PP gates with 4 texel-centre taps (`DistortSize`) + filtered SceneTexture. Rule scene: UE **3.70 / 11.85 / 24.0 px** vs expected 3.69/11.91/24 (was 4/12/24 nearest); the CPU ref gives 3.60/11.85/24.0. Gate check: distort2 is unshifted beyond x+36 px (target outside the 120 px quad) in both UE and the CPU ref. UE vs ref in the distortion rows: mean 0.04-0.06/255. All display patches ≤ 0.41/255.
- **UE frame sequences:** `--vfxue name@start:end:step` (≤240 frames; one shared textures dir), `tools/ue/vfx_sequence.ps1` (render+export every json), `tools/vfx/sequence_sheet.py` (sheet UE/ref/|d|×4, GIF, per-frame stats). UE `/Game/SM2_Work/VfxUe` fresh (old → `VfxUe_bak3`), Exports `Exports\mtl40\{all,ue}`. UE vs CPU ref mean /255 (PSNR): volkite_const_default (2 distortion batches) 0.1-0.9 s: 0.40/0.68/0.61/0.72/0.65 (31.98-34.77 dB); frag grenade 0.05-0.45 s: 0.19/0.89/0.89/0.56/0.38 (30.86-46.88 dB); outdoor frag 150 ms 0.92.
- Images: box /workspace/mtl_fix/mtl40/seq/{volkite,frag,outdoor_frag}_sheet.png + .gif + .json, ue/sm2_vfx_rules.png, ref/rules_ref.png, dudv_alpha0.png.
- Tests: SM2.Assets 198 pass (+3: scene keys, frame exposure, distortion gate), python +4 (test_vfx_mtl40).

## mtl1-39 (2026-10-28 KST): UE scene rebuilt with mtl1-38 masters, RGBA8 VFX textures, light RE (spec flag / sfx blend still HEURISTIC)
- **UE rebuild:** fresh `/Game/SM2_Work/VfxUe` (old → `VfxUe_bak2`), masters rebuilt on import (PROVEN tint/contrast, falloff exp 1). Exports `Exports\mtl39\fx`, captures `Captures\mtl_fix\mtl39\` (BC) and `...\mtl39\tc\` (RGBA8). Rule scene unchanged: all patches ≤ 0.41/255.
- **Texture compression fix (`ue_vfx_frame.py`):** the PNGs are already decoded game BCn; UE re-encoding to BC1/3 adds a second block error that additionalContrast (cine_bomb 41×) amplifies (fireball blue +11/255). VFX textures now import as uncompressed RGBA8 (`TC_EDITOR_ICON`, sRGB + mips kept); `SM2_UE_TEX_BC=1` restores BC. Mean |UE − CPU ref| /255 (UE36 → UE39 BC → UE39 RGBA8): grenade 2.57 → 1.29 → **0.89** (p99 23), acid 1.10 → 1.12 → **0.30** (p99 5), cine_bomb 18.32 → 4.37 → **0.40** (p99 8). Sheet `/workspace/mtl_fix/mtl39/m39_sheet.png` (UE36 / UE39 BC / UE39 RGBA8 / CPU ref / diff×4).
- **Spec-map flag writer (OBJ_MTLBLEND_LAYER_CONST[15].x):** light RE without full auto-analysis (`/workspace/mtl_fix/mtl39/re/`: `xref.py` .pdata+capstone RIP xrefs, `immscan.py`, `callers.py`, Ghidra `-noanalysis` + `DecompFn.java` on Convert.exe, `-readOnly -noanalysis` on retail proj3). New evidence (PROVEN facts, not the rule): retail texture-usage channel table 0x144ccee10 (27 names, idx 0 MD … 21 MSCRGHAO, 25 MMRAOT, 26 MTRAO); `FUN_14267baf0` parses a usage string split on `_` into `mask |= 1<<idx`; `FUN_14267b210` = td usage serializer (hdr_scale only for MLM|HDR); `FUN_14267aaa0` mask → name. OBJ_MTLBLEND_LAYER_CONST / CB_OBJ_MATERIAL_DATA strings only in CB-registration code (retail 0x1421ca290…, Convert 0x14116c–0x1411a0) ⇒ CB filled by layout. No function tests MSCRGHAO/MMRAOT/MTRAO/MSCG immediates outside the shader-permutation cluster (only 0x142a1e600, a bit table). Convert.exe mtl-suffix path (`FUN_14151a6b0` ← 0x140e35c90, `_mtlSetSplitMaterialImpl` FUN_1416f77c0 ← 0x1416ee416) = material-library suffix deduction, not the flag. **Writer still NOT FOUND → UseSpecMap stays HEURISTIC.**
- **SFX material → blend define:** PROVEN pieces: sfx define slots (0x142450650) bit0 SFX_ALPHA_BLEND, bit1 SFX_BLEND_ADD_NO_ALPHA, bit2 SFX_IS_DECAL; game_material `shaders.sfx` section parser `FUN_1426212d0` (pass 0x38, caller 0x142619ef0): blendMode absent/"none" 0, "add" 1, "blend" 2, "premul_a" 3 (+ tint packed RGBA8, emissive.intensity/adaptiveIntensity/bloomIntensity, writeDepth, tex); entry stored 0x28-stride (manager+0x150), serialized by 0x1426192c0. The enum → define-bit consumer was not located (no direct reader found) ⇒ "add → ADD_NO_ALPHA, blend → ALPHA_BLEND, premul_a → neither" remains **HEURISTIC**; code unchanged. (mtl1-44: superseded. FUN_1426212d0 is the game_material parser; the td sfx loader is FUN_1425fd580 and the selector FUN_142451840 is PROVEN.)
- **Particle 0x400 CPU path:** emitter parser 0x14178f3b0: `blendType` at +0xe4 ("blend" 0, "add" 1, "blend_add" 2), `isBlendAlphaMultiply` bool at +0xe8 (PROVEN). No function touching +0xe4/+0xe8 with 0x400 found (2 unrelated hits) ⇒ the shared-flags builder and whether vertex colour carries age alpha stay **HEURISTIC**; unchanged.
- Tests: SM2.Assets 195 pass. Browser builds.

## mtl1-38 (2026-10-28 KST): shaders-1 integrated, static-shader formulas applied (HEURISTIC → PROVEN)
- shaders-1.patch applied (SHADERS.md, `tools/shaders/*`, RE_NOTES "Shader bundles" section).
- **LUT apply PROVEN** (hdr PS 44/47): `CaptureLut.Coordinate` + `GradingLut.Lookup` texel centre = PS uvw exactly (new test `CoordinateMatchesHdrPs44Uvw`, 6 values, 1e-5). Relabelled in CaptureGrading.cs, vfxframe_scene.py, vfxframe_ref.py, ue_vfx_frame.py TONEMAP. Not modelled: PS 47 dither, 64512 pre-exposure clamp (no effect after sat).
- **textureTintControl + additionalContrast PROVEN** (particles PS 281): `rgb = mix(tex·V·boost, t·V·tex.a, t)`, `boost = 1 + sat(1 − |key/255 − tex|)·additionalContrast`. Browser `SM2_VFX_TINT_EXACT` / `SM2_VFX_CONTRAST` now **default on** (`=0` = old). UE master material (`ue_vfx_frame.py` PARTICLE) and the CPU reference switched from the luminance-lerp heuristic to the PROVEN form. Visible change: t = 0 now keeps texture colour (cine_bomb fireball orange instead of grey; grenade core warmer; acid unchanged) — CPU ref mean |diff| grenade 2.4, acid 0.0, cine_bomb 32.8 /255. Sheet `/workspace/mtl_fix/mtl38/m38_sheet.png`.
- **Particle flags PROVEN** (PS 281 GLSL 127-131, 788-790, 1077-1091): ac = sat((texA·vertA − clip)/(1 − 0.5·clip)), clip = 0x400 ? 0 : clip; rgb ×= sat(ac + 0x400); alpha = 0x20 ? 0 : ac. blend_add = blend: browser legacy fallback (`uMode == 2`) removed. **Open (HEURISTIC):** for 0x400 our rgb keeps ×vertexAlpha; the PS does not apply it, but whether the CPU folds age alpha into vertex colour (VS 248: TEXCOORD.rgb = COLOR_1·TEXCOORD_1.w·exposure) is untraced, so unchanged.
- **SFX blend:** PSO states per define PROVEN (ADD_NO_ALPHA One/One alpha Zero/One; ALPHA_BLEND SrcAlpha/InvSrcAlpha; neither SrcAlpha/InvSrcAlpha alpha Zero/Zero — mtl1-44: that is the DUDV pass only; colour-pass neither = One/InvSrcAlpha alpha One/Zero). Material → define selection not found (mtl1-44: PROVEN, FUN_142451840) (only the define table 0x142450650 / caller 0x142332e80), so "every sfx mesh = ADD_NO_ALPHA" (plasma capture) stays HEURISTIC; code unchanged, labels updated.
- **Light falloff d⁴:** browser (EffectRenderer light loop), `CaptureLightUnits.Attenuation`, `vfxframe_scene.attenuation` all use sat(1 − d⁴/r⁴)² (PROVEN PS 214). UE point lights cannot express it: UE 5.7 `RadialAttenuation` = (1 − d²/r²)^n (DynamicLightingCommon.ush). Exponent changed 4 → **1** (best fit, max error 0.13 vs 0.59) — APPROXIMATION.
- Tests: SM2.Assets 195 pass (+6). Browser builds.

## mtl1-37 (2026-10-28 KST): CDT bones via tpl_markup locators, retail remap defaults
- **CDT shape bones = `link_tpl_obj` (FINDINGS §3).** New `TplMarkup` (reads `<tpl>_markup` `objectInfos[].entities[]` of `__type "tpl_markup_locator"`: name, parent objectId, localTransform position/rotation°) + `CdtObjectResolver` (exporter, browser, PhysBench). Order: exact template object → locator moved onto its parent with offset folded (`Transform(offset, R·T)`, boxes also get `Rotation`) → old cpp name remap/reject only for names in neither list. Sidecar: `ResolvedFrom` on capsules/spheres/boxes, `axesUe` on rotated boxes; cpp uses `axesUe` for the box rotation and logs `tplLocator=N`. Euler order XYZ is HEURISTIC (calgar's locator has rotation 0).
- **Evidence:** cc_calgar.tpl_markup `Backpack_CDT_L` = locator on `powerplant_backpack` at (0.2031592, 0, -0.5929316), rot 0. Box (0.52×1×0.6 m) world centre (0, 1.746, -0.749) = on the body centreline behind the back; 4 cloak particles ≤0.8 cm inside at rest (all <1 cm).
- **UE (build37; Binaries → `Binaries_bak11`, cpp `.bak31`), `run33.ps1` on `Exports\mtl37`:** calgar remapped 0 / rejected 0 / missingBones 0 / tplLocator 1 / boxRestInside 0 on all 8 cloths; own 0 (+164 pinned), skin 2-18, wrongSide 12-18 = same as the mtl32 remap+reject baseline (now by evidence, not by name). sorcerer (no locators): own 152-154 (worst 8.04), skin 14-16 = unchanged. nan 0, PIE False. Captures `Captures\mtl_fix\mtl28\c28_m33_m37{calgar,sorc}.png`, sheet box `/workspace/mtl_fix/mtl37/m37_sheet.png`.
- **simRemapConfig defaults (exe PROVEN):** retail ClothEntry ctor 0x141093260: maxInfluenceDist 0.15 (0x14109326c), distanceRemapPower 5 (0x141093273), useTopologicalDistance false (0x14109327a). Ours were 0.05 / 1 (uncited); meta_desc says 0.15 / 10 (older). Calgar: 1 cloth gets 0.15, 5 cloths get power 5. `maxInfluenceVert` is parsed into `ClothEntry.MaxInfluenceVert`/sidecar (value 4 in all 61 mtl31 cls occurrences) but the retail exe has **no** such string and its serializers (0x141092850, 0x141093480) read only the 3 fields ⇒ ignored by the engine.
- **Spec-flag writer (d1.c):** CDTConverterExe Ghidra auto-analysis OOMs (2 GB, then 10 GB heap: `ghidra/analyze2.log`, `analyze3.log`), so d1.c was never written. Still NOT FOUND; shader side (SHADERS.md §5) says the flag is CPU-only. Rule stays HEURISTIC.
- Tests: `CdtObjectResolverTests` (markup parse, box fold+rotation+idempotence, unknown names untouched, retail defaults). 189 pass.

## mtl1-36 (2026-10-28 KST): VFX light units calibrated from capture CB, cine_bomb missing assets
- Evidence (capture, plasma nightly CB_COMMON_LIGHTNG, s4462): per light P0=(pos, mult), P1=(HDR colour, 1/r^4), P2=(spot dir, offset), P3=(spot scale, shadow idx); att = sat(1-d^4/r^4)^2; no 1/pi on particles; unlit particles take COMMON_HDR_AVERAGE_LUMINANCE (ambient = avgLum).
- Fit: fx_plasma_exterminator_shot_venting r 24 exact, colour ratio within 1.3 % after sRGB decode, magnitude lin(Color)·I·avgLum within -5 %/-3 %; fx_plasma_charging_side r 2 exact, mult in its spline range. Model `CaptureLightUnits`: capture HDR = lin(ColorCtrl)·I·(adaptive ? avgLum : 1); exported light = relative to particle ambient (1 = ambient). Adaptive factor HEURISTIC (one light); 233/270 authored flags true, default true. Ground/cover albedo 0.18 HEURISTIC.
- `--vfxlights <sfx> <out.json>` dumps all 933 sfx_lights (9 samples each).
- Half-float safety: exposed vertex/ground light clamped to `VfxFrameExport.MaxExposed` = 64 (cine_bomb light authored 1.7e5 overflowed fp16 -> cyan patches in UE; 567 -> 0 cyan px).
- cine_bomb "missing" texture/mesh: lookup was fine, assets were not on the box. Frames now list `missingTextures`/`missingTemplates`; `tools/vfx/missing_assets.py` writes the extractor script; two rounds fetched part_seq_explosion_mid_512_8x8, imp_concrete_wall_01, sfx_geom_debr_expl_{concrete,stones}_01 -> missing [].
- UE vs CPU ref (mean /255, p99): acid 0.93/11, cine_bomb 0.77/13, grenade 1.13/36. Rules scene unchanged. Tests 185 pass (+CaptureLightUnitsTests). Sheet `/workspace/mtl_fix/mtl36/mtl36_sheet.png`.

## mtl1-35 (2026-10-28 KST): VFX lights, mesh particles, soft depth, grading LUT in the UE export
- **Lights:** `frame["lights"]` = sfx_light samples at the frame time (position, colour·intensity in sprite HDR units, radius m). Used for the lit ground (`tools/vfx/vfxframe_scene.py ground_batch`, browser GroundFs formula baked per vertex, shared by UE and the CPU reference) and for mesh vertex light (browser MeshFs, fall = sat(1-d²/r²)²). UE also spawns tagged PointLights (unitless, falloff exponent 2, radius ×100) for use in real scenes; the unlit capture masters ignore them. Light units are HEURISTIC: cine_bomb's light (~1.3e5, r 228 m) saturates the ground in both renderers.
- **Mesh particles:** `EffectMeshParticles.Build` instances flattened to world triangles (13-float stride + `indices`), one batch per (emitter, material). Translucent sfx materials -> Additive (SfxMesh, fresnel alpha); opaque lit combine materials -> new `ueBlend` "Solid" (`M_SM2Vfx_Solid`, depth write). Shading baked per vertex (browser formulas, HEURISTIC). VAT meshes in rest pose. Drawn before the sprites. acid_mine 0.2 s: 1 mesh, 5472 tris.
- **Soft depth (capture-proven, PS 101818):** va *= sat((sceneDepth - particleDepth) / softDepth) on AlphaComposite/Translucent/Additive (rgb and alpha). Not on Masked (UE cannot read SceneDepth there) or Distortion; the CPU reference has the same exclusions. New rule-scene row soft0..2 (gaps 0.25/0.5/1.5 m): UE within 0.3/255.
- **Grading LUT:** the 22 content `*_lut` textures are 32³ BGRA8 (format proven, `neutral_lut` = identity). `CaptureLut.Bake` reproduces CS s1996 (log-input, Hable×whitepoint, sRGB, NEXT lookup) and the export writes the baked TONEMAP_LUT as a 1024×32 strip (`textures/sm2lut_<lut>_<wp·1e6>.png`, `frame["grading"]`). The UE PP tonemap samples the strip (trilinear) when present; the HDR->coordinate mapping is the inverse of the bake input (HEURISTIC: final PS 1209 not disassembled). `SM2_VFX_GRADING=story_tower_lut SM2_VFX_WHITEPOINT=1.343645` = the outdoor capture's grade. Smoke/plasma used runtime 16³ COLOR_GRADING volumes that were not dumped (identity is HEURISTIC).
- **Verification (512 px, ground 0.06):** UE vs CPU reference mean |d| acid_mine 1.09, grenade 1.27, cine_bomb 0.37 (/255; p99 outliers on spark streaks, mip vs mip 0). Rule scene: all 25 patches within 0.41/255 incl. soft depth. Regression: no-LUT render == mtl1-34 UE output (mean 0.000); identity LUT vs exact Hable mean 0.47, max 1. story_tower vs identity: mean 1.9 (subtle). No capture has pixel dumps of the LUT or the effects, so capture checks are formula/cbuffer level. The hellbrute pretonemap EXR vs final_331.png does not match (mean ~78; snapshot predates later passes, final is upscaled 4K): not usable as tonemap evidence.
- **Fixed in verification:** the CPU reference used w = 1 as depth for orthographic cameras (everything failed the depth test in the rule scene); it now interpolates view depth.
- **Tests:** CaptureGradingTests (bake = sRGB(Hable) at nodes, coordinate inverse, apply, BGRA layout, strip layout, lights/triangle JSON, soft rule). SM2.Assets 180/180.
- **VFX overall:** ~90 % -> ~92 % (estimate).

## mtl1-34 (2026-10-28 KST): VFX frozen-frame export to UE with capture rules, engine VFX defaults, browser R/B fix
- **Export:** `ForgeLens --vfxue <sfx> <aliases.ps> <raw> <out> name@sec...` writes `<name>_<ms>.vfxframe.json` + textures: every sprite/trail/spark batch the browser draws (EffectSprites), the vfxbench Fit camera (`fovY` radians + `fovYDeg`), exposure, and the capture rules (`VfxFrameExport.Rules()`). `--vfxue-rules <out>` writes the synthetic rule scene (`VfxRuleScene`): tonemap ramp, particle blend flags, trail/spark, distortion over a ramp, each with an expected 8-bit value or pixel shift derived from CaptureRules.
- **UE (`tools/ue/ue_vfx_frame.py import|render|export`):** one master per capture blend state. Particles One/InvSrcAlpha -> BLEND_AlphaComposite (measured equal). Trails -> Translucent. Sparks -> Masked 0.5 (HEURISTIC kind). SFX meshes -> Additive. One ProceduralMeshComponent per batch (sort priority = draw order). Tonemap = post-process material (Hable x 1.375385, saturate, no LUT) on a FINAL_COLOR_LDR SceneCapture2D with manual exposure (effective scale measured 1.0). Distortion: SceneColor/refraction are constant inside SceneCapture2D in 5.7, so it is two passes: a shift pass into RGBA16F, then the PP material samples PostProcessInput0 at UV + shift (nearest texel).
- **Rule scene, UE vs CaptureRules:** all display patches within 0.41/255. Distortion 4.0/12.0/24.0 px vs 3.69/11.91/24 (nearest texel), sign correct. UE vs CPU reference (`tools/vfx/vfxframe_ref.py`): mean |d| 0.005, max 7.
- **Real effects, UE vs CPU reference (512 px, engine defaults):** grenade 0.15/0.30/0.60 s mean |d| 1.9/1.3/1.4; bolter muzzle 0.02; thunderhawk trail 0.04; slash_cross (premul + Masked) 1.6; side_twist_distortion 0 (flat backdrop). The p99 outliers are on sub-pixel spark streaks: UE samples mipmapped textures, the reference samples mip 0.
- **Engine VFX defaults (Mod Tools + retail ctors, parent patch `modtools-vfx-defaults.patch`, see /workspace/rdc/modtools/FINDINGS.md):**
  - `isBlendAlphaMultiply` absent = true.
  - `textureTintControl` absent = 0.
  - GPU `streaks` absent = enabled, ratio 0.3.
  - A/B on the 7 frames: the grenade GPU `b_sparks_1..3` now classify as sparks (Masked; 3 batches per frame). This matches the opaque spark PSO in capture 88302 (eid 2086).
  - Pixel change is small: mean 0.08-0.25/255, 227 px > 8 at 0.15 s. The other effects are unchanged (their keys are explicit).
- **vs capture 88302 (smoke):** the export has the yellow spark streaks and grey smoke. The capture also shows the fire flash and a lit HDR scene. Not exported: mesh particles, lights (the browser's orange glow on the grenade), soft depth fade. The LUT is not applied. Exposure 1/250 is HEURISTIC (capture EXPOSURE_DIV_AVG_LUM 0.024-0.032).
- **Bug fixed (browser):** `EffectTextures` uploaded TextureDocument's BGRA as RGBA, so VFX textures had R/B swapped. Fire atlases are warm only in BGRA order (seq_fin_expl_g mean BGR [31.7, 17.8, 8.1]). They are now swapped the same way GlPreview does for meshes.
- **Bug found and fixed in verification:** `fovY` is in radians. The first UE/reference renders read it as degrees and zoomed into the smoke.
- **Tests:** `VfxUeExportTests` (blend mapping, composite = EffectBlendRea, trail/spark/sfx, rule expectations, camera units) + CoveringTintTests defaults. SM2.Assets 173/173, Extractor 42/42, LibSaber 5/5 (HaloCEA out of scope).
- **VFX overall:** ~85 % -> ~90 % (estimate).

## mtl1-33 (2026-10-28 KST): saved calgar regenerated, missing-bone collision handling, wrong-side-of-limb metric
- **Missing shape bones (cpp; HEURISTIC rule, UE evidence):** shared CDT shapes can name bones the model lacks (calgar: 52x60x100 cm box `Backpack_CDT_L`; the FBX has `powerplant_backpack[_cdt]`, no `Backpack*`). AddCollision now resolves each bone: exact name, else (sidecar `collision.missingBoneRemap`, default true) the unique bone containing every core token of the missing name (side l/r and `cdt` are qualifiers, +1 each for a match, no opposite side; ties -> skip). Any remapped shape that still holds free rest particles (box: any inside; capsule/sphere: rest fit at the 0.25 floor) is rejected, because UE showed `Backpack_CDT_L -> powerplant_backpack_cdt` wraps 114 free cloak rest particles (own-collider penetration 0 -> 204, worst 12.7 cm). Capsules whose second bone is missing are skipped (previously they collapsed into a sphere). The log line gains `remapped`, `remapRejected`, `boxRestInside` plus `collision remap (HEURISTIC) shape:bone->bone` / `collision skipped (bone not in mesh) shape:bone[...]` lines. calgar: the 7 small cloths keep the remapped box (0 rest particles inside), the cloak drops it. Sim results with remap+reject = remap off (identical samples).
- **Wrong-side metric (cpp; HEURISTIC):** `MeasureClothWrongSide(Comp, 1.0, 10.0)` / `ue_c28.py samplewrong`. For each free particle A = animated position (clothing asset physical mesh + bone weights, CPU skinned), P = simulated position. If neither end is more than 1 cm behind the body but a 1 cm-step point on A->P is, the particle went through or around a body part. The skin metric and this one now share a body grid (`SM2Skin`, cell = 10 cm; skin numbers unchanged vs mtl1-32). Validation on tech_priest: old json + shared PA (legs through skirt) **48-52** (skirt on tech_priest_base_4, mean move 6.5 cm, worst 31.7) vs saved mtl31 asset **11-12** (worst move 7 cm).
- **Saved calgar:** `ue_chaos_cloth.py cc_calgar,chars_cloth` from the adopted mtl32 json; old asset -> `cc_calgar_ChaosCloth_bak14`. The first run failed: the rename left an ObjectRedirector (referenced by `/Game/SM2_Work/Tests/mrq16/L_wind16`) that `fix_redirector` did not see yet, so the duplicate hit an existing path. Deleted the redirector (`tools/ue/mtl_fix/ue_redir33.py`) and reran; `ue_chaos_cloth.py` now retries the redirector fix once and stops with an error instead of running CreateChaosCloth on None. Result: 8/8 cloths, bound sections as before, 8 collision PAs, PA/ABP `_14`. L_wind16 loads the new mesh by path.
- **UE (build33 then build34; Binaries -> `Binaries_bak9` / `_bak10`, cpp `.bak29` / `.bak30`, header `.bak11`).** `tools/ue/mtl_fix/run33.ps1` = run32 + samplewrong, capture `c28_m33_<tag>.png`.

| run | own | skin (free) | wrongSide |
|---|---|---|---|
| tech_priest old json, shared PA | - | 13-17 | 48-52 |
| tech_priest SAVED (mtl31) | 0 | 22-25 | 11-12 |
| calgar mtl32 json, remap kept on cloak (build33) | 204 (+1528 pinned), worst 12.7 | 2-18 | 12-18 |
| calgar mtl32 json, remap off | 0 (+164 pinned) | 2-16 | 10-18 |
| calgar mtl32 json, remap + reject (build34) | 0 (+164 pinned) | 2-16 | 10-18 |
| **calgar SAVED regen** | 0 (+164 pinned) | 2-20 | 10-18 |

  calgar wrong-side hits are mostly cloak particles at calgar_body_3. nan 0, PIE False after every run. PhysTestMap was saved by `ue_dirty save` before the restarts (only the C28 spawn/clean had dirtied it).
- Correction to mtl1-32: the missing leg ornaments in the calgar captures change between runs of the same json (the mtl1-33 diag captures lack them, the saved one has them), so they are a capture/streaming effect, not a cloth result.
- Image: box /workspace/mtl_fix/mtl33/m33_sheet.png; PC `Captures\mtl_fix\mtl28\c28_m33_*.png`.
- Open: the game's real attach point for `Backpack_CDT_L` (jetpack bones) is unknown, so the remap is a name heuristic. Wrong-side does not see crossings deeper than 10 cm from any body vertex (thick limbs) or through cloth-only regions.
## mtl1-32 (2026-10-28 KST): per-cloth PA regression (calgar, sorcerer_exalted), skin penetration metric, saved tech_priest regenerated
- **Skin metric (cpp, build32; HEURISTIC):** `MeasureClothSkinPenetration(Comp, ThresholdCm, MaxDistCm, Report)` / `ue_c28.py sampleskin`. Body = enabled LOD0 sections without clothing data whose material is not a cloth/disabled section's. Body verts are CPU linear-blend skinned (component-space x RefBasesInv). Each sim particle takes the nearest body vertex within 10 cm and counts as inside when dot(P-V, N) < -1 cm. Reports free/pinned counts, worst depth, top body slots and a per-clothing-asset split (extra render-slot assets run duplicate sims and appear twice). It does **not** detect wrong-side cases (skirt wrapped behind a leg). Installed build32 (old Binaries -> `Binaries_bak8`, source cpp `.bak28`, header `.bak10`).
- **Runs:** `tools/ue/mtl_fix/run32.ps1 <asset> <tag> <json|saved> [cfg]` (run31 + sampleskin; `saved` skips cdiag and `ue_c28` falls back to the saved `<asset>_ChaosCloth` when no diag exists). Data: `Scripts\mtl_fix\exp32.ps1` -> `Exports\mtl32\{cc_calgar,cc_sorcerer_exalted}`; variant jsons `Exports\mtl32\var\`. A = current json + shared PA, B = current json + per-cloth PA, C = mtl32 json (shared CDT union) + per-cloth PA. Pre-kick + 3 post-kick samples; "own" = vs the cloth's own colliders, "skin" = new metric (free particles; pinned-inside baseline in brackets).

| char / variant | own | skin (free) | skin worst |
|---|---|---|---|
| tech_priest A (old json, shared) | 106-113 | 13-16 (+34) | 2.9-3.65 cm |
| tech_priest C (mtl31 json, per-cloth, diag) | 0 | 24-25 (+34) | 2.9-3.6 cm |
| **tech_priest SAVED regen** | 0 | 22-25 (+34) | 2.89-3.65 cm |
| calgar A | 0 (+285 pinned) | 20/50/53/39 | 3.6-4.2 cm |
| calgar B | 0 (+164 pinned) | 38/62/66/52 | |
| **calgar C (adopted)** | 0 (+164 pinned) | 2/16/14/4 | 2.0-3.6 cm |
| sorcerer_exalted A | 209-211 (worst 4.77) | 6-17 (+28) | ~5.0 cm |
| **sorcerer_exalted B (kept)** | 110-112 (worst 3.78) | 9-10 | ~5.0 cm |
| sorcerer_exalted C | 152-154 (worst 8.04, more colliders) | 14-16 | 4.35-4.54 cm |

  tech_priest: the skin count does not see the A legs-through-skirt case (wrong side), which is visible in the capture; C/saved look right. The extra C counts are skirt/tabard particles near tech_priest_base_4/_8. calgar: A/B errors are mostly ribbon_R (0 own colliders) on calgar_body; the shared shapes in C fix it (C has missingBones=1 per cloth: one shared shape's bone is not in the mesh). A also hides the leg ornaments in the capture. sorcerer: per-cloth PA does not regress (own halves, skin same, A tabard balloons in the capture); the mtl32 union is mixed (skin count up, worst down), so the current json stays. nan 0 and PIE False after every run.
- **Adopted:** calgar mtl32 json -> `Exports\chars_cloth\cc_calgar\...\cc_calgar.physics.json` (old `.bak2`); the saved calgar ChaosCloth was **not** regenerated. Sorcerer_exalted json unchanged.
- **Saved tech_priest:** `ue_chaos_cloth.py cc_tech_priest,chars_cloth` from the mtl31 json; old asset -> `cc_tech_priest_ChaosCloth_bak15`. 4/4 cloths, 5 assets, collision shapes 15/16/16/16 (missingBones 0), 4 collision PAs, bound 5/5, PA_..._SM2_15 + ABP_..._15. SIE on the saved mesh = the diag numbers above.
- Image: box /workspace/mtl_fix/mtl32/m32_sheet.png; PC `Captures\mtl_fix\mtl28\c28_m32_*.png`.
- Open: regenerate the saved calgar ChaosCloth from the adopted json; fix calgar's missing shared-shape bone; a wrong-side (behind-limb) test for the skin metric.
## mtl1-31 (2026-10-28 KST): tech_priest cloth penetration — per-cloth collision PA, shared CDT union
- **Root cause (UE A/B):** every cloth of a mesh shared ONE collision PhysicsAsset, so the skirt also collided with the tabards' leg capsules (rest-fitted to the tabard particles, up to 30 cm). The skirt was pulled between the legs and the legs showed through. **Generic fix (cpp, build31):** one collision PA per cloth (`PA_<mesh>_ClothCollision_<sim>`); the extra render-slot assets of a cloth reuse it. Sidecar `ue.chaosCloth.collisionPerCloth=false` restores the shared PA. MeasureClothPenetration tests each cloth only against its own asset's PA (or the override); `collisionPAs` = unique PAs.
- **Parser (HEURISTIC, data evidence):** prop `sharedCDT*` shapes are added to **every** cloth on top of its own cdt lists (skipping no-bone, radius<=0, zero boxes and duplicates of own shapes). This replaces the mtl1-30 "no lists → all shared" fallback. Retail: 282 cls with prop_cloth_sim; cdt references are never name-only (306 items, all with bones); 217 define shared shapes; 2995 cloths have own+shared, 1014 own only, 538 shared only. Sidecar `collision.source` = config | sharedCDT (HEURISTIC) | config+sharedCDT (HEURISTIC), `sharedShapesAdded`. Test PhysicsTests.SharedCdtShapesApplyToEveryCloth.
- **Data:** `Bin\SM2Extractor_mtl31` → `Exports\mtl31\cc_tech_priest` (--include-cloth-sim; 4 cloths, skirt 3→15 shapes, others 4→16). This replaced `Exports\chars_cloth\cc_tech_priest\...\cc_tech_priest.physics.json` (old one kept as `.bak2`). The saved `cc_tech_priest_ChaosCloth` asset was **not** regenerated.
- **UE (build31 installed; old Binaries → `Binaries_bak7`, source `.bak27`):** `tools/ue/mtl_fix/run31.ps1` per variant (unsaved diag, SIE, +80z kick, 3 samples). "own" = free particles >1 cm inside the cloth's own colliders. "ref" = vs the body ragdoll PA `PA_cc_tech_priest_SM2_1` (`ue_c28 sampleref`): this is noisy and **not** ground truth (~100-130 even pre-kick; 2 runs of the same variant differ by up to 30). "vis" = `vis_intrusion.py` grey-in-robe pixel delta sim−base (HEURISTIC).

| variant | own | ref | vis |
|---|---|---|---|
| v0 old json, shared PA (= mtl1-30) | 105-113 (+55 pinned), worst 3.0 cm | 106-114 | +6.4 / +8.8 (legs through skirt) |
| v1 old json, per-cloth PA | 0 | 141-154 | +0.9 |
| **v2 mtl31 json, per-cloth PA (adopted)** | 0 | 157-161 | +0.8 |
| v3 mtl31 json, shared PA | 103-106 (+57 pinned) | 103-118 | -1.3, span 1.08 (skirt flares out in spikes) |
| v2 + CollisionThickness 1 / 4 cm | 0 / 0 | 128-131 / 157-162 | +0.8 / +0.5 |
| v2 + self-collision (2 cm) | 0 | 155-163 | +0.5 |
| v2 backstop off / 3 cm (default 1) | 0 (worst 0.53 cm) / 0 | 125-130 / 120-132 | +0.8 / +0.8 |

  Thickness, self-collision and backstop changes stayed within run-to-run noise, so the defaults are unchanged (2 cm thickness, CCD, 1 cm backstop, self-collision off). nan 0 and PIE False after every run. Image: box /workspace/mtl_fix/mtl31/m31_sheet.png; PC `Captures\mtl_fix\mtl28\c28_m31_*.png`.
- Open: no skin-based penetration metric yet (the ref PA is coarse). The sidecar does not use the game's selfCollision/interCollision distance (7 cm); the self-collision A/B showed no gain. Regression on other chars (sorcerer_exalted, calgar) with per-cloth PA has not been run.
## mtl1-30 (2026-10-28 KST): one clothing asset per render slot; shared CDT collision fallback
- **Multi-section binding (cpp, build30b):** steps 3-5b of CreateChaosCloth are now a `MakeAsset` lambda. UE binds one section per clothing-asset LOD, so every further render slot of a cloth gets its **own** asset (`SM2Cloth_<sim>_<slot>`), built before any bind: _SIM source → another asset from the same (disabled) sim section; render-as-sim → the slot's own section if hit3cm >= 0.5, else (median nearest-particle distance <= the cloth's simRemap `MaxInfluenceDist`) a copy of the primary sim mesh, as the game remaps render meshes from the sim within maxInfluenceDist. Extra assets share the cloth's collision PhysicsAsset (shapes not re-added). Sidecar `ue.chaosCloth.bindAllRenderSlots=false` restores primary-only. BindRecs/bindCheck cover every asset.
- **Collision 0 shapes (root cause):** npc_lesser_sorcerer_client.cls cloths have **no** cdtCapsules/Spheres/Boxes lists; only prop-level `sharedCDT*`. Parser: a config with no cdt lists at all uses **all** shared shapes (skips no-bone / radius<=0, e.g. the unnamed `powerplant_backpack` sphere); explicit lists keep their meaning. **HEURISTIC** (exe xrefs to the names are only (de)serializers, RE_NOTES). Sidecar: `collision.source`, `source.CollisionFromShared`, a note. Test: PhysicsTests.ClothWithoutCdtListsUsesAllSharedShapes (SM2.Assets 163, Extractor 42, LibSaber 5 pass).
- **Data:** `Bin\SM2Extractor_mtl30` → `Exports\mtl30\cc_sorcerer` (physics.json differs from mtl15 only in collision + simSectionExported); copied over mtl15's physics.json (old kept as `.bak1`).
- **UE check (build30b, old Binaries → `Binaries_bak5`/`_bak6`, source `.bak25`/`.bak26`):** cdiag on mtl15 cc_sorcerer: skirt_SIM 1/1 bound, ribbons_SIM **2/2** bound (REND_01 section 16 + REND_02 section 15 via `SM2Cloth_ribbons_SIM_ribbons_REND_02_0`, REND_02 hit3cm 0.03, median 15 cm <= 200 cm), **collision shapes=10** per cloth (7 capsules + 3 spheres, missingBones 0). ue_c28 SIE after +80z kick: 3039 particles (375+1332+1332), nan 0, collisionPAs 1, 28 free particles >1 cm inside (worst 1.44 cm). PIE False after. Image: box /workspace/mtl_fix/mtl30/m30_sheet_v2.png; PC Captures\mtl_fix\mtl28\c28_m30_{rest,kick}.png.
- Open: ribbons_REND_02 runs a duplicate ribbons sim (2x cost); the 6 s sample before the kick read 0 particles (SIE warm-up), so the sim check uses the post-kick sample.
## mtl1-29 (2026-10-28 KST): cloth render-as-sim verified in UE; render slots never claimed as _SIM
- Installed build28 (old Binaries -> `Build\sm2phys\installed_SM2PhysicsSetup.bak2\Binaries_bak3`) and restarted UE. Result: ribbons_SIM converted (render-as-sim, section 16, 1332 verts, hit3cm 0.93). **skirt_SIM did not**: step 1b (vertex-hit _SIM search) picked skirt_REND (section 17, 567 verts, on the particles within 0.5 cm), which left 0 render sections, so it stayed bone-based.
- **Generic fix (cpp, build28b):** step 1b skips sections whose slot matches a cloth's RenderMeshes, so the hidden _SIM shell can't be confused with a render slot that shares its vertices. Step 2b then handles it. Binaries_bak4 = build28.
- **UE check (build28b, mtl15 cc_sorcerer, unsaved duplicate):** 2/2 cloths convert: skirt_SIM (section 17, 375 particles, 78 pinned, worstMatch 0 cm) and ribbons_SIM (section 16, 1332 particles, 180 pinned, worstMatch 8.93 cm). SIE (`ue_c28.py`): the cloth component reports 1707 particles (375+1332), nan 0. The base mtl15 mesh reports 0. Rest vs 6 s: 1299 changed px on the cloth actor and 0 on the base, at the skirt hem and ribbon edges (static ref pose, so the motion is small). The first sample at 2 s had 0 particles (sim warm-up). SIE stopped; diag duplicate dropped (never saved). Image: box /workspace/mtl_fix/mtl28/ue/c28_sheet.png; PC Captures\mtl_fix\mtl28\c28_*.png.
- Open (fixed in mtl1-30): only one render section is bound per cloth (ribbons_REND_02 stays bone-skinned, as with _SIM sources). The cdiag run reports collision shapes=0 on the duplicate.
## mtl1-28 (2026-10-28 KST): per-kind VFX blend (trails/sparks), distortion backdrop, cloth render-as-sim fallback
- **Trails** draw with SrcAlpha/InvSrcAlpha (capture CB_PASS_TRAIL). They look dimmer than the old blend; that dimming is from the capture. **Sparks** (IsGpu && StreakRatio>0, **HEURISTIC** mapping) draw opaque with alpha-test 0.5, blending off (capture smoke 88302 eid 2086, PS 1770712), before the other kinds. CPU particles stay One/InvSrcAlpha. `SM2_VFX_KIND_BLEND=0` restores the old path. `CaptureRules.VfxDrawKinds`; the bench logs `(trail N spark N)`.
- **Distortion preview:** effects with DistortTexture/SelfDistortTexture and no coverings get a non-painting checker backdrop (plane or sphere, `CoveringDemo.For`) that faces the camera 0.5 m behind the origin, so uv += 0.04*dudv-0.02 is visible. Bench: diff of DISTORT=0 vs on is non-zero (plane mean ~3, sphere ~3.7). Image: box /workspace/mtl_fix/mtl28/vfx/mtl28_vfx_sheet.png.
- **Cloth, mtl15 cc_sorcerer 0 cloths (root cause):** the mtl15 FBX has 18 materials and no `skirt_SIM`/`ribbons_SIM`; mirr15 has 20. mtl15 was exported without `--include-cloth-sim` (`HideClothSim`), and physics.json is the same in both. CreateChaosCloth required a _SIM section. **Generic fix:** if there is no _SIM section, the plugin uses the largest render section as the sim mesh when >=50% of its vertices are within 3 cm of a sim particle (sidecar `ue.chaosCloth.renderAsSim`, default true). The sidecar gets `simSectionExported`, and the exporter warns. Plugin built (build28, BUILD SUCCESSFUL). **Not installed in UE_VFX_57 and not verified in UE** (needs an editor restart).
- Tests: dotnet Assets 162, Extractor 42, python unittest OK.
## mtl1-27 (2026-10-28 KST): material layers in the sidecar, InstanceTint, spec-flag evidence
- **Spec-map flags (item 1, writer NOT found):** OBJ_MTLBLEND_LAYER_CONST[15].x bit meanings decoded from PS 22961/29540 (RE_NOTES). Flags = 0 on **all 16 dumped Hellbrute draws** (5 PS: 19112, 21380, 22961, 29540, 33973; chameleon and non-chameleon), every one with a MetGray(r)+Roughness(a) DXN spec bound. So the mtl1-26 chameleon-only rule is too narrow for the Hellbrute, but the data cannot separate "per asset" from "per spec format" (no non-Hellbrute capture). Bit 32 already describes the MetGray layout (rgb tint, rough from a), so the flags are probably not derived from the texture format. **UseSpecMap rule unchanged (HEURISTIC)**. Next step: one capture of a non-Hellbrute asset with a Metalness(r)+Roughness(g)+AO(b) spec map.
- **Layers (item 2):** sidecar slots get `layers[]` = Saber material layer0..3 (non-empty): `index, texture, subMaterial, tint (raw bytes as stored), vcSet, uvSet, tiling[u,v], blending{method, useLayerAlpha, useHeightmap, blendNormalAdd, weightMultiplier, heightmapSoftness, texChannelBlendMask, weightsColorSet, heightmapColorSet, heightmapInvert, heightmapOverride}`. These are exported only. The importer does not shade them yet. Exe layer parser FUN_14217b180: method "dirt" = 1, "dirt_no_spec" = 2, other values = 0.
- **Calgar check:** in the mtl27 export (PC `Exports\mtl27\calgar`, extractor `Bin\SM2Extractor_mtl27`), cloak_REND_01/02 have **one layer** (ch_calgar_coat_01, method common), so the cape stripe does not come from material layering. The coat albedo has no stripe in its main panel; trim bands sit in the right-hand atlas columns. **HEURISTIC:** the stripe is probably UV-mapped trim, a decal, or a cloth-sim part. The only multi-layer slots on Calgar are hair: L1 `ch_calgar_head_hair_01_ao`, uv1, `dirt_no_spec`, weightMultiplier 0.89.
- **InstanceTint (item 3):** M_SM2 gets the vector `InstanceTint` (white) and the static switch `UseInstanceTint` (off). On: albedo_lin = poly(InstanceTint * srgb(tex)), as in the game (exe FUN_1423bb1e0 / PS). The chameleon path always multiplies by InstanceTint, and white is exact. The tint is a runtime per-object colour (obj+0x74 packed RGB, optional byte scale, x10 with flag 0x200), so there is no static source. The importer applies an optional sidecar override `instanceTint [r,g,b]`, and `decode_instance_color()` decodes a runtime packed value.
- Tests: python 11 (adds test_instance_tint.py); dotnet SM2.Assets 158 (adds MaterialLayersTests). UE was not rebuilt or checked for this patch.
## mtl1-26 (2026-10-28 KST): chameleon per capture formula (generic, all chameleon slots)
- Source: /workspace/rdc/hellbrute/cham/FORMULA.md (frame 68141, PS 19112 GBUFFER / 22961 forward). M_SM2 `chameleon()` now: t = sat(N.V_ts)^fresnelPwr, 3-step ramp C0->C1->C2->C3 (thresholds from `chameleon_ramp`), c += (1-c)*metal, **colours raw .td/255 (sRGB, no longer linearised)**, texture re-encoded to sRGB, multiplied by c, then linearised with the game polynomial. Applied before AlbedoTint/MetallicTint. Every slot with sidecar `chameleon` gets it; missing fields fall back to `CHAMELEON_CAPTURE_DEFAULTS` (explicit 0 kept). Sidecar values for Hellbrute = capture CB exactly (exe/capture evidence).
- Metal/rough: new static switch `UseSpecMap` (default on). Off for chameleon slots: metal = sat(bias), rough = sat(scale+bias) (=1 on the captured draws; flags OBJ_MTLBLEND_LAYER_CONST[15].x = 0 on 7/7 chameleon draws). **HEURISTIC** generalisation: the flag writer was not located.
- Second lobe ("clear coat"): kept as the existing additive Substrate slab (spec only, F0 = lerp(0.04, SecondSpecTint 0.95-0.98 x lin(tex), m2), rough 0.63, anisotropy + shift). Matches the game's additive direct-only lobe; Substrate has no legacy ClearCoat pin and a vertical layer would attenuate the base. Not changed in this patch.
- Tests: `python3 test/python/test_chameleon.py` (8 tests, `unreal` stubbed): ramp rows vs CB, sRGB colours, defaults, poly, multiply-then-linearise, GBUFFER hue order, metal whitening.
- UE check (/Game/SM2_Work/Tests/mtl26pre = mtl1-25 importer vs mtl26 = this patch, same mtl26 export, ue_cap_lit `lit|base fit view=ref`): chameleon-pixel mean LDR before (41,68,81) -> after (36,49,58); game finals 3430/3441/3452 mean (28,51,60). Blue panels less glossy/cyan. Lighting is not matched (UE scene lights vs game), so this is a direction check, not a match. UE BaseColor capture is not a clean albedo under Substrate (red channel ~13/255 where the formula gives ~2): HEURISTIC, likely the second slab. Image: box /workspace/mtl_fix/mtl26/before_after_mtl26.png (+stats.json); PC Captures\mtl_fix\mtl26\{pre,after,pre_base,after_base}.

## ui-43 (2026-10-28 KST): Content Browser
- Categories come from data where linked: `.cls` nameTpl plus the `__type` chain, and Morpheme `linksAnimation`. Name and keyword placements are HEURISTIC and carry an `H` badge, a tooltip and a context-menu reason.
- **All** is the default and unclassified assets go to **Other**; a test enforces that nothing is unreachable.
- Thumbnails are disk-cached (`%LOCALAPPDATA%/LibSaber/SM2Browser/thumbs`):
  - models are a clay render (HEURISTIC look);
  - effects are a CPU splat approximation (HEURISTIC);
  - clips without an owner model show a glyph.
- Tests: SM2.Assets 233, Extractor 43, LibSaber.Tests 5, chameleon python 8 — all pass.
- Open: close-time state saving is unverified under Xvfb.

## ui-44 (2026-10-28 KST): Kitbasher
- `KitbashLibrary` (Customization/Kitbash.cs) + right-column **Kitbash** tab. Preview and export share `ArmorVariant`. Tank preset 1 preview and export: 978 hidden / 54 shown. Kitbash spec export checked (996 / 36).
- HEURISTIC: STORY_X → loyalist constructor; champion preset → `cc_*_champion_x_NN`; about half of the shared-part objects are not in a given constructor tpl (marked `⚠ not in tpl`).
- Tests: SM2.Assets 237, Extractor 43, LibSaber.Tests 5, python 8 — all pass.

## mtl1-49 (2026-10-29 KST): firearm uid rule, colour schemes, context playback
- Bolt rifle vs auto-bolt fixed by a generic uid rule; colour-scheme picker with game names and swatches (bddd777).
- Context playback both directions (ContextIndex / ContextResolver): effect → character + clip + held weapon + socket + event time; model + clip → held weapon (picker) + event effects + Shoot → muzzle (slot picker). Toggles "Show props/weapons" / "Show effects". Verified in the GUI on the PC (Sorcerer stave ray prep, rubric marine bolter shoot, toggles off).
- Tests: SM2.Assets 278 pass.
- RE map: `docs/RE_MAP/` (INDEX.md, 11 domains, index.tsv, 727 symbols), generator `tools/re_map/build_re_map.py`.
