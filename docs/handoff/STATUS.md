# STATUS — SM2 pipeline (as of 2026-10-27 ~05:15 KST, tip 57c38f9)

Branch `cursor/sm2-pck-extractor-4420`, PR #1. Always confirm the current state with `git fetch && git log --oneline -30 origin/cursor/sm2-pck-extractor-4420`.
Percentages are the previous agent's estimates of how complete each track is against the "looks exactly like in-game" goal.

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
| Mannequin → SM2 retarget prototype | prototype | 70 | Cadian index finger curl −14…−20° open |
| Browser GPU preview (v3/v4: ANGLE, MSAA, blend passes, anim ordering, Space) | done | 95 | CPU renderer ignores blend and physics |
| Secondary physics (jiggle exact port, cloth PBD ≈ NvCloth) browser + FBX bake | done + UE verified | 85 | Titus ≈96 %, Calgar cape ≈74 % reproduced |
| Browser VFX (`.sfx` sprites, meshes, trails, lights, distortion, sockets, anim-event cues) | in progress | ~85 | see ROADMAP "VFX3 remaining" |
| Bulk validation (37 assets) | done | 100 | PASS 31 / WARN 3 / FAIL 3 → constructors fixed afterwards |
| UE verification of constructors + physics | done (first pass) | 100 / 85 | open issues below |
| In-game side-by-side comparison | todo | 0 | needs the PC free + in-game captures |
| Niagara export (`.sfx` → Niagara) | not started | 0 | LAST; ask the user first |

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
  - distortion strength `SM2_VFX_DISTORT` 0.04;
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
