# Adaptive light multiplier (REA item 2) — 2026-10-29 KST, PC REA daemon
## Answer
FUN_14224ecf0(out, light, adaptScale, shadowIdx): P1.rgb *= adaptScale when light+0x6d bit0 (isAdaptiveIntensity).
- PROVEN: adaptScale = FUN_141e90cb0(&DAT_144f9cee0, viewport) = clamp(*(float*)(DAT_144f9cee0 + vp*0x198 + 0x18c), 0.05, 50).
  0x144f9cee0+0x18c = 0x144f9d06c = per-viewport avgLum (same DAT that FUN_141e90d00 writes to CB_COMMON +0x160 AVERAGE_LUMINANCE_CLAMPED / +0x164 avgLum).
  => adaptive multiplier == COMMON_HDR_AVERAGE_LUMINANCE_CLAMPED (scene auto-exposure avg luminance, unitless HDR, range [0.05, 50]).
- Callers of the packer (xrefs 0x14224ecf0):
  - FUN_141ee9e80 (0x141eea048/331/517): per-viewport light CB fill (DAT_144faeea0), 3 loops: 16 first-block lights, the main list (shadowIdx from light), extra lights at lVar6+0x841a0 (stride 0x80, shadow -1). ALL pass clamp(avgLum). PROVEN.
  - FUN_141de8140 (0x141de826c): "LIGHTS_BUFFER" structured buffer (0x50 stride) passes adaptScale = 1.0 (0x3f800000) — no adaptation on that path. PROVEN (what consumes LIGHTS_BUFFER not traced; likely non-viewport/baked use, INFERRED).
- Applies to every light type (point/spot/directional) that has bit0 set; the bit is the only gate. PROVEN (packer). Non-adaptive lights get factor 1.
- Other FUN_141e90cb0 users: 0x14184cd7f, 0x14184d3fd (trail upload area), 0x14189f70a, 0x141906bee, 0x141ef822c — not inspected (same clamped avgLum fed to trail/particle code; lead).
- Still HEURISTIC: CaptureLightUnits.DefaultAdaptive = true for lights lacking the key (packer only reads the bit; default comes from desc deserializer, not traced this round).
## Repo impact (not edited)
- CaptureRules.cs CaptureLightUnits.Capture(): `adaptive ? averageLuminance : 1` -> should use clamp(avgLum, 0.05, 50). Relative() (capture/avgLum) then = adaptive ? clamp(avgLum)/avgLum : 1/avgLum.
- VfxUeExport.cs l.129 lightUnits text + l.232 bench ground lighting via l.Relative(avgLum); VfxFrameExport.cs l.143 captureHdr: pick up the clamp automatically once Capture()/Relative() change. Mark adaptive factor PROVEN (was HEURISTIC, "one light").
- Effect for capture scenes: none (avgLum 4.15/4.82/... inside [0.05,50]). Only matters with SM2_VFX_SCENE=bench / SM2_VFX_EXPOSURE override where avgLum=(1/250)/exposure may leave the range. Bench exposure 1/250 itself stays HEURISTIC (not addressed by this trace).
- Docs: RE_NOTES "R+0x6d bit0" + packer entry -> add adaptScale source; SYMBOL_DB: FUN_141e90cb0 = GetAvgLumClamped(vp), FUN_141ee9e80 = per-viewport light CB fill, FUN_141de8140 = LIGHTS_BUFFER build.
