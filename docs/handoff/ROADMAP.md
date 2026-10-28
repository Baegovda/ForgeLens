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
1. **[checked mtl1-19: keep TINT_EXACT/contrast opt-in; default grey matches in-game grey smoke]** **Smoke colour (follow-up):** the grenade smoke blue cast is fixed by `textureTintControl`. Check against an in-game capture. `additionalContrast` + `contrastColorKey` (hot-colour ramp, 2,969 non-zero uses) is not implemented: REA the GPU-side shader or approximate it, labelled as a heuristic.
2. **[checked mtl1-19: opt-in SM2_VFX_TRAIL_TC=1; in-game shots also show no visible trail]** **Bolter smoke trail:** faint by data (see HANDOFF vfx3 part 3); not a bug. Optional: apply `tcIn` scale on trails; confirm visually in-game.
3. **[mtl1-23: RenderDoc captures resolved blend PSOs (particles/trails/sparks/sfx meshes), distortion strength (0.08, ±0.02), tonemap + exposure (key ~0.11/avgLum) - see STATUS mtl1-23; still open: ratio*, blend_add vs blend in-game split, size radius, sfx[560].w td source]** [mtl1-20 retry: still PARTIAL] **REA of the guessed values:**
   - `ratioDependency` / `ratioXY`;
   - `blend_add` semantics (blendType parser 0x141751cd0 gives only the enum; find the blend-state setup);
   - exposure 1/250 and light ×100 scale;
   - size radius vs half-width on meshes/trails;
   - distortion strength.
4. **[REA'd mtl1-19: structure matches current model, see STATUS mtl1-19; time base of dynamic curve open]** **genOnFly** exact emission (currently a burst for explosion targets, per-parent rate for flow targets). Trace the `attached` runtime near the genOnFly string @0x143644b38.
5. **[mtl1-21: DEMO host preview added (plane/sphere, heuristic)] [mtl1-20: classified, not drawable standalone - covering painter writes the host surface coveringMask; needs a host character + covering_simulation]** **Flesh emitter:** `part_mag_ee_bolter_flesh` untyped entry. It is now known to be an `em_covering_*` surface painter (blood r0.4 on HEAD), shown as a note. Optional: draw a decal-like covering on the model.
6. **[mtl1-20: animStage source REA'd (spline over age) and implemented; rest of VAT was done in vfx3e-g]** **VAT blood splashes** (`splash_5_v2_anim_data*`, `shader_blood_splash`): the layout is still unknown, see the findings in HANDOFF vfx3 part 3. The pct is downsampled (2400×471, 8 bands). REA the vertex shader / `combineMtlAnimControl.animStage`.
7. **SELF_DISTORTION** render: **done** (mtl1-19; mtl1-23 uses the capture distortion formula 0.08·sat(10|off|)·off clamped ±0.02, quad-space use HEURISTIC). degNear on trails and mesh particles: **done** (trails TRAIL_DEGR; mesh particles mtl1-17).
8. Then report VFX done (with %).

## UE verification — results so far (details in STATUS.md / HANDOFF "UE verification")
- Constructors PASS (`/Game/SM2_Work/Tests/bulk_fix`). Titus + Calgar physics bake PASS (`/Game/SM2_Work/Tests/physics`), with 60 Hz import fixed.
- Follow-ups, in order:
  1. Calgar cape red/white stripe: REA the cloak material (cc_calgar cloak `.td`, layer blend?).
  2. Re-import Calgar clips at 60 Hz (`Anims60`, same as Titus).
  3. Spiky cloak hem: raise `maxBonesPerCloth` above 1024 (Calgar has 1133 free particles), or regularise the polar fit on near-collinear one-rings.
  4. Loop-seamless bakes (blend the tail into frame 0 after settle).
  5. Optional: assign `PA_*_sm2` physics assets; the manual AnimDynamics/Chaos Cloth recipe comes from `physics.json` → `ue`.

## Leads from the mtl1-23 captures
- Armory per-instance customization colours (LWI_INST_DATA_BUF, see HANDOFF mtl1-23): map slots +21..23 / +28..30 to the _cc mask channels (HEURISTIC today) and apply in UE/browser.
- Hellbrute: sections -> draws **done** (mtl1-24: 31/31 by index count, variants already correct, no exporter change). Open: dump DYNAMIC_GLT_BUFFER rgb and the chameleon draw CBs (eids 3419-3452, PS 22961) in the next capture; compare M_SM2 chameleon/aniso response under matched lighting.
- Chameleon **done** (mtl1-26). InstanceTint param + sidecar layers[] **done** (mtl1-27). Open: writer of OBJ_MTLBLEND_LAYER_CONST[15].x (flags 0 on 16/16 Hellbrute draws; needs a non-Hellbrute capture, UseSpecMap still HEURISTIC); importer shading of layers[] (dirt / dirt_no_spec AO layers, vertex-colour weights); Calgar cape stripe source (not a material layer); matched-lighting UE vs game comparison.
- Mod Tools (appid 3856330): click Install in Steam, then ilspycmd the managed DLLs for TD/material (chameleon, aniso, second spec, colorization, DYNAMIC_GLT), .sani, cloth, sfx. Then multi-layer material export (layers[].blending weights) and cloth coverage rule (rigid if SIM does not cover REND).
- Trails and sparks are not drawn by the browser GPU renderer yet; blend rules are ready in VfxBlendRules.

- mtl1-28: trails SrcAlpha/InvSrcAlpha + sparks opaque (spark classification HEURISTIC), distortion backdrop preview **done**. Cloth render-as-sim fallback **verified in UE** (mtl1-29: 2/2 cloths on mtl15 cc_sorcerer, simulating in SIE). mtl1-30: every render slot bound (own asset per slot), shared-CDT collision fallback (HEURISTIC) → 10 shapes/cloth. Next: confirm the shared-CDT rule in the exe runtime (not the serializers); avoid the duplicate sim for remapped render slots. mtl1-31: per-cloth collision PA (fixes the tech_priest legs-through-skirt; own-collider penetration 105-113 → 0) + shared CDT union (HEURISTIC). Thickness/self-collision/backstop A/B gave no gain beyond noise, so defaults are unchanged. Next: skin-based penetration metric; regression of per-cloth PA on sorcerer_exalted/calgar; regenerate the saved tech_priest ChaosCloth asset. mtl1-32: done — skin metric (`sampleskin`, HEURISTIC, misses wrong-side), per-cloth PA does not regress calgar/sorcerer_exalted, calgar mtl32 json adopted (skin 20-53 -> 2-16), saved tech_priest regenerated (_bak15). Next: regenerate saved calgar; wrong-side check; calgar missing shared-shape bone.

## Later / backlog
- In-game captures on the PC (when the user isn't playing) for: grenade/bolter VFX, Titus seals, Calgar cape, Calgar hair.
- WARN items: rubric/sorcerer 0-root-motion loco clips (REA whether those clips are in-place by design).
- Retarget: Cadian index finger curl offset.
- `ue_cap_lit.py`: wait for texture compilation before capturing.
- CPU renderer: blend modes and physics (low priority; GPU is the default).

## Possible future development (not on the roadmap)
- Niagara conversion (`.sfx` → UE Niagara). Removed from the roadmap by the user (2026-10-27). Only start if the user asks for it again.
- UE multi-game extraction (generic Unreal Engine `.pak`/IoStore extraction for other games, beyond SM2/Swarm). Added by the user (2026-10-28) as a possible future item only; not started.
