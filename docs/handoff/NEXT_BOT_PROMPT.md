# NEXT_BOT_PROMPT  -  paste this to the next Grok Bot

The full prompt is **`docs/HANDOFF_CURRENT.md` §10**. This file is the short copy.

You are taking over **ForgeLens** for owner **Baegovda** on PC **Revaptor-FX**. Do not rely on prior chat.

## Read first (in order)
1. `docs/HANDOFF_CURRENT.md` (wins).
2. `AGENTS.md`, `docs/RULES_CORE.md`
3. `docs/re/REA_FINDINGS.md`, `docs/re/REA_DEEP.md` (**§6 wins over §4** on nmb ops)
4. `docs/POE2_M2.md`, `docs/POE2_M3.md`, `docs/WUKONG_M0.md`

## Repo / branch / tip
- GitHub: **Baegovda/ForgeLens** (private)
- Branch: **`cursor/sm2-pck-extractor-4420`**
- Last **runtime** commit: **`3a6ba3f`**. At `f82bb4b` the branch was **390 ahead / 1 behind** `origin/master`. Re-measure with `git fetch && git rev-list --left-right --count origin/master...HEAD`.
- Push clone: `C:\Users\Revaptor_FX\ls_push`. Pick machine **Revaptor-FX** by name (id `d3fb99ad-25ca-45c1-81af-41e5ca09104a` was the previous account).
- Do **not** push/merge `master` without an explicit owner decision.

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

## Current state
- UseSpecMap **in code** (`UseSpecMapPolicy`, `FUN_14217da70`). K=10 **removed** (`LevelLightUe`). Rest-fit **off**. `collisionThickness` default **0**.
- nmb `c4`/`98`/`95` meanings are in `REA_DEEP.md` §6 only. `nmb_reader.py` and `PlayerFireClips.cs` still treat them as undecoded. `SM2_MUZZLE_SHOOTTIP` defaults **off** even though cls `shootTip.locator` is parsed.
- Coat A/B (DOC, not re-measured here): penetrations 206→303 with thickness 0; do not restore heuristics. Next: capsules.
- Open REA: IBL numbers, shadow bias/cascades, spot directions, UE intensity units (INFERRED).
- Ghidra: last note was PID **102852**, log **Import failed (Negative delay)**, empty `sm2.gpr`. **Pending PC check.** Do not kill casually.
- UE old-asset cleanup: **pending PC verification.** Chat figure ~817 files / ~4.0 GB is **not in the repo**. The 04:52 KST snapshot claimed nothing was deleted. CSV `C:\Users\Revaptor_FX\sm2_re\ue_old_assets_candidates.csv`.
- `ForgeLens_latest` publish commit is **unconfirmed** (docs said both `1461bbf` and `eddf91e`).
- R99 notes: KawaiiPhysics disabled in the host uproject (`bak_r99_kawaii`); SM2PhysicsSetup via RunUAT. **PC, not re-checked.**

## Prioritized next steps
1. PC: Ghidra full analyze (confirm the failed import), then REA for IBL, shadow bias/cascades, spot direction. Re-cite Evidence IDs.
2. Coat penetration via **collision capsules** — do not silently restore thickness/restFit.
3. Finish the UE intensity unit bridge (no K=10). Spot direction is part of step 1.
4. Apply **nmb ops** in `nmb_reader.py` / `PlayerFireClips`, then default muzzle attach to `shootTip.locator` (`SM2_MUZZLE_SHOOTTIP` is opt-in today).
5. Full **material re-export/reimport** after UseSpecMap.
6. UE deletes only after a live disk check (`editor-free`, referencers, CSV). Log `docs/handoff/CLEANUP_DELETED.tsv`.
7. Owner-gated: PoE2 P1–P4, Wukong M1, Niagara.

## How to work
- Prefer Shell with machineId on Revaptor-FX for git/UE/Ghidra.
- Take/release editor lock; use EngineWire when editor up, else headless Cmd.
- Commit docs/code carefully; push fast-forward only (no --force) with the push lock held.
- If older STATUS / HANDOFF_NEW_BOT / ledger text conflicts, **`docs/HANDOFF_CURRENT.md` wins**.
