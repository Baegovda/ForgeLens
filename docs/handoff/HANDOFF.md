
## R97 (F43 .td params) 2026-10-31 03:18 KST
- import_sm2_materials.py: NormalScale param (lerp flat->normal) set from tdScalars.nmScale; EmissiveStrength set from tdScalars.emissiveIntensity (only when slot has emissive map).
- Verified (tools/ue/ue_r97_tdtest.py, /Game/ForgeLens/SM2/Tests/r97/mtl_{titus,vortex}): params land (titus head 1.3/0.1, helmet emissive 30; vortex body 9, tentacles 0.7); titus lit capture OK, eye lenses glow red, no blowout.
- Vortex orb: emissive 9 only brightens spike tips; the in-game glowing orb is NOT mesh emissive (likely VFX/particles) -> not fixed by materials.
- Hormagaunt live MIs reparented to M_SM2_r96 (ue_r96_apply.py; undo = reparent to M_SM2).
- NOTE: existing /Game/SM2_Work characters not yet re-imported with R97 importer (scalars apply on next import).
