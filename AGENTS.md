# AGENTS.md — start here (new agent / Grok Bot entry point)

> 이 파일이 진입점입니다. 레포 주소만 받은 새 에이전트는 이 파일부터 끝까지 읽고, 아래 "Read order"대로 따라가면 바로 작업을 이어갈 수 있습니다.

## 1. What this project is
- **LibSaber** parses Saber3D engine game files. On branch `cursor/sm2-pck-extractor-4420` (PR https://github.com/Baegovda/LibSaber/pull/1) it has grown into a **Warhammer 40,000: Space Marine 2 (SM2) asset browser + exporter to Unreal Engine 5.7**:
  - `LibSaber.SM2Browser`: an Avalonia GUI that browses the game's `.pak`/`.pck` archives and previews textures, models, animations, secondary physics and VFX on the GPU, then exports the selected assets.
  - `LibSaber.SM2Extractor`: a CLI with `list` / `extract` / `export`, sharing the browser's export path.
  - `LibSaber.SM2.Assets`: the format logic (meshes, materials, `.sani` animation, physics, effects, customization variants).
  - `tools/ue/*.py`: Unreal Editor Python scripts for import, materials, verification and captures. `tools/bulk/`: batch validation.
- **Goal:** make **SM2 work perfectly first**. The output should look like the game, in both the browser and Unreal.
- **Halo is out of scope.** Ignore `LibSaber.HaloCEA` / `Halo2A`, and ignore their test failures.
- **Not a bulk ripper.** It is an explorer that exports what the user selects.

## 2. Read order
1. **This file.**
2. `docs/handoff/STATUS.md`: what is done, partial or todo (with %), commits, tests and open limitations.
3. `docs/handoff/ROADMAP.md`: the approved work order and the exact next steps.
4. `docs/handoff/WORKFLOW.md`: where work runs (box vs the user's PC), build/test, push via the cloud agent, captures, PC paths, and the 20-minute report routine.
5. `docs/SYMBOL_DB/INDEX.md` (the RE "genome map": per-domain overviews + `index.tsv` of every known exe symbol) — **grep it before starting new RE**; `docs/handoff/RE_NOTES.md` holds the long-form evidence tables.
6. `docs/handoff/FORMATS.md`: SM2 formats, and which source file parses each one.
7. `HANDOFF.md`: the long chronological log of every stage, with full details. Read the **newest sections at the bottom** first.
   - Its top "bootstrap" block is older. Where it conflicts with this file (owner name, push method, "next step"), **this file and docs/handoff win**.
8. `git log --oneline -30 origin/cursor/sm2-pck-extractor-4420` is the source of truth for what has landed. Always `git fetch` first.

## 3. Hard rules (non-negotiable)
1. **REA-first.** Every format, shader or behaviour conclusion must come from reverse-engineering `sm2_retail.exe` (Ghidra / rizin / REA). **The REA tool is https://github.com/morluto/rea** (npm `rea-agents`, CLI `rea` + stdio MCP `rea mcp`); setup and caveats in `docs/handoff/WORKFLOW.md` → "REA (morluto/rea)". **REA is the primary RE tool**: run it through the persistent daemon (`tools/re/rea_daemon.py`, client `tools/re/rea_call.py`); plain Ghidra headless / rizin only as fallback. Check `docs/SYMBOL_DB` (symbol database) before any new RE. Do not use the old `user-rea` plugin. Cite the exe address or string (e.g. `FUN_14133eae0`, `"blendMode" @0x14355b708`) in code comments, commit messages and HANDOFF.
   - Anything not proven must be labelled **heuristic / assumption** (see the "Heuristics" lists in STATUS.md).
   - Never "guess from bytes" without saying so.
2. **Never delete the user's assets.** Before overwriting a file or folder, back it up as `.bakN` (UE: `_bakN`), with N the next free number.
   - Deleting anything (backups, UE folders, caches) needs the user's **explicit approval for that exact deletion**.
3. **Work only in** the PC's `C:\Users\Revaptor_FX\Desktop\SM2_Work` and Unreal's `/Game/SM2_Work`.
   - `/Game/SM2_Work/Original/*` is the user's original import: read-only.
   - Tool installs live in `C:\Users\Revaptor_FX\SM2Extractor*` / `SM2Browser_*` (historical) and `Desktop\SM2_Work\Bin`.
4. **The user games on that PC.** While a game is running (e.g. Against the Storm):
   - don't steal focus (start processes with `-WindowStyle Hidden`, priority Idle);
   - don't run heavy GPU, Unreal or export work.
   Check first with `Get-Process | ? MainWindowTitle` and see WORKFLOW.md.
   - **2026-10-27 update from the user:** heavy work during gaming is OK as long as it doesn't disrupt the game (keep it hidden + idle priority, no focus stealing).
   - The user says "게임끝" when the PC is free. If they say they're playing again, stop heavy PC work immediately and continue on the box.
5. **Update HANDOFF.md at the end of every stage:** append a dated section with state, evidence, paths and commits. Also update `docs/handoff/STATUS.md`.
6. **Niagara (`.sfx` → UE Niagara) is not on the roadmap** (user, 2026-10-27). It is only a possible future development; don't start it unless the user asks.
7. **No secrets in the repo** (tokens, passwords, credentials).

## 4. Talking to the user (owner)
- **Language:** Korean, short and plain (해요체). No jargon dumps. Explain results, not internals.
- **Interim reports are mandatory** ("중간중간 보고"). Report at every stage, and every report carries a **progress %** for the current track, e.g. "VFX는 이제 약 85%예요."
- **Step by step:** one item at a time in the approved order. Report when an item finishes, then go to the next.
  - Ask before a big new phase that hasn't been approved (Niagara, deletions, anything outside SM2_Work).
- If a % estimate was wrong, correct it openly ("아까 85%라고 했는데 다시 세어 보니 70%가 맞아요").
- Give times in KST and mark estimates "(추정)".
- When the user asks "상황보고" / "하는중?", answer immediately with what is running, the %, and the ETA.

## 5. Permissions the user has granted
- Full permission on the SM2 project: build, install new tool builds (old ones kept as `.bakN`), push to PR #1, manage folders inside SM2_Work, and restart Unreal Editor (save first).
- **Deletions only with explicit approval**, per case. Examples already approved and done: the bulk `*.bakN` export folders, the UE `_bakN` test folders, empty `_Legacy`/`Test` folders.
  - Not approved: the DDC cache.
- Pushing to PR #1 goes through the Cursor cloud agent (WORKFLOW.md §4).

## 6. Where things are (summary; details in WORKFLOW.md)
| What | Where |
|---|---|
| User's PC | Windows "Revaptor-FX", ListMachines id `d3fb99ad-25ca-45c1-81af-41e5ca09104a` (at time of writing) |
| Game | `D:\Steam\steamapps\common\Space Marine 2\client_pc` |
| Unreal | 5.7.4, project `K:\Unreal Projects\UE_VFX_57`, work in `/Game/SM2_Work`, driven by **EngineWire / MCP Automation Bridge 0.5.30** (https://github.com/ChiR24/Unreal_mcp, native MCP HTTP `127.0.0.1:3000/mcp`, `tools/ue/enginewire_call.py` + `uerun.ps1`); UEMCP (`tools/ue/uemcp_call.py`) stays as fallback. See WORKFLOW.md §5 |
| Work folder | `C:\Users\Revaptor_FX\Desktop\SM2_Work\{Scripts,Exports,Captures,Bin,Raw,Backup,...}` |
| Box (bot's Linux machine) | repo `/workspace/LibSaber` (+ worktrees), exe `/workspace/rea/sm2_retail.exe`, Ghidra project `/workspace/rea/ghidra_proj2.gpr` (program `sm2_retail.exe`), push agent `bc-65aed255-5102-5266-bcbd-f60b85b14984`, patches `/workspace/patches/<stage>/` |
