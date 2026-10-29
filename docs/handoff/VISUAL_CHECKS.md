# VISUAL_CHECKS — in-game vs export, for the owner (2026-10-30 KST)
Compare the same effect/clip in game and in the UE export (Forge Lens capture or UE_VFX_57). Write result + date in the last column.

| # | What | Look at in game | Look at in export | Evidence now | Result |
|---|------|-----------------|-------------------|--------------|--------|
| V1 | Ribbon trail fade + age colour (R36) | sword/thruster trail: ends fade in and out; colour changes from new (head) to old (tail) | same trail: both ends fade (`SM2_VFX_TRAIL_FADE` default on); colour follows age, not position; add-blend fades rgb, others alpha | code PROVEN; age span 0..1 INFERRED (U3), owner alpha=1 HEURISTIC (U2) | |
| V2 | Adaptive light clamp (R37) | muzzle/explosion light brightness in dark vs bright area | same light: brightness scales with scene avg lum, never beyond ×0.05..×50 | PROVEN; capture scenes unchanged (avgLum 4.15–4.82); bench 1/250 HEURISTIC (U6) | |
| V3 | Player fire clip per weapon (R38) | fire aimed and hip for: bolt pistol, stalker, melta shotgun, bolt rifle, heavy bolter | PropAnimStates picks `tacticus_shtN_shoot_aim/blind` per FIRE_CLIP.md table | mostly INFERRED; bolt-rifle aim + heavy aim HEURISTIC (U9) | |
