# >>> NEW AGENT: START HERE (bootstrap) <<<

You were given only this repo link. Follow these steps in order before you do anything else. When you finish, you should be able to continue exactly where the previous agent stopped.

## 0. Read this whole file
- Then read the newest dated "update" sections at the bottom; they are the latest state.
- `git log --oneline -15` on branch `cursor/sm2-pck-extractor-4420` is the source of truth for what is done.
- The PR is https://github.com/Baegovda/LibSaber/pull/1.

## 1. Required connectors / MCP: check each one and install it if it's missing
Check with GetMcpServerStatus / GetDynamicTools. Install missing ones with SearchPlugins and InstallPlugin, then authenticate with AuthenticateMcpServer.

| What | Name | Used for | If missing |
|---|---|---|---|
| REA (reverse engineering) | namespace `user-rea` (plugin "REA"; Ghidra, JADX and similar) | the REA-first rule, mandatory | SearchPlugins "REA", then install. Its MCP times out on the 81 MB exe, so run Ghidra headless or rizin on the box directly |
| GitHub | `cursor-github` (read-only) | PR and commit reads | RequestScmConnect / InstallPlugin |
| Owner's PC shell | ListMachines, then the machine named **Revaptor-FX** (id `d3fb99ad-25ca-45c1-81af-41e5ca09104a` at the time of writing) | PowerShell, Unreal control, push | the owner has to register the PC |
| Unreal Editor | **UEMCP** on the PC (`pip install uemcp`, https://github.com/ATDev-Inc/uemcp), driven by `tools/ue/uemcp_call.py` | import, materials, captures | see step 2 |

## 2. Tools: check they exist, install them if missing
- **Box (shared Linux machine):**
  - Ghidra at `/home/box/ghidra`, plus `/home/box/tools/ghidra-nativeaot`.
  - `rizin` at `~/.local/bin/rizin`.
  - dotnet at `/home/box/tools/dotnet`.
  - Game exe at `/workspace/rea/sm2_retail.exe`.
  - RE outputs: `/workspace/rea/out` is empty now (the old SANI_FORMAT.md / decode_sani.py are gone). Ghidra project `/workspace/rea/ghidra_proj2` (program sm2) and decompiles in `/workspace/rea/dec/<addr>.c`; decompile more with `JAVA_HOME=/home/box/tools/jdk25 /home/box/ghidra/support/analyzeHeadless /workspace/rea/ghidra_proj2 sm2 -process sm2_retail.exe -noanalysis -readOnly -scriptPath /workspace/rea/ghidra_scripts -postScript Decomp.java /workspace/rea/dec <addrs>`.
  - Repo at `/workspace/LibSaber`.
  - If any of these are missing, reinstall them, or copy the exe from the PC (D:\Steam\steamapps\common\Space Marine 2). Copy it to C:\Users\Revaptor_FX first, because the D: path can be refused.
- **PC:**
  - Python 3.10 with `pip install uemcp mcp`.
  - Copy `tools/ue/uemcp_call.py` to `C:\Users\Revaptor_FX\`.
  - Unreal 5.7.4 project at `K:\Unreal Projects\UE_VFX_57`. The editor must be open, and it listens on 127.0.0.1 only.
- **Smoke test:** `python uemcp_call.py` should list the tools, including `ue_python`.

## 3. How to push: the box has no GitHub login, the PC does (gh as Baegovda)
1. Box: `git bundle create /workspace/sm2.bundle <last-pushed>..HEAD`
2. CopyFromBox to `C:\Users\Revaptor_FX\sm2.bundle`. Check the size; one copy silently delivered a stale file.
3. PC: `cd C:\Users\Revaptor_FX\ls_push; git fetch ..\sm2.bundle HEAD; git merge --ff-only FETCH_HEAD; git push origin HEAD`
   - If `ls_push` is missing, clone the branch into it first.
- Commit identity: `-c user.name="Cursor Agent" -c user.email=cursoragent@cursor.com`.

## 4. Working contract with the owner (india님, Korean)
- Reply in Korean, short and plain.
- Report at each stage.
- Do items one at a time, in order, and confirm before starting the next big step.
- The owner granted full permission to build, install, push and manage folders. Never delete the owner's assets; move them instead.
- **REA first, always.** Base every format or shader conclusion on exe addresses or strings, and cite them.
- Work only inside the PC's `Desktop\SM2_Work` and Unreal's `/Game/SM2_Work`.
- **Next step at handoff:** step 3, second specular (gold metallicTint) plus anisotropic material layers. Then step 4, the `dotnet test` restore hang. Niagara (.sfx to Niagara) is deferred.
- At the end of every stage, update this file, commit and push.

---

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
  - The .sani spec now lives in code comments: src/LibSaber.SM2.Assets/Animation/EtnaCurves.cs and SaniReader.cs (the old `/workspace/rea/out/SANI_FORMAT.md` is gone). Python reference: py/sani.py.
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
5. **.sani gaps.** Done for SM2, see "sani gaps (2026-10-26)" and "sani gaps, part 2" below. v3 empty-name tracks bind by template animNmb, root rotation is applied, and unskinned tpl parts are world-baked and rigidly bound.
6. **Push and tests.** Push the local commits to PR #1 and get the tests running.

## Rules
- Keep this file updated at the end of each stage: state, open items, commit hashes.
- Never touch the owner's original assets. All SM2 work lives under /Game/SM2_Work in the layout below ("UE layout (unified)").
- Before editing any of the owner's scripts or files, back them up as `.bakN`.

## 2026-10-26 update (Cursor Agent)
### Layout (all work lives here now; supersedes "never touch /Game/SM2")
- PC: `C:\Users\Revaptor_FX\Desktop\SM2_Work\{Exports\<asset>, Anim\Titus_Anim, Captures\{TypeTest,TypeTest_overview,Item2_head}, Raw\{TypeTest_raw,titus_td}, Scripts\}` (uemcp_call.py stays in home).
- UE: `/Game/SM2_Work/{Original/cc_titus_assault (old /Game/SM2), v4/cc_titus_assault, cc_titus_assault, cc_swarm_hormagaunt, refrt_imp_rhino_01, refrt_tyr_arch_column_01, wpn_bolter_heavy_01}`; 595 assets moved via rename_assets, 0 redirectors. (Superseded by "UE layout (unified)" below.)
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
3 VFX (Niagara last; first browser playback as in-game), 5 .sani (done, see part 2), 6 tests (dotnet test hangs at restore; push now works via bundle).

## Bulk .sani validation (2026-10-26)
- Census of 23,227 .sani: v2 F6 sub2 20,244; v2 F2 sub2 1,223; v1 F6 787; v2 F6+F7 542; v2 F6 sub1 218; v1 F2 73; v3 F2+F3 61; v3 F6+F7 40; v2 F2+F3 21; v2 F2 sub1 18. F8 appears in 661 files.
- Decoder before: 21,488 OK / 1,739 FAIL. After: 23,126 OK / 101 FAIL (all v3).
- New: v1 accepted (FUN_14222fe40 defaults to sub 1), sub-1 raw-float codec (vtbl 0x1437e71c0; FUN_142228a70/228dd0/228e30/228ed0/227680), and F7 .sani_data = F2+F3 (F3 parsed, not applied).
- Remaining: v3 (101), F3 second slot not applied, v1 root motion (F4 without sub) not decoded, 1,405 clips with a rotation step over 90° (snap/hide?) not yet checked.
- UE numeric check, 5 clips (Titus, Hormagaunt, Sorcerer): no NaN, steps are plausible.
- ue_cap.py anim=@fraction: the capture scene keeps actors from earlier runs (overlapping meshes). Clean up the actors before trusting a visual comparison.

## GUI parity and animation export (2026-10-26, step 2)
- The browser and the extractor share one export path, AssetExporter.Export. That shared path covers skipping cloth sim meshes, BC5 B=0, stored channels, composite LOD hiding, and blendMode/albedoTint.
- The Export window adds:
  - an "Include *_SIM cloth" option
  - an Animations choice: None, Selected .sani rows, Preview clip, or Every matching clip
  - a Root motion checkbox
- New `Export/ExportPlan.Apply` turns the list selection into ExportSettings.Clips. Selected .sani files ride on the selected model as `{model}__{clip}.fbx` and are no longer written as standalone .anim.json.
- New `Animation/ClipResolver` is used by both the GUI and `--anim` (CLI).
- `ue_import_anim.py` is now an embedded resource and is written next to the anim FBX, the same way import_sm2_materials.py is.
- Verification: `tools/guiharness` runs ExportPlan.Apply + Export (the GUI code path) for cc_titus_assault.tpl with 2 clips. Its output is 281 files, all byte-identical (MD5) to two CLI `--anim` runs. In UE the clips import with the written ue_import_anim.py into /Game/SM2_Work/Anim_GUI, with no NaNs, and the title clip matches the CLI import (Anim_Bulk).
- ue_cap.py:
  - Actors are tagged SM2Cap, and a capture first clears tagged or nearby leftovers.
  - New "pose" mode (pose, then capture, then export, each a separate call) uses override_animation_data plus update-in-editor. The title clip now renders posed, where before it showed the bind pose.
  - Open: the go_to_jungle cinematic renders an empty frame at 0.5, so the framing or root-motion offset still needs checking.

## Second specular + anisotropic highlight (2026-10-26, open item 3 of the material list)
RE evidence (sm2_retail.exe, VA; rdata VA = file offset + 0x140001200):
- Strings: "second_spec" 0x1436daad8, "anisotropic_spec" 0x1436daac0, "metallicTint" 0x1436daab0, "anisotropy" 0x1436daa80, "extraAnisotropy" 0x1436daa90, "extraIntensity" 0x1436daaa0, "dirMap" 0x1437f92dc, "shift" 0x1437f92d4, "second_spec." 0x1437f9ea0, "anisotropic_spec." 0x1437f9ec0.
- glt field map FUN_1425c1d00: albedo_tint glt+0x30 (0x1425c2194), top-level metallicTint glt+0x40 (0x1425c21cb), second_spec sub-struct glt+0x94 (`add eax,0x94` 0x1425c282a), anisotropic_spec glt+0x108 (`add eax,0x108` 0x1425c2a5a).
- second_spec fields FUN_142576200: enabled +0, metallicTint +4, metalness.scale +0x14, .bias +0x18, roughness.scale +0x1c, .bias +0x20.
- anisotropic_spec fields FUN_142579ff0: enabled +0, angle +4, dirMap +8 (texture), anisotropy +0x18, intensity +0x1c, extra{anisotropy, intensity, shift} +0x20.
- Text reader FUN_1425b9500 (second_spec 0x1425ba12a, aniso 0x1425ba387): metallicTint read as a color and scaled (mulps 0x1425ba1fd). Legacy upgrade FUN_142621620 (~0x1426218b5) packs the tint as A<<24|R<<16|G<<8|B, so `.td` [A,R,G,B] = ARGB dword (same as albedo_tint).
- Permutation selection FUN_1423b7640: 0x1423b801f `aniso.enabled && anisotropy(glt+0x120) > 0.01` ([0x143c843ac] = 0.01) sets the aniso bit, and with dirMap bound (glt+0x110 != 0) also the dir-map bit (edi = 0x80000000); 0x1423b819a `second_spec.enabled (glt+0x94)` and no layer blend sets 0x200. Define names from FUN_1423b6260: GLT_USE_ANISO_SPEC 0x1437f88a0, GLT_USE_ANISO_SPEC_DIR_MAP 0x1437f88b8, GLT_USE_SECOND_SPEC 0x1437f89c0. Register/texture names FUN_1421b0bc0: PS_REG_GLT_ANISO_PARAMS 0x143729b28, GLT_TEX_ANISO_DIR 0x143729de0.
- Defaults (373/362 disabled full-dump blocks in the 18,713 retail .td): second_spec tint [0,255,255,255], metalness {1, 0}, roughness {0, 0.7}; aniso angle 90, anisotropy 0.5, intensity 1, extra {0.5, 1, 0.2}, dirMap "". enabled defaults false (the reader passes 0). Usage: second_spec enabled in 363 blocks / 244 files; aniso in 483 / 335; dirMap set in 77 (hair `_dm`, format "Diff(rgb)", usage "MD", neutral 127).
- Not in the exe: the HLSL itself. Lobe math below is an interpretation, not decoded.

Implemented:
- Meshes/SpecLayers.cs parses glt.second_spec / glt.anisotropic_spec of the variant (direct-child matching, so extra.* does not leak into top-level fields). MeshPart.SecondSpec/AnisoSpec (copied in Copy()). dirMap is a map with role `anisoDir`, exported linear.
- Sidecar (AssetExporter.WriteMaterialSidecar, shared by browser and CLI): `secondSpec {metallicTint RGBA, metalness{scale,bias}, roughness{scale,bias}}`, `anisotropicSpec {angle, anisotropy, intensity, dirMap, extra{anisotropy,intensity,shift}}`, null when disabled.
- import_sm2_materials.py: static switches UseSecondSpec, UseAnisoSpec, UseAnisoDirMap (all off = previous graph result). Second lobe: m2/r2 = saturate(packed * scale + bias), single lobe blended toward it by SecondSpecWeight (0.5) * m2, recolored by SecondSpecTint only when tint A > 0. Aniso: Anisotropy pin = AnisoAnisotropy, Tangent = (cos, sin, 0) of AnisoAngle or the AnisoDir RG*2-1, Specular = saturate(0.5 * AnisoIntensity). extra{} (shifted hair lobe) stays in the sidecar only.
- Checked: sidecar harness through WriteMaterialSidecar on real .td (hormagaunt torso, titus torso/hair/legs, calgar hair with dirMap) and the UE script against a recording mock of `unreal` (all pins/links non-null, per-instance switches and values). No game archives on the box, so no full export; `dotnet test` still hangs at restore (SpecLayersTests added, not run).
- To verify in UE: re-export a model (e.g. cc_swarm_hormagaunt, cc_titus_assault) with this build, run import_sm2_materials.py, check M_SM2 compiles (Project Settings > Rendering > Anisotropic Materials on), MI_* of hormagaunt torso has UseSecondSpec/UseAnisoSpec on, gold sheen on chitin, hair highlight stretched along strands; tune SecondSpecWeight.

## Step 3 PC verification (2026-10-26, second spec + aniso in UE)
Build/install: CLI + browser published from c493d33 (= origin 19c7eb0; win-x64 self-contained per pubxml) and installed to `C:\Users\Revaptor_FX\SM2Extractor` (old folder backed up as `SM2Extractor.bak1`, 1.19 GB).
Exports (new CLI, `Desktop\SM2_Work\Exports\step3_<asset>\tpl\<asset>.tpl\`, log `step3_<asset>.log` beside):
- cc_swarm_hormagaunt: 26 slots, secondSpec 8 / aniso 8 / dirMap 0 (head_01..04, hormagaunt_mesh torso, _2 legs, _6, blades_01). tint (0.988, 0.8, 0.522, A 1), metalness {0, 0.2}, roughness {0, 0.49}; aniso angle 90, anisotropy 0.4, intensity 3.
- cc_titus_assault: 41 slots, only head_01_2 (translucent) / head_01_8 = ch_titus_hair. tint (0.749, 0.827, 1, A 0), metalness {1, 0}, roughness {0, 0.67}; aniso 70 / 0.65 / 1. Armor has no second_spec/aniso in this tpl, so it keeps the old look.
- cc_calgar: 44 slots, secondSpec 7 / aniso 7 / dirMap 3 (calgar_head_01, _8, _10 -> `ch_calgar_head_hair_01_dm`, linear, present); cloak_REND_01/02 aniso 0.2 / intensity 1.5; head_01_2 angle 120 / intensity 20; head_01_7 angle 45 / intensity 10.
UE (5.7.4, Substrate on, `r.AnisotropicMaterials` = 1 already, no restart needed):
- `tools/ue/ue_step3.py <asset> import|verify` imports the FBX (skeletal, no materials) to `/Game/SM2_Work/step3/<asset>` and runs the exported import_sm2_materials.py; all instances assigned (26/26, 41/41, 44/44).
- verify: per-slot UseSecondSpec / UseAnisoSpec / UseAnisoDirMap match the sidecar for every slot, MISMATCH [] on all three. dirMap slots get AnisoDir = ch_calgar_head_hair_01_dm, the rest the default texture. Tint params are stored linear (gamma 2.2 of the sidecar sRGB, e.g. 0.8 -> 0.612).
- M_SM2 and M_SM2_Translucent compile: no Error lines in UE_VFX_57.log over the import window (792 shader jobs, 100% done); stats M_SM2 vs 179 / ps 161 / 5 samplers, M_SM2_Translucent vs 398 / ps 1214 / 9 samplers. Only warnings: FBX "no smoothing group" (known).
Captures (`Desktop\SM2_Work\Captures\Step3\`):
- Plain `ue_cap.py ... final` renders near-black at the capture spot (no lights there), so `tools/ue/ue_cap_lit.py` was added (copy of ue_cap.py plus three SM2Cap-tagged radius-limited point lights removed by `export`, manual exposure, `intensity=` / `ev=` / `off=<folder>` args). Unlit runs stay in `Step3\<name>`, lit ones in `Step3\lit\<name>` (intensity=80).
- True A/B: `tools/ue/ue_step3_ab.py <asset>` duplicates each instance with a layer on into `/Game/SM2_Work/step3/_ab_off/<asset>/<MI>_off` with all three switches off; `ue_cap_lit.py ... off=<that folder>` swaps them in on the capture actor only.
- Mean |diff| switches off vs on (0..255, front/side/back): hormagaunt 0.89 / 0.28 / 0.91 (3.6% / 1.2% / 3.9% pixels > 10), visible as extra sheen on chitin and legs. Titus full body 0 (hair hidden under the helmet). Titus head 0.20 / 0 / 0.19. Calgar body 0.70 / 1.40 / 0.44 (cloak). Calgar head 2.25 / 0 / 1.57: hair highlight clearly brighter and streaked.
- Old-import "before" (`/Game/SM2_Work/cc_*`) vs step3 also differs on Titus armor (2-3 mean), which comes from older importer changes, not the new layers; use the `_AB_switches` sets to judge this step.
- Head mode: the "side" camera sits inside the pauldron for Titus/Calgar and sees nothing; front/back are usable.
Open: tune SecondSpecWeight (0.5) / aniso Specular against game references; the extra{} hair lobe is still sidecar-only.

## sani gaps (2026-10-26)
All five gaps from item 5, done REA-first (Ghidra project /workspace/rea/ghidra_proj2, decompiles in /workspace/rea/dec).
- PC backup before install: C:\Users\Revaptor_FX\SM2Extractor.bak2 (old build). Raw file restored: Raw\sani_all\...\pve_relay_boss_intro_shot_01_char_vortex_beast1_vortex_beast.sani was overwritten by an old sanival call (TSV header, 92 bytes, kept as .sani.bak1) and restored from Raw\sani_all.zip (60,885 bytes). sanival now refuses an unknown flag / non-folder / non-.tsv output.
- Loader FUN_14222fe40 accepts version 1..3; F2/F6 = slot 0 (clip+0x60), F3/F7 = slot 1 (clip+0x68), F4 = root (FUN_142234f10); codec factory FUN_142234c00 reads the sub u16 only when version > 1.
1. **v3: FIXED.** vtbl 0x1437e74e0. Header FUN_14222d0d0 (8 counts nT nR nS nF cT cR cS cF), carve FUN_14222d550, planar T,R,S,F frames FUN_14222d6b0, frame size FUN_14222d7c0 = nT*4+nR*6+nS*6+nF*2 (sampler c810 is the reference). Name table FUN_1422336c0: bones (+24) then scalars (+28), interned by FUN_141c3fa90 (string-pool id; empty string -> id 0). Scalars go to AnimationClip.Scalars. v3/F7 .sani_data = F2 block + F3 block. Raw v3 sub-1 codec (vtbl 0x1437e72d0) not decoded (no retail file uses it).
   - Empty names: FIXED in part 2 (bind by template animNmb rank).
2. **F3/F7 second slot: FIXED (as additive flag).** Stats dumper FUN_141060280 prints HasAdditive (string 0x1435ef808) = clip+0x68 != 0. Numerically slot1 R(f) = R0(f)*inv(R0(0)) (max err 0.00x deg), T(f) = T0(f)-T0(0), scales 1 (cultist_stand_shoot, cadian_talk, door_mushroom close). Only slot 0 is exported; AnimationClip.HasAdditive is set.
3. **F6 without .sani_data: NO-OP.** Census: 0 such files in retail; preview fallback kept.
4. **v1 root motion: DECODED, partial in export.** F4 sub 1 vtbl 0x1437e77b8 ([1] FUN_14222ef50 exact, [2] FUN_14222ef80 lerp T + FUN_1427ae2c0 blend R, [3] FUN_14222f060 carve, [7] FUN_14222f0d0 load); size FUN_14222f140: float4 T[frames] then float4 R[frames] (w,x,y,z). Sub 2 root = vtbl 0x1437e7800. All 860 v1 files carry F4; 2 are non-identity (vortex_beast boss_intro / outro): T is all zero, R is non-identity (e.g. boss_intro frame 250 = (w .785, x -.366, y .354, z -.354)). ClipPose/FbxWriter/GltfWriter apply only root translation, so the FBX is byte-identical to the bak2 export (MD5 61234DEB...). Root rotation: APPLIED in part 2.
5. **Rotation steps > 90 deg: NOT A BUG.** FUN_1427ae2c0 is slerp-approx with hemisphere flip (dot<0 -> negate), same shortest path as UE; codes are unsigned code*scale+min (no wrap). Box sample: 1548 snaps vs 27 one-frame spikes, mostly attach/hand/prop/IK bones. PC: 1967 files have a step > 90 deg. No code change.
- Validation: PC Raw\sani_validation3.tsv 23,227 OK / 0 FAIL (v1 860, v2 22,266, v3 101); additive 664, scale tracks 101. Box /workspace/sani/val3.tsv 449/449 OK; v3 hologram_planet matches py/sani.py.
- Tests: test/LibSaber.SM2.Assets.Tests/SaniGapTests.cs (4 synthetic tests; pass in a shim runner on the box, real `dotnet test` still hangs at restore there).
- Tools: tools/sanival (`sanival <folder> <out.tsv>`, `--dump <file> <bone|index>`, `--root <file> [step]`), tools/ue/ue_sani_fix.py (import mesh + before/after anim; `check` prints AnimPose WORLD bone poses at 0/25/50/75/100%).
- UE (/Game/SM2_Work/sani_fix): door_mushroom (mesh + after/door_mushroom__close, 3.267 s; before = no clip at all) - flower_base_bind_01 moves (-500,-310,-265) -> (-387,-274,207), flower_base_bind_04 pitch -74 -> -18, no NaN. cc_vortex_beast before/after boss_intro (19 s) identical poses (see gap 4). Captures Captures\sani_fix\door_mushroom_{before_bind,after_f000,f050,f100} (box /workspace/sani/captures): camera ends up inside the door's hull, so they are not useful visually; the AnimPose numbers are the evidence.

## sani gaps, part 2 (2026-10-26)
CLI: box /workspace/pub3 -> PC C:\Users\Revaptor_FX\SM2Extractor3\LibSaber.SM2Extractor.exe. SM2Extractor2 = a04e0bc, and the older SM2Extractor is kept. Always pass the full `--anim tpl/<n>.tpl/animations/<clip>.sani`, because a short name matches by substring and exports many clips.
1. **v3 empty-name tracks: FIXED (a04e0bc).**
   - Exe: the `CONFIG.useSaniAnimationsInTpl` path FUN_141db55f0 -> FUN_142232230 takes the tpl objects with animNmb != -1, sorts them by animNmb (FUN_14127d230) and writes clip+0x50[i] = object id. No name is compared.
   - Code: Export/ClipBinding.cs and Bone.AnimIndex. When the counts do not match, it falls back to names. An explicit bone index is kept.
   - Logs: door_mushroom close 130/130 (was 128), hologram_planet anim 47/47 (was 0).
2. **Unskinned tpl parts: FIXED (this series).**
   - TemplateMeshReader baked unskinned parts with the node's LOCAL MatrixModel. ReadSkeleton proves MatrixModel is local (inverse binds pair only as world = local * parentWorld).
   - hprobe: hologram_planet has 63 of 65 geometry nodes with world != local, up to 8.72 m. Rhino has 0 of 10, so the change is a no-op there.
   - The parts are now baked with the composed world matrix, and each one gets a rigid skin weight (J0 = node, W0 = 1), so node tracks move it. Exporter note: "N unskinned parts are bound rigidly...".
   - UE: the old mesh shows one stray outer sphere (bind bounds sphere 52 m). The new one shows the planet with its rings (17 m), and the rings move between t = 0, 0.25 and 0.5 (9.5 to 11 % of pixels change).
3. **Root rotation (F4 R): APPLIED (a04e0bc).**
   - Exe: FUN_14222fe40 case 3 samples the root (FUN_142234f10, vtbl+8) at frame 0 and frame N-1. It uses FUN_1427aa8a0 (q inverse), FUN_1427ace20 (R^-1 v) and FUN_1427ad8b0 (q mul): root+8 = R0^-1 (T_last - T0), and root+0x14 = R0^-1 R_last. The result goes to clip+0x70. So p' = R p + T, which is `local * R * T` (row vectors) for parentless bones.
   - Code: ClipPose, FbxWriter (R key times) and GltfWriter (bakes T and R on parentless bones). The identity-R path is byte-identical.
   - Census (PC Raw\sani_root_census.tsv): 23,227 clips, 418 with R. 416 of them are turn clips whose angle matches the name. The other 2 are cinematics, pve_relay boss_intro 89.6 deg and outro 50 deg, both with T = 0.
   - Root motion is per controller: `MorphemeController.useAnimRootMotion` (+0x41, string 0x1435da538, registered at 0x140e4e9be in FUN_140e4b2f0). Cinematics presumably play without it: boss_intro ends tilted at z 805 with R, but in the idle stance without it. The exporter therefore warns for clips with R and suggests `--no-root-motion` for those 2 shots. There is no path heuristic, because the exe gives no proof of one.
   - vortex_beast attack_melee_180 (v2): CENTRE yaw f0/fN was 89/89 and is now 89/-91 (a true 180). The largest one-frame step drops from 79.1 cm to 24.9 cm.
   - No regression: the titus run clip FBX and every mesh FBX are byte-identical.
- UE additions:
  - Props/hologram_planet (rigid-bound mesh)
  - Animations/hologram_planet/hologram_planet__anim
  - Tests/sani_before/hologram_planet/localbake/* (old mesh + anim)
  - Animations/door_mushroom/door_mushroom__close (130/130; the old 128/130 clip is in Tests/sani_before/door_mushroom)
  - Animations/cc_vortex_beast/{boss_intro, attack_melee_180} (before copies in Tests/sani_before/cc_vortex_beast)
- Captures: tools/ue/ue_cap2.py. Each capture takes three calls (pose, capture, export). The centre is the posed bones, and the radius is max(bone spread, mesh bounds sphere). The camera sits at R/sin(fov/2), so it is outside the mesh. cdt/_glr/rb_/_lod/nav_silhouette slots are hidden. Output: PC Desktop\SM2_Work\Captures2 and box /workspace/sani/captures2 (12 sets x front/side/three_quarter).
- Known limits:
  - Affix-hidden volumes cannot be told apart by UE slot name (pass hide=).
  - There is no unit test for the world bake yet (needs a tpl fixture).
  - A 90 deg turn clip was not imported. The census and the 180 clip cover the rotation sign.

## UE layout (unified, 2026-10-26)
Everything our work created lives in `/Game/SM2_Work`. An inventory of /Game (255,440 assets) found no SM2 work outside it: no /Game/SM2, /Game/SM2_Test, /Game/SM2_vN or scratch/_calib folders remain. The `M_SM2` / `M_SM2_Translucent` / `M_SM2_ClothSimHidden` masters are written per asset by import_sm2_materials.py, so they stay next to that asset's instances. Different generations differ, so they are not merged.
```
/Game/SM2_Work/
  Materials/              M_Hide (capture helper, was _Tools)
  Original/cc_titus_assault/   owner's original import, untouched; /Game/Work/02/Lightning_01 references its mesh and Anim/run_fwd
  Characters/<asset>/     current import (mesh, Skeleton, masters, MIs, Textures/): cc_titus_assault, cc_swarm_hormagaunt, cc_calgar (was step3/*), cc_sorcerer_exalted, cc_vortex_beast
  Props/<asset>/          refrt_imp_rhino_01 (was refrt_imp_rhino_01_v2, fixed LOD), refrt_tyr_arch_column_01, wpn_bolter_heavy_01, door_mushroom, hologram_planet
  Animations/<asset>/     clips: cc_titus_assault/{Bulk,GUI,yaw180} (skeleton = Original/cc_titus_assault), cc_sorcerer_exalted/Bulk,
                          cc_swarm_hormagaunt/Bulk (skeleton = Tests/pre_step3/cc_swarm_hormagaunt), cc_vortex_beast, door_mushroom, hologram_planet (sani "after")
  Tests/<stage>/<asset>/  step3_ab_off (was step3/_ab_off), sani_before, pre_step3 (old root cc_titus_assault, cc_swarm_hormagaunt),
                          v4 (cc_titus_assault), rhino_stacked (old refrt_imp_rhino_01), typetest (future ue_typetest.py output)
```
- How it was moved: `tools/ue/ue_reorg_layout.py plan|apply`. It moved 827 assets with AssetTools.rename_assets, left 0 redirectors and saved everything. Afterwards the 41 old folders that were empty on disk were removed, and nothing was deleted. A disk backup from before the move is at `Desktop\SM2_Work\Backup\Content_SM2_Work.bak1` (2.7 GB).
- How it was verified: `tools/ue/ue_verify_layout.py`. It reports 963 assets, 0 redirectors and 0 missing /Game dependencies. Every mesh's MIs and parents resolve inside its own folder (Titus 41 slots, Hormagaunt 26, Calgar 44, Rhino 16, Column 11, Bolter 26), and every anim resolves its skeleton. All the masters have shader stats, i.e. they compile: M_SM2 161 ps instructions, Translucent 1214, ClothSimHidden 141.
- Scripts now targeting this layout (repo `tools/ue` and `Desktop\SM2_Work\Scripts`):
  - ue_step3.py → Characters|Props/<asset>.
  - ue_step3_ab.py → Tests/step3_ab_off/<asset>.
  - ue_sani_fix.py: mesh → Characters|Props/<asset>, "after" → Animations/<asset>, "before" → Tests/sani_before/<asset>. Optional export tags are args 4 and 5.
  - ue_typetest.py → Tests/typetest/<dir>.
  - ue_cap*.py → Materials/M_Hide.
  - ue_import_anim.py / ue_bulk_anim.py take the destination as an argument: pass Animations/<asset>/<set>.
  - The product default in import_sm2_materials.py stays /Game/SM2/<mesh> for end users, and our tools pass the folder explicitly.

## Material finishing (2026-10-26, SM2 only, Substrate)
Evidence (sm2_retail.exe + decoded PS bytecode; shaders in box `/workspace/mat/glsl/`):
- 1a Defines: GLT_USE_ANISO_SPEC / _DIR_MAP / GLT_USE_SECOND_SPEC = permutation bits 30 / 31 / 41 (FUN_1423b6260); gates in FUN_1423b7640 (aniso > 0.01, dirMap bound, second_spec enabled and no layer blend). SpecLayers.cs matches; the exporter does not check "no layer blend" (no effect on characters).
- 1b Second spec (PS 03840_0 non-aniso, 00004_1 aniso; cb6 OBJ_MTLBLEND_LAYER_CONST layer0 rows 6..21): m2 = saturate(row18.w*packedMetal + row7.w); tint = row18.xyz (alpha unused); F0_2 = flag32 (row21.x) ? tint*specRGB*m2 : lerp(0.04, tint*albedoLinear, m2); r2 = max(saturate(row19.x*packedRough + row19.y), 0.02); F = (1-Fc)*F0_2 + saturate(50*F0_2.g)*Fc. Added to base spec at weight 1, direct lights only. Non-aniso second lobe has no visibility term (D*F*NdotL, ~4x a standard lobe); the aniso one keeps 1/(4pi) (standard level).
- Aniso: packer FUN_1423ba150 (CB_STATIC_GLT): row5 = (cos, sin, -sin, cos) of clamp(angle,0,180)*deg2rad; row6 = (intensity, extra.intensity, k, k_extra) with k = 1 - clamp(a, 0, 0.95) (0x1423ba35c..3ec); row4.y = extra.shift (0x1423ba3f4). Shader: alphaT = (r*k)^2, alphaB = r^2, D = min(.., 128) * intensity. In aniso+second shaders the second lobe IS extra{} (tangent normalize(T + shift*N), k_extra, extra.intensity); without second spec extra{} is unused.
- 3 SecondSpecWeight does not exist in the game; removed. Gain fixed from the visibility difference: SecondSpecGain 4 (non-aniso) / 1 (aniso). Aniso intensity = raw glt value.
- Base F0 in game: lerp(0.16*reflectance^2, albedo, metal), reflectance = row8.w (source not traced; UE keeps 0.04).
- Unresolved: GBuffer PS 00165_2 reads rows 18/19 as a fresnel tint (layout may be per permutation); whether the deferred path carries the second lobe.
import_sm2_materials.py (graph, M_SM2 and M_SM2_Translucent):
- Base = SubstrateSlabBSDF: DiffuseAlbedo = albedo*(1-metal), F0 = lerp(0.04, albedo, metal) (x AnisoIntensity, saturated, when aniso), F90 1. Aniso remap A = (k^2-1)/(k^2+1), roughness' = r*sqrt((k^2+1)/2) (matches both game axes exactly with Substrate alphaT = a(1+A), alphaB = a(1-A)). Tangent = (cos, sin) of AnisoAngle or AnisoDir (UseAnisoDirMap).
- Second = Slab with albedo 0, F0 = saturate(F0_2 * SecondSpecGain * AnisoExtraIntensity), F90 = saturate(50*F0_2.g)*gain, r2 remapped by AnisoExtraAnisotropy, frame rotated by AnisoExtraShift (T2 = normalize(T + s*N), N2 = normalize(N - s*T)). SubstrateAdd(base, second) behind static switch UseSecondSpec -> Front Material. Translucent: SubstrateWeight(front, opacity). AO / WPO stay on root pins; legacy BaseColor/Metallic/... pins no longer linked.
- Params: AnisoAngle/Intensity/Anisotropy, AnisoExtraAnisotropy (0.5) / ExtraIntensity (1) / ExtraShift (0.2), SecondSpecGain, SecondSpecTint (linear), SecondMetalness/RoughnessScale/Bias. apply_spec_layers no longer clamps anisotropy.
UE verify (`tools/ue/ue_mat_apply.py <asset> apply|verify`, PC `Desktop\SM2_Work\Scripts`, into `/Game/SM2_Work/Characters/<asset>`):
- hormagaunt 26/26 (secondSpec 8), calgar 44/44 (7), titus 41/41 (2); switches/params/tint match the sidecar, MISMATCH [] on all. No Error lines in the log. Stats: M_SM2 vs 179 / ps 273 / 5 samplers (was 161); Translucent vs 398 / ps 1150 / 9.
- Backup before apply: `Desktop\SM2_Work\Backup\Characters_mat.bak1\` (robocopy of the three Characters folders).
- Captures (ue_cap_lit.py intensity=80): PC `Desktop\SM2_Work\Captures\Mat\{before,after}\<asset>_{body|head}\`; box `/workspace/mat/captures/compare_*.jpg` (top before, bottom after). Mean |diff| 0..255 front/side/back: hormagaunt body 8.2/2.1/7.5, calgar body 4.8/8.7/4.1, calgar head 4.3/6.5/3.6, titus body 2.9/1.8/2.2, titus head 1.8/3.8/2.3. The first after set was captured mid shader compile (grey checker); retaken after compile.
Caveats / next:
- Calgar hair (extra.intensity 20, saturated F0 -> 1) looks blotchy and too bright: UE also lights the second slab with IBL (game: direct only), and intensity can only go through F0 <= 1. Candidate fix: scale the second slab by a Substrate coverage or drop its IBL via a specular-only light-function trick; needs an in-game reference.
- Shift is a frame rotation approximation; DIR_MAP decode (RG*2-1) not verified against the shader.

## Calgar hair fix (2026-10-26, mat2)

Reference: in-game close-up (short grey buzz cut, matte, almost no aniso streak).
Compare image: box `/workspace/mat/captures/calgar_hair_ref_compare.jpg` (reference | before c1c4de0 | after).

Exe evidence (PS 00004_1 / 00042_0 GLSL):
- Base spec layer (`glt` level) metal/rough scale+bias, metallicTint, reflectance (F0 = lerp(0.16*refl^2, albedo, metal)):
  now parsed (`SpecLayers.Base()` -> `BaseLayer`), written to the sidecar as `baseLayer` and applied by the importer.
- Direct-light AO: every local light (diffuse + both spec lobes) is scaled by `saturate(2*AO - 1 + |NdotL|)` (00004_1 ~l.4167),
  AO = spec.B mixed by per-instance weights (`_66[_339*108+36..39]`; CPU writer not located).
- Dir-map tangent decode (00042_0 l.487-489, 2697-2716) matches the importer (no angle rotation when dirMap is present).

APPROXIMATION (clearly labelled in `import_sm2_materials.py`, `DirectAo` block):
- UE/Substrate has no per-light hook, so `occ` = NdotL-weighted mean of the game's factor over the hemisphere
  (closed form, AO>=0.5: 1-u^3/3, u=2-2AO; AO<0.5: 2/3-d+d^3/3, d=1-2AO) multiplies base diffuse, base F0/F90 and second-slab F0/F90.
- `DirectAoStrength` = 1 only on slots with an aniso dirMap (hair); 0 elsewhere, so other materials are unchanged.

Result: fewer white blotches, darker/more matte grey. Remaining differences:
- the UE aniso path is still brighter and more streaky than the game (A/B: `tools/ue/ue_hair_ab.py` modes nosecond/noaniso/nospec; nospec is the most matte);
- the capture rig has almost no IBL while the in-game shot is ambient-dominated;
- the hair cards read more voluminous than the in-game buzz cut;
- UE uses Smith V on both lobes (the game has no V on the second lobe); no per-slab IBL exclusion in Substrate;
- the rows 8/9 CPU packer is not located; the MetallicTint lerp is done in linear space;
- other characters (hormagaunt, titus) receive the baseLayer bias too: re-check them.

PC: content backup `Desktop\SM2_Work\Backup\Characters_mat2.bak1\cc_calgar`; scripts `.bak1/.bak2`; sidecar `.json.bak1`.
Apply: `ue_mat_apply.py cc_calgar apply`, wait ~170 s, `ue_tmp_stream.py` (FullyLoadUsedTextures), then `ue_cap_lit.py capture ... view=ref`.
Verify: `ue_mat_apply.py cc_calgar verify` -> `SLOTS 44 MIs 44 secondSpec 7 MISMATCH []`.

## Animation → UE: survey + fix 1/2 (2026-10-26, anim_fix12)

Survey report: box `/workspace/anim_survey/REPORT.md` (12 clips, 0 NaN, frame-drop fixed in d52abd2).
Before/after sheets and UE check output: box `/workspace/anim_survey/fix12/`. UE: `/Game/SM2_Work/Tests/anim_survey/fix12/<asset>/`.

1. **Import yaw: auto (no hardcoded 180).** `sm2_anim_import()` (same block in `tools/ue/ue_import_anim.py` (embedded in the exe), `ue_anim_survey.py`, `ue_sani_fix.py`)
   imports at yaw 0, compares skeleton bone 0 at t=0 with the skeleton ref pose (component space) and reimports with the
   90°-multiple difference if any. Fresh Interchange skeletons (old `_Skeleton` Null and new ROOT-rooted) → 0. The old
   `Original/cc_titus_assault` skeleton (needed 180) was not re-tested (read-only), the auto rule should pick 180 there.
2. **Root motion on bone 0 = ROOT.** Exe: the loader FUN_14222fe40 keeps the 0xF4 root track apart from the bone slots
   (slot 0 clip+0x60, slot 1 clip+0x68, root clip+0x70; case 3 builds root+8 = R0^-1(T_last-T0), root+0x14 = R0^-1 R_last via
   FUN_1427aa8a0/FUN_1427ad8b0; sampler FUN_14222f1b0, codec FUN_142234f10). It is an entity transform, not a skeleton
   bone, and the tpl root object (Object00C9 etc.) and ROOT have identity MatrixLT. Export choice: ROOT carries it.
   - `UnrealRoot.Promote` (fbx-unreal default; `--fbx-armature-null` = old hierarchy): ROOT becomes the only root, the tpl root object
     becomes its child (same world bind, bone order and skin indices unchanged), and no `{mesh}_Skeleton` Null is written.
     ClipPose applies 0xF4 to parentless bones, so the motion lands on ROOT = UE bone 0.
   - The import helper sets EnableRootMotion + RootMotionRootLock ANIM_FIRST_FRAME (`noroot` to skip).
   - glTF is unchanged: it has no Null, so its root bone (tpl root object) already carries the motion.
   - Existing skeletons from older exports have a different hierarchy, so re-export and re-import the mesh (new skeleton).
   - Verified (UE 5.7.4, `extract_root_track_transform`): Titus run ROOT 485.9 cm / 0.933 s, in place when extracted, facing +Y;
     Hormagaunt run 506.7 cm / 0.633 s (= 800 cm/s; the old 480 / 0.600 lost a frame); Vortex slam 488.2 cm + 180° turn.
     Before: bone 0 `_Skeleton` 0 cm, root motion off.
- Build: `C:\Users\Revaptor_FX\SM2Extractor5\LibSaber.SM2Extractor.exe`. Tests: SM2.Assets 52/52, SM2Extractor 35/35.
- Open: cinematic rebase (level-space c06/c10), `--anim-only` / multi-clip export, additive clip type, Mannequin RTG (see REPORT.md).

## SM2Browser v2 preview (2026-10-26, browser_v2)
Installed: `C:\Users\Revaptor_FX\SM2Browser_v2\LibSaber.SM2Browser.exe` (single-file win-x64; older builds kept as `.bakN`).
Bench: `LibSaber.SM2Browser.exe --bench <game> tpl/cc_titus.tpl <outdir> [frames]` renders old (Bench/MeshPreviewLegacy.cs) vs new
renderer from the same cameras and writes bench.txt + PNGs; `SM2_BENCH_TESS=1` adds the tessellation string scan.

Root causes in the old MeshPreview (all CPU, Titus LOD0 = 422,499 verts / 299,607 tris / 38 parts / 525 bones):
- Stutter: single-threaded raster of all 300k tris every frame via IReadOnlyList, new WriteableBitmap each frame, 33 ms timer
  advanced by a fixed step. Old: 221 ms/frame at 1100x1000 (4.5 fps), 263 ms at 1600x1200 (32-core PC).
- Grain/sand: nearest-neighbour texel fetch, no mips, affine UVs, no AA, rendered at DIPs capped 1100x1400 then stretched.
- Faceting: Fill/FillTextured shaded one colour per triangle; FillLit used normals[a] for the whole triangle; skinned normals
  were never transformed.
- Not tessellation: sm2_retail.exe parses `tessellation_parameters` in fcn.1421df540 (string 0x1436ea3f8, xref 0x1421df5c9;
  smooth = `pn_triangles` 0x1436ea3e0 xref 0x1421df6b3 | `phong` 0x1436ea3f0; displace `heightmap` 0x14364a858; `maxLevel`
  0x1436ea428; also `TESSELLATION: ` 0x1437eb300, `isAdaptiveSubdivision` 0x1437f5588, `USE_ADAPTIVE_SUBDIVISION` 0x1437f5318,
  `displacementMapping` 0x143634590). Scan of all 37,403 .td in the catalog: 0 contain tessellation/pn_triangles/phong/
  isAdaptiveSubdivision/displacementMapping (700 contain "heightmap" = hdetm detail maps). Titus tpl files: none (only
  "heightmap" in cc_titus.tpl). So Titus pauldrons are plain meshes (pre_order_shoulder_01 24,222 + 12,207 tris).
- FBX/glTF already write per-vertex normals (GltfWriter NORMAL accessor; FbxWriter LayerElementNormal ByPolygonVertex, from
  VertexSerializer.DecompressNormalFromInt16) — export was never faceted.

New renderer (src/LibSaber.SM2.Assets/Meshes/MeshPreview.cs): parallel skin (positions+normals), projection, chunked binning
into bands with sub-pixel cull, parallel band raster; perspective-correct barycentrics; interpolated vertex normals + per-pixel
TBN; per-part pre-resolved box-filtered MipChain with bilinear sampling and per-triangle LOD; Toksvig spec AA; 2x2 SSAA when
idle (AA toggle); renders at physical pixels. Prepared slice arrays cached per slice.
Numbers (PC, median): 1100x1000 new 1x 21 ms (47 fps; clear 2.6 / project 1.0 / bin 0.9 / raster 14.9), 1600x1200 26.5 ms
(38 fps); 2x2 SSAA still frame 54 / 69 ms. Playback of a Titus cinematic clip with In place: ~26 ms (39 fps), skin 1.7 ms.

UI: toolbar and anim bar are WrapPanels (no clipping at the default 1440x800); "AA" toggle + render stats; Anim "Filter clips"
TextBox (live, case-insensitive substring over the path, Bind pose always first, clips containing the model key - e.g. "titus"
- marked with a star and sorted first); "In place" (default on) rebases cinematic clips that start far from the origin (reference
bone = topmost bone displaced >= 50% of the max at t=0; cached per clip); Bind pose / model change resets clip, time label and
slider; playback is real-time; mouse wheel no longer changes AnimBox/LodBox/PartBox selection when closed; Meshes list is a
TreeView with _LODn variants nested (collapsed) under their base row (lowest LOD promoted if no base).
Next: a GPU path would be the real fix for >60 fps with SSAA.

## SM2Browser v3: GPU preview (2026-10-26, browser_v3)
- **Path:** `Gpu/GpuPreviewControl.cs` (Avalonia `OpenGlControlBase`) + `Gpu/GlPreview.cs` (Silk.NET.OpenGL 2.22).
  - Windows uses ANGLE (OpenGL ES 3 on Direct3D 11), Avalonia's default and the most robust driver path. Program.cs order: AngleEgl, Wgl, Software.
  - Shaders carry a runtime header: `#version 300 es` on ANGLE, `330 core` on desktop GL. The box runs desktop GL through Mesa llvmpipe; X11 options clear Avalonia's llvmpipe blacklist.
- **Scene:** `MeshPreview.BuildScene` (`MeshPreviewScene.cs`) shares slice, visibility, material, camera, light, In-place and skeleton logic with the CPU `Render`. The GPU draws exactly what the CPU would.
- **Buffers and skinning:**
  - Static interleaved VBO/IBO per batch (LOD/slice), uploaded once and cached by reference.
  - Skinning in the vertex shader: 4 weights; the bone palette is an RGBA32F texture (4×N, `texelFetch`), so there is no bone-count limit.
- **Materials:**
  - Per-pixel cotangent-frame normal mapping; spec map metal=r, rough=a|g, ao=b.
  - Toksvig gloss. Same light rig in linear light; sRGB encoded in the shader.
  - Albedo textures are `SRGB8_ALPHA8`, others RGBA8. Full mip chains, trilinear filtering, up to 16x aniso.
  - Uploads are budgeted to 120 ms per frame and continue on the next frame.
- **Frame:**
  - MSAA 8x (4x if the driver caps lower), resolved to an RGBA8 FBO and then blitted to Avalonia's framebuffer.
  - Wireframe draws a GL_LINES index buffer built lazily. Skeleton overlay has no depth test.
  - Renders on demand. During Play it renders on every compositor frame (vsync), sampling the clip at that frame's time.
- **UI:**
  - Toolbar `GPU` toggle (on by default). AA is disabled in GPU mode because MSAA is always on.
  - Stats read `GPU WxH MSAA nx · cpu ms · fps`; the tooltip gives the GL renderer/version.
  - Automatic CPU fallback when GL init fails, a frame throws, or no frame arrives within 2.5 s of showing the view. Avalonia never calls GL callbacks under software compositing; PreviewNote says why.
- **Bench:**
  - Run `LibSaber.SM2Browser.exe --glbench <game folder|synthetic> <model> <out> [frames]`. It writes glbench.txt and PNGs.
  - It measures uncapped (vsync-off) ms/frame at 1100×1000, 1600×1200, 1920×1080 and 2560×1440, for a static orbit and for clip playback, plus presented (vsync-on) fps.
  - On the PC: `Desktop\SM2_Work\run_glbench_v3.ps1`. Run it only when no game is running.
- **Box check (synthetic sphere, llvmpipe):**
  - The image matches the CPU renderer in orientation, colour and lighting, and skinning moves the mesh. Results are in `/workspace/browser_run_shots/v3/box_synthetic/`.
  - Box GPU numbers are software GL only: 14 ms at 1100×1000.
- **Install:** `C:\Users\Revaptor_FX\SM2Browser_v3\LibSaber.SM2Browser.exe` (MD5 2123211DEBDC00C2E902F78553B9D4A4). The first v3 build is kept as `.bak1`.
- **PC numbers (RTX 4090, ANGLE D3D11, MSAA 8x, aniso 16x; Titus LOD0, 300k tris, 525 bones):**
  - First frame including the texture upload: 374 ms.
  - Vsync off, static: 0.17 ms (1100×1000) to 0.26 ms (2560×1440).
  - Vsync off, playback: 0.3–1.2 ms. Playback cost is mostly CPU clip sampling plus the palette.
  - Vsync on at 1600×1001: 120 fps static and 120 fps playing (monitor refresh).
  - For comparison, CPU v2 ran 21–26 ms (38–47 fps).
  - Captures: `Desktop\SM2_Work\Captures\v3_gpubench` and `/workspace/browser_run_shots/v3/pc/`.
- **Open:**
  - The MainWindow GPU toggle and fallback have not been driven by hand on the PC. The bench exercises the same control and renderer from the same exe.
  - Wireframe on LOD0 (300k tris) is a solid silhouette, as on the CPU.

## Animation finishing (2026-10-26, anim_finish)

Base 62d96e7. PC build: `C:\Users\Revaptor_FX\SM2Extractor6\LibSaber.SM2Extractor.exe` (previous builds `.exe.bak1`, `.exe.bak2`).

New export flags
- `--anim` is repeatable; one value may also hold `;`/`,`-separated tokens, `@list.txt` (one per line, `#` comments), and globs
  (`*`, `?` over the whole .sani path, case-insensitive), e.g. `--anim "*jetpack_part_03_shot_03_char_titus*"`.
- `--anim-only`: writes only `{model}__{clip}.fbx` against the model's skeleton (no bind mesh, materials, textures). Requires `--anim`.
- `--anim-rebase first-frame|none` (default `first-frame`).

Rebase rule (ClipPose.Rebase)
- Stage 1, ROOT: if ROOT is off its bind (translation or heading) at frame 0, C = T(-p0)·RotY(-yaw)·T(pb) on every parentless local.
- Stage 2, CENTRE: if, after stage 1, CENTRE (pelvis) is still ≥ 2 m from its bind, the same C is applied below ROOT
  (to ROOT's children, in world space), so ROOT stays at its own track. v1 shots (c06 story_tower jetpack, c10 pve_relay boss_intro)
  keep ROOT at bind and put the level position on CENTRE. A 0.5 m crouch is a pose, not rebased. Only heading is removed; pitch/roll stay.
- Why default on: the game's loader `FUN_14222fe40` treats root motion relative to frame 0 (R0⁻¹(T_last−T0)); gameplay clips start
  at bind so it is a no-op for them. The removed placement goes to `{fbx}.rebase.json` (`sourceMeters`, `sourceYawDegrees`,
  `unrealLevelCm` = (x, z, y)·100, matches the survey's c06 CENTRE); place the actor there in Sequencer. `--anim-rebase none` keeps level space.
- Measured: c06 removed (-4705.916, 419.689, -1661.708) m, -55.5°; c10 removed (-43.253, -24.967, -29.468) m, -91.4°.

Timing (PC, anim-only, one run): titus 5 clips 8.3 s (~1.7 s/clip), vortex 2 clips 6.1 s; UE batch import 0.60–0.75 s/clip.
Old flow (full export + import per clip) was ~45 s/clip.

UE import (`tools/ue/ue_import_anim.py <fbx|folder> <mesh|skeleton> <dest> [yaw|auto] [rootmotion|noroot|autoroot]`)
- Folder: every `*__*.fbx` under it (recursive), one `import_asset_tasks` batch.
- Yaw auto: reads skeleton bone 0's reference yaw only and picks the 90° multiple that puts it at 180 (fresh ROOT-rooted and the
  old Null skeleton → 0; old Original skeleton → 180). The earlier "measure t0 of the first clip" probe was fooled by
  clips (vortex slam ROOT starts 90° off its bind; c10 after rebase) and is gone.
- Root motion: on (lock ANIM_FIRST_FRAME) for gameplay clips; off (REF_POSE) for additive and cinematic clips
  (cinematic = `.rebase.json` beside it, "cinematic" or `_shot_<n>` in the name) — those are for Sequencer.
- `__ADD` clips: AdditiveAnimType AAT_LOCAL_SPACE_BASE, BasePose ABPT_LOCAL_ANIM_FRAME, ref frame 0.
- Check: `tools/ue/ue_anim_finish_check.py [root]` (SEQ settings, ROOT/CENTRE/HEAD t0/end, ROOTTRACK, ADDMOVE).
  Result `/Game/SM2_Work/Tests/anim_finish2` (skeletons from anim_survey/fix12):
  c06 and c10: ROOT (0,0,0) yaw 180, CENTRE t0 at (0,-5,148) / (0,-52,280) cm; titus run root track 485.9 cm, melee 489.3, turn90 yaw 90;
  vortex slam 488.2 cm with yaw 0 import; blink: only lid bones move (31 bones, max upperLid04_R 18.2°).
  `/Game/SM2_Work/Tests/anim_finish` is the first (pre-fix) import, kept for comparison.

Additive (REA, sm2_retail.exe; Morpheme task registrar `FUN_142c961b0`, rdx = task fn, r8 = name)
- `TaskBlend2TransformBuffsAdditive` (string 0x143b7fee8, LEA 0x142c96b28) → `FUN_142cddd90`: T = T_base + w·T_add; rotation =
  fast polynomial slerp identity→q_add by w (constants 0x143b83080..0x143b830bc), composed with the base rotation.
- `TaskFeatherBlend2TransformBuffsAdditive` (0x143b7ff88, LEA 0x142c96ba4) → `FUN_142ce01c0` (per-bone weights).
- `HasAdditive` string 0x1435ef808, used by the dumper `FUN_141060280` (0x1410602f6, 0x141060455).
- Slot 1 (0xF3/0xF7, clip+0x68) is slot 0 relative to its own frame 0, so UE base = this clip's frame 0.
  Exported as `{model}__{clip}__ADD.fbx`. Face clips: Apply Additive, or Layered Blend per Bone from HEAD for full-pose face clips
  (lipsync c11 has no additive slot; treat it as a partial clip with a layered blend).

Open
- Multi-character shots: each actor is rebased on its own; restore relative placement from each `.rebase.json`.
- Additive rotation compose order (q_base·q_add vs q_add·q_base) is not proven from the decomp.
- c10 ROOT carries its own heading track (180 → 149°); harmless with root motion off.

## SM2Browser v4: anim ordering, Space, GPU blend passes (2026-10-26, browser_v4)

Install: `C:\Users\Revaptor_FX\SM2Browser_v4\LibSaber.SM2Browser.exe` (MD5 C7B88AF9E83CE31C510E6CDD50A17DD1; first v4 build kept as `.bak1`). v3 untouched.

**Space** toggles play/pause (tunnel KeyDown on the window). Ignored while a TextBox has focus (search, anim filter) or a ComboBox dropdown is open; needs a mesh, a clip with curves, and the Play button visible.

**Anim list order** (`Animation/CharacterAnimations.cs`): "Bind pose" first, then ★ main, ☆ cinematic, then the rest (each: same-model name first, then path). Status bar shows the ★/☆ counts and the source; the tooltip lists the classes.
- Data link, not names: `ssl/**/*.cls` (3471 text files) → class whose `geom = { nameTpl = "<tpl>" }` matches the model → walk the parent chain (trailing `__type = "<parent>"`) for `morphemeController.nameBundle` (Titus: story_titus → pc_marine_story_client → … → pc_marine_base_client, `nameBundle = "primaris"`) and `animSet` ("titus") → `morpheme/<bundle>/<bundle>.mr.resource` (JSON `res_desc_morpheme`), `linksAnimation` = ★ set (primaris: 1813 clips, tacticus + group_animations/tacticus_vs_*; calgar: 16).
- ☆ = clip names the character (whole-word token: animSet + tpl stem, e.g. `titus`) and its path is cinematic (`/cinematics/`, `cinematic`, `/scenes/`, `xscene`, `_shot_`, `/shot_`, `/story_`, `story_`), excluding `/interplay/` and lipsync. Titus: e.g. `cinematics/story_cannon/.../shot_01/..._char_titus_intercessor.sani`.
- No class/bundle → fallback: non-lipsync clips naming the token are ★.
- No per-template `tpl/<x>.tpl/animations` folder exists; the class cache is built once per catalog (parallel regex scan, ~72 MB).

**GPU blend passes** (`Gpu/GlPreview.cs`, `PreviewPartDraw.Blend/Tint/Aniso/SecondSpec/Center`), from the material (`blendMode` + diffuse alpha channel AKill/Transp):
1. opaque; 2. masked: alpha test at 0.5, sharpened `(a-0.5)/fwidth(a)+0.5`, `SAMPLE_ALPHA_TO_COVERAGE` under MSAA; 3. translucent: premultiplied `ONE, ONE_MINUS_SRC_ALPHA`, depth write off, depth LESS, sorted back-to-front by part centroid.
- Masked/translucent are two-sided (N flipped toward the eye). AlbedoTint applied.
- Titus head findings (`SM2_PARTS_ONLY=1 --glbench` dumps every part's blend/channels/alpha stats):
  - white/dark eye rims = `ch_eye_shadow_01` (96 tris, translucent, Transp(a), alpha mean 134) drawn opaque in v3; lashes `ch_eyelashes_02` (soft_cool, translucent, alpha mean 15) likewise; `ch_titus_head` variant "hair" (2539 tris, translucent) = hairline/brow shell.
  - hair = same 13,582 cards twice: variant "hard" masked (AKill) + variant "soft" translucent (blendMode=blend). Hard pass writes depth; soft pass with LESS only fills the cut-away fringes (standard two-pass hair).
  - hair shading: glt.anisotropic_spec (aniso 0.65, empty dirMap → default 90°, along B) + second spec on → Kajiya-Kay primary lobe on T and albedo-tinted secondary lobe on `normalize(T + shift*N)` (same as import_sm2_materials.py spec_layers), wrapped by N·L. Cotangent frame is derived from screen derivatives when the part has no normal map.
- Before/after (v3-style all-opaque vs v4): `/workspace/browser_run_shots/v4/cmp_{front,left,right,back}.png`, `head_*_{before,after}.png` (bench: helmet hidden, target = eye-part centroid). PC: `Desktop\SM2_Work\Captures\v4_gpubench`.
- Bench (RTX 4090, ANGLE D3D11, MSAA 8x): static 0.37–0.46 ms, playing 0.68–1.23 ms, vsync 116–120 fps.

Caveats / pending: CPU renderer still ignores blend (draws all opaque). In-app GUI captures of the sorted Titus list and the GPU toggle check in the real window were not driven (user active on the PC at Unreal Editor); verify by opening cc_titus.tpl → Animations. Space-key behaviour verified by code path only.
