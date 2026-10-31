# POE2 M3 - levels (.tgm terrain), hideout in Unreal, cart rhoa physics, PIE swing check (2026-10-31 KST)

## M3.1 .tgm reader and room assembly (done)
- Code: `src/LibSaber.SM2.Assets/Games/Poe2/Poe2Terrain.cs` (`Poe2Reader.ReadTgm`, `TdtTile`, `LoadLevel`), `Poe2Source.PrepareLevel`,
  CLI `--poe2level <prepared> <out> <room.arm> [<ggpk> <tool dir>]` (Bench/Poe2Bench.cs), viewer PoE2 window tab **Levels** (English strings, test hook `FL_POE2_LEVEL=<arm>`).
- .tgm layout, PROVEN on all 24 cosmic tiles (every index < vertex count, shape index ranges add up to the index count):
  `u8 version(4) | 6 f32 bbox | u8 shapeCount | u8 | u32 vertexCount | u32 triangleCount | 12 B unknown |
  shapeCount x {u16 id, 6 f32 bbox, u32 indexStart, u32 indexCount} | vertexCount x 24 B {f32 xyz, u8x4 normal (-128), u8x4 tangent, f16 u, f16 v} | u16 indices`.
  Edge tiles end with 142 more bytes (unknown, unused). u32 indices above 65535 vertices is an assumption (no such tile seen).
- .tdt: `u32 14 | u32 string-table chars | UTF-16 strings`; the first string is the .tgt (PROVEN on 27 tdt files). .tgt JSON gives `tile_mesh` + `normal_materials` (`uint` = shapes per material, same run-length rule as .sm).
- Placement (INFERRED, no in-game check): 1 grid cell = 1 tile = 250 units; doodad x/y in 23 sub-units per tile (waypoint 219,219 = centre of 19x19).
  K cells use the .tst tile, S cells the .gft `default` fill, plus a ring of `OuterBufferSize` (6) cells with the .gft section named by the `OuterGroundType` .gt (`CosmicGround`).
  In the space hideout every one of these .tdt points to the same `cosmic_fill_hideout.tgt` (81 verts, 128 tris, `void_hideoutc.mat`), so tile rotation, which is not applied, makes no visible difference here. It will matter for other areas.
- Space hideout result (PC `PoE2\m7out\poe2level.txt`): 961 tiles (31x31), 4 doodads (waypoint core, map device cauldron, stash, guild stash) from their .ao -> SkinMesh .sm, 84,303 verts, 129,859 tris, 22 parts, 24 PNGs.
  Files: PC `PoE2\m7out\space_hideout.glb` (27.8 MB, embedded PNGs) and `space_hideout.fbx` (Unreal space). Not added to the repo (size).
- Materials: `void_hideoutc.mat` is CelestialMaterial + ProjectiveUV_Accurate (`[4, 2048, 2048]`). The base colour is an 8x8 texture. UVs are world-projected at 1/512 per unit (INFERRED), and `01. Space Texture` is kept as the emissive map (approximation: no scroll, no parallax). The waypoint mesh uses `transparentobjectsc.mat` (invisible in game; shown here). Chest filler items have only normal maps found (role names not matched).
- Viewer: `docs/handoff/poe2_m3/viewer_level_t120_0.png` (PoE2 window, Levels tab, "84303 verts, 129859 tris, 22 parts (64.5 s)" incl. extraction) and `viewer_level_t120_1.png` (main viewport: 31x31 floor, the 4 objects small at the centre). The camera frames the whole floor, so the objects look tiny. Ground shows flat grey in the preview (the celestial look is not modelled).
- NOT done: tile rotation from the .arm edges/corners, .gt/.et ground blending and ground masks, .tgm tail bytes, decals, lights/.env, other areas (only the space hideout was tested).

## M3.3 Cart rhoa physics (done, not seen moving yet)
- Export: `--poe2export m4 ue_exp3\cartrhoa art/models/monsters/rhoa/cartrhoa.sm art/models/monsters/rhoa/rig.ast *` with `FL_POE2_ALLCLIPS=1` (6,631 verts, 7,862 tris, 6 parts, 52 bones, 10 clips).
- Unreal: `tools/poe2/ue_poe2_import_safe.py` (new: same import as ue_poe2_import.py but saves only its own assets and never deletes; the old `save_dirty_packages(True, True)` would also save other workers' dirty maps) -> `/Game/ForgeLens/POE/cartrhoa`: SkeletalMesh, Skeleton, 10 AnimSequence, 8 textures, 12 MaterialInstanceConstant (6 MI_<slot> + 6 importer FBXLegacy MICs left in place, no deletions).
- `tools/poe2/ue_poe2_physics.py` -> PA_cartrhoa_SM2 + ABP_cartrhoa_SM2Phys: simulated 17, kinematic 3, constraints 17, missing bones 0, compiled 1 (PC `PoE2\m3_cart_phys.json`).
  17 bones: mouthpiece ropes L/R, saddle cords L/R x2, tail feathers (centre x3, L x3, R x3), undertail L/R. Values are the plugin defaults (HEURISTIC, no game values found).
- Runner `tools/poe2/ue_poe2_go.ps1`: one locked EngineWire call (waits for editor-free, refuses on NEEDS_ACTION / game / no editor, named owner, always releases).

## M3.2 UE level import (space hideout)
- Assets: /Game/ForgeLens/POE/Levels/space_hideout (SM_space_hideout, 22 MIs, 23 textures, L_space_hideout); celestial master /Game/ForgeLens/POE/_Shared/M_Poe2Celestial (emissive R*(1.15,3.5,4)+B*(1.5,8,12), x0.1, tile 0.3) replaces the 8x8 white base.
- Captures (RGBA16F non-sRGB RT): docs/handoff/poe2_m3/ue_level_{overview,room,objects,top}.png, sheet_ue_level.jpg.
- Note: to switch maps, another worker's dirty L_hub_battle_barge was left via new_blank_map(False) without saving (its saved 15:21 file untouched; unsaved post-save edits, ~12 SceneCapture2D actors, were discarded). The saved hub map was reopened afterwards.

## M3.4 PIE swing check (editor PIE only)
| run | tune (world,damp,stiff,dampDrive,swing) | bellcrow jump/settle | lich jump/settle | cartrhoa jump/settle |
|---|---|---|---|---|
| baseline | 0,-,none | moved=0 | moved=0 | moved=0 |
| t1 | 1,0.5,100,8,1 | 23.0/23.0 POP | 101.3/38.3 POP | 18.9/5.3 POP |
| t2 (kept) | 0.4,0.9,400,40,0.5 | 15.9/11.5 POP | 53.3/23.4 POP | 8.5/2.2 JITTER |
All bones now move under physics (20/7/17), nan=0. Lich tail and BellCrow bells still pop above threshold: NOT verified good; values are heuristic. Frames: pie3_rest.png, pie3_swing_1..6.png, sheet_pie3_swing.jpg.
