# HANDOFF — SM2 → Unreal asset pipeline (read this first)

For any agent continuing this work: read this whole file before you do anything.

## Owner and how to work with them
- The owner is india (address them as india님). They speak Korean, so reply in Korean.
  - They want a short progress report at every stage, not just at the end ("중간중간 보고").
  - When they say "실행해줘", launch `LibSaber.SM2Browser.exe` on their PC.
- **Investigate by reverse-engineering first (mandatory).** For any file-format, material or shader problem:
  - Reverse-engineer the game exe with REA/Ghidra/rizin before guessing from bytes.
  - Cite the addresses or strings you based each conclusion on.
  - Do not guess from bytes.
- **Goal:** an explorer-style GUI that browses Space Marine 2 resources, shows previews, and exports only the selected assets to Unreal or Blender. It is not a bulk extractor.

## Environment
- **Owner's PC (Windows, "Revaptor-FX")**
  - Tools: `C:\Users\Revaptor_FX\SM2Extractor` (`LibSaber.SM2Browser.exe` GUI, `LibSaber.SM2Extractor.exe` CLI).
  - Game: `D:\Steam\steamapps\common\Space Marine 2`.
    - Paths on D: can be refused. If so, copy the file to `C:\Users\Revaptor_FX\` first.
  - Reference viewer: IndexV2 at `C:\Users\Revaptor_FX\IndexV2`. It has no .sani decoder.
- **Unreal Engine 5.7.4** project at `K:\Unreal Projects\UE_VFX_57`. Log: `Saved\Logs\UE_VFX_57.log`.
  - **UEMCP (editor control).** The editor listens on 127.0.0.1 only. Run on the PC in PowerShell:
    `cd C:\Users\Revaptor_FX; $code=@' <python> '@; @{code=$code}|ConvertTo-Json|Set-Content -Encoding utf8 ue_args.json; & python uemcp_call.py ue_python ue_args.json`
  - **Screenshots** only work when the Level Editor viewport is visible. Moving the viewport camera from a script often does nothing.
  - **Importing animations.** Import animation-only FBX with the legacy importer:
    1. Run console command `Interchange.FeatureFlags.Import.FBX 0`.
    2. Import with FbxImportUI set to FBXIT_ANIMATION and the target skeleton.
    3. Set the flag back to 1.
    - Reason: Interchange in 5.7 has bug UE-307815.
- **Box (shared Linux machine):** repo clone at `/workspace/LibSaber`.
  - The game exe is at `/workspace/rea/sm2_retail.exe` (81 MB).
    - The REA MCP times out on it; run Ghidra headless or rizin directly instead.
  - The .sani spec and a reference decoder are in `/workspace/rea/out/` (`SANI_FORMAT.md`, `decode_sani.py`).
  - Push: on the box `git bundle create /workspace/sm2.bundle <lastpushed>..HEAD`, CopyFromBox to C:\Users\Revaptor_FX\sm2.bundle, then in C:\Users\Revaptor_FX\ls_push: `git fetch ..\sm2.bundle HEAD; git merge --ff-only FETCH_HEAD; git push origin HEAD`.
- **Repo:** https://github.com/Baegovda/LibSaber. PR #1 is on branch `cursor/sm2-pck-extractor-4420`.

## Build and install
1. On the box: `dotnet publish` both projects to `/workspace/sm2cli` and `/workspace/sm2browser`.
2. Zip each output with Python.
3. Copy the zips to `C:\Users\Revaptor_FX\` on the PC.
4. On the PC: stop the browser process, then `Expand-Archive -Force` into `SM2Extractor`, then delete the zips.
- `dotnet test` hangs at package restore on the box.

## Current state (2026-10-26)
- **Meshes, skeletons, textures:** all work.
  - Exporting the 47 slots of the character cc_titus_assault gives 41 slots, because the cloth sim meshes are excluded.
- **Cloth sim meshes:** `*_SIM` parts are cloth simulation meshes and the game never renders them.
  - Evidence: `simMeshName` at 0x1435f1440, `renderMeshes`, and the nvcloth library.
  - They are excluded by default; `--include-cloth-sim` keeps them.
- **Texture channels:** the label `(a)` is a logical channel that the game remaps in the shader (`swizzleTexInShader` at 0x14233bf50).
  - For BC5 (DXN), Roughness is stored in g.
  - For BC4 (DXT5A), AO is stored in r.
  - The materials json records where each map is stored in a `stored` field.
- **Material script:** `src/**/import_sm2_materials.py`, which ships alongside each export.
  - Parents: M_SM2 (masked, two-sided), M_SM2_Translucent, M_SM2_ClothSimHidden.
  - Texture-to-mask links use the RGBA pin, because the RGB pin causes a compile error.
  - It assigns materials to both skeletal and static meshes.
- **Animation (.sani):** the class-A codec has been reverse-engineered.
  - Spec: `/workspace/rea/out/SANI_FORMAT.md`.
  - Key functions: reader 0x14222fe40, sampler FUN_142229bb0, root motion FUN_14222f1b0.
  - Rotation is stored as modified Rodrigues (three numbers that rebuild a rotation quaternion).
  - Idle, attack and run all work on cc_titus_assault_Skeleton.
- **Type tests:** Tyranid, weapon, prop and vehicle all import. Results are in `Desktop\SM2_TypeTest`.

## Open items (do them in order, one at a time)
1. **Tyranid color: resolved.** The grey look came from a capture taken before the textures had loaded; the diffuse textures themselves carry the real colors.
   - Commit ef29997 applies the material data's `albedo_tint` (stored as [A, R, G, B]) as the M_SM2 `AlbedoTint` parameter.
   - Not done yet: the second_spec (gold metallicTint) and anisotropic highlight layers.
2. **Transparent face slots.** Titus's head_01* slots are marked transparent. Confirm in the game what their alpha means before using the translucent parent on them.
3. **VFX.** There is no `.sfx` to Niagara conversion; files are extracted raw only.
4. **Rhino parts.** Check whether destroyed or variant parts are stacked on the hull.
5. **.sani gaps.**
   - F3/F7 second slot.
   - version >= 3.
   - F6 clips without `.sani_data` (preview frames only).
   - Root-motion direction: the run moves along −Y while the mesh faces +Y.
6. **Push and tests.** Push the local commits to PR #1 and get the tests running.

## Rules
- Keep this file updated at the end of each stage: state, open items, commit hashes.
- Never touch the owner's original assets in /Game/SM2. Test imports go under /Game/SM2_Test or /Game/SM2_vN.
- Before editing any of the owner's scripts or files, back them up as `.bakN`.

## 2026-10-26 update (Cursor Agent)
### Layout (all work lives here now; supersedes "never touch /Game/SM2")
- PC: `C:\Users\Revaptor_FX\Desktop\SM2_Work\{Exports\<asset>, Anim\Titus_Anim, Captures\{TypeTest,TypeTest_overview,Item2_head}, Raw\{TypeTest_raw,titus_td}, Scripts\}` (uemcp_call.py stays in home).
- UE: `/Game/SM2_Work/{Original/cc_titus_assault (old /Game/SM2), v4/cc_titus_assault, cc_titus_assault, cc_swarm_hormagaunt, refrt_imp_rhino_01, refrt_tyr_arch_column_01, wpn_bolter_heavy_01}`; 595 assets moved via rename_assets, 0 redirectors.
- Capture: Scripts/ue_cap.py, two calls (capture, then export) - same-call export returns a stale RT.

### Item 2 RESOLVED - head_01* alpha
- Translucency is driven by the variant `glt.blendMode`, not `Transp(a)` in format_descr. Enum string table @0x144cc9190: opaque, blend, add, add_no_alpha, add_smooth_no_alpha, premul_a, mul, mul_inv (default opaque); "blendMode" @0x14355b708. akill @0x14364a800 (table 0x144ccc7e0), compared at 0x14173bb19/0x14173bb54.
- ch_titus_head.td default (skin) variant has no blendMode -> opaque; only `hair` variant is blend; ch_eye_shadow_01 = blend/glass.
- Exporter: MaterialMaps.BlendOf: non-opaque blendMode -> translucent, else AKill -> masked, else opaque; sidecar `blendMode`.
- Verified: head_01/head_01_7 -> M_SM2 (opaque), head_01_9/_2/_5/_10 -> M_SM2_Translucent, head_01_8 masked; Rhino glass still translucent; Hormagaunt head opaque; 41/41 slots imported.
- Visual: Captures/Item2_head/compare_iso.jpg (ue_cap.py head mode, `only,head` = every non-head slot gets /Game/SM2_Work/_Tools/M_Hide, masked opacity 0). Face, hair, eyes render complete in v4 and the new import; no holes in the skin. (M_SM2_ClothSimHidden still ghosts, do not use it for isolation.)

### Item 4 RESOLVED - Rhino stacked parts = LOD composite levels (not destroyed/variants)
- tpl object dump (rea/work/tpldump, objOBJ.Affixes): Object0001 = lod_compex_level+lod_composite_marker group; 2,3,4 = parts with `compound_skin_lod_index/index/0` + lod_composite_root_part_marker; group 5 -> parts 6,7,8 index 1; group 9 -> A,B index 2; C,D = markers with their own geometry (merged far proxies). All share one vertex pool, so the old export stacked 5 levels (52 slots, 219,544 tris) -> z-fighting.
- These affix names are not plaintext in sm2_retail.exe (searched; likely hashed), so evidence is the tpl data itself.
- Fix: IndexMeshVisibility.AddCompositeLods hides parts with index>0 and every marker sibling of an index-0 part (with descendants). refrt_imp_rhino_01 now 16 slots / 114,920 tris; _static.tpl uses the same scheme. Meshes without these affixes are unaffected.
- Verified: /Game/SM2_Work/refrt_imp_rhino_01_v2 (16 slots, 0 bad), Captures/Item4_rhino/compare.jpg (top = old stacked, bottom = fixed).
- Tools: tools/ue/ue_cap.py, ue_typetest.py (3rd arg = export folder name).

### Item 3 STEP 1 DONE - VFX .sfx -> Niagara-friendly param dump + textures
- Finding: every retail .sfx (2,381 under ssl/, 0 binary) is SSL text, not a binary particle blob. Node types from data and exe strings: CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0, sfx_part @0x14363c148, sfx_group @0x143646930, nameTex @0x143644760, lwiGeom @0x143645a70, ageSpline @0x143644580, constantFlow @0x143644cf8, emitDir @0x1436445f0.
- Structure: root/sfx_group `list = {name = node}`, sfx_part `emitters2 = {...}`. Choice fields are tagged unions: `__type = "local__X"` -> only member X is live, the other stored alternatives are dead editor leftovers (e.g. emission.type local__constantFlow {x,y} = particles/s; explosion = burst). typeParams kind default = sprite, lwiGeom/geomPrimitive = mesh (nameGeomTpl). Splines flat [t,v,...] over normalized age, colors 0..255.
- Exporter: Effects/SslText.cs (parser), Effects/EffectSummary.cs (union resolve + per-emitter digest). `export --asset X.sfx` now writes X.sfx.json (resolved tree), X.niagara.json (sim CPU/GPU, renderer, blend, spawn, shape, cone, speeds, lifetime, size, color, gravity, emitScale/timeScale, textures, meshes, full), and exports all referenced textures (PNG/TGA/DDS) + resolves mesh tpl paths.
- Verified: 150 random .sfx parse with no failure; Exports/sfx_fx_iron_halo_25hp (10 emitters, 10 textures, 1 mesh), Exports/sfx_fx_helbrute_head_glow (5 emitters, 6 textures).
- Next for item 3: UE script that builds a Niagara system from niagara.json (sprite/mesh emitters with spawn rate, lifetime, size/color curves), and export the referenced geom tpls.

### Item 5 (part) RESOLVED - root-motion direction
- Data is consistent: run F4 root ends at source (0,0,+4.859) = +Z, the character front (Assumption: source +Z front). In the tpl the root node Object00C9 (#201) and ROOT (#202) have identity MatrixLT.
- UE evidence (AnimPose, WORLD): Interchange-imported skeleton ref has Object00C9/ROOT yaw 180; the legacy-importer anim had yaw 0, so the whole animated body faced -Y (eyes y=-54) and the root moved to y=-485.9. Import mismatch, not a decoder bug.
- Fix: tools/ue/ue_import_anim.py (also SM2_Work/Scripts) imports anim-only FBX with legacy FBX + import_rotation yaw 180. Result /Game/SM2_Work/Original/cc_titus_assault/Anim_yaw180/*: root yaw 180 = ref, eyes y=+54.5 (face +Y like the mesh), run root 0 -> y=+485.9. All 3 clips reimported. Old /Anim kept.
- Still open in item 5: F3/F7 second slot, version >= 3, F6 clips without .sani_data.

### Open
3 VFX (UE Niagara builder from niagara.json), 5 .sani F3/F7 / v3 / F6-without-data, 6 tests (dotnet test hangs at restore; push now works via bundle).
