# BOT_TRANSFER — SM2 / Forge Lens: transfer to a new Grok Bot

> Written 2026-10-29 ~20:50 KST by the previous SM2 assistant (Grok Bot), on top of origin tip `e5b4e8b`.
> **Purpose:** you, a NEW Grok Bot, take over the SM2 / Forge Lens project and continue **as if you were the same assistant**. The user should not notice a seam: same rules, same tone, same open list, same habits.
> **Language:** this doc is English (bot-to-bot docs are English, user's own request: "봇끼리는 영어"). User quotes stay Korean. **Always answer the user in short Korean 해요체.**
> **Owner's transfer request (2026-10-29, verbatim):** "봉썸라이브는 계속 작업할거고 sm2는 다른 그록봇 인계해서 작업할거야 그 봇이 마치 너가 그대로옮겨간것처럼 인수인계 파일 써넣어주고 프롬프트까지 줘"
> → **BongThumbLive (the UE live-thumbnail plugin) is NOT part of this handoff.** It stays with the original bot. Don't touch `K:\Unreal Projects\_Plugins\BongThumbLive`, the `Baegovda/BongThumbLive` repo, or box `/workspace/btl*`, `/workspace/bongthumb`, `/workspace/animthumb`.

Where this file lives (if you got only the repo URL, you are already reading the right file):
- Repo: `docs/BOT_TRANSFER.md` in `Baegovda/ForgeLens` (box clone `/workspace/LibSaber`).
- Box copy: `/workspace/handoff/BOT_TRANSFER.md`.
- **The box is ONE Linux machine shared by all of this user's agents** (the original bot is still working there on BongThumbLive). Files you see under `/workspace` may belong to it. Use your own worktree; never reset/delete another agent's worktree or kill its processes.

Read order after this file: `docs/handoff/START_HERE.md` → `HANDOFF.md` top (request ledger R01–R33 / F01–F60 / X01–X03) → `HANDOFF.md` newest "Update …" sections at the bottom → `docs/handoff/WORKFLOW.md` → `docs/handoff/STATUS.md` → `docs/SYMBOL_DB/INDEX.md` → `docs/handoff/RE_NOTES.md`. Where docs disagree, **newest dated statement wins**, and this file wins over all of them for anything about the transfer.

---

## 0. Resume checklist (do this first — this file is self-sufficient)
You may have been given **only the repo URL** `https://github.com/Baegovda/ForgeLens`. Everything you need is in this file; the other docs add detail.
1. **Who/how to talk:** the user (owner) is Korean. **Reply in short Korean 해요체**, interim reports with progress % + ETA "(추정)" in KST. Docs/commits in English, user quotes in Korean.
2. **Access you should have** (check with `GetDynamicTools` / `ListMachines`):
   - The user's Windows PC **"Revaptor-FX", machineId `d3fb99ad-25ca-45c1-81af-41e5ca09104a`** (PowerShell via `Shell`/`Read` with machineId). It has git + `gh` logged in as the repo owner, the game, UE 5.7.4, Python, VS 2022.
   - The Linux **box** (shared by all of the user's agents; `/workspace`, `/home/box/tools`). If `/workspace/LibSaber` is missing on your box: you cannot clone a private repo without a login, so on the PC run `cd $env:USERPROFILE\ls_push; git bundle create $env:USERPROFILE\Desktop\SM2_Work\Backup\fl_full.bundle --all`, `CopyToBox` it, `git clone fl_full.bundle /workspace/LibSaber`, then delete the PC bundle. Tools missing on a fresh box (dotnet 8, Ghidra 12.1.4 + JDK, node24 + rea-agents 6.8.0, `sm2_retail.exe` copied from the game folder via the PC): reinstall per §4 and WORKFLOW §2.
   - `cursor-github` MCP (read-only) is optional.
3. **Repo:** `Baegovda/ForgeLens` (private), branch **`cursor/sm2-pck-extractor-4420`** (default). Read the tip on the PC: `git -C $env:USERPROFILE\ls_push log --oneline -5`.
4. **Push:** only via PC `Desktop\SM2_Work\Scripts\mtl_fix\push.ps1` (patches named `0*.patch` in `%USERPROFILE%` → `git am -3` in `ls_push` → ff-only push). **Never force-push.** Details §2.
5. **Key paths:** PC work folder `C:\Users\Revaptor_FX\Desktop\SM2_Work`; game `D:\Steam\steamapps\common\Space Marine 2\client_pc`; UE project `K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject` (work only in `/Game/SM2_Work`, EngineWire `127.0.0.1:3000/mcp`); deployed app `SM2_Work\Bin\ForgeLens_latest\ForgeLens.exe` (next backup `.bak18`); box repo `/workspace/LibSaber`, REA `/workspace/rea`. Details §3–§4.
6. **Current state (2026-10-29 ~20:55 KST):** all SM2 work is pushed (this transfer commit is the newest); nothing SM2 is running; UE closed; no game running; REA daemon on the box is cold (no Ghidra child). Details §6.
7. **Next item:** the **REA batch, all four together in one REA/Ghidra session** — ribbon trail per-point colour writer, adaptive light multiplier (`param_3` of `FUN_14224ecf0`), `DAT_14502deac` writer, player fire clip selection (HEURISTIC today). Then the data/tool items in §7 order.
8. **Rules:** §5 (evidence first + PROVEN/INFERRED/HEURISTIC labels, never ask for in-game captures, never delete assets / `.bakN` backups never overwritten, protected UE paths, never save `Lightning_01`, act first then report, editor restarts pre-authorised with `-AutoDeclinePackageRecovery`, crash auto-recovery, stop your PIE, generic rules, no untrusted executables, no Discord unasked, Niagara only after asking, ledger every request, industry term + short Korean explanation, work has top priority).
9. **Set up your hourly progress routine** (§9), silent when nothing changed.
10. **Send the user one short Korean message**: taken over, what you verified, what you start now, % / ETA (추정).

## 1. Project goal
**Forge Lens** = an asset browser/viewer for **Warhammer 40,000: Space Marine 2** (textures, models, animations, secondary physics, VFX, customisation/colour schemes, prop animation) + a **selective exporter to Unreal Engine 5.7** (FBX + materials + physics + clips; optional one-click Direct-to-Unreal import).
- Goal: SM2 output that **looks and moves exactly like the game**, in the viewer and in UE (the user's UE goal is Sequencer cinematic renders → Chaos Cloth for cloth, bone physics for bone parts, quality over realtime perf).
- Explorer, not a bulk ripper: the user picks assets and exports them.
- Halo code in the repo is out of scope (ignore HaloCEA test failures).
- Later (only after SM2 is done, and only when the user says): **Path of Exile** (X02), then **Unreal Engine games** (.pak/IoStore, X03). Niagara conversion (X01) only if the user asks.

Code map: `src/LibSaber.SM2.Assets` (formats), `src/LibSaber.SM2Browser` (Avalonia GUI, exe name `ForgeLens.exe`), `src/LibSaber.SM2Extractor` (CLI), `tools/ue/*.py` (UE editor scripts), `tools/ue/SM2PhysicsSetup` (UE plugin), `tools/re/*` (REA daemon/client, scanners), `tools/symbol_db/*`, `tools/vfx/*`, `test/*`.

## 2. Repo and push
- Repo: **https://github.com/Baegovda/ForgeLens** (private; migrated 2026-10-29 from the public fork Baegovda/LibSaber — PR #1 stays there, not migrated, leave it alone). Branch **`cursor/sm2-pck-extractor-4420`** = default branch. Tip at transfer: **`e5b4e8b`** (+ this doc commit, local on the box only until pushed).
- **The box has NO GitHub login** (`git fetch` from the box fails with "could not read Username"). `cursor-github` MCP may read, but writes go through the PC.
- **Push method (ledger R23) — fast-forward only, NEVER force-push:**
  1. Box: work in your own worktree based on the latest origin tip; commit with `-c user.name="Cursor Agent" -c user.email=cursoragent@cursor.com`.
  2. `git format-patch origin/cursor/sm2-pck-extractor-4420..HEAD -o /workspace/patches/<stage>/`
  3. `CopyFromBox` each patch to `C:\Users\Revaptor_FX\0NNN-<name>.patch` (file name must start with `0`) on PC machine **`d3fb99ad-25ca-45c1-81af-41e5ca09104a`** ("Revaptor-FX"). Check sizes after copying.
  4. PC: `powershell -File "$env:USERPROFILE\Desktop\SM2_Work\Scripts\mtl_fix\push.ps1"` — it resets clone **`C:\Users\Revaptor_FX\ls_push`** to the origin tip, `git am -3` every `0*.patch`, deletes applied patches, pushes ff-only, prints `TIP <sha>`. On `AM FAILED` it aborts and leaves the patch in `%USERPROFILE%` → rebase your work on the new tip, delete/redo leftover `0*.patch`, rerun.
  5. Sync the box: the box can't fetch, so bring the new tip back with a bundle: PC `cd ls_push; git bundle create <SM2_Work>\Backup\x.bundle <old_tip>..origin/cursor/sm2-pck-extractor-4420` → `CopyToBox` → box `git fetch /path/x.bundle origin/cursor/sm2-pck-extractor-4420:refs/remotes/origin/cursor/sm2-pck-extractor-4420` → delete the temp bundle on the PC. (This is how `e5b4e8b` was brought to the box on 10/29 20:47 KST.) Alternatively read `git log` in `ls_push` via PC Shell.
- The transfer commit that adds this file was pushed by the previous bot via push.ps1 on 2026-10-29 (see §10).

## 3. Environment
| What | Value |
|---|---|
| User PC | Windows "Revaptor-FX", user `Revaptor_FX`, machineId `d3fb99ad-25ca-45c1-81af-41e5ca09104a`, **128 GB RAM**, RTX 4090. PowerShell via `Shell` with machineId. PC shell works even when browser routing to the PC is offline. If the PC is offline, retry later (R21). |
| PC work folder | **`C:\Users\Revaptor_FX\Desktop\SM2_Work\`** only: `Bin` (deployed builds), `Scripts` (incl. `mtl_fix\push.ps1`, `mtl_fix\oneclick\`), `Export`/`Exports`, `Captures`, `Raw`, `Anim`, `Backup`, `Build`, `Tools`, `Research`, `cleanup_log.txt`. |
| Game | `D:\Steam\steamapps\common\Space Marine 2\client_pc` (read-only; some D: reads are refused → copy to `C:\Users\Revaptor_FX\` first). |
| Unreal | **UE 5.7.4**, project `K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject`. Work only in **`/Game/SM2_Work`**. |
| UE control | **EngineWire / MCP Automation Bridge 0.5.30** (plugin `McpAutomationBridge` in the project), native MCP HTTP `http://127.0.0.1:3000/mcp`, token in `%USERPROFILE%\.enginewire_token` (**never commit/print it**). Run scripts with `Desktop\SM2_Work\Scripts\uerun.ps1 <ABSOLUTE script path> <comma,joined,args>` → `C:\Users\Revaptor_FX\enginewire_call.py`. Fallback UEMCP (`uemcp_call.py`). Details: WORKFLOW §5. |
| Deployed Forge Lens | `Desktop\SM2_Work\Bin\ForgeLens_latest\ForgeLens.exe` (10/29 17:08, sha256 `3B74C617…2124A36`, oneclick build). Backups `.bak14`–`.bak17`; **next deploy → `.bak18`**. "실행해줘" = launch this exe on the PC (R28). Forge Lens rewrites `settings.json` on exit → back it up before QA, restore after. |
| State at transfer (20:50 KST) | No game running, **UE not running** (port 3000 closed), no leftover `0*.patch` in `%USERPROFILE%`. Foreground apps include Chrome/Discord/KakaoTalk etc. The user often watches **Chzzk (치지직)** streams while you work — that's fine; work has top priority and may use full CPU/RAM, but start PC processes hidden and never steal focus. |
| Box repo | `/workspace/LibSaber` (main clone, branch `cursor/sm2-pck-extractor-4420`) + many worktrees `/workspace/LibSaber-*` (old stages; leave them). Make yours: `git -C /workspace/LibSaber worktree add -b <name> /workspace/LibSaber-<name> origin/cursor/sm2-pck-extractor-4420`. Patches `/workspace/patches/<stage>/`, stage artefacts `/workspace/mtl_fix/<stage>/`, RenderDoc captures `/workspace/rdc`, Titus sequence `/workspace/titus_seq/`, level lights `/workspace/mtl_fix/level_lights/`. |
| .NET | `export DOTNET_ROOT=/home/box/tools/dotnet PATH=/home/box/tools/dotnet:$PATH`; `dotnet build-server shutdown` then build serially `-m:1`. Expected tests: SM2.Assets **372**, SM2Extractor **43**, LibSaber.Tests **5**, `test/python` **20** (HaloCEA failures = ignore). |
| Box limits | **15 GB RAM, 8 cores, no swap.** Ghidra/REA can OOM. Never run REA alongside big builds. |

## 4. REA and the Symbol DB (primary RE path)
- **REA = https://github.com/morluto/rea** (npm `rea-agents` **6.8.0**). CLI `/home/box/tools/rea-cli/node_modules/.bin/rea`; Node 24 at `/home/box/tools/node24`; needs **Ghidra 12.1.4** at `/home/box/tools/ghidra_12.1.4_PUBLIC` (not 12.2), JDK `/home/box/tools/jdk25`. Target exe `/workspace/rea/sm2_retail.exe` (81 MB).
- **Daemon (use this, never one-shot CLI calls):** `cd /workspace/rea && nohup setsid python3 rea_daemon.py &` (repo copy `tools/re/rea_daemon.py`; queue `/workspace/rea/rea_q`; log `/workspace/rea/rea_daemon.log`). Client: `python3 tools/re/rea_call.py <tool> '<json>' [timeout_s]` (`REA_FULL=1`, `REA_MAX`). Tools: `procedure_pseudo_code`, `xrefs`, `procedure_callers`, `procedure_assembly`, `search_strings`, `inspect_analysis_view`, …
  - Startup = full import + analysis ≈ **22 min**; then requests take 0.1–2 s. **Never kill the daemon or cancel a request** (discards the DB → another 22 min). Env and analyzer list: WORKFLOW §2 "Box".
  - **Daemon state at transfer:** processes `rea_daemon.py` (pid 653800) + `rea mcp` (653801) alive since 10:22 KST, but **no Ghidra/Java child process** and the last queue answers are from 11:33 → the Ghidra session most likely died (OOM; one OOM kill is recorded at 09:37). A new request will probably trigger a fresh ~22 min import. Before relying on it, check `pgrep -af java`; if there's no Ghidra child, treat the daemon as cold and plan the import (don't run builds meanwhile). These pids belong to the shared box — don't kill them while the original bot might be using them; ask the parent/user only if it blocks you.
  - **Fallback when REA OOMs or is cold:** headless Ghidra read-only on `/workspace/rea/ghidra_proj3` (12.1.4, via `/workspace/rea/g.sh`) or `ghidra_proj2` (12.2 `/home/box/ghidra`, program `sm2_retail.exe`, `GHIDRA_HEADLESS_MAXMEM`), `DecompAt`/`DecompFn -noanalysis` (`tools/re/DecompAt.java`, `/workspace/mtl_fix/mtl39/re/`), scanners `tools/re/{xref,callers,immscan,flagscan,patscan}.py`, capstone venv `/tmp/cs` (text VA = file offset + 0x140000a00). rea-batch 1–5 all used the 3–3.5 GB headless route successfully.
  - Box inventories: `/workspace/rea/rea_procs.json` (156,646 procs), `rea_strings.json` (56,142 strings).
- **Symbol DB (R16):** `docs/SYMBOL_DB/` = `INDEX.md` + per-domain md (animation, containers, customization, materials, meshes, misc, physics, rendering, scripting, shaders, vfx) + `index.tsv` (every known exe symbol, status PROVEN/INFERRED/HEURISTIC, link) + `rea_string_xrefs.tsv`. **grep it before any new RE.** Add every new result (address, meaning, status, doc link) by hand — the generator `tools/symbol_db/build_symbol_db.py` drops hand-added rows when re-run on the box.
- Other evidence sources: shader GLSL `/workspace/shaders/out/glsl/<family>/<idx>.{ps,vs}.glsl` + `index/*.jsonl` (SHADERS.md); RenderDoc `/workspace/rdc` + `tools/rdc` (replays can stall late in the frame → replay narrow eid windows); retail data pulled to `/workspace/vfx_raw`, `/workspace/vfx3_raw`, `/workspace/phys_raw`; SSLB script reader `tools/re/sslb.py`.

## 5. Standing rules (follow exactly; full dated list = HANDOFF ledger A, R01–R33)
1. **Evidence first** (R12/R13/R14/R32): game data → exe (REA) → shader bytecode / existing RenderDoc captures. Cite addresses and strings (e.g. `FUN_14224f010`, `"blendMode" @0x14355b708`) in code comments, commit messages and HANDOFF. Label every conclusion **PROVEN / INFERRED / HEURISTIC**. **HEURISTIC is the last resort**, only after the search failed: label it in code + docs, say what was searched, and add it to HANDOFF "REA 일괄 처리 목록".
2. **Never ask the user for in-game captures** (R13). Use static analysis and the existing captures.
3. **Never delete user assets.** Before overwriting, back up as `.bakN` (UE: `_bakN`), N = next free number above the highest existing one; **never overwrite or reuse an existing `.bakN`** (R03). Obvious test assets / no-longer-needed backups may be removed in **one batch cleanup pass** at the end of a batch, logged (R04, `SM2_Work\cleanup_log.txt`). DDC cache deletion not approved.
4. **Protected:** never touch `/Game/SM2_Work/Original/*` or `/Game/Work/*`; **never save `Lightning_01`** (R05). Work only in `Desktop\SM2_Work` and `/Game/SM2_Work` (R06).
5. **Act first, report after** (R07): work non-stop, make decisions yourself, report them in one batch, pending questions at the end.
6. **Editor restarts are pre-authorised** (R08): save first (`ue_dirty.py save` skips protected/Lightning_01), quit, relaunch visibly with **`-AutoDeclinePackageRecovery`**.
7. **Crash auto-recovery** (R09): close the crash reporter, read the report (`K:\Unreal Projects\UE_VFX_57\Saved\Crashes\`, log `Saved\Logs\UE_VFX_57.log`), relaunch with `-AutoDeclinePackageRecovery`, resume.
8. **Always stop any PIE/SIE you start** (R10).
9. **Fixes are generic rules**, never per-character patches; a browser-only export must give the same result without you (R11).
10. **No untrusted executables** (R24). **No Discord actions unless asked** (R25). **Niagara only after asking** (R19 / X01).
11. **Record every user request** in the HANDOFF ledger (term, Korean meaning, status) and keep it current (R31); update HANDOFF + STATUS at every stage (R18).
12. **Translate each request into industry terms with a short Korean explanation** (R02), e.g. "Ribbon trail point writer — 리본 트레일 점 색을 CPU가 쓰는 코드".
13. **Work has top priority:** full CPU/RAM is OK (PC has 128 GB). The user often watches Chzzk streams; while a game runs keep PC jobs hidden + Idle priority, no focus stealing (R26). "게임끝" = PC free.
14. **Push ff-only via push.ps1, never force** (R23). No secrets in the repo.
15. Game-data parsing is delegated: do it without asking (R17). Plugin options go in a settings UI, not console vars (R27).
16. **Batch REA work:** the user wants all REA-solvable items collected in one list and handled together in one REA session, not one by one (memory pressure). See §7.
17. Talking to the user: **short Korean 해요체**, interim reports with **progress %** and ETA "(추정)", times in **KST**. Correct a wrong % openly. On "상황보고"/"하는중?" answer at once (running, %, ETA).

## 6. Current status (2026-10-29 20:50 KST)
Done (ledger B F01–F60; highlights of 10/29, commits on origin):
- sfx emissive exposure k = `adaptiveIntensity` PROVEN (`8bdd80a`); SSLB script reader + power-fist hand rule (`fcc16bf`); sfx parts hidden + "FX parts" toggle (`81f9a49`).
- Physics phys52: jiggle key fix, spring damper, ComplexAsSimple/double-sim fix, Titus 34/34 bodies (`8ca0bdd`).
- Character colour-scheme picker, 104 in-game schemes (`49355fd`) + darkness fix (`7aecce7`) + texture-array patterns (`ffb3ba6`).
- Weapon swing trails (`4c4155a`) + fix54 additive blend / normal-map trails out (`bcb17ff`).
- Prop/weapon embedded animation decoder + condition picker + UE export (`a590998`), reload/fire timing from data (`58e2bea`).
- REA batches 1–5: spring damper PROVEN, light falloff ×P0.w/(d²+P0.w) PROVEN, light desc → render-light field map PROVEN, UseSpecMap bit meanings DATA + importer fix, **attenCurveParam → P0.w PROVEN** (`FUN_14224f010` / packer `FUN_14224ecf0`, `d843b51`).
- Level scene parsing started: `hub_battle_barge.pak` `.cd_list` (5,898 objects, names + world matrices + overrides) and `.class_list` (661 classes) → 1,865 lights (box `/workspace/mtl_fix/level_lights/`, `cdlist.py`, `classlist.py`).
- One-click Direct-to-Unreal export (`058605a`, docs `bd5fc0c`); START_HERE (`75828f0`); repo migration docs (`e5b4e8b`).
- Disk cleanup 10/29: 26.8 GB + earlier ≈159 GB.
Track estimate on record: browser VFX ≈95 % (ROADMAP item 7). Other tracks have no recorded % — estimate fresh from STATUS when you first report, and correct openly if wrong.
Nothing is running for SM2 right now. Last SM2 activity: the repo migration commit 18:14 KST.

## 7. Open list (do in this order unless the user reprioritises)
**REA batch (do all of these together in ONE REA/Ghidra session — user's wish):**
1. **Ribbon trail per-point colour writer** — the CPU code that writes `trlPOINT` (color 0..255, intensity, globalAge, …) into `RB_TRAIL_POINTS` (only referenced by layout registration `FUN_1421ca290`). Tried: serializer offsets `FUN_14183b3a0` (visuals +0x90, trailParams +0x280, …), scans hit `FUN_14187e8b0` (param lerp) and `FUN_141a9a470` (render-instance builder) — neither. Data route: RenderDoc dump of the trlPOINT buffer from sm2_frame7894 stalled → replay only the trail eid window (find trail eids first by PS name). Goal: replace `SM2_VFX_TRAIL_FADE` / ribbon HEURISTICs.
2. **Adaptive light multiplier** — `isAdaptiveIntensity` = render light +0x6d bit 0; in packer `FUN_14224ecf0` P1.rgb is × `param_3` when bit 0 is set. Trace `param_3` (likely avgLum/adaptation) → implement in bench + UE; `DefaultAdaptive = true` for sfx lights is still HEURISTIC; the four armory level lights had no key and factor 1 (DATA).
3. **`DAT_14502deac`** — global bool read in `FUN_14224f010`: when set, all non-directional lights use the inverse-squared regime (P0.w 0.0025). Currently INFERRED (0 in captures). Find its writer(s) (console var / quality setting?) and confirm default. SYMBOL_DB `index.tsv` row exists.
4. **Player fire clip selection (HEURISTIC)** — player firing clips have no Shoot track, so prop-anim picks the shoot clip by name. Find the data/exe source (anim graph / morpheme state, firearm library, `fireRateAffectShootAnim` default not decoded).
5. Also re-check while the session is up (from HANDOFF "REA 일괄 처리 목록" + STATUS Heuristics): MNM-on-non-distortion trail path (fix54 skip HEURISTIC), UseSpecMap mask writer (CB filled by reflection), Shoot → muzzle link, visibility → scale 0 / holding-branch pick in prop anim, bench exposure 1/250 / histogram auto-exposure mapping.
**Data / tool work (no REA needed):**
6. **Left/right pauldron (+ gauntlet/greave) colour overrides** — colour-scheme section ↔ part matching (currently HEURISTIC) in `ArmorColourSchemes.cs`.
7. **Colour scheme → UE master material** — `<tpl>.colour_scheme.<id>.json` (UE MI params) is already written on export; wire it into `M_SM2` / `import_sm2_materials.py` (one-click currently only tags the mesh; the scheme is baked into the albedo).
8. **F43: `.td` non-texture params in the importer** (`import_sm2_materials.py` / sidecar).
9. **Muzzle flash offset & aim pose from data** (F46 Titus walk-and-shoot sequence, box `/workspace/titus_seq/`): muzzle flash in front of / at the side angle of the barrel, replace the generated pitchUp35 aim pose with game data.
10. **Physics for other characters** — re-run phys52 rules (jiggle Name key, spring damper, no ComplexAsSimple) on the other characters in UE, `.bakN` first, PIE stopped.
11. **Cloth build28** — install/verify the Chaos Cloth plugin build28 (plugin install needs UE closed → restart is pre-authorised; DLL lock check with `Get-CimInstance Win32_Process -Filter "Name like 'UnrealEditor%'"`).
12. **Level / prop placement extraction** — the user is interested. Extend `/workspace/mtl_fix/level_lights/cdlist.py` + `classlist.py` from lights to all objects (prop tpl + world matrix) → Forge Lens level view and/or UE level placement under `/Game/SM2_Work/Levels/…` (never save Lightning_01). Multi-light container objects (29) are still skipped.
13. Small follow-ups: one-click leaves stale assets on reimport (by design, nothing deleted); stray test clip `/Game/SM2_Work/Animations/cc_calgar` → remove in the next batch cleanup (R04). Artificer presets (F27), F45 physics motion validation without captures, cinematic sequences, model UV scroll.
**Later (only when the user says):** Path of Exile (X02), then UE games .pak/IoStore (X03). Niagara (X01) only on request.
(BongThumbLive items such as F42 "clear cache" are **not yours** — original bot.)

## 8. Pitfalls
- Box OOM: REA/Ghidra on 15 GB; no parallel big builds; headless fallback at 3–3.5 GB heap works.
- `push.ps1` leaves the failing patch after `AM FAILED`; the box cannot `git fetch` (use the PC bundle route in §2).
- SYMBOL_DB generator drops hand rows; edit by hand.
- `fl_oneclick.ps1` needs Windows PowerShell 5 (`powershell.exe -File`), not pwsh 7. `uerun.ps1` needs absolute script paths and comma-joined args. Don't name a PowerShell helper `R`; `$c`/`$C` collide.
- Headless UnrealEditor-Cmd: Interchange asserts without Slate → legacy factories (handled in `ue_forge_direct.py`).
- Titus campaign armour `*_02` has no CC block → only helmet/acc/crux recolour (PROVEN, not a bug).
- Older docs mention cloud-agent push, a 20-minute routine, "deletions always need approval", EngineWire port 8090 — superseded (push.ps1, hourly, R04, port 3000).
- HANDOFF.md bootstrap block (top, "NEW AGENT: START HERE") is older; START_HERE + this file win.

## 9. Your hourly progress routine
The original bot ran a routine named `sm2-20` (hourly, R22). **Create your own** hourly routine (name it `sm2-20` if free, otherwise `sm2-20b`) — load the `routines` skill first. It must:
- check your running subagents/executors, your worktree (`git log -3`, `git status`), new files under `/workspace/patches/*`, `/workspace/mtl_fix/<your stage>/`, `/workspace/browser_run_shots/*`, and only if cheap the PC capture folders;
- send a 2–4 line Korean 해요체 report: what finished, what's running, **progress %**, ETA (추정, KST), blockers;
- **stay silent when nothing changed** since the last report; never send a stale/duplicate report; never say "nothing is running" while an executor is active;
- be paused/deleted when the user says stop or all approved work is done.
(The original bot should stop its own `sm2-20` for SM2 — tell the user if both are reporting.)

## 10. Transfer record
- Box clone `/workspace/LibSaber` fast-forwarded 1565d17 → `e5b4e8b` (bundle from PC `ls_push`), then this doc + README/HANDOFF/START_HERE/AGENTS pointers were committed and pushed from the PC via push.ps1 (patch copy: box `/workspace/handoff/*.patch`).
- Ledger: R33 added (SM2 transferred to a new Grok Bot; BongThumbLive stays with the original bot).
