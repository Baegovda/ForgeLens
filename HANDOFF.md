> **New agent: read [`AGENTS.md`](AGENTS.md) first** (entry point, rules, user preferences), then `docs/handoff/` (STATUS, ROADMAP, WORKFLOW, RE_NOTES, FORMATS). Where this file's bootstrap below conflicts with them (push method, next step, owner), AGENTS.md / docs/handoff win. This file stays the chronological log: append a dated section per stage.

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
| REA (reverse engineering) | namespace `user-rea` (plugin "REA"; Ghidra, JADX and similar) | the REA-first rule, mandatory | **Obsolete (2026-10-27):** the REA tool is now https://github.com/morluto/rea on the box — see `docs/handoff/WORKFLOW.md` → REA (morluto/rea) |
| GitHub | `cursor-github` (read-only) | PR and commit reads | RequestScmConnect / InstallPlugin |
| Owner's PC shell | ListMachines, then the machine named **Revaptor-FX** (id `d3fb99ad-25ca-45c1-81af-41e5ca09104a` at the time of writing) | PowerShell, Unreal control, push | the owner has to register the PC |
| Unreal Editor | **EngineWire / MCP Automation Bridge 0.5.30** plugin in the project (https://github.com/ChiR24/Unreal_mcp), driven by `tools/ue/enginewire_call.py` via `uerun.ps1`; fallback **UEMCP** (`pip install uemcp`, https://github.com/ATDev-Inc/uemcp, `tools/ue/uemcp_call.py`) | import, materials, captures | see step 2 and WORKFLOW.md §5 |

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
  - Copy `tools/ue/enginewire_call.py` and `tools/ue/uemcp_call.py` to `C:\Users\Revaptor_FX\`, `tools/ue/uerun.ps1` to `Desktop\SM2_Work\Scripts\`.
  - Unreal 5.7.4 project at `K:\Unreal Projects\UE_VFX_57`. The editor must be open, and it listens on 127.0.0.1 only.
- **Smoke test:** `python enginewire_call.py` lists 23 tools (`system_control`, `manage_asset`, …); fallback `python uemcp_call.py` lists `ue_python`.

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
  - **Editor control: EngineWire (primary, `enginewire_call.py`, http://127.0.0.1:3000/mcp + token) or UEMCP (fallback, `uemcp_call.py`).** Both accept the same `ue_python ue_args.json`. Run on the PC in PowerShell:
    `cd C:\Users\Revaptor_FX; $code=@' <python> '@; @{code=$code}|ConvertTo-Json|Set-Content -Encoding utf8 ue_args.json; & python enginewire_call.py ue_python ue_args.json` (fallback: `uemcp_call.py`)
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

## Material recheck after baseLayer / DirectAo (2026-10-26, mat3)
Re-export with the current CLI (`C:\Users\Revaptor_FX\SM2Extractor6`, contains e6cadfa) into `Desktop\SM2_Work\Exports\mat3_<asset>\` (log `mat3_<asset>.log`; the step3_ exports are untouched): `LibSaber.SM2Extractor.exe export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/<asset>.tpl --format fbx-unreal --output <dir>`. Exported import_sm2_materials.py is byte-identical to the repo one and to `Scripts\import_sm2_materials_mat.py`.
- Sidecar diff vs step3: only the new `baseLayer` key; maps unchanged. Hormagaunt: all 26 slots identity (scale 1 / bias 0, tint white, reflectance 0.5), so no expected change. Titus: 12 of 41 non-identity: roughness bias +0.12 (purity seals legs_01_2 / sholder_armor_R_01, eye head_01_3), +0.05 (helmet_01, helmet_01_3, jetpack_2), +0.02 (face head_01); hair head_01_2 / _8 metalness bias 0.15 + roughness bias 0.2 + metallicTint (0.898, 0.792, 0.675); jetpack_3 (sc_grey_90) roughness constant 1; eyelashes head_01_5 metal 0.96 / rough 1 constant; eye shadow head_01_10 metal 0 / rough 1 constant.
- UE: `ue_mat_apply.py <asset> apply mat3_` (new optional 3rd arg = export folder prefix, default `step3_`) into `/Game/SM2_Work/Characters/<asset>` (pre-apply copy: `Desktop\SM2_Work\Backup\Characters_mat3.bak1`). verify: hormagaunt 26/26, secondSpec 8, MISMATCH []; titus 41/41, secondSpec 2, MISMATCH [], the 12 BASE slots above print with the sidecar values. M_SM2 vs 179 / ps 298 / 5 samplers, M_SM2_Translucent vs 398 / ps 1174 / 9.
- Captures `Captures\Mat\mat3\<asset>_{body|head}` (ue_cap_lit.py intensity=80; head mode needs `head only,head` as args 5/6, otherwise arg 6 is read as the keep list and the helmet stays). The first set right after apply was grey checker (shaders still compiling) even with no ShaderCompileWorker left; retaken ~3 min later. Box: `/workspace/mat/captures/compare_mat3_*.jpg` (rows: mat1 after / mat3 / |diff| x4).
- Mean |diff| mat1-after vs mat3 (front/side/back): hormagaunt body 0.57 / 0.18 / 0.44 (slightly brighter chitin highlights, from the master graph changes since mat1, not baseLayer); titus body 0.13 / 0.06 / 0.13 (helmet/seals rougher); titus head 1.54 / 1.87 / 1.56, all on the hair: a little darker and warmer (albedo *= lerp(1, metallicTint, 0.15), rougher), which is what the PS 00004_1 cb6 rows 8/9 formula gives. No regression found, so no fix in this pass.
- Not checked: there is no Hormagaunt or Titus in-game reference on the box (only `/workspace/mat/ref/calgar_ingame_head.png`), so "vs in-game" is still open for these two. Open REA question: eyelashes (head_01_5, translucent) get metal 0.96 constant; whether the translucent hair/lash shader reads glt.metalness the same way as glt PS 00004_1 was not traced.

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

## Mannequin → SM2 retarget prototype (2026-10-26, retarget)
Tool: `tools/ue/ue_sm2_retarget.py` (UE 5.7 Python, run through uerun.ps1). Builds NEW assets only; the owner's
`IK_titus_deathwatch`, `IK_cadian_*` and the Mannequin assets are never edited (the batch step first copies the
Manny clips to `<dest>/../MannySrc`, so nothing is written into `/Game/Characters`; `redirectors /Game/Characters` = none).
- `mesh <fbx> <dest>`: import a fresh ROOT-rooted SM2Extractor6 export (`Exports\retarget\{cadian,titus}\tpl\...`).
- `rig <mesh> <dest> <name>` → `IK_SM2_<name>`: retarget root CENTRE, FBIK (start CENTRE), 28 chains named exactly like
  IK_Mannequin (Spine BACKdown→BACKup, Neck, Head, Clavicle, Arm ARM1→HAND, Leg LEG1→FOOT2, Thumb `x1_fing1→x1FINGeff`,
  metacarpals `xN_fing1`, fingers `xN_fing2→xNFINGeff`), goals Left/RightHandIK, Left/RightFootIK. Armor/cloth unmapped.
- `rtg <dest> <name>` → `RTG_Manny_to_<name>`: auto_map_chains EXACT, retarget pose `SM2_AutoAlign` (auto_align_all_bones,
  then finger bones reset to the SM2 bind pose, see below), then `configure_ops`.
- `batch <dest> <name> <anims...>` → `<dest>/Anims/<anim>_<name>`; an existing output is kept as `_bakN`.
- `eval <manny> <sm2>`: world-space hands/feet/head error vs Manny×pelvis ratio, spine twist, index curl, planted-foot speed, root disp.
- `fbx <anim folder> <out>`: FBX export of the retargeted clips (`Desktop\SM2_Work\Exports\retarget\anims\{cadian,titus}`).
- `config`, `unalign`, `fingers`, `info`, `redirectors`: repair / tuning / inspection.

UE 5.7 API gotchas found:
- `IKRetargetFactory` already creates the 6 default ops; `add_default_ops` on top duplicates them (guarded: only when 0 ops).
- Ops created before the IK rigs are assigned resolve no bones → retargeted clips had **0 root motion**. Fix: `run_op_initial_setup(i)` for every op (in `configure_ops`). After it: walk 450→471 cm, jog 1060→1110 cm (Cadian, ratio 1.047), Titus 694/1635 (ratio 1.542) = exactly Manny × pelvis-height ratio.
- `RootMotionOpSettings.source_root_bone/target_root_bone/target_pelvis_bone` are deprecated; even reading them raises.
- `chains_to_retarget` elements are returned by value: modify `ch[i]`, write `ch[i] = x`, then `set_settings`. (Before this fix every per-chain change — IK offsets, finger rotation mode — silently did nothing.)

Results (contact sheets Manny / Cadian / Titus rows: `/workspace/retarget/sheet_*.jpg`; Idle, Walk, Jog, Jump):
| check | Cadian | Titus |
|---|---|---|
| hands vs Manny×ratio | 5–8 % of pelvis height | 7–13 % |
| feet | 2–5 % | 2–5 % |
| spine twist |diff| mean | 0.1–1.6° | 0.1–2.3° |
| index curl vs Manny | −14…−20° (bind pose is 12° straighter) | ±4° |
| root motion | = Manny×ratio | = Manny×ratio |
| foot min z (bone) | +1–2 cm | +6–7 cm (boot sole thickness, bone above ground) |
| planted-foot slide, walk | ≈ expected (+3 %/+20 %) | ≈ expected |
| planted-foot slide, jog | 43 vs 25 cm/s expected | 97–105 vs 34–37 cm/s expected |
Finger curl fix: auto-align straightened SM2 fingers onto Manny's directions (retarget-pose curl ~18° vs Manny ref 42°), and
deltas are copied from that pose → every clip ~24° less curled. Resetting finger bones in the retarget pose to the SM2 bind
pose (`unalign`) fixed Titus (±4°) and halved Cadian's gap.

Open:
1. Jog foot sliding (Titus ~2.7× expected). IK is live (a +30 cm goal offset lifts the feet 30 cm) and pelvis
   `scale_horizontal` 1.0 is the minimum (0.65 → 220, 1.25 → 320 cm/s); `GENERATE_FROM_TARGET_PELVIS` and
   `affect_ik_horizontal` 0 change nothing. Next: put the leg goal on the ankle (FOOT1) instead of FOOT2, check the FBIK
   bone settings/iterations, or add a foot-planting/stride-warping step.
2. Cadian finger bind curl is 12° straighter than Manny: add a small retarget-pose curl on `x*_fing2/3` if it shows.
3. Feet hover 1–7 cm: that's bone height; check against the boot sole mesh before adding a static offset.

### Retarget 2: jog foot sliding fixed (2026-10-26, retarget2)
Cause (per-frame dump, `/workspace/retarget/rt_probe6.py`): the IK Goals op scales the source foot offset by the
**leg-chain** ratio while root/pelvis move by the **pelvis-height** ratio. Titus legs are relatively short
(leg ratio 1.396 vs pelvis 1.542), so the planted FOOT2 drifted ±8 cm with the stride. Earlier "IK has no effect"
tests were the by-value chain-settings bug, not the solver.
Fix (`leg_fix`, run by `configure_ops`, or `legfix <rtg> [ext] [z]`): leg chains `extension = pelvis ratio / leg ratio`
(Titus 1.104, Cadian 1.049) and `static_offset.z = (ext-1)·(hip z − FOOT2 z)` (Titus +13.3, Cadian +4.4 cm) because
extension also pulls the goal down along the leg. Pelvis `scale_horizontal` stays 1.0 (0.65/1.25 are worse).
Planted-foot speed now vs Manny×ratio: Titus jog 29/25 vs 37/34, walk 222/175 vs 223/177; Cadian jog 22/20 vs 25/23,
walk 151/138 vs 151/120. Root motion unchanged (= Manny×ratio). Feet min z: Titus +8 cm (was +6), Cadian +1.6.
Still open: Cadian index curl −14…−20° (bind 12° straighter than Manny; needs a per-bone retarget-pose curl offset whose
local bend axis has to be probed first); Titus foot lift could be scaled ~0.85 if the boots read as hovering.

## Bulk validation (2026-10-27, step 4, bulk)
Base b913484. Branch `bulk`. Tools in `tools/bulk/` (PC copies in `Desktop\SM2_Work\Scripts\bulk\`) and `tools/ue/ue_bulk.py` (`Scripts\ue_bulk.py`). The report is `/workspace/bulk/REPORT.md` with `contact_sheet.jpg`.
- **Batch:** `manifest.csv` (37 assets: SM, Tyranid, Chaos, Human, Vehicle, Prop/Weapon/Door; 1-2 loco/idle clips per skeleton). Run order:
  1. `bulk_export.ps1 [-Only a,b] [-Par N] [-Exe path]`: writes `Exports\bulk\<a>`. An existing folder is renamed to `<a>.bakN`. Writes `<a>.log` and merges `results.json`.
  2. `bulk_ue.ps1 [-Only] [-Mode import|check]`: runs `ue_bulk.py` per asset into `/Game/SM2_Work/Tests/bulk/<a>`. An existing folder is renamed to `_bakN`. Writes `Exports\bulk\<a>.ue.json`.
  3. `bulk_cap.ps1`: offscreen `ue_cap_lit.py` front/side/back captures into `Captures\bulk\<a>`. Don't name a variable `$c` there: PowerShell variables are case-insensitive, so `$c` clobbers `$C`.
  4. On the box: run `make_report.py`, then `contact_sheet.py`.
- **ue_bulk.py checks:**
  - bones, ref-pose NaN, bounds;
  - slot count vs sidecar, MI count;
  - MI vs sidecar: switches, baseLayer, second spec, aniso;
  - texture params equal to the first sidecar map of each role, sidecar missing maps, files on disk;
  - per clip: frames, root motion flag, root displacement/speed, NaN, max bone distance.
- **Fixed: glt.blendMode "none" was exported translucent.**
  - 218 slots were affected:
    - chimera hull;
    - `ch_undersuit_v1/v2` on champions and constructors;
    - `ch_lictor_legs_01`;
    - `awp_censers_03`.
  - A further 7 slots took a layer's nested `blendMode = "tex_blend"` (`ch_soldier_helmet_01`, ravenwing/lamenters helmets).
  - REA: string-to-enum 0x14230f690 runs over the 20-entry table at 0x144cc9180 (none, black, opaque, blend, add, ...); enum-to-string is 0x14230f710.
  - `MaterialMaps.BlendMode` now reads only the direct `glt` child. `BlendOf` treats none and opaque as opaque.
  - Covered by `BlendModeTests`.
- **Diagnostic:** the export log now names up to 8 unmatched clip tracks. The partial matches are expected:
  - tacticus clips on non-Titus marines match 110-238/282: face_bind bones;
  - swarm hormagaunt matches 62/101: tongue/eye bones;
  - `CharacterWorldSpaceTM` never matches.
- **Not bugs:**
  - **termagant_caps texture params at defaults:** a stale UE import from the first export. A fresh import is clean.
  - **Placeholder tpls:** `imp_*_dyn` doors are nav_silhouette boxes (8 verts); real doors are e.g. `imp_bur_door_03` and `refrt_imp_gate_door_02`, both added to the batch. `cc_rubric_marine` is grey-box `sc_grey_*`.
  - **Bounds:** the log's "File bounds" are whole-tpl bounds and include markers and collision.
- **Open:**
  - **Constructor tpls** (`cc_sm_constructor_*`, `cc_ch_constructor_soldier`) hold every customization variant:
    - 5,315 nodes and 274 textures;
    - 375-670 s per export, ~8-10 GB RAM, 9-20 GB on disk;
    - every texture is written 3x (dds+png+tga).
    - The UE import of the sniper crashed the editor (RHI breadcrumbs during texture builds).
    - **Done:** `--variant` and `--tex-formats`. See "Constructor tpls" below.
  - **Fixed: Tyranid monsters and walkers had no root motion in UE (`UnrealRoot.HasSingleRoot`).**
  - Affected: carnifex, lictor, hive tyrant, warrior, zoanthrope, dreadnought, helbrute.
  - Their tpl hierarchy is `ObjectNNNN → ObjectNNNN → CENTRE` with no `ROOT`. FindPromotable declined, so FbxWriter kept the `{mesh}_Skeleton` Null. That Null became bone 0, and the 0xF4 motion sat on bone 1.
  - Now a single parentless bone also drops the Null.
  - Verified on the PC (`Exports\bulk_fix` → `/Game/SM2_Work/Tests/bulk_fix`): dreadnought `walk_fwd_v02_5_2ms` gives 526 cm/s, matching its name.
  - Not re-exported: the non-animated single-root assets (chimera, servitor, servo skull, doors, weapons).
  - Tests: `UnrealRootTests` (+3). `AnimFbx_IsSkeletonOnlyAndTheCurvesMove` now uses two roots so it keeps the Null.
- **Checker and tools:**
  - `ue_bulk.py` counts `M_SM2_ClothSimHidden` (collision/nav slots) as hidden, not as a mismatch.
  - `ue_bulk.py` takes an optional set name: `import <asset> bulk_fix`.
  - In `make_report.py`, `meta_fix/` overrides `meta/`. A zero-displacement loco clip with ROOT as bone 0 is a WARN (in-place; rubric `rubric_fwd`, lesser sorcerer `walk_fwd`; not verified in REA).
- **Captures:**
  - Captures right after a batch import can show grey placeholder textures while textures compile. 7 assets were recaptured into `Captures\bulk_re`.
  - TODO: `ue_cap_lit.py` should wait for texture compilation.
- **Result** (`/workspace/bulk/REPORT.md`): 37 assets, PASS 31, WARN 3 (captain partial tracks, rubric/sorcerer in-place clips), FAIL 3 (constructors, not imported).
- **Disk:**
  - Cleanup was approved by the user. Deleted `Exports\bulk\*.bakN`: 36 folders, 61.1 GB. C: free went from 4.9 GB to 66.0 GB; it was ~50 GB after the bulk_fix imports and DDC.
  - Deleted 18 of the 24 `/Game/SM2_Work/Tests/bulk/*_bakN` folders through the editor API.
  - The other 6 (calgar, chaplain, intercessor, primaris_captain, titus_assault, titus_deathwatch) were reported in use because their new `_Skeleton` still referenced the renamed mesh. **Correction (2026-10-27):** all 6 were deleted with `Scripts\bulk\ue_del_bak.py` after a UE restart. They took ~0.26 GB on K:, not the ~2.2 GB estimated earlier. No `Tests/bulk/*_bakN` folders remain.

## Constructor tpls: `--variant` + `--tex-formats` (2026-10-27, constructor)
Base d0ad14f. Branch `constructor`. Patch: `/workspace/patches/constructor/`.
- **Problem:** `cc_sm_constructor_tank`, `cc_sm_constructor_sniper` and `cc_ch_constructor_soldier` are customization libraries, not characters. They hold every armor piece of every region.
  - Exports took 375-670 s and 12-23.5 GB, with every texture written 3x.
  - The UE import of the sniper aborted the editor.
- **REA (how the game hides armor):**
  - Geom script bindings:
    - HideObj `FUN_14133eae0`;
    - ShowObj `0x14133fec0` (a thunk to `FUN_141349c70`);
    - HideObjs `0x14133eaf0`;
    - ShowObjsOnly `0x141347dd0`.
  - HideObj and ShowObj loop over the names and call `FUN_14133e750(geom, name, show, isPattern, 0x140000)`.
  - `FUN_14133e750`:
    - It looks up the name through `FUN_141d912a0` and checks the flags at `node+0x18`.
    - It then calls `FUN_142216790(tree, node, 0x140000, hide)`. That walks the subtree with `FUN_14225c680` and sets or clears bits at `node+0x1c`.
    - So hiding is recursive: hiding a `_grp`/LOD group node hides everything under it.
    - With `isPattern` it glob-matches through `FUN_142781420`/`FUN_142781890` and applies `FUN_141d99490` per match.
  - Default look: `ssl/body/customization/progression/character_customization_outfit_state_release.sslbin` calls `SetActivePresetToDefault` next to the constant `"PREDEFINED_PRESET_1"`.
  - Team and class come from the loading-scene dummy. `ssl/loading_scene_logic/customizable_character_dummy_tank.cls` has `nameTpl="cc_sm_constructor_tank"`, `team="LOYALIST"`, `masteryType="PVE_TANK"`.
- **Code** (`Customization/SslText.cs`, `Customization/ArmorVariant.cs`, `AssetExporter.ApplyVariant`):
  - The customization library is the text SSL. Customizable objects are every region's `meshes`, including subtrees.
  - Hide set = (customizable − shown by the preset/overrides) ∪ `meshesToHide` of the shown regions ∪ `powerfist*` ∪ `-obj`, minus `+obj`.
  - The filter only hides parts. It never unhides parts already hidden as LOD, volume or cloth-sim (HideReason `variant`).
  - Appearance resolution: the dummy `.cls` (`CharacterFromDummyClass`), then the tpl name, then the `PVE_PVP_<X>` fallback.
  - Writes `{model}.variant.json`: team, appearance and its source, preset, regions, hidden/visible counts, and notes.
  - With a variant active, only textures referenced by visible parts are written. The tpl TexList and the per-part `Material.Slots` are skipped (`partsOnly`).
  - `MeshPart.Node` (a tpl node id) and `Bone.SourceName` were added so parts can be matched to object names.
- **CLI:**
  - `--variant <spec>`, with tokens split on `,` or `;`:
    - `default`;
    - `all` / `none`;
    - `preset=N` or `preset=PREDEFINED_PRESET_N`;
    - `appearance=TEAM/UID`;
    - `team=`;
    - `SECTION=uid` (a region override);
    - `+obj` / `-obj`.
  - Without `--variant`, constructor tpls get `default` (PREDEFINED_PRESET_1). `--variant all` keeps every piece, as before. Other tpls are unchanged.
  - `--tex-formats dds,png,tga|all`. The default stays dds+png+tga.
  - png-only is safe for UE: `import_sm2_materials.py` reads the sidecar `packing[].stored` and falls back to dds only for old sidecars. The FBX links png, then tga, then dds.
- **UE RHI crash:**
  - Report: `Saved\Crashes\UECC-Windows-CF5F62BC...`, 2026-10-26 23:44 KST. "Abort signal received" in D3D11, with `LogRHI: Error: Breadcrumbs 'RHIThread'`.
  - It happened during the sniper's material import, after 89 2K textures (each with a "Required Memory Estimate 351 MB"). The whole Python build ran inside one editor tick.
  - Cause: resource exhaustion from texture count and size, not a data bug.
  - Mitigation:
    - The variant filter (sniper 895 → 81 png).
    - `import_sm2_materials.py` calls `unreal.SystemLibrary.collect_garbage()` every `GC_EVERY = 32` texture imports.
  - The separate 00:43 crash (62B0) was an ensure in ForceDeleteObjects on `cc_termagant_bak1`, which is unrelated.
- **Re-export** (`tools/bulk/ctor_export.ps1 -InPlace`, exe10, default variant, `--tex-formats png`, 3 parallel) into `Exports\bulk_fix\<a>`:

  | tpl | before s | after s | before GB | after GB | textures before → after | FBX MB | hidden / visible parts |
  |---|---|---|---|---|---|---|---|
  | tank | 670.8 | 104.7 | 23.5 | 0.394 | 1031×3 → 69 png | 365 → 24.1 | 978 / 54 |
  | sniper | 513.8 | 102.7 | 19.8 | 0.413 | 895×3 → 81 png | 22.4 | 778 / 64 |
  | soldier | 375.5 | 64.6 | 12.1 | 0.276 | 520×3 → 53 png | 17.1 | 370 / 41 |

  - Times are wall-clock with 3 exports running in parallel. In earlier runs, the variant alone was 58-96 s.
  - Material sidecar slots after filtering: tank 54 (was 1032), sniper 64, soldier 41.
  - Validation on the real tpls (box console app): tank hid 565 of 620 customizable objects, sniper 442 of 498, soldier 237 of 286.
  - The sniper has 4 benign notes: its preset lists torso_chest4/5 and torso_l/r_pauldron, which its library doesn't have.
- **UE:** `ue_bulk.py import <a> bulk_fix` imports into `/Game/SM2_Work/Tests/bulk_fix/<a>`, then run the captures.
- **Tests:**
  - `ArmorVariantTests` (+9): SSL parse, default preset, preset 2 grp subtree and meshesToHide, overrides and forced objects, unknown region, no LOD unhide, request parse, appearance from cls/name, and `Part.Copy` keeping Node.
  - `VariantOptionTests` (+7).
  - Totals: SM2.Assets 81, SM2Extractor 42, LibSaber.Tests 5, all passing.
- **Cleanup (approved by the user, 2026-10-27 01:14 KST):**
  - `/Game/SM2_Work/_Legacy` (SM2_Test, SM2_v4, SM2) and `/Game/Test` held 0 files. Both were removed.
  - The registry check printed `GONE True True` (`Scripts\legacy\ue_rmpaths.py`).

## Secondary physics: browser + Unreal (2026-10-27, physics)
Base b7ae735. Patch: `/workspace/patches/physics/`. Captures: `/workspace/browser_run_shots/physics/`.

The goal is the motion the game shows on purity seals, tabards, cloth strips, cloaks, cables and dangling parts.
- The browser simulates it live.
- The exporter bakes the same simulation into the FBX, so Unreal plays it with no runtime setup.

### REA (sm2_retail.exe)
Address conversion: rdata VA = file offset + 0x140001200; .text VA = file offset + 0x140000a00. Decompiles are in `/workspace/rea/dec_phys*`.

**Where the data lives.** The character `.cls` (text SSL) carries it in two places:
- `prop_cloth_sim.clothes[]`: `simMeshName`, `renderMeshes[]`, `maxInfluenceDist`, `distanceRemapPower`, `useTopologicalDistance`, and `config`.
- `prop_phys_jiggle`, with one settings block per bone.

The field-name strings are at 0x1435f10c0..0x1435f1510. `PhysicsLocator` finds the `.cls` whose `nameTpl` is the model (Titus → `pc_marine_pve_story_titus_client.cls`, Calgar → `npc_calgar_marine_client.cls`).

**Jiggle (`PhysJiggleObject`).** `JiggleSolver` is an exact port of the step loop at 0x14147e5b5 (inside FUN_14147e500).
- Settings layout:
  - +0x10 stiffness, +0x14 mass, +0x18 damping, +0x1c gravity;
  - +0x20 offsetDirection, default (0,0,1) @0x143609db8;
  - +0x28 offsetDistance;
  - +0x30/+0x38 constraintMin/Max, defaults (-1,-1,-1)/(1,1,1) @0x143609e08/0x143609dd8;
  - +0x40 writeback flag.
- The step:
  - M = bindLocal × parentWorld, orthonormalised by FUN_140fd45e0. Target = M·(dir·dist).
  - Spring: v = (v + (t−p)·k/m·dt [+ gravity/m on y]) · exp(−damping·dt).
  - Per-axis clamp FUN_14171ad80 in the bone frame, scaled by dist (0x14147ea5b..0x14147eb3c).
  - Sentinel 888.888 @0x1435571a0 before the first step. Teleport check at 0x14147ed64.
  - The bone is re-aimed at the particle; it does not translate.

**Cloth config.** The struct is registered at 0x141090c39..0x141091847, and the constructor ends at 0x14108f119.
- Phases: the constructor at 0x141089140 gives stiffness 0.8, multiplier 1, compression 0.9 and stretch 1.1.
- Other blocks and defaults:
  - tether +0x40 (0.5, 1; 0x14108a660);
  - motion +0x4c (0.5, 1; 0x141089966);
  - separation +0x58 (off, 1, 1; 0x141089fe6);
  - selfCollision +0x64 (off, 0.5, 0; ctor 0x14108efce);
  - gravity +0x74 = (0, −9.8, 0) @0x14108f112;
  - solverFrequency +0xe0 = 30;
  - stiffnessFrequencyScale +0xe4 = 0.5;
  - environment +0xb0 (airDrag 0.5, airLift 0.5, winds 0; 0x14108f03a..0x14108f0a4).
- Collision shapes: capsules, spheres and boxes on bones.
- Sim mesh: the `*_SIM` shell.
  - COLOR0.r is the per-particle max distance; c ≤ 1e-4 means pinned (it follows skinning).
  - Render parts bind to the closest sim triangle (barycentric plus normal height). The weight is 1 within `maxInfluenceDist`, else (maxInfluenceDist/d)^distanceRemapPower.

### Browser (live)
- Code: `Physics/{PhysicsParser, PhysicsSetup, PhysicsLocator, JiggleSolver, ClothSolver, PhysicsRuntime}.cs`.
- `ClothSolver` is a PBD stand-in for NvCloth. It runs:
  - phases (V/H/bend/shear) with compression and stretch limits;
  - tethers and motion constraints (max distance);
  - capsule, sphere and box collision;
  - drag and lift wind with unit particle mass;
  - substeps = solver frequency.
- GPU preview: the `Physics` checkbox (shown when the model has a `.cls` setup).
  - It resets on clip change, loop and time jump-back.
  - About 0.7–0.9 ms per step, so 120 fps holds.
- Render parts are matched by exact name, falling back to the name without `_LODn`. `cc_calgar.tpl` only has `cloak_REND_01_LOD1` etc. (`ClothEntry.DrivenParts`).

### Export: baked into the FBX (`Physics/PhysicsBake.cs`, `Physics/PhysicsSidecar.cs`)
Unreal cannot be set up live from Python (see below), so the exporter bakes the simulation (on by default; `--no-physics` turns it off).
- **Jiggle:** the exact port runs over each clip at 60 Hz. Each jiggle bone's local transform is written as keys.
- **Cloth:** `AddClothBones` adds helper bones `{SimMesh}_physNN`.
  - There is one per free sim particle (`particlesPerBone` = 1, cap 1024 per cloth); pinned particles stay on skinning.
  - Each helper is parented to the particle's dominant skin bone and placed at the particle.
  - Every render vertex of the driven parts, in the base buffer and every LOD level, is re-skinned. Its closest sim triangle's corner bones get barycentric × influence weight; the original skin keeps the rest. The top 4 are kept and renormalised.
  - Per frame, each helper's rotation is a polar-decomposition fit (Müller, warm-started) over the particle and its one-ring. Translation = the simulated particle.
- **Clip settings:**
  - Preroll is 1.5 s, holding frame 0 so springs and cloth settle.
  - Baked locals are keyed by bone name (`AnimationClip.Baked` / `BakedBones`), so they survive `VisibleGeometry` and `UnrealRoot.Promote` copies.
- **`--anim-only` exports** add the same helper bones (they are deterministic: same tpl and `.cls` give the same names), so the clips carry both jiggle and cloth curves.
  - The UE skeleton needs the helpers, so a model exported before this patch must be re-exported and re-imported.
  - Without them, Unreal ignores the cloth tracks and keeps the jiggle tracks.
- **Sidecar `{model}.physics.json`:** the source values, what was baked per clip, and the Unreal mapping for a live setup. `ue_sm2_physics.py` is written next to it.

Measured on the box (PhysBench, `SM2_PHYS_BAKE=1`). "baked" is the export skeleton played as plain skinning with physics off, which is exactly what Unreal plays:

| Model / clip | Helpers | Jiggles | On vs off | Baked vs live | Reproduced |
|---|---|---|---|---|---|
| Titus run | 922 | 34 | 4.80 % px | 0.18 % | ≈96 % |
| Calgar convoy run (cloak, belts, ribbons) | 1294 | 3 | 3.02 % px | 0.78 % | ≈74 % |

Bake time: 0.5 s (Titus, 57 frames) and 3.0 s (Calgar, 399 frames).

FBX check with assimp (`/workspace/phys_export/titus`): the model FBX has 581 nodes. The clip has 368 animation channels with physics and 281 without (34 jiggles + helpers).

### Unreal mapping (sidecar `ue`, for a live AnimDynamics / Chaos Cloth setup)

| SM2 | UE |
|---|---|
| jiggle offsetDirection·offsetDistance | AnimDynamics PendulumOffset = −dir·d·100 cm |
| stiffness/mass | LinearSpringConstant = k/m |
| damping | LinearDampingOverride = damping |
| gravity/mass | GravityScale = (g/m)/−9.8 |
| constraintMin/Max | linear limit along the offset = c.z·d·100; angular limits = atan(c) in degrees |
| phases V/H × multiplier | Chaos Cloth EdgeStiffness = avg(V,H)·mult |
| bend | BendingStiffness |
| shear | AreaStiffness |
| tether stiffness/scale | TetherStiffness / TetherScale |
| damping | DampingCoefficient |
| gravity | GravityOverride (cm, axis-converted) |
| airDrag/airLift | Drag / Lift |
| linear/angular velocity scale | LinearVelocityScale / AngularVelocityScale |
| thickness, friction | CollisionThickness, FrictionCoefficient |
| solverFrequency | SubdivisionCount = ceil(freq/60) |
| COLOR0 max distance | MaxDistance mask (m → cm) |
| capsules/spheres/boxes | physics asset bodies (cm) |

### UE side (`tools/ue/ue_sm2_physics.py`, embedded and written next to the export)
- `... -run=pythonscript -script="ue_sm2_physics.py <physics.json> <mesh asset> [/Game/SM2_Work/Tests/physics] verify"` checks that every jiggle and helper bone is in the skeleton and prints `SM2PHYS` lines.
- `physasset` makes a physics asset through `SkeletalMeshEditorSubsystem.create_physics_asset`. It renames the result to a free `_N` name and never replaces an existing asset.
- These cannot be done from Python, which is why the default is the bake:
  - There is no clothing factory: "Create Cloth Asset from Selection" is UI only.
  - AnimGraph nodes (AnimDynamics, RigidBody) cannot be added.
  - Control Rig `RigUnit_PointSimulation` is disabled.

### Limits and open items
- NvCloth is approximated (PBD):
  - wind uses unit particle mass;
  - self-collision is skipped;
  - box collision assumes size is the full extent.
- Jiggle scalar defaults (when a `.cls` omits stiffness, mass or damping) are not verified. The teleport threshold is unknown.
- The CPU renderer is not physics-driven; only the GPU preview is.
- Baked clips are not loop-seamless (each clip is simulated from a settled frame 0).
- Helper bone count is high (one per free particle). Unreal handles it, but `AddClothBones(particlesPerBone, maxBonesPerCloth)` can trade fidelity for fewer bones (4 per bone, cap 64 → Calgar 1.7 % residual).
- **PENDING (PC was in use):**
  - Export on the PC with this build.
  - Import to `/Game/SM2_Work/Tests/physics`.
  - `ue_sm2_physics.py verify`.
  - Capture browser vs UE side by side on the same clip (titus `tacticus_run_v01_intercessor`).

## Browser VFX playback (2026-10-27, vfx)
Branch `vfx-work` (from 642fb56), patches in `/workspace/patches/vfx/`; not pushed. Niagara/UE export is out of scope.

### Code
- `src/LibSaber.SM2.Assets/Effects/`:
  - `SslText` parser
  - `EffectLibrary`: sfx_group instancing and `__type` prototype inheritance
  - `EffectDefinition`: emitters, lights, attached sub-emitters, alpha masks, draw target
  - `EffectSimulation`: fixed 60 Hz, seeded, deterministic, with backward seek replaying from 0
  - `EffectSprites`: camera-facing quads, sorting, flipbooks
  - `PartAliases`: flipbook grid
- `src/LibSaber.SM2Browser/Gpu/EffectRenderer.cs`: GPU pass with a lit ground grid, one draw per emitter batch with its blend state, HDR exposure + tonemap, soft particles, and an alpha-mask texture unit.
- `MainWindow.Vfx.cs`:
  - Selecting an `.sfx` (use the Effects filter + search box) plays it on the GPU preview.
  - VFX bar: Restart, scrub (pauses), Loop, Ground. Space and the Play button toggle play/pause. Drag orbits, wheel zooms.
  - Effects, textures and `part_aliases.ps` are read from the open catalog by basename.
- Dev mode without a game folder: `SM2_VFX_LOOSE="sfxDir|part_aliases.ps|texDir" SM2_VFX_OPEN=<name>`.
- Bench: `--vfxbench <sfxDir> <part_aliases.ps> <texDir> <out> name@sec[@distance]...` writes 4x2 sheets, perf numbers and a determinism check.
  - Env vars: `SM2_VFX_ONLY`/`SM2_VFX_SKIP` (emitter filters), `SM2_VFX_EXPOSURE`, `SM2_VFX_LIT`, `SM2_VFX_FRAMES=1`.
- Tests: `test/LibSaber.SM2.Assets.Tests/EffectTests.cs` (6) cover parse, group override, explosion count, constantFlow rate, genOnDie, determinism/seek, aliases and curves. The full suite passes (101).

### REA (sm2_retail.exe; VA = strings_ascii offset + 0x140000000)
- **Node types:** sfx_group @0x143646930, sfx_part @0x14363c148, sfx_trail @0x14363c280, CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0.
- **part_aliases.ps** is read by 0x1418fe5c0. Keys: nFrameX @0x143647ea0, nFrameY @0x143647ea8, nFrameMax @0x143647eb0 (0 means X*Y; clamp 255), motionVectorScale @0x143647ec0, Mesh.Vertex0..6 @0x143647ed4.
- **blendType** parser is 0x141751cd0: blend=0, add=1, blend_add=2. **sorting** parser is 0x1417534f0. softDepth @0x143645530.
- **attached** @0x143644cd0 → genOnFly @0x143644b38 / genOnCDT @0x143644b48 / genOnDie @0x143644b58. Entries are `{name, numParts @0x143644b18}`. Error string @0x143648810 says LCS-mode emitters cannot be attached.
- **speedInherit** @0x143644878 is a {min,max} fraction of the parent velocity.
- **Untyped emitter entries** (5499) are CPU emitters only when they carry `__node`. Orphans are skipped (this removed the orange square in arifle).
- **Draw target:**
  - `drawInSimulationTex = drawInSimulationEnabled` draws into the wind/water sim only, not colour (grenade wind_expl/water_expl).
  - `animIn.channel = DISTORTION` is refraction only.
  - Neither is drawn.

### Coverage of the starter set (captures in `/workspace/browser_run_shots/vfx/`)
| effect | emitters | drawn | not drawn |
|---|---|---|---|
| fx_bolter_muzzle_client | 14 + 1 light | 12 sprite + light | 2 lwiGeom mesh |
| part_wpn_arifle_mzl_client_01 | 10 | 10 | 1 orphan |
| part_mg_frag_grenade_expl | 33 | 31 | 2 sim-only |
| part_mg_smoke_puff_01 | 3 | 3 | – |
| fx_cine_bolter_tracer | 1 | 1 | – |
| part_mag_ee_bolter_flesh | 9 | 6 | 3 mesh, 1 untyped |
| part_mag_bullet_mid_blood_red | 3 | 3 | 1 orphan |
| part_mag_blood_red | 27 | 27 | – |

Perf on the box (llvmpipe, 512²):
- sim ≤ 0.021 ms/step;
- sprite build 0.02–1.6 ms;
- GPU 2–25 ms (grenade spikes higher on llvmpipe);
- every effect is deterministic;
- the UI holds about 60 fps on the grenade.

### Limits / open
- Not rendered:
  - lwiGeom mesh particles;
  - sfx_trail*;
  - distortion/refraction;
  - wind/water sim draws;
  - additionalContrast;
  - GPU-only features (vector fields, depth collision).
- Heuristics:
  - size is treated as a radius;
  - ratioDependency is simplified;
  - blend_add is interpreted;
  - Exposure 1/250 and lit ×100 were tuned on corpus medians;
  - genOnFly is approximated (a burst for explosion targets, a per-parent rate for flow targets).
- Standalone sockets: a socketed effect is placed 1.2 m up, pointing +X. Attaching to a model bone is not wired up yet (`EffectInstance.Socket` hook exists).
- Grenade smoke reads a bit dark/blue. Not compared side by side with the game: this needs in-game captures on the PC when it is free.

## VFX 2: mesh particles + trails (2026-10-27, vfx2)
Based on 00d32bd. Patches in `/workspace/patches/vfx2/`. Captures in `/workspace/browser_run_shots/vfx2/` (before: `../vfx/`).

### What was added
- **lwiGeom mesh particles.** `Effects/EffectMeshParticles.cs` parses the particle into `MeshParticleDef` (REA: lwiGeom @0x143644870, parser 0x141760a45..0x141762f4d).
  - `nameGeomTpl` names the template. `typeInst` (enumeration/range/default) picks the sub-object: template parts grouped by node, in node order.
  - `initRotAxises.random`. `applyRotationFromEmitter`.
  - `sfxMtlOverrides`:
    - color (constExpr / ageSpline / ageSplineNew). Raw values are **R,G,B,A**. Evidence: the flesh splash `[255,44,3,155]` is blood red, and read as A,R,G,B it turns blue.
    - emissiveIntensity, colorTransparency.
  - Each instance's world matrix = scale(size×emitScale) · rotation (+Z aligned to velocity if `isAlignSpeed`) · translation.
- **Browser side** (`SM2Browser/Effects/EffectMeshes.cs`): an async cache of tpl→GPU meshes (TemplateMeshReader). The material is read from the texture `.td`:
  - `preset "sfx"`: premultiplied translucent, with `tint [A,R,G,B]` and `clipping.level`.
  - `shader_blood_splash` (solid_blood) and `game_material "flesh"` (flesh_material): the texture is only a placeholder, so they draw white × override colour. Flesh gets an assumed dark red; this is not REA-confirmed.
  - Everything else: opaque and lit.
- **Mesh shader** (`EffectRenderer` `_mesh`):
  - Lighting: key light plus ambient, plus the effect's point lights.
  - Emissive ×25 for sfx materials only.
  - Override colours are sRGB.
  - Hue-preserving tonemap, so saturated blood stays red instead of going orange.
- **sfx_trail ribbons** (`Effects/EffectTrails.cs`; REA strings 0x143645e08.., save serializer 0x1417b1031; the runtime builder was **not** traced):
  - One point per sim step at the parent, split by `maxSegLength`. Points die after `timeLifeSeg`; the length is clipped by `maxTrailLength`.
  - Width and colour are sampled along the ribbon.
  - Shapes: billboard/cylinder (camera-facing), tape, cross (2 planes), star (3 planes).
  - The union `__type` decides the shape (saves keep stale members).
  - Standalone preview flies the root along +X at 40 m/s.
  - `lighting.isEnabled` trails use the same LitScale as sprites.
- **Mesh sources:**
  - Bench: env `SM2_VFX_MESHES` or a sibling `mesh/` folder (`tpl/<name>.tpl/`).
  - UI loose mode: 4th field of `SM2_VFX_LOOSE`.
  - Catalog mode: by basename.
- **Tests:** `EffectGeomTrailTests` (3) cover lwiGeom parse + RGBA colour, mesh spawn/expiry and trail segmenting/expiry. The full suite passes (104).

### Coverage
| effect | before (vfx) | after (vfx2) |
|---|---|---|
| fx_bolter_muzzle_client | 12 sprite + light; 2 mesh not drawn | 12 sprite + 2 mesh + light |
| part_mag_ee_bolter_flesh | 6/9 (3 mesh + 1 untyped not drawn) | 6 sprite + 3 mesh (gibs + blood splashes); 1 untyped `emitter?` left |
| trail_bolter_tracer_autority / trail_stalker_bolter_tracer_client | not supported | drawn (star / billboard ribbons) |
| trail_bolter_smoke | not supported | simulated (139 pts), but nearly invisible: additive, colour ≤177/255 × intensity 2 |

### Known gaps / next
- Blood-splash VAT animation (`splash_5_v2_anim_data*`, `combineMtlAnimControl.animStage`) is not implemented, so the static rest mesh is drawn.
- Distortion pass, socket attach to the selected model in the UI, and the grenade smoke colour are not done.
- `smoke_spray_01` does not exist in retail (only `_02`/`_03`). It is genuinely missing in the game data, not an extraction miss.

## vfx3 (part 1): effects on model sockets + anim-event cues

- `EffectHost` (Assets/Effects): bone sockets from pose worlds (`SetPose`), `Attach(def, bone)`, wildcard `name*`; `sfx_source_pt` = root.
- Emission axis REA: emitter without `emitDir` (`parentArea.direction isEmitByNormal`) emits along the parent normal; on a bone that is
  bone **+Z** (`EffectDefinition.EmitByNormal`; wpn tip barrel = +Z, part_mzl_bolter flame_circle_2 z+, fx_jetpack_attack jets z+ on jet bones). No parent: +Y as before.
- Anim events: ETNA block 245 (`AnimEvents.Parse`, `AnimationClip.EventTracks`), times normalised 0..1. Event→VFX is data-driven in
  blueprint .sso graphs (`AnimEventVfxMap`: BulkVfxOnMorphemeEvents + exec BFS from MorphemeEvent nodes to sfx starters + string pipes).
  Map unions all blueprints -> per-blueprint source filter (UI "blueprints" box / `SM2_VFX_BP_FILTER`). `*_stop_effect` events not handled.
- UI: HostVfxBar (effect name, socket combo "Auto (nameParent)" / bones, Attach, Clear, Loop, Anim events, filter). Overlay via
  `GpuFrameRequest.Overlay`. Env: `SM2_MODEL_OPEN="tplDir[|sani]"`, `SM2_HOST_OPEN`, `SM2_HOST_EVENTS=1`, `SM2_HOST_LOD1`, `SM2_HOST_CAM`, `SM2_HOST_PLAY`, `SM2_HOST_DEBUG=1` (per-frame quad log).
- Bench: `--hostvfxbench <tplDir> <sani|-> <sfxDir> <aliases.ps> <texDir> <out> <effect|-> [bone|-] [seconds] [every]`.
- Known issues (todo, vfx3 item 5): fx_jetpack_attack sprites huge (ageSize spline 0..33 x scale; size treated as half-width) -> size semantics
  need REA; `degNear` near-camera fade not implemented; loose wpn_bolt_rifle_01 tpl decodes LOD1 parts + Object* proxies (drum shape);
  tacticus dash clip on cc_titus_assault leaves ROOT ~9.5 m away even with In place.

## vfx3 part 2 (Grok Bot, 2026-10-27)
- d6b28f4 missing-texture sprites are skipped (were white quads; this was the "oversized jetpack flame"; size = half-width is right).
- e5628ce distortion pass: `typeParams.distortion` (normal map, power ageSpline) + `visuals.sdr` DISTORTION. Scene grabbed once,
  quads redrawn with offset (RG*2-1)*power*alpha*`SM2_VFX_DISTORT` (default 0.04, heuristic). SELF_DISTORTION parsed only.
- 904110b anim-event stop cues: BlueprintNodeVfxStop / SfxLibraryStopsfx / StopPackOfVfx / StopAllSfx and duration-event
  OnDeactivate/OnExit set `EffectInstance.StopTime` (emission, trails, lights stop; live particles finish). Retail: 62 stop events.
- a8abb8c `optimization.degNear/degFar` linear ramp (radStart->radEnd) on size/intens/alpha/disp for sprites (`SM2_VFX_DEG=0` off).
  Field set confirmed in exe reflection strings; curve shape not REA'd. Not on trails/mesh particles yet.
- ac0a3d3 bolter drum: Object0080..0098 merged proxies (root children, no mesh name) hidden on templates with a _LOD chain.
  Note LOD0 of rigid weapons is spread over several vertex pools (by vertex format); the preview already draws visible parts of all pools.
- 8df6d52 In place: rebase reference must carry deforming bones (cam_anim was picked), gameplay clips get no rebase,
  `MeshPreview.RemoveRootMotion` strips ROOT XZ travel per frame.
- Todo: REA grenade smoke colour, trail_bolter_smoke faintness, ratio, blend_add, exposure/light scale, genOnFly;
  untyped flesh emitter (part_mag_ee_bolter_flesh); VAT blood splashes; SELF_DISTORTION render; degNear on trails/meshes.

## vfx3 part 3 (Grok Bot, 2026-10-27)
- 1f9ffce coverings: `em_covering_*` entries (no typeParams/visuals/emission, only `physics.cdt.coveringInteraction.radiusScale`)
  are surface painters (coveringMask layer of nearby surfaces), not particles. Parsed to `EffectDefinition.Coverings`
  (`CoveringDef`: kind, radiusScale, isVisible) and shown as a note in the VFX panel; no longer `Unsupported`.
  Corpus: 27 (blood r1 x15, water r1 x6, mud r1 x3, blood r0.4 x2, blood r0.6 x1). part_mag_ee_bolter_flesh = blood r0.4 on HEAD.
- 28d731d grenade smoke colour: `seq_fin_expl_g` is channel-packed (R dark shading, G mid, B hot/fire mask), so drawn as RGB it
  is blue. `visuals.textureTintControl` (@0x143645278, field +0x140, 0..1) is set to 0 on most of its users ->
  texture used as luminance (`EmitterDef.TextureTint`, sprite shader mix(lum, rgb, tint)); absent = 1 (unchanged).
  Capture: browser_run_shots/vfx3/tint/grenade_tint_cmp.png, grenade_tint_zoom.png.
- Not implemented (data noted): `additionalContrast` (0..400, 2969 non-zero) + `contrastColorKey` [A,R,G,B] (e.g. grenade
  37.53 / orange 246,102,14): looks like a hot-colour ramp on bright texels (fire core). Shader is GPU-side, not REA'd.
  `applyColorGradients` (default on, 61 x False) feeds PART_CPU_EMITTERS_COLOR_GRADIENTS; also GPU-side.
- trail_bolter_smoke: spawned by `EcsComponentPjlShotSfx.sfxTrailsGlow*Uids` on bolter_bullet (firearm_describer.sso), i.e.
  it follows the projectile. Faint by data: blend add, colour ARGB alpha<=113 (x alphaMult 2), rgb 38..177, intensity 1x2,
  texture trail_smoke_hard is a thin wisp (mean alpha 17), and degNear radStart 2 -> radEnd 20 scaleAlpha 0 fades it near
  the camera. Not a bug; tcIn scale [0.025,1] (Clamp) is not applied to trails (corpus: 0.2/0.1/0.01 x 1 common).
- VAT blood splashes (not implemented, findings): splashes_vtxa_lwi parts splasheNN map to solid_blood.td materials splash_NN
  (shaderType shader_blood_splash; floatParam0-2 bounds min (-0.5,-0.5,-0.0624), 3-5 scale (1/255,1/255,0.001397), 6 = 120,
  7 = 60/119, tex1 albedo = splash_5_v2_anim_data (pos), addInfo _n (normals), tex2 addInfo _d (precision low byte?)).
  Tri-soup meshes (splashe05 19086 verts); UV1 u = (col+0.5)/2400, v takes 8 values (119k+0.5)/951 = 8 row bands.
  The pct is downsampled (2400x471, `downsampled: true`), bands are 58.875 rows. Decoded positions at the band rows do not
  reproduce the rest mesh (best 0.10 m mean error over all perms/frames) and triangles are only coherent at some rows,
  so the layout/encoding is still unknown. splashe06/08/10/... are 1-tri placeholders.

## UE verification: constructors + physics (2026-10-27, ue_verify)
Base e4ceb7d (extractor built from it: PC `Desktop\SM2_Work\Bin\SM2Extractor_e4ceb7d`). Patches: `/workspace/patches/ue_verify/`.
Captures: PC `Desktop\SM2_Work\Captures\ue_verify\`, box `/workspace/browser_run_shots/ue_verify/`. UE 5.7.4, editor not restarted.
- **Constructors → `/Game/SM2_Work/Tests/bulk_fix/<a>`** (existing `Exports\bulk_fix` exports from 01:34, exe10 = same constructor code;
  default variant PREDEFINED_PRESET_1, png only), `ue_bulk.py import <a> bulk_fix`, one at a time:

  | tpl | mesh / mat / anim s | slots = MIs | textures | bones | errors | clip root speed |
  |---|---|---|---|---|---|---|
  | tank | 89.6 / 51.3 / 87.0 | 54 = 54 | 69 | 6104 | 0 | 520.6 cm/s |
  | sniper | 69.1 / 57.9 / 60.1 | 64 = 64 | 81 | 5315 | 0 | 520.6 cm/s |
  | soldier | 9.8 / 41.3 / 7.1 | 41 = 41 | 53 | 2328 | 0 | 520.6 cm/s |

  - No D3D11 RHI abort, no new crash report (last is the unrelated 00:43 62B0), no `LogRHI: Error` in the log. Editor peaked ~14 GB.
  - 0 material mismatches, 0 bad texture params, 0 missing files. Looks: one armor set each, no stacked pieces; tank and sniper
    bare-headed (`helmet_*_no_helmet`), sniper cloak, chaos soldier helmeted (variant.json regions match).
  - The bone count still includes the bones of hidden parts (harmless; only parts are hidden).
- **Physics → `/Game/SM2_Work/Tests/physics/<a>`**: fresh PC exports `Exports\physics\{cc_titus,cc_calgar}` (`--tex-formats png`,
  physics on; Titus `tacticus_run_v01_intercessor` 46 s, Calgar `calgar_last_stand_convoy_start_run_calgar` 40 s), then
  `ue_bulk.py import <a> physics`, then `ue_sm2_physics.py <json> <mesh> <folder> physasset`:
  - Titus: 1447 bones, 38/38 MIs; 34 jiggle (0 missing) + 922 helper bones (0 missing). Calgar: 1596 bones, 44/44 MIs; 3 + 1294 (0 missing).
  - `PA_cc_titus_sm2`, `PA_cc_calgar_sm2` created (not assigned).
  - Motion check `tools/ue/ue_phys_probe.py <json> <anim> [samples]` (local swing vs frame 0, per jiggle group / sim mesh):
    Titus 32/34 jiggles move (max 95°), every helper moves (tabards 25-42 cm, seals 26-38 cm); Calgar 3/3 jiggles, cloak
    1024 helpers up to 161 cm, belts/ribbons 41-52 cm. Frame captures (ue_cap_lit pose → capture → export) show the tabards,
    purity seals and the cloak following the run with no exploded vertices.
  - AnimDynamics / Chaos Cloth live setup: still not scriptable. 5.7 exposes `AnimGraphNode_AnimDynamics`, `ChaosClothAsset` etc. as
    classes, but no AnimGraph node or cloth-asset creation API (`SkeletalMeshEditorSubsystem` has only create/assign physics asset).
    The bake is what plays; the json `ue` blocks remain the manual recipe.
- **Fixes (this stage):**
  - `ue_sm2_physics.py` crashed with `KeyError: 'name'`: the sidecar writes the jiggle name as `Name` (C# member name). Reads both.
  - Clip import resampled the 60 Hz bake to the FBX's 30 fps (Titus 28 frames instead of 57 keys). The shared `sm2_anim_import`
    block (ue_import_anim.py, ue_anim_survey.py, ue_sani_fix.py) now sets `custom_sample_rate` from the `{model}.physics.json`
    `baked[].rate` for baked clips: Titus re-import → 56 frames / 57 keys (`Tests/physics/cc_titus/Anims60`).
    The `Anims` imports in `Tests/physics/*` are the 30 fps ones from before the fix.
  - `ue_cap_lit.py fit`: frames the whole skeletal mesh from its imported bounds. The actor bounds cropped the constructors to shin..shoulder.
- **Open:**
  - Calgar cape shows a diagonal red/white stripe pattern in UE. It is already in the no-physics bulk capture
    (`Captures\bulk\cc_calgar`), so it is a material/texture issue, not physics. Not investigated (REA the cloak material next).
  - A few spiky triangles on the Calgar cloak hem late in the clip (t≈0.9). Cloak has 1133 free particles but helpers are capped at 1024,
    and thin-strip clusters swing up to ~180° locally (polar fit on near-collinear particles). Matches the ≈74 % figure; not fixed.
  - Baked clips are still not loop-seamless; no in-game or browser side-by-side yet.

## vfx3 part 4: particle shader bytecode (Grok Bot, 2026-10-27 05:15 KST)

Notes only. No code changed this round.

**Where the shaders are**
- `default.pak` → `shader_cache/prebuild/pc/*.sdc`. Relevant files:
  - `common_pc_dx12_particles.sdc` (CPU sprites)
  - `common_pc_dx12_particles_gpu*.sdc`
  - `common_pc_dx12_trail.sdc`, `common_pc_dx12_lightning_trail.sdc`
  - `pc_dx12_sfx.sdc` (mesh particles: SFX_* tints/gradients)
  - `pc_dx12_combine_material.sdc`, `pc_dx12_distort.sdc`
- PC copy: `Desktop\SM2_Work\Raw\shaders_vfx\` (extracted with `SM2Extractor_e4ceb7d -f *particles*.sdc ...` from `D:\Steam\steamapps\common\Space Marine 2\client_pc\root\paks\client\default\default.pak`).
- Box copy: `/workspace/vfxsh/*.sdc`.

**How to unpack and read them**
- Unpack: `python3 /workspace/mat/sdcx.py X.sdc outdir` gives `NNNNN_k.dxbc` (DXIL containers, zlib records).
  - Box output: `/workspace/vfxsh/dx_*`.
  - `particles_gpu.sdc` (30 MB) has a different header and sdcx.py fails on it; not unpacked.
- Disassemble with names (cbuffer layouts, resource names): `LD_LIBRARY_PATH=/home/box/tools/dxc/lib /home/box/tools/dxc/bin/dxc -dumpbin f.dxbc`.
  - Output: `/workspace/vfxsh/ll_*`.
- Readable GLSL: `/home/box/tools/dxil-spirv/build/dxil-spirv f.dxbc --glsl --output f.glsl`.
  - Output: `/workspace/vfxsh/gl_*`.

**Sprite pixel shader**
- Reference shader: `particles 00114_2` (lit color PS). Per-emitter data is `PART_EMITTERS_SHARED_DATA`, a float4 buffer with 6 float4 per emitter, indexed by `TEXCOORD_2.x & 0xffff`.
- CPU-side mirror: a global array at `0x14400c7ec` with stride 0x60 and 6144 entries (init at `FUN_140175824`, upload near `0x141854211`). The packer has not been found yet.
- Shared-data slots, read from the shader:
  - `[1].rgb` = contrastColorKey in raw 0..255; `[1].w` = additionalContrast. Inferred from value ranges, but very likely.
  - `[0].z` = factor t, unconfirmed (see the formula below).
  - `[0].x` = alpha-mask softness.
  - `[2].x` = alpha-mask distortion amount.
  - `[3].x` = flags: 8 = distortion texture, 16 = alpha mask, 32 = no base, 64 = lit, 1024 = soft alpha.
  - `[4].x` = final intensity multiplier.
  - `[4].y` = flowmap strength.
  - `[4].w` = colour-gradient row (int; <0 = none).
- Formula, exact from the shader (tex = flipbook lerp of 2 frames by `fract(TEXCOORD_11.z)`, V = vertex colour TEXCOORD):
  ```
  if (row>=0) { l = dot(tex.rgb, (0.2127,0.7152,0.0722))*tex.a; tex = gradTex(l, (row+0.5)/1024) * l; }   // PART_CPU_EMITTERS_COLOR_GRADIENTS, 1024 rows
  boost = 1 + additionalContrast * saturate(1 - length(key.rgb/255 - tex.rgb));                         // contrast fire core
  c = tex.rgb * V.rgb * boost;  a = tex.a * V.a;
  rgb = c + t*(t*V.rgb*tex.a - c);   // t = shared[0].z
  rgb += litTerm*a; rgb *= maskAlpha * shared[4].x * ...;  alpha out = a*mask (premultiplied-style)
  ```
- What t means is still open. If t = textureTintControl, then tint=1 means vertex colour × alpha only, and tint=0 means the full texture colour. Under that reading, `seq_fin_expl_g` with tint 0 would render blue (its B channel is the hot fire channel; checked visually, BC7 so no channel swap).
- Most likely `_g` textures are meant for the colour-gradient path (`applyColorGradients` defaults to True; corpus has False ×61 only). But no gradient data was found anywhere: there is no `colorGradient` key in any .sfx/.td, and `part_data_color_gradients` / `partEMITTER_CPU_COLOR_GRADIENT_REF` is a runtime registry.
- So the current tint=0 → luminance heuristic (0010) stays. The next step is to find the gradient source: `colorGradient` field at sfx_part +0x138 (serializer `FUN_1417c6988`, type `FUN_142746030`) and `colorGradientRef` (0x143645f38, xrefs `0x1417aebbb`...).

**Next steps**
1. Implement the contrast boost above in `EffectRenderer` SpriteFs: uniforms `uKey` (contrastColorKey/255) and `uContrast`.
   - It is safe now, but should start behind `SM2_VFX_CONTRAST=1` until it is checked against the grenade.
2. Decide what t is by finding the CPU packer: search for writes of stride 0x60 into `0x14400c7ec`, or into the per-frame copy around `0x141854211`.
3. Trails: `trail.sdc` has `CB_PASS_TRAIL`, `CB_ANIM_TRAILS`, `TRAIL_DIFF`/`TRAIL_DISTORT`/`TRAIL_NORMAL`/`TRAIL_ALPHA_MASK_TEX`/`TRAIL_DYNAMIC_MASK`, `TRAIL_DEGR` (degNear?), `TRAIL_FLATLIGHTING`.
   - Read the tc-scale and degradation code there (`/workspace/vfxsh/gl_common_pc_dx12_trail`).
4. VAT (`shader_blood_splash`): probably in `pc_dx12_sfx.sdc` (mesh particle VS with `CB_OBJ_MATERIAL_DATA`).
   - Find a VS that reads two textures with `floatParam6`/`floatParam7`-like frame counts (120 / 60). Still unsolved.

## vfx3c: additionalContrast smoke boost (Grok Bot, 2026-10-27 14:00 KST)
- Box rebuilt from scratch today: .NET 8.0.427, Temurin 25 `/home/box/tools/jdk25`, Ghidra 12.2 `/home/box/ghidra`, rizin 0.9.1 `~/.local/bin`,
  dxc v1.9.2609 `/home/box/tools/dxc`, dxil-spirv built at `/home/box/tools/dxil-spirv/build`. Ghidra re-analysis of sm2_retail.exe running into
  `/workspace/rea/ghidra_proj2.gpr`, **program name now `sm2_retail.exe`** (log `/workspace/rea/ghidra_analyze.log`).
- **REA evidence:** strings `"additionalContrast"` @0x143645248, `"contrastColorKey"` @0x143645260 (next to `"textureTintControl"` @0x143645278).
  sfx_part typeParams serializer (0x14178d437 / 0x14178d48b): additionalContrast = float +0x12C (`FUN_142745110`), contrastColorKey = colour
  +0x130 (`FUN_1427456a0`). Same visuals block as textureTintControl, so read from `visuals.*`.
- **Code:** `EmitterDef.AdditionalContrast` (default 0, clamped >= 0) and `EmitterDef.ContrastColorKey` (raw RGB 0..255 from [A,R,G,B]; alpha dropped
  because the PS reads `PART_EMITTERS_SHARED_DATA[1].rgb` only). `EffectRenderer` SpriteFs: `uKey` (= key/255), `uContrast`,
  `tex *= 1 + uContrast * saturate(1 - length(uKey - tex))` — the particles.sdc PS formula from vfx3 part 4.
  Opt-in: `SM2_VFX_CONTRAST=1` (`EffectRenderer.ContrastEnabled`); off = unchanged output.
- **Heuristics:** the boost is applied to the tint-mixed texture (after the textureTintControl luminance heuristic) and before vertex colour/exposure;
  the gradient path (applyColorGradients) that precedes it in-game is still missing, so results on `_g` flipbooks are approximate.
  Shared-slot mapping ([1].rgb key, [1].w contrast) is still inferred from value ranges (vfx3 part 4).
- Tests: `ContrastTests` (3). SM2.Assets 116, SM2Extractor 42, LibSaber.Tests 5 — all pass.
- **sdcx.py recreated** at `/workspace/mat/sdcx.py` (outside the repo): scans zlib records, carves DXBC containers by header size, writes
  `NNNNN_k.dxbc`. The header/index is not decoded (heuristic); record numbers may differ from the lost original. It now also unpacks
  `particles_gpu.sdc`. Box results: particles 140 rec/541 containers, particles_gpu 385/832, trail 311/949, distort 6/24, sfx 661/1943
  in `/workspace/vfxsh/dx_*`; dxc -dumpbin and dxil-spirv read them (resource bindings show PART_EMITTERS_SHARED_DATA t8 and
  PART_CPU_EMITTERS_COLOR_GRADIENTS t12, space2).
- **Still needed:** browser capture of the grenade with SM2_VFX_CONTRAST=1 vs off, and an in-game comparison, before turning it on by default.
- Workflow notes: the user (2026-10-27) allows heavy PC work during gaming if it doesn't disrupt (hidden + idle). Push agent now `bc-65aed255-5102-5266-bcbd-f60b85b14984`.

## 2026-10-27 14:35 KST — REA tool = morluto/rea (install + tests)

- User: the REA tool for SM2 is **https://github.com/morluto/rea** (rea-agents 6.8.0; CLI `rea`, stdio MCP `rea mcp`, 229 tools). The old `user-rea` plugin note is obsolete.
- Installed on the box: clone `/home/box/tools/rea`, CLI `/home/box/tools/rea-cli/node_modules/.bin/rea`, private Node 24.22.1 `/home/box/tools/node24`, Ghidra **12.1.4** `/home/box/tools/ghidra_12.1.4_PUBLIC` (REA rejects 12.2). `/home/box/ghidra` 12.2 and `ghidra_proj2` untouched.
- Tests: doctor healthy with 12.1.4 + JDK 25; `/bin/ls` decompile OK (~15 s); `sm2_retail.exe` via existing-project reuse → `provider_timeout` at 120 s (project not yet saved by the running 12.2 analysis).
- Next: give REA a long `REA_GHIDRA_STARTUP_TIMEOUT_MS` (e.g. 6 h) or prepare a 12.1.4 project for reuse after the current 12.2 job ends. Details/MCP config: `docs/handoff/WORKFLOW.md`.

## 2026-10-27 (KST) — vfx3d: trail shader, shared-data packer, VAT search

**Trail VS (proven, `pc_dx12_trail.sdc` 00001_1, CB_PASS_TRAIL = cb4):**
- d = |vertex − camera| (cb1[3]). near = saturate((d−DEGR.y)·DEGR.x), far = saturate((d−DEGR.z)·DEGR.w).
- colour k = lerp(1, OFFSET.z, far)·lerp(MISC.w, 1, near); width = lerp(MISC.y,1,near)·w·lerp(1,MISC.z,far);
  rgb *= saturate(d²/(MISC.x+1e-4)); alpha = a·k·(width/max(width,pixelMin)).
- UV = perPointUV + MAPPING.zw + rot(...)·MAPPING.xy → texcoord scale DOES apply to trails.
- Implemented: TrailDef.TexScale (`transform.scale`, behind `SM2_VFX_TRAIL_TC=1`), DegNear/DegFar per-vertex fade
  (width/rgb/alpha; scale inside radStart for near, beyond radEnd for far), honours `EffectSprites.DistanceFades`.
- HEURISTIC: mapping of sfx JSON fields onto DEGR/MISC/OFFSET slots (not traced on the CPU side).

**PART_EMITTERS_SHARED_DATA packer (proven):**
- Init 0x140175820 ("part_cpu_data_shared"), buffer 0x14400c7ec stride 0x60, counter 0x14409c7ec, CS 0x14409c7f0, dirty 0x14400c4e8.
- Slot allocator 0x1417e4529..0x1417e47b3 calls packer FUN_1417e1e50 (call 0x1417e473e); upload memcpy 0x90000 @ 0x141854209.
- Packer: [0].z ← desc+0x4e8 (0x1417e21d1/0x1417e21d7) = typeParams(+0x3a8)+0x140 = **textureTintControl**;
  [1].xyz ← desc+0x4d8/4dc/4e0, [1].w ← desc+0x4d4 (0x1417e20d6..e6) = key/contrast.
- Exact shader formula (t=tint): hdr = c0 + t·(t·vColor.rgb·tex.a − c0), c0 = tex.rgb·boost·vColor.rgb.
  Behind `SM2_VFX_TINT_EXACT=1` because the gradient path is still missing.

**VAT:** not in `pc_dx12_sfx.sdc` (574 VS scanned; TEXCOORD1 + 2-texture VS are wind/foliage). Likely another .sdc. Open.

## 2026-10-27 (KST) — vfx3e: VAT blood-splash VS found, sprite corner math, leads

**VAT VS (proven):** in `pc_dx12_combine_material.sdc` (box unpack `/workspace/vfxsh/dx_pc_dx12_combine_material/`),
records 00000_0/00000_1/00001_1/00001_2 (VS; inputs V_POSITION int4, V_TEXCOORD0, V_TEXCOORD1, SV_InstanceID).
Exe permutation string `COMBINE_MATERIAL_SHADER0_BLOOD_SPLASH`. Not in pc_dx12_sfx.sdc (prior guess wrong).
- uv1 *= cb7[1].w; frame: flags&25==25 -> animStage·(frames−1) (combineMtlAnimControl); else per-object anim
  (start/len/rate, clamp or fract, optional reverse) or free-running frames·frac(seed/frames) + fp6·time (cb0[23].y).
- f0=floor(f), t=frac; v(f) = bands<2 ? −f/frames : uv1.y − frac(f/frames)/bands; samples rows f0 and f0+1, lerp t.
- pos = fp0..2 + rgb·255·fp3..5 (8-bit only; the `_d` texture is NOT read by the VS); normal = rgb·2.007874−1.007874.
- Then instance 3x4 transform. cb3 rows: [6].xyz min, [6].w/[7].xy scale, [7].z rate (fp6=120), [7].w frames (fp7),
  [8].z bands, [18].x pos tex, [19].y normal tex. HEURISTIC: row 6 = floatParam0 (inferred from matching values).
- Why earlier decode failed: rows are per-frame (1 texel row = 1/(frames·bands)), counted *downward from uv1.y*,
  and the shipped pct is downsampled (471 vs ~952 rows), so frames must be sampled with bilinear v, not band rows.
- Code: `Effects/VatDecode.cs` (pure helpers + tests). Not yet wired into the renderer.

**Sprite quad (proven shader-side, common_pc_dx12_particles 00024_0):** corners are ±1 (const arrays −1,−1,1,1 / −1,1,1,−1)
times per-vertex sizes V_TEXCOORD.y (x) and |V_TEXCOORD.z| (y), plus pivot offset 0.99608−2·colorAlpha.
So the size the CPU writes is a half-extent; whether CPU writes size or size/2 is NOT traced (open).

**degNear on sprites:** sprites are CPU-expanded; no shader proof. Data (degNear scaleAlpha 0, 2→20) only makes sense
with the trail-VS semantics, so the fix is behind `SM2_VFX_DEGNEAR_FIX=1` (HEURISTIC).

**Leads (not resolved, Ghidra headless died OOM before save — log line 326 — so rizin only):**
- Field-name string xrefs (all are JSON (de)serialisers, not runtime use): ratioDependency 0x143645320 (refs 0x141790409…),
  ratioXY 0x143644f28 (0x141786eb1…), blend_add 0x14363ab78 (0x141751d81, 0x14178e041…), genOnFly 0x143645d38 (0x1417acdc6,
  0x1417ae934 writes to [rsi+0x9b0]…). Next: follow these desc offsets into the runtime like the shared-data packer.
- exposure 1/250, light ×100, distortion strength: not traced this pass.

## 2026-10-27 (KST) — vfx3f: VAT hooked into the browser (SM2_VFX_VAT=1)
- Reader: `TemplateMeshReader` now keeps UV set 1 (`ExportedMesh.Uvs1`, × submesh UvScaling[1], no flip — HEURISTIC,
  mirrors the VS `uv1 * cb7[1].w`). EffectMesh vertices are pos/normal/uv/uv1 (stride 40, attrib 6 `aTex1`).
- `VatDecode.Parse(td)` reads floatParam0..7(,10) and the `*anim_data*` / `*anim_data*_n` textures from shader_blood_splash
  materials (HEURISTIC key/name matching; bands default 8). `EffectMeshMaterial.Vat` carries it.
- Mesh VS: with `SM2_VFX_VAT=1` and the position texture loaded, it reproduces the combine_material VS decode
  (two rows f0/f0+1, lerp, pos = min + rgb·255·scale, normal = rgb·2.007874 − 1.007874); textures via `LinearTexture` (units 3/4).
- Frame = `StageFrame(particle normalised age, frames)`, i.e. animStage := age/life — HEURISTIC (CPU source of animStage untraced).
- Not verified visually (no splash assets on the box); `EffectRenderer.VatDraws` counts VAT draws for checking.
  Possible mismatch: reader bakes node transforms into positions, VAT positions are object-local.

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

## 2026-10-27 14:55 KST update — Unreal MCP switched to EngineWire (Cursor Agent)
- Installed **EngineWire / MCP Automation Bridge v0.5.30** (latest stable; https://github.com/ChiR24/Unreal_mcp, MIT). 0.6.0-beta-f (first release actually named EngineWire) is a prerelease and was skipped.
- No prebuilt binaries are released; built with `RunUAT BuildPlugin` (UE 5.7.4, VS 2022 17.14, 3 min 36 s, success) in `Desktop\SM2_Work\enginewire\built`, installed to `K:\Unreal Projects\UE_VFX_57\Plugins\McpAutomationBridge`. `.uproject` unchanged (plugin is EnabledByDefault).
- Native MCP HTTP on `127.0.0.1:3000/mcp` with capability token (`DefaultGame.ini`, backup `DefaultGame.ini.bak1`; token file `C:\Users\Revaptor_FX\.enginewire_token`, not in git).
- Verified after editor restart: 23 tools listed, `execute_python` listed 5164 assets under `/Game/SM2_Work`, wrong token → 401, `uerun.ps1 ue_verify_layout.py` runs via EngineWire in 1.6 s (UEMCP: 42 s).
- New `tools/ue/enginewire_call.py` and `tools/ue/uerun.ps1` (EngineWire first, UEMCP fallback). UEMCP stays installed; `Scripts\uerun_uemcp.ps1` / `uerun.ps1.bak1` keep the old runner. Node server 0.5.30 also installed in `enginewire\server` (optional).

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

## vfx3m (2026-10-27 KST) — particle blend flags 0x20/0x400 (PROVEN)
- Chain: FUN_1417e1e50 builds 0x60 B/emitter record → FUN_1417e4420 copies to 0x14400c7ec+id*0x60 (dirty _DAT_14400c4e8|=1) → FUN_1418541b0 uploads 0x90000 B as t8,space2 PART_EMITTERS_SHARED_DATA (pc shaders index id*6+row). Row3.x = flags.
- bit 0x20 = blendType(+0x48c)==add; bit 0x400 = !(isBlendAlphaMultiply(+0x490) && blendType!=add).
- particles.sdc PS 00000_0: rgb *= vertexAlpha*(0x400 ? 1 : texAlpha'); outAlpha = 0x20 ? 0 : vertexAlpha*texAlpha'. blend_add has no bit (= blend).
- Implemented `EffectBlendRea` + `visuals.isBlendAlphaMultiply` parse; opt-in `SM2_VFX_BLEND_REA=1` (PSO ONE/INV_SRC_ALPHA is HEURISTIC, implied by add alpha=0). Tests: BlendReaTests.
- Open: other rows of the 0x60 record, distortion.power/ratioXY via GPU curve slots inst+0x200..0x250, trail DEGR CPU packing.

## vfx3n (2026-10-27 KST) — bench check of SM2_VFX_BLEND_REA + 0x60 record field map
- Bench (`--vfxbench`, llvmpipe, box): `/workspace/browser_run_shots/vfx3m_blend/{off,on}/`, `*_cmp.png` (OFF top / ON bottom).
  frag grenade mean |Δ| 1.02/255 (max 186): sparks and smoke much brighter/greyer with ON (non-alphaMultiply emitters no
  longer scale RGB by texture alpha). hierophant_bomb_ee: no change (mesh-dominated). Plausible per REA, but smoke looks
  overbright → keep opt-in until an in-game reference. Data: isBlendAlphaMultiply True in 3,373 of 15,168 emitter entries.
- Caveat: earlier HEURISTIC (vfx3l) that blend_add picks the SFX_BLEND_ADD_NO_ALPHA permutation (FUN_142450650) is NOT
  modelled by EffectBlendRea (flags-only; blend_add = blend there).
- Visuals parser FUN_14178ce70: visuals base = desc+0x3a8 (anchors: blendType +0xe4→0x48c, additionalContrast +0x12c→0x4d4).
  Field offsets (desc): lighting 0x478, domainLightingTessDensity 0x47c, lightingTranslucency 0x480, lightsSensitivity 0x484,
  ambientLight 0x488, blendType 0x48c, isBlendAlphaMultiply 0x490, usePixelFog 0x491, alphaIntensity 0x4d0, gloss 0x500,
  angleAlpha.isEnabled 0x560 / alphaSpline 0x568, alphaMask 0x578, sdr 0x628, projection {isEnabled 0x6d0, attenuation 0x6d4,
  fadeVerticalEdges 0x6d8, dist 0x6dc}, temporalAA 0x6e0.
- 0x60 record (FUN_1417e1e50 0x1417e1e50.., PROVEN CPU side): r0.x alphaMask-derived (+0x620, if alphaMask on); r0.y 1/max(|x|,1e-6)
  of desc+0x3a0 / +0x1b0; r0.z textureTintControl; r1.xyz contrastColorKey, r1.w additionalContrast; r2.x alphaMask-derived;
  r2.y domainLightingTessDensity; r2.z k/projection.dist; r2.w projection.attenuation; r3.x flags (0x20 add, 0x400 !alphaMul,
  plus lighting/gloss/sdr/alphaMask bits); r3.y flags2 (0x10 ← desc+0x8f); r3.z lightingTranslucency; r3.w fadeVerticalEdges
  (clamped); r4.x lightsSensitivity; r5.xy temporalAA.
- GPU side (pc 00000_0): reads r0.x (min with 1-texA, alpha-mask softness), r0.z, r1.xyzw, r2.x, r3.x, r4.x, r4.y, r4.w.
  r4.x multiplies the whole non-additive colour (line ~800) — semantics as "lightsSensitivity" HEURISTIC (could be a lit
  permutation); not implemented. r4.y/r4.w are written outside the decompiled window (not yet attributed).
- Not done this chunk: distortion.power/ratioXY via GPU curve slots, trail DEGR CPU packing.

## vfx3o (2026-10-27 KST) — curve slots, row 4 writers, softDepth reciprocal
- FUN_1417e29b0 (0x1417e29b0..0x1417e2b4f) "curve slots" are tagged resource pointers (ptr | kind<<48), i.e. texture
  bindings, not curves. default: inst+0x200 ← desc+0x3a8 (visuals.texture; kind 1 iff payload+0x1fa set), +0x210 ← 0x580
  (alphaMask tex, kind 3), +0x220 ← 0x630 (sdr), +0x230 ← 0x508 (gloss), +0x240 ← 0x270, +0x250 ← 0x310.
  distortion (type 1): only +0x240 ← desc+0x1b8 (distortion visuals.texture, payload+8). So distortion.power is NOT here.
- Correction to vfx3n: r0.y = 1/max(|softDepth|,1e-6) and flag 0x200 = softDepth<0 (distortion: payload+0 = softDepth, parser
  in vfxh_1.c ~2190; default +0x3a0 is the same field by analogy — HEURISTIC). The reciprocal is NOT exposure. Already
  implemented as soft particles (EmitterDef.SoftDepth).
- r0.w (+0xc) = count from desc+0x29c (distortion) / +0x738 (default, enabled by +0x734); r4.z (+0x48) = half2 of the
  normalised direction (+0x73c/+0x744 or default DAT_143638ad8) when count ≤ 1.
- r4.y (+0x44) = lookup(desc+0x318 name)+0x34 (fallback +0x1c) × desc+0x398, only if desc+0x308 (FUN_1417e1e50 ~0x1417e2333).
- r4.w (+0x4c) = colour-gradient LUT row (desc+0xa04 & 0xfffff) if visuals.applyColorGradients (desc+0x430), else -1.
  PS 00000_0 ~438–450: lum = dot(texRGB, Rec709)·texA; colour = gradLUT(u=lum, row)·lum. PROVEN, but
  applyColorGradients is False in all 61 occurrences in our sfx dump → not implemented (zero impact).
- distortion.power (payload+0x98 = desc+0x248, FUN_14177c520 parser): DispScan +0x248 in 0x1417b0000–0x141a00000 found a
  0/1-tagged scalar at desc+0x240/0x248 sampled from a 64-entry LUT in FUN_1417f0c60 → particle+0xe4, but the tag at 0x240
  overlaps distortion localOffsetX (payload+0x90), so it belongs to another typeParams member. Unresolved.
- Trail DEGR: the name handles 0x145025180/1a8/220/2c0 have only the registration WRITEs in FUN_1421ca290 (DataRefs);
  the packer reads them indirectly. Dead end statically.

## vfx3p/q (2026-10-27 KST) — distortion.power + trail DEGR: no progress (stop rule hit)
- distortion.power (desc+0x248): DispScan +0x248 over 0x141a00000–0x142600000. Float readers: 141e28110/141e28b00/141e29eb0/
  141e2c500 (unrelated objects), 1424540e0 (material UV-transform packer: 0x214/0x2ec/0x3bc/0x248 = 4 texture-transform
  slots of a material, not an emitter), 1423ba150. FUN_1417f0c60 (vfx3o) is gated by FUN_141a98e80 and loops desc+0x208/
  0x218/0x228 → lwiGeom member, not distortion. The distortion fullscreen shaders (dx_pc_dx12_distort) only read CB_COMMON.
  No distortion-power consumer found statically; mapping stays HEURISTIC.
- Trail DEGR: DataRefs over 0x145025000..+0x300 → only the WRITEs in FUN_1421ca290; callers of FUN_1418c09a0
  (1418beed0, 1418bf500, 1418c1940) update trail points (0x50-byte stride), not the CB. DispScan of 0x1418b0000–0x1418e0000
  for +0xf0/+0x100 stores (MISC_2@240) gave no CB_PASS_TRAIL writer. Constants are likely set by handle through a
  generic setter, so a capture is needed (not allowed: no game/EAC). DEGR slot mapping stays HEURISTIC.

## vfx3r (2026-10-27 KST) — in-game side-by-side prep (no injection; user screenshots)
- Bench captures: `/workspace/browser_run_shots/ingame_cmp/{off,blend,tint,vat,all}/<effect>_sheet.png`,
  contact sheet `ingame_cmp_contact.png` (rows = effects, cols = off/BLEND_REA/TINT_EXACT/VAT/all+CONTRAST, d = mean |Δ| vs off).
  PC copy: `Desktop\SM2_Work\Captures\ingame_cmp\`. User reference shots go to `Desktop\SM2_Work\Captures\ingame_ref\`.
- Effects: part_mg_frag_grenade_expl, fx_bolter_muzzle_client, part_mag_chainsaw_blood_splash_red_01,
  fx_mgs_hierophant_bomb_ee, trail_stalker_bolter_smoke, part_mg_candle_smoke_hub.
- Bench caveats: muzzle/blood/candle are tiny at the fixed bench camera; chainsaw blood + candle miss textures
  (part_splash_drops_01…, flame_sml_16x8); hierophant sheet renders empty in this run and VAT shows d=0 (needs the tex8/mesh set
  used in vfx3g) — fix the bench framing before comparing those.
- GPU-capture route (user accepted EAC risk, 2026-10-27): no official no-EAC switch exists (official mod support = "mods
  detected" mode, private lobbies, EAC still on). Least-invasive offline route without modifying files: run
  `client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe` directly (what the Nexus Vortex extension's
  "launch without EAC — offline only" tool does) via RenderDoc 1.46 portable (`Desktop\SM2_Work\Tools\RenderDoc_1.46_64`,
  signature Valid). Not launched by the agent.

## vfx3s (2026-10-27 15:40 KST) — bench fixes + GPU-capture attempt result
- **GPU capture attempt FAILED (EAC relaunch), 15:33 KST.** `renderdoccmd capture` on
  `client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe` (Steam running): the injected process (PID 38920) exits
  immediately and the game relaunches itself via `start_protected_game.exe` (PID 91044) → new Retail.exe child (PID 59596)
  + `EasyAntiCheat_EOS.exe` (PID 96252, 15:33:36). RenderDoc never attaches to the real process; no `.rdc`, trigger log empty.
  The "run Retail.exe directly = offline no-EAC" route from vfx3r is **disproved** for this build. Game closed, no bypass tried.
  Remaining options are user-side screenshots (F12) into `Captures\ingame_ref\`.
- **Incident:** the launch command created and immediately deleted an empty `steam_appid.txt.check` in the game root
  (`D:\Steam\steamapps\common\Space Marine 2\`). Verified absent afterwards; no game file was modified. Agents must not
  write probe files into the game folder.
- Textures: `part_splash_drops_01`, `part_splash_central_01`, `part_rs_glow_01` (PC `Raw\vfx\tex3\pct`) and new
  `flame_sml_16x8`, `part_splash_mid_03`, `part_seq_plasma_expl_01` (+ `_vectors`/`_am`/`_nm`; extracted read-only from the
  paks with `SM2Extractor_e4ceb7d extract -f`) → PC `Raw\vfx\tex9\`, box `/workspace/vfx3_raw/tex9/pct/` (with the tex3 mips).
  Only `gpu_emitter_texture` (GPU emitter placeholder) and mesh `sfx_geom_debr_expl_stones_small` are still missing.
- `EffectCamera.Fit` (EffectTextures.cs): samples the whole capture window, ignores particles below y = −0.5 (no floor in the
  bench; HEURISTIC), centres on the median with the 25..75 % box ×1.6, min distance 0.15. Hierophant bomb was empty because
  falling debris (CPU_EMITTER_9, y ≈ −280 m) and stone02_big mesh chunks (x ≈ +33 m) set the frame; now the core renders and
  VAT shows d = 0.48 (was 0). `SM2_VFX_FITDBG=1` prints the fit + per-emitter means.
- Bench needs `SM2_VFX_MESHES=/workspace/vfx3_raw/mesh1` (the vfx3r run missed splashes_vtxa_lwi etc.). Script:
  `tools/vfxbench/ingame_cmp.sh`. Re-rendered `/workspace/browser_run_shots/ingame_cmp/` (old run kept as `ingame_cmp.bak1`),
  PC `Captures\ingame_cmp\` (old → `ingame_cmp.bak1`).
- Still open (not framing): fx_bolter_muzzle_client and part_mg_candle_smoke_hub build **0 quads** (3/9 particles alive, all
  filtered in EffectSprites.Build); part_mag_chainsaw_blood_splash_red_01 spawns **0 particles** in 0.5 s (covering-triggered?).
  Those three rows of the contact sheet are empty and not comparable yet.

## vfx3t (2026-10-27 15:50 KST) — the 3 empty bench effects
- **Muzzle + candle (0 quads): bench camera inside degNear.** `SM2_VFX_SPRDBG=1` (new, EffectSprites reject reasons) showed
  every quad culled with alphaK = 0: their sprite emitters have `optimization.degNear radStart 6 → radEnd 2, scaleAlpha 0`
  (alpha 0 at ≤ 2 m) and the tight small-effect frame put the eye at < 2 m. Not a format bug. Fix (bench only):
  `EffectCamera.NearFadeClear` keeps the eye at max(radStart, radEnd)·1.05 of any fading sprite emitter (cap 20 m) and
  narrows the FOV to keep the frame (`EffectCamera.Fov`). Now muzzle 1–3 quads, candle 1–9 quads per frame.
- **Chainsaw blood (0 particles): isVisible.** The source `drops_source2` is `isVisible = False` (life 0.001 s, genOnDie →
  cpu_drops_2 ×50, cpu_drops_3 ×2), and `EffectInstance.Step` skipped emission for hidden emitters.
  Exe: `isVisible` (string 0x143645df0) is emitter desc **+0x88** (parser FUN_1417adea0, next to emitScale/isUseParentRotate);
  DispScan +0x88 over 0x1417d8000–0x141900000: only draw-submission gates read it (FUN_1417f71b0 @0x1417f71c5 and
  FUN_1417ee5c0 @0x1417ee5f4 return when 0; FUN_1417e9480 @0x1417ea4b9 checks the parent's flag for transforms). No emission
  path reads it → hidden emitters still simulate (PROVEN for the draw gate; "no emission reader" = scan result).
  Fix: hidden emitters with attached children keep emitting (`EffectInstance.HiddenSpawn`, default on,
  `SM2_VFX_HIDDEN_SPAWN=0` = old behaviour); they still never draw. Test `HiddenSourceStillSpawnsAttachedChildren`.
  The blood effect now spawns 259 particles but still draws nothing — **correctly**: all 4 emitters (incl. cpu_drops_2/3 and
  em_covering_blood) are `isVisible = False` in the data, so the exe's draw gate skips them too. Its row stays empty by design
  (template/disabled asset; the name in fx_huge_blood_explosion etc. is only an sfx_group node).
- Re-rendered ingame_cmp (previous run → `ingame_cmp.bak2`), PC `Captures\ingame_cmp\` (old → `ingame_cmp.bak2`).
- VFX estimate ≈ 73% (bench coverage, isVisible semantics proven; no format change).

## vfx3u (2026-10-27 15:55 KST) — in-game reference comparison
- Refs (user, photo mode, 3840×2160): box `/workspace/ingame_ref/`, PC `Captures\ingame_ref\`. Side-by-sides:
  `/workspace/browser_run_shots/ingame_cmp/side_by_side_{grenade_early,grenade_late,bolter_muzzle,bolter_trail,blood_vs_hierophant}.png`
  (left = in-game crop, right = bench frames off/blend/tint/vat/all).
- 154731/154743 = frag grenade (fireball+sparks / smoke+sparks): **BLEND_REA matches best** (bright yellow-white additive core and
  spark streaks; off/tint/vat are dull, ~10× fewer bright pixels). Still missing vs game: the big orange fireball volume, smoke
  density/persistence (game smoke is thick and lit; ours thin and gone by ~1.3 s), spark count/length (game sparks are long streaks).
- 154807 = bolter muzzle flash: game shows a large orange flash + flame tongues; bench shows ~nothing in every config →
  muzzle is still broken (flash is probably the sfx_geom_muzzle mesh emitters, `meshes 0` in the log). Open.
- 154833/154843 = bolter tracer (bright yellow line) + muzzle smoke wisp: tracer is a different effect (bolt projectile trail);
  trail_stalker_bolter_smoke (pink thin quad) is not comparable. Muzzle smoke wisp matches candle-like soft smoke qualitatively.
- 155018 = chainsaw execution blood (dark red, ~RGB 110/30/25, volumetric strands): not one of our bench effects
  (part_mag_chainsaw_blood_splash_red_01 is all-hidden). Nearest = hierophant VAT splash: shape type matches (strand splash) but
  ours is far too bright/orange (RGB ~156/115/53 incl. glow) → blood colour/lighting is wrong in all configs. Open.
- Tint/VAT/CONTRAST: no visible evidence either way at these references (d ≤ 0.5). Keep opt-in.
- **Default change:** `SM2_VFX_BLEND_REA` default ON (`=0` disables); test `DefaultOnUnlessDisabled`; ingame_cmp.sh sets
  `=0` for the off/tint/vat columns so the sheets stay comparable.
- VFX estimate ≈ 74%.

## vfx3v (2026-10-27 16:10 KST) — in-game gaps
- **Muzzle flash "missing" = capture sampling, not format.** fx_bolter_muzzle_client: 11 flame emitters live 0.030–0.045 s
  (SM2_VFX_SPRDBG `quad` lines), i.e. <3 steps at 60 Hz, and the 2 s/8-frame sheet first samples t=0.125 s. New
  `SM2_VFX_STEP_HZ` (default 60) + `name@0.08` at 480 Hz shows the flash (flame_circle/cross/front, blue_smoke, 2 meshes).
  Remaining diffs: core is blue-white (blue_smoke_01 rgb 70/252/555 Add) vs orange in game; flame_circle_1/2 mesh
  emitters have no texture (`tex ''`) and draw as grey columns → mesh material for sfx_geom_muzzle still unresolved (open).
- **Grenade fireball = same cause.** main_core_01..03 + flash_01 exist only for t < ~0.18 s; first 3 s-sheet frame is 0.1875 s.
  At 480 Hz/@0.32 the fireball is there; blend/all match in-game colour+size best (off/tint/vat too pale).
- **Smoke thin:** smoke emitters are lit ('Medium/Best Quality'); bench has no scene light and uses heuristic
  `LitScale`=100. SM2_VFX_LIT=400 gives in-game-like grey smoke. Not defaulted (heuristic, needs scene light REA).
- **Blood:** the earlier "too orange" was the hierophant *bomb*, wrong reference. part_mag_chainsaw_blood_splash_red_01 has
  all 3 emitters isVisible=false (+0x88, parser FUN_1417adea0) → presumably made visible by gameplay (HEURISTIC, toggle
  not traced). Debug `SM2_VFX_FORCE_VISIBLE=1` draws them: dark red (rgb ~30,0,0 lit) matches the in-game colour; shape is
  too blobby/large (cpu_drops_3 size ~19 m at n 0.84 — size curve or scale suspect, open).
- ingame_cmp.sh now also renders `<col>/early/` (480 Hz muzzle/grenade, force-visible blood) and runs
  tools/vfxbench/side_by_side.py. Debug `quad` lines in SPRDBG. VFX ≈ 77%.

## vfx3w (2026-10-27 KST)
- Muzzle mesh: tpl part Variant (shadingMtl) -> named td block (mzl_geom in water_drops_01.td: preset sfx, tint [255,237,72,18], emissive 111, softFreshnel power 5). EffectMeshes.Material now reads named block tex/tint/softFreshnel (SM2_VFX_MESH_MTL=0 disables). Fresnel alpha in mesh FS is heuristic. Texture "phantom_hands" not extracted -> white base. Open: flame_cross_4/6/8 grey columns (rgb ~26000 saturates).
- Hidden blood emitters: part_mag_chainsaw_blood_splash_red_01 is a template (isVisible=False); visible copies are part_mag_chainsaw_*_finisher_01 (nameParent sfx_source_pt) and parent emitters2 overrides (fx_huge_blood_explosion). Bench/side-by-side now uses warrior_finisher_01; FORCE_VISIBLE dropped there.
- Blood size: emitScale = emitter desc+0x70 (FUN_1417adea0), used in FUN_1417ecc30/1417f24a0/1417f3f60 as emission-count/threshold, not size (inconclusive) -> size unchanged. alphaMask.clip.power parsed (FUN_141782a60, desc+0x88) as EmitterDef.MaskClip, opt-in SM2_VFX_MASK_CLIP=1 (no visible change).
- Finisher mesh colour: sfxMtlOverrides color constExpr [255,44,3,155] + emissiveIntensity spline 10.8->0 -> renders bright red; in-game darker (scene lighting/exposure). Open.
- Lit smoke scene light: not traceable (runtime scene/sun); LitScale=100 stays heuristic (SM2_VFX_LIT=400 closer).

## vfx3x (2026-10-27 KST)
- phantom_hands (BC7A 2048², wispy smoke) + part_muzzle_side_01 extracted read-only (SM2Extractor_e4ceb7d extract -f) → PC `Raw\vfx\tex10\`, box `/workspace/vfx3_raw/tex10/`. Muzzle rings now textured: wispy and much dimmer than the old white-base orange rings; in-game glow likely from bloom (mzl_geom bloomIntensity 111, adaptiveIntensity), not simulated. UV scroll (overrideAffixScroll speedY 11) not applied. Open.
- Grey columns = flame_cross_6/8 (emitDir x+/x-, blend+isBlendAlphaMultiply, colorIntensity 55555, alphaClip age 0→1). Root cause: alpha-clip math. particles.sdc 00000_0 (vfxsh_glsl/pc/00000_0.glsl l.118-122, 466, 873-876): clip = flag0x400 ? 0 : TEXCOORD_2.y; ac = sat((tex.a*vertexA - clip)/(1 - 0.5*clip)); rgb *= sat(ac + flag0x400); SV_Target.w = add ? 0 : ac. Vertex alpha (~0.08) is inside the clip, so these burn out almost immediately. Implemented in sprite FS (BLEND_REA path), default on, SM2_VFX_CLIP_EXACT=0 disables. Same shader writes HDR unclamped (NaN/Inf guard only) → blending is in HDR; our per-fragment tonemap stays. SM2_VFX_HDR_PREMUL=1 (opt-in, HEURISTIC) tonemaps the premultiplied contribution instead; it over-brightens, left off.
- flame_cross_4 (nameParent "tip", isEmitByNormal, isAlignSpeed) streaked +Y with no host socket. EffectInstance.SocketForwardFallback (default on, SM2_VFX_SOCKET_FWD=0 disables, HEURISTIC): the effect root stands in for the socket → local +Z. Flips the bench muzzle direction on screen.

## vfx3y (2026-10-27 KST)
- Mesh UV scroll: sfx `overrideAffixScroll {override, speedX, speedY}` parsed by FUN_1425fd580 (shader desc +0xd8 speedX, +0xdc speedY; serializer FUN_1425ff110; strings @1437ffc18). EffectMeshMaterial.Scroll; mesh FS samples uv + scroll*time (EffectLayer.Time = effect time). Units UV/s HEURISTIC. Default on, SM2_VFX_MESH_SCROLL=0 disables. mzl_geom: speedY 11. Evidence output /workspace/rea/vfxy_1.c.
- Bench bloom (opt-in SM2_VFX_BLOOM=1, HEURISTIC, not the game's post chain): second mesh-only pass weighted min(1, bloomIntensity*SM2_VFX_BLOOM_GAIN[0.01]), 3x box blur radius SM2_VFX_BLOOM_RADIUS[10] px, added x SM2_VFX_BLOOM_STRENGTH[1.5] (Bench/BenchBloom.cs). Only meshes feed it (sprites have no bloomIntensity read yet). ingame_cmp.sh "all" column enables it. Muzzle rings now give a warm orange glow, closer to in-game; slight red haze over the whole tile from the large ring meshes.

## vfx3z (2026-10-27 17:15 KST)
- z1 Bench bloom: sprites now feed the bloom pass; only light above a bloom point blooms (BenchBloom, SM2_VFX_BLOOM_POINT
  default 0.45, HEURISTIC). Evidence: engine bloom is a scene HDR threshold bloom ("Bloom.BloomPoint" @0x14363dbf0,
  "COMMON_HDR_BLOOM_POINT" @0x1436e9f08); sprite emitters have no bloomIntensity (only sfx mesh materials, @0x1436446f0).
  The dim red haze from the big muzzle ring meshes is gone; the core keeps a warm orange glow.
- z2 Finisher blood: combine_material is a deferred G-buffer shader (dxil-spirv of pc_dx12_combine_material.sdc →
  /workspace/vfxsh_glsl/combine/, perm 00001_0: 5 RTs, instance colour /255 + sRGB→linear polynomial, emissive RT4 =
  CB_COMMON[22].x·mtl[8].x·material emissive, not the sfx tint). Brightness = scene light, so lit (non-sfx) meshes get
  bench light EffectRenderer.SurfLight 60 (was 200; SM2_VFX_SURF_LIGHT, HEURISTIC). Blood red R 186→111 vs ref ~107.
  Still more saturated than in-game (ref G/B ~60/44 from scene ambient/fog).
- z3 LitScale 100→400 (SM2_VFX_LIT). Lit particle shaders read only runtime light (COMMON_SUN_INTENSITY @0x1436e9ed0,
  COMMON_SKY_SH_HARMONICS @0x1436eb790, light grid) × COMMON_HDR_EXPOSURE_DIV_AVG_LUM @0x1436e9ee8; no per-effect
  constant exists. Value from the in-game refs: HEURISTIC.
- z4 Distortion: particles.sdc 00005_1 PS: o = (tex.rg·2.007874−1.007874)·TEXCOORD.w, mask = sat(10·(|o.x|+|o.y|)),
  RT = mask·(o·2+0.5); VS 00001_1 passes V_TEXCOORD.w through. Implemented (SM2_VFX_DISTORT_EXACT=0 = old radial edge).
  Power→V_TEXCOORD.w and the resolve scale (SM2_VFX_DISTORT 0.04) stay HEURISTIC.
- z5 Trail DEGR: CB_PASS_TRAIL member strings @0x14378f170..0x14378f288 have 0 code refs (ghidra_proj3 DataRefs) → CB
  filled by fixed layout. No new evidence; mapping stays HEURISTIC.
- z6 7 side-by-sides (side_by_side.py now also writes blood_vs_hierophant, bolter_smoke). Box old run →
  ingame_cmp.bak7; PC Captures\ingame_cmp old → next .bakN.
- Patches /workspace/patches/vfx3z/. VFX ≈ 79%.

## vfx4-0 (2026-10-27 17:30 KST) — roadmap change
- User removed Niagara conversion from the roadmap; it is kept only under ROADMAP → "Possible future development". AGENTS.md rule 6, STATUS, WORKFLOW updated.
- New order: vfx4-1 VFX heuristic reduction (finisher blood saturation, distortion power/0.04, trail DEGR), vfx4-2 UE export test (no Niagara) into /Game/SM2_Work, vfx4-3 browser viewer polish.

## vfx4-1 (2026-10-27 17:50 KST) — VFX heuristic reduction (Ghidra ghidra_proj3)
- Outputs /workspace/rea/vfx4_1.c (DispScan 0x248), vfx4_2.c (DecompAt strings), vfx4_3.c (DispScan trail offsets).
- **distortion.power → vertex alpha: PROVEN.** FUN_1417eeed0 writes colour (0,0,0,power(age)) for distortion emitters; define
  "DISTORTION_SCALE_CONST_MUL_VC_ALPHA" @0x1437f5a40. Current code already matches (r*alpha with alpha=alphaK); comment updated.
- **0.04 resolve strength: still HEURISTIC.** Only the CB names were found (CB_PASS_DISTORTION_POSTPROC @0x1437133f8, layout FUN_1421aa940).
- **Trail DEGR: still HEURISTIC** for the slot mapping. New proven desc offsets (degNear +0x3e8, degFar +0x400, degradationRadius +0x418,
  parser FUN_14183a330) and the desc→instance copy FUN_14187f1e0; the CB pack is still not traced.
- **Finisher blood saturation: still HEURISTIC** (scene ambient/fog; no exe evidence). SurfLight 60 unchanged.
- No behaviour change, so no new env flag. VFX ≈ 80%.

## vfx4-2 (2026-10-27 18:00 KST) — Unreal export test (no Niagara)
- PC exports (SM2Extractor_9474df0, fbx-unreal) → `Desktop\SM2_Work\Exports\ue_export\`: grenade, muzzle group (no own textures;
  textures from donors fx_cine_bolter_muzzle + part_mgs_cine_dread_window_chaos for phantom_hands), trail smoke, finisher blood,
  tpl sfx_geom_muzzle + splashes_vtxa_lwi. Texture list = the bench SPRDBG `tex` names + sfx nameTex.
- `tools/ue/ue_vfx_export_test.py` (import / capture / export, separate uerun calls; uerun args are ONE comma list, script path absolute):
  imports into `/Game/SM2_Work/VfxExport` (existing → `_bakN`; _bak1/_bak2 are earlier test runs), masters M_SM2Vfx_Add / M_SM2Vfx_Blend
  (unlit, two-sided; blend from the bench), MI per texture; muzzle mesh uses MI_phantom_hands tinted mzl_geom orange.
  Preview only (whole flipbook, no curves) = HEURISTIC, not the game shader.
- Verified: all 20 textures + 2 meshes render textured (`Desktop\SM2_Work\Captures\ue_export\ue_export_board.png`; first dark try
  kept as `.png.bak1`, fix = manual exposure). Blood mesh uses its FBX lit materials (red). Backdrop BlackUnlitMaterial did not load →
  grey sky background (cosmetic). Capture actors are SM2Cap-tagged and removed by `export`; the editor level was not saved.

## vfx4-3 — browser polish (done)
- MainWindow.axaml(.cs): search shortcuts, a clear button, the export button now follows the selection, and Ctrl+E exports. GameCatalog.Query matches all words.
- Tests: Assets 142 (SearchWordsTests added), Extractor 42, LibSaber 5. The GUI has not been run on the PC (build only).
## mtl1 (2026-10-27 18:20 KST) — sorcerer/hellbrute colours: glt.chameleon; cloth bake; lictor sway
- Cause: textures/sRGB were fine and no tintByMask/colorization is used; Thousand Sons `paint`/`blue`/`ruby` variants enable `glt.chameleon`, which we ignored.
- REA: parser FUN_142576f30 (enabled +0, color0..3 +4/+0x14/+0x24/+0x34 ×1/255 const _DAT_1435640d0, offset1 +0x44, offset2 +0x48, fresnelPwr +0x4c), descriptor FUN_142578190, writer FUN_142577110, glt parse FUN_1425bb840, regs FUN_1421b0bc0, define GLT_CHAMELEON FUN_1423b6260 (strings 0x1437f9de0, 0x143729b78/b98). Shader pc_dx12_glt.sdc PS 03558_0, CB_STATIC_GLT rows 10-13 colours, 14-15 offsets: t=pow(sat(N·V),r15.z); 3-step colour ramp (r14.xy, r14.zw, r15.xy); albedo *= lerp(ramp, 1, metal).
- Code: SpecLayers.Chameleon → MeshPart.Chameleon → sidecar `chameleon`; M_SM2 `UseChameleon` + ChameleonC0..3/R0/R1 (Custom node). HEURISTIC: offset1/offset2 → thresholds (0, o1, o2) (CPU packer not traced), unset colour = white, colours treated sRGB.
- PC: exports Exports\mtl_cham\{cc_sorcerer (+idle clip lesser_sorcerer_idle_lesser_sorcerer, 585 cloth helper bones baked), cc_hellbrute}; UE /Game/SM2_Work/Tests/mtl_cham/* (earlier import kept as _bak1). Captures Captures\mtl_fix\after_cham_*, lictor_sway_t*_*.png, lictor_face.jpg.
- Open: hellbrute after-capture looks greyer than the bulk "before" (check variant/texture vs bulk, and chameleon defaults); browser GL preview does not render chameleon yet (cloth/physics preview already from 642fb56).

## mtl1-B (2026-10-27 18:50 KST) — hellbrute grey fix
- Cause 1: the chameleon Custom HLSL node's pins likely did not bind from Python, so the paint went grey. Cause 2: PixelNormalWS in the Substrate front graph. Bulk and mtl_cham use the same variants and textures (hash-identical armor_01). Toggling UseChameleon off restored the bulk look, which isolated the problem to the chameleon graph.
- Fix: the ramp is now built from plain nodes. N·V uses the tangent-space normal (detail_switch) · CameraVector transformed to tangent space, matching the glt PS. R0.w is read from the vector param's "A" pin (the RGB pin has 3 components and gave a "Not enough components" compile error).
- Result: Captures\mtl_fix\after_cham2_cc_{hellbrute,sorcerer}_front.png. Front-facing paint gets color3 (dark teal) and grazing angles get the cyan C1/C2 ramp. No in-game hellbrute reference exists (Captures\ingame_ref has only marine shots). The threshold mapping and color0 = white remain heuristic.

## mtl1-A: SM2PhysicsSetup editor plugin (live physics on import)
- `tools/ue/SM2PhysicsSetup/` — UE 5.7 Editor C++ plugin. `unreal.SM2PhysicsSetupLibrary.setup_from_json(mesh, physics_json, dest)`
  builds `PA_<mesh>_SM2` (sphere body per cloth helper bone; pinned particles maxDistance==0 + anchor parent kinematic; ball joints
  swing 25°/twist 12.5° to nearest closer helper; jiggles jointed to skeleton parent) and a post-process AnimBP `ABP_<mesh>_SM2Phys`
  (LocalToComponent → RigidBody(PA, WorldSpace) → ComponentToLocal), then assigns both to the mesh. Constraint limits/body radii are HEURISTIC.
- Build: `RunUAT BuildPlugin -Plugin=…uplugin -Package=<tmp> -TargetPlatforms=Win64` (VS2022 BuildTools 14.44), copy the package (minus HostProject/Intermediate) to `<Project>/Plugins/`.
- `tools/ue/ue_bulk.py import` calls it when `<name>.physics.json` exists and saves PA/ABP/mesh; warns and skips if the plugin isn't loaded.
- STATUS: builds; installed in UE_VFX_57/Plugins; NOT yet verified in-editor (editor restart pending: dirty non-SM2 packages open).
- mtl1-A VERIFIED (2026-10-27): editor restarted (21 dirty pkgs backed up as `.uasset.bak1`/`.umap.bak1`, then saved), plugin loads
  (`unreal.SM2PhysicsSetupLibrary`). `ue_bulk.py import cc_lictor|cc_sorcerer mtl_phys` -> PA/ABP built and saved
  (lictor: simulated=398 kinematic=42 constraints=440 missingBones=0). `tools/ue/ue_phys_verify.py spawn|sample|cap|export|clear`
  compares twin actors (post-process ABP on/off) in the editor world: helper-bone deviation lictor 2–5 cm, sorcerer mean 7–11 / max 48 cm,
  time-varying, no NaN. Frames: `Captures/mtl_fix/phys_<model>_t{1,2,3}_{physON,animOnly}_{front,side}.png`. No joint tuning needed yet (HEURISTIC limits).
- KNOWN BUG (ue_bulk re-import): `rename_directory(folder -> _bakN)` on LOADED assets followed by importing into the same path yields a mesh
  with private references into `_bakN` (InterchangeAssetImportData / SkeletalMeshDescriptionBulkData) -> "Illegal reference to private object",
  the mesh can't be saved. Workaround used: import into a fresh set (`mtl_phys`, export folders copied). FIXED (mtl1-1): ue_bulk now duplicate_directory -> _bakN (saved), then reimports in place; mesh save is checked (error if it fails). Verified on mtl_phys/cc_lictor.
- PC `Scripts/ue_bulk.py` had diverged from the repo copy; the physics block was patched in on the PC (backup `ue_bulk.py.bak1`).

## mtl1-2: chameleon in the browser GL preview
- `PreviewPartDraw.Chameleon` (from MeshPart.Chameleon) -> GlPreview `SetChameleon` uniforms uCham/uChamC[4]/uChamR0/uChamR1; fragment:
  t = pow(sat(N_mapped·V), r15.z), 3-step ramp, base *= lerp(c, 1, metal) — same as pc_dx12_glt.sdc PS 03558_0 and the UE master.
  HEURISTIC packing identical to import_sm2_materials.chameleon_ramp (thresholds 0, offset1, offset2). CPU renderer unchanged.
- Check: `--glbench synthetic` now writes gpu_synthetic_chameleon.png (box: /workspace/browser_run_shots/cham/). Not yet installed on the PC.

## mtl1-3: customization parts (armor + weapon) and the browser part picker
- **Armor:** see "Constructor tpls" above (character_customization_library.sso, `--variant SECTION=uid`).
- **Weapons (new, data + ssl evidence; none of these field names are plaintext in sm2_retail.exe, so the selection logic is ssl-only):**
  - `ssl/weapons/creator/customization/firearm_skins_library.sso`: `skinsGroups[] = { firearmUid, skins{ skin_N = FirearmSkin{ geomCustomizerRegions[], material?, attachingActors{locator, actorName}?, overridedActorName? } } }`. 68 firearmUids. Accessors are in firearm_skin.sslbin: GetGeomCustomizerRegions, GetMaterial, GetAttachingActors, GetOverridedActorName.
  - `ssl/geom_customizer/geom_customizer_preset_firearm.sso`: `regions{ name = GeomCustomizerRegion{ meshes[] } }`, 109 regions. The meshes are tpl object names; LOD copies (`base_01_LOD1`) are sibling nodes.
  - prop_geom_customizer_equipment.sslbin: ShowActiveRegions = HideAll + Show(region). The region Show/Hide ends in the native Geom ShowObj/HideObj (0x14133fec0 / 0x14133eae0 → 0x14133e750).
  - tpl → uid: `ssl/weapons/common/firearm/mgs/<uid>/<uid>_client.cls` geom.nameTpl. Several uids share a tpl (aux_melta_bolter also names wpn_bolt_rifle_01), so the uid with the most skins wins.
- **Code:**
  - `Customization/WeaponVariant.cs`: WeaponCustomization and WeaponVariant.Apply.
  - `--variant skin=<key>[,regions=a|b|c][,+obj,-obj]`. Only for `wpn_*` tpls, and only when given; the default export is unchanged.
  - `AssetExporter.DescribeVariant` builds the slots.
  - ExportWindow part picker for the previewed model:
    - Armor: preset combo plus one combo per body section.
    - Weapon: skin combo plus one checkbox per region present in the tpl.
    - It builds the `--variant` spec.
- **HEURISTIC:** region `main` is always shown (equipment defaultRegion not decoded). Skin `material` and `attachingActors` are reported but not applied; this is mesh-only.
- **Tests** (PC `Desktop\SM2_Work\Exports\cust_test\*`):
  - wpn_bolt_rifle_01 → arifle_bolt_rifle (80 customizable objects):
    - skin=01_red: 12 visible parts.
    - skin=05_h: 10 visible parts.
    - Custom regions (base_02_01|base_02_02|bolt_01|magazine_01|sight_telescopic_01|cartridge_01): 13 visible parts.
  - cc_sm_constructor_tank:
    - Default: 54 visible parts.
    - HELMET=helmet_tank_relic_01, LEFT_PAULDRON=left_pauldron_tank_relic_01: 53 visible parts.
- **Install:** the browser and CLI are installed in `C:\Users\Revaptor_FX\SM2Extractor` (backups `*.exe.bak2`). This build also brings in the mtl1-2 chameleon preview.
- CopyFromBox fails for 72 MB files ("temporarily unreachable"). Split them into 20 MB chunks and concatenate on the PC.

## mtl1-4: heuristic refinements (2026-10-27 KST)
- **Physics joint angles (changed):**
  - Cloth helper swing limit is now DERIVED from NvCloth `maxDistanceMeters`, which is the motion-constraint radius r around the skinned position. A particle at pivot distance L can deviate by atan(r/L).
  - Clamped to 5..60° (the clamp is HEURISTIC). The twist limit is half the swing. With no data it falls back to 25°. Jiggles keep 15° (no constraint data in current exports).
  - Built into `Build\sm2phys\pkg` (previous build in `pkg_bak1`). **Not installed yet:** it needs an editor restart, and the VFX executor is using the editor. To install: copy `pkg` over `UE_VFX_57\Plugins\SM2PhysicsSetup` (back up as `.bakN` first), restart the editor, and re-run setup_from_json for the assets.
- **Blood saturation, distortion 0.04, trail DEGR slot mapping:** unchanged and still HEURISTIC.
  - vfx4-1 (VFX executor, same day) already re-checked the exe: only CB names / shader defines were found, with no CPU writer. A runtime capture is not allowed (EAC).
  - These live in VFX files, which this task must not touch.

## mtl1-5: batch characters with physics (2026-10-27 KST)
- **Set:** `Exports\chars_phys\<name>` → `/Game/SM2_Work/Tests/chars_phys/<name>` (mesh, materials, `PA_<name>_SM2`, `ABP_<name>_SM2Phys`). Imported with `ue_bulk.py import <name> chars_phys`. Physics comes from the currently installed plugin (old fixed limits of 25°/15°).

| name | cloths | jiggles | helpers | bones | slots |
|---|---|---|---|---|---|
| cc_calgar | 8 | 3 | 1294 | 1596 | 44 |
| cc_chaplain | 4 | 23 | 894 | 1396 | 34 |
| cc_commissar | 6 | 0 | 1764 | 1970 | 13 |
| cc_tech_priest | 4 | 7 | 2002 | 2684 | 41 |
| cc_sorcerer_exalted | 4 | 2 | 2614 | 3038 | 30 |
| cc_titus_assault | 6 | 34 | 922 | 1455 | 41 |

- All six imported with 0 errors, 0 material mismatches and 0 ref-pose NaNs.
- **Sim check is inconclusive:** these exports have no clips, so `ue_phys_verify` stays at t=0 with deviation 0 (no NaN). The editor does not advance the AnimBP without a playing clip. Next step: export one clip per character (or use a pose-tick) and re-sample.
- `ue_bulk`'s `physics` report string comes back empty through Python (out-param), although the PA and ABP are created.
- Customization visual check: box `/workspace/cust_test/{bolter_01red_vs_05h,bolter_01red_vs_custom,marine_default_vs_relic}.png` (numpy rasterizer of the exported glbs).

## mtl1-6: Forge Lens rename + dark theme (2026-10-27)
- User-facing name is now **Forge Lens**. The SM2Browser csproj has AssemblyName=ForgeLens and Product "Forge Lens", and the window titles changed to match. Namespaces, project folders, the app.manifest identity and the RecentAssets appdata folder (`LibSaber.SM2Browser`) are unchanged, so existing recents are kept.
- Install: `C:\Users\Revaptor_FX\SM2Extractor\ForgeLens.exe` (MD5 AE60BB09C98C1F1A5D797FADF5E57424). The old `LibSaber.SM2Browser.exe` is kept, with a copy at `.bak3`. When the user says "실행해줘" ("run it"), launch ForgeLens.exe.
- Theme (App.axaml only; no behaviour changes): Fluent Dark palette, greys #1E1F22/#252629/#2B2C30, one accent #E8892B. Buttons and inputs are 28 px with radius 3, toolbar and status bar have borders, column headers use the `header` class, and the Export button uses the `accent` class.
- Screenshots: `Captures\mtl_fix\ui_before.png` and `ui_after.png`, also on the box at /workspace/mtl_fix/. Captured with `Scripts\mtl_fix\wincap.ps1` (PrintWindow, off-screen, no focus; run it under powershell.exe 5.1).

## mtl1-7: SM2PhysicsSetup v2 installed + live sim check with clips (2026-10-27)
- **Plugin:** the new build (cloth swing = clamp(atan(maxDistCm/segment),5,60)°, DERIVED, with the clamp a HEURISTIC; twist = swing/2) is installed in `UE_VFX_57\Plugins\SM2PhysicsSetup`. The old copy moved to `Desktop\SM2_Work\Build\sm2phys\installed_SM2PhysicsSetup.bak1`; it sits outside Plugins so UE doesn't load a duplicate. `plug.py` → PLUG True.
- **Re-run:** done for the 6 chars_phys characters plus mtl_phys/cc_lictor and mtl_phys/cc_sorcerer with 0 errors. ue_bulk backed up the previous imports as `_bakN` (calgar `_bak1` + `_bak2`, lictor `_bak2`, the rest `_bak1`). The plugin does not overwrite, so new assets are named `PA_/ABP_<mesh>_SM2Phys_2`, and the mesh's PhysicsAsset/PostProcess ABP point to these new ones.
- **Clips (re-export with `--anim`; old export folders kept as `<name>.bak1`):** calgar `calgar_run_fwd`, chaplain `chaplain_idle_v01`, commissar `captain_idle_1_commissar`, tech_priest `tech_priest_censer_walk_loop`, sorcerer_exalted `exalted_sorcerer_dash_fwd`, titus_assault `tacticus_run_fwd_sound_intercessor`. All have nan=0.
- **Live sim (`tools/ue/ue_physlive.py`, Simulate-In-Editor):** the editor world doesn't tick in a hidden editor, so I use SIE. Sequence: `ue_phys_verify spawn` → `physlive cams` → `start` → `play` (re-plays the clip on the PIE copies, re-inits the off twin without the PP ABP, sets AlwaysTickPoseAndRefreshBones) → `sample` / `cap` → `stop` / `camclear`. Mean/max deviation in cm of all helper bones, physON vs animOnly:

| char | bones | mean | max | NaN |
|---|---|---|---|---|
| calgar (run) | 917 | 11–17 | 80–132 | no |
| chaplain (idle) | 732 | 8.8–9.0 | 46–50 | no |
| commissar (idle) | 921 | 2.8–3.6 | 28–53 | no |
| tech_priest (walk) | 1818 | 56–59 | 164–177 | no; **FLAG:** the robe drops about 1.4 m (helper z −88 vs +51) |
| sorcerer_exalted (dash 13 m/s) | 2406 | 69–137 | 208–467 | no; **FLAG:** world-space lag / drop on the root-motion dash |
| titus_assault (run) | 782 | 9–17 | 43–75 | no |

- PNGs: `Captures\mtl_fix\phys_<name>_live_{physON,animOnly}_{front,side}.png`. Side-by-side copies are on the box at `/workspace/mtl_fix/phys_live_<name>_cmp.png`.
- **Next:** for tech_priest and sorcerer_exalted, check that the top cloth bodies are kinematic and constrained to the parent, and add linear limits. Python can't edit constraints, so this has to go in the plugin cpp.
- The editor map in use, `/Game/Work/02/Lightning_01` (not ours), was dirtied by the temporary actors (all cleared). **Do not save it.**

## mtl1-8a: "Anim Only (Manny)" — clips written directly on SK_Mannequin (2026-10-27)
- **CLI:** `export ... --anim <clips> --anim-only-manny` (built-in SKM_Manny reference, `Retarget/SK_Mannequin.ref.json`, 161 bones, embedded) or `--anim-mannequin <ref.json>` (a dump from `tools/ue/ue_dump_skeleton.py`). Both imply anim-only and turn physics off. Output: `{model}__{clip}_Manny.fbx`, which already uses SK_Mannequin's bone names, hierarchy and ref pose. No IK Retargeter is needed in UE.
- **Forge Lens:** Export window checkbox **"Anim Only (Manny)"**.
- **Method (HEURISTIC, world-delta, `MannequinRetarget.cs`):** W_manny = Rref_manny · A · inv(Rref_sm2) · R_sm2(t).
  - A is the shortest arc from the Manny bone direction to the SM2 bone direction.
  - Root and pelvis translation are scaled by the pelvis-height ratio (titus 0.649).
  - Unmapped bones (twist, corrective) keep their ref locals. `ik_*` copy the foot and hand worlds.
  - World composition recurses because SM2 parents can come after their children (titus LEG2L = 4, parent LEG1L = 156).
  - `SM2_MANNY_DEBUG=1` prints DBG lines.
- **UE import:** `tools/ue/ue_import_manny.py <fbx|folder>` imports onto `/Game/Characters/Mannequins/Meshes/SK_Mannequin` into `/Game/SM2_Work/Anims_Mannequin`, keeping `_bakN` backups.
  - **Yaw is 0.** Auto would give 180 and turn the clip around.
  - A reimport reuses the yaw stored on the existing asset, so the script resets it first. This was the cause of the earlier "rotated 180°" result.
  - `ue_bulk.py import` picks up `*_Manny.fbx` automatically, imports them onto SK_Mannequin, and leaves them out of the SM2 clip import.
- **Verified (titus run / idle / attack), AnimPose world positions:** Manny vs SM2 ×0.649 at 8 bones × 4 times, mean 8.0–9.6 cm, max 14.8 cm. The remaining gap is proportions.
  - Plots: box `/workspace/mtl_fix/retarget_{run,idle,attack}.png` (red SM2, blue Manny, front and side).
  - The SceneCapture renders stayed on frame 0 because the editor world does not tick, so the plots are drawn from AnimPose instead.
- **Test assets:** `/Game/SM2_Work/Anims_Mannequin/SM2ref` (titus SM2 reference clips) and `Anims_Mannequin_y0` (an earlier test).
- **New test map:** `/Game/SM2_Work/Tests/PhysTestMap`, created with `phys_map.py`; it never saves Lightning_01. It is now the editor's current map.

## mtl1-8/9 physics: robe pins, CENTRE base-bone sim space, PhysTestMap (2026-10-27)
- **Plugin (cpp, installed in UE_VFX_57 and verified with PLUG True; old copy kept at `Build\sm2phys\installed_SM2PhysicsSetup.bak3`):**
  - Multi-particle helpers are now pinned by the maxDistance=0 index.
  - Every skeleton parent of a cloth pins its closest helper and gets a kinematic body.
  - The RigidBody node simulates in the CENTRE base-bone space with WorldAlpha 0 and DampingAlpha 0, plus velocity and acceleration caps (all HEURISTIC).
  - New UFUNCTIONs: `SetupFromJsonEx` (skip cloths), `TuneRigidBody`, and `CreateChaosCloth`.
- **Tuning:**
  - `ue_bulk.py` calls `TuneRigidBody` after setup, so an older installed binary gets the same defaults.
  - `tools/ue/ue_tune_rb.py` is the manual tuning script.
- **Live test:** runs in `/Game/SM2_Work/Tests/PhysTestMap` (`tools/ue/ue_phys_map.py`). Lightning_01 is never saved. The test twins use NO_COLLISION, so there is no ComplexAsSimple spam.
- **Measured, mean/max cm (physON vs animOnly, all helper bones):**

| char | mtl1-7 | now | NaN |
|---|---|---|---|
| calgar run | 11–17 / 80–132 | 11–13 / 87–121 | no |
| chaplain idle | 8.8–9.0 / 46–50 | 5.9–6.0 / 35 | no |
| commissar idle | 2.8–3.6 / 28–53 | 2.4–2.8 / 30–47 | no |
| titus run | 9–17 / 43–75 | 8–14 / 33–54 | no |
| tech_priest walk | 56–59 / 164–177 | 23–24 / 92–110 | no; **FLAG:** the robe still hangs about 70 cm low on helper 0 (constraint stiffness can't be set from Python) |
| sorcerer_exalted dash | 69–137 / 208–467 | 41–62 / 245–341 | no |

- An intermediate setting (WorldAlpha 1 / DampingAlpha 0.5) made the sorcerer explode (mean 234–611 cm, max 106 m). The 0/0 setting fixed it.
- PNGs: box `/workspace/mtl_fix/phys_v3_<char>_cmp.png`.
- `ue_mrq_warmup.py` sets the MRQ engine and render warm-up frames on a config (for cloth and RigidBody settling). It keeps a `_bakN` copy of the config.

## mtl1-10 Chaos Cloth option (opt-in, 2026-10-27)
- **How to run:**
  1. Export with `--include-cloth-sim`.
  2. Run `ue_bulk.py import <asset> <set> chaoscloth`, or `tools/ue/ue_chaos_cloth.py <asset> <set>` on its own.
- **What it does:**
  - Duplicates the mesh to `<asset>_ChaosCloth`. An existing copy is renamed `_bakN` first.
  - `CreateChaosCloth` turns each `_SIM` section into Chaos clothing data, pins the maxDistance=0 particles, and binds the render sections.
  - Every cloth that can't be converted stays on the bone method via `SetupFromJsonEx` (`<folder>/ChaosCloth/PA_*`, `ABP_*`).
  - The bone method stays the default.
- **Sim section match (HEURISTIC):**
  - First by slot name with ≥80% of vertices within 2 cm of a sim particle.
  - Otherwise the earlier geometry test (95% within 0.5 cm).
- **Converted:**
  - calgar: belt_01, belt_04
  - tech_priest: skirt_v7, tabards_bck_v4
  - sorcerer_exalted: skirt, tabard
- **FLAG:** the cloak / ribbon / sleeves / tabards_frt / beads sections have slot hits of 0.00–0.74 against simPositionsUeCm. The sim positions don't match the imported section (likely a different bind space or a welded/split topology), so these stay bone-based. Not yet investigated.
- **Live check:** PhysTestMap, SIE, `liveC.ps1`. Values are mean cm of the helper bones, physON vs animOnly. Converted cloths no longer drive their helper bones, so a lower number partly reflects that.
  - calgar: bone 11–13 → chaos 7–8
  - tech_priest: bone 23.5 → chaos 1.9–2.4. Visually, the robe no longer sags below the feet.
  - sorcerer: bone 55–63 → chaos 20–21
  - No NaN in any run.
  - PNGs: box `/workspace/mtl_fix/cloth_cmp_{calgar,tech_priest,sorcerer_exalted}.png` (top = bone method, bottom = Chaos).
- **MRQ:** before rendering cloth or RigidBody shots, run `tools/ue/ue_mrq_warmup.py <config> 32 8` so the sim settles (engine and render warm-up frames).
