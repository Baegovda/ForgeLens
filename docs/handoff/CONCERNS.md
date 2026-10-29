# CONCERNS — every risk raised with the owner, its measure and status (R41, 2026-10-30 KST)
Status: **done** (built and run once) · **done-untested** (built, not run for real) · **rule-only** (written rule, no tool) · **none**.
**Rule:** a number or name not measured in this session must be labelled *unmeasured*. Never fill a gap with a plausible value.
**Rule:** every owner decision goes into the HANDOFF ledger immediately (not at the end of the task).

| # | Concern | Measure | Status | How verified |
|---|---------|---------|--------|--------------|
| 1 | Analysis cache re-read every session | `RE_CACHE/INDEX.tsv` + `recall.py` before any decompile | done | R39 tests; INDEX 880 rows |
| 2 | Bot forgets new tools | README RE toolbox + BOT_TRANSFER §0 toolbox/STATE | rule-only | docs_check lists the tool files |
| 3 | Docs drift from reality | `docs_check.py [--pc]` | done | R39 found `query`→`pattern`; R41 run on PC |
| 4 | Unversioned big files lost | `pc/backup.ps1` → `K:\SM2_Backup` + sha256 manifest + verify | done (partial) | R41 run: see BACKUPS.md; ghidra_proj3 (box only) not copied to PC |
| 5 | Env / tool version drift | versions in §0 STATE; exe sha256; REA pin | done | exe_check MATCH 10/30 |
| 6 | INFERRED piling up | `UNVERIFIED.md` register (U1–U11) | rule-only | — |
| 7 | Push ordering / stale origin | `box/refresh_origin.sh` after every push | done | used R39 |
| 8 | Bot memory loss (long chat / compaction) | §0 STATE block + ledger-at-once rule | rule-only | — |
| 9 | PC dependency / reboot | manual one-command start `start_rea.ps1`, `gs.ps1 start` (owner: no logon auto-start); both no-op when running | done | R41 idempotence run: both printed no-op, pids 61672/95404 untouched |
| 10 | Memory shortage | `mem_watch.ps1 -Once` every 10 min → alerts.txt `[mem]` | done | R41 forced alert (threshold 999 G) written then cleared |
| 11 | Parallel push conflict | `box/push_lock.sh` + one-push rule | done | test_r40 PushLock |
| 12 | REA version change | `rea_version.py` (pin 6.8.0) in start_rea.ps1 + docs_check --pc | done | PC: "REA version OK 6.8.0" |
| 13 | Over-trust in PROVEN | `VISUAL_CHECKS.md` V1–V3 | rule-only | owner's visual comparison **not yet done** |
| 14 | Game patch | `patch_watch.ps1` daily → alerts.txt `[patch]`; relocate procedure in exe_check.py | done | R41 run: MATCH; mismatch path not exercised |
| 15 | Usage limit | check on request (CheckSubscriptionUsage) | none | — |
| 16 | Wrong autonomous judgment while owner away | ff-only pushes, never delete, `.bakN` never overwritten | rule-only | — |
| 17 | GitHub access loss | pushes go through the PC (`gh` logged in) | none | unmeasured |
| 18 | Disk space | `disk_watch.ps1` every 10 min (C: <100 GB, others <50 GB) | done | R41 run: C:302 D:649 E:2867 J:119 K:784 GB free |
| 19 | Security | game data read-only; servers loopback + token; no untrusted exes | rule-only | not audited |
| 20 | Ghidra server speed | measured | done | R39/R40 README numbers |
