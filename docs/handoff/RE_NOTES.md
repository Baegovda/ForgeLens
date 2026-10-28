# RE_NOTES — sm2_retail.exe evidence index

All addresses are virtual addresses in `sm2_retail.exe` (retail Steam build, 81 MB; box copy `/workspace/rea/sm2_retail.exe`, Ghidra project `/workspace/rea/ghidra_proj2.gpr`, program `sm2_retail.exe` since the 2026-10-27 box rebuild; older notes said `sm2`).
- Conversions used: rdata VA = file offset + 0x140001200; .text VA = file offset + 0x140000a00; for `strings`-style offsets, VA = offset + 0x140000000.
- `FUN_xxxxxxxx` = Ghidra function. `@0x...` = string address. Plain `0x...` = instruction, table or constant.
- Decompile more with:
  ```
  JAVA_HOME=/home/box/tools/jdk25 /home/box/ghidra/support/analyzeHeadless /workspace/rea ghidra_proj2 -process sm2_retail.exe -noanalysis -readOnly -scriptPath /workspace/rea/ghidra_scripts -postScript Decomp.java /workspace/rea/dec <addrs>
  ```
- The REA MCP times out on this exe, so use Ghidra headless or rizin directly.
- **Status column:** **proven** = read from code/data; **data** = inferred from game data with the exe only giving the field names; **heuristic** = not proven.

## Meshes / templates
| Evidence | What it proves | Status | Code |
|---|---|---|---|
| `simMeshName` @0x1435f1440, `renderMeshes`, nvcloth lib | `*_SIM` parts are cloth-sim shells that are never rendered | proven | IndexMeshVisibility / export skip |
| tpl affixes `compound_skin_lod_index`, `lod_composite_*` (not plaintext in exe) | Rhino-style composite LOD levels: show only index 0 | data | `IndexMeshVisibility.AddCompositeLods` |
| `tessellation_parameters` FUN_1421df540 (@0x1436ea3f8), `pn_triangles` @0x1436ea3e0, `phong` @0x1436ea3f0 | tessellation exists in the engine, but 0 of 37,403 `.td` use it, so the preview needs none | proven | – |
| `CONFIG.useSaniAnimationsInTpl` path FUN_141db55f0 → FUN_142232230; sort FUN_14127d230 | v3 empty-name tracks bind by tpl `animNmb` rank | proven | `ClipBinding.cs` |
| MatrixModel local (ReadSkeleton pairing) | unskinned parts are baked in world space and rigidly bound | proven (data) | `TemplateMeshReader` |
| merged `Object*` proxies on LOD-chain weapons | hidden (bolter "drum") | data | d181b5c |

## Materials / shaders
| Evidence | What it proves | Status | Code |
|---|---|---|---|
| `swizzleTexInShader` 0x14233bf50 | `(a)` labels are logical channels: BC5 roughness in g, BC4 AO in r | proven | `MaterialMaps` sidecar `stored` |
| `blendMode` @0x14355b708; table 0x144cc9180 (20 entries: none, black, opaque, blend, add, …); str→enum 0x14230f690; enum→str 0x14230f710; older table ref 0x144cc9190 | translucency comes from the variant's direct `glt.blendMode`; `none` = opaque | proven | `MaterialMaps.BlendMode/BlendOf`, `BlendModeTests` |
| `akill` @0x14364a800 (table 0x144ccc7e0), compares at 0x14173bb19/0x14173bb54 | alpha test is independent of blendMode | proven | MaterialMaps |
| glt field map FUN_1425c1d00: albedo_tint +0x30, metallicTint +0x40, second_spec +0x94, anisotropic_spec +0x108 | layer offsets | proven | `SpecLayers.cs` |
| second_spec FUN_142576200; aniso FUN_142579ff0; text reader FUN_1425b9500; legacy ARGB pack FUN_142621620 | field layouts; `.td` colours are [A,R,G,B] | proven | SpecLayers |
| permutations FUN_1423b7640 (0x1423b801f aniso > 0.01 [0x143c843ac]; 0x1423b819a second spec); define names FUN_1423b6260 (`GLT_USE_ANISO_SPEC` 0x1437f88a0, `_DIR_MAP` 0x1437f88b8, `GLT_USE_SECOND_SPEC` 0x1437f89c0) | when each layer is active | proven | SpecLayers |
| aniso packer FUN_1423ba150 (0x1423ba35c..3ec k = 1−clamp(a,0,0.95); 0x1423ba3f4 shift) + PS 03840/00004/00042 GLSL | lobe math for the UE Substrate graph | proven (shader) | `import_sm2_materials.py` |
| PS 00004_1 direct-light AO `saturate(2AO−1+|NdotL|)` | DirectAo approximation for hair | proven formula, UE approximation | import script `DirectAo` block |

## Animation (.sani / ETNA)
| Evidence | What it proves | Status | Code |
|---|---|---|---|
| loader FUN_14222fe40 (versions 1..3; slot0 clip+0x60, slot1 +0x68, root +0x70); codec factory FUN_142234c00 | file layout, F2/F3/F6/F7/F4 blocks | proven | `SaniReader.cs`, `EtnaCurves.cs` |
| sampler FUN_142229bb0; root motion FUN_14222f1b0 / FUN_142234f10 | class-A codec, modified Rodrigues rotations | proven | EtnaCurves |
| class A vtbl 0x1437e73e0 (hdr FUN_14222bed0, carve FUN_14222bff0); v3 vtbl 0x1437e74e0 (FUN_14222d0d0/d550/d6b0/d7c0); raw sub-1 vtbl 0x1437e71c0; v3 raw 0x1437e72d0 (unused) | every retail codec | proven | EtnaCurves |
| root sub-1 vtbl 0x1437e77b8 (FUN_14222ef50/ef80/f060/f0d0/f140), sub-2 0x1437e7800; slerp FUN_1427ae2c0 | v1 root motion; rotation steps > 90° are not a bug | proven | EtnaCurves |
| case 3: R0⁻¹(T_last−T0) via FUN_1427aa8a0 / FUN_1427ace20 / FUN_1427ad8b0 | root rotation applied; first-frame rebase is consistent | proven | `ClipPose` |
| `HasAdditive` @0x1435ef808, dumper FUN_141060280 | slot 1 = additive | proven | `__ADD` export |
| Morpheme `TaskBlend2TransformBuffsAdditive` @0x143b7fee8 → FUN_142cddd90; feather FUN_142ce01c0; registrar FUN_142c961b0 | additive blend math | proven (compose order not) | UE import policy |
| `MorphemeController.useAnimRootMotion` +0x41 (@0x1435da538, reg 0x140e4e9be in FUN_140e4b2f0) | root motion is per controller | proven | export warning |
| ETNA block 245 = anim events | event tracks | data | `AnimEvents.cs` |

## Customization (constructor tpls)
| Evidence | What it proves | Status | Code |
|---|---|---|---|
| HideObj **FUN_14133eae0**, ShowObj 0x14133fec0 (→ FUN_141349c70), HideObjs 0x14133eaf0, ShowObjsOnly 0x141347dd0 | geom script bindings | proven | `ArmorVariant.cs` |
| FUN_14133e750 → name lookup FUN_141d912a0, flags node+0x18 → **FUN_142216790**(tree,node,0x140000,hide) walking FUN_14225c680, bits node+0x1c; pattern FUN_142781420/890, FUN_141d99490 | hiding is recursive over the subtree (group hide hides children) | proven | ArmorVariant |
| `SetActivePresetToDefault` + `"PREDEFINED_PRESET_1"` in `character_customization_outfit_state_release.sslbin` | default look = preset 1 | data | `--variant default` |
| `customizable_character_dummy_*.cls` (`team`, `masteryType`) | team/class per constructor | data | `CharacterFromDummyClass` |

## Physics
| Evidence | What it proves | Status | Code |
|---|---|---|---|
| field strings 0x1435f10c0..0x1435f1510 | `.cls` `prop_cloth_sim.clothes[]` / `prop_phys_jiggle` fields | proven | `PhysicsParser.cs` |
| jiggle step loop **0x14147e5b5** in FUN_14147e500; orthonormalise FUN_140fd45e0; clamp FUN_14171ad80 (0x14147ea5b..eb3c); sentinel 888.888 @0x1435571a0; teleport 0x14147ed64; defaults dir (0,0,1) @0x143609db8, min/max @0x143609e08/@0x143609dd8 | exact jiggle port | proven (scalar defaults and teleport threshold not) | `JiggleSolver.cs` |
| cloth config registration 0x141090c39..0x141091847, ctor ends 0x14108f119; phases 0x141089140; tether 0x14108a660; motion 0x141089966; separation 0x141089fe6; selfCollision 0x14108efce; gravity 0x14108f112; environment 0x14108f03a..0x14108f0a4 | cloth defaults | proven | `PhysicsSetup.cs` |
| `*_SIM` COLOR0.r = max distance (≤1e-4 pinned) | motion constraints | data | `ClothSolver.cs` |
| NvCloth solver itself | **approximated** with PBD | heuristic | ClothSolver |

## VFX (.sfx / SSL)
| Evidence | What it proves | Status | Code |
|---|---|---|---|
| node types: **sfx_group @0x143646930**, sfx_part @0x14363c148, sfx_trail @0x14363c280, CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0, nameTex @0x143644760, lwiGeom @0x143645a70, ageSpline @0x143644580, emitDir @0x1436445f0 | `.sfx` is SSL text with tagged unions (`__type = local__X`) | proven | `EffectLibrary`, `SslText` |
| emission union: constantFlow @0x143644cf8, explosion @0x143644d18 (struct @0x143644be0, load 0x141777823), dynamicFlow @0x143644d08 | spawn models | proven | `EffectSimulation` |
| part_aliases reader **0x1418fe5c0** (from 0x141a90761): nFrameX/Y @0x143647ea0/ea8, nFrameMax @0x143647eb0 (0 = X·Y, clamp 255), motionVectorScale @0x143647ec0, Mesh.Vertex0..6 @0x143647ed4 | flipbook grid | proven | `PartAliases` |
| blendType parser **0x141751cd0** / writer 0x141751dd0: blend 0, add 1, blend_add 2 | enum only; **blend_add's blend state is interpreted** | proven / heuristic | `EffectDefinition` |
| sorting @0x143645dd8, parser 0x1417534f0; softDepth @0x143645530 | sort modes, soft particles | proven | EffectSprites / Renderer |
| attached @0x143644cd0 → genOnFly @0x143644b38, genOnCDT @0x143644b48, genOnDie @0x143644b58, numParts @0x143644b18; LCS error @0x143648810 | child emitters; **genOnFly timing approximated** | proven / heuristic | EffectSimulation |
| speedInherit @0x143644878; emitScale @0x143645da0 | inheritance, scaling | proven | EffectDefinition |
| ageSize @0x143645588, colorIntensity @0x1435630b8, alphaIntensity @0x143645238, speedFrame @0x143644718, isFrameCycle @0x143644708, isRandomFlip @0x1436452e8 | sprite params | proven (size = half-width checked; exposure ×1/250 and lit ×100 heuristic) | EffectSprites |
| channel strings DIFFUSE/ALPHA_MASK/SELF_DISTORTION/GLOSS/DISTORTION near 0x1436442d0 | draw targets; DISTORTION = refraction only | proven | EffectDefinition |
| lwiGeom @0x143644870 (block 0x141760a45..0x141762f4d), nameGeomTpl @0x1436447b0, typeInst @0x1436447e8, sfxMtlOverrides @0x143644810, colorTransparency @0x143644700 | mesh particles; override colours are **R,G,B,A** (blood check) | proven + data | `EffectMeshParticles` |
| trail serializer 0x1417b1031: maxTrailLength @0x143645e08, maxSegLength @0x143645e18, timeLifeSeg @0x143645e28, addFirstPointOnStart @0x143645e88; visuals 0x1417b06cd (colorIntensityMultiplier @0x143645cf8, alphaMult @0x143645d30, fixateTex @0x143645c00); shapes billboard @0x1436459f0, tape @0x1436459c0, cross @0x1436459d0, star @0x1436459e0; topology @0x1436458b0 | trail params; **the runtime ribbon builder was not traced** | proven / heuristic | `EffectTrails` |
| `visuals.textureTintControl` @0x143645278 (typeParams+0x140) | 0 = texture used as luminance (grenade smoke blue cast) | proven field, shader mix heuristic | f99b30f series |
| `optimization.degNear/degFar` reflection strings | camera-distance fades; **curve shape assumed linear** | heuristic | eaa33bd |
| socket emit axis: no `emitDir` + `parentArea.direction isEmitByNormal` → bone **+Z** | from data (part_mzl_bolter siblings say z+, jetpack jets z+) | data | `EffectDefinition.EmitByNormal` |
| anim event → VFX: blueprint `.sso` graphs (BulkVfxOnMorphemeEvents, MorphemeEvent → sfx starter BFS); stop nodes BlueprintNodeVfxStop / SfxLibraryStopsfx / StopPackOfVfx / StopAllSfx | which events fire which effects | data | `AnimEventVfxMap` |
| `em_covering_*` (only `physics.cdt.coveringInteraction.radiusScale`) | surface painters (coveringMask), not particles | data | `CoveringDef` |

## Not found / open RE questions
- Composite-LOD affix strings are hashed (not plaintext).
- HLSL lobe source is not in the exe; the decoded PS bytecode was used instead.
- Rows 8/9 material CPU packer not located.
- Trail runtime builder, genOnFly runtime, blend_add blend state, `additionalContrast` shader, VAT splash vertex layout.
- Jiggle scalar defaults and teleport threshold.
- rubric/sorcerer in-place loco clips.

## VFX contrast fields (2026-10-27)
- `"additionalContrast"` @0x143645248, `"contrastColorKey"` @0x143645260, `"textureTintControl"` @0x143645278 (file offset + 0x140001200).
- sfx_part typeParams serializer at 0x14178d437..0x14178d4df: additionalContrast = float at +0x12C (`FUN_142745110`), contrastColorKey = colour at +0x130 (`FUN_1427456a0`), then textureTintControl.
  Other references to the same strings: 0x14178e4cf, 0x14178ef11, 0x14178fc06 (sibling serializers/readers).
- Shader use: particles.sdc PS, `PART_EMITTERS_SHARED_DATA[1]` (rgb = key, w = contrast); see HANDOFF vfx3 part 4.

### Particle shared data packer (2026-10-27)
- 0x140175820 init "part_cpu_data_shared"; 0x1417e1e50 packer; 0x1417e473e call; 0x141854209 upload (0x90000).
- desc+0x4e8 → shared[0].z = textureTintControl; desc+0x4d4..0x4e0 → shared[1].
- Trail VS DEGR/MISC/OFFSET math: see HANDOFF vfx3d.

### vfx3e (2026-10-27)
- Blood-splash VAT VS: pc_dx12_combine_material.sdc 00000_0 (string COMBINE_MATERIAL_SHADER0_BLOOD_SPLASH). See HANDOFF vfx3e.
- Serialiser xrefs: ratioDependency 0x141790409, ratioXY 0x141786eb1, blend_add 0x141751d81/0x14178e041, genOnFly 0x1417acdc6/0x1417ae934.

## vfx4-1 (2026-10-27)
- `FUN_141796980` distortion typeParams parser: softDepth +0, visuals.texture +8, localOffsetX/Y +0x90/+0x94, **power +0x98** (spline, FUN_14177c520), isRandomFlip +0xa8, rotation +0xac, ratioDependency +0xc0, isNoOrientToCam +0xe8 (payload = desc+0x1b0).
- `FUN_1417eeed0` CPU particle vertex: typeParams==1 → colour (0,0,0,power(age)) (desc+0x248 type 1 = 64-sample spline, else const desc+0x250); ×1.0 `DAT_143c844b4`; non-distortion colour ×1/255 `DAT_143560150`.
- `"DISTORTION_SCALE_CONST_MUL_VC_ALPHA"` @0x1437f5a40 (FUN_14236e020 define table). `"CB_PASS_DISTORTION_POSTPROC"` @0x1437133f8, `"DISTORT_POSTPROC_PARAMS"` @0x143713418 (CB layout FUN_1421aa940; filler not found).
- Trail desc parser `FUN_14183a330`: degNear +0x3e8, degFar +0x400 (FUN_14181e410 each), degradationRadius +0x418. `FUN_14187f1e0` copies desc+0x3e0..0x414 → instance +0x1ec..0x220 (degFar triplet scaled) and +0x418 → +0x224. `FUN_1418c09a0` reads +0x400/+0x404/+0x418.

## mtl1-27 material flags / layers / instance tint
- OBJ_MTLBLEND_LAYER_CONST[15].x (CB_OBJ_MATERIAL_DATA byte 336, asint), PS 22961 l.615-800 = 29540: 2 rough raw from diffuse.a; 4 branch; 32 spec.rgb tint + rough from spec.a; 64|8192|16384 metal = sat(sum)*spec.r; 64|8192|16384|32768 rough term from spec.g; 8192|16384|32768 AO from spec.b; 128/256 hDetM channel select; 512 invert; 1024/2048 detail; 4096. flags 0: metal raw 0, rough raw 1. Captured 0 on draws 2846 2856 2870 2882 2893 2945 2956 2967 2979 3027 3364 3374 3419 3430 3441 3452 3464 3793 3806. Writer not found: FlagScan (OR immediates) gave 263 hits in the 0x14232-0x14236 shader-permutation cluster, which is noise. "MetGray"/"format_descr" are not in the exe.
- Material layer parser FUN_14217b180 (copies 14217a2a0/1421793b0/1421784d0; material parser FUN_1420ba210, layers at +0x450). Layer struct bytes: +0 vcSet, +1 uvSetIdx, +4 tilingU, +8 tilingV, +0xc0 heightmapOverride, +0x108 method (1 "dirt", 2 "dirt_no_spec", else 0), +0x109 flags (1 useLayerAlpha, 2 useHeightmap), +0x10a heightmap.invert, +0x10b blendNormalAdd, +0x10c heightmapUV.uvSetIdx (0xff off), +0x10d texChannelBlendMask, +0x10e weights.colorSetIdx, +0x10f heightmap.colorSetIdx, +0x110 weightMultiplier, +0x114 heightmapSoftness, +0x118 heightmapUV tiling, +0x120 upVector.enabled, +0x124/+0x128 upVector angle/falloff (derived).
- DYNAMIC_GLT writer FUN_1423bb1e0 (glt vtable 0x1437edb20): record+0 = inst; default DAT_143561fd0 (1,1,1,1). If !(param_4[1]>>19 & 1): rgb = bytes of *(u32*)(obj+0x74) (R 16-23, G 8-15, B 0-7) * 1/255. If mode (param_7+0x12) == 2: rgb *= byte(obj+0x7c)/255; == 1: the value goes to .w. If obj flags & 0x200: rgb *= 10. obj is a runtime instance (no objOBJ static field). PS: albedo_lin = poly(inst.rgb * tex_srgb [* chameleon c]).

## mtl1-28 sparks / trails
- "SPARKS" strings @0x1436dceac / 0x1436e16a0 have data-table refs only (no code xref found). The spark draw kind (opaque, blending off) comes from the capture (smoke 88302 eid 2086, PS 1770712). The mapping from emitter to spark (IsGpu && StreakRatio>0) is **HEURISTIC**. Trails: CB_PASS_TRAIL, SrcAlpha/InvSrcAlpha (capture).

## mtl1-30 cloth shared CDT
- Prop cloth struct (serializer FUN_1410953d0 / deserializers FUN_141094a60, FUN_141094fb0, FUN_141094260): clothes @+0x148 (count +0x150, stride 0x140), simulationDistance +0x158, decreaseSolverFreq +0x15c, enableTeleportationInCine +0x15d, **sharedCDTCapsules @+0x160 (count +0x168, stride 0x48)**, sharedCDTSpheres @+0x170 (count +0x178, stride 0x28), sharedCDTBoxes after, interCollisionCfg {iterations +0x194, collisionDistance +0x198, collisionStiffness}.
- Per-config `cdtCapsules` (string @0x1435f1328) is referenced only by FUN_14108f130/14108ff30/141090c00/141091880 (config (de)serializers). The runtime consumer that decides which shapes a cloth collides with was **not** located, so "no cdt lists → all shared shapes" is **HEURISTIC**.


## mtl1-31 shared CDT union (HEURISTIC, data only)
- Retail scan of 282 cls files with prop_cloth_sim: cdt capsule/sphere entries are always full items with bones (306), never name references to shared shapes. 217 files define shared shapes. Of all cloths, 2995 have own and shared shapes, 1014 have own only, 538 have shared only. Shared shapes are prop-level and can't be attached to a cloth by name, so the parser treats them as applying to every cloth of the prop (union with own lists). The exe consumer is still not located (only (de)serializers; see mtl1-30).
- UE evidence for per-cloth colliders: a shared PA (the union of every cloth's shapes) pushes particles of one cloth with shapes rest-fitted to another (tech_priest v0/v3, STATUS mtl1-31).

## mtl1-34 VFX export / UE findings
- `TextureDocument.Decode` (TextureDecode.DecodeLibrary) returns BGRA. GlPreview swaps it for meshes (GlPreview.cs ~480-498), but EffectRenderer uploaded it as RGBA without a swap. Fixed in EffectTextures.
- Engine defaults for absent sfx keys (constructors FUN_14178cbf0 / FUN_1417c6290; the readers only overwrite present keys):
  - isBlendAlphaMultiply = true.
  - textureTintControl = 0.
  - GPU streaks: isEnabled true, ratio 0.3.
  - See /workspace/rdc/modtools/FINDINGS.md.
- UE 5.7 SceneCapture2D (measured):
  - BLEND_AlphaComposite = One/InvSrcAlpha.
  - Manual exposure, bias 0, physical camera off -> exposure scale exactly 1.
  - SceneColor (translucent) and RM_2D_OFFSET refraction return a constant inside the capture, so distortion is a separate shift pass + PostProcessInput0 resample (point sampled).
  - With capture_every_frame off, editor capture_scene() skips the PP material.

## mtl1-35 grading LUT / soft depth
- Content LUTs (client_pc, 22 `*_lut`, e.g. neutral_lut, story_tower_lut, trygon_lut): 32×32×32 BGRA8, `_0.pct_mip` 131072 B, linear_rgb=false. Texel (r,g,b) at ((b·32+g)·32+r)·4, BGRA. neutral_lut is the identity (texel 16 -> 132). Values are display-space.
- TONEMAP_LUT bake = CS s1996 (smoke 88302 eid 3226, plasma 5994, outdoor 137266 eid 39915): u = i/31; x = max(0, 0.18·2^((u-0.43)·14)) - 0.18·2^(-0.43·14); t = sRGB(sat(Hable(x)·WP)); out = lerp(PREV, NEXT(t·(N-1)/N + 0.5/N), weight); captures weight 1. Outdoor NEXT = story_tower_lut, WP 1.343645; smoke/plasma NEXT = runtime 16³ COLOR_GRADING (not dumped).
- Apply side (final PS 1209/1159) not on the box. HEURISTIC mapping: coordinate = 0.43 + log2((x + c0)/0.18)/14, texel u·31, trilinear. Identity bake through it is within 1 LSB of exact Hable.
- `common_pc_dx12_lut.sdc` (4909 B shader cache) extracted with the LUTs; not inspected.
- Soft depth (particles PS 101818, plasma nightly dis s101818 ~l.1426-1446): sceneZ = -(n·f)/((n-f)·(d - n/(n-f))) from PART_RENDER_DEPTH + COMMON_CAMERA_NEAR_FAR_Z; fade = sat(((sceneZ - TEXCOORD3.x)·(1/softDepth) - 0.5)·sign + 0.5), flag 512 -> sign -1; rgb and alpha *= fade.
- Hellbrute pretonemap EXR vs final_331.png (E 0.030691): mean |d| ~78; not usable as tonemap evidence (earlier snapshot, 4K upscaled final).

## mtl1-36 VFX light units (CB_COMMON_LIGHTNG, particles s4462)
- `COMMON_LIGHTS_NUM` int4 (x directional, y point), `COMMON_DYN_LIGHT_PARAMS[1024]` packoffset 16; light i = PARAMS[4i..4i+3]: P0 (pos, mult), P1 (HDR colour, 1/r^4), P2 (spot dir, offset), P3 (spot scale, shadow idx, ...); PARAMS2[i] > 127 = no projector.
- att = sat(1 - d^4·invR4)^2 · P0.w, zeroed when P0.w > 200000. No 1/pi on particles. Unlit path (_label167) uses COMMON_HDR_AVERAGE_LUMINANCE.
- Plasma: avgLum 4.150339, EXPOSURE_DIV_AVG_LUM 0.027324 (Key ~0.114); smoke 4.8185 / 0.023783 (no valid light).

| light | match | CB | model |
|---|---|---|---|
| 0 fx_plasma_exterminator_shot_venting | r 24 exact, colour (94,120,152) sRGB-decoded ratio 1.3 % | 274.0 / 269.0 | lin(0.596)·200·4.150 = 260.5 (-5 %/-3 %) |
| 2 fx_plasma_charging_side | r 2 exact | 1145 | lin·569·avgLum, 569 inside spline 150..4448 |
| 3 fx_pve_ship_console_screen LIGHT_for_override | r 1, I 50 -> 54.6 | colour overridden | excluded |
| 1 | r 3.82 | - | unmatched |

- Raw (non-decoded) colour does not fit; ColorMultCtrl always spline [0,10], ignored. Adaptive scaling by avgLum HEURISTIC (one light).

## mtl1-37 tpl_markup locators / cloth remap defaults
- `.tpl_markup` `objectInfos[]` = per template object {objectId.name, animationRole, entities[]}; `tpl_markup_locator` entity = {name, localTransform{position m, rotation euler°}} parented to that object. Calgar: Backpack_CDT_L@powerplant_backpack (0.203,0,-0.593), fx_loc_chest@BACKup, fx_muzzle_l@ARM2L (0,90,0), fx_loc_hit_l@HANDL, fx_muzzle_r@ARM2R, fx_loc_hit_r@HANDR, fx_loc_hit@ROOT. sorcerer_exalted: none.
- ClothEntry layout (serializer 0x141093480): +0 simMeshName, +8 renderMeshes, +0x18 simRemapConfig {maxInfluenceDist f32, +0x1c distanceRemapPower f32, +0x20 useTopologicalDistance u8}, +0x28 config (ctor 0x14108ef40). ClothEntry ctor 0x141093260: 0.15 / 5 / false. No `maxInfluenceVert` string in the retail exe.

## Shader bundles (.sdc) — static (2026-10-28 KST, see SHADERS.md)
- Format PROVEN (loader FUN_1420b4ce0): header + crc32-keyed groups + zlib streams; 80 files, 48,828 DXIL binaries; permutation defines from `filenamespreload`; DXIL HASH == RenderDoc shader hash.
- Final PS 1209/1159 = hdr PS 44/47: uvw = sat(0.43 + log2((c + 0.18·2^(−0.43·14))/0.18)/14)·31/32 + 1/64, c = EXPOSURE·(scene + bloom). The mtl1-35 LUT-apply HEURISTIC is now PROVEN (PS 47 adds ±1/255 triangular dither; alpha 0).
- SFX blend PROVEN via `psos`: SFX_BLEND_ADD_NO_ALPHA = One/One (alpha keep), SFX_ALPHA_BLEND = SrcAlpha/InvSrcAlpha.
- Particles: single premultiplied One/InvSrcAlpha PSO; flag 0x20 (add) writes alpha 0; flag 0x400 (unless isBlendAlphaMultiply) skips colour×alpha; blend_add has no bit ⇒ identical to blend (shader side PROVEN).
- textureTintControl t = shared[0].z: rgb = mix(tex·vc·boost, t·vc·tex.a, t); boost = 1 + sat(1 − |key/255 − tex|)·additionalContrast (shared[1]).
- OBJ_MTLBLEND_LAYER_CONST[15].x: same bit tests in all 5,414 glt PS (decals omit 512) ⇒ CPU-only; UseSpecMap stays HEURISTIC.
- Clustered light window (particles PS 214): sat(1 − d⁴·C1.w)² · spot², no 1/d² in the point path.
- mtl1-38: PS 281 colour factor = sat(ac + flag0x400) (no vertex alpha with 0x400; vertex alpha only via ac); VS 248 TEXCOORD.rgb = COLOR_1.bgr·TEXCOORD_1.w·CB_COMMON[22].y, TEXCOORD.w = vertex alpha. SFX define table built at 0x142450650 (SFX_ALPHA_BLEND / SFX_BLEND_ADD_NO_ALPHA / SFX_IS_DECAL bits at +0x1028); the material → define selector is not found yet.
