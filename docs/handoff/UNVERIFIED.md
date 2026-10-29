# UNVERIFIED — open INFERRED / HEURISTIC register (2026-10-30 KST)
Close an item = new evidence → change label here + in the source doc + `re_cache_build.py`. Plan column = cheapest next check.

| # | Item | Label | Source | Verification plan |
|---|------|-------|--------|-------------------|
| U1 | slot {7,2} = RB_TRAIL_POINTS | INFERRED ~85% | R36, RE_CACHE/notes/trail_points.md | RenderDoc: draw binding "Trail points data" → shader resource name |
| U2 | trail owner alpha `owner+0x144` (code uses 1) | HEURISTIC | R36 | xrefs writers of +0x144 in SfxTrail owner (gs.py xrefs / summary) |
| U3 | trail gradient spans age 0..1 (curve+0x400) | INFERRED | R36 | decompile FUN_1427cd960 caller arg; visual check V1 |
| U4 | spawn colour non-null p5 caller | open | R36 | xrefs FUN_1418bd8d0 vtbl slot callers |
| U5 | adaptive default for lights without key (`DefaultAdaptive=true`) | HEURISTIC | R37 | trace light desc deserializer default of +0x6d bit0 |
| U6 | bench exposure 1/250 | HEURISTIC | R37 | RenderDoc COMMON_HDR_AVERAGE in a bench-like scene |
| U7 | LIGHTS_BUFFER (FUN_141de8140, 1.0) consumer | INFERRED | R37 | xrefs of the buffer name → shader |
| U8 | nmb NodeDef/Expression layout, Switch selection 1=floor | INFERRED | R38, FIRE_CLIP.md | visual check V3 per weapon; decode ops 95/98/c4 |
| U9 | fire clips: unassigned wpnType → default 0 (bolt-rifle family aim, 1.41 aim), heavy aim (floor into rifle branch), 1.5/1.7 undecoded ops, 1.4 SubtractiveBlend, 1.51 SimpleTypeConvert | HEURISTIC (R40 `nmb_verify.py`: table = network for all 124 rows, 64 rows need an assumption; settled: no table/network disagreement, labels now per row in FIRE_CLIP_VERIFY.tsv) | R38, R40 | still open: Expression default value, ops 95/98/c4, index the 2 node records, heavy aim state (lead: `LOCO|heavy_bolter_turret`, `heavy_flammer_shoot`), which blind variant state is active; visual V3 |
| U10 | split-skeleton rule covers all assets (1 case found) | INFERRED | R35 | rerun `ue_fix_split_skeletons.py scan` after each bulk import |
| U11 | START_HERE "Remaining HEURISTICs" list (reload/shoot by name, visibility→scale 0, spring-damper, trail emission/spacing, melee_dark skip, layer0..2, isMetallic, customUV, Champion names, Shoot→muzzle, category rules, VatDecode age/life, firearm main, overlay preview) | HEURISTIC | START_HERE.md, STATUS "Heuristics" | one REA question each; check RE_CACHE first (`recall.py`) |
| U12 | EngineWire / UEMCP live in GUI editor | PROVEN 10/30 05:00–05:07 KST (R47): GUI `UE_VFX_57` pid 33536, bridge up 14 s after launch (log 05:00:24), smoke `execute_python` → UE 5.7.4, /Game/SM2_Work 982 assets: EngineWire 1.76 s (warm; 1st call 11.0 s), UEMCP 3.81 s; rules_check prints "editor + port3000 open -> EngineWire" | R47 | closed |
