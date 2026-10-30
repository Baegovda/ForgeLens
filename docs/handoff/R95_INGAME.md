# R95 in-game vs Unreal fidelity (Titus)

Route that works (SM2 PATCH 15.2, KR client, user mod "TESTING MENU 4.4" -> "modified files" notice, accept):
1. Launch via Steam, at title focus window (AttachThreadInput + SetForegroundWindow) and Alt+Enter -> windowed 1938x1127 at 0,0 on primary.
2. Enter (press button), Enter (accept notice), click "이어하기" (Continue). Never "새 게임".
3. Hub: input is ignored until a real click inside the window (title bar + client). Then WASD (scancode) / mouse look work.
4. Terminal just left of the spawn point: hold E -> war map. Click 캠페인 tab, hold Enter on 계속, accept "게임 모드 전환" -> campaign barge with Titus.
5. Esc -> 사진 모드 (photo mode). RMB drag rotates, WASD/Space move, F hides UI. Camera moves are not repeatable (overshoot).
All input via tools/input_block.ps1 (hook). Scripts: r95_ingame/g.ps1, seq.ps1.

Result (Titus):
- In-game photo: ingame_titus_photomode_01.jpg (face/chest, right edge), _02 (back). Chapel lighting is green/teal.
- UE r92 floor-map capture had near-white armour (sat 0.03-0.04, val ~0.9).
- UE base-colour (unlit, SCS_BASE_COLOR) capture ue_titus_unlit.jpg: armour is ultramarine blue, chest rgb (10,14,34) hue 229 sat 0.72.
  Source diffuse PNGs: ch_titus_body_02 mean (69,77,100), ch_titus_legs_02_a (68,72,91).
- Conclusion: the near-white is lighting/exposure of the r92 floor test map, not the material/texture. No material change made.
- Not verified: same pose/camera/lighting lit comparison (ue_titus_lit.jpg is dark: default level light only); Calgar/Sorcerer/Hormagaunt/Vortex in-game.
