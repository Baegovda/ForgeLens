# ROADMAP — approved work order and next steps

## Work order the user approved (2026-10-26 22:07 KST), with status
1. GPU browser renderer (v3/v4): **done**
2. Animation finishing (cinematic rebase, anim-only/multi-clip export, additive): **done**
3. Mannequin → SM2 retarget prototype (Cadian, then Titus): **done** (finger curl open)
4. Material recheck (Hormagaunt/Titus): **done**, no regression
5. Bulk validation (37 assets): **done**, which led to:
   - 5a. Constructor fix (`--variant`, `--tex-formats`): **done + UE verified**
   - 5b. UE folder cleanup (approved deletions only): **done**
6. Secondary physics (user request: purity seals and cloth should move as in-game in the browser and carry over to Unreal): **done + UE verified**
7. Browser VFX (the user said go at 02:32 KST, "일단 냅두고 그담거해봐"): **in progress, ~85 %**
8. In-game side-by-side comparison (VFX, physics, materials): **todo**. It needs the PC free and in-game captures.
9. Unreal export test without Niagara (meshes/textures/materials of the compared effects → `/Game/SM2_Work/VfxExport`): **done** (2026-10-27, vfx4-2).
10. Browser viewer polish (asset search, preview, export button): **done (vfx4-3)**.

## Next: VFX3 remaining (do in this order unless the user says otherwise)
Pushed so far: sockets/bone attach, anim-event cues and stop cues, distortion, degNear on sprites, missing-texture skip, bolter proxies, in-place fix, coverings, textureTintControl (grenade smoke). The remaining items:
1. **Smoke colour (follow-up):** the grenade smoke blue cast is fixed by `textureTintControl`. Check against an in-game capture. `additionalContrast` + `contrastColorKey` (hot-colour ramp, 2,969 non-zero uses) is not implemented: REA the GPU-side shader or approximate it, labelled as a heuristic.
2. **Bolter smoke trail:** faint by data (see HANDOFF vfx3 part 3); not a bug. Optional: apply `tcIn` scale on trails; confirm visually in-game.
3. **REA of the guessed values:**
   - `ratioDependency` / `ratioXY`;
   - `blend_add` semantics (blendType parser 0x141751cd0 gives only the enum; find the blend-state setup);
   - exposure 1/250 and light ×100 scale;
   - size radius vs half-width on meshes/trails;
   - distortion strength.
4. **genOnFly** exact emission (currently a burst for explosion targets, per-parent rate for flow targets). Trace the `attached` runtime near the genOnFly string @0x143644b38.
5. **Flesh emitter:** `part_mag_ee_bolter_flesh` untyped entry. It is now known to be an `em_covering_*` surface painter (blood r0.4 on HEAD), shown as a note. Optional: draw a decal-like covering on the model.
6. **VAT blood splashes** (`splash_5_v2_anim_data*`, `shader_blood_splash`): the layout is still unknown, see the findings in HANDOFF vfx3 part 3. The pct is downsampled (2400×471, 8 bands). REA the vertex shader / `combineMtlAnimControl.animStage`.
7. **SELF_DISTORTION** render (only parsed now); degNear on trails and mesh particles.
8. Then report VFX done (with %).

## UE verification — results so far (details in STATUS.md / HANDOFF "UE verification")
- Constructors PASS (`/Game/SM2_Work/Tests/bulk_fix`). Titus + Calgar physics bake PASS (`/Game/SM2_Work/Tests/physics`), with 60 Hz import fixed.
- Follow-ups, in order:
  1. Calgar cape red/white stripe: REA the cloak material (cc_calgar cloak `.td`, layer blend?).
  2. Re-import Calgar clips at 60 Hz (`Anims60`, same as Titus).
  3. Spiky cloak hem: raise `maxBonesPerCloth` above 1024 (Calgar has 1133 free particles), or regularise the polar fit on near-collinear one-rings.
  4. Loop-seamless bakes (blend the tail into frame 0 after settle).
  5. Optional: assign `PA_*_sm2` physics assets; the manual AnimDynamics/Chaos Cloth recipe comes from `physics.json` → `ue`.

## Later / backlog
- In-game captures on the PC (when the user isn't playing) for: grenade/bolter VFX, Titus seals, Calgar cape, Calgar hair.
- WARN items: rubric/sorcerer 0-root-motion loco clips (REA whether those clips are in-place by design).
- Retarget: Cadian index finger curl offset.
- `ue_cap_lit.py`: wait for texture compilation before capturing.
- CPU renderer: blend modes and physics (low priority; GPU is the default).

## Possible future development (not on the roadmap)
- Niagara conversion (`.sfx` → UE Niagara). Removed from the roadmap by the user (2026-10-27). Only start if the user asks for it again.
