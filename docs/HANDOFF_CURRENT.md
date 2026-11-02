# HANDOFF_CURRENT — ForgeLens (reconciled 2026-11-02)

> **This file wins** over `docs/HANDOFF_NEW_BOT.md`, `docs/handoff/STATUS.md`, `docs/handoff/NEXT_BOT_PROMPT.md`, `README.md` status lines, `HANDOFF.md` bootstrap, and older milestone headers when they disagree.
> Written by a docs-only reconciliation against the tree at parent `f82bb4b` (no runtime code changes). Times in KST. Labels: **CODE** = read in this tree this session. **DOC** = written in-repo, not re-run. **PC** = lives on Revaptor-FX; not checked from this checkout.

Repo: https://github.com/Baegovda/ForgeLens (private). Branch `cursor/sm2-pck-extractor-4420`. App **ForgeLens.exe** version **0.97.0** (`src/LibSaber.SM2Browser/LibSaber.SM2Browser.csproj` `<Version>`; window title `Forge Lens v{AppVersion.Version}`).

---

## 1. Git (measured)

| Fact | Value |
|---|---|
| Last **runtime** commit | `3a6ba3f` — R99: SM2PhysicsSetup BuildPlugin deploy + commissar coat PIE A/B |
| Docs-only commits after that, before this file | `8790595`, `33a8cda`, `f82bb4b` |
| Other branches around 2026-11-01 | **none.** `origin/master` is `949d9d5` (2026-10-26, "Point agents at the SM2 branch"). |
| vs `origin/master` at `f82bb4b` | **390 ahead / 1 behind** (`git rev-list --left-right --count`). Older docs said ~387 (true at `3a6ba3f`, stale after the three docs commits). |
| `master` | Owner decision only. Do not merge or push `master` from a docs pass. |

Runtime commits after the old "cleanup tip" `1461bbf` that docs still cited as "app tip": `98c6886` test fix, `4960763` UseSpecMap sidecar, `5db0ee5` rest-fit sidecar, `e9ad274` hub lights core, `ddc8f88` REA-proven UseSpecMap + lights + cloth thickness, `4be1d4e` GlPreview UseSpecMap via `PreviewPartDraw.Source`, `3a6ba3f` R99 plugin deploy notes + coat A/B script.

---

## 2. Architecture (CODE)

Shared pipeline. Game-specific readers feed the same mesh / clip / material / preview / export path. Unreal content root: `/Game/ForgeLens/<CODE>/` with legacy `/Game/SM2_Work` when `FORGELENS_UE_ROOT=legacy` (`UnrealContentPath` in `src/LibSaber.SM2.Assets/Export/UnrealDirect.cs`, mirror `tools/ue/fl_roots.py`). Explicit `GameRoots` keys are **SM2** and **POE** only. Any other code (BMW, …) uses the fallback `/Game/ForgeLens/<CODE>/`.

| Piece | Where |
|---|---|
| Formats | `src/LibSaber.SM2.Assets` (SM2). PoE2: `Games/Poe2/` (`Poe2Reader`, terrain, skills, particles). Wukong draft: `Games/Bmw/BmwGlb.cs` |
| GUI | `src/LibSaber.SM2Browser` → `ForgeLens.exe` (Avalonia). PoE2 window `Poe2Window.cs`. Wukong window `BmwWindow.cs` |
| CLI extractor | `src/LibSaber.SM2Extractor` — `list` / `extract` / `export` only (`ExtractorOptions.cs`). **PoE2 switches are not on this exe.** |
| CLI benches | `ForgeLens.exe` (`Program.cs`): `--levelbench`, `--bench`, `--clothpen`, `--physbench`, `--vfxbench`, `--hostvfxbench`, `--vfxue`, `--vfxlights`, `--vfxemitters`, `--vfxue-rules`, `--fxparts`, `--propbench`, `--thumbbench`, `--meshparts`, `--openbench`, `--poe2skills`, `--poe2prep`, `--poe2level`, `--poe2export`, `--glbench` |
| PoE2 GGPK | `ForgeLensPoe/ForgeLens.Ggpk3/` (private LibGGPK3 copy; remove before distribution) |
| UE Python | `tools/ue/*.py`, `tools/poe2/*.py`, `tools/bmw/ue_bmw_import.py` |
| UE plugin | `tools/ue/SM2PhysicsSetup` (`SetBodiesKinematic`, `ListBodies`, cloth rest-fit, collision thickness) |
| RE | `tools/re/rea_daemon.py`, `rea_call.py`, `nmb_reader.py`, `docs/SYMBOL_DB/`, `docs/re/` |
| Halo | `LibSaber.HaloCEA` / `Halo2A` — out of scope |

**PoE2 argument shapes (CODE, `Poe2Bench.cs` / `VfxUeExport.cs` / `Program.cs`):**

- `--poe2skills <prepared> <out.tsv>`
- `--poe2prep <ggpk> <prepared folder> <tool dir> <game path>…`
- `--poe2export <prepared> <out> <mesh.sm|.fmt> [rig.ast] [clip|*]` (`FL_POE2_ALLCLIPS=1` = all clips)
- `--poe2level <prepared> <out> <room.arm> [ggpk tool dir]`
- `--vfxue poe2:<prepared> - - <out> <effect.ao>@seconds…`
- `--glbench poe2:<prepared> "<mesh>|<ast>|<clip>" <out>`

**SM2 extractor (still accurate in README):** `list` / `extract` / `export` with `--filter`, `--prefix-archive`, `--no-overwrite`, `--anim`, `--format fbx-unreal|gltf|both`, `--lod`, `--no-root-motion`, `--variant`, `--tex-formats`. Exit codes 0 / 1 / 2 as README states.

**.sfx today (CODE, `AssetExporter.cs`):** a text `.sfx` exports `{name}.sfx.json` plus `{name}.niagara.json` (emitter fields). It does **not** create a Niagara asset. Binary `.ps` / `.sfx` stay raw. The GPU viewer **does** draw `.sfx` (not "text/hex only").

---

## 3. What works (CODE unless marked DOC)

### SM2
- Archive list/extract, textures, `.tpl` meshes, `.sani` clips, FBX/glTF export, material sidecar `materials.json` + `import_sm2_materials.py`.
- **UseSpecMap PROVEN and in code** (`ddc8f88`, `4be1d4e`): `UseSpecMapPolicy` — sample packed spec when usage has MSCRGHAO (bit 21 → flag `0x2000`) or MSCG (bit 18 → `0x40`). Writer `FUN_14217da70`. Sidecar `useSpecMap`. Viewer `GlPreview` uses the same `Decide` via `PreviewPartDraw.Source`.
- **Level lights:** packer `FUN_14224ecf0` + P0.w `FUN_14224f010` PROVEN. `LevelLightUe.UeIntensity` = authored I × colorScale (×400 only if inverse-square). **FIT K=10 is removed.** Spots keep cones. Direction is **not** in the lights TSV → UE spot forward (**INFERRED** gap).
- **Cloth defaults:** `PhysicsParser` `collisionThickness` default **0**. `RestFitPolicy.Enabled` = **false** (exe has no rest-fit knobs; optional heuristic). Plugin reads sidecar and defaults thickness 0 / rest-fit off (`SM2PhysicsSetupLibrary.cpp`).
- Browser cloth solver already collides with **capsules / spheres / boxes** from cloth config (`ClothSolver.cs`). That did not stop the commissar coat PIE penetration (see TODO).
- Player fire clips: `PlayerFireClips.For` is a **hand table** from `primaris.nmb`, not a live bytecode interpreter. Many rows INFERRED; 1.4 / 1.41 / 1.5 / 1.51 / 1.7 / unassigned / heavies-in-aim still **HEURISTIC**.
- Muzzle locator **data path exists**: `ContextIndex` reads `prop_firearm_shoot_tip.shootTip.locator` (default name `shootTip`). Attach at that node only when `SM2_MUZZLE_SHOOTTIP=1` (`ContextResolver.MuzzleAtShootTip`, **default off**).
- VFX viewer, frozen-frame `--vfxue`, one-click export, colour schemes, prop anim, physics sidecar — landed across R35–R98 (**DOC** for UE visual results).

### PoE2
- M0–M3 readers and UE scripts are in tree (`docs/POE2_M0.md` … `M3.md`).
- M2 H VFX code `edf8e92` is in git. **Visual leftovers are still open** in `docs/POE2_M2.md` (Frost Bomb cut edge, Glacial Bolt dark blobs, several skills barely visible).
- `SetBodiesKinematic` + `ListBodies` are in the plugin header (`760c055`). R99 notes say the plugin **binary** was built with RunUAT on the PC (**DOC**, not re-run here). BellCrow PIE after that API was **not** the R99 test.

### Wukong
- M0 survey: `docs/WUKONG_M0.md` (community AES key is in that file on purpose; tools must use env `BMW_AES_KEY` only).
- M1 draft in tree (`1461bbf`): `BmwGlb.cs`, `BmwWindow.cs`, `tools/bmw/ue_bmw_import.py`. **No** pak decrypt reader in git (`Program.cs` / `bmw_pak.py` were left untracked because they contained key literals). Index decrypt **not** verified.

### Not started
Stellar Blade, Elden Ring.

---

## 4. In progress

| Item | State |
|---|---|
| Ghidra full analyze of Retail.exe | **PC.** Last write-up: headless import failed (`IllegalArgumentException: Negative delay`), `sm2.gpr` size 0, java PID 102852 left running (~04:38 KST). Do not kill unless the owner says so. Restart with a timeout that does not overflow `Timer`. **Not re-checked this session.** |
| REA open lighting | IBL numeric CB not proven. Shadow bias/cascades: names exhausted in the exe (`docs/re/REA_DEEP.md` §5) — need `.sdc` or a capture. Spot **directions** missing from the lights TSV. UE intensity unit bridge **INFERRED**. Evidence IDs still "pending" until a healthy Ghidra analyze. |
| Coat vs body | R99 A/B script `tools/ue/ue_r99_coat_ab.py`. **DOC** measure: free penetrations 206 → 303 when thickness 0 and rest-fit off; heuristics **not** turned back on. Next: capsule data / Chaos setup, not a silent restore of thickness or rest-fit. |
| nmb ops 95 / 98 / c4 | Meanings **DATA-PROVEN** in `docs/re/REA_DEEP.md` §6 (`c4` jump-if-falsey, `98` jump-if-truthy, `95` prep/coerce with ALU **INFERRED**). Exe dispatch table not found. **`nmb_reader.py` and `PlayerFireClips.cs` do not implement those meanings yet.** `REA_DEEP.md` §4 still says the ops are open; **§6 wins.** |
| Material re-export / UE reimport | Not done. UseSpecMap policy is in code; existing UE material instances are stale until reimport on the PC. |
| UE old-asset delete | **Pending PC verification.** See §6. |
| `ForgeLens_latest` | **PC, conflicting docs.** README/STATUS said a publish of `1461bbf` (2026-11-02 01:52). A later HANDOFF section said a publish of `eddf91e`. Nothing in git records a publish of `3a6ba3f`. Check the exe timestamp on the PC before telling the owner the viewer matches tip. |
| PoE2 P2 PIE | Plugin API exists; BellCrow kinematic PIE after the R99 plugin build is **unverified**. |
| Niagara | Not started. Owner gate (P11). |

---

## 5. TODO (verified against this tree)

Do these in order unless the owner picks a track. Do not start Niagara, do not launch any game, do not force-push, do not touch `master`.

1. **Ghidra, then REA (PC only).** Confirm whether PID 102852 / `C:\Users\Revaptor_FX\sm2_re\ghidra_full.log` is still the failed import. If it failed, restart headless on Revaptor-FX (large heap, do not pass an analysis timeout that overflows). Then REA (`tools/re/rea_daemon.py`) for IBL CB numbers, shadow bias/cascades from `.sdc` or capture, and spot-light direction (cd_list 4×4 / matrix — the lights TSV has none). Re-cite Evidence IDs in `docs/re/REA_FINDINGS.md`. **Never run Ghidra full-analyze on the bot box.**
2. **Coat clipping.** Use collision capsule data (cloth config capsules are already parsed; REA says capsule stride 0x48, `collisionThickness` default 0). Find why Chaos still puts the commissar coat through the body after thickness 0. Do **not** re-enable `RestFitPolicy` or a 2 cm thickness without the owner. R99 numbers 206 and 303 are from the previous PC run (`docs/handoff/r99/` notes); re-measure if you change anything.
3. **Light units + spot direction.** Keep `LevelLightUe` (no K=10). Finish the UE candela / falloff-exponent bridge (still INFERRED). Find spotlight direction and thread it into `ue_level_lights_core.py` / `ue_r98_hublights.py`.
4. **Re-export all SM2 materials and reimport** under `/Game/ForgeLens/SM2/` (and legacy only if that map still uses it) so `UseSpecMap` matches `UseSpecMapPolicy`. Editor lock. Save only our packages. Log deletions in `docs/handoff/CLEANUP_DELETED.tsv`.
5. **Fire clips + muzzle.** Apply §6 op meanings in `tools/re/nmb_reader.py` (`run_expr` still takes both branches for unknown ops) and in `PlayerFireClips` for wpnType 1.5 / 1.7 (and re-check 1.51 aux). Then switch muzzle attach to the cls `shootTip` locator as the **default** (`SM2_MUZZLE_SHOOTTIP` is opt-in today). Visual check is owner-only — do not ask for in-game shots; do not claim the flash sits on the barrel until a capture exists.
6. **PoE2 (owner-gated polish, code already started):**
   - **P1** visual leftovers still listed open in `docs/POE2_M2.md` (Frost Bomb edge, Glacial Bolt blobs, faint skills). Commit `edf8e92` did not close those sentences.
   - **P2** BellCrow: call `SetBodiesKinematic` on the chain-root bells, PIE with `tools/poe2/ue_poe2_pie.py`. Plugin rebuild was claimed in R99; confirm the DLL on the PC is the R99 binary before PIE.
   - **P3** BongThumbLive dots spinner — other repo; only if the owner asks.
   - **P4** levels past the space hideout (tile rotation, ground blend). Not in code.
7. **Wukong M1.** Pak index decrypt with `BMW_AES_KEY` only. Do not commit key literals. Draft window/GLB import is already in tree. `GameRoots` has no BMW entry; fallback path is `/Game/ForgeLens/BMW/` — add an explicit key only if a caller passes a different game string.
8. **Niagara (P11).** Offer only. No converter work until an explicit go.
9. **UE cleanup** — §6. Resume only after a live referencer check and `editor-free`.
10. **Carried, not re-verified this session:** F45 cloth motion vs game (owner capture); chaplain test-folder delete (materials still referenced — keep); viewer "Unreal-style UI" batch (no commit found; scope unknown); CPU-mesh / GL-texture memory budget (`PerfBudget` covers threads and texture decode only, **DOC** from prior handoff); BongLodge unverified items; `ForgeLens_latest` republish.

---

## 6. UE cleanup — pending verification (PC, not in git)

The previous handoff (`docs/HANDOFF_NEW_BOT.md`, labelled verified **2026-11-02 ~04:52 KST**) said the delete had **not** landed: every candidate folder still existed, the KEEP set was present, and `ue_editor_busy.txt` was `editor-free`. **This session did not read the UE project disk.** Treat that table as a snapshot, not current truth.

The owner’s last chat note (not stored as a measured repo file): an Unreal cleanup of **about 817 old assets (~4.0 GB)** under `/Game/ForgeLens/` was **interrupted partway**. Those two numbers are **not** in the repo. `docs/handoff/CLEANUP_PLAN.md` is an older **`/Game/SM2_Work`** inventory (2026-10-30) and is a different cleanup. The candidate CSV cited by the previous bot is `C:\Users\Revaptor_FX\sm2_re\ue_old_assets_candidates.csv` (not in git).

**Intended delete set** (paths under `Content/ForgeLens/SM2/`, from that handoff):

- `Tests/r63_cloth`, `Tests/r67_cloth`, `Tests/r67_phys`, `Tests/r84_legacy`, `Tests/r96` (nested bak, including `cc_sorcerer_bak1`), `Tests/r97`, `Tests/r97_orbdark`
- `Tests/chars_phys/cc_commissar`, `Tests/chars_phys/cc_tech_priest` — there is **no** top-level `SM2/chars_phys` target
- `Sequences/BoltRifleFire_r97`

**KEEP** (same snapshot; do not delete without a new referencer check): `Tests/r92_floor`, `Tests/r54_level`, `Tests/r72_mats`, `Tests/chars_cloth2` (and chaplain folders), `VFX/vortex_orb`, `Tests/r90_cloth`, `Tests/chars_phys/cc_titus_assault`, `Tests/physics`, `Tests/pre_step3`, `Tests/sani_before`, `VfxUe_bak3`, `Characters/cc_chaplain/cc_chaplain_bak1.uasset`, `Content/ForgeLens/POE/Tests`.

Next bot: list the CSV, stat those folders, compare to 817 / 4.0 GB, then delete only approved `delete` rows with the editor free. Log `docs/handoff/CLEANUP_DELETED.tsv`.

---

## 7. Undocumented or easy to miss in the README

Present in code, missing or wrong in the long README body before this pass:

- All `ForgeLens.exe` bench switches in §2 (PoE2 lives on the **browser** exe, not `LibSaber.SM2Extractor`).
- `UseSpecMapPolicy`, `LevelLightUe`, `RestFitPolicy`, `TextureUsage` bit table.
- `Games/Poe2/*`, `Games/Bmw/BmwGlb.cs`, `BmwWindow.cs`, `tools/bmw/ue_bmw_import.py`.
- Plugin `SetBodiesKinematic` / `ListBodies`.
- `.sfx` → `.sfx.json` + `.niagara.json` (still not a Niagara system).
- GPU preview of `.sfx` (README used to say `.ps` and `.sfx` are not converted).
- Default UE skeleton root is `/Game/ForgeLens/SM2/…`, not `/Game/SM2/cc_titus_assault/…`.
- `nmb` op semantics in `docs/re/REA_DEEP.md` §6 are **not** what `nmb_reader.py` executes.
- BMW is not a key in `GameRoots`; the fallback still yields `/Game/ForgeLens/BMW/`.

---

## 8. Findings (left as code; not "safe one-liners")

- Applying c4/98/95 changes fire-clip selection for aux weapons. Needs the TSV re-verify (`nmb_verify.py`), not a blind patch.
- Default-on `shootTip` changes every firearm preview. The env flag exists so the old root attach stays until a visual check.
- `docs/re/REA_FINDINGS.md` said "do not apply yet" after the code had already applied those changes. Corrected in this pass.
- `docs/WUKONG_M0.md` still prints the public community AES key. That is intentional for humans. Do not copy it into a committed tool.
- Test counts in STATUS (433/43, 372, …) were **not** re-run here. Do not quote them as current.
- R99 coat 206→303, hub averages, orb RGB, and "editor-free" are **DOC/PC**, not re-measured here.

---

## 9. Rules the next bot must keep

- Korean **해요체** to the owner. Result first. Docs and commits in English. KST.
- Never launch or control SM2, PoE2, Wukong, or any game. Never start the UE editor while a game is running.
- Generated content only under `/Game/SM2_Work` and `/Game/ForgeLens/`. Never `/Game/Work/*` or the owner’s own assets. Editor lock `C:\Users\Revaptor_FX\ue_editor_busy.txt`.
- Push fast-forward only. No force-push. No `master` unless the owner says so.
- `BMW_AES_KEY` env only. No EngineWire token in the repo.
- Niagara only after an explicit go.
- Ghidra full analyze and REA against the retail exe: **owner PC**, not this box.
- Report final results. Answer "상황보고" immediately with what is running and what is done.
- PC: Revaptor-FX, user `Revaptor_FX`. Machine id changes per Cursor account — pick the machine **by name**. Work root `C:\Users\Revaptor_FX\Desktop\SM2_Work`. UE 5.7.4 `K:\Unreal Projects\UE_VFX_57`. Games: SM2 `D:\Steam\steamapps\common\Space Marine 2\client_pc`; PoE2 `D:\Daum Games\Path of Exile2`; Wukong `D:\Steam\steamapps\common\BlackMythWukong`.

---

## 10. Prompt for the next bot

```
You are taking over ForgeLens (Baegovda/ForgeLens, private) for owner Baegovda on Windows PC Revaptor-FX.

Read first, in order:
1. docs/HANDOFF_CURRENT.md (this reconciliation — it wins).
2. AGENTS.md and docs/RULES_CORE.md.
3. docs/re/REA_FINDINGS.md and docs/re/REA_DEEP.md (section 6 wins over section 4 on nmb ops).
4. docs/POE2_M2.md (open visual leftovers), docs/POE2_M3.md, docs/WUKONG_M0.md.

Git: branch cursor/sm2-pck-extractor-4420. Last runtime commit 3a6ba3f (R99 plugin notes + commissar coat A/B). At f82bb4b the branch was 390 commits ahead and 1 behind origin/master. Do not merge or push master. Confirm with git fetch && git log --oneline -8 && git rev-list --left-right --count origin/master...HEAD.

App version 0.97.0. PoE2 CLI flags are on ForgeLens.exe (Program.cs), not LibSaber.SM2Extractor.exe. UE roots: /Game/ForgeLens/<CODE>/ and legacy /Game/SM2_Work. GameRoots explicit keys: SM2, POE; BMW uses the fallback /Game/ForgeLens/BMW/.

Hard rules: never run any game; Korean 해요체 results-only to the owner; no engine-folder edits; no deletes without a referencer check (the ForgeLens old-asset CSV was owner-approved but the delete was interrupted — re-verify on disk before deleting); AES only via BMW_AES_KEY; Ghidra full analyze only on Revaptor-FX; never force-push; Niagara only if the owner explicitly says go.

Current code vs docs:
- UseSpecMap is PROVEN and implemented (UseSpecMapPolicy, FUN_14217da70, bit 21 MSCRGHAO / bit 18 MSCG). FIT K=10 is removed (LevelLightUe). Rest-fit defaults off. collisionThickness default 0.
- nmb ops c4/98/95 are DATA-PROVEN in REA_DEEP §6 but nmb_reader.py and PlayerFireClips.cs still treat them as undecoded. Muzzle shootTip is parsed from cls but SM2_MUZZLE_SHOOTTIP defaults off.
- Coat PIE (documented, not re-measured): penetrations 206→303 with thickness 0; do not silently restore heuristics. Next: collision capsules.
- Spot direction and UE intensity units are still open. IBL numbers and shadow bias need Ghidra/REA or a capture.
- PoE2 M0–M3 code is in tree; Frost Bomb / Glacial Bolt leftovers are still open text in POE2_M2.md. BellCrow SetBodiesKinematic exists; PIE after the R99 plugin build is unverified.
- Wukong M1 is a draft window + GLB helper. Pak reader is not in git.
- UE cleanup of old /Game/ForgeLens assets (~817 files / ~4 GB in chat only; not in the repo) is pending verification on the PC. Last in-repo snapshot (04:52 KST) claimed nothing was deleted. CSV: C:\Users\Revaptor_FX\sm2_re\ue_old_assets_candidates.csv.
- ForgeLens_latest publish commit is conflicting in old docs (1461bbf vs eddf91e). Check the exe on the PC. 3a6ba3f may be unpublished.

Work order: (1) PC Ghidra status, then REA for IBL, shadows, spot direction; (2) coat capsules without re-enabling rest-fit; (3) light unit bridge; (4) material re-export/reimport; (5) apply nmb ops and then default muzzle to shootTip.locator; (6) PoE2 P1–P4 and Wukong M1 only if the owner wants that track; (7) Niagara only on explicit approval; (8) finish the UE delete only after editor-free + referencer check, and log CLEANUP_DELETED.tsv.

PC paths: Desktop\SM2_Work, ls_push, ue_editor_busy.txt, K:\Unreal Projects\UE_VFX_57, sm2_re\ghidra_full. Push ff-only (push.ps1 or git push, never --force).
```

---

## 11. Pass log (5 passes, this session)

1. **Docs read.** Entry set: `AGENTS.md`, `README.md`, `HANDOFF.md` (head + tail), `docs/HANDOFF_NEW_BOT.md` (full), `NEXT_BOT_PROMPT.md`, `STATUS.md` (live + sampled ledger), `START_HERE.md`, `ROADMAP.md` (head + niagara/UseSpecMap), `UNVERIFIED.md`, `FIRE_CLIP.md`, `CLEANUP_PLAN.md`, `RULES_CORE.md`, `SUBAGENT_PREAMBLE.md`, `BOT_TRANSFER.md` (head), `WORKFLOW.md` (head), `POE2_M0`–`M3` headers and M2 open line, `WUKONG_M0.md`, `docs/re/REA_FINDINGS.md`, `docs/re/REA_DEEP.md` §4–§7, `SYMBOL_DB/INDEX.md` + materials header/row, `SHADERS.md` §5 (already cited `FUN_14217da70`). Git: only `cursor/sm2-pck-extractor-4420` and `master`; no 2026-11-01 side branch.
2. **Identity vs code.** Version `0.97.0` matches csproj. Ahead/behind 390/1 at `f82bb4b` (docs said 387 or tip `1461bbf` / `33a8cda` / `3a6ba3f` as if that were HEAD). PoE2 flags found in `Program.cs`, absent from `ExtractorOptions.cs`. `GameRoots` is SM2+POE plus fallback.
3. **REA vs code.** `UseSpecMapPolicy` and `LevelLightUe` match REA_FINDINGS (K=10 absent; intensity = I × colorScale). `RestFitPolicy.Enabled` is false; parser and plugin default thickness 0. `REA_FINDINGS.md` "do not apply yet" was false — those edits are in `ddc8f88` / `4be1d4e` / `e9ad274`. `nmb_reader.py` line 15 and `run_expr` still mark 95/98/c4 undecoded; `PlayerFireClips` 1.5/1.7 comments say "undecoded". `MuzzleAtShootTip` is env `== "1"`. `REA_DEEP` §4 vs §6 contradiction recorded; §6 kept.
4. **Other tracks vs code.** `SetBodiesKinematic` / `ListBodies` exist in `SM2PhysicsSetupLibrary.h`. `BmwGlb.cs`, `BmwWindow.cs`, `ue_bmw_import.py` exist; no `bmw_pak.py` in tree. `ClothSolver` has capsules (so "no capsule code" would be wrong; the open bug is coat penetration under UE thickness 0). `AssetExporter` writes `.sfx.json` and `.niagara.json` and states Niagara is not auto-built. `817` / `4.0GB` do not occur in the repo (grep). Cleanup plan file is SM2_Work-era, not this ForgeLens delete.
5. **Re-read this file against the tree, then a 49-check script** (version, every `Program.cs` flag, extractor has no `--poe2export`, `UseSpecMapPolicy` / `LevelLightUe` / `RestFitPolicy.Enabled == false` / parser thickness default 0, muzzle env, `PlayerFireClips` 1.5/1.7, `nmb_reader.py` line 15, BMW files present and `bmw_pak.py` absent, `SetBodiesKinematic`, `GameRoots` SM2+POE, `.niagara.json`, `GlPreview` `Decide(part.Source)`, this file’s 390 / `3a6ba3f` / 817 wording, REA_FINDINGS “applied”, README and AGENTS pointers, `817` absent from `CLEANUP_PLAN.md`). All 49 passed. No instruction remains to restore K=10 or to treat UseSpecMap as an unfound writer. PC-only rows stay labelled PC/DOC. Historical ledgers were not rewritten; pointers and the sentences a new agent would obey were corrected.
