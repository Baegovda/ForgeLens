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

## Step E (partial)

- Multi-LOD: mp_halfsphere_lod1_ff opens with 2 LODs (380/190 tris) via a real Files-tab click.
- cleaveGeo01.fmt: present; it uses the V8 .fmt layout (vertex_buffer/index_buffer), now supported by Poe2Reader.
- BellCrow dark: albedo textures are dark in the data (mean RGB ~43-64). The body material applies TWEAK_Albedo Albedo_MULT1=1.6, which was ignored. It is now read into MeshPart.AlbedoTint (used by the UE/material export) and baked into the viewer preview albedo as an sRGB factor of 1.6^(1/2.2). It builds; the brighter result has not been checked visually yet.


### Step E.2: BellCrow render, glTF images

- BellCrow rendered with the TWEAK_Albedo fix (handoff/poe2_m2/bellcrow_tint.png). It is still very dark. NettleAnt renders correctly in the same renderer, so the renderer is fine; the crow's textures are dark (colour mean RGB 43-64, SSS mean 27/21/18). In game the SSSAdd and SpecGloss terms brighten it, and the preview does not model them. Judgement: the colour is correct; it looks darker than in game because SSS and specular are not shaded. The tint helper is Poe2Bench.TintAlbedo, shared by glbench and the viewer.
- --poe2export now writes <name>.glb with embedded PNGs. Every visible part gets its own primitive with a baseColor/normal material, and baseColorFactor comes from AlbedoTint (clamped to 1). Khronos gltf-validator 0 errors on bellcrow.glb (3 materials, 6 images, 4 primitives, attack_01 anim, 87 nodes). Before: 20640 ACCESSOR_WEIGHTS_NON_NORMALIZED; GltfWriter now normalises weights and zeroes unused joints. This is a shared writer change and also applies to SM2 exports.


### Step E.3: six more rigged monsters (prepared folder m4 via tools/poe2/mesh_prep.ps1)

handoff/poe2_m2/monsters6_sheet.png shows bind pose and the first clip at t=0.5 (glbench poe2:m4). All six have a GLB export with embedded textures.

| model | bones | clips | tris | result |
|---|---|---|---|---|
| basicskeleton | 49 | 252 | 4322 | good; skeleton overlay draws some bones to a common point (overlay only, mesh OK) |
| wingedrhoa | 52 | 10 | 5862 | good |
| harpymonstergull | 54 | 57 | 4864 | bind good; attack_01 at t0.5 shows stretched legs/claws: likely a bad track or bone mapping, OPEN |
| wolfremake | 40 | 30 | 6996 | good |
| ramgiant | 56 | 46 | 5160 | bind good; Wheel_attack_01 pose is crouched; plausible, not checked against the game |
| lichboss | 115 | 42 | 50294 | renders almost black, same class as BellCrow (dark albedo plus missing SSS/spec/emissive shading), OPEN |


### Step E.4: weak effects (handoff/poe2_m2/fx_sheets_e.png, fx_spark_e.png, fx_vfxbench_e.txt)

- Arc impact overexposed: fixed. The CPU .pet AdditiveBlend now maps to SM2 BlendAdd. fw.dds and lightning_bolts_vert.dds are white RGB with an alpha mask (alpha mean 2-7%), and pure Add drew them as white squares. The 0.35 CPU damping is gone (FL_POE2_FX_CPU, default 1). The bolts and the ring now read clearly.
- Spark invisible: fixed, but it is faint. The projectile is only the trail sparkLightning_xtra (lightningSpark.mat, LIGHTNING_Main). The 4x1 strip sheet now uses one column, rotated so the bolt runs along the trail (texture suffix #strip, from TRAIL_UVSelect Rotate UV 0.5). The colour is LIGHTNING_Main '02. Lightning Color' (0.023/0.032/0.11) normalised; strength is max*100 (HEURISTIC). The shader's decay power / white-hot core is not modelled.
- Smoke flat tint: NOT fixed. It is still a constant tint from SIM_Smoke2 04.Albedo_RGB; the lit SIM normal/density shading is not modelled.
- Stormblast explosion: the grey octagon in the first frames is also in the older step-D sheet, so it is not a regression. It is OPEN (probably a flash/glow card without a soft mask).
- All six benches are deterministic. Firebolt, impact and wind spear are unchanged.


### Step E.5: Unreal import (prepared)

- tools/poe2/ue_poe2_import.py runs inside the editor through Scripts/uerun.ps1 (EngineWire). It imports the PNG textures (normal maps as TC_NORMALMAP, linear) to <dest>/<name>/Textures, then the FBX with the legacy automated FBX pipeline: skeletal mesh, skeleton and animation, materials from the FBX texture references, no physics asset. It writes a per-folder asset-class report.
- tools/poe2/ue_poe2_run.ps1 -Dirs <export dirs> [-Dest /Game/ForgeLens/POE] [-WaitMin 60]: polls ue_editor_busy.txt every 30 s and gives up after WaitMin. It refuses if NEEDS_ACTION.txt exists, if a game runs, or if UE_VFX_57 is not open (it never launches the editor). It writes 'poe2m2 <ts>' to the lock and always writes editor-free back.
- Inputs on the PC: PoE2\ue_exp\{bellcrow, wolfremake, basicskeleton, wingedrhoa} (--poe2export, one clip each). Blender 5.0 imports every FBX and GLB with the right bone count, the clip and the images (handoff/poe2_m2/ue_exp_blender_check.txt).
- Note: RULES_CORE says UE work only under /Game/SM2_Work; the owner's M2 brief names /Game/ForgeLens/POE/. The script is restricted to /Game/ForgeLens/POE.


### Step E.6: Unreal import (done, 06:39-06:50 KST)

- The lock was held by the SM2 worker (sm2fid-r97o/r97s/r97t) from 05:39 until 06:38 'editor-free'. The editor was relaunched by that worker at 06:35 with -unattended -nosplash. Its MainWindowTitle is empty, so the runner now matches the process command line. Route: EngineWire (port 3000) via Scripts/uerun.ps1. The lock was taken as 'poe2m2 <ts>' for each run and released. NEEDS_ACTION.txt was absent before and after.
- Imported into /Game/ForgeLens/POE/{bellcrow, wolfremake, basicskeleton, wingedrhoa}: SkeletalMesh, Skeleton and AnimSequence (attack_01 1.833 s, attack_01 1.5 s, approach_sword_sword_01 1.333 s, death_01 1.0 s), plus Textures/*.png. Bone counts in UE: 86/40/49/52, the same as the source.
- Materials: shared /Game/ForgeLens/POE/_Shared/M_Poe2Lit (BaseColorTex, NormalTex, roughness 0.8), with Materials/MI_<slot> per mesh slot. Textures are assigned from the part map in poe2export.txt (checked: all slots have colour and normal). FBX slot names replace | with _. The importer's own FBXLegacyPhong MICs (untextured) are still in each folder and unused.
- Not done in UE: the TWEAK_Albedo tint (BellCrow x1.6), SSS/spec maps, masked fur, more than one clip per mesh (FBX export writes one clip), and a visual UE viewport check.
- Result files: handoff/poe2_m2/ue_import_result.json and ue_import_check.json. Checker: tools/poe2/ue_poe2_check.py.

