# SM2 REA Deep Pass — ForgeLens peel (static)

- Target: `/workspace/sm2_re/sm2_retail.exe` sha256 `4c4fb3a009a803ea5c27e9ced0a36e7c401ef95eb6dd7d651a629f54ee0252fc`
- Mode: **static only**; game never run; **no ForgeLens code changes**
- Tools: REA `binary_session{}` → `open_binary` (ghidra) OK; `search_strings`/`analyze_function` MCP **-32001 timeout** → headless Ghidra `/workspace/rea/gh/proj` `-noanalysis` + pefile/capstone
- Evidence: `/workspace/sm2_re/evidence/*` (decomp IDs = filenames; REA MCP Evidence IDs pending cold analyze)
- Zone: Asia/Seoul; started 2026-11-02 ~03:16 KST
- Prior: `/workspace/sm2_re/REA_FINDINGS.md` (A UseSpecMap writer, B P0.w) reused

---

## 1) Full material system — OBJ_MTLBLEND_LAYER_CONST

### Label: **PROVEN** (CPU packer + usage→bit + shader bit semantics from prior DXIL)

### Writer family
| Addr | Role |
|------|------|
| `FUN_14217da70` | primary LAYER packer → `param_1[0x3c]` = `[15].x`, `param_1[0x3d]` = `[15].y` |
| `FUN_14217e330`, `FUN_14217ebf0` | siblings (same `[15].x` / `0x2000` logic) |
| Reg table | DATA xref `@0x145359914` |

CB: `OBJ_MTLBLEND_LAYER_CONST` = `CB_OBJ_MATERIAL_DATA` member, **0x40 float4**; `[15].x` byte **336** (=0x150).

Evidence: `evidence/FUN_14217da70.c`, `deep_batch2_cloth_mtl_light.c`.

### Texture usage bit table (PROVEN complete, 27 entries)

Serializer `FUN_14267baf0` walks `longlong*` table **`0x144ccee10` … `0x144cceee8`** (exclusive), sets `*(param_3+4) |= 1 << bitIndex` on prefix match of texture usage string.

| bit | mask | name |
|----:|------|------|
| 0 | 0x00000001 | MD |
| 1 | 0x00000002 | MSC |
| 2 | 0x00000004 | MSP |
| 3 | 0x00000008 | MRGH |
| 4 | 0x00000010 | MNM |
| 5 | 0x00000020 | MDT |
| 6 | 0x00000040 | MDTM |
| 7 | 0x00000080 | MT |
| 8 | 0x00000100 | MAK |
| 9 | 0x00000200 | MH |
| 10 | 0x00000400 | MBR |
| 11 | 0x00000800 | MBL |
| 12 | 0x00001000 | MR |
| 13 | 0x00002000 | MF |
| 14 | 0x00004000 | MLM |
| 15 | 0x00008000 | MPM |
| 16 | 0x00010000 | MRC |
| 17 | 0x00020000 | HDR |
| 18 | 0x00040000 | MSCG |
| 19 | 0x00080000 | MEM |
| 20 | 0x00100000 | MAO |
| 21 | 0x00200000 | **MSCRGHAO** |
| 22 | 0x00400000 | MD16U |
| 23 | 0x00800000 | MRGHAOD |
| 24 | 0x01000000 | MHTERR |
| 25 | 0x02000000 | MMRAOT |
| 26 | 0x04000000 | MTRAO |

Stored on texture object `+0x2c` (usage dword). Evidence: `decomp/FUN_14267baf0.c` L93–116.

### LAYER_CONST[15].x flag writers (PROVEN from `FUN_14217da70`)

Material GPU record `param_2` texture slots: `+0x80`, `+0xa0`, `+0xb0`, `+0xc0` (pointers; usage at `tex+0x2c`).

| Flag | Condition | Meaning / DATA |
|------|-----------|----------------|
| 0x2 | `*(+0x90)+0x2c & 8` | usage bit3 **MRGH** (seed value 2) |
| 0x4 | `param_2+0x68 != 0` | slot present |
| 0x8 | `param_2+0xa0 != 0` | slot present (↔ MNM) |
| 0x10 | `param_2+0x58 != 0` | slot present (detail/partial DATA) |
| 0x20 | `*(+0xb0)+0x2c & 2` | usage bit1 **MSC** |
| 0x40 | `*(+0xb0)+0x2c` bit18 | **MSCG** |
| 0x80 | `*(+0xc0)+0x2c & 0x40` | usage bit6 **MDTM** |
| 0x100 | `*(+0xc0)+0x2c` bit9 | **MH** |
| 0x200 | `*(char*)(param_2+0x103) != 0` | material bool |
| 0x400 | `*(float*)(param_2+0x7c) != 0` | nonzero scalar |
| 0x800 | `*(+0x80)+0x2c` ∩ pack `{0x100080000}` bits | MEM-ish |
| 0x1000 | `*(+0x80)+0x2c & 0x100000` | **MAO** bit20 |
| **0x2000** | `*(+0xb0)+0x2c` bit21 | **MSCRGHAO / UseSpecMap** |
| 0x4000 | `*(+0xb0)+0x2c` bit25 | **MMRAOT** |
| 0x8000 | `*(+0xb0)+0x2c` bit26 | **MTRAO** |
| 0x20000 / 0x60000 | `param_2+0x104` (−1 / 0 / else) | mode tri-state |

`[15].y` (`param_1[0x3d]`) packs separate feature bits from `+0x100..+0x118` (channel/invert/detail style); defaults involve tiny float bitcasts `2.8026e-45` / `8.40779e-45` (= int 2 / 6).

### Shader metalness / roughness / AO (PROVEN prior DXIL + flag table)

From RE_NOTES mtl1-27 / glt PS (unchanged this pass): asint(`[15].x`) tests:
- **2**: rough raw from diffuse.a
- **4**: branch
- **32**: spec.rgb tint + rough from spec.a
- **64|8192|16384**: metal = sat(sum)*spec.r
- **64|8192|16384|32768**: rough from spec.g
- **8192|16384|32768**: AO from spec.b
- **128/256**: hDetM channel; **512** invert; **1024/2048** detail; **4096** …
- flags **0**: metal raw 0, rough raw 1

Packed **MSCRGHAO** (bit21 → LAYER 0x2000): channels metal/rough/…/AO in one map — matches armory DATA 0x2000↔MSCRGHAO 68/68.

### Layer blend / tint / emissive (PROVEN layout; packers cited)

| Item | Evidence |
|------|----------|
| Material layer parser `FUN_14217b180` (copies `14217a2a0`/`1421793b0`/`1421784d0`); layers @ material `+0x450` | offsets: vcSet+0, uvSetIdx+1, tilingU/V +4/+8, heightmapOverride+0xc0, method+0x108 (1 dirt / 2 dirt_no_spec), flags+0x109, … weightMultiplier+0x110 |
| glt field map `FUN_1425c1d00` | albedo_tint+0x30, metallicTint+0x40, second_spec+0x94, anisotropic_spec+0x108 |
| DYNAMIC_GLT `FUN_1423bb1e0` | instance tint; ×10 if obj flags&0x200 |
| Aniso packer `FUN_1423ba150` | k=1−clamp(a,0,0.95) |
| Chameleon | CPU `FUN_1423ba150` / desc+0xb8; LAYER 0x2000 absent when no MSCRGHAO on `+0xb0` (derived, not a chameleon branch) |

### ForgeLens changes (**do not apply here**)
1. **UseSpecMap** = material specular/packed tex (`GPU+0xb0`) usage has **bit21 MSCRGHAO**; optional also MSCG bit18 / LAYER 0x40 for legacy spec path.
2. Ship full **usage bit table** (above) into `SYMBOL_DB` / materials docs; stop guessing channel packs.
3. Drive metal/rough/AO from LAYER bits + MSCRGHAO sampling as shader (not `isMetallic` HEURISTIC alone).
4. Layer dirt methods 1/2 and DYNAMIC_GLT tint rules already partially in import — align UE Substrate with `FUN_14217b180` / `FUN_1423bb1e0`.

### Remaining unknown (material)
- LAYER **0x1** unused in packer seed (starts 0 or 2); **0x2/0x4/0x8/0x10** CPU conditions now PROVEN (table above).
- `[15].y` bit dictionary not fully named (offsets known).

---

## 2) Cloth / physics

### Label: **PROVEN** (field→offset map + defaults); RestFit*/F45 motion = **exhausted / not in exe as named knobs**

### Config registration & (de)serializers
| Addr | Role |
|------|------|
| `FUN_14108ef40` | cloth **config ctor** defaults |
| `FUN_14108efce` / `FUN_14108f03a` | selfCollision / environment subsection ctors |
| `FUN_14108f112` | gravity Y writer (`+0x78 = -9.8`) |
| `FUN_141089140` | phase (vertical/horizontal/bend/shear) ctor |
| `FUN_14108a660` | tether ctor |
| `FUN_141089966` / `FUN_141089fe6` | motion / separation partial ctors |
| `FUN_14108f130` / `FUN_14108ff30` / `FUN_141090c00` / `FUN_141091880` | config (de)serialize |
| `FUN_14108ad40` / `FUN_14108b4e0` | **environment** (air/wind) (de)serialize |
| `FUN_141089170` / `FUN_141089600` | phase field (de)serialize |
| `FUN_141093260` / `FUN_141093480` | ClothEntry ctor / serialize (`maxInfluenceDist=0.15`, `distanceRemapPower=5`) |
| `FUN_1410953d0` / `FUN_141094a60` / `FUN_141094fb0` / `FUN_141094260` | prop_cloth_sim root |

Evidence: `evidence/cloth_ctors_batch1.c`, `deep_batch2_*`, `deep_batch3_cloth_env.c`.

### Phase struct (vertical / horizontal / bend / shear) — stride 0x10
Defaults **PROVEN** (`FUN_141089140`):

| offset | field | default |
|--------|-------|--------:|
| +0x0 | stiffness | **0.8** |
| +0x4 | stiffnessMultiplier | **1.0** |
| +0x8 | compressionLimit | **0.9** |
| +0xc | stretchLimit | **1.1** |

Config layout (from `FUN_141090c00` / write path):
- `vertical` @ **+0x00**, `horizontal` @ **+0x10**, `bend` @ **+0x20**, `shear` @ **+0x30**

### Tether / motion / separation
| Block | offset | defaults |
|-------|--------|----------|
| tether | +0x40 | stiffness 0.5, mult 1.0, enabled 0 (`FUN_14108a660`) |
| motion | +0x4c | +4→0.5, +8→1.0 (`FUN_141089966`); fields stiffness + distanceScale |
| separation | +0x58 | radiusScale/offsetScale style 1.0/1.0 (`FUN_141089fe6`) |
| selfCollision | +0x64 | stiffness + stiffnessMultiplier nested |

### Solver / collision / gravity (config)
| field | offset | default (ctor) | status |
|-------|--------|----------------:|--------|
| gravity | **+0x74** (float3; Y at +0x78) | **(0, -9.8, 0)** | PROVEN |
| cdtCapsules | +0x80 (count +0x88) | empty | PROVEN |
| cdtSpheres | +0x90 | empty | PROVEN |
| cdtBoxes | +0xa0 | empty | PROVEN |
| environment | +0xb0 | see below | PROVEN |
| solverFrequency | +0xe0 | **30.0** | PROVEN |
| stiffnessFrequencyScale | +0xe4 | **0.5** | PROVEN |
| acceleationFilterWidth | +0xe8 | **30** (int) | PROVEN |
| **collisionThickness** | **+0xec** | **0.0** | **PROVEN** |
| friction | +0xf0 / +0xf4 region | 0.05 seen at +0xf4 | PROVEN-ish (confirm per write path) |
| inertia.angular / centrifugal | +0xfc / +0x104 / +0x108 | 1.0 triad | PROVEN |
| interCollision | +0x10c | bool | PROVEN |
| continuousCollision | +0x10d | bool | PROVEN |
| collisionMassScale | +0x110 | **20.0** | PROVEN |

### Environment block (@ config+0xb0)
| field | rel offset | notes |
|-------|-----------:|-------|
| airDrag | +0 | f32 |
| airLift | +4 | f32 |
| localWindVelocity | +8 | vec |
| globalWindVelocity | +0x14 | vec |
| useWindSystem | +0x20 | u8 |
| windSystemInfluenceScale | +0x24 | f32 |
| fluidDensity | +0x28 | f32 |
| collisionWithEnvironment | +0x2c | u8 |

### Prop root `prop_cloth_sim` (PROVEN, matches RE_NOTES)
clothes @+0x148 (count +0x150, stride 0x140); simulationDistance +0x158; decreaseSolverFreq +0x15c; enableTeleportationInCine +0x15d; sharedCDTCapsules +0x160; sharedCDTSpheres +0x170; sharedCDTBoxes +0x180; interCollisionCfg iterations +0x194, collisionDistance +0x198, collisionStiffness follows.

### ClothEntry remap defaults (PROVEN)
`maxInfluenceDist=0.15`, `distanceRemapPower=5`, `useTopologicalDistance=false` (`FUN_141093260`).

### RestFitFloor / ShallowFrac / ThickCm (ForgeLens HEURISTIC)
- **No** `restFit*`, `RestFit`, `ShallowFrac` strings in retail exe (pefile scan).
- Game **`collisionThickness` default = 0.0**, not 2 cm — UE `ThickCm=2.0f` is importer-only.
- Rest-fit floor/deep/shallow are **Chaos/UE collision fitting knobs**, not NvCloth asset fields.
- **Exhausted** for replacing HEURISTIC with exe constants: use **per-asset `collisionThickness` + capsule radii from cdt\***; keep RestFit* as documented HEURISTIC or derive from measured penetration vs authored CDT (data), not from a missing engine constant.

### F45 motion params
- Literal `F45` appears **once** at VA `0x1436a7600` inside a packed binary blob (`…F45\x02…`), **zero** RIP-lea / absolute QWORD code refs found.
- Likely unrelated tag or embedded resource; **not** a cloth motion API name.
- Closest PROVEN motion fields: cloth **`motion`** block (stiffness/distanceScale) + NvCloth solver kernel strings at `FUN_142dbbaf0` (`cloth::SwSolverKernel::*`) — PBD approximation in ForgeLens remains HEURISTIC vs NvCloth.
- Anim **F4** root-motion subtypes exist in RE_NOTES (`FUN_14222ef80` etc.); not “F45”.
- **Status: exhausted as named F45 cloth params** — treat ForgeLens “F45 motion unverified” as still open only if it means a specific asset format version outside this string.

### Ragdoll
Strings: `ragdollTagName`, `ragdollUnstableSpeed`, `ragdollUnstableSize` — not deeply decompiled this pass (next batch if needed).

### ForgeLens changes (**do not apply here**)
1. Map `.cls` cloth config → UE Chaos/PBD using **PROVEN offsets/defaults** above (esp. gravity −9.8, phase 0.8/1/0.9/1.1, solverFrequency 30, **collisionThickness from asset**).
2. Replace global `ThickCm=2` with **asset collisionThickness** (often 0) + shape radii; document RestFit* as non-game.
3. Wire airDrag/airLift/wind block when viewer supports wind.
4. Do not invent F45 constants from exe.

---

## 3) Lighting pipeline (beyond P0.w)

### Label: **PROVEN** (CPU pack + adapt + spot + prior shader); shadows/IBL/fog partial

### Already PROVEN (reused)
| Func | Role |
|------|------|
| `FUN_14224f010` | P0.w |
| `FUN_14224ecf0` | 16-float `COMMON_DYN_LIGHT_PARAMS` pack |
| `FUN_141384090` | desc → render light (`attenCurveParam`→+0x54, flags→+0x6d) |
| `FUN_141e90cb0` | `clamp(avgLum, 0.05, 50)` adaptScale |
| `FUN_141e72cf0` | light desc serializer (radius, softRadius, useInverseSquaredFalloff, …) |

Constants: `0.0025`, `8e22`, `299`, `400`, `π/360`, `256` (see REA_FINDINGS).

Spot: type 1; `cos(angle*π/360)`; cone scale/offset in P3/P2; falloff window shader `(1−d⁴·P1.w)²`.

### Shadows
- Packer writes **shadow index** at `out[13] = param_4`; `out[14]` projector-related (`+0x68`, sign from bit3); `out[15]` specular kill.
- Full shadow-map / cascade CPU not peeled this pass → **INFERRED** index-only on light CB.

### Exposure / tonemap / bloom (shader PROVEN prior; CPU strings located)
- Final tonemap hdr PS 44/47: `c = EXPOSURE·(min(scene,64512)+min(bloom,64512))`; LUT uvw with GREY=0.43, RANGE=14; CB_PASS_HDR layout in SHADERS.md.
- Strings: `HDR_LUT_GREY_POINT`, `HDR_LUT_RANGE_LOG`, `WHITEPOINT_SCALE`, `vignetteParams`, `bloomIntensity` (lea cluster `@0x1417a0953`… / `@0x141f180e4`… / `@0x1424cce48`…).
- SFX emissive / LitScale path: prior PROVEN `FUN_1424540e0` adaptive emissive (`SFX_EMISSIVE_PARAMS`); this pass retained decomp in batch2 (large).
- **Bench exposure 1/250** still HEURISTIC (needs capture).

### Ambient / IBL / fog
- Particle/fog consumers of `COMMON_DYN_LIGHT_PARAMS` documented in SHADERS/RE_NOTES; dedicated IBL CB packer **not newly proven** this pass.

### ForgeLens changes
1. Keep P0.w / ×400 / spot cone / adaptScale fixes from REA_FINDINGS.
2. Pass through **shadowIdx** if viewer clusters lights.
3. Tonemap: use PROVEN GREY/RANGE/EXPOSURE formula for hub preview; bloomIntensity from td/sfx fields when bound.
4. Fog/IBL: still need dedicated pass (mark open).

---

## 4) VFX LitScale / fire-clip nmb / muzzle

### LitScale / VFX exposure
- **PROVEN prior**: adaptive emissive via `FUN_1424540e0` + avgLum clamp; SFX loader `FUN_1425fd580` emissive.intensity→+0x24.
- Exact name `LitScale` **not** in exe; treat as ForgeLens alias of emissive/exposure scale → keep using PROVEN SFX_EMISSIVE path.

### Fire-clip / nmb ops 95 / 98 / c4
- Strings: `nmb`, `nmbLocks`, `Expression: %s`, `NodeDef.cpp`.
- lea `nmb` → `0x140e37ce8` / `0x140e37f05`; `nmbLocks` → `0x14137c489`… (decomp `evidence/deep_batch5b_nmb.c`).
- **Opcodes 95/98/c4 still not identified** in those functions this pass — needs dedicated bytecode/dispatcher peel. Status: **open / U8–U9**.

### Muzzle tip vs shootTip
- `muzzle` string `@0x1435520c8` — only lea hit `0x1402365c0` is an **empty stub** (decomp returns).
- Locators DATA: `fx_muzzle_l/r` in tpl_markup (RE_NOTES mtl1-37).
- `shootTip` string **absent**. Shoot→muzzle remains **HEURISTIC** (event track name).

---

## Angles tried / exhausted
1. REA MCP analyze/search timeout → headless Ghidra retained project (works).
2. Usage table complete via `FUN_14267baf0` bounds.
3. Full LAYER[15].x OR-imm conditions from packer.
4. Cloth field string table `0x1435f10c0..` → serializer offset map + ctor defaults.
5. RestFit*/F45 named constants: **absent** as engine knobs.
6. Wind/air environment block mapped.
7. bloomIntensity / HDR LUT strings located; tonemap formula reused from shader RE.
8. nmb op decode & muzzle attach: **not closed**.

---

## Suggested next static slices (if continued)
1. Decompile bloomIntensity serializers `@0x1417a0953` cluster + CB_PASS_HDR writer.
2. Fog / IBL CB packers via string `common_light_grid` / `FOG_`.
3. nmb expression opcode table (search immediate 0x95/0x98/0xc4 near `ExpressionCPUpdate`).
4. Ragdoll constraint serializers (`ragdollUnstable*`).
5. Re-cite with REA Evidence IDs once ghidra cold analyze finishes (`analyze_function` on hot addrs).

### nmb container field strings (near 0x1435d9334)
- See VA dump in session; serializers FUN_140e37ce0/FUN_140e37f00 are **asset field binders**, not expression opcode dispatch — op 95/98/c4 still open.
