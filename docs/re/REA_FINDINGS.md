# SM2 REA Findings — ForgeLens unknowns A/B

- Target: `/workspace/sm2_re/sm2_retail.exe` sha256 `4c4fb3a009a803ea5c27e9ced0a36e7c401ef95eb6dd7d651a629f54ee0252fc` (= Steam Retail Revaptor-FX)
- Tools: headless Ghidra 12.1.4 on retained `/workspace/rea/gh/proj` + pefile/capstone; `user-rea` open_binary OK but analyze_* MCP timed out (cold import) — Evidence IDs below are **headless decomp artifact IDs** pending REA re-cite
- Mode: static only; game never run; **no code changes**
- Zone: Asia/Seoul; updated after box FS reset rebuild

---

## (A) Who writes `OBJ_MTLBLEND_LAYER_CONST[15].x` (UseSpecMap)

### Label: **PROVEN** (CPU writer + condition)

### Observations

**Writer:** `FUN_14217da70` (siblings `FUN_14217e330`, `FUN_14217ebf0` — same `[15].x` / `0x2000` logic).  
Registered in RVA table @ `0x145359914` (Ghidra DATA xref). Decomp: `/workspace/sm2_re/decomp/FUN_14217da70.c`.

**Store site:** `param_1[0x3c] = flags_as_float`  
- `0x3c` floats = float4 index **15**, component **.x** → `OBJ_MTLBLEND_LAYER_CONST[15].x` when `param_1` is the start of that 64-float (0x40 float4) member.  
- Matches CB_OBJ_MATERIAL_DATA byte **336** (= 0x60 LAYER base + 15×16).

**Condition for bit `0x2000` (packed MSCRGHAO / UseSpecMap):**
```c
lVar1 = *(longlong *)(param_2 + 0xb0);          // texture object*
uVar5 = *(uint *)(lVar1 + 0x2c);                // usage mask
fVar17 = fVar13 | 0x2000;
if ((uVar5 >> 0x15 & 1) == 0) fVar17 = fVar13; // bit 21
```
Usage table `0x144ccee10`: **bit 21 = MSCRGHAO** (`0x200000`). Matches armory DATA 0x2000↔MSCRGHAO 68/68.

**Other LAYER bits from same function (same usage dword @ tex+0x2c):**

| Flag | Condition (summary) | Usage bit / slot |
|------|---------------------|------------------|
| 0x8 | `param_2+0xa0 != 0` | slot presence (DATA↔MNM) |
| 0x40 | `*(param_2+0xb0)+0x2c` bit 18 | MSCG |
| 0x100 | `*(param_2+0xc0)+0x2c` bit 9 | MH |
| 0x800 | texture @ `+0x80` usage ∩ mask | — |
| 0x1000 | `*(param_2+0x80)+0x2c & 0x100000` | MAO bit 20 |
| 0x2000 | `*(param_2+0xb0)+0x2c` bit 21 | **MSCRGHAO** |

**Chameleon:** DATA flags=0. Writer emits 0x2000/0x40 only when those texture objects exist **and** usage bits set; chameleon paths without MSCRGHAO/MSCG at `+0xb0` naturally get 0 — explains chameleon correlation without a separate “chameleon” branch in this function.

**Negative evidence:** no `test/and imm 0x200000` within 120B of `or imm 0x2000` outside shader-permutation cluster (capstone scan) — remap uses **`bt`-style bit 21** (`>> 0x15`), not a second immediate.

### Inferences
- Legacy DATA `0x40↔*_spec` is consistent with MSCG (or co-bound spec) on the `+0xb0` texture; precise legacy-only predicate not separately named in exe.
- `UseSpecMapPolicy.Decide(hasSpecularMap && !chameleon)` ≈ outcome but **not** the CPU predicate.

### ForgeLens changes (**applied** `ddc8f88`, viewer `4be1d4e`)
1. `UseSpecMapPolicy.Decide` is on when usage has MSCRGHAO (bit 21 → `0x2000`) or MSCG (bit 18 → `0x40`). Evidence string cites `FUN_14217da70`.
2. Sidecar `materials.json` `useSpecMap` + `import_sm2_materials.py` + `GlPreview` use that decision.
3. Chameleon stays off when those bits are absent (no separate chameleon branch).
4. Still open: re-export/reimport existing UE material instances (PC). The "do not apply yet" line was stale after `ddc8f88`.

### Evidence
- Decomp `FUN_14217da70.c` L244–263; siblings `FUN_14217e330.c`, `FUN_14217ebf0.c`
- Usage table dump (27 names, MSCRGHAO=21)
- REA Evidence IDs: **pending** (MCP analyze timeout)

---

## (B) Hub / level light intensity & falloff

### Label: **PROVEN** (CPU pack + P0.w + constants + desc→render). UE FIT K=10 is **not** engine math.

### Observations

| Function | Role |
|----------|------|
| `FUN_14224f010` | `P0.w` from type/flags/`attenCurveParam` |
| `FUN_14224ecf0` | packs 16 floats `COMMON_DYN_LIGHT_PARAMS` |
| `FUN_141384090` | desc → render-light (`attenCurveParam`→`+0x54`, flag bits→`+0x6d`) |

**P0.w:** directional→0; constAtten→`8e22`; invSq→`0.0025`; else `a>0 ? 299*a⁴+1 : pow(256,a)`.

**Colour:** `(+0x50)*(+0x70)*(+0x4c)` [×adaptScale if bit0] [×**400** if invSq].

**P1.w:** `1/(r*soft)⁴` (or negated under const atten).

**Spot:** type 1; `cos(angle*π/360)`; cone scale/offset in P3/P2.

**Constants (PE):** `0x1437e9930=0.0025`, `0x1437e9990=8e22`, `0x1437e9974=299`, `0x14355c10c=400`, `0x143560154=π/360`, `0x1436dba7c=256`.

Shader window (prior): `sat(1-(d²·P1.w))²` — not UE candela InverseSquare.

### ForgeLens changes (**applied** `e9ad274` / `ddc8f88`)
1. `LevelLightUe.UeIntensity` = I × colorScale (×400 only if `useInverseSquaredFalloff`). **FIT K=10 removed.** Do not restore it.
2. Spots keep cones (`SpotConeDegrees`). **Direction is still missing** from the lights TSV (default spot forward).
3. `attenCurveParam` → P0.w is in `LevelLightUe.P0w` / `CaptureLightUnits`.
4. Adaptive factor remains `clamp(avgLum, 0.05, 50)` on the capture path. UE candela/exponent bridge stays **INFERRED**.

### Evidence
- `/workspace/sm2_re/decomp/FUN_14224ecf0.c`, `FUN_14224f010.c`, `FUN_141384090.c`
- REA Evidence IDs: **pending**

---

## Angles exhausted (A)
1. String xrefs OBJ_MTLBLEND / CB_OBJ — layout only  
2. Store-to-`+0x150` scan — ctor noise / AABB false positives  
3. Usage serializer `FUN_14267baf0` — produces mask, not LAYER word  
4. Bitfield serdes `FUN_142671c70` — reflection, not CB pack  
5. Immediate co-occurrence / proximity test `0x200000`↔`or 0x2000` — empty outside permutation cluster  
6. **Hit:** float bitcast packer `FUN_14217da70` `param_1[0x3c]` ← usage bit 21  

## REA MCP note
`open_binary` succeeds; `analyze_function` / `search_strings` / `read_bytes` hit MCP -32001 during Ghidra cold analyze. Investigation used retained project headless `-noanalysis` (same PE). Full re-import started under `/workspace/sm2_re/ghidra_proj` for later REA Evidence re-cite.
