# SM2 cascade shadow + Morpheme names → ForgeLens (t105, 2026-11-06 Asia/Seoul)

No game launch, no new RenderDoc capture, no L951 static writer search. Counters unchanged: OPEN13 / R60 / NU27 / PROVEN433 / COMPLETE9/12.

## Code
- `src/LibSaber.SM2.Assets/Effects/Sm2CascadeShadow.cs` — PROVEN consumer math: Qj=dot((P,1),Rj); NUM_SPLITS@768 x=search count, y=primary slot; reg=owner*24+(slot<<2)+52; primary xyz no /w; loop xyz/w; P=TEXCOORD13 (obj*OBJ_WORLD, before -VIEW_POSITION).
- `src/LibSaber.SM2.Assets/Effects/Sm2MorphemeAssetTypes.cs` — names 1/2/6/10/11 only.
- Build: `dotnet build src/LibSaber.SM2.Assets` 0 errors. Not wired to a render pass; **no on-screen validation**.

## Numeric check (offline, existing dump)
Measured v0 P=(31.8365,6.7862,23.5787): primary Q=(0.48946,0.72912,0.35033,1.0) — equals the t98 shadowQ **prediction** (PS output not contrasted → still PREDICTION_only).

## New derivation from unused bytes (t105) — [Confirmed numerically, not CPU-writer proof]
Script `D:\sm2_re_rd_version_tests\scratch\gpu_cascade_cb_t98\cascade_light_basis_t105.py` → `cascade_light_basis_t105.json`.
- Slots 52/56/60 share one orthonormal 3x3 basis (max |dot| ~7e-8); row2 ∥ row0×row1 → orthographic directional light, axis (-0.69468,0.57358,0.43408), sign OPEN.
- R3=(0,0,0,1) everywhere → w=1; loop divide is identity in this capture.
- World units per shadow unit: slot52 88.94 (uv) / 88.50 (depth); slot56 266.81 / 269.57 (uv exactly 3.000x); slot60 130.63 / 171.01 / 177.60 (non-square; primary).
- Interpretation [Proposal]: Rj = Bias(scale,offset) · Ortho(extent) · LightView(basis); order and Bias split not separable from the product alone.

## Still OPEN — required artifact (no search invented)
- L951 CPU writer + light/view/Bias composite: need runtime CPU-side values (light view matrix, ortho extents, bias) from an approved runtime source for the same frame, or a second frame's CB with known camera move to separate view-dependent translation. Static search exhausted — do not re-run.
- shadowQ: need PS 21153 SampleCmp input/output debug value for v0 (approved capture) to promote from prediction.
- Morpheme L64/L351 fields: need Morpheme 7 headers or a reference rig dump.
- Unreal: [Proposal] map axis to a DirectionalLight rotation + CSM distances from extents; not implemented, not validated.
