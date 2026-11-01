# HANDOFF_NEW_BOT - ForgeLens + Bong* projects, transfer to a new Grok Bot account

> **NEW BOT: START HERE. The owner's usage on the old account is nearly exhausted (2026-10-31). Read this whole file first, then section 0.** Last update: 2026-11-02 (SM2 oddities A/B/C after tip `e937708`; see section above). Prior cleanup tip `1461bbf`. See section "2026-11-02 정리" below — it wins over older sections here when they disagree.

Written 2026-10-31 ~16:00-16:30 KST by the previous assistant ("ForgeLens" bot, Grok Bot) because the owner's usage is running out.
Owner request (t279u, verbatim): "이제 사용량이 얼마 안남아서 다른 계정의 봇에게 인계할 인수인계문서를 아주아주 꼼꼼하게 모든걸 전달할수있게 남겨줘 5번 재확인하고 보강해줘"

Labels: **[V]** = VERIFIED this session (file/commit/command output read 2026-10-31 15:58-16:30 KST). **[I]** = INFERRED (from docs or the previous bot's chat, not re-checked). **[U]** = UNVERIFIED / unknown.
Where this file and older docs disagree, THIS file wins (it is the newest). Older docs: `docs/BOT_TRANSFER.md` (10/29, SM2-only, partly stale), `docs/RULES_CORE.md`, `HANDOFF.md` (2,872 lines, ledger + per-round notes).

---------------------------------------------------------------------------------------------------

---------------------------------------------------------------------------------------------------
---------------------------------------------------------------------------------------------------
## 2026-11-02 SM2 oddities A/B/C (metal/rough, cloth rest-fit, hub lights) — after tip e937708

Owner brief: deep root-cause + move decisions into core/sidecar; implement PROVEN/strong INFERRED only; no game launch.

### Root causes (labels)
| Area | Root cause | Label |
|---|---|---|
| **A** metalness/roughness UseSpecMap | Spec-map channel flags live in CPU CB OBJ_MTLBLEND_LAYER_CONST[15].x; writer still **not found**. Decision was duplicated in import_sm2_materials.py only so preview/UE could drift. | **HEURISTIC** rule (strong **DATA**: armory 128197 0x2000/0x40 bound-spec correlation; chameleon flags 0). Closing evidence = REA store into CB byte 336 / reg 21. |
| **B** cloth rest-fit | Rest-fit floor/shallow knobs lived as UE CVars + scene A/B scripts (sm2.RestFit*), not in physics.json, so reimport lost commissar coat overrides. Algorithm itself is **HEURISTIC** (R76-R97k). **F45** motion vs in-game still unverified. | **HEURISTIC** (A/B). F45 needs owner observation only. |
| **C** hub lighting | Placement math (ARGB, K*Intensity, spot->point, YZ swap) lived in one-off ue_r98_hublights.py. Only ARGB order **PROVEN**; K=10 **HEURISTIC FIT**. Map_Floor EV -5->0 already landed historically (d7e4e92). | ARGB **PROVEN**; K **HEURISTIC FIT**; spot->point **INFERRED** (no direction in TSV). |

### What changed (this pass)
- **A:** UseSpecMapPolicy -> materials.json useSpecMap / useSpecMapEvidence; import_sm2_materials.py prefers sidecar; viewer GlPreview uses the same policy.
- **B:** RestFitPolicy -> physics.json ue.chaosCloth.restFit + collision.restFit*; SM2PhysicsSetup AddCollision prefers sidecar over CVar defaults (CVars remain A/B).
- **C:** LevelLightUe (C#) + tools/ue/ue_level_lights_core.py; ue_r98_hublights.py calls the shared core.
- Tests: Sm2OdditiesPolicyTests; test_level_lights.py hub core; fixed brittle EffectTests.HiddenSourceStillSpawnsAttachedChildren.
- Dotnet: Assets.Tests **433** pass, Extractor.Tests **43** pass. Python test_level_lights 6/6.
- UE before/after hub PNGs: **not captured** this pass (editor free but full re-place of ~1800 lights + visual judgment left for owner after reimport; no game). Plugin C++ change needs UE plugin rebuild before PIE cloth check.

### Commits (origin tip **eddf91e**)
| SHA | What |
|---|---|
| [98c6886](https://github.com/Baegovda/ForgeLens/commit/98c6886) | EffectTests HiddenSource fixture |
| [4960763](https://github.com/Baegovda/ForgeLens/commit/4960763) | SM2 A UseSpecMap sidecar |
| [5db0ee5](https://github.com/Baegovda/ForgeLens/commit/5db0ee5) | SM2 B RestFit sidecar |
| [e9ad274](https://github.com/Baegovda/ForgeLens/commit/e9ad274) | SM2 C hub lights core |
| [eddf91e](https://github.com/Baegovda/ForgeLens/commit/eddf91e) | HANDOFF docs |

### ForgeLens_latest
- Single-file win-x64 publish from tip **eddf91e** → `Desktop\SM2_Work\Bin\ForgeLens_latest\ForgeLens.exe` (~48.0 MB), also `Bin\ForgeLens_eddf91e`.
- Previous renamed (kept): `Bin\ForgeLens_latest_old_eddf91e` (was 1461bbf build).

### Still HEURISTIC / open
1. UseSpecMap CB writer (F49 / SYMBOL_DB materials).
2. RestFitFloor / ShallowFrac values (not from retail); F45 physics motion.
3. Hub intensity K=10 FIT; spot direction if a future dump adds it.
4. PoE2/Wukong tracks untouched.

## 2026-11-02 정리 (KST ~01:55)

Owner-approved cleanup on Revaptor-FX. **This section wins** over older STATUS.md / AGENTS.md content; those two are stale — follow this file.

### Tip
- Branch `cursor/sm2-pck-extractor-4420` tip: **`1461bbf`** (`1461bbf4404c2b2ad4f3c852b0020d1e018a895b`)
- Previous tip was `2bd616b`.

### Commits pushed this session
| SHA | What | Status |
|---|---|---|
| [`edf8e92`](https://github.com/Baegovda/ForgeLens/commit/edf8e9256273951bbb2965c10bbc0dd5bccb3dd2) | **P1** PoE2 M2 H VFX leftovers (glow Add, FXLit tint, SIM shade, gradient white, radial glow fade, ground depth, short fit) | C# Release build OK (warnings only) |
| [`760c055`](https://github.com/Baegovda/ForgeLens/commit/760c055cc6b33abbbb30e52f9752658ff1d8ea0c) | **P2** SM2PhysicsSetup `SetBodiesKinematic` + `ListBodies` (BellCrow); `ue_poe2_pie` kin API; `ue_poe2_quit.py` | **code only, UE plugin build/PIE unverified** |
| [`1461bbf`](https://github.com/Baegovda/ForgeLens/commit/1461bbf4404c2b2ad4f3c852b0020d1e018a895b) | **Wukong M1 draft** BmwGlb + BmwWindow + toolbar button + `ue_bmw_import.py` | C# build OK; draft only. Pak readers **not** committed (hard-coded AES key literals in `tools/bmw/Program.cs` + `bmw_pak.py` — left untracked on `fl_bmw`; use `BMW_AES_KEY` / `docs/WUKONG_M0.md`) |

### Excluded (left untracked on purpose)
- `docs/handoff/poe2_m2/viewer_fx2/` (~97 PNGs, ~15.5 MB capture dumps)
- `*.bak1` / `*.bak2` source backups under `src/`
- `fl_bmw/tools/bmw/Program.cs`, `bmw_pak.py` (AES key literals)
- No game data/binaries committed

### Editor lock
- File: `C:\Users\Revaptor_FX\ue_editor_busy.txt`
- Old: `poe2m3 btl25 deploy+editor launch 2026-10-31T18:16:42`
- Confirmed no `UnrealEditor*` process → set to: `editor-free 2026-11-02T01:52:10`

### ForgeLens_latest
- Path: `C:\Users\Revaptor_FX\Desktop\SM2_Work\Bin\ForgeLens_latest\ForgeLens.exe`
- Fresh `dotnet publish` from tip **1461bbf** (app code changed)
- Exe LastWriteTime (KST): **2026-11-02 01:52:25**, size 47997887
- Previous folder renamed (not deleted): `Bin\ForgeLens_latest_old_20261029`
- Also kept: `Bin\ForgeLens_1461bbf`, prior `Bin\ForgeLens_2bd616b`

### Remaining worktree state
- **PoE2\dev** (`cursor/sm2-pck-extractor-4420` @ 1461bbf, clean tracked): only untracked `viewer_fx2/` + `*.bak*` backups
- **fl_bmw** (branch `wukong-m1-draft` @ local draft base): untracked `tools/bmw/Program.cs`, `bmw_pak.py` only (AES keys)
- **ls_push**: push clone at tip

### P-list status (from §5; updated)
| P | Item | Status after 2026-11-02 |
|---|---|---|
| P1 | PoE2 VFX leftovers | **DONE** (edf8e92). Captures still untracked in viewer_fx2 |
| P2 | BellCrow kinematic C++ API | **code pushed** (760c055); UE plugin rebuild + PIE still open |
| P3 | BongThumbLive dots spinner | unchanged / pending |
| P4 | PoE2 levels beyond hideout | unchanged / pending |
| P5 | Wukong M1 | **draft UI+Glb+import** (1461bbf); pak decrypt tools not in repo (keys) |
| P6–P10 | as before | unchanged |
| P11 | PoE2 → Niagara | **owner go/no-go still needed** |

### Docs note
- **This file still wins.** A 2026-11-02 docs-refresh pass updated `AGENTS.md`, `START_HERE`, `BOT_TRANSFER`, `RULES_CORE`, `SUBAGENT_PREAMBLE`, `STATUS.md` (current summary + historical SM2 ledger), `README`, root `HANDOFF.md` (append-only pointer), `POE2_M*.md`, `WUKONG_M0.md`, `ROADMAP`, `WORKFLOW` for the stale items listed in the owner brief. Re-read those files; do not resurrect interim-% / `sm2-20` / "Niagara out of roadmap" / "`/Game/SM2_Work` only" / "BTL not ours".
- **master cleanup remains an owner decision** — docs refresh pushes only to `cursor/sm2-pck-extractor-4420`.

### Open owner questions
1. **Niagara P11** go/no-go?
2. Next focus: **PoE2 polish** vs **Wukong M1** (finish pak reader without committing key literals)?
3. **Chaplain test folder** deletion (still needs moving ~34 materials first)?
4. **master** cleanup vs keep working branch (`cursor/sm2-pck-extractor-4420` ≈ **372 ahead** / 1 behind `master` as of this pass)?

## 0. First 30 minutes checklist (new bot)

1. **Connect the PC.** Ask the owner (Korean, one line) to add his Windows PC to your account. `ListMachines` -> pick the one named **Revaptor-FX** (Windows user `Revaptor_FX`). The old machineId `d3fb99ad-25ca-45c1-81af-41e5ca09104a` belongs to the OLD account; yours will differ. Smoke test (PowerShell via Shell + machineId):
   `hostname; git -C $env:USERPROFILE\ls_push log --oneline -3; Get-Content $env:USERPROFILE\ue_editor_busy.txt; Get-Process UnrealEditor*,ForgeLens* -EA 0`
2. **Save memory** (your account has none): the facts in sections 1 and 2 below (owner, tone, rules, never launch the game, core concept, paths).
3. **Read** (on the PC, repo `C:\Users\Revaptor_FX\Desktop\SM2_Work\PoE2\dev` or `ls_push`): this file -> `docs/RULES_CORE.md` -> `HANDOFF.md` from line ~2845 ("SM2 FINAL STATUS") to the end -> `docs/POE2_M2.md`, `docs/POE2_M3.md` -> `docs/WUKONG_M0.md`.
4. **Check state before touching anything:** `git -C ...\PoE2\dev status -sb` (other workers' uncommitted files may be there - see 6.2), editor lock file, `window_watch\heartbeat.txt` age (< 30 s), `window_watch\NEEDS_ACTION.txt` (should not exist), game not running (`Get-Process "Warhammer*","PathOfExile*","b1-Win64*" -EA 0`).
5. **Nothing is running** at 17:40 KST: the VFX plate job (50dbf5b) and physics tuning (0ed49e4) finished and are pushed. Start with the todo in section 5. The dev tree only has untracked `.bak1` source backups from the VFX worker (keep them).
6. **Send the owner one short Korean 해요체 message**: taken over, what you checked, what you start now. Then work; report only the final result.

---------------------------------------------------------------------------------------------------
## 1. The owner and how to talk to him

- Name used in chat: **Exodus** / "Guilliman". GitHub **Baegovda**. Windows user `Revaptor_FX`, PC **Revaptor-FX** (i9-14900K, 128 GB RAM, RTX 4090 [I, from BOT_TRANSFER]). Time zone **Asia/Seoul (KST)**; always give times in KST. Often writes from his phone. Often watches Chzzk streams while you work (fine).
- **Language/tone:** short Korean **해요체**. **Result first**, then: what was done, what was verified, what was NOT verified. No filler.
- **Never state a number you did not measure** (say "아직 안 쟀어요" / "재 본 적 없어요"). Never guess times.
- **Admit faults at once and plainly** (e.g. "제 정리가 부족했어요").
- **Dislikes needless questions.** Decide yourself; ask only what truly needs his word (deletions, things only he can do), at the end of the message.
- Terms: **기드라 = Ghidra, 레아 = REA, 봉썸 = BongThumbLive**. "게임끝" = PC free. "실행해줘" = launch the deployed ForgeLens exe (`Desktop\SM2_Work\Bin\ForgeLens_latest\ForgeLens.exe` [V exists]; newest builds also in `Bin\ForgeLens_r95b`, `ForgeLens_r95`).
- Docs, commits, code: **English**. Bot-to-bot: English. User quotes stay Korean.
- **Report only final results** (no interim progress messages unless he asks "현재상황"/"상황보고" -> then answer at once with what is running and what is done). (This supersedes the older RULES_CORE "interim reports with % + ETA".) [I, owner instruction in the old chat]
- When he is asleep ("잔다", "끝까지 작업 끝내놔 결과만 알려주고") keep working through the whole list and send one result message.

---------------------------------------------------------------------------------------------------
## 2. Standing rules (all still in force)

### 2.1 Work rules
1. **Never stop midway.** Finish the list. If a worker/executor dies (they die after ~15-50 min, or when the PC drops offline), **restart it yourself** from its notes, the git log and the uncommitted files. Do not report "it stopped" as a result.
2. **Do everything yourself**, including editor/PC popups. When a test needs real mouse/keyboard input, block the owner's physical input with `tools/input_block.ps1 -Script <test.ps1> -ArgsJson '{...}'` (default `-Mode hook`, 60 s watchdog `-MaxBlockSec`, always unblocks, restores cursor; covering windows minimised and restored, never closed). **Check the foreground window/pid before every click.** [V file exists]
3. **NEVER launch, focus, send input to or otherwise control any game (Space Marine 2, PoE2, Wukong, anything) - not you, not any worker (owner t265u).** In-game screenshots are taken **only by the owner**. Use game data (read-only), the ForgeLens viewer and Unreal. Never launch the UE editor while a game is running. [V in RULES_CORE]
4. **CORE CONCEPT (owner t247u/t248u):** every supported game (SM2, PoE2, Black Myth: Wukong, Stellar Blade, Elden Ring) = **the SM2 feature set with only the game changed**: models, skeletons, animations, textures, materials, physics/cloth, levels/props, the ForgeLens viewer, Unreal import under **`/Game/ForgeLens/<CODE>/`** (SM2, POE, BMW, ...). One shared pipeline; only game-specific readers differ. **PoE2 skills (effects/animations) must be viewable in the viewer.** [V top of RULES_CORE/SUBAGENT_PREAMBLE]
5. **Game order:** SM2 (done except items in 4.1) -> **PoE2** (current) -> **Black Myth: Wukong** -> **Stellar Blade** -> **Elden Ring** (not UE). PoE2 only, never PoE1 (owner t215u).
6. **All ForgeLens UI strings in English** (no Korean in the UI).
7. **Never delete without the owner's word.** Exception granted 10/30 (R61): generated assets under `/Game/SM2_Work/` may be cleaned freely after a referencer check, logged in `docs/handoff/CLEANUP_DELETED.tsv` [V RULES_CORE]. Everything else needs a yes. History: owner said "대기중인거 전부 끝내" (t276u) which covered the old orb effects -> **deleted 2026-10-31 15:05 KST** with backup `K:\SM2_Backups\predelete_20261031_1505\` (211 files) [V HANDOFF R98]. The chaplain test folders were **kept** (still referenced; see 4.1). Source/PC files: back up as `.bakN` (next free N, never overwrite).
8. Fixes are generic rules, never per-character patches. No untrusted executables. No Discord actions. Game data read-only.
9. **Repo privacy** [V `gh repo list` 2026-10-31]: PRIVATE = ForgeLens, BongThumbLive, BongLodge, BongHub. PUBLIC = BongGIF, BongExtend, IndexV2, PIPBONG, PIPBONG-releases, PipEL.A, PipEL.A567, TrafficMonitor, TrafficMonitorPlugins, TrafficMonitorSkin, GGPK_Custom, LibGGPK3. Nothing was changed; do not change visibility without the owner.
10. **LibGGPK3** (AGPL-3.0, aianlinb): verbatim private copy allowed in a ForgeLens-named folder with notices (`ForgeLensPoe/ForgeLens.Ggpk3/`, commit e5872fd); remove before any distribution [V POE2_M0]. Upstream clone on PC: `Desktop\SM2_Work\PoE2\lib_upstream`.
11. **No secrets in docs/repo.** EngineWire token lives in `%USERPROFILE%\.enginewire_token` (never print). PC git/gh is logged in as Baegovda. The Wukong community AES key is written in `docs/WUKONG_M0.md` (public community key; not repeated here).

### 2.2 Unreal editor rules
- Project **`K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject`**, UE **5.7.4** at `C:\Program Files\Epic Games\UE_5.7`. [V paths exist; UnrealEditor pid 94632 was running at 16:00]
- **Route:** editor running + port 3000 open -> EngineWire MCP (`McpAutomationBridge` 0.5.30, `http://127.0.0.1:3000/mcp`) via `Desktop\SM2_Work\Scripts\uerun.ps1 <ABS script> <comma,args>` -> `C:\Users\Revaptor_FX\enginewire_call.py` (fallback `uemcp_call.py`). Editor not running -> headless `UnrealEditor-Cmd -run=pythonscript ... -unattended -nullrhi -AutoDeclinePackageRecovery`. Never both. State the route in reports.
- **WARNING: `uerun.ps1` writes a SHARED `C:\Users\Revaptor_FX\ue_args.json`.** Two workers at once -> one ran the other's script (incident in POE2_M2 "Incident"). PoE2 uses `tools/poe2/ue_poe2_call.ps1` (private args file) and `tools/poe2/ue_poe2_go.ps1` (lock-aware). Use private-args callers for any parallel work. [V]
- **Editor lock:** `C:\Users\Revaptor_FX\ue_editor_busy.txt`. Idle = `editor-free <time>`. Take it with a named owner + purpose (e.g. `poe2m3 level+pie 2026-10-31T15:57:37`), keep it short, always release to `editor-free`. Wait for free; never steal. [V file content at 15:58 = `poe2m3 level+pie 2026-10-31T15:57:37`, i.e. held by the physics worker]
- **Launch flags:** `-unattended -nosplash -AutoDeclinePackageRecovery`. Restarts are pre-authorised (save own assets first). Stop any PIE you start.
- **Window watcher (owner t231u/t262u):** `C:\Users\Revaptor_FX\window_watch\` (`heartbeat.txt`, `events.jsonl`, `shots\`, `watcher.log`, `crash_*.txt`, `NEEDS_ACTION.txt`). Code `tools/watch/{watcher.ps1,start.ps1,stop.ps1,status.ps1,test_dummy.ps1}`; autostart shortcut `ForgeLensWindowWatch.lnk` in Startup. 250 ms poll. **Crash Reporter windows are closed within 5 s** (WM_CLOSE = Close Without Sending; measured detect->closed 105 ms), crash summary saved, `NEEDS_ACTION.txt` appended. Message Log windows auto-closed. Save Content / other modals are only logged + screenshotted. Every worker: check `NEEDS_ACTION.txt` at start and around each editor step; heartbeat older than 30 s -> `tools\watch\start.ps1`. [V heartbeat 15:58:26 fresh; one crash file `crash_20261031_035957.txt`]
- **Save Content dialog / saving:** save ONLY assets under `/Game/SM2_Work` or `/Game/ForgeLens`. Never save the owner's own content (`/Game/Work/*`, `Lightning_01`, his own skeleton) or another worker's dirty maps. Use `tools/poe2/ue_poe2_import_safe.py`-style "save own packages only" (the old `save_dirty_packages(True,True)` saved others' maps). [V POE2_M3]
- Captures: render targets must be **RTF_RGBA8 / RGBA16F non-sRGB** (see 6.1).

### 2.3 Git / push rules
- Repo **Baegovda/ForgeLens** (private), branch **`cursor/sm2-pck-extractor-4420`** (default). Never force-push. One push at a time (named lock). ff-only.
- PC push clone: `C:\Users\Revaptor_FX\ls_push`. Push script: `Desktop\SM2_Work\Scripts\mtl_fix\push.ps1` (resets ls_push to origin tip, `git am -3` every `%USERPROFILE%\0*.patch`, pushes ff-only, prints `TIP <sha>`; `AM FAILED` -> rebase and redo). [V script path referenced; behaviour I from BOT_TRANSFER]
- PoE2 dev tree helper: `Desktop\SM2_Work\PoE2\pushstep.ps1 -Msg "<msg>" -Paths <files>`: adds ONLY the given paths, commits, rebases on origin, format-patch -1 to %USERPROFILE%, runs push.ps1, then `reset --keep` to origin. [V script read]
- Box route (old account): box can't fetch/push; `tools/re/box/push_lock.sh acquire <name>` -> format-patch -> CopyFromBox `C:\Users\Revaptor_FX\0NNN-*.patch` -> push.ps1 -> `refresh_origin.sh` -> release. On your new box, set the repo up from a PC bundle (BOT_TRANSFER 0.2). Simplest: work and commit on the PC.
- Other repos (owner's GitHub, `gh` logged in on PC): **BongThumbLive** clone `C:\Users\Revaptor_FX\AppData\Local\Temp\BongThumbLive_repo` (local tip 58e39fe; origin tip **ce19434**, so `git pull --ff-only` first) [V]; **BongLodge** `C:\Users\Revaptor_FX\BongLodge_src\BongLodge` (main, tip b3ad469, in sync) [V].
- No routines exist (routine `sm2-20` was deleted by the owner, t245u). RULES_CORE's routine line is stale. [I]

### 2.4 Stale lines in older docs (superseded)
- RULES_CORE "BongThumbLive is NOT ours" -> stale: the owner later asked this bot to fix BTL (spinner); BTL is in scope when he asks. [I]
- RULES_CORE "Unreal: work only in /Game/SM2_Work" -> now also `/Game/ForgeLens/<CODE>/` (SM2 assets were migrated to `/Game/ForgeLens/SM2/...`; the hub map is still `/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge`). [V paths in HANDOFF R98]
- "Niagara only after asking" -> still true for big Niagara ports; the owner asked about the SIZE of a PoE2->Niagara conversion (t278u) but did not order it. [I]
- "Interim reports with % + ETA" -> superseded by "final results only".
- "Never ask for in-game captures" -> still: never ask; but owner-only checks (F45, muzzle/aim) wait until he volunteers captures.

---------------------------------------------------------------------------------------------------
## 3. Architecture and pipeline

- **ForgeLens** = multi-game asset browser/viewer (Avalonia, .NET 8) + exporter (glTF/FBX/PNG + JSON sidecars) + Unreal importers (Python in editor) + UE plugin `SM2PhysicsSetup` (bone physics / cloth rest-fit). Code map [I BOT_TRANSFER]: `src/LibSaber.SM2.Assets` (formats; PoE2 in `Games/Poe2/` e.g. `Poe2Reader`, `Poe2Particles.cs`, `Poe2Terrain.cs`), `src/LibSaber.SM2Browser` (GUI, exe `ForgeLens.exe`, `MainWindow.axaml.cs`, `SettingsWindow.cs`, `PerfBudget.cs`), `src/LibSaber.SM2Extractor` (CLI), `tools/ue/*.py`, `tools/ue/SM2PhysicsSetup`, `tools/poe2/*`, `tools/re/*`, `tools/watch/*`, `tools/vfx*`, `tools/level/*`.
- **Per-game readers:** SM2 (.pak/.td/.tpl/.sfx/.cls...), PoE2 (GGPK via LibGGPK3 copy + Oodle `oo2core_9_win64.dll` taken from `C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\DotNET\AutomationTool`; formats .sm/.smd/.fmt/.ast/.mat/.dds/.ao/.pet/.arm/.tdt/.tgt/.tgm, tables .datc64; reference `adamthedash/poe_data_tools`). Wukong reader not written yet.
- **PoE2 CLI switches** (ForgeLens exe): `--poe2skills`, `--poe2export <prepared> <out> <mesh.sm> <rig.ast> <clips|*>` (`FL_POE2_ALLCLIPS=1` = all clips), `--vfxue poe2:<prepared> - - <out> <effect.ao>@start:end:step`, `--poe2level <prepared> <out> <room.arm> [<ggpk> <tool dir>]`; viewer PoE2 window tabs incl. Skills and Levels; test hook `FL_POE2_LEVEL=<arm>`. Mesh prep: `tools/poe2/mesh_prep.ps1`, `prep.ps1`. [V POE2_M2/M3]
- **Unreal bridge:** EngineWire / McpAutomationBridge 0.5.30 on 127.0.0.1:3000 (token file). Scripts run inside the editor's Python.
- **PoE2 UE scripts** (`tools/poe2/`): `ue_poe2_import.py`, `ue_poe2_import_safe.py` (own saves only, no deletes), `ue_poe2_check.py`, `ue_poe2_probe.py`, `ue_poe2_physics.py` (phys_ bones -> SM2 physics JSON -> `unreal.SM2PhysicsSetupLibrary.setup_from_json`), `ue_poe2_vfx_frame.py` (frozen-frame VFX, limited to /Game/ForgeLens/POE), `ue_poe2_level.py`, `ue_poe2_pie.py` (PIE swing check), runners `ue_poe2_call.ps1` (private args), `ue_poe2_go.ps1` (locked), `ue_poe2_run.ps1`; Blender checks `bl_check.py`, `bl_stretch.py`; `poe2_clicks.ps1` (real-click viewer test). [V listing]
- **Workers/executors:** the old bot ran executor subagents per job. Typical failures [I from experience in this chat]: worker dies after ~15-50 min (resume from notes/commits); the PC goes "temporarily unreachable" for 1-2 min (retry up to ~30 min; run long jobs as detached PowerShell scripts that write logs, then poll logs); two workers sharing `ue_args.json` or the push flow collide (use named locks); workers ending early with "done" while items remain (re-check against the list). Give every worker `docs/SUBAGENT_PREAMBLE.md` + the game rule + editor-lock rule + "only commit your own files".
- **PC tools:** Ghidra 12.1.4 + JDK 25 + Node 24 + rea-agents 6.8.0 in `Desktop\SM2_Work\Tools\re_env\` (`g.ps1`, `rea_env.ps1`, `ghidra_serve`, `proj_sm2`) [V listing]; Blender 4.3 and 5.0 in `C:\Program Files\Blender Foundation\` [V]; RenderDoc 1.46 in `SM2_Work\Tools` (nightly 1.47 at `C:\Users\Revaptor_FX\Tools\RD_nightly\` [V exists]); VS 2022 for plugin builds [I]. REA/GhidraServe: manual start only, never kill a running one.

---------------------------------------------------------------------------------------------------
## 4. Project status (as of 2026-10-31 ~16:00 KST)

Origin tip of ForgeLens at writing: **`db0e1db`** "PoE2 M3: UE space hideout captures, PIE swing tune, docs" [V ls_push log]. (The dev tree shows the same two M3 commits under other hashes 3f8f511/8cd0597 = local pre-rebase copies; `pushstep.ps1` resets them.)

### 4.1 SM2 (Warhammer 40,000: Space Marine 2) - essentially finished
Done (commits on origin) [V git log]: cloth measurement + rest-fit (R90-R93, 5033e39 etc.), sRGB capture fix (R93), viewer memory/threads budget + Settings window (R95/R95b), commissar coat per-cloth override (R97k, 7b56d75), muzzle flash soft render (R97o, b1748f2), chaplain own skeleton + Titus cloth helper bones (R97p/q, c8b1730), hub sky/env (R97r/u, 32a5067, 12304bd), missing-bone ensure fix (R97v, 5f42061), final captures (R97w, 59114d1), orb Niagara + runes + bloom (R97s-x, 338b721, 800700c, 666acbb), hub sun from data ARGB proven (R97y, dbfd39f), final status (4503668), orb deletion record (f9d6909), orb bloom card + Map_Floor EV -5->0 (R98a, d7e4e92), 1,821 of 1,824 hub point/spot lights from game data (R98b, 21eaf69), tracker audit (R98c-f, 01bd403), window title 0.97.0 (f4dc100).
Measured results [V HANDOFF]: orb ring average 201,180,216 vs game 194,177,217; hub 4-view average 17,30,34 vs game 22,29,32.
Remaining:
- **F45** physics motion vs game - needs owner's in-game captures (don't ask; wait).
- **Muzzle flash position / aim pose** (R51) visual check - needs owner's captures.
- **F49 REA:** writer of the UseSpecMap flag (`OBJ_MTLBLEND_LAYER_CONST[15].x`) not found; switch stays HEURISTIC. Multi-hour Ghidra/REA work, no new lead.
- Orb: game's hot yellow-white centre not reproduced; bloom card faces only the capture camera (not a billboard).
- Hub: intensity scale K=10 and fill light 3 are FITS; spot lights placed as points (no direction in data), no shadows; red slightly low.
- **Chaplain test assets kept**: `/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_chaplain` (used by 8 r90_cloth test meshes) and `Tests/chars_phys/cc_chaplain` (its ~34 MI_* used by main `Characters/cc_chaplain/cc_chaplain`). To delete, first move those materials; owner said keep (15:06). [V HANDOFF R98]
- Older small items [I]: F27 artificer presets, pauldron colour-section matching (HEURISTIC), commissar coat beyond the override, model UV scroll, cinematic sequences.

### 4.2 PoE2 (Path of Exile 2, Kakao client) - in progress
Game: `D:\Daum Games\Path of Exile2\Content.ggpk` (159.9 GB, GGPK v3, bundles inside) read-only. [V POE2_M0]
- **M0** (5c48c8f [V]): GGPK probe, file list, formats.
- **M1** (7d9f511, d4a154b): Poe2Reader sm/smd/fmt/ast/mat/dds -> shared mesh/clip model; skills list; .ao/.pet effect reader.
- **M2** (30e4683 ... 83594c7): real skill->effect links (629/1147 skills), orientation fix (**PoE left-handed, -Y forward; reflection + winding flip**), viewer PoE2 UI (English), particle sim via SM2 EffectDefinition, 7 rigged monsters (bellcrow 73 clips, wolfremake 30, basicskeleton 252, wingedrhoa 10, harpy 57, lichboss 42, ramgiant 46), UE import `/Game/ForgeLens/POE/` (M_Poe2Lit masters, MICs), 4 effects as 19 frozen frames in `/Game/ForgeLens/POE/VFX` (mesh+material, NOT Niagara), phys_ bones physics bellcrow (20) and lich (7). Harpy attack_01 finger stretch up to 2.24x = game's own animation (left as is).
- **M3** (bc4aec1, 4973954, db0e1db): `.tgm` reader (24/24 tiles), `.arm` room assembly, space hideout 961 tiles + 4 doodads, viewer Levels tab, UE `/Game/ForgeLens/POE/Levels/space_hideout` + `L_space_hideout` (celestial floor material is an approximation `M_Poe2Celestial`), cart rhoa (10 clips, 17 phys bones), PIE swing tune t2 (world 0.4, damp 0.9, stiff 400, dampDrive 40, swing 0.5): all bones move, but **lich tail and BellCrow bells still pop -> not verified good; heuristic values**. [V POE2_M3]
- **M2 G - VFX plates removed + real viewer captures (DONE, 50dbf5b) [V git log]:** owner saw a semi-transparent white plate behind the lightning sphere and other effects (t272u-t274u, wanted it REMOVED, not transparent). Removed: untextured cards, material-texture sprites, huge glow cards, XYLock ground flashes; hooks `FL_POE2_EFFECT` / legacy-plate toggle. Real ForgeLens window captures of **16 skills** + before/after sheets in `docs/handoff/poe2_m2/viewer_fx/` (`skills_sheet_1.png`, `skills_sheet_2.png`, `before_after_plate.png`, `bench_before_after.png`, scripts `fxg_cap.ps1`, `fxg_sheets.ps1`, list `fxg_skills.txt`). **Remaining:** Frost Bomb has a cut (hard) edge, Glacial Bolt shows dark blobs, several effects are barely visible, smoke is flat colour.
- **M3.4b - physics tuning (DONE as far as possible, 0ed49e4) [V git log]:** 8 per-mesh tune sets, front/side PIE camera. Cart rhoa OK; lich tail about half as jumpy; **BellCrow bells still pop** - root cause: the top bell bones are simulated 80-126 cm from their parents (lever arm). Fix needs a NEW function in the `SM2PhysicsSetup` C++ plugin (physics bodies are not exposed to Python) to make those top bones kinematic/follow animation, then plugin rebuild + editor restart (restart pre-authorised; take the editor lock; game must not run). Values remain heuristic.
- Not done: tile rotation, ground blending/masks, decals, lights/.env, other areas; smoke is flat colour; stormblast octagon only in the viewer; no subsurface/specular; cart rhoa/others physics not seen against the game.
- **Niagara conversion** (owner asked for an estimate, t278u, and it was offered as the NEXT job after the current work; he has not said go yet): large, no time measured. Steps: (1) converter EmitterDef -> Niagara emitter (spawn rate, lifetime, velocity, size/colour curves, flipbook, mesh particles, lights) on 5 representative effects; (2) compare with the viewer; (3) automated compile + validation (EngineWire-made emitters needed an editor open+compile in SM2 orb work, R97t); (4) extend to the most-used effects. Not started; do only if the owner says go.

### 4.3 Black Myth: Wukong (code BMW) - M0 done (f4dc100); M1 draft (1461bbf)
Install `D:\Steam\steamapps\common\BlackMythWukong` (app 2358720, ~150 GB), exe `b1\Binaries\Win64\b1-Win64-Shipping.exe`, paks `b1\Content\Paks\pakchunk*.pak` (no IoStore). Custom UE 5.0 pak **v11 + extra int16**, **AES-256 encrypted index**, **Oodle**. Community AES key + usmap in `docs/WUKONG_M0.md`; tools must use **`BMW_AES_KEY`** (do not commit key literals). **M1 draft in-tree:** BmwGlb + BmwWindow + toolbar + `ue_bmw_import.py`. Pak decrypt readers with hard-coded keys left untracked. Key decrypt path still not verified in CI. Never run the game (no usmap dumping). [V WUKONG_M0 + 2026-11-02 정리]

### 4.4 Stellar Blade, Elden Ring - not started. Elden Ring is not UE (FromSoftware formats). [I]

### 4.5 BongThumbLive (봉썸, UE 5.7 plugin, private repo)
Plugin installed at `K:\Unreal Projects\_Plugins\BongThumbLive` (not a git clone) [V]. Repo clone `%LOCALAPPDATA%\Temp\BongThumbLive_repo` (HANDOFF.md, NEW_BOT_PROMPT.md inside). Origin tip **ce19434** "Dots spinner: real filled rotating dots (was short bars); not yet built/verified live" (2026-10-31 14:53 KST) [V gh api]. **Next: build the plugin, deploy, check the dots spinner live in the editor (8 round dots rotating, trailing ones smaller/dimmer), capture, report.** Previous: pkg24 spinner colour/style/speed (58e39fe).

### 4.6 BongLodge (UE editor window placement plugin, private repo)
`C:\Users\Revaptor_FX\BongLodge_src\BongLodge`, tip b3ad469, in sync with origin [V]. Per `LIMITATIONS.md` (10/30 22:40) [V]: 6.1 PASS, 6.2 PASS, 6.3 PASS, 6.4 PASS, 6.5 fixed (DisplayFusion profile cause), 6.6 PASS (undock), 6.7 identity = tab-id set. **Unverified:** real monitor unplug/replug (only simulated), dock-back by drag, 6.5 residual ~120 ms 0,0 flash (not seen again in 8 launches), "shared Root" case. Test project BLTest, scripts in `BongLodge_src\tests`.

### 4.7 hologram_planet split skeleton - done (fixed 10/29-10/30, audit 0 split); remove from todo. [I from SM2 worker report]

---------------------------------------------------------------------------------------------------
## 5. Todo (priority order) with exact next steps

| # | Item | Next step |
|---|---|---|
| P1 | PoE2 VFX leftovers | Frost Bomb cut edge, Glacial Bolt dark blobs, barely visible effects, flat smoke; re-capture with `viewer_fx/fxg_cap.ps1` + `fxg_sheets.ps1`, send sheet |
| P2 | BellCrow bell pop | add SM2PhysicsSetup C++ function to set named bodies kinematic (follow anim) for the top bell bones, rebuild plugin (VS 2022), restart editor with lock, PIE check via `tools/poe2/ue_poe2_pie.py`; lich tail further smoothing optional |
| P3 | BongThumbLive dots spinner build + live check | pull ce19434, build with UE 5.7 (editor closed or plugin hot-reload rules per BTL HANDOFF), capture |
| P4 | PoE2 levels beyond the hideout | tile rotation from .arm, ground blending, one outdoor area |
| P5 | Wukong M1 | pak reader + index decrypt test with the key from WUKONG_M0, file count |
| P6 | Stellar Blade M0, Elden Ring M0 | format surveys like WUKONG_M0 |
| P7 | SM2 leftovers | F49 REA UseSpecMap writer (batch REA session); F45 + muzzle/aim only with owner captures |
| P8 | Viewer: Unreal-style UI batch (no gizmo) | was marked in progress in the old todo; state [U]: no commit with UI/Unreal-style in its message since 10/30 20:00 [V]; it was likely never started - confirm scope with the old notes or the owner's original request before starting |
| P9 | Memory budget gaps | CPU mesh copies and GL textures are not budgeted (R95b) |
| P10 | BongLodge unverified items | real monitor removal, dock-back, shared Root |
| P11 | PoE2 -> Niagara (offered as next job) | when the owner says go: converter on 5 effects -> compare with viewer -> extend by skill usage (4.2) |

---------------------------------------------------------------------------------------------------
## 6. Key technical findings

1. **Washed-out Unreal captures = double gamma** [V HANDOFF R93]: capture RT was `RTF_RGBA8_SRGB` while FINAL_COLOR_LDR is already display-referred. Use `RTF_RGBA8` (or RGBA16F non-sRGB). Fixed in `ue_r92_floor`, `ue_r90_cloth`, `ue_r72_mats`, `ue_r67_phys`, `ue_r63_cloth`, `ue_pie_phys` [I list]. Exposure was never the main cause.
2. **SM2 colour order is ARGB**: ColorCtrl `[A,R,G,B]` PROVEN (R97y); light colours used as sRGB -> (c/255)^2.2 like the game packer. Position SM2 m (x,y,z) -> UE cm (x,z,y). Light falloff P0.w/(d^2+P0.w) and attenCurveParam -> P0.w PROVEN (10/29).
3. **PoE2 is left-handed, -Y forward**: reflection + triangle winding flip on import (M2 step B).
4. **Commissar coat:** global ShallowFrac change harms others; per-cloth CVar `sm2.RestFitShallowOverride` default `coat_SIM_01=0.04;straps_SIM_01=0.04` -> death pen 68 -> 24, others unchanged (R97k).
5. **.td non-texture params** are applied in the importer (R50 f839c9e, R97 e04be15); second_spec metal tint + anisotropic layer done (19c7eb0, c1c4de0, 1c892ee).
6. **Viewer memory budget** (PerfBudget.cs): Auto threads = all logical cores; Auto memory = 50 % RAM; enforced only at texture decode (reservation, mip drop); CPU mesh copies / GL textures not budgeted. `SM2_THREADS`, `SM2_MAXMEM` env overrides.
7. **Map_Floor PPV exposure** was left at -5 -> reset to 0 (R98a). Check exposure before judging brightness.
8. **PoE2 secondary motion** = `phys_*` bone names only; no cloth data, no per-bone stiffness found.
9. **.tgm layout** PROVEN (POE2_M3 M3.1). Tile = 250 units (INFERRED).
10. **ForgeLens rewrites settings.json on exit** -> back up before UI tests, restore after [I].
11. Avalonia window has no UIA tree -> click by coordinates from window screenshots, check foreground first (R97l).
12. Editor crashes 01:54/03:29 (10/31) = D3D11 CreateSwapChainForHwnd 80070005 when a new editor window opens; RTSS/Afterburner/DisplayFusion hooks loaded (correlated, not proven) -> launch with `-unattended -nosplash`.

---------------------------------------------------------------------------------------------------
## 7. Path map

PC (Revaptor-FX):
- `C:\Users\Revaptor_FX\Desktop\SM2_Work\` - work root: `Bin` (deployed builds, `ForgeLens_latest`), `Scripts` (`uerun.ps1`, `mtl_fix\push.ps1`), `Raw` (game-extracted data, e.g. `level_lights\`), `Export(s)`, `Captures(2)`, `Shots`, `Anim`, `Backup`, `Build`, `Research`, `Tools\re_env` (Ghidra/REA), `PoE2\` (dev tree `dev`, outputs `m0..m7*`, `ue_exp*`, `pushstep.ps1`, `lib_upstream`), `alerts.txt`, `rules_check.ps1`, `RULES_CORE.md` copy. [V listing]
- ForgeLens repos: `PoE2\dev` (active dev tree, has other workers' uncommitted files), `C:\Users\Revaptor_FX\ls_push` (push clone - don't edit by hand). Old worktrees `C:\Users\Revaptor_FX\fl_cloth90, fl_cloth91, fl_r54, fl_r95fid, fl_w4, fl66` - **don't touch `fl_w4`** (another worker's tree).
- UE project `K:\Unreal Projects\UE_VFX_57`; logs `Saved\Logs\UE_VFX_57.log`, crashes `Saved\Crashes\`. Plugins dir `K:\Unreal Projects\_Plugins\` (BongThumbLive, backups). Other UE projects `AnimThumbTest`; BongLodge test project BLTest [I].
- Backups: `K:\SM2_Backups\` (`predelete_20261031_1505`, `ForgeLens_premigrate_20261031`, `SM2_Work_premigrate_20261031`), also `K:\SM2_Backup` [V].
- Editor control: `C:\Users\Revaptor_FX\enginewire_call.py`, `uemcp_call.py`, `ue_args.json` (shared!), `ue_editor_busy.txt`, `.enginewire_token` (secret).
- Watcher: `C:\Users\Revaptor_FX\window_watch\`.
- Games: SM2 `D:\Steam\steamapps\common\Space Marine 2\client_pc` [V exists]; PoE2 `D:\Daum Games\Path of Exile2` [V doc]; Wukong `D:\Steam\steamapps\common\BlackMythWukong` [V doc].
- Bong repos: BTL `%LOCALAPPDATA%\Temp\BongThumbLive_repo`, BongLodge `C:\Users\Revaptor_FX\BongLodge_src\BongLodge`; other folders `BongHub`, `BongGIF`, `BongChzzk` (BongExtend), `PIPBONG`.

Box (old account, NOT transferable): `/workspace/...` clones, REA DBs, shader dumps. Rebuild only if needed (BOT_TRANSFER 0.3-0.5). The PC has everything required.

Repo docs: `docs/RULES_CORE.md`, `docs/SUBAGENT_PREAMBLE.md` (give to every worker), `docs/BOT_TRANSFER.md` (10/29 box rebuild recipe), `HANDOFF.md` (ledger), `docs/POE2_M0..M3.md`, `docs/WUKONG_M0.md`, `docs/handoff/` (START_HERE, STATUS, UNVERIFIED, VISUAL_CHECKS, CLEANUP_*.tsv, BACKUPS, image folders r96/r97/r98/poe2_m*), `tools/re/README.md` (tool registry).

### Useful commands
- State: `Get-Content $env:USERPROFILE\ue_editor_busy.txt; Get-Item $env:USERPROFILE\window_watch\heartbeat.txt | % LastWriteTime; Test-Path $env:USERPROFILE\window_watch\NEEDS_ACTION.txt; git -C $env:USERPROFILE\ls_push log --oneline -5`
- Watcher: `powershell -File <repo>\tools\watch\status.ps1` / `start.ps1`.
- Commit+push own files from dev tree: `powershell -File $env:USERPROFILE\Desktop\SM2_Work\PoE2\pushstep.ps1 -Msg "..." -Paths docs\X.md`
- Detached long job: `Start-Process powershell -WindowStyle Hidden -ArgumentList '-File','job.ps1' -RedirectStandardOutput log.txt`, then poll the log (survives PC drop-outs of the agent connection).

---------------------------------------------------------------------------------------------------
## 8. Open questions for the owner (ask only at the end of a result message)
1. Delete the kept chaplain test folders (needs moving ~34 materials of the main chaplain first)? Owner said keep (15:06) - ask only if relevant.
2. Start the PoE2 -> Niagara conversion (large; offered as next after current work)?
3. In-game captures for F45 physics and muzzle/aim pose, if he wants those closed (never ask him to; just note they wait).
4. Order after PoE2 polish: more PoE2 areas first, or Wukong M1?

---------------------------------------------------------------------------------------------------
## 9. Verification passes (this document)
See the bottom section "Pass log".

## Pass log (5 passes, 2026-10-31 KST)
1. Draft from RULES_CORE, SUBAGENT_PREAMBLE, BOT_TRANSFER, POE2_M0/M2/M3, WUKONG_M0, HANDOFF tail (R93-R98c-f), git log (origin, 443 commits), PC listings.
2. Paths: checked existence of SM2/PoE2 game folders, UE 5.7 + project, ForgeLens_latest exe, uerun.ps1, push.ps1, ue_args.json, token file (exists; not read), RD_nightly -> 5 [I] upgraded to [V].
3. Commits: every hash cited in sections 4 and 6 looked up in `git log origin/...`; 5c48c8f (M0) confirmed; found dev tree "ahead 2" = local duplicates of db0e1db/4973954 (noted).
4. Other repos: BTL origin tip ce19434 via `gh api` (local clone is behind at 58e39fe - noted); BongLodge tip/LIMITATIONS read; repo visibility via `gh repo list` (matches the old report). Removed the AES key from this doc (points to WUKONG_M0).
5. Gaps/stale rules: added 2.4 superseded lines (routine, BTL scope, /Game/ForgeLens, Niagara, interim reports), shared ue_args.json hazard, P8 UI batch has no commits [U], in-flight P1/P2 uncommitted files listed from `git status`.
Still [U]: BLTest project path; owner's exact scope for the Unreal-style UI batch; PC hardware specs (from 10/29 doc).
6. **Final pass (17:40 KST):** origin log re-read: 50dbf5b (VFX plates + 16 captures) and 0ed49e4 (physics 8 tune sets) present [V]; viewer_fx folder listing [V]; replaced the "in progress" P1/P2 text with results and new next steps; added the Niagara offer and the start-here banner. Re-confirmed cited SM2/PoE2/Wukong/BTL hashes d7e4e92, 21eaf69, f9d6909, bc4aec1, 4973954, db0e1db, f4dc100, ce19434 (from pass 3/4). Root cause of the BellCrow pop is from the worker's commit/report, not re-measured by me [I].
