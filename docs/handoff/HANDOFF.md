
## R97 (F43 .td params) 2026-10-31 03:18 KST
- import_sm2_materials.py: NormalScale param (lerp flat->normal) set from tdScalars.nmScale; EmissiveStrength set from tdScalars.emissiveIntensity (only when slot has emissive map).
- Verified (tools/ue/ue_r97_tdtest.py, /Game/ForgeLens/SM2/Tests/r97/mtl_{titus,vortex}): params land (titus head 1.3/0.1, helmet emissive 30; vortex body 9, tentacles 0.7); titus lit capture OK, eye lenses glow red, no blowout.
- Vortex orb: emissive 9 only brightens spike tips; the in-game glowing orb is NOT mesh emissive (likely VFX/particles) -> not fixed by materials.
- Hormagaunt live MIs reparented to M_SM2_r96 (ue_r96_apply.py; undo = reparent to M_SM2).
- NOTE: existing /Game/SM2_Work characters not yet re-imported with R97 importer (scalars apply on next import).

## R97b/R97d 2026-10-31 04:06 KST
- Migration /Game/SM2_Work -> /Game/ForgeLens/SM2 (owner t260u), tools/ue/ue_r97_migrate.py, editor -unattended:
  * Backups: K:\SM2_Backups\{SM2_Work,ForgeLens}_premigrate_20261031 (9.0 GB + 3.3 GB).
  * Attempt 1 (03:24-03:29) crashed (the 'Continue with rename?' prompt for maps + Message Log window -> D3D11 CreateSwapChain 80070005). Rolled back byte-exact from the backups (3593 new files removed, 2194 restored, robocopy /L diff = 0).
  * Attempt 2: 4586 assets moved (maps excluded), 0 failed batches. SM2_Work/Characters/cc_calgar -> ForgeLens/SM2/Characters/cc_calgar_work (name clash with the existing FL cc_calgar). 24 referencers re-saved; 1417 redirectors kept (1411 now unreferenced), NOT deleted yet.
  * Stayed in /Game/SM2_Work: Original (protected) and 7 maps (PhysTestMap, r54 L_hub_battle_barge, r63 Map_Cloth, r67 Map_calgar/Map_vortex, r72 Map_Mats, r90 Map_r90).
  * Verified (ue_r97_verify.py): 6 FL character meshes load with skeleton and all MIs resolve. Physics assets on calgar(both)/sorcerer/vortex; hormagaunt/titus have none (as before).
  * 68 tool scripts repointed to /Game/ForgeLens/SM2 (maps + Original kept). SM2PhysicsSetup plugin has no SM2_Work path (nothing to change). Importer default already /Game/ForgeLens/SM2 (fl_roots.py / UnrealDirect.cs).
- Map_Floor blowout (t263u): the cause was 24 R95c point lights (key 800 / green rim 2500 / fill 150) + PPV manual bias 8, not R97 emissive (Vortex MIs EmissiveStrength 0/1). Now: 0 point lights, 1 warm directional 3 lux + DaylightAmbient sky 3, PPV and captures manual bias -5 (ue_r97_floor.py 3 3 -5). Captures Shots\r97_floor\s3_*.png: no clipped pixels. Vortex centre mean 35,39,48 vs game crop 95,100,103 (the game orb/body glow is VFX, see R97h).
- R97h Vortex orb: game data proves it is VFX: blueprint_vfx_vortex_beast.sso starts 'fx_vortex_beast_back_idle' (CPU emitters, emissiveIntensity overrides). Exported to Exports\r97\vortex_vfx\export (niagara.json + 6 textures). Not yet built in UE.

## R97a applied 2026-10-31 04:14 KST (new root)
- ue_r97_apply.py on the 6 /Game/ForgeLens/SM2/Characters meshes: M_SM2_r97 + .td scalars (calgar_work 8, calgar 8, titus 6, vortex 10, hormagaunt 0, sorcerer_exalted 17 slots). MI backups in /Game/ForgeLens/SM2/Tests/_bak_r97/<char>; old masters kept.
- Map_Floor capture after shaders compiled (docs/handoff/r97/floor_r97.jpg): no clipped pixels. Vortex spike tips now glow blue (emissive 9); Vortex centre mean 38,47,59.
- R97b Titus side-by-side: docs/handoff/r97/titus_sbs.jpg (in-game photo mode vs UE Map_Floor). Blue armour and gold aquila/trim match qualitatively; the game scene is darker and warm-lit. Calgar: no user/in-game screenshot this session, so only the material/.td check was done (8 slots carry tdScalars, head nmScale 0.5/0.65 applied).

## R97c-item (muzzle/aim) 04:22 KST
- ue_seq_fire.py (R59 tool, new root): LS_arifle_bolt_rifle in /Game/ForgeLens/SM2/Sequences/BoltRifleFire_r97. Aim clip = tacticus_wpn10_aim_hold_intercessor (from MUZZLE_AIM.tsv arifle_bolt_rifle_client.cls, wpnType 1). Aim section swapped (1), 8 MuzzleFlash bindings attached to socket SM2_shootTip.
- The combimelta variant (wpn151 aim clip) is not imported, so it can't be built yet.
- Not verified visually: no viewport frame render of the sequence.

## R97h Vortex orb 04:22 KST
- Game data: npc_vortex_beast_client.cls attaches fx_vortex_beast_back_idle to nameParent "DISK" (isAutoStart). The sfx has 23 emitters; the main ones (whirl_lines/whirl_core/core) are MESH particles of tpl/fx_vortex_beast_lwis (5 parts, texture vfx_vortex_beast); the sprite emitters are spark_01 sparks (6-11/s), runes (66/s), warp (22/s) and inner_glow (intensity 1111).
- tools/ue/ue_r97_orb.py: imports the lwis mesh + texture into /Game/ForgeLens/SM2/VFX/vortex_orb, builds M_vortex_orb (additive unlit, rotator 0.15, OrbColor 1,0.35,1, OrbIntensity 20; colour/intensity HEURISTIC) and attaches R97_vortex_orb to bone DISK of R92_vortex in Map_Floor. ue_r97_orbxf.py sets the relative transform: roll 90, scale 0.3 (HEURISTIC, fitted to the ring).
- docs/handoff/r97/vortex_orb_sbs.jpg: the swirl now fills the ring like the game. The game's bright magenta-white core (inner_glow sprites) is still missing.
- NOT Niagara: UE 5.7 Python has no emitter/module authoring API, so the sprite emitters are not reproduced.

## R97 (d)(e)(f) 04:24 KST
- (d) Physics, from game data (SM2Extractor export log/physics.json):
  * Titus (pc_marine_pve_story_titus_assault_client.cls): 6 cloths, 34 jiggle bones. ue_sm2_physics.py physasset created /Game/ForgeLens/SM2/Tests/physics/PA_cc_titus_assault_sm2 and assigned it (was None).
  * The imported Titus lacks the cloth helper bones (*_physNN), so baked cloth needs a re-import of the physics-on FBX (not done).
  * Hormagaunt: the export writes no physics.json and the log has no physics line, so the game data has no cloth/jiggle for cc_swarm_hormagaunt. Nothing to set up.
  * Vortex, Calgar and Sorcerer already had PAs.
- (e) Level/props: the R54 levelbench hub_battle_barge level (map still /Game/SM2_Work/Tests/r54_level/L_hub_battle_barge) survives the migration: 1441 actors, 1401 static mesh components, 0 missing meshes, 1390 unique meshes under /Game/ForgeLens/SM2/Tests.
- (f) Sky/env from game data is already applied on that level (tools/level/sky_env.py state): SkyLight = game cubemap T_buttle_barge_sky_1 (now /Game/ForgeLens/SM2/Tests/r54_level/Sky), angle -44.33, intensity 6; fog colour (0.039,0.049,0.081) = game hbbm_fog_v2 (40,50,83); atmosphere hidden. Game-unit to UE mapping is HEURISTIC. Not re-captured this session.

## R97g commissar coat plugin 04:29 KST
- Bug found: in FitScale the 'Shallow' counter was never incremented, so sm2.RestFitShallowFrac could never trigger. That is why R93 saw floorSkipped=0 everywhere.
- Fixed and added a per-shape log ('restShape capsule|sphere <bones> r preFloor raw final shallow deep freeParticles') plus a per-cloth 'restParticles total'. Defaults unchanged (RestFitShallowFrac 1 = always floor).
- Built pkg97 (BUILD SUCCESSFUL, Build\sm2phys\build97.log) and installed. Backups: installed plugin -> Build\sm2phys\installed_SM2PhysicsSetup.bak29; repo source -> Build\sm2phys\src_SM2PhysicsSetup.bak1. Editor restarted with -unattended.
- Measurement (ue_r90_cloth.py prep commissar, Shots\r97_coat\{cur,sf04}.txt):
  * coat_SIM_01 (3021 particles, 1113 free) has ONE capsule BACKdown->LEG2L r 9.2/12.5: preFloor 0.286, 46 shallow (4.1%), 0 deep. The 0.90 floor raises it to 0.90. This CONFIRMS the R92 inferred cause.
  * With sm2.RestFitShallowFrac 0.04: coat final 0.286 (floorSkipped 1). The straps (566 free, 30-45 shallow) also drop to 0.77-0.88 (floorSkipped 4).
  * The sim A/B (skin/pen counts on the run clip) was NOT run.
- 03:50 ensures: two handled (non-fatal) ensures at editor start (frame 0, 03:50:00), AnimNode_RigidBody.cpp:1156 'SkeletonBoneIndex >= 0' and :1915. A RigidBody anim node references a bone missing from its skeleton.
  * Map_Floor has no ABP on any actor (ue_r97_rbcheck.py), so the source is something loaded at startup: BongThumbLive asset thumbnails or a chars_phys ABP.
  * INFERRED: the commissar/tech_priest meshes in chars_cloth2 use chars_phys skeletons (mismatch), and the log shows a linker warning on PA_cc_commissar_SM2.
  * No symbols in the stack, so the exact asset is not pinned.
