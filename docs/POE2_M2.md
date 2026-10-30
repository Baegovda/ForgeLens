# POE2 M2 - viewer UI, real skill links, particles, more meshes, Unreal import

CORE CONCEPT: same SM2 feature set, only the reader changes. Game never launched or controlled.

## Step A (2026-10-31 ~04:55-05:05 KST) - real skill -> effect link from tables - DONE
`src/LibSaber.SM2.Assets/Games/Poe2/Poe2Skills.cs` (used by `--poe2skills <prepared> <out.tsv>`), replaces the M1 name match.
Chain (PROVEN by row values for Firebolt):
- ActiveSkills `firebolt` <- SkillArtVariations `firebolt` (.ActiveSkill = firebolt, vaal_fireball, playtest_spell)
  - .ProjectileArt `fireball` -> ProjectilesArtVariations (Id fireball, Variant 0) -> Projectiles `Metadata/Projectiles/Fireball` -> AOFiles `.../fire_fireball/ao/rig.ao`, loop `projectile`, impact `impact`
  - .AnimatedArt `fireball_standard`, `fireball_large` -> MiscAnimatedArtVariations (Variant 0) -> MiscAnimated `FireballExplosion`, `FireballExplosionLarge` -> AOFile
- ActiveSkills.GrantedEffect `FireballPlayer` -> GrantedEffects.Animation `GreaterFireball` (Animation table: Mainhand `SpellProjectile`) = character cast animation id.
- Result: 1,147 skills, 629 with table-linked effects (M1 name match: 89), 184 with a projectile, 1,666 effect rows. `docs/handoff/poe2_m2/skills.tsv`.
- Not linked yet: skills whose art is in EffectPacksArt / BeamArt / ObjectArt / BuffVisuals / ground effects (e.g. Ice Nova has none of ProjectileArt/AnimatedArt). Only Variant 0 (Default) art is used.
- Tables needed in the prepared folder (`dump-tables --mode json`): activeskills, grantedeffects, skillartvariations, projectilesartvariations, projectiles, miscanimatedartvariations, miscanimated.

## Step B (~05:05-05:15 KST) - orientation fix, LODs, more characters, independent glTF/FBX check - DONE
- Orientation (INFERRED from bone names, no in-game check): BasicSkeleton toes (L_ankle->L_foot->aux_L_foot) point to PoE -Y and L_* bones are at +X
  => PoE space is left-handed (D3D style). New map (x,y,z) -> (x,-z,-y)*0.01 (PoE -Y -> ForgeLens +Z front, -Z -> +Y up, L stays left) is a
  reflection, so triangle winding is flipped. M1 renders showed the back of the skeleton; M2 default view shows the face (mon_sheet.png).
- LODs: smd/fmt `lods[1..]` now read into `ExportedMesh.Levels` (hidden by default, like SM2). All 6 tested meshes have 1 LOD, so the LOD path is UNTESTED.
- `?` as clip name picks the first idle (else run, else first) animation. `tools/poe2/mesh_prep.ps1` also pulls the .mat files an .sm names and every .dds they use.
- Rendered in the viewer GL path (bind + clip t=0.5 s, `docs/handoff/poe2_m2/mon_sheet.png`):
  BasicSkeleton1 attack_01 (49 bones) / AnglerFishParasite idle_01 (34 bones, 26 clips) / BellCrow bell_enraged_idle_01 (86 bones, 73 clips, 57k tris, 4 parts) /
  BansheeRemake idle_01 (69 bones) / NettleAnt idle_01 (42 bones). Textured and animating, except BellCrow renders almost black (cause NOT found:
  textures decode to normal colours, mean RGB 64,59,53; material-per-shape order or lighting suspected).
- Independent check in Blender 5.0 (headless, `tools/poe2/bl_check.py`): basicskeleton1.fbx imports 2480 verts / 3625 faces / 49 bones / action attack_01 frames 1-46,
  height 1.903 m; basicskeleton1.gltf same mesh + 49 bones + action attack_01; crudeclaw fbx/gltf 796 verts / 651 faces. glTF carries no images (writer limitation, same as SM2).
  (These exports were made before the orientation fix.)

## Step C - viewer UI (PoE2 source)
- New: Games/Poe2/Poe2Source.cs (wraps poe_data_tools: cached file list, Prepare() follows referenced files, skill tables), Poe2Window.cs (GGPK path + Open GGPK, Files tab with search, Skills tab with effect list and Animation combo; English strings only), MainWindow.Poe2.cs (ShowPoe2 reuses the SM2 Model display path). "PoE2" button added to the viewport toolbar.
- Tools are looked up in <exe>\poe2tools; cache in %LOCALAPPDATA%\ForgeLens\poe2cache.
- Test hooks: FL_OPEN_POE2=1, FL_POE2_AUTOOPEN=1, FL_POE2_GGPK, FL_POE2_SKILL=<id>, FL_POE2_SELECT=<path>, FL_POE2_AUTOPLAY=1.
- Verified on the PC (no clicks, env hooks only): Open GGPK -> Skills -> fireball -> 4 effects listed; greater.ao loaded into the main viewport with 4 emitter parts (254 verts, 13 bones, 1.00 s clip), about 21 s including extraction. Screenshots: handoff/poe2_m2/ui_poe2_window.png, ui_main_fireball.png.
- Not verified: manual clicking through the Files tab; the effect still renders as untextured flat meshes (particle sim is step D).
- Side finding: mp_halfsphere_LOD1_FF.fmt reports 2 LODs (380/190 tris), the first multi-LOD asset seen.

## Step D - particle simulation (PoE2 effects -> SM2 EffectDefinition)
- New: Games/Poe2/Poe2Particles.cs maps an effect .ao to the shared SM2 EffectDefinition (CORE CONCEPT: the SM2 fixed-step
  simulation, sprite/trail builders, viewer VFX view and --vfxbench are reused unchanged).
  - .pet GPU emitters: duration, particle life min/max, particles_per_second / burst+particles_count, is_continuous, particle_mesh.
  - update .mat graphs: SphereEmitter Radius, EmitVelocityRandom / EmitVelocityAlong (Velocity, Cone Size), Gravity, Air,
    AirResistanceMultiplier, CurlNoise_Wind, EmitScale.
  - render .mat graphs: Colour (RGBA over life), Scale / ScaleXYZ, Vibrance / COLORV / Channel_LookUpColor / SIM_Smoke2 tint,
    Alpha_Multiply, Force* blend, first albedo-like texture, flipbook grid from the _NxM_ file name (random pick or over life),
    LuminanceAlpha (texture key suffix #lum).
  - .pet CPU emitters (key_values, 7-point graphs over life: rate, life, scale, colour/opacity, velocity, acceleration, frames).
  - .trl trails: trail_length, max_segment_length, segment_duration, scale curve, material texture/tint.
- Older .fmt (section V8, vertex_buffer/index_buffer) now loads: cleaveGeo01.fmt was never missing, it failed on the V8 layout.
- Bench: `--vfxbench poe2:<prepared> - - <out> <effect.ao>@sec`; prepare with `--poe2prep <ggpk> <out> <tools> <ao...>`.
- Viewer: selecting an .ao effect in the PoE2 window plays it in the SM2 particle view (fallback / FL_POE2_RIGVIEW=1: M1 rig view).
- Textures: no-alpha particle textures get alpha = max(RGB); SIM_* smoke maps use density as alpha with the material tint.
- HEURISTIC (no in-game comparison possible, game not launched): Scale unit 0.05 m (FL_POE2_FX_SIZE), colour intensity 60
  (FL_POE2_FX_INTENSITY, CPU x0.35), trail width 0.05 m per unit, gravity -4 m/s2, Air friction 1, curl noise as tangential
  speed, mesh particles drawn as glow sprites, world-locked (face_lock None/X/Y/Z) and tileable-mask sprites hidden,
  distortion passes hidden, projectile fly-by 8 m/s over 4 m.
- Verified: bench sheets for Firebolt (GreaterFireball greater.ao + impact_greater.ao), crossbow stormblast explosion, arc impact,
  wind spear projectile, spark projectile (handoff/poe2_m2/fx_sheets.png, fx_vfxbench.txt): all deterministic (replay identical).
  Viewer: Firebolt projectile (rig.ao) plays with heads + trails after real clicks (ui_firebolt_particles.png).
- Not verified / weak: spark draws nothing visible (its lightning trail texture/mask), arc impact is overexposed, smoke shading is
  a flat tint, no in-game reference; event start times and emitter intervals are ignored.

## Step C+ - real clicks in the PoE2 window
- tools/poe2/poe2_clicks.ps1 run under tools/input_block.ps1 (hook mode, 30 s, 0 physical events dropped; foreground was
  checked first, game not running): Files tab -> search mp_halfsphere_lod1_ff -> first result opens in the main viewport
  (230 verts, LODs 2 = 380/190 tris, LOD combo lists both) -> Skills -> firebolt -> projectile effect plays as particles.
  Screenshots: ui_files_tab_click.png, ui_firebolt_particles.png; log ui_clicks.txt.
- Fix: two selection events raced (generation counter) and the loser fell back to the rig view; superseded requests now skip
  the fallback.
