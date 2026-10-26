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
  - Earlier RE outputs at `/workspace/rea/out`.
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
