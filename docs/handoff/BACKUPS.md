# BACKUPS — big non-git artefacts (2026-10-30 KST). Not committed; rebuild steps in brackets.
| Artefact | Where | Size | sha256 / note |
|----------|-------|------|---------------|
| sm2_retail.exe (analysed build) | PC `re_env\work\sm2_retail.exe`; box `/workspace/rea/sm2_retail.exe`; = installed `...\client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe` | 85,129,768 B | `4c4fb3a009a803ea5c27e9ced0a36e7c401ef95eb6dd7d651a629f54ee0252fc` (FileVersion field empty) [copy from game] |
| ghidra_proj3 | box `/workspace/rea/ghidra_proj3` | 880,985,101 B (9 files) | Ghidra DB dir, no single hash [headless import of the exe, ~46 min box] |
| proj_sm2 | PC `re_env\proj_sm2` (GhidraServe) | see `gs.ps1 status` | Ghidra DB dir [`gs.ps1 import`, ~10–15 min PC] |
| REA temp project | PC temp, deleted on daemon exit | ~20 GB RAM | not a backup target [`start_rea.ps1`] |
Rule: never back these up into git; if lost, rebuild with the bracket command.
