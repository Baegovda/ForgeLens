
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
