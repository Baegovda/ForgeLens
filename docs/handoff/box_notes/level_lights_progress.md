# level-lights progress (2026-10-29 KST)
- Extracted scenes/hub_battle_barge.pak (read-only) → PC Raw\level_lights\hub_battle_barge\; parsers cdlist.py / classlist.py; merged dump hub_battle_barge_lights.tsv (1,865 single-light objects, 29 multi-light containers skipped).
- Pairing: armory 128197 eid 9799 L0..L3 = customization spot_light_bot_set1 / bot_3 / kick_2 / bot_5 (0 m); armory_pairing.tsv exact (P0.w 300/300/1/300, P1.w, P2.w, P3.x, colour (c/255)^2.2·I).
- Code: FUN_14224f010 (P0.w) + FUN_14224ecf0 (COMMON_DYN_LIGHT_PARAMS packer) PROVEN (f299.c, f299_callers.c).
- Implemented in bench shaders, VfxUeExport, VfxFrameExport JSON, vfxframe_scene.py, ue_vfx_frame.py. Tests 343/343, py 18/18.
- Commit d55bbe3 (box) → pushed as d843b51 (b6cf3a1..d843b51 ff, push.ps1).
- Deploy: ForgeLens.exe sha256 07210b04…42ad; previous (1b409a47…5319) → ForgeLens.exe.bak16.
