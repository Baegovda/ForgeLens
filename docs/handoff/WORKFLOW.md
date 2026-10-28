# WORKFLOW — how the work is done

## 1. Where things run
| Kind of work | Runs on | How |
|---|---|---|
| Code, parsing, analysis, Ghidra/rizin RE, unit tests, headless browser captures, reports | **Bot's Linux box** (persistent, shared by the user's agents) | `Shell` without machineId |
| Anything needing the full game install, the real exe GUI, Unreal Editor, in-game captures | **User's Windows PC "Revaptor-FX"** | `ListMachines` → machine id (`d3fb99ad-25ca-45c1-81af-41e5ca09104a` at time of writing), then `Shell`/`Read` with that machineId (PowerShell) |
| Pushing to GitHub | **Cursor cloud agent** | format-patch → cloud agent `git am -3`, build, test, fast-forward push (§4) |
- The box is cheaper per step (fewer round trips): do analysis and rendering there. Only exe/UE/game-data steps go to the PC.
- Move files between them with `CopyToBox` (PC → `/workspace/...`) and `CopyFromBox` (box → PC). Check the size after copying; a copy once delivered a stale file.

### Before any PC step: is the user gaming?
```powershell
(Get-Process | ? MainWindowTitle | Select -Expand ProcessName) -join ','   # look for a game (e.g. AgainstTheStorm), not just UnrealEditor
```
**Update 2026-10-27 (user):** heavy PC work is allowed while the user games, as long as it does not disrupt the game: still start everything hidden and at idle priority, never steal focus. (Older rule below: light read-only steps only.)
If a game is running, do only light read-only steps on the PC. Start PC processes hidden and at idle priority so they never take focus:
```powershell
$p = Start-Process -FilePath $exe -ArgumentList $a -WindowStyle Hidden -PassThru; $p.PriorityClass = 'Idle'; $p.WaitForExit()
```

## 2. Paths
### PC (verified 2026-10-27 by read-only listing)
- Home `C:\Users\Revaptor_FX`
  - `enginewire_call.py`: the EngineWire (MCP Automation Bridge) native-HTTP client (primary). `uemcp_call.py`: the UEMCP client (fallback).
  - `ls_push\`: clone used by the old bundle push.
  - `SM2Extractor*`, `SM2Browser_v2..v4`, `SM2Browser_e6cadfa`: older installed builds (`.bakN` kept).
- Work folder `C:\Users\Revaptor_FX\Desktop\SM2_Work\`:
  - `Scripts\`: PC copies of `tools/ue/*.py` (+ `.bakN`), `uerun.ps1`, `bulk\`, `legacy\`, `ue_verify\`, `sanival*`.
  - `Exports\<set>\<asset>\`: CLI exports, e.g. `bulk`, `bulk_fix`, `physics`, `mat3_*`, `step3_*`, `retarget`, `anim_finish`. Logs sit beside them.
  - `Captures\<stage>\`: UE captures, e.g. `bulk`, `bulk_re`, `ue_verify`, `Mat`, `Step3`, `v4_gpubench`. Also `Captures2`.
  - `Bin\`: CLI builds `SM2Extractor7..10`, **`SM2Extractor_e4ceb7d`** (current), `SM2Extractor_uev`.
  - `Raw\`: extracted game files used for RE/bench, e.g. `Raw\vfx\texN`, `sani_all`.
  - `Backup\`: content backups `*.bakN`.
  - Also `Anim\`, `Tools\rootprobe.exe`.
- Game: `D:\Steam\steamapps\common\Space Marine 2\client_pc`. Some D: reads are refused; copy to `C:\Users\Revaptor_FX\` first.
- Unreal 5.7.4 project: `K:\Unreal Projects\UE_VFX_57`.
  - Log: `Saved\Logs\UE_VFX_57.log`. Crash reports: `Saved\Crashes\`.
  - Content `/Game/SM2_Work/{Animations, Characters, Materials, Original (read-only), Props, Tests/<stage>}`.
  - Tests folders now: `anim_finish(2)`, `anim_survey`, `bulk`, `bulk_fix`, `mat2_hair`, `physics`, `pre_step3`, `retarget`, `rhino_stacked`, `sani_before`, `step3_ab_off`, `v4`.
- Tools on the PC: Python 3.10 (`pip install uemcp mcp`; mcp ≥1.30 for streamable HTTP), Node 24 / npm 11, VS 2022 (Community + Build Tools 17.14), git, gh (logged in as the repo owner).
- No Ghidra project on the PC; RE happens on the box.

### Box
- Repo: `/workspace/LibSaber`, with worktrees `/workspace/LibSaber-*` (one per worker; **never share a worktree with another running worker**). Make your own: `git worktree add -b <name> /workspace/LibSaber-<name> origin/cursor/sm2-pck-extractor-4420`.
- Patches: `/workspace/patches/<stage>/000N-*.patch`.
- RE:
  - exe `/workspace/rea/sm2_retail.exe` (81 MB);
  - Ghidra project `/workspace/rea/ghidra_proj2.gpr` (program **`sm2_retail.exe`** since the 2026-10-27 rebuild; use `analyzeHeadless /workspace/rea ghidra_proj2 -process sm2_retail.exe ...`; heap via `GHIDRA_HEADLESS_MAXMEM=10G`, `MAXMEM` is ignored);
  - decompiles `/workspace/rea/dec*/<addr>.c` (`dec_phys*`, `dec_cust`, `dec_deg`, `dec_tint`, `dec_sani`, ...);
  - Ghidra `/home/box/ghidra`, JDK `/home/box/tools/jdk25`, rizin `~/.local/bin/rizin`.
- **REA (morluto/rea) — the REA tool for this project** (installed 2026-10-27; replaces the old `user-rea` plugin note, which is obsolete):
  - Repo clone (docs/source) `/home/box/tools/rea`; CLI installed with npm at `/home/box/tools/rea-cli` (rea-agents **6.8.0**); binary `/home/box/tools/rea-cli/node_modules/.bin/rea`.
  - Needs Node 22.23.4+/24.22.1+ (system node is 20 → too old). Private Node **24.22.1** at `/home/box/tools/node24` (not on the system PATH; nothing global changed).
  - REA 6.8.0 only accepts **Ghidra 12.1.x** (doctor rejects 12.2 as `unsupported_version`). So a second Ghidra, **12.1.4 at `/home/box/tools/ghidra_12.1.4_PUBLIC`**, is used for REA only. `/home/box/ghidra` (12.2) is unchanged and stays the tool for our own headless runs / `ghidra_proj2`.
  - Env for CLI and MCP:
    `PATH=/home/box/tools/node24/bin:$PATH GHIDRA_INSTALL_DIR=/home/box/tools/ghidra_12.1.4_PUBLIC JAVA_HOME=/home/box/tools/jdk25 GHIDRA_HEADLESS_MAXMEM=8G REA_ANALYSIS_PROVIDER=ghidra REA_GHIDRA_STARTUP_TIMEOUT_MS=21600000`
    (REA ignores `JAVA_TOOL_OPTIONS` etc.; heap only via `GHIDRA_HEADLESS_MAXMEM`.)
  - Check: `rea doctor --provider ghidra --json` → `"healthy":true`. Example: `rea decompile <abs exe> --procedure entry --provider ghidra --json`; `rea --help` lists 175 commands (decompile, function, decompile-all, execute-rizin-command, inspect-binary-layout, ...).
  - MCP (stdio): command `/home/box/tools/node24/bin/node`, args `["/home/box/tools/rea-cli/node_modules/rea-agents/scripts/rea.mjs", "mcp"]` or simply command `/home/box/tools/rea-cli/node_modules/.bin/rea` args `["mcp"]` with the env above (PATH must include node24). 229 MCP tools. The MCP client's per-call deadline must cover Ghidra startup.
  - Tests 2026-10-27: `/bin/ls` copy → decompile `entry` OK in ~15 s. `sm2_retail.exe` with `REA_GHIDRA_PROJECT_PATH=/workspace/rea/ghidra_proj2.gpr` + `REA_GHIDRA_PROGRAM_NAME=sm2_retail.exe` and a 120 s deadline → `provider_timeout` (the project had no saved program yet: the 12.2 headless analysis was still running).
  - Caveats: (1) default startup deadline 330 s is far too short for the 81 MB exe (this is why the old REA "timed out"); a fresh import means full auto-analysis (expect hours) — raise `REA_GHIDRA_STARTUP_TIMEOUT_MS`. (2) Project reuse opens a read-only **copy** of the `.gpr/.rep`; it needs a saved program, and a DB saved by Ghidra 12.2 may not open in 12.1.4 — for reuse, prepare a project with 12.1.4's `analyzeHeadless` in a new directory. (3) Don't run REA fresh imports of the exe while another big Ghidra job runs (CPU/RAM). (4) `rea setup` would edit agent configs — not run; register MCP by hand.
- .NET 8 SDK: `/home/box/tools/dotnet` (8.0.427).
- Shader tools: dxc `/home/box/tools/dxc` (v1.9.2609), dxil-spirv `/home/box/tools/dxil-spirv/build/dxil-spirv` (built with zig cc via `/home/box/tools/zigcc`, no system g++), `.sdc` unpacker `/workspace/mat/sdcx.py` (recreated 2026-10-27, outside the repo).
- Raw game data pulled from the PC: `/workspace/vfx_raw`, `/workspace/vfx3_raw` (`ssl`, `td`, `tpl`, `pct`), `/workspace/phys_raw`.
- Outputs:
  - captures `/workspace/browser_run_shots/<stage>/`;
  - bulk report `/workspace/bulk/REPORT.md`;
  - UE verify sheets `/workspace/ue_verify/`.
- If the box is new or empty: clone the repo, install .NET 8 + Ghidra, and copy `sm2_retail.exe` from the game folder via the PC (`CopyToBox`). Re-run Ghidra analysis (slow; it once ran out of memory, so give it a big heap).

## 3. Build and test (box)
```bash
export DOTNET_ROOT=/home/box/tools/dotnet PATH=/home/box/tools/dotnet:$PATH
cd /workspace/LibSaber-<yours>
dotnet build-server shutdown            # avoid MSB3883 / locked shared compiler server
dotnet build LibSaber.sln -c Release -m:1
dotnet test test/LibSaber.SM2.Assets.Tests      -c Release -m:1
dotnet test test/LibSaber.SM2Extractor.Tests    -c Release -m:1
dotnet test test/LibSaber.Tests                 -c Release -m:1
# HaloCEA tests: out of scope (they fail).
python3 test/python/test_chameleon.py         # UE importer material math (unreal stubbed, stdlib only)
```
- Build serially (`-m:1`). Several workers share the box, and parallel builds hit MSB3883 (file in use).
- Windows builds for the PC, self-contained single-file:
  ```bash
  dotnet publish src/LibSaber.SM2Extractor -c Release -p:PublishProfile=win-x64 -o /workspace/<out_cli>
  dotnet publish src/LibSaber.SM2Browser   -c Release -p:PublishProfile=win-x64 -o /workspace/<out_browser>
  ```
  Zip with Python, `CopyFromBox` to `C:\Users\Revaptor_FX\`, and `Expand-Archive` into a **new** folder (`Desktop\SM2_Work\Bin\SM2Extractor_<sha>`). Never overwrite an older build; keep it, or rename it `.bakN`.
- CLI export (PC):
  ```
  LibSaber.SM2Extractor.exe export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/<n>.tpl --format fbx-unreal --output <dir> [--anim tpl/<n>.tpl/animations/<clip>.sani | "glob*"] [--anim-only] [--variant default|all|...] [--tex-formats png] [--no-physics] [--no-root-motion]
  ```
- Pull a few game files to the box for RE/bench: on the PC, run `extract "<client_pc>" --output <Raw\...> -q --filter <glob>...` (hidden, idle priority), `Compress-Archive`, then `CopyToBox`. Example: `/workspace/vfx_raw/extract_tex6.ps1`.

## 4. Pushing to PR #1 (cloud agent)
The box has no GitHub write login. Pushes go through a Cursor cloud agent with repo access (the user enabled cloud agents and granted GitHub access to Baegovda/LibSaber).
1. On the box, commit on top of the **latest remote tip** (`git fetch` first). Identity: `-c user.name="Cursor Agent" -c user.email=cursoragent@cursor.com`.
2. `git format-patch origin/cursor/sm2-pck-extractor-4420..HEAD -o /workspace/patches/<stage>/`
3. Send a follow-up to the cloud agent (CloudAgent reply, mode **queue**). Current agent (2026-10-27): `bc-65aed255-5102-5266-bcbd-f60b85b14984` (the older `bc-abf903a2-7eaf-532a-9a9c-bd757453bf47` is replaced). If it's gone, launch a new one on the repo/branch with the same instructions. Include the patch contents and these instructions:
   - check out `cursor/sm2-pck-extractor-4420` and pull;
   - `git am -3 <patches>`;
   - `dotnet build-server shutdown`, then build serially (`-m:1`; MSB3883 otherwise);
   - run the three SM2 test projects (expected counts in STATUS.md);
   - if green, **fast-forward push** (no force push) and report the new tip and test counts.
4. Verify with `git fetch` that the tip moved. Report the commit to the user with the PR link.
- Fallback (older, PC-based): `git bundle create /workspace/sm2.bundle <lastpushed>..HEAD` → `CopyFromBox` to `C:\Users\Revaptor_FX\sm2.bundle` → in `C:\Users\Revaptor_FX\ls_push`: `git fetch ..\sm2.bundle HEAD; git merge --ff-only FETCH_HEAD; git push origin HEAD`.

## 5. Unreal (EngineWire primary, UEMCP fallback)
- **EngineWire / MCP Automation Bridge** (https://github.com/ChiR24/Unreal_mcp, MIT). Installed 2026-10-27: stable **v0.5.30** (the plugin is still named `McpAutomationBridge` in 0.5.x; the rename to `EngineWire` is only in the 0.6.0-beta line, not used).
  - Plugin: `K:\Unreal Projects\UE_VFX_57\Plugins\McpAutomationBridge` (prebuilt Win64 binaries; the release ships source only, so it was built with `RunUAT BuildPlugin -TargetPlatforms=Win64` against UE 5.7 into `Desktop\SM2_Work\enginewire\built\`; log `enginewire\build.log`). The project is Blueprint-only, so never drop the source-only zip in directly — rebuild with BuildPlugin and copy `Binaries`, `Source`, `.uplugin`.
  - Config: `Config\DefaultGame.ini` section `[/Script/McpAutomationBridge.McpAutomationBridgeSettings]`: `bEnableNativeMCP=True`, `NativeMCPPort=3000`, `bRequireCapabilityToken=True`, `CapabilityToken=…`, loopback only. Pre-install copy: `DefaultGame.ini.bak1`.
  - Token: `C:\Users\Revaptor_FX\.enginewire_token` (same value as the ini; sent as `X-MCP-Capability-Token`; override with `$env:ENGINEWIRE_TOKEN`). **Never commit it.** Wrong token → HTTP 401.
  - Endpoint: `http://127.0.0.1:3000/mcp` (Streamable HTTP, served by the editor itself; no Node needed). Status bar shows `● MCP :3000`. Log category `LogMcpNativeTransport`.
  - Optional Node stdio server (not needed by our scripts): `Desktop\SM2_Work\enginewire\server` (`npm i unreal-engine-mcp-server@0.5.30`; it talks to the plugin's WebSocket port 8091).
- Run a script: `Desktop\SM2_Work\Scripts\uerun.ps1 <script.py> <args...>` (repo copy `tools/ue/uerun.ps1`). It writes `ue_args.json`, then calls `python C:\Users\Revaptor_FX\enginewire_call.py ue_python ue_args.json` when port 3000 is listening, otherwise `uemcp_call.py` (force UEMCP with `$env:UE_MCP='uemcp'`). Old UEMCP-only runner: `Scripts\uerun_uemcp.ps1` / `uerun.ps1.bak1`.
- `enginewire_call.py` usage: no args = list the 23 tools; `ue_python args.json` = uemcp-compatible (maps to `system_control` / `execute_python`, prints the captured stdout); `<tool> args.json` = raw tool call.
- Speed: `ue_verify_layout.py` takes ~1.6 s via EngineWire vs ~42 s via UEMCP.
- Fallback UEMCP (ATDev-Inc/uemcp, pip) is still installed; smoke test `python uemcp_call.py`.
- Restart procedure: save all (`unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)`), `unreal.SystemLibrary.quit_editor()`, relaunch `UnrealEditor.exe "K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject"` minimized; EngineWire is up when port 3000 listens.
- Key scripts (`tools/ue/`, PC copies in `Scripts\`):
  - `ue_bulk.py import|check <asset> [set]`: import + checker into `/Game/SM2_Work/Tests/<set>/<asset>`.
  - `ue_import_anim.py`: anim-only FBX via the legacy importer (Interchange bug UE-307815). It does yaw auto, root-motion policy and 60 Hz for baked clips.
  - `ue_mat_apply.py`, `ue_step3*.py`: materials.
  - `ue_sm2_physics.py <physics.json> <mesh> [folder] verify|physasset`, `ue_phys_probe.py`: physics.
  - `ue_cap_lit.py` (modes capture/pose/export/fit, each **a separate call**; same-call export returns a stale render target): lit captures. Wait for shaders/textures to finish compiling, or the capture comes out grey.
- Restarting the editor is allowed (save all first). Long imports run inside one editor tick; import big sets one asset at a time.

## 6. Headless browser captures (box)
The browser has bench modes that render offscreen through Mesa llvmpipe; Avalonia's llvmpipe blacklist is cleared in `Program.cs`. Run them under Xvfb:
```bash
xvfb-run -a -s "-screen 0 1920x1080x24" dotnet run --project src/LibSaber.SM2Browser -c Release -- <mode> ...
```
| Mode | Args |
|---|---|
| `--glbench` | `<game folder|synthetic> <model> <out> [frames]` (GPU preview, also on the PC for real numbers; `SM2_PARTS_ONLY=1`) |
| `--bench` | `<game> <model> <out> [frames]` (CPU old vs new) |
| `--physbench` | `<tplDir> <sani> <clsRoot> <out> [seconds] [every]` (`SM2_PHYS_BAKE=1` compares baked vs live) |
| `--vfxbench` | `<sfxDir> <part_aliases.ps> <texDir> <out> name@sec[@dist]...` (`SM2_VFX_ONLY/SKIP/EXPOSURE/LIT/FRAMES/MESHES/DISTORT/DEG`) |
| `--hostvfxbench` | `<tplDir> <sani|-> <sfxDir> <aliases.ps> <texDir> <out> <effect|-> [bone|-] [seconds] [every]` |
- Interactive dev mode without a game folder: `SM2_VFX_LOOSE="sfxDir|part_aliases.ps|texDir|meshDir" SM2_VFX_OPEN=<name>`, `SM2_MODEL_OPEN="tplDir[|sani]"`, `SM2_HOST_*`.
- Make before/after sheets with Python (PIL) and save them under `/workspace/browser_run_shots/<stage>/`.
- On the PC, `--glbench` gives real GPU numbers (RTX 4090, ANGLE D3D11). Run it only when no game is running.

## 7. Bulk validation (`tools/bulk/`, PC copies in `Scripts\bulk\`)
1. `bulk_export.ps1 [-Only a,b] [-Par N] [-Exe path]` → `Exports\bulk\<a>` (an existing folder is renamed `.bakN`), plus `results.json`.
2. `bulk_ue.ps1 [-Only] [-Mode import|check]` → `/Game/SM2_Work/Tests/bulk/<a>` and `<a>.ue.json`.
3. `bulk_cap.ps1` → `Captures\bulk\<a>`. Don't use a PowerShell variable `$c` next to `$C`: variable names are case-insensitive.
4. On the box: `make_report.py` → `REPORT.md`, then `contact_sheet.py`.
- `manifest.csv` lists the 37 assets. `ctor_export.ps1 -InPlace` re-exports the constructors.

## 8. Stage routine (what "done" means for a stage)
1. REA evidence first, with addresses noted.
2. Code + tests on the box, in your own worktree.
3. Captures (box headless, and/or UE on the PC when it's free).
4. Append a dated section to HANDOFF.md and update `docs/handoff/STATUS.md`.
5. Format-patch → cloud agent push.
6. Short Korean report to the user with the % and what's next.

## 9. The 20-minute progress report routine
The user wants automatic interim reports. Create a recurring routine (see the bot's `routines` skill), e.g. named `sm2-20`, every 20 minutes. It should:
- check running executors/subagents, the box worktrees (`git log -3`, `git status`), new files in `/workspace/patches/*` and `/workspace/browser_run_shots/*`, and, only if cheap, the PC capture folders;
- write a 2–4 line Korean report: what finished, what is running, **progress %**, ETA (추정), blockers;
- **stay quiet if nothing changed** since the last report. Never send a stale or duplicate report, and never say "nothing is running" while an executor is active;
- Niagara is off the roadmap (possible future development only); never start it unprompted.
Delete or pause the routine when the user says to stop, or when all approved work is done.

- mtl1-27: python tests `cd test/python && python3 -m unittest` (unreal stubbed). Material-layer check: export with the new extractor, then read `slots[].layers` from the materials.json (PowerShell ConvertFrom-Json one-liner, see STATUS mtl1-27).
- mtl1-28: `SM2_VFX_KIND_BLEND=0` = old blend; `SM2_VFX_COVERING=plane|sphere` also gives a distortion backdrop (non-painting); `SM2_VFX_DISTORT=0` turns distortion off for A/B. Standalone trails: `SM2_VFX_EXPOSURE=0.5`, dist 15. Missing textures: run the extractor hidden at idle with `extract "<client_pc>" --output Raw\vfx\texNN -q --filter <glob>`, zip, CopyToBox, then `cp -n` into /workspace/vfx3_raw/texall (exit 2 = mod paks skipped, OK). Cloth: the sidecar `ue.chaosCloth.renderAsSim` (default true) lets the plugin use a render section as sim when the export has no _SIM (export without --include-cloth-sim).
- mtl1-29: `uerun.ps1` takes script args as ONE array: pass `cc_sorcerer,mtl15` (comma), not space-separated (the 2nd arg is dropped). In PowerShell don't name a helper function `R` (alias of Invoke-History). Restart helpers: `ue_dirty.py [save]` (lists or saves dirty packages, skipping Lightning_01, Original/*, Work/*, diag_unsaved), `ue_diagdrop.py` (drops the unsaved cdiag duplicates so quit does not prompt), `ue_quit`, then relaunch. Live cloth check: `ue_c28.py spawn|cap|exp <tag>|start|sample|kick|stop|clean <name> <set>` (needs the ue_cdiag duplicate). Plugin binary backups live in `Build\sm2phys\installed_SM2PhysicsSetup.bak2\Binaries_bakN`.
- mtl1-30: `uerun.ps1` needs the **absolute** script path (relative paths resolve against the UE binaries dir). Inside a helper function, split a quoted arg string yourself: `& .\uerun.ps1 $script ($a -split ',')`. ue_c28 `cap` renders, `exp <tag>` writes the PNG. The SIE sample right after `start` can read 0 particles; sample after `kick`.
- mtl1-31: `ue_cdiag.py <name>,<set>[,<physics.json>]` (3rd arg = A/B sidecar variant; prints `PJ`). `ue_c28.py sampleref` measures the cloth against the base mesh's body PA (fixed reference, coarse). `tools/ue/mtl_fix/run31.ps1 <tag> <json> ["Prop=V;..."]` does one full variant run (diagdrop, cdiag, cfgtune, SIE, kick, samples, capture, stop, clean). `vis_intrusion.py` scores the captures (HEURISTIC, red robe only). In-memory knobs via cfgtune: CollisionThickness, bUseSelfCollisions, SelfCollisionThickness. Backstop is a weight map, so vary it with sidecar `backstopCm` (-1 = off). Per-cloth collision PA: sidecar `collisionPerCloth` (default true).
- mtl1-32: `ue_c28.py sampleskin` = free particles >1 cm behind the skinned body surface (MeasureClothSkinPenetration, nearest body vertex within 10 cm; prints top body slots and per-asset counts). `tools/ue/mtl_fix/run32.ps1 <asset> <tag> <json|saved> [cfg]` = run31 for any chars_cloth asset plus sampleskin; `saved` tests the saved `<asset>_ChaosCloth` (ue_c28 falls back to it when no diag duplicate exists, so run ue_diagdrop first).
