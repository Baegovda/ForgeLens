# REA batch progress (started 2026-10-29 13:00 KST)
- worktree /workspace/LibSaber-reab (branch rea-batch-work @ bcb17ff)
- 13:05 mem: MemAvailable ~2.4GB (virtio balloon holds ~9GB; prior java OOM-kill at dmesg 41983s, anon 8GB). REA daemon python+rea mcp alive, Ghidra JVM dead.
- 13:15 headless Ghidra proj3 fallback works: gd.sh (3.5G heap, 13 s/run). REA daemon not reopened (OOM risk).
- 13:50 item3: HDR_TONEMAP_EXPOSURE == COMMON_HDR_EXPOSURE_DIV_AVG_LUM in all captures (plasma 5994 0.027324 etc). Unlit trail VS: 82/82 colour VS multiply COLOR by CB_COMMON[22].y avgLum (lit 352 + distort 88 don't; TRAIL_LIGHTNING 82 do) -> same "particle-relative" convention as particles (VS 390), so NO trail-only exposure factor (hypothesis rejected).
  EMISSIVE = [colorIntensityMultiplier, alphaMult, 1, explicitBloom] (data: plasma tracer cim10/add/bloom1 = [10,1,1,1]; trail_rubric_eye cim5 alphaMult4 = [5,4,1,0]); MISC_2.x = 1/softDepth (rubric softDepth 0.5 -> 2, none -> 1e6). visuals desc offsets (serializer FUN_141836a80): lightingMult +0xc color +0x10 cim +0x18 colorIntensity +0x20 ambientLight +0x50 isAlphaInherited +0x54 alphaMult +0x58 explicitBloom +0x5c alphaClip +0x60 fresnel +0x70 softDepth +0x74 blendType +0x78. CPU point writer (T8.y scale) not found: no F16C in exe, CB writer scan negative.
- 14:20 item1: data survey 255 .sfx sfx_light_base carry attenCurveParam (-1 x88, -0.5 x54, 0.2 x36, 0.5 x29, 0 x25, 1 x21 ...). sfx_light_base factory FUN_1419b0a00 (vtable 0x1436423c0, size 0x110), update slot19 FUN_1418c3b30 -> gsLIGHT_MANAGER::CreateLight FUN_1413831e0 -> FUN_141383440/FUN_141384090; light->+0x18 = desc. Scan for [light+0x18]->+0x19/+0x1c readers in 0x141000000-0x142900000: none. Still not found.

## (time label corrected: ~13:00 KST) item 4 spring damper — PROVEN
- Solver FUN_1414828a0 (per damper, joints stride 0x38 state, joint desc ptr at state+0x30): h = dt(DAT_144e7ac80) * timeScale(+0x20).
  Rest dir (bone-pose child-parent) bent toward (0,-1,0) by acos(-dir.y)*min(gravity,1) (gravity<=1e-6 off) via FUN_1427ab3c0.
  Current dir limited to jointAngle deg from rest via FUN_1427ab3c0 (jointAngle<=1e-6 -> FUN_1427aab90 unlimited).
  Then per-axis RK4 (0.5h, h/6) of x=target-pos: a = -(k/m)x - d*v. |v|>2e6 or NaN -> 0. Constraint callbacks (+0x48) then.
- Joint defaults PROVEN (FUN_1416e9a20 / FUN_1415e28a0 grow path): stiffness 0, dampingRation 0, mass 1, isParent F, jointAngle 10, isInvertedLook F, gravity 0.
- Data: 3828 joints; mass absent 505, jointAngle absent 20, gravity absent 78. timeScale absent 2/1511 (default still 1, INFERRED).
- Code: PhysicsParser defaults dampingRation 1->0, jointAngle 30->10; sidecar driveDamping = d*timeScale (was 2*d*w0), cone = jointAngle (0=180), gravityScale clamp 0..1, note rewritten. Tests +4.

## (time label corrected: ~13:20 KST) item 5/6 colour scheme darkness — PROVEN shader + INFERRED enum index
- Exe enum string tables at 0x144ccbf40: tintBlendMode = [multiply, linear_blend]; blendMode = [tint_only, tex_hardlight, tex_blend].
- Retail td (PC Raw\td_all, 18,713 files): tintBlendMode linear_blend 20,052 / multiply 1,127; blendMode tex_blend 16,444 / tint_only 1,243; tintMode=multiply 363.
- glt PS 003784: CB_STATIC_GLT[180+L]==0 -> col = tintC*base, else col = tintC (replace). rec +80+L: 0 tint path (running metal), 1 hardlight pattern (texArray, sRGB hardlight), 2 tex_blend (metal = saturate(metal0*scale+bias)).
- Cause of darkness: the bake always multiplied tint by the neutral _a albedo; linear_blend (95 % of layers) replaces. Fixed in ArmorSchemeBake (Albedo/Specular take tintModes/patternModes), CustomizationSlot.PatternModes, UE params Layer{i}TintBlendMode/MultiplyBase. Tests +2.
- Not done: texArray patterns (tex_hardlight/tex_blend with textureIndex>=0) and lens colours still not emulated.

## 13:32 KST: committed, deployed, pushed
- Commit 7aecce7 on origin cursor/sm2-pck-extractor-4420 (ff bcb17ff..7aecce7 via push.ps1). Local box commit 4536dcc (same content; am re-stamped).
- Exe sha256 a1717c41cda9211127f8d5a2df24aeb95bf6699142ec5396535d54860a2ae94c → ForgeLens_latest\ForgeLens.exe; previous (12:27 KST build) → ForgeLens.exe.bak12.
- Docs: HANDOFF batch list statuses + F59 + "Update rea-batch", STATUS top entry, RE_NOTES section, SYMBOL_DB physics/customization/vfx/index.tsv (hand-added; the generator drops rows whose dumps are not on the box, so not re-run).
- No screenshots (colour preview needs game data on the PC; did not open a GUI on the user's desktop). Item 7 not started.

## Phase 2 (2026-10-29 13:53 KST)
- (a) Colour scheme shots done (new_/old_ cc_titus, cc_sm_soldier_story × IF/DA/BA): soldier_story fixed (bright), titus campaign armour has no CC block.
- (b) item 7: extracted ssl weapons/body_state/commands (3,323 files) + 768 reload/shoot .sani headers.
  - reload_sound_seq track: in exactly 18/23,227 retail .sani; all 18 are the *reload* clips → DATA reload detection; RELOAD_END at snd_reload_mag_in (INFERRED).
  - firearm_library_pve.sso firearms.<uid>.barrelDescriptions.*.shootSettings.fireRate.shotsPerMin (58 firearms parsed); uid ← mgs/<uid>/<uid>_client.cls geom.nameTpl; class→uid by suffix strip (INFERRED).
  - Player firing clips carry no Shoot track (only NPC/last_stand clips) → firing clip still by name (HEURISTIC), rate now DATA.
  - fireRateAffectShootAnim default (omitted) not decoded (bytecode initializer).
  - local commit 60f8794; 330/330 tests.

## rea-batch 2 wrap-up (2026-10-29 14:20 KST)
- Commits pushed (push.ps1 ff-only): 58e2bea item 7, ffb3ba6 item 6, c6ac199 item 1 inverse-square + docs. origin 7aecce7..c6ac199.
- Deployed ForgeLens.exe sha256 17789d258aa595e49fc413211345f4dbb67e60b0169483e58f7993a2a93b573b; previous → ForgeLens.exe.bak13 (sha256 a1717c41…e94c).

## Round 3 finished (2026-10-29 15:00 KST)
- (1) Screenshots OK, no fix. (2) desc→render light PROVEN, P0.w writer open. (3) UseSpecMap raw flags DATA, writer open. (4) fadeParams PROVEN (lightning VS), ribbon opt-in SM2_VFX_TRAIL_FADE=1 HEURISTIC. (5) trail_melee_dark .cls overrides DATA.
- Commit 3ead97e (box) → pushed as 31102cf (c6ac199..31102cf ff). Tests 335/335.
- Deploy: ForgeLens.exe sha256 57405a15…f08ce; previous (17789d25…) → ForgeLens.exe.bak14.
- Patch: %USERPROFILE%\0001-rea-batch-3-trail-fadeParams-opt-in-SM2_VFX_TRAIL_FA.patch

## Round 4 finished (2026-10-29 15:37 KST)
- (1) P0.w writer: open. Light-offset scans (FUN_14325ed30 only, not light), +0x50 vector-load scan (copies/comparators), 300.0 search (non-light refs → level data). No capture with varying sfx attenCurveParam.
- (2) UseSpecMap: bit meanings DATA (0x2000 MSCRGHAO 68/68, 0x40 legacy _spec 66/67, 0x8 MNM 133/134, 0x100 MH, 0x800 MEM, 0x1000 MAO, 0x10 detail partial); importer UseSpecMap = has spec map and not chameleon. Writer open.
- (3) Ribbon point writer: open. FUN_14183b3a0 trail layout PROVEN; scan d:0xa8 d:0x28c → FUN_14187e8b0 (lerp), FUN_141a9a470 (render-instance builder); RB_TRAIL_POINTS refs only FUN_1421ca290; RenderDoc trlPOINT dump (PC round4/trl4.py) on sm2_frame7894 stalled late in the frame (no markers).
- Commit 80066d8 (box) → pushed as b6cf3a1 (31102cf..b6cf3a1 ff). Tests 335/335.
- Deploy: ForgeLens.exe sha256 1b409a47…5319; previous (57405a15…08ce) → ForgeLens.exe.bak15.
- Patch: %USERPROFILE%\0002-rea-batch-4-UseSpecMap-off-without-a-specular-map-DA.patch (applied and removed by push.ps1).

## Round 5 / level-lights finished (2026-10-29 KST)
- attenCurveParam → P0.w PROVEN (FUN_14224f010 + armory level-light pairing 4/4). Details: /workspace/mtl_fix/level_lights/progress.md.
- Commit d55bbe3 (box) → pushed as d843b51 (b6cf3a1..d843b51 ff). Tests 343/343.
- Deploy: ForgeLens.exe sha256 07210b04…42ad; previous (1b409a47…5319) → ForgeLens.exe.bak16.

## rea-batch 6 (2026-10-29 ~23:00–00:00 KST, new account, headless proj3 via tools/re/box/g.sh)
- Outputs box /workspace/rea/b6/ (c_packer.txt, dr_deac.txt, x_deac.log/json, d_*.txt, bytefind.py disp-byte scanner).
- (2) adaptive multiplier PROVEN: FUN_141e90cb0 clamp(avgLum,0.05,50); (3) DAT_14502deac 3 READs only, BSS, INFERRED false; (1) CPU point writer PROVEN FUN_141902840/FUN_141847c30, trlPOINT map INFERRED, upload not found; (4) no exe strings → script data, HEURISTIC; (5) not attempted.
- No code change. Worktree /workspace/LibSaber-rea-batch6 (branch rea-batch6), patch /workspace/patches/rea-batch6/.
