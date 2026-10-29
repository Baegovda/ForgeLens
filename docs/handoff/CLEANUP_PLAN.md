# CLEANUP_PLAN — /Game/SM2_Work (inventory only, 2026-10-30 KST)

Nothing was deleted, moved or modified. Editor was open; files were only read.
Project: `K:\Unreal Projects\UE_VFX_57\Content\SM2_Work`. Full per-package list: `CLEANUP_CANDIDATES.tsv` (path, disk bytes incl. .uexp/.ubulk, category, reason, evidence, refcount).

## Method
- Scanned all 258,460 .uasset/.umap under `Content` (whole project, not only SM2_Work), first 4 MB of each (header/name table), regex `/Game/...` → package reference graph. 3,836 packages under SM2_Work, 1,145 with zero referencers.
- Limits (so refs are **INFERRED**, not PROVEN): name-table string match, not a full import-table parse; soft refs from Python/config/Blueprint strings, DDC, and assets the user opens by hand are invisible; packages whose header exceeds 4 MB could miss refs (not measured how many).
- Exporter code (`tools/ue/*`) writes to `/Game/SM2_Work/Tests/...` for test runs, `Characters`, `Props`, `Animations`, `Anims_Mannequin`, `Sequences`, `VfxUe`, `VfxExport`, `Materials` — those are "current" targets.

## Sizes (disk, measured)
| Folder | files | MB |
|---|---|---|
| Tests | 2,997 | 18,486 (bulk_fix 2,775 · chars_cloth 8,536 · chars_phys 4,827 · oneclick 541 · physics 1,083 · pre_step3 531 · retarget 117 · sani_before 77) |
| Characters | 291 | 936 |
| Original (protected) | 136 | 468 |
| Sequences | 58 | 200 |
| Props | 115 | 185 |
| Anims_Mannequin (+_y0) | 7 | 81 |
| Animations | 15 | 71 |
| VfxExport / VfxUe / VfxUeRules | 70/78/28 | 15 / 7.5 / 0.3 |
| VfxUe_bak1/2/3 | 100/89/66 | 7.4 / 11.9 / 11.6 |
| **Total** | **4,051** | **20,481** |
Files with `_bakN` in path: 595, 6,492 MB.

## (a) Old versions / scratch — archive candidates (14,177 MB total, 3,038 packages)
- `Tests/*` (all subfolders except the 4 packages in (d) and `PhysTestMap`): test-run outputs, refs only from inside Tests or none. INFERRED. Biggest: chars_cloth ≈4.6 GB (incl. `cc_calgar/sorcerer_exalted/tech_priest_ChaosCloth_bak1..15`, 15 versions each), chars_phys ≈4.6 GB, bulk_fix ≈2.8 GB.
- `VfxUe_bak1`, `VfxUe_bak2`: superseded copies of `VfxUe`. PROVEN by name; refs only between bak folders.
- `VfxUe_bak3`: **except** `Materials/sm2d_white` and `Materials/sm2d_white_lin` — current `VfxUe/Materials/M_SM2Vfx_{Backdrop,Distort,Premul,Spark,Trail}` and `PP_SM2Tonemap` still point at them (INFERRED from name table). Fix those refs first or keep the two textures.
- `Props/hologram_planet/hologram_planet_Skeleton_bak1..3` (0.1 MB), `Sequences/TitusWalkFire/Backup/*_bak1` (0.3 MB). PROVEN by name, unreferenced.
- Proposed action: move to `/Game/SM2_Work/_Archive/2026-10-30/<same path>` **inside the editor** (Content Browser move / editor script → redirectors, then Fix Up Redirectors). Never move .uasset on disk (breaks refs). Real disk is only freed when the archive is later deleted (R04 batch cleanup, needs the owner's OK).

## (b) Unreferenced non-Test assets — need owner check (category d, 386 MB)
Unreferenced ≠ unused: these are mostly top-level assets the user opens directly.
- `Sequences` 12 pkgs 117 MB, `Anims_Mannequin` 6 / 81 MB, `Animations` 15 / 71 MB (incl. stray `cc_calgar` flagged in BOT_TRANSFER §7.13 → archive), `Props` 6 / 15.7 MB, `VfxExport` 22 / 2.4 MB, `Characters` 72 / 1.4 MB, `VfxUe` 44 / 0.5 MB, `VfxUeRules` 15 / 0.2 MB, `Materials` 1, `Anims_Mannequin_y0` 1. HEURISTIC for "unused".

## (c) Protected / in use — do not touch
- `/Game/SM2_Work/Original/*` (136 pkgs, 468 MB), `/Game/Work/*` (Original refs `/Game/Work/02`), `Lightning_01` (not under SM2_Work), map `Tests/PhysTestMap`.
- `Characters`, `Props`, `Sequences/TitusWalkFire`, `VfxUe`, `Animations` referenced packages.

## (d) Uncertain — Tests assets used from outside Tests (keep or re-home first)
- `Tests/chars_phys/cc_titus_assault/cc_titus_assault` ← `Sequences/TitusWalkFire/LS_TitusWalkFire` (+ `_bak1`)
- `Tests/chars_phys/cc_titus_assault/cc_titus_assault_Skeleton` ← `ABP_TitusWalkFire`, 3 `Anims_Mannequin/SM2ref` clips, a TitusWalkFire motion clip
- `Tests/pre_step3/cc_swarm_hormagaunt/cc_swarm_hormagaunt_Skeleton` ← `Animations/cc_swarm_hormagaunt/Bulk/...attack_1`
- `Tests/sani_before/hologram_planet/localbake/hologram_planet` ← `Props/hologram_planet/hologram_planet_Skeleton`
(97 MB). If Tests is archived, these must move with redirectors or be re-pointed.

## Not determined
- Editor-side truth (Reference Viewer / Size Map), soft refs, and whether the open editor has unsaved changes on any candidate.
- Exact reclaimed disk after an editor move (redirectors left behind; size only freed on later deletion).
- "Superseded export" mapping per asset beyond folder/name patterns.
