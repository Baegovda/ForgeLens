# rea-batch 7 — where ribbon trail points go into RB_TRAIL_POINTS

Source: PC REA daemon (rea-agents 6.8.0 + Ghidra 12.1.4), 2026-10-29 KST. Raw outputs are in `raw/`.

## Answer: the upload site is found. The per-point colour baker is one hop further up and still open.

1. **FUN_14184c8d0 is the RB_TRAIL_POINTS uploader. (PROVEN)**
   - It holds a pool of GPU buffers, created by `FUN_1420a00d0(dev,2,2,0,0x6400,0x50,…)` and named `"vidSDR_RES_BUFFER, Trail points data %d"`. That is 0x6400 = 25600 elements with a 0x50 = 80-byte stride.
   - It maps the buffer (vtbl+0x28), runs `memcpy(map + off*0x50, pts, n*0x50)`, then unmaps (vtbl+0x30).
   - It binds the buffer with `FUN_142094dd0(ctx, &DAT_14363c198 {7,2}, buf)`. It also fills two const streams, "Trail chunk offset" (slot {10,2}) and "Trail tex atlas" (slot {11,2}, values 1/atlasW, 1/atlasH, W, frames−1), then issues the draw.
   - When the 0x6400 buffer fills up, it moves on to the next buffer.
   - FUN_14184ddd0 is a helper that gets or creates a buffer from the same pool.
2. **The bound slot is RB_TRAIL_POINTS. (INFERRED, high)** The buffer is named "Trail points", and the only element type with an 80-byte stride is trlPOINT. The {7,2} slot id was not mapped by hand against the FUN_1421ca290 resource table. That table is reflection only: every xref to its globals (0x144cc6bd0, 0x145025e00, 0x145024c30) is inside FUN_1421ca290. This is why earlier batches found no writer by following the descriptor.
3. **Callers that stage the points. (PROVEN)**
   - FUN_14184cd20 (callers FUN_1418bcdf0, FUN_1418bffe0, FUN_14190a7e0) and FUN_14184d3c0 (caller FUN_14190d980) walk a trail list `param_7`. That list holds 0x160-byte trail records in a ring (`(head+i)%cap`).
   - Each record has a point ring: points ptr at +0x10, count at +8, head at +0xc, capacity at +0x18, 80-byte trlPOINT.
   - The callers copy the points into a stack batch (`afStack_a168`, at most 0x202 points per flush). They optionally apply a 3x4 transform to the position (`param_9`), patch an age term (`(now − pt+0x2c)·1.5e-5`), then call FUN_14184c8d0.
   - Colour bytes are copied **unchanged** here. The colour is baked before this point, into the record's point ring.
   - FUN_14190a7e0 passes `param_1 + 200` (0xc8) as the trail list.
4. **Lightning trails take a separate path. (PROVEN)** The layout for trlPOINT_DATA (pkPosXY / pkPosZLen / pkAgeIntensity / scale) and LIGHTNING_TRAIL_POINTS is registered in FUN_1421b4610. It does not apply to ribbons.

## Still open, next step
The per-point colour baker is the code that appends or updates 80-byte entries in the trail record's ring (record+0x10, `%cap` at +0x18).
- Look for writers of 0x160-byte records in the list at owner+0xc8, by xrefs and decompiling the vtables of FUN_14190a7e0, FUN_1418bcdf0 and FUN_14190d980 (the update side of the same render-element classes).
- Alternatively, use RenderDoc on only the draws that bind "Trail points data".

## Confidence
- Upload function FUN_14184c8d0: PROVEN (~95%).
- {7,2} = RB_TRAIL_POINTS: INFERRED (~85%).
- Colour is not modified at upload or staging: PROVEN for these 3 functions (~90%).
- Colour baker location: open (record ring, INFERRED).

## Follow-up (2026-10-29 ~23:55 KST): the per-point colour baker is found
Source vtables, PROVEN: the class string is "SfxTrail…". 0x143648880.. holds update 0x141909810 and draw 0x14190a7e0. 0x143648be0.. holds 0x14190d560 and draw 0x14190d980. See `raw/vt.txt`.

- **FUN_141909810 is the trail update. (PROVEN)** It walks the record ring at owner+0xd0 (`param_1[0x1a]`), with head at +0xcc and capacity at `param_1[0x1b]`, stride 0x160. For each live point it calls
  `FUN_141847640(pt, trailDesc, dt*scale, owner+0x144, rec+0x154, headPt, tailPt)`.
  New or empty points get the sentinel `pt+0x10 = 0xff640000` when pt+0x10 ≤ 1e-6.
- **FUN_141847640 is the per-point updater. (PROVEN)** The point fields it uses:
  - age: half2 at pt+0x34, normalised to t;
  - base colour: BGRA bytes at pt+0x14;
  - position along the trail: pt+0x28;
  - tex anim: pt+0x24;
  - inputs at pt+0x1c and pt+0x4c.
  It writes **`pt+0x10 = FUN_141ab9b60(trailDesc+0x90 /*visuals*/, baseRGBA(pt+0x14), t, …)`**. It also writes the scale/width at pt+0xc (scale curve at desc+0x2d0 with 64 taps, or `pt+0x18 * desc+0x2cc`), the texture frame at pt+0x24 (FUN_141ab9e90 on desc+0xb0), and the position at pt+0..8 (physics, FUN_141ab9f20 on desc+0x3b0).
- **FUN_141ab9b60 is the colour baker. (PROVEN)** Its first argument is the visuals block, whose layout is from the serializer (FUN_141836a80 / FUN_14183b3a0).
  1. It starts from rgba = the point's base colour bytes (0..255).
  2. If visuals+0x10, the colour curve/gradient, is present: rgba *= lerp(LUT[i], LUT[i+1]) / 255, where idx = t * curve+0x400 − 0.5, the LUT has 256 entries, and LUT entries are read by FUN_1427cd960.
  3. Fade comes from fadeParams at visuals+0x1d0..0x1dc:
     - front = +0x1d0, abs flag at +0x1d4;
     - back = +0x1d8, abs flag at +0x1dc;
     - when a flag is false, the value is multiplied by the trail length (tailPos − headPos);
     - f = sat((tail − pos)/front) * sat((pos − head)/back).
  4. If blendType (visuals+0x78) == 1 (additive), rgb *= f. Otherwise a *= f.
  5. If isAlphaInherited (visuals+0x54) is set, a = inherited alpha.
  6. Then a *= the final multiplier argument (alpha mult). Each channel is clamped to 0..255 and packed as A<<24 | R<<16 | G<<8 | B.
- **Consequence. (PROVEN)** Ribbon trails apply fadeParams with the same front/back formula as the lightning VS. The CPU applies it before the upload. `SM2_VFX_TRAIL_FADE` can become the default, and its HEURISTIC label can be removed. One caveat: the head and tail positions come from the ring (headPt/tailPt +0x28).
- **Argument mapping caveat. (INFERRED)** Ghidra's signature for FUN_141ab9b60 has 8 parameters, but 9 are passed. Which argument is head, which is tail, and which is the alpha multiplier (owner+0x144 vs pt+0x1c) is not fully pinned down.
- **Still open:** the emit side that writes the spawn colour bytes at pt+0x14 (probably visuals color +0x10 / colorIntensity). Find it via the writer of rec+0x8 / +0xc in FUN_1418bbd20 / FUN_1418bba30 or FUN_14190ab20, which are called from FUN_141909810.
- **Slot {7,2} = RB_TRAIL_POINTS:** still INFERRED (~85%). I didn't map the binding table by hand this round.

## Follow-up 2 (2026-10-29 ~23:58 KST)
Raw files are all in `raw/` (from b7_raw2.zip): e_*.c, v_*.c, u_*.c, w_*.c, vt.txt, vt2.txt.

### (1) Spawn colour pt+0x14 — PROVEN
- **FUN_1418bd8d0(this, slot, pos, p4, p5) is the add-point function.** It is the last vtable slot in all three SfxTrail vtables (0x1436487b8, 0x143648930, 0x143648c70), and FUN_1418bbd20 and FUN_141909810 call it through `vtbl+0x170`. It builds an 80-byte point on the stack, starting at `uStack_198`:
  - pt+0..8 = pos;
  - **pt+0x14 = p5 ? (a<<24 | r<<16 | g<<8 | b) from the floats at p5+0x40 (r), +0x44 (g), +0x48 (b), +0x4c (a) : 0xFFFFFFFF**;
  - pt+0x18 = p4 ? p4[0] : 1.0;
  - pt+0x1c = p4 ? p4[2] : 255.0.
- FUN_1418bbd20 and FUN_141909810 always pass p4 = p5 = NULL. FUN_1418bbd20 subdivides with FUN_1418bbb50 (max segment length from desc+0x284), then adds the points.
  So **for the normal emitter path, the spawn colour is white 0xFFFFFFFF, the width factor is 1, and the inherited alpha is 255. (PROVEN for these two callers)**
- A non-null p5 is a colour source supplied by an external caller: four floats at +0x40..+0x4c, on a 0..255 scale. Which caller passes it is not traced. INFERRED: it is an attach or inherit path.
- So **every visible colour comes from the visuals gradient** in FUN_141ab9b60 (visuals+0x10 LUT, sampled over normalised age). The desc colour / colorIntensity are applied later. From rea-batch 2, PS_REG_TRAIL_EMISSIVE.x = colorIntensityMultiplier, applied on the GPU. That they are not in the CPU bake is INFERRED: no CPU read of them was seen.

### (2) FUN_141847640 → FUN_141ab9b60 argument mapping
`FUN_141ab9b60(visuals = desc+0x90, &rgba(pt+0x14), t_age, pos = pt+0x28, oldestPos = ring[head]+0x28, newestPos = ring[head+count-1]+0x28, inhAlpha = pt+0x1c, alphaMult = owner+0x144, [9th = pt+0x38 half, unused by the body])`.
- head/tail: the caller passes `iVar4*0x50+base` (the head index, the oldest point) and `(head+count-1)%cap` (the newest point). Both are PROVEN from the caller.
- In the body: len = newest − oldest. front = F or F·len, and k_front = sat((newest − pos)/front), the fade near the newest point, at the emitter end. k_back = sat((pos − oldest)/M'), the fade near the oldest end. **PROVEN**
- **The alpha multiplier is owner+0x144. pt+0x1c is the inherited alpha, which replaces a only when visuals+0x54 (isAlphaInherited) is set.** Placement in arguments 7 and 8 is PROVEN by the order. The 9th argument is a Ghidra over-count (INFERRED).
- The gradient index uses **age** t (pt+0x34, half2), scaled by curve+0x400, into a 256-entry LUT. It is not indexed by position along the trail. **PROVEN**

### (3) Slot {7,2}
- FUN_142094dd0(ctx, {id, set}) only clears the dirty bit `ctx+0xfc + (set*0x53 + id>>5)*4`, then calls vtbl+0x2c0. So {7,2} is (resource id 7, set 2).
- The FUN_1421ca290 table entries (stride 0x38) store other numbers (RB_TRAIL_POINTS entry: 0xb, 0x19, 2, 3). They do not map to id 7 directly.
- **It stays INFERRED (~85%)**, supported by the buffer name "Trail points data", the 0x50 stride, and the fact that it is the only buffer bound in the draw. To prove it, find the code that consumes the table, i.e. how ids are assigned at shader-reflection time.
