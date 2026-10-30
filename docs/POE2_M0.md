# POE2 M0 - read-only probe of the Kakao PoE2 Content.ggpk (2026-10-30 ~23:30-23:50 KST)

Scope: **PoE2 only** (owner t215u). Game folder `D:\Daum Games\Path of Exile2` opened READ-ONLY
(`FileAccess.Read`, `FileShare.Read`, closed after each run). Nothing written there. Unreal editor not used.

## Tooling
- `ForgeLensPoe/ForgeLens.Ggpk3/` = private verbatim copy of LibGGPK3 (AGPL-3.0, aianlinb, commit e5872fd). Remove before any distribution (README there).
- `ForgeLensPoe/ForgeLens.Poe.Probe/` = console tool: `list <ggpk> <outdir>` | `find <ggpk> <regex> [max]` | `extract <ggpk> <outdir> <path>...`.
  Build: `dotnet build -c Release` (SDK 8.0.426 already installed, net8.0). Needs `oo2core.dll` next to the exe (not committed).
- Oodle (PROVEN): no oo2core*.dll in the PoE2 Kakao folder (Oodle is presumably linked into the exe - INFERRED). The Epic-signed
  `oo2core_9_win64.dll` from `C:\Program Files\Epic Games\UE_5.4\Engine\Binaries\DotNET\AutomationTool` works (index + bundles decompress).

## Kakao build layout (PROVEN)
- Single `Content.ggpk` 159,936,662,738 B, GGPK version **3**, 71,046 raw GGPK files: `Bundles2/` (_.index.bin + Content/Folders/Streaming/Tiny.* `.bundle.bin`) + `Art/Videos` (331), `Art/ggdh` (1).
  So it is the standalone-client layout: bundles **inside** the GGPK -> `LibBundledGGPK3.BundledGGPK`. No loose Bundles2 folder on disk.
- Bundle index: 63,376 bundles, **4,266,875** files (5 with unresolved paths). Paths are lower-case in the index.
- Timings (Stopwatch in Probe, warm disk cache, 3 runs): GGPK + index open 0.90-1.06 s, path parse 3.85-3.93 s, total 4.76-4.99 s; full list+stats 16.8 s.
- Top level (files): art 1,019,254 / metadata 329,135 / shadercache* ~573k each (5 APIs) / audio 50,553 / data 3,568 / minimap 2,800 / cachedhlslshaders 3,245 / shaders 197.
  Full 2-level tree: `docs/handoff/poe2_m0/poe2_tree2.txt`; per-extension counts+bytes: `poe2_ext.txt`; full list (297 MB, not committed) PC `Desktop\SM2_Work\PoE2\m0\poe2_filelist.txt`, head 300 lines committed.

## Formats present (counts, PROVEN from the index)
.tgm 220,767 / .mat 203,523 / .dds 150,582 / .ao 73,549 / .sm 31,764 / .smd 31,587 / .fmt 26,974 / .tdt 23,252 / .ast 17,916 / .amd 17,595 / .tgt 15,635 / .arm 15,572 / .fxgraph 12,381 / .tmd 9,706 / .datc64 2,976 / .pet 37,147 / .epk 8,481 / .trl 5,865.
`.aoc` 0 and `.dat64` 0 (PoE2 uses `.datc64`). 2,861,369 files have no extension (mostly shader caches - INFERRED from tree).

## M0 sample extraction (PC `Desktop\SM2_Work\PoE2\m0\x\...`)
Mesh: `art/models/items/weapons/onehandweapons/claws/basetypes/crudeclawdrop.sm` (556 B) and what it references:
- `.sm` = UTF-16LE text, `version 6`: `SkinnedMeshData "...CrudeClawDrop_3a652060.smd"`, `Materials 1` -> `...Textures/CrudeClawc.mat`, `BoundingBox`, `BoneGroups 0`.
- `.smd` (32,687 B) binary; first bytes `03 04 01 00 1C 00 00 00` then 6 floats = the same bounding box as the .sm (-8.332 -15.257 -3.301 ...) (PROVEN by value match). Rest NOT parsed.
- `.mat` = UTF-16LE **JSON** `version 4`: 2 textures (`CrudeClaw_Colour_BC7.dds`, `CrudeClaw_Normal_BC7.dds`), graph `Metadata/Materials/SpecGlossSpecMaskOpaqueBN.fxgraph` (extracted, 52,712 B) with params AlbedoSpecMask_TEX (sRGB), NormalGlossAO_TEX (linear), UsesRoughness.
- `.fmt` (`crudeclaw.fmt`, fixed mesh) extracted, not parsed.
- Textures: plain DDS (DX10 header, DXGI 98 = BC7_UNORM, 1024x1024, 11 mips, 1,398,276 B) - not brotli-wrapped for these two (some PoE .dds are brotli/soft links per poe_data_tools - not seen here).
- PNG: Pillow 10.4 decodes BC7 -> `m0\crudeclaw_colour_bc7.png`, `crudeclaw_normal_bc7.png` (visually checked: wood/metal albedo; tangent normal map, mean B 216). Normal alpha mean 203 = gloss/AO packing (INFERRED from the param name NormalGlossAO).

## Not verified
- .smd/.fmt/.ast/.ao internals; no mesh converted to geometry yet. Skinned meshes with bones (BoneGroups 0 here: a drop item).
- Behaviour while the game is running (FileShare.Read would fail if the client has the file open for write - not tested).
- Timings are warm-cache; cold first open not measured separately.

## M1 plan (reuse before writing parsers)
1. `adamthedash/poe_data_tools` (Rust, crate + CLI; GGPK/Steam/CDN; PoE2 parsers for smd/fmt/tgm/ast/ao/mat/... reported ~100 % success on PoE2 4.5.x): build its CLI on the PC, run against our ggpk (or against the M0-extracted files) and use its smd/ast/fmt decoders as the reference; glTF export is on its TODO, so write a small glTF writer on top.
2. Cross-check with `annalithic/poeformats`, zao's `poe-rs` / gist fmt->glTF, Noesis `fmt_PoE_sm.py` (old PoE1 layout, for hints only).
3. `.datc64` tables via `poe-tool-dev/dat-schema` (PoE2 schema) when item/monster -> model mapping is needed.
4. Pipeline: sm/smd(+ast skeleton, ao/amd animation) -> glTF (skin+anim), mat JSON -> MI parameters, dds -> png (Pillow) or direct DDS import.
5. Unreal import (later step, editor lock rules apply) under `/Game/ForgeLens/POE/<path>`; first target = this crude claw (static), then one skinned monster with its .ast + one animation.