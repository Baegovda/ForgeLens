# Polish R48–R56 (10/30 night, owner asleep, t108u "알아서 다 하고 선조치후보고")
| R | Item | Result | Evidence | Undo |
|---|---|---|---|---|
| R48 | L/R pauldron, gauntlet, greave, bracer colours | `SectionOf` side-aware | affixes PROVEN (`character_body_section_library.sso`); name→side INFERRED | revert commit |
| R49 | Colour scheme → `M_SM2` | `UseColourScheme` switch + importer reads the single `*.colour_scheme.*.json` | M_SM2 compiled via EngineWire in `/Game/SM2_Work/Tests/r49_scheme` (test asset kept); not run on a real export | revert; the test folder can be removed by the owner |
| R50 | F43 td scalars | `tdScalars` (nmScale, emissive intensity/bloom/adaptive) → sidecar → MI metadata `SM2.td.*` | td fields PROVEN (38,258 td scanned); graph mapping not done (emissive units unproven) | revert commit |
| R51 | Muzzle flash offset / aim pose | NOT DONE | needs an REA session; `/workspace/titus_seq` is gone from the box | — |
| R52 | Physics other characters | applied to cc_calgar (3 jiggles), cc_sorcerer_exalted (2 jiggles, damper PA, 0 damper bodies), cc_vortex_beast (0 sim bodies); cc_swarm_hormagaunt has no physics sidecar | EngineWire; mesh backups `<n>_bak1`; meshes had no PA/ABP before | re-assign none / copy back from `<n>_bak1` |
| R53 | Cloth build28 | installed DLL = pkg52b (10/29 11:03), its source has the mtl1-28b fix (INFERRED) | no new functional cloth run tonight | — |
| R54 | Level / prop placement | NOT DONE | `/workspace/mtl_fix/level_lights` gone from the box; needs a new design | — |
| R55 | `_bakN` check (list only) | `BAKN_LIST.tsv`: 188 files, 4,123,337,408 B (186 Tests/chars_cloth, 2 VfxUe_bak3) taken before R52 added 3 `_bak1` meshes | filesystem scan | — |
| R56 | proj_sm2 backup | `K:\SM2_Backup\proj_sm2` 9 files, 1,218,790,475 B, source/dest sha manifests identical; GhidraServe restarted (pid 73032, ping ok) | backup.ps1 record+copy | — |
