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
