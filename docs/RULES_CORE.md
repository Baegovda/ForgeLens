# ⭐ CORE CONCEPT (READ FIRST)

> **CORE CONCEPT: Every supported game (SM2, PoE2, Black Myth: Wukong, Stellar Blade, Elden Ring) is the SM2 feature set with only the game changed.**
>
> **Each new game must reach the same features as SM2:** models, skeletons, animations, textures, materials, physics/cloth, levels/props, the ForgeLens viewer, and Unreal import under `/Game/ForgeLens/<CODE>/`.
>
> **Build a shared pipeline; only the game-specific file readers/format parsers differ.**
>
> **PoE2 skills (effects/animations) must also be viewable in the viewer.**

---
# RULES_CORE — SM2 / Forge Lens standing rules (read once per session + after every context summary)
Source of detail: BOT_TRANSFER §0/§5, HANDOFF ledger R01–R43. Newest dated owner statement wins. Run `rules_check` right after reading.

## Talk
- Short Korean 해요체. Write Ghidra = 기드라, REA = 레아. Code/paths/file names in English. Times in KST.
- **Final results only** (interim % + ETA superseded 2026-11-02 / HANDOFF_NEW_BOT). "상황보고"/"하는중?" → answer at once with what is running and what is done.
- Evidence labels PROVEN / INFERRED / HEURISTIC (HEURISTIC last, labelled, say what was searched).
- NEVER state a number/name not measured this session → "아직 안 쟀어요".
- Request → industry term + short Korean explanation. Handoff docs short, core-only.
- Disk/RAM warnings: only when a problem exists; watchers log to `SM2_Work\alerts.txt`, no popups; mention only if non-empty (owner 10/30).

## Work
- Act first, report after; work non-stop; questions at the end. Work has top priority.
- REAL-INPUT TESTS (owner t220u 10/30): never abort because the owner's windows/input interfere; block his physical mouse+keyboard for the test: `tools/input_block.ps1 -Script <test.ps1> -ArgsJson '{...}'` (default `-Mode hook` = low-level hooks drop non-injected events, no elevation; `-Mode blockinput` = user32 BlockInput, self-elevates, but the viewer's 3D viewport then ignored injected LMB/hover in R88). Watchdog kills the run after `-MaxBlockSec` (60), finally always unblocks, cursor restored, covering windows minimised and restored (never closed, never UnrealEditor). Tests call `InputBlockOn` before the first input (r83/r84/r87 scripts do it in `Focus`).
- PC jobs: High priority, hidden, never steal focus. Don't drop to Idle unasked (owner 10/29; supersedes R26 "Idle while gaming").
- Record every owner request in the HANDOFF ledger at once.
- Fixes are generic rules, never per-character patches. Never ask for in-game captures.
- No untrusted exes. No Discord unasked. Niagara: PoE2 large port = owner-gated P11 (offer OK; start only on explicit go). Game data parsed read-only.
- REA daemon / GhidraServe: manual start only, no autostart (R41).
- Batch REA work in one session. Before any decompile: `python3 tools/re/recall.py <addr|words>`.
- New tool → add a row to `tools/re/README.md` (registry) in the same commit.

## Unreal (UE 5.7.4, K:\Unreal Projects\UE_VFX_57; `/Game/ForgeLens/<CODE>/` default + legacy `/Game/SM2_Work`)
- ROUTE RULE: check UnrealEditor running (`rules_check`) →
  - `rules_check` UE line: editor off → headless; editor + port 3000 open → EngineWire; editor + port closed → bridge DOWN → UEMCP or fix bridge. Only `UE_VFX_57` counts (other projects, e.g. BLTest, ignored).
  - running → EngineWire MCP: `Desktop\SM2_Work\Scripts\uerun.ps1 <abs script> <args>` → `enginewire_call.py` → 127.0.0.1:3000/mcp (fallback UEMCP `uemcp_call.py`);
  - not running → headless `UnrealEditor-Cmd -run=pythonscript ... -AutoDeclinePackageRecovery` (never both at once).
  - Always state the route used in the report.
- Assets (R61, owner 10/30): everything under `/Game/SM2_Work/` and `/Game/ForgeLens/` that we generated (incl. `Original/*`, `_bakN`, Tests, hologram_planet) may be deleted/renamed/moved/re-imported freely after a live referencer check; log deletions in `docs/handoff/CLEANUP_DELETED.tsv`. Never touch `/Game/Work/*`, the owner's own content, or `Lightning_01`; if unsure, leave it and list it. Source code / PC files: back up as `.bakN` (next free N, never overwrite an existing one).
- Editor restarts pre-authorised: save, quit, relaunch with `-AutoDeclinePackageRecovery`. Crash → read report, relaunch, resume. Stop any PIE you start.
- WINDOW WATCHER (owner 10/30): during editor work keep the PC watcher running (`C:\Users\Revaptor_FX\window_watch`; check `tools\watch\status.ps1`, run `start.ps1` if `heartbeat.txt` is older than 30 s). Read the tail of `events.jsonl` for Save Content / crash / modal windows (Message Log is auto-closed); open the screenshot in `shots\` only when a new event appears.
- Never print/commit the EngineWire token (`%USERPROFILE%\.enginewire_token`).

## Git / push
- Repo Baegovda/ForgeLens (private), branch `cursor/sm2-pck-extractor-4420`. Box can't fetch/push.
- Own box worktree → `push_lock.sh acquire` → `git format-patch` → CopyFromBox to `C:\Users\Revaptor_FX\0NNN-*.patch` → `Desktop\SM2_Work\Scripts\mtl_fix\push.ps1` (ff-only, prints TIP) → `refresh_origin.sh` → `release`.
- Never force-push. One push at a time. No secrets in the repo.

## Routine / scope
- ~~Routine `sm2-20`~~ **deleted by owner (t245u)** — do not recreate. Report final results only.
- BongThumbLive (봉썸) **is in scope when the owner asks** (P3 dots spinner). "게임끝" = PC free. "실행해줘" = launch `Bin\ForgeLens_latest\ForgeLens.exe` (build from tip **1461bbf** as of 2026-11-02 01:52 KST).

## Tools quick index
| Tool | When |
|---|---|
| `tools/re/pc/rules_check.ps1` | session start / after summary: rules sha, UE route, REA/GS up, alerts |
| `tools/re/recall.py` | before ANY decompile (RE_CACHE/INDEX.tsv) |
| `ghidra_serve/gs.ps1 start\|status` · `gs.py sum\|batch\|decompile` | own Ghidra queries (PC, proj_sm2) |
| `pc/start_rea.ps1` · `rea_call.py` | REA 6.8.0 daemon (pinned) |
| `re_cache_build.py` | after every RE batch |
| `exe_check.py` | game patched? (exe sha256) |
| `docs_check.py [--pc]` | docs/paths/REA args + this file still wired |
| `pc/{mem,disk,patch}_watch.ps1` → `SM2_Work\alerts.txt` | silent watchers; read alerts via rules_check |
| `pc/backup.ps1 copy\|verify` | big artefacts → K:\SM2_Backup |
| `box/push_lock.sh`, `box/refresh_origin.sh` | push flow |
| `uerun.ps1` / `enginewire_call.py` / `uemcp_call.py` | UE editor control (route rule) |
| `nmb_verify.py` | fire-clip table vs primaris.nmb |
| `tools/input_block.ps1` | any real mouse/keyboard test: blocks owner input (hook / BlockInput), 60 s watchdog |
Full registry: `tools/re/README.md`.

## Crash Reporter windows (R97, owner t261u/t262u)
- A UE Crash Reporter window must never stay open: the window watcher (tools/watch/watcher.ps1, 250 ms poll) closes it within 5 SECONDS (measured detect->closed 105 ms; dummy window launch->closed 1.28 s). It sends WM_CLOSE (= Close Without Sending), saves %USERPROFILE%\window_watch\crash_<ts>.txt (crash folder, errors, log tail) and appends %USERPROFILE%\window_watch\NEEDS_ACTION.txt.
- EVERY worker checks %USERPROFILE%\window_watch\NEEDS_ACTION.txt at start and before/after each editor step; if present: read the summary, handle it (relaunch the editor only if the game is not running, fix the lock file), then delete the marker.
- If the watcher heartbeat (window_watch\heartbeat.txt) is older than 30 s, restart it with tools/watch/start.ps1 before using the editor.
- Launch the editor with -unattended -nosplash (no modal dialogs/Message Log windows). The 01:54 and 03:29 crashes were D3D11 CreateSwapChainForHwnd 80070005 while a NEW editor window (Message Log/dialog) was created; swap-chain hooks (RTSS/MSI Afterburner, DisplayFusion) are loaded. Correlated, not proven.

## Game is user-only (R97, owner t265u)
- NEVER launch, focus, send input to, or otherwise control Space Marine 2 yourself, in any worker, ever. In-game captures are taken only by the user. Use game data (extractor), the viewer and Unreal only.

