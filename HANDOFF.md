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
  - The box has no GitHub login, so commits made there are not pushed. Push them through a cloud agent or ask india.
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
1. **Tyranid color.** The Hormagaunt comes out grey-pink because of a runtime tint/dye step.
   - Work in progress: commit ef29997 carries `albedo_tint` through. Check how far that got and verify it in Unreal.
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
