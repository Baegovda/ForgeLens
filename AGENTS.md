# AGENTS.md — start here (new agent / Grok Bot entry point)

> **NEW BOT / CURRENT HANDOFF (2026-11-02): read [`docs/HANDOFF_NEW_BOT.md`](docs/HANDOFF_NEW_BOT.md) FIRST.** It wins over this file, START_HERE, BOT_TRANSFER, STATUS, and RULES_CORE where they disagree. See its "2026-11-02 정리" section for tip, P1–P11, ForgeLens_latest, editor lock.

> Older cold-start notes: [`docs/handoff/START_HERE.md`](docs/handoff/START_HERE.md) (10/29, partly stale) and [`docs/BOT_TRANSFER.md`](docs/BOT_TRANSFER.md) (10/29 SM2-only transfer; partly superseded).

> 이 파일은 개요입니다. 레포 주소만 받은 새 에이전트는 **HANDOFF_NEW_BOT.md**부터 읽고, 아래 Read order로 이어가면 됩니다.

## 1. What this project is
- **ForgeLens** (repo https://github.com/Baegovda/ForgeLens, private; branch `cursor/sm2-pck-extractor-4420`) is a multi-game asset browser + Unreal 5.7 exporter. It grew out of LibSaber / SM2 work (migrated 2026-10-29 from the public fork Baegovda/LibSaber; that fork is kept untouched; any old PR there was not migrated).
  - `LibSaber.SM2Browser` → published as **`ForgeLens.exe`** (Avalonia GUI): browse archives, preview textures/models/anims/physics/VFX, PoE2 and Wukong windows, export.
  - `LibSaber.SM2Extractor`: CLI `list` / `extract` / `export` (+ PoE2 switches).
  - `LibSaber.SM2.Assets`: format logic (SM2 + `Games/Poe2/`; Wukong draft under `tools/bmw` + Bmw*).
  - `tools/ue/*.py`, `tools/poe2/*`, `tools/bmw/*`, `tools/bulk/`, `tools/watch/*`, UE plugin `SM2PhysicsSetup`.
- **CORE CONCEPT:** every supported game (SM2, PoE2, Black Myth: Wukong, Stellar Blade, Elden Ring) = the SM2 feature set with only the game changed. Unreal import under **`/Game/ForgeLens/<CODE>/`** (SM2, POE, BMW, …). Legacy `/Game/SM2_Work` still allowed (`FORGELENS_UE_ROOT=legacy` or existing assets). See `docs/RULES_CORE.md`.
- **Game order:** SM2 (essentially done) → **PoE2** (current) → Wukong → Stellar Blade → Elden Ring. PoE2 only, never PoE1.
- **Halo is out of scope.** Ignore `LibSaber.HaloCEA` / `Halo2A` test failures.
- **Not a bulk ripper.** Explorer that exports what the user selects. App version **0.97.0** (`LibSaber.SM2Browser.csproj` `<Version>`).

## 2. Read order
1. **`docs/HANDOFF_NEW_BOT.md`** (source of truth; "2026-11-02 정리" first).
2. `docs/RULES_CORE.md` + `docs/SUBAGENT_PREAMBLE.md` (give preamble to every worker).
3. `docs/POE2_M0.md` … `M3.md`, `docs/WUKONG_M0.md` as needed for the track.
4. `docs/handoff/STATUS.md` (historical SM2 stage log + current pointer at top).
5. `docs/handoff/WORKFLOW.md`: box vs PC, build/test, push via `push.ps1` (ff-only).
6. `docs/SYMBOL_DB/INDEX.md` before new RE; `docs/handoff/RE_NOTES.md` for long evidence.
7. `HANDOFF.md`: long chronological ledger — read the **newest sections at the bottom** first. Do not rewrite history; append only.
8. `git fetch && git log --oneline -30 origin/cursor/sm2-pck-extractor-4420` is the commit truth.

## 3. Hard rules (non-negotiable)
1. **REA-first** for SM2 exe conclusions. REA = https://github.com/morluto/rea via daemon `tools/re/rea_daemon.py` + `rea_call.py`; plain Ghidra/rizin fallback. Check `docs/SYMBOL_DB` first. Label unproven claims **HEURISTIC**.
2. **Assets (R61):** under `/Game/SM2_Work/` **and** `/Game/ForgeLens/` generated content may be cleaned after a referencer check; log deletions in `docs/handoff/CLEANUP_DELETED.tsv`. Never touch `/Game/Work/*` or the owner's own content. Source/PC files: `.bakN` backups.
3. **PC work folder** `C:\Users\Revaptor_FX\Desktop\SM2_Work`. Unreal paths: **`/Game/ForgeLens/<CODE>/`** (default) and legacy `/Game/SM2_Work` (both allowed).
4. **Never launch/control any game** (SM2, PoE2, Wukong, …). In-game captures are owner-only. Never start the UE editor while a game is running.
5. Update `HANDOFF.md` (append) and keep STATUS/HANDOFF_NEW_BOT current at stage ends.
6. **Niagara:** large PoE2→Niagara ports are **owner-gated (P11)** — offer/estimate OK; do not start without an explicit go. Smaller SM2 orb Niagara work already landed historically.
7. **No secrets in the repo** (EngineWire token, hard-coded AES key literals in tools — use `BMW_AES_KEY` env for Wukong).
8. **No routines:** owner deleted `sm2-20`; do not recreate. Report **final results only** (no interim % / ETA spam unless the owner asks "현재상황" / "상황보고").
9. Push ff-only to `cursor/sm2-pck-extractor-4420` via PC `push.ps1` / named push lock. **Never force-push.** Do not push to `master` unless the owner decides master cleanup.

## 4. Talking to the user (owner)
- **Language:** Korean, short **해요체**. Result first; what was verified / not verified. No filler.
- **Results only** (supersedes older "interim % + ETA" wording in this file / RULES_CORE / START_HERE). Answer "상황보고" at once with what is running and what is done.
- Never state a number you did not measure. Admit faults plainly. Ask only what needs his word, at the end.
- Docs/commits/code: **English**. Times in **KST**.

## 5. Permissions the user has granted
- Full permission on ForgeLens / SM2 / PoE2 / related tracks: build, deploy to `Bin\ForgeLens_*` (keep old folders), push to the working branch, manage generated UE content under `/Game/SM2_Work` and `/Game/ForgeLens`, restart Unreal (save own assets first; take editor lock).
- Not approved without a yes: deleting non-generated content, changing repo visibility, force-push, committing secrets, launching games, starting Niagara P11, master-branch cleanup.

## 6. Where things are (summary; details in HANDOFF_NEW_BOT §7 / WORKFLOW.md)
| What | Where |
|---|---|
| User's PC | Windows "Revaptor-FX", user `Revaptor_FX`. MachineId differs per Grok Bot account — use `ListMachines` by name. |
| Games | SM2 `D:\Steam\steamapps\common\Space Marine 2\client_pc`; PoE2 `D:\Daum Games\Path of Exile2`; Wukong `D:\Steam\steamapps\common\BlackMythWukong` |
| Unreal | 5.7.4, `K:\Unreal Projects\UE_VFX_57`, EngineWire MCP `127.0.0.1:3000` |
| Work folder | `Desktop\SM2_Work\{Bin,Scripts,PoE2,Captures,...}`; deploy `Bin\ForgeLens_latest\ForgeLens.exe` |
| Push clone | `C:\Users\Revaptor_FX\ls_push` + `Scripts\mtl_fix\push.ps1` |
| Editor lock | `C:\Users\Revaptor_FX\ue_editor_busy.txt` (`editor-free` when idle) |
