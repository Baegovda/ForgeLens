# Morpheme network (.nmb)
- `nmb_reader.py <x.nmb> [--state REGEX] [--map wpn.tsv]`: dumps switch/expression trees of states (default: player shoot states) and evaluates wpnType → fire clip. Layout derived from data, see docs/handoff/FIRE_CLIP.md.

# SSLB scripts (datapars)
- `sslb.py <file.sslbin> [x]`: classes / members / constants (x = also disassemble every method); `pb.py <file>`: raw protobuf dump. Layout: docs/SYMBOL_DB/scripting.md.

# Light exe RE (no Ghidra auto-analysis) — mtl1-39
- `xref.py <exe> <out.json> --str S | --va 0xHEX ...`: .pdata function bounds + capstone RIP-relative refs.
- `callers.py <exe> 0xTARGET...`: direct call/jmp callers (also via one E9 ILT thunk, e.g. Convert.exe).
- `immscan.py <exe> imm1,imm2,... <min>`: functions using at least <min> of the immediates.
- `patscan.py` / `flagscan.py`: one-off structural scans (array+0x150 stride 0x28 readers; emitter +0xe4/+0xe8 with 0x400). Edit constants.
- `DecompFn.java <out.c> addr...` (Ghidra headless `-noanalysis`: creates functions at addresses and decompiles),
  `DecompAt.java <out.c> addr...` (already-analysed project, `-readOnly -noanalysis`).
Needs pefile + capstone (box: /tmp/cs/bin/python), JAVA_HOME=jdk25, GHIDRA_HEADLESS_MAXMEM≤8G, run under nice.

# RE toolbox (R39, 2026-10-30) — check here before writing a new tool
| Tool | What / when | Command |
|------|-------------|---------|
| `recall.py` | FIRST step: is it already known? searches RE_CACHE/INDEX.tsv | `python3 tools/re/recall.py 0x141ab9b60` / `recall.py trail colour` |
| `re_cache_build.py` | rebuild INDEX.tsv after every RE batch (RE_NOTES, RE_CACHE/notes, FIRE_CLIP, SYMBOL_DB) | `python3 tools/re/re_cache_build.py` |
| `ghidra_serve/` | persistent own-code Ghidra server on saved PC project `re_env\proj_sm2` (loopback + token, read-only). One Ghidra process per project folder. | `gs.ps1 import` (once) · `gs.ps1 start` / `stop` / `status` |
| `ghidra_serve/gs.py` | client; tools `decompile`(=`procedure_pseudo_code`, `max_lines`), `xrefs`, `search_strings`(`pattern`), `function_at`, `list_functions`, `summary`; batch = many queries per call | `gs.py sum 0xA 0xB -c 15` · `gs.py batch q.jsonl` · `gs.py xrefs '{"address":"0x..."}'` |
| `rea_call.py` + `pc/start_rea.ps1` | REA 6.8.0 daemon (pinned; temp project, re-imports each start ~11 min) | `start_rea.ps1` · `rea_call.py search_strings '{"pattern":"x"}'` |
| `exe_check.py` | patch check: exe sha256 vs analysed build | `python tools/re/exe_check.py` |
| `docs_check.py` | documented paths/arg names still valid | `python3 tools/re/docs_check.py [--pc]` |
| `pc/mem_watch.ps1` | log when PC free RAM < 20 GB (log only) | `powershell -File mem_watch.ps1` |
| `box/refresh_origin.sh` | refresh box origin ref from PC bundle after a push | see script header |
| `box/push_lock.sh` | one push at a time (mkdir lock, stale 1 h) — acquire before format-patch, release after refresh_origin | `push_lock.sh acquire <agent>` / `release <agent>` / `status` |
| `nmb_verify.py` (R40) | fire-clip table (PlayerFireClips.cs port) vs primaris.nmb per weapon × plasma × aux; lists assumptions, re-labels rows, exit 1 on MISMATCH | `python3 tools/re/nmb_verify.py primaris.nmb --all-states --out docs/handoff/FIRE_CLIP_VERIFY.tsv` |
| `rea_version.py` (R41) | REA guard: installed rea-agents must be 6.8.0 (called by start_rea.ps1, docs_check --pc) | `python tools/re/rea_version.py` |
| `pc/disk_watch.ps1`, `pc/patch_watch.ps1`, `pc/mem_watch.ps1 -Once` (R41) | silent watchers → one line per tag in `SM2_Work\alerts.txt` (empty = OK), history `re_env\alerts.log`. Scheduled by `pc/install_tasks.ps1` (mem/disk every 10 min, patch daily 09:00). **Cost no tokens**: they only write alerts.txt; the bot reads it when it looks | `type %USERPROFILE%\Desktop\SM2_Work\alerts.txt` |
| `pc/rules_check.ps1` (R44) | session start / after summary: RULES_CORE sha+mtime, UE running → route (EngineWire vs headless), REA/GhidraServe up, alerts.txt; ≤10 lines, read-only | `powershell -File rules_check.ps1` |
| `pc/backup.ps1` (R41) | copy big artefacts to `K:\SM2_Backup` + sha256 manifest; `verify`; `record` = manifest only (proj_sm2 while served). Never deletes | `backup.ps1 copy` · `backup.ps1 verify` · `backup.ps1 record proj_sm2 <path>` |
REA daemon and GhidraServe are **manual starts** (owner 10/30: no logon auto-start): `start_rea.ps1`, `gs.ps1 start`; both are no-ops when already running.

## gs.py batch recipe (R40)
One query per line (`#` comment), short form or JSON:
```
sum 0x141909810          # summary (sum ADDR [code lines])
dec 0x141ab9b60 30       # decompile, max lines
xrefs 0x14224ecf0        # [limit]
str ho_shoot             # regex [limit]
at 0x141909900           # function_at
funcs 0x141909000 0x141910000 20
```
`gs.py batch q.txt` (or `-` = stdin) → one compact block per query `## n tool args`; `--json` = raw. Round-trip time on stderr.
Measured PC 2026-10-30 01:2x KST, 20 mixed queries (8 sum, 3 dec 30, 3 xrefs, 3 str, 2 at, 1 funcs): 198–229 ms server round trip, 246–292 ms wall (3 runs); same 20 as separate calls 1,123 ms; compact output 11.4 k chars vs 15.3 k JSON.
Measured 2026-10-30 KST (PC): `gs.ps1 import` 21 min (full analysis, saved 1.22 GB); `gs.ps1 start` ready in 8–10 s (56,190 strings indexed); decompile 42–54 ms server-side, ~110 ms per gs.py call; string search 6 ms; batch of 3 in ~63 ms; RAM ~1 GB idle. Known gap: `summary` signature shows Ghidra's listing prototype (often `undefined f(void)`); use `-c N` for the decompiled signature.
| `tools/ue/ue_phys52_all.py` (R52) | phys52 setup on other characters via EngineWire: PIE check, mesh → `<n>_bakN`, SetupFromJson + TuneRigidBody, prints BEFORE/AFTER PA/ABP | `uerun.ps1 ...\ue_phys52_all.py cc_calgar,cc_sorcerer_exalted[,probe]` |
| `tools/re/muzzle_aim.py` (R51) | firearm client cls → shoot-tip locator (+ barrel overrides), muzzleSfx, wpnType → player `tacticus_wpn<code>_aim_hold` clip | `python muzzle_aim.py <Raw>\cls17\ssl\weapons <Raw>\sani_all\animation\primaris\tacticus out.tsv` |
| `tools/level/placement.py` (R54) | scene cd_list + class_list → every object: class, kind, geom.nameTpl, world matrix (TSV) | `python3 tools/level/placement.py <scn_dir> hub_battle_barge out.tsv` |
| `tools/level/placement_view.py` (R54) | placement.tsv → standalone HTML marker view (top/side/orbit, kind colours) | `python3 tools/level/placement_view.py in.placement.tsv out.html` |
| `ForgeLens --levelbench` (R54) | level from the user's own game files via `Level/LevelScene` (same code as the viewer click on `scenes/<lvl>.scn/<lvl>.cd_list`): cd_list+class_list+lwi_container, templates, textures, point lights, centred camera; placement cache `%LOCALAPPDATA%\ForgeLens\levels`; renders top/persp/close; ue=1 writes templates/*.obj + instances_ue.tsv (local test output, never commit) | `ForgeLens --levelbench <client_pc> <level\|list> <out> [ue 0\|1]` |
| `ForgeLens` level cache (R54b) | `%LOCALAPPDATA%\ForgeLens\levels\<lvl>.flpack` (built template geometry + placements + cameras + texture refs, keyed by game file stamps + options, CacheVersion) and `%LOCALAPPDATA%\ForgeLens\textures\<catalogFileCount>\<fileIndex>_<edge>.tex` (shared decoded textures). User cache only, never shipped; delete to force a rebuild; `SM2_LEVEL_NOCACHE=1` bypasses it in levelbench | automatic on level open |
| `ForgeLens` env `SM2_SELECT` / `SM2_WINSHOT` (R54b) | drive the real viewer: `SM2_SELECT=scenes/<lvl>.scn/<lvl>.cd_list` selects that row in the file list (same handler as a click), window opens without activation; `SM2_WINSHOT=<png>` saves an in-app window capture + `<png>.txt` (stats, open time) | `tools/re/pc/r54_viewer_test.ps1 -Level pvp_city` (also a PrintWindow capture) |
| `ForgeLens` fly camera + env `SM2_FLYTEST` (R64) | viewer `Fly` toggle: RMB look, WASD/QE, Shift x4, wheel+RMB speed, F frame (levels default fly). `SM2_FLYTEST=1` with `SM2_WINSHOT` runs W 2 s / look / Shift+D,E / F via the same code and saves `<png>_fly1..3.png` + eye positions in `<png>.txt` | `$env:SM2_FLYTEST='1'; tools/re/pc/r54_viewer_test.ps1 -Level hub_battle_barge` |
| `ForgeLens` HUD + env `SM2_WINSHOT_CPU` (R66) | viewport HUD (H / `HUD` toggle). `SM2_WINSHOT_CPU=1` switches a mesh to the CPU path after load so in-app shots include the 3D view; FLYTEST adds `_fly4` (RMB+wheel speed), `_details`, `_nohud` shots. `r54_viewer_test.ps1 -Select <catalog path> -Exe <exe>` opens any file | `$env:SM2_FLYTEST='1'; $env:SM2_WINSHOT_CPU='1'; tools/re/pc/r54_viewer_test.ps1 -Select tpl/cc_calgar.tpl/cc_calgar.tpl` |
| `ForgeLens --meshparts` + `tools/re/pc/r71_viewer_test.ps1` (R71) | per-part inventory TSV (hideReason, nonRender) for models matching a path regex; test script opens a model via `SM2_SELECT`, `SM2_MESHGROUP_SHOT=1` saves collapsed/expanded Meshes-group shots, mesh tree dump, window title |
| `tools/re/pc/r74_memsoak.ps1` + `SM2_MEMSOAK` hook (R74) | scripted memory soak in the real window: opens a `;` list of catalog paths like a click, per step open time, peak WS/private, after-GC private, managed, levels cached → TSV; `-LevelCache 99` reproduces the pre-R74 keep-every-level cache. App always logs `%LOCALAPPDATA%\ForgeLens\memory.log` every 30 s |
| `ForgeLens --thumbbench` + env `SM2_PICKTEST` / `SM2_PICK_XY` / `SM2_ORBITTEST` / `SM2_ORBIT_DAMPING` / `SM2_KITBASH` (R75) | thumbnail clay vs textured PNG + ms; pick hover/select/deselect GPU frames + latency log; orbit-physics fling ON/OFF cloth deviation log + frames; inertia damping; open/close the Kitbash section for shots |
| `tools/ue/ue_r54_level.py` (R54) | levelbench output → UE scratch map `/Game/SM2_Work/Tests/r54_level` (import/place/capture/export/reset) | `uerun.ps1 -py ue_r54_level.py -argv import,<dir>,0,40` |
| `tools/ue/ue_r53_cloth.py` (R53) | import a chars_cloth FBX into a NEW `/Game/SM2_Work/Tests/r53_cloth/<n>` and run CreateChaosCloth; prints plugin report | `uerun.ps1 ...\ue_r53_cloth.py cc_sorcerer_exalted` |
| `tools/ue/ue_cleanup_refs.py` (R57) | delete UE assets under dirs only if every referencer is also deletable (fixpoint); refuses Original/Work/Lightning_01; prints R57DEL/KEEP/SUM | `uerun.ps1 ...\ue_cleanup_refs.py /Game/SM2_Work/Tests/x[,dry]` |
| `tools/ue/ew_socket.py` (R58) | add skeleton sockets `SM2_<bone>` via EngineWire create_socket (Python API is read-only), then list | `python ew_socket.py <meshPath> shootTip tip tip_gl` |
| `tools/ue/ew_schema.py` (R58) | print EngineWire tool input schemas (token never printed) | `python ew_schema.py control_editor --grep screenshot` |
| `tools/ue/ue_seq_fire.py` (R59) | F46-style fire Level Sequence: duplicates LS_TitusWalkFire, aim pose = MUZZLE_AIM.tsv aimHoldClips clip (replaces pitchUp35), MuzzleFlash bindings attached to `SM2_shootTip` (flag `tip`); optional frame opens it | `uerun.ps1 ...\ue_seq_fire.py <MUZZLE_AIM.tsv>,arifle_bolt_rifle_client.cls,shootTip,/Game/SM2_Work/Sequences/BoltRifleFire,180` |
| `tools/ue/ue_view_actor.py` (R59) | aim the level viewport at an actor (incl. Sequencer spawnables) for screenshots | `uerun.ps1 ...\ue_view_actor.py "Skeletal Mesh Actor2,220,180,-5,0"` |
| `tools/ue/ue_pie_phys.py` (R60) | Simulate physics check on a scratch map under Tests: setup/anims/addcap/sim/osc (per-frame tick: stretch + jump of simulated bodies, phys vs `nophys` baseline)/result/cap (SceneCapture PNG, viewport-independent)/stop | `uerun.ps1 ...\ue_pie_phys.py setup` · `osc,8,80,1.0,phys` · `result` · `cap,<png>` · `stop` |
| `tools/ue/ue_r63_cloth.py` + `r63_pass.ps1` (R63) | readable cloth A/B check: prep (dup + create_chaos_cloth), scene (grey floor, lights, manual exposure, ON/OFF actors, SceneCaptures), place, anim, sim/osc/cap/measure (measure_cloth_penetration/skin/wrong_side)/stop, tune | `uerun.ps1 ...\ue_r63_cloth.py prep` · `scene,0` · `place` · `r63_pass.ps1 -Tag base` |
| `tools/ue/ue_r67_phys.py` + `r67_pass.ps1` (R67/R68) | ON/OFF physics check per subject (calgar jiggle via post-process ABP, vortex tentacle Chaos cloth): prep_vortex, rootzero (zero ROOT translation of the calgar test clip), scene,<subj>,<ev>, cap, sim/stop, osc, measure (jiggle ON-vs-OFF degrees, cloth penetration), bodies,<mesh> |
| `tools/ue/ue_r72_mats.py` (R72) | SM2 materials for meshes left on WorldGrid: state (slot/WorldGrid/MI-with-texture counts), apply,<name>[,<exp>] (exported import_sm2_materials.py from a png export, default `Exports/r72`; dds-only exports fail in UE 5.7 for BC7), copy (Characters MIs onto test duplicates by slot name), scene,<ev> / cap,<dir>,<tag> (before/after map) |
| `tools/ue/ue_r67_cloth.py` (R69) | sidecar variants of the sorcerer cloth (collision radius scale, backstopCm) rebuilt with create_chaos_cloth into Tests/r67_cloth; `use,<tag>` swaps them onto Map_Cloth for r63_pass |
| `tools/ue/ue_r64_level.py` + `r64_level.ps1` (R65) | r54 scratch level: stats, fixzero (re-import zero-bound meshes), light (dir/sky/point/spot + manual exposure PPV), capture overview + 2 close-ups, export | `uerun.ps1 ...\ue_r64_level.py fixzero,<r54_mesh dir>` · `r64_level.ps1 -Ev 1.5` |
| `tools/ue/ue_r66_level.py` + `r66_level.ps1` (R66L) | r54 scratch level from `--levelbench <game> <level> <dir> 1`: reset, textures, masters, mis, meshes (OBJ + slot MIs), place (one ISM actor per template), sky, capture/export (overview, close1-3, viewer cams vpersp/vclose), probe, stats. EngineWire via uerun.ps1; only /Game/SM2_Work/Tests/r54_level |
| `tools/ue/ue_r51_muzzle.py` (R51) | import a weapon FBX (Exports/r51) as skeletal into `/Game/SM2_Work/Tests/r51/<w>`, list tip bones (shootTip/tip/tip_gl) for attaching the muzzle | `uerun.ps1 ...\ue_r51_muzzle.py wpn_bolt_rifle_01` |
| `ForgeLens.exe --openbench <game> <out.tsv> <model.tpl>...` (R76) | headless model-open stage timing: mesh decode, texture read vs decode (serial), ResolvePreview (parallel), byte compare of decoded images → TSV |
| env `SM2_UETEST=1` (with `SM2_SELECT` + `SM2_WINSHOT`; run via `tools/re/pc/r71_viewer_test.ps1 -Env "SM2_UETEST=1" -Done "settings file"`) (R79) | UE-style viewport check: 6 view modes, Show bounds/grid/skeleton/helpers, wheel-toward-cursor, Alt+RMB dolly, bookmarks, F on selection, outliner search/hide/solo/undo/redo/copy, stats + help; GPU + window PNGs `<shot>_ue_*.png` + log in `<shot>.txt` |
| `tools/level/bc6hdr/` (R80) | raw BC6H mip (e.g. `buttle_barge_sky_1_0.pct_mip` 4096x2048) -> Radiance .hdr float (BCnEncoder DecodeRawHdr), prints max / mean luminance | `dotnet run -c Release -- <in> 4096 2048 out.hdr [u|s] [scale]` (PC) |
| `tools/level/sky_env.py` (R80) | UE: game sky + env lighting for the r54 hub copy: state / import <hdr> (TextureCube) / apply [skyInt] [fogScale] (specified-cubemap SkyLight -44.33 deg, atmosphere hidden, game fog + sun colour) / revert | `uerun.ps1 ...\sky_env.py state` |
