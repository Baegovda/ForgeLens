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
| Mannequin → SM2 retarget prototype | prototype | 70 | Cadian index finger curl −14…−20° open; Anim Only (Manny) root yaw-180 fix mtl1-13 |
| Browser GPU preview (v3/v4: ANGLE, MSAA, blend passes, anim ordering, Space) | done | 95 | CPU renderer ignores blend and physics |
| Secondary physics (jiggle exact port, cloth PBD ≈ NvCloth) browser + FBX bake + **live UE (plugin v2, mtl1-7)** | done + UE live verified | 88 | 6 chars live, no NaN; tech_priest robe drop + sorcerer_exalted dash lag FLAG |
| Chaos Cloth option (opt-in) + sidecar collision PA | in progress | 70 | mtl1-13: collision wired, no NaN, 2–6% particles >1 cm inside; wind not yet applied |
| Browser VFX (`.sfx` sprites, meshes, trails, lights, distortion, sockets, anim-event cues) | in progress | ~85 | see ROADMAP "VFX3 remaining" |
| Bulk validation (37 assets) | done | 100 | PASS 31 / WARN 3 / FAIL 3 → constructors fixed afterwards |
| UE verification of constructors + physics | done (first pass) | 100 / 85 | open issues below |
| In-game side-by-side comparison | todo | 0 | needs the PC free + in-game captures |
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
- HEURISTIC: `main` is always on; skin material and attachments are not applied.

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
