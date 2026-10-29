# heuristics4 progress (2026-10-29 KST) — REA-first pass on 4 HEURISTICs
Repo: /workspace/LibSaber-ui45 (branch work -> origin/cursor/sm2-pck-extractor-4420, base 28489a0). REA daemon pid 653800 (left running).
## Items
1. exposure — **PROVEN (RE done, code pending)**. CB_DYNAMIC_SFX writer FUN_1424540e0 (asm 0x14245470b-0x142454759):
   SFX_EMISSIVE_PARAMS(+0x230) = (desc+0x24 intensity [instance override], 0, 0, desc+0x28 adaptiveIntensity ? 1.0 : 0.0);
   +0x12c SFX_BLOOM_INTENSITY = desc+0x2c; +0x130 SFX_BLOOM_TINT = desc+0x14 (same desc as loader FUN_1425fd580).
   CB_COMMON fill FUN_141e90d00: +0x160 COMMON_HDR_AVERAGE_LUMINANCE_CLAMPED = clamp(avgLum[vp], 0.05, 50), +0x164 = avgLum, +0x150 = key/avgLum (DAT_144f9d068/DAT_144f9d06c, stride 0x198).
   Corroboration: FUN_14186c1d0 (light-ish parser) multiplies intensity by avgLum when adaptiveIntensity.
   => k = adaptiveIntensity (default false via sfx_sdr_preset_default.sd). Default k=1 was wrong.
2. light falloff attenCurveParam — pending
3. particle 0x400 vertex alpha — pending
4. UseSpecMap (OBJ_MTLBLEND_LAYER_CONST[15].x writer) — pending

## Final (2026-10-29 09:41 KST)
- Item 1 PROVEN, code done: per-material k = emissive.adaptiveIntensity (default 0; SM2_VFX_SFX_LUM_K forces). Tests 279/279 pass.
- Item 2: shader formula PROVEN; attenCurveParam consumer not found -> HEURISTIC. (FUN_1423ba150 hit = CB_STATIC_GLT packer, false positive.)
- Item 3: 0x400 in GPU-bound emitter record (FUN_1417e1e50 rec+0x18); no CPU consumer (other 0x400 tests = zlib) -> HEURISTIC emulation.
- Item 4: UseSpecMap writer (CB_OBJ_MATERIAL_DATA byte 336) not found -> HEURISTIC.
- Pushed origin/cursor/sm2-pck-extractor-4420 28489a0..8bdd80a via PC push.ps1.
- REA Ghidra provider was OOM-killed at ~09:37 KST (box 15 GB, concurrent dotnet publish in /workspace/LibSaber-mtl51); needs open_binary (~22 min) before next use.
