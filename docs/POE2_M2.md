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
