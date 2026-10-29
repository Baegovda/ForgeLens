# SM2 RenderDoc attempt (2026-10-28 ~09:55-10:07 KST)
- Mod loader (PyInstaller, SM2_Ultimate_Launcher.pyc) does NOT disable EAC:
  * LAUNCH MODDED/VANILLA -> webbrowser.open('steam://rungameid/2183900') (EAC path)
  * LAUNCH TEST SCENE -> Popen('<game>\Warhammer 40000 Space Marine 2.exe', '--startup','--set','/Config/WhiteListedVariables/enableModdersAutostartFlow','true')
    root exe = EOSBootstrapper; .ini ApplicationPath=start_protected_game.exe -> EAC still runs
  * "EAC Splash Screens" only swaps EasyAntiCheat\SplashScreen.png
- EAC-free: launching client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe directly (env SteamAppId=2183900, no file changes) reaches main menu/mission select (game_norenderdoc.png). No EAC module loaded.
- RenderDoc 1.46 portable (renderdoccmd capture) injected OK (renderdoc.dll loaded) but game crashed ~9s:
  EXCEPTION_ACCESS_VIOLATION write @ dxgi.dll+0x11725 -> target renderdoc.dll+0x1507290 (.rdata, i.e. a wrapper vtable).
  Stack heavy with EOSOVH-Win64-Shipping.dll (Epic overlay hooking DXGI) -> heuristic: EOS overlay vtable-patching RenderDoc's wrapped swapchain.

## 2026-10-28 11:20 KST — 12-capture triage
Picks: smoke=frame88302, hellbrute=frame68141, plasma/distortion=frame101917, armory=frame128197, outdoor=frame137266 (thumbs in /workspace/rdc/thumbs, montage.jpg).
RenderDoc 1.46 replay hits DXGI_ERROR_DEVICE_REMOVED in every capture at an early compute pass (right after a DiscardResource + Dispatch following CB_PASS_CAPSULE_SHADOWS / CB_PASS_LIGHTBUFFER):
 hellbrute eid 1197/1211, smoke 877, plasma 2089, armory ~3248, outdoor 19204. Deterministic; it also happens with only one replay running and with D3D12.BindlessFeedback off -> not OOM.
Only depth/normal prepass, shadows, wind sim, and light-tiling run before the fault; no blended draw and no particle, translucent, or post pass gets reached.
Per-capture outputs: <cat>/draws.jsonl|json, cbuffers.jsonl|json (named CB vars via DXIL reflection), dis/, thumb.jpg.
CB_COMMON HDR (evidence): EXPOSURE_DIV_AVG_LUM hellbrute .030691 smoke .023783 plasma .027324 armory .225831 outdoor .03224;
 AVG_LUM 3.661/4.819/4.150/0.1086/2.287; BLOOM_POINT 8/8/8/0.24/1.82; WHITE_POINT_SCALE_UNCHARTED 1.375 (outdoor 1.3436), REINHARD .01 (outdoor .00063); GAMMA (1,1,0).
 exposure_div_avg_lum*avg_lum (key) = hellbrute .1124 smoke .1146 plasma .1134 outdoor .0737 armory .0245 (mtl1-40 correction: NOT constant; earlier "0.112-0.117 in every scene" was wrong).
Material: CB_STATIC_GLT/CB_DYNAMIC_GLT hold bindless texture indices (tintMask, GLT_TEX_CUSTOM_*), TINTBYMASK=1 -> per-instance colour lives in bindless GPU buffers (GPU_INST_DATA_BUFFER/INST_RES_BUFFER), not in CBs (heuristic).

## 12:10 KST — RenderDoc nightly v1.47 (aac221a8, 2026-10-26) FIXES replay
- Downloaded official renderdoc.org/autobuilds zip -> C:\Users\Revaptor_FX\Tools\RD_nightly\ (Authenticode Valid, Baldur Scott Karlsson). 1.40 not needed.
- No DXGI_DEVICE_REMOVED; full frames replay. Outputs: PC rdc\an_<cat>_n\, box /workspace/rdc/<cat>/nightly/.
- smoke/hellbrute/plasma/armory done; outdoor + deep_hellbrute/deep_plasma/deep_armory queued (seq_n.ps1 / seq_deep.ps1, sequential, Idle).
### Blend states (measured)
- CPU smoke/explosion particles (CB_PARTICLES, PS 101818): One/InvSrcAlpha premultiplied (alpha One/InvSrcAlpha) -> NOT blend_add.
- Sparks particles (eid 2086, 228 inst): blending disabled (opaque/alpha-test, writes VELOCITY).
- Trails (CB_PASS_TRAIL): SrcAlpha/InvSrcAlpha, alpha Zero/One. PS_REG_TRAIL_EMISSIVE [5,4,1,0], TRAIL_COLOR_FADEOFF [3.85,7.34,-1.69,1].
- Plasma SFX meshes (CB_PASS_SCREEN_EFFECT_STATIC/CB_DYNAMIC_SFX): One/One additive, alpha Zero/One -> blend_add correct here.
- Emissive SFX intensity *= lerp(1, COMMON_HDR_AVERAGE_LUMINANCE_CLAMPED, sfx[560].w) (disasm s33060) -> exposure compensation.
- Bloom composite: SrcAlpha/One into __AHDR_BLOOM_*; fog apply One/Src1Col (dual source).
### Distortion (measured)
- Particles write DUDV (R10G10B10A2) premultiplied One/InvSrcAlpha; enc = mask*(0.5+2*offset), mask=sat(10*|offset|)*depthfade; offset = nm.xy * per-particle strength (TEXCOORD0.w from emitter data).
- Mesh distortion CB_PASS_DISTORTION DISTORT_PARAMS [1.0, 0.1, 0, 0].
- Post: CB_PASS_DISTORTION_POSTPROC DISTORT_POSTPROC_PARAMS [1,1,1/2560,1/1440] (same in all captures).
### Tonemap (CB_PASS_HDR, all captures)
- curve params [0.12,0.05,0.45,0.2],[0.02,0.08]; EXPOSURE = EXPOSURE_DIV_AVG_LUM; whitepoint 1.375385; LUT 16^3, LUT_RANGE_LOG 14, GREY 0.43; bloom gamma 0.3 (downsample), amount 1; vignette off.
### Hellbrute (68141)
- ch_hellbrute_* draws 2870..3027 (GBUFFER). OBJ_DATA[0].w = -1 and GLT_TEX_CUSTOM_* invalid -> NO customization/tint; colour = albedo texture ch_hellbrute_armor_02 (COLORIZATION 0.502 neutral, TINTBYMASK white).
### Armory (128197)
- Marine armour (ch_soldier_arms_02, eid 10623) uses _cc mask + CUSTOM_TEX_ARR etc., OBJ_DATA[0].w=7024 (customization index, heuristic).

## 12:35 KST — deep results (nightly 1.47)
- Outdoor (outdoor/nightly): HDR exposure .03224, whitepoint 1.343645, LUT weight 1.0, curve same as others; DISTORT_POSTPROC [1,1,1/2560,1/1440]. Blends: 70x One/One alpha Zero/One (additive SFX) to COLOR_FP16, 37 SrcA/InvSrcA, 7 DUDV writers, 2 premult One/InvSrcA.
- Hellbrute 68141: armour plates GBUFFER_0 albedo median sRGB (2,97,127) = #02617F (hue ~194, dark cerulean/teal); final frame blue median (46,96,130). Colour is in ch_hellbrute_armor_02 albedo texture itself (blue panels), no tint (OBJ_DATA.w=-1). Verdict: neither navy (#000080, hue 240) nor bright cyan; hue nearer cyan but value ~0.5.
- Distortion post (5160 PS disasm): uv += (DUDV.xy*0.04 - 0.02) i.e. 0.02*(2*dudv-1), hardcoded; gated by DUDV.w test at offset location. Particle PS (4996): dudv.xy = mask*(0.5+2*off), off = nm.xy*TEXCOORD0.w*fade -> uv shift = 0.08*off. Measured frame: |dudv-0.5| mean ~0.022 (~0.0009 uv, ~2px@2560), max L1 0.189 (~0.0075 uv, ~19px); only 5376 px nonneutral.
- Armory 10623: customization colours come from LWI_INST_DATA_BUF (=GPU_INST_DATA_BUFFER) at base = INST_DATA_OFFSETS[3].w*inst + OBJ_DATA[0].w(7024) + LWI_PARAMS.w(110592). Record: +21..23 = (0,.095,.145),(.151,.244,.622),(.229,.264,.276); +28..30 = (.008,.337,.412),(.263,.329,.482),(.51,.545,.557); +12/13 (1,0,1,0) +14 (0,.4,0,.4) (slot roles = heuristic). GLT_CUSTOMIZATION_LAYERS_ENABLED 0, TILING 2. Marine GBUFFER albedo median (21,68,80).

## Hellbrute chameleon dump (2026-10-28, ~13:40 KST)
See hellbrute/cham/FORMULA.md, glt_records.json, lit_vs_gbuffer.json. View-angle ramp black->cyan->teal->mint on N.V^0.7, multiplied into sRGB albedo; roughness 1; 2nd aniso specular lobe tinted by raw texture (F0 ~0.94*tex in forward pass), shifted tangent +0.4N. Per-instance GLT tint = white.
