# SM2 precompiled shader bundles (.sdc) — static extraction (2026-10-28 KST)

## Where
- `default.pak` → `shader_cache/prebuild/pc/*.sdc`: 80 files, 761,415,285 B (largest `pc_dx12_glt.sdc` 292 MB).
- `resources.pak` → `shader_cache/prebuild/shader_cache.resource` (set → pc_dx12), `pc_dx12.shader_cache.resource`
  (family → pc\*.sdc map), `shaders/shaders_code.resource` (source file list code\*.fx/psh/vsh/csh; no source text).
- PC: `SM2_Work\Raw\sdc_all\shader_cache\prebuild\pc\` (extractor `Bin\SM2Extractor_mtl31`, `extract -f shader_cache`).
- Box: `/workspace/shaders/raw/sdc/` (80 .sdc + 3 .resource).

## Format (PROVEN: loader FUN_1420b4ce0 / FUN_1420b66c0 / FUN_1420b27b0, parse == file size on all 80)
- Header u32 groupCount, entryCount, dataSize, version(=1).
- groupCount × { u32 zSize, u32 n, n × { u32 key, u32 size, u32 strLen, char[strLen] } }.
- Then dataSize bytes = groupCount back-to-back zlib streams; stream g inflates to the concatenation of its entries.
- key = crc32(name). Names from the exe: `sdc_version` 0x1fd371a2 (=15), `filenamespreload` 0xb8ca1e98,
  `binarydictionarysize` 0x529909c8, `shaderbinary%06d` (DXBC/DXIL container per binary), `macro` 0xae8fc643
  (define-name list), `src` 0x6044248d (include list), `psos` 0x37a5fe67 (prebuilt PSO descs, 5 files).
  Unnamed: 0x97eb2089 pdb names, 0xeb2b14e1 8-byte per-binary recs, 0x1fff58e7 md5s, 0xec6393d0 2nd record table.
- filenamespreload record = {u32 len, bytes[len] = [stage(1 VS,2 PS,4 CS), 1-based macro indices...], i32 binaryIdx}
  → the define set of every permutation (validated: capture CS s1996 = hdr #32 HDR_GLUE_FLARES_APPLY_COLOR_GRADING+HDR_TONEMAP_LUT).
- DXIL HASH part (16 B) is unique per binary (48,828 distinct) and equals RenderDoc's "shader hash" → capture ↔ sdc map.
- psos record (213 B, after 36-byte header with count @32): i32 VS idx, i32 PS idx (same family), …, float 1.0,
  3 bytes, 8 RT blocks × 8 B {6 flag bytes, colourPreset, alphaPreset}, tail (formats?). Preset enum (shared colour/alpha,
  calibrated on 16 capture draws): 0 Zero/One, 1 Zero/Zero, 2 One/Zero, 3 SrcAlpha/InvSrcAlpha, 5 One/One,
  6 and 7 One/InvSrcAlpha, 18 One/One Max. RT flag byte5=1 ⇒ RT copies RT0 blend.

## Counts
80 families, 48,828 binaries (glt 9,060 / 30,388 preload perms; sm_mask 9,120; zfill 7,002; base 3,694; item_highlight 3,226;
ui 2,832; mtl_fill 2,125; sfx 1,943; trail 949; particles_gpu 832; particles 541; hdr 109; combine_material 50; distort 24;
deferred_lighting 10; distort_postproc 5; lut 2 ...). All disassembled (dxc -dumpbin, 0 failures); GLSL (dxil-spirv) for
hdr lut distort distort_postproc particles particles_gpu sfx trail combine_material deferred_lighting lightbuffer fog screen_effect glt (14,546, 0 failures).

## Index / outputs (`/workspace/shaders/out/`)
- `bin/<family>/<idx:06d>.<vs|ps|cs>.dxbc`, `dis/<family>/*.txt` (4.8 GB), `glsl/<family>/*.glsl`.
- `index/<family>.jsonl`: idx, stage, size, old vfxsh name (NNNNN_k), pdb, nperm, macros_common/any, perms.
- `index/families.json`, `index/hash.tsv` (hash → family/idx/macros), `index/capture_map.tsv` (440/472 capture shaders
  matched; the 32 misses are s61151..s61175 in every capture = runtime/driver shaders),
  `index/psos_<family>.jsonl`, `index/glt_mtlblend_flags.json`.
- Tools `tools/`: sdc.py (parser), sdc_index.py, hash_map.py, dis_all.sh, glsl_all.sh, psos.py, mtlblend_flags.py.
  Usage: `python3 tools/sdc_index.py` → bin+index; `tools/dis_all.sh`; `tools/glsl_all.sh <fam...>`; look up a capture
  shader: `grep <hash> out/index/hash.tsv`; find perms: `jq -c 'select(.macros_common|index("X"))' out/index/<fam>.jsonl`.

## Findings
1. Final tonemap pass (capture PS 1209/1159, CB_PASS_HDR, reads FSR_PRE_POST_PROCESS + HDR_BLOOM + TONEMAP_LUT,
   writes FRAME_BUF_00 RGBA8, blend off) = hdr PS 44 (HDR_GLUE_FLARES + _APPLY_TONEMAP_LUT) or 47 (+_APPLY_DITHER);
   3-texture signature matches exactly (45/48 add HDR_TEX4 bloom texture). Formula (PROVEN from DXIL):
     c = EXPOSURE · (min(scene,64512) + min(bloom,64512))
     vignette if intensity ≥ 1e-6: r = |uv.zw|, R = innerRadius·vtx; if r>R: f = 1/((r−R)²+1); c *= intensity·(f²−1)+1
     bias = 0.18·2^(−GREY·RANGE)        (GREY = HDR_LUT_GREY_POINT 0.43, RANGE = HDR_LUT_RANGE_LOG 14)
     uvw = sat(GREY + log2((c + bias)/0.18)/RANGE)·(31/32) + 1/64 ;  out.rgb = LUT3D(uvw) (linear sampler s4), a = 0
     dither (PS 47): n = fract(sin(uv.y·543.31 + uv.x)·493013)·2−1 ; out += (sign(n) − sign(n)·sqrt(1−|n|))/255
   CB_PASS_HDR registers: [0] vignette flags,intensity,invWH; [1].x innerRadius; [4].z EXPOSURE, .w WHITEPOINT; [8].z RANGE, .w GREY.
2. SFX blend (PROVEN from psos): SFX_BLEND_ADD_NO_ALPHA perms ⇒ preset 5 One/One (alpha Zero/One keep);
   SFX_ALPHA_BLEND ⇒ 3 SrcAlpha/InvSrcAlpha; neither ⇒ 3 with alpha Zero/Zero. RT1 SURFBUF_HEIGHT copies RT0;
   RT2 TRANSPARENT_BLOOM_MASK One/One, alpha One/Zero.
3. Particles: all particle PSOs are premultiplied One/InvSrcAlpha (preset 7, alpha 6/0) on COLOR_FP16_0, Max on the
   mask RTs; blend mode is in-shader (PS 281, shared[3].x flags):
     flag 0x20 (blendType==add): out.a = 0 and fog inscatter term dropped ⇒ pure additive through One/InvSrcAlpha.
     flag 0x400 (set unless isBlendAlphaMultiply && !add): colour multiplier = sat(alpha+1) = 1 (not premultiplied) and
       alpha-erosion threshold disabled; cleared ⇒ colour ×= alpha. blend_add (2) gets no bit ⇒ same as blend. (PROVEN shader side.)
     flag 0x200 flips the soft-depth fade sign; 0x8 gates the lighting branch.
4. Particle colour (PS 281; shared = PART_EMITTERS_SHARED_DATA[emitter*6 + k]):
     tex = sample (optionally remapped through PART_CPU_EMITTERS_COLOR_GRADIENTS row shared[4].w when ≥0, u = lum(tex)·tex.a)
     boost = 1 + sat(1 − |shared[1].rgb/255 − tex.rgb|) · shared[1].w     (contrastColorKey / additionalContrast)
     t = shared[0].z (textureTintControl):  rgb = mix(tex.rgb·vc.rgb·boost, t·vc.rgb·tex.a, t)
     alpha = sat((tex.a·vc.a − e)/(1 − e/2)), e = erosion (TEXCOORD_2.y, 0 when flag 0x400).
5. glt spec-map flags OBJ_MTLBLEND_LAYER_CONST[15].x (CB_OBJ_MATERIAL_DATA reg 21): 5,414 glt PS read it, all test the
   same bits {2,4,32..32768}; the 164 GLT_IS_DECAL perms omit bit 512. No permutation hard-codes it ⇒ it is a pure
   CPU per-material value; the UseSpecMap writer cannot be settled from shaders. (mtl1-39: exe light RE also
   did not find it; see LibSaber STATUS mtl1-39.) SFX define slots: bit0 SFX_ALPHA_BLEND, bit1 SFX_BLEND_ADD_NO_ALPHA,
   bit2 SFX_IS_DECAL (retail 0x142450650); material blendMode enum none0/add1/blend2/premul_a3 (FUN_1426212d0).
6. Clustered light falloff (particles PS 214/215 PASS_LIGHT_ATLAS_DRAW, CB_COMMON_LIGHTNG light i = regs 4i+1..4i+3):
     k = sat(P.w·400); L = P.xyz − k·x; d2 = |L|²; win = sat(1 − (k·d2)²·C1.w)²; spot = sat(dot(−L̂, D.xyz)·S + D.w)²
     I = spot·win·((1−k)+P.w)·C1.rgb  (no 1/d² in the point path; area/tube branch uses 1/(d²+P.w)·0.25).
   So the window is (1 − d⁴·C1.w)² (C1.w presumably 1/r⁴), not the browser's (1 − d²/r²)². C1.w/P.w values are CPU.
