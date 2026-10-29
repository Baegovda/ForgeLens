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
| U9 | fire clips: bolt-rifle aim (sht16), heavy weapons aim, unassigned wpnType → default 0 | HEURISTIC | R38 | find the state heavy weapons use for aim in primaris.nmb |
| U10 | split-skeleton rule covers all assets (1 case found) | INFERRED | R35 | rerun `ue_fix_split_skeletons.py scan` after each bulk import |
| U11 | START_HERE "Remaining HEURISTICs" list (reload/shoot by name, visibility→scale 0, spring-damper, trail emission/spacing, melee_dark skip, layer0..2, isMetallic, customUV, Champion names, Shoot→muzzle, category rules, VatDecode age/life, firearm main, overlay preview) | HEURISTIC | START_HERE.md, STATUS "Heuristics" | one REA question each; check RE_CACHE first (`recall.py`) |
