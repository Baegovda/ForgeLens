# Overnight decisions log (mtl1-44, started 2026-10-28 23:43 KST)
- Step 1 (VFX blend selector): worktree /workspace/LibSaber-ui45 reset to branch `work` = origin/cursor/sm2-pck-extractor-4420 (e771536); it was at ui-45 d5f507a.
- Ghidra project /workspace/rea/ghidra_proj3 (read-only), helper /workspace/rea/g.sh.
- Define table global instance = DAT_14506cd70 (static init FUN_142332e80 -> FUN_142450650).
- FOUND: SFX key builder = vtable 0x1437f0670 slot1 FUN_142451840: *key|=1 (SFX_ALPHA_BLEND) if *param_8==0 && *param_10==0; |=2 (ADD_NO_ALPHA) if *param_8==0 && *param_10==1. Tracing param_10 origin.

## Step 1 done (2026-10-28 ~23:58 KST): blend selector PROVEN → commit 2454ba3 (pushed)
- Decision: a .td with no blendMode (66/107) → keep additive (HEURISTIC). The preset default is not on disk, and the capture shows One/One.
- Decision: UE premul_a → Translucent master (straight hdr, opacity va·a). This composites identically to One/InvSrcAlpha of premultiplied rgb, so no new master is needed.
- Decision: browser blend/premul_a → premultiplied One/InvSrcAlpha (visually equal to the game's SrcAlpha/InvSrcAlpha straight output).
- Exe deployed: old exe → ForgeLens.exe.bak3. New sha256 AF4B19CEBFDBBD9B68A4D3A0A1A83AFD9C1522693A0239671E74105B5886E462.
- Deferred question: is the sfx preset default really add? This could be confirmed if the preset files ever turn up (`_presets_` dir).

## Steps 3+4 (2026-10-29 ~00:00–01:10 KST)
- Order: step 4 code (generic hem fixes) was written and measured on the box BEFORE the UE visual check, so a single old/new UE comparison covers both steps (3 = verify mtl1-43 physics, 4 = improve hem spikes).
- Step 4 decision: the spikes were rotation-fit flips, so the cluster-fit rotation step per frame is capped at 30° (SM2_PHYS_FIT_MAXSTEP, 0 = off; APPROXIMATION). Collider sub-step lerp (SM2_PHYS_COLLIDER_LERP) is kept on because it removes about 15% of the >30° steps in combination with the cap. A higher FitPrior (1 → 4) was rejected because it freezes the cloak.
- Step 4 decision (Cadian finger): the finger-curl tool now skips fingers whose |gap| < 5° (MIN_GAP), which fixes the titus regression. The ~6° clip-dependent jog residual stays final, because a generic retarget offset can't fix a per-clip difference.
- UE check: the imports are untextured (the pct textures were not moved into <set>\cc_calgar). The check compares shapes only, so this is acceptable.
- UE learnings (for the next agent):
  - Editor-world SkeletalMesh and PoseableMesh components never re-evaluated from the uemcp bridge (the pose stayed at the reference pose).
  - EditorAssetLibrary refuses to work in play mode ("The Editor is currently in a play mode"), so assets have to be resolved before SIE.
  - SceneCapture ShowOnly lists must be re-pointed at the PIE copy.
  - The working recipe: spawn in the editor world → SIE → play_animation + rate 0 + set_position → wait 1 s → capture → export → stop SIE → destroy the actors.
  - The SIE I started was stopped every time, and the level (PhysTestMap) was not saved.
- Viewport realtime was switched on through LevelEditorSubsystem.editor_set_viewport_realtime(True) during the attempts. The previous state couldn't be read; on is the editor default, so it was left on.
- Steps 3+4 pushed as 4df3cab (mtl1-45).

## Step 5 (gun-skin attachments), ~01:00 KST
- Scale 0.7 comes from purity_seal_01.cls via the IactorDesc type `__type` (HEURISTIC: inheritance that works like ssl .cls).
- The attachment is merged into the weapon mesh (one skeletal mesh, with the seal bones under purity_seal_N) rather than exported as separate files, so the UE import and animations keep working unchanged.
- Paper strips: paper_rend is exported static in its bind pose; paper_sim follows the --include-cloth-sim setting. The seal cloth is not baked (APPROXIMATION). A generic attachment cloth bake would be a separate step.
- The Forge Lens preview is not changed (export only). Note: the browser picker cannot show the seals yet.
- Verification was done with box renders of the real CLI exports (skin_03, skin_04) instead of a UE import, to save time; the geometry and placement are visible in those renders.
- The tpl→uid pick: wpn_bolt_rifle_01 resolves to arifle_bolt_rifle (existing UidFor ordering, not changed).
- Step 5 pushed as 2ce469a (mtl1-46).

## Step 6 (mtl1-47) — melee materials + power fist hand hide — pushed c95af37 (2026-10-29 ~01:15 KST)
- DECISION: apply skin `material` by texture naming convention `<base>_<mat>{,_nm,_spec,_em}` (HEURISTIC, matches every shipped red/blue/... td set). Swap only when the sibling exists.
- DECISION: material skins mostly switch actor (meleeActorOverride / overridedActorName). Implemented following the override: exporting the artificer tpl with skin=01_red gives the in-game look; base tpl + 01_red just notes "no siblings".
- FOUND+FIXED: melee factory nameTpl was read from the wrong path (properties.geom instead of type.properties.geom), so artificer/relic actors mapped to the class tpl. Changes ActorsFor results for those tpls (more correct).
- DECISION: honored melee excludeObjectsToApplyMaterial (from skin actor + override actor).
- DECISION: power fist hand = RIGHT_GAUNTLET section active region minus visibilityUnchangableMeshes (HEURISTIC; section enum in bytecode not decoded). Hides raider_hand_R_01 + raider_arm_R_01 on the raider; visual check OK (m47_powerfist.png).
- DECISION: if the tpl has none of the fist objects (engineer), keep the hand + note, rather than leaving a stump.
- LIMIT / deferred Q: bolt rifle artificer tpl maps to arifle_auto_bolt_rifle (pre-existing UidsByTpl from firearm_versions cls); preview doesn't swap materials; Forge Lens exe not redeployed for this step (CLI only).
- 01:13 KST: parent reports UE editor restarted 01:07 (PID 71736); unsaved mtl44 test MIs/PhysTestMap lost (autosaves in Saved\Autosaves.bak8). DECISION: not regenerated — step 3 evidence PNGs already exported (Captures\mtl_fix\mtl44); assets were scratch.

## Step 7 (mtl1-48), 2026-10-29 ~01:25 KST
- Static RE finished: ApplyMaterialSuffix → per-submesh material variant from the diffuse td `materials` block (proved with td/wpn_chainsword_01_artificer_01_01.td). Decided to replace the mtl1-47 naming heuristic with the td block (kept naming as fallback) because blue/white/yellow/violet were wrong (they reuse red/green NM/spec).
- Did not change tpl layer subMaterial ("default") in materials.json; informational only.
- VFX bench rerun at c95af37; old-vs-new sheets vs the 6 in-game photos; no in-game captures taken.
- Did NOT redeploy Forge Lens exe (preview doesn't use skin materials; CLI-only change). Exe sha unchanged AF4B19CE…; .bak4 unused.
- Pushed 30563e4 via push.ps1. HaloCEA tests (4) fail in full-solution run: unrelated project, needs Halo data, not touched.
- REVISED (~01:26 KST): deployed Forge Lens after all, since the deployed exe (2454ba3) lacked steps 5–7 export code. Published at 30563e4 (PublishProfile=win-x64); old exe → ForgeLens.exe.bak4 (AF4B19CE…); new sha FF7E06BBE6141A8FC38C53DA2B1F7B2097C53131BBB06091A114FFFF966E05B9; smoke launch OK (window title shown), closed. Temp copy removed.
- 03:17 commit bddd777 (items 2+3 code): firearm Resolve rule = explicit uid= > skin override actor (mgs/ before firearm_versions/, then shorter uid) > own actor > prefix owner; colour schemes limited to skin-library materials; fuzzy W token (HEURISTIC) for power_halberd/one-handed hammer names

## Items 6–8 (context playback, reverse direction, toggles), 03:20–03:46 KST
- DECISION: one shared rule set (`LibSaber.SM2.Assets/Context`) for both directions: ContextIndex (all 2510 .cls + .cls.resource, built once per catalog in parallel) + ContextResolver.
- PROVEN links (game data): `.cls.resource` linksDesc = owned .sso blueprints, linksDescVisual = owned .sfx; class `sfx.list` key → effect; prop_inventory_client actor descs → held weapon; prop_model_weapon_holds → hold locator; blueprint morpheme (duration) events → VfxRestart/StartSfx uid → effect. Sorcerer chain: fx_terminator_sorc_stave_ray_prep ← wpn_melee_force_stave_terminator (resource) ← npc_occult_terminator_sorcerer_client inventory; event vfx_ray_prepare.
- HEURISTIC (labelled in the UI): character from the clip bundle suffix when no class has the model's exact tpl (rubric marine); weapon by clip-name tokens when the inventory has none (rubric_bolter_client for rubric_*_shoot_*); `Shoot` track event → weapon `muzzle*` sfx slot (no blueprint link found for the Shoot track); clip ranking when no clip carries the effect's event; hand-bone fallback when the hold locator is missing.
- DECISION: forward search ranks characters with an event link, PROVEN, non-_base, non-auto_ first (the _base class was found first before).
- DECISION: the weapon is merged into a copy of the model under the hold locator (prefix "wpn"); the weapon's own blueprints / sfx resolve their sockets in the weapon scope first (wpn_<socket>), so the stave ray sits on the stave head (stave_fx_src_1), not a same-named character bone.
- DECISION: QA env vars for screenshots without clicks: SM2_CTX=1, SM2_CLIP, SM2_TIME (pause), SM2_CAM (yaw:pitch:dist), SM2_SHOW_PROPS=0, SM2_SHOW_FX=0, SM2_CTX_CHAR/CLIP/WEAPON; SM2_OPEN now also accepts a bare file name.
- FIXED during GUI test: VfxPlaybackChanged fired inside InitializeComponent (NRE crash) → guarded; all async void context handlers wrapped (errors go to the status line).
- Screenshots (Captures\mtl_fix\mtl49): ctx_sorc_stave_ray_prep.png (Sorcerer + stave + cast clip, ray prep on the stave head at 1.4 s), ctx_rubric_fire_weapon_muzzle.png (rubric marine shoot clip + bolter + pink muzzle flash at 0.07 s), ctx_rubric_toggles_off.png (both toggles off: model + animation only).
- Known limit: the rubric marine preview is untextured grey in this view (pre-existing preview material resolution, not part of these items).

## Item 4 — final cleanup (04:15–04:35 KST)
- PC files (C:): deleted 140 paths, **142.34 GB freed** (C free 50.4 → 192.8 GB):
  - Exports test outputs: bulk (89.6 GB), anim_survey (21.3), bulk_td (4.6), re14e (4.3), mtl47 (4.1), mtl15 (3.4), mirr15 (2.6), mtl_cham, mtl16, mtl_phys, mtl27, mtl18/26/26pre, mtl_fix, mtl44, mtl32, mtl37, mtl31, mtl43, mtl46, mtl30, mtl48, mtl39, mtl40.
    Kept: bulk_fix, chars_phys, chars_cloth, retarget, physics-related, mtl49 and all early/user-looking exports (Titus_Unreal*, refrt_*, etc.).
  - Bin: ForgeLens_mtl49test, ForgeLens_mtl38, SM2Browser_vfx3g, SM2Extractor_{15m,18,22,mtl27,30,31,37,43,44,46,47,48}. Kept SM2Extractor_mtl49 (current CLI).
  - ForgeLens_latest: .bak1–.bak4 deleted; **.bak5 kept** (latest backup of the deployed exe).
  - Raw: mtl49_ctx/ctx2/ctx3/ssl/texts, mtl48_td.
  - C:\Users\Revaptor_FX agent transfer zips (cli*, ctx49*, ssl49, texts49, ev48, m47/48_check, ext, m, SM2Extractor_*.zip, sm2x6*, LibSaber.SM2Extractor-9474df0, SM2Browser_e6cadfa, mtl15*/26*/35_lut/44_phys, cap18, cpp*, f14*/f15*, s15*, matj*, mp*, mir*, btl_push*). Untouched: BongGIF_src, bonggif_handoff, frameflow_desktop_src, _animthumb/_bongthumb, sani_pick, step3_captures, side_by_side*, single-letter zips.
- UE (/Game/SM2_Work/Tests only, deleted in the editor via EditorAssetLibrary.delete_directory after an AssetRegistry referencer check; script Scripts\mtl_fix\ue_tests_cleanup.py): bulk, mtl15/16/18/26/26pre/42_detail/43/44, mrq16, mirr15, mtl_fix, mtl_cham, mtl_phys, anim_survey, anim_finish(2), v4, rhino_stacked = **≈16.2 GB** of .uasset. Skipped (referenced from outside Tests): pre_step3 (← Animations/cc_swarm_hormagaunt), sani_before (← Props/hologram_planet). Kept: bulk_fix, chars_cloth, chars_phys, physics, retarget. Nothing saved, Original/* and /Game/Work/* untouched; Lightning_01 not saved.
- Box: old publish dirs (pub, pub18, pub_FL38, pub_FL42b, pub_ForgeLens, pub_SM2Browser, pub_SM2Extractor, pub_mtl30/31, pub_x22), /tmp/p4x patches, m49 transfer zips = 716 MB. Kept pub49 (= deployed build).
- Total ≈ **159 GB** freed.
- Token answer: yes — one cleanup pass at the end is cheaper (one inventory + one delete script instead of re-listing folders after every step).

## 04:50 KST — REA queue (a)(b), SYMBOL_DB rename, push
- Pushed 2 commits: rename RE_MAP→SYMBOL_DB, mtl1-50 (REA daemon tools, rea_string_xrefs.tsv + generator, coverage, REA-first docs, HEURISTIC notes). Tip 28489a0.
- Decision: generator-based coverage (5,062 asset funcs, 193 covered = 3.8%) replaces the earlier ad-hoc count (4,947/181) — reproducible from tools/symbol_db/rea_string_xrefs.py.
- Decision: no behaviour change for exposure k (adaptiveIntensity lead INFERRED only); blendMode default unchanged (now PROVEN add).
- Exe not rebuilt: only comments changed in C# (deployed 463f383 exe is functionally current).

## mtl1-51 (2026-10-29 ~10:20-10:55 KST): sfx effect submeshes in models
- Pushed 81f9a49 (on 167c99f, ff-only via push.ps1). Deployed ForgeLens_latest\ForgeLens.exe sha256 97de5179…01ae (built from fcc16bf + mtl1-51; 167c99f is docs only); previous exe → ForgeLens.exe.bak6.
- Decision: hide by default instead of drawing at rest. td variant `preset = "sfx"` is the PROVEN shader class; hidden-at-rest INFERRED from dynamicAlphaMask (FUN_1425f1900/FUN_1425f1cb0) and power-state glow. Placeholder-material locators (sfx_trail, exaust_source, …) need a name filter (HEURISTIC) because characters have real placeholder geometry (backpack, TECH_robo_hand*).
- FX parts toggle draws them additive (missing blendMode = add PROVEN mtl1-50) with td tint over part.Texture; sfx `tex`/layers/scroll not emulated, so power sword glow shells read as flat white when toggled on (HEURISTIC, known).
- Character colour picker: not supported (wpn_ gate in LoadColoursAsync; ColourSchemesFor = weapon td `materials` variants filtered by weapon skin libraries; armor colours are armor_color_library.sso CustomizationLayerParamsProject per-layer tint/patternTint/spec_metalnessTint with no scheme source or preview application path). Not implemented, per request.
- 11:37 KST mtl1-52: campaign Titus *_02 armour has no td CC block → scheme only recolours helmet/acc/Crux pauldron on cc_titus (PROVEN); demo also on cc_sm_soldier_story. Colours look darker than in-game (material tint buffer/exposure not emulated).

## trail53 (2026-10-29 KST, weapon swing trails)
- DECISION: weapon trails = sfx_trail_triangle (PROVEN FUN_141848c40/FUN_1418296f0/FUN_141839b50); drawn as stateless ribbons from re-posed clip frames (120 Hz) inside start→stop-pack windows; builder rule HEURISTIC. Distortion trails skipped. cls sfx.list override merged for swept trails only.
- DECISION: REA daemon Ghidra was OOM-killed; not restarted (memory, 3 other workers) → headless DecompAt fallback (3 GB).
- DECISION: ledger F52/F54 were taken (phys52, mtl1-52) → trail row is F55.
- NOTE: deploy race — another worker's 11:30 exe got moved to ForgeLens.exe.bak8 by my 11:37 deploy; redeploying a combined build (origin mtl1-52 + trail53) to fix.
- DECISION: screenshots via Scripts\mtl_fix\trail53\fl_shot_pw.ps1 (PrintWindow, SetWindowPos NOACTIVATE, Idle) — no focus steal.
- trail53 final (11:52 KST): combined exe E91EDF1E… deployed to ForgeLens_latest (F91E trail-only → .bak9). Pushed ff-only via push.ps1, origin tip 4c4155a.
