# NEXT_BOT_PROMPT  -  paste this to the next Grok Bot

You are taking over **ForgeLens** for owner **Baegovda** on PC **Revaptor-FX**. Usage on the previous account was about to run out; do not rely on prior chat  -  use the docs and git tip below.

## Read first (in order)
1. `docs/HANDOFF_NEW_BOT.md`  -  section **"2026-11-02 ~04:55 KST  -  usage-cutoff handoff"** wins over older sections.
2. `AGENTS.md`
3. `docs/handoff/STATUS.md`, `docs/handoff/START_HERE.md`, `docs/handoff/WORKFLOW.md`
4. REA: `docs/re/REA_FINDINGS.md`, `docs/re/REA_DEEP.md`

## Repo / branch / tip
- GitHub: **Baegovda/ForgeLens** (private)
- Branch: **`cursor/sm2-pck-extractor-4420`**
- Code tip before docs handoff: **`3a6ba3f`**. Docs tip (this file): **`33a8cda`** (`33a8cdaabcf0ee2be2d76be2c9cd6380f65d0ec7`). Confirm with `git fetch && git log --oneline -5`
- Push clone (clean): `C:\Users\Revaptor_FX\ls_push` on machineId **`d3fb99ad-25ca-45c1-81af-41e5ca09104a`**
- vs master: ~**387 ahead / 1 behind**  -  **do not** push/merge `master` without an explicit owner decision

## PC paths
- Work root: `C:\Users\Revaptor_FX\Desktop\SM2_Work\`
- UE project: `K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject` (UE 5.7.4)
- Content roots: only `/Game/SM2_Work` and `/Game/ForgeLens/<CODE>/` (SM2, POE, BMW, …)
- Editor lock: `C:\Users\Revaptor_FX\ue_editor_busy.txt` (idle = `editor-free …`)
- Push: `Desktop\SM2_Work\Scripts\mtl_fix\push.ps1` (ff-only; applies `%USERPROFILE%\0*.patch`) **or** direct `git push` (never `--force`; verify merge-base equals remote tip first) from `ls_push` if you committed there; one push at a time via named lock (`forgelens_push.lock` / `tools/re/box/push_lock.sh`). **Never force-push.**
- RE / Ghidra: `C:\Users\Revaptor_FX\sm2_re\` (project `ghidra_full`, log `ghidra_full.log`); JDK+Ghidra under `Desktop\SM2_Work\Tools\re_env\`
- UE delete candidates CSV: `C:\Users\Revaptor_FX\sm2_re\ue_old_assets_candidates.csv`

## Hard rules
- **Never run any game** (SM2, PoE2, Wukong, …). Owner takes in-game shots.
- Korean **해요체**, results-only replies to the owner.
- No engine-folder edits. No deletes without confirmation (CSV `delete` rows already owner-approved).
- AES / Wukong: only **`BMW_AES_KEY`** env; **never** commit `fl_bmw/tools/bmw/Program.cs` or `bmw_pak.py` with a key.
- **Ghidra only on the user PC** with max resources (target ~96GB heap, 32 CPU)  -  **never** on the bot box.
- Secrets: never print EngineWire token; no key literals in docs/commits.

## Current state (handoff-time)
- R99: SM2PhysicsSetup deployed via RunUAT BuildPlugin; KawaiiPhysics **disabled** in host uproject (`bak_r99_kawaii`) due to UBT RulesError.
- Commissar coat A/B: thickness 0 + restFit off → measured penetrations **206→303**; heuristics **not** re-enabled; capsule data suspected.
- REA PROVEN: UseSpecMap `FUN_14217da70` (bit21 MSCRGHAO / bit18 MSCG); lights `FUN_14224ecf0`/`FUN_14224f010`; tonemap `FUN_1423bfc00`; cloth defaults; nmb `c4`/`98`/`95`; muzzle from cls locator; SFX `FUN_1425fd580`.
- Open REA: IBL values, shadow bias/cascades, spot directions, intensity→UE units (INFERRED).
- Ghidra PID **102852** was left running; log had **Import failed (Negative delay)** / empty `sm2.gpr`  -  verify/restart on PC if needed; do not kill casually.
- UE old-asset cleanup **interrupted**: all listed delete folders **still exist**; KEEP list verified present (r92_floor, r54_level, r72_mats, chars_cloth2, vortex_orb, r90_cloth, chaplain folders, cc_titus_assault, physics, pre_step3, sani_before, VfxUe_bak3, cc_chaplain_bak1, POE/Tests).

## Prioritized next steps
1. Coat penetration via **collision capsules** (Chaos / cdt data)  -  do not silently restore thickness/restFit heuristics.
2. Resume **CSV-approved UE deletes** when `editor-free`; log `docs/handoff/CLEANUP_DELETED.tsv`.
3. Code: apply **nmb ops** + **muzzle locator** path; then full **material re-export/reimport**.
4. After healthy Ghidra analyze: close REA open items; re-cite Evidence IDs.
5. Owner-gated only: PoE2 P1-P4, Wukong M1, Niagara.

## How to work
- Prefer Shell with machineId on Revaptor-FX for git/UE/Ghidra.
- Take/release editor lock; use EngineWire when editor up, else headless Cmd.
- Commit docs/code carefully; push fast-forward only (no --force) with the push lock held.
- If anything in HANDOFF conflicts with older STATUS/ledger text, **HANDOFF usage-cutoff section wins**.
