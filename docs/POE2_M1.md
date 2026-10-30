# POE2 M1 - meshes, skeleton, animation, textures, materials into the shared ForgeLens pipeline

CORE CONCEPT: same SM2 feature set, only the reader changes. PoE2 data is loaded into the SAME internal model SM2 uses
(`ExportedMesh`, `Bone`, `MeshPart`, `AnimationClip`), so the viewer renderer (GlPreview), the glTF writer and the FBX writer
are shared unchanged. Game never launched or controlled. Unreal editor not used in M1.

## Step 1 (2026-10-31 ~04:30-04:50 KST) - DONE
Reader: `src/LibSaber.SM2.Assets/Games/Poe2/Poe2Reader.cs`. Viewer hooks: `src/LibSaber.SM2Browser/Bench/Poe2Bench.cs`,
`--glbench poe2:<prepared> "<mesh>|<ast>|<clip>" <out>` (GpuBench branch) and `--poe2export <prepared> <out> <mesh> [ast|-] [clip|*]`.

### Reference decoder = adamthedash/poe_data_tools (MIT/Apache-2.0), rev 9c6478b
- Built on the box: rustup nightly (rust-toolchain.toml), `pip install ziglang cargo-zigbuild`,
  `cargo zigbuild --release --target x86_64-pc-windows-gnu -p poe_data_tools-cli` (1 min 08 s). Pure-Rust Oodle (oozextract), no oo2core dll needed.
- `tools/poe2/ast_bundle.rs` = our 10-line example (put in `crates/poe_data_tools-lib/examples/`, `cargo zigbuild ... --example ast_bundle`):
  dumps the decompressed animation bundle of an .ast v8+ (poe_data_tools parses headers but leaves `data: null` for v8+).
- PC copies (not committed, binaries): `Desktop\SM2_Work\PoE2\tools\poe_data_tools.exe`, `ast_bundle.exe`.
- `tools/poe2/prep.ps1` builds a "prepared folder": `raw\` (extract), `json\` (translate), `<ast>.anim.bin`.
  `-p 2 --ggpk "D:\Daum Games\Path of Exile2\Content.ggpk"` works directly on the Kakao GGPK (extract of ~120 files 7.5 s).

### Formats (PROVEN by decode + render)
- `.sm` (text) -> `smd_file`, `materials[]`, `bone_groups`. `.smd` v3: dolm section, lods[0].vertices {pos, normal i8x4, tangent, tex_coord0, skin_bones u8x4, skin_weights u8x4/255}, indices U16, shape_extents {start_index,count_index}.
- `.fmt` v9: same vertex layout without skin; `shapes[]` {name, material, triangle_start}.
- `.ast` v12: 49 bones {child, sibling, transform 4x4 row-major LOCAL (parent-relative)}; hierarchy = child/sibling walk from bone 0.
  BasicSkeleton rig is mostly flat (most jntBnd bones are children of root). 252 animations, data in an embedded bundle.
  Track (v10+): u8 unk, u32 bone index, 6 x u32 counts, u32 unk5; then scale keys [frame,x,y,z], rotation [frame,x,y,z,w],
  position [frame,x,y,z], unk2 [4f], unk3 [5f], unk4 [4f]. attack_01 decodes to exactly its 48,689 bytes; frames 0..45 @30 fps = 1.50 s.
- `.mat` v4 JSON: graphinstances[].custom_parameters (AlbedoSpecMask_TEX / NormalGlossAO_TEX / Translucency map ...) -> MeshPart.Texture/Normal/Emissive + Maps.
- `.dds`: plain DDS (DX9 DXT1/DXT5 and DX10 BC7) decoded with BCnEncoder.Net (already a dependency).
- Space: PoE = centimetres, -Z up (BasicSkeleton1 bbox z -195.6..0.18). Internal = metres Y-up: (x,y,z) -> (x,-z,y)*0.01, a rotation (no mirror).
- smd UVs are stored in -1..0 (range PROVEN); +1 applied (same texel under wrap).

### Verified (rendered through the real viewer GL renderer, ANGLE/D3D11, RTX 4090)
- Static: `art/models/items/weapons/onehandweapons/claws/basetypes/crudeclaw.fmt` 796 verts / 651 tris, BC7 albedo correct (wood + metal spikes).
- Skinned + animated: `art/models/monsters/basicskeleton/basicskeleton1.sm` + `rig.ast` clip `attack_01`: 2480 verts / 3625 tris, 49 bones,
  KatarinaSkeleton1 albedo; bind pose clean; t=0.5 s pose is a coherent lunge (no exploded vertices), skeleton overlay follows the mesh.
  Contact sheet: `docs/handoff/poe2_m1/m1_sheet.png`.
- Export: `.gltf` + Unreal-space `.fbx` + PNG per texture written for both (`Desktop\SM2_Work\PoE2\m1\out_claw`, `out_skel`).

### NOT verified
- Facing/handedness vs the game (no in-game comparison; owner rule: do not launch the game). Normal map packing (NormalGlossAO) not used in preview (albedo only).
- glTF/FBX not opened in Blender/Unreal yet; Unreal import under /Game/ForgeLens/POE/ not done.
- Multi-shape / multi-LOD smd, other vertex formats, .tgm (terrain), .arm/.tdt (levels) not tried.
- The interactive viewer UI (file browser on a GGPK) is not wired yet; only the bench/CLI path opens PoE2 files.

### Resume
Rebuild: `dotnet build src\LibSaber.SM2Browser -c Release -o Desktop\SM2_Work\PoE2\bin_m1`.
Prepared folder used: `Desktop\SM2_Work\PoE2\m1` (raw + json + rig.ast.anim.bin).

## Step 2 (2026-10-31 ~04:50-05:00 KST) - skills: list + effect preview (first cut) - DONE (proxy quality)
Code: `src/LibSaber.SM2.Assets/Games/Poe2/Poe2Effect.cs` (`ReadAo`, `LoadEffect`), `Poe2Bench.Skills` (`--poe2skills <prepared> <out.tsv>`),
effect preview through the same viewer path: `--glbench poe2:<prepared> "<effect.ao>||<animation>" <out>`, export `--poe2export <prepared> <out> <effect.ao> - <animation>`.
Prepared folder used: `Desktop\SM2_Work\PoE2\m1s` (fire_fireball effects + art/particles/fire_fireball + normalized_prtcls + 20 particle textures + tables).

### Data chain (PROVEN on Firebolt / fire_fireball)
- `data/balance/activeskills.datc64` (dump-tables JSON via dat-schema, 1,147 rows): Id, DisplayedName, GrantedEffect, ActionType, ActiveSkillTypes, Icon.
- `data/balance/projectiles.datc64` (2,103 rows, all with AOFiles): `Metadata/Projectiles/Fireball` -> AOFiles `Metadata/Effects/Spells/fire_fireball/ao/rig.ao`,
  LoopAnimationIds `projectile`, ImpactAnimationIds `impact`, MiscAnimatedFlash, speed 1040.
- Skill -> projectile link: HEURISTIC name match of ActionType/Id to the projectile id (firebolt ActionType `Fireball` -> Projectiles/Fireball). 89 of 1,147 skills matched.
  The real link (GrantedEffect -> .ot/.ais projectile spawn) is NOT decoded yet. Other effect tables dumped: miscanimated (11,324 rows: Id -> AOFile),
  miscobjects (1,160), skillartvariations (737), miscanimatedartvariations, grantedeffects.
- `.ao` (text, JSON via translate): ClientAnimationController skeleton `.ast`, default_animation, BoneGroups, ParticleEffects / TrailsEffects per animation
  (embedded JSON: events {time, filename .pet/.trl, bone_group}), SkinMesh, socket/parent/translation.
- `.pet` v4: `payload.gpu_emitters[]` {particle_mesh .fmt (optional), update_material .mat (behaviour = fxgraphs), render_passes[].render_material, durations, emit_burst, particles_count}.
- `.trl`: emitters with curve strings ("2 0 1 Linear 1 1 Linear"), material, segment lengths - parsed by poe_data_tools, not used yet.
- Effect rig `.ast` v12 decodes with the same track code (projectileRIG: 13 bones, clips impact/projectile 1.00 s).

### Verified (viewer GL render, docs/handoff/poe2_m1/fx_sheet.png)
- rig.ao `projectile`: rig + clip load, 4 emitters (halfsphere mesh particle + 3 sprite proxies) placed on bone group `head` -> `aux_tip_attach_01`; renders and plays.
- impact.ao `once`: 1-bone rig, 9 emitters -> 8 drawn (mp_sphere mesh + sprites); `cleaveGeo01.fmt` not found under normalized_prtcls (path to resolve).
- `--poe2skills` writes `docs/handoff/poe2_m1/skills.tsv`.

### NOT done / NOT verified (honest state)
- It does NOT look like the game: no particle simulation (rate, lifetime, velocity, size/colour curves are in the update fxgraphs), mesh particles
  without a colour texture draw flat white additive, sprites use a fixed 40 cm cross (size not from data). Colour gradients of render mats not applied.
- Trails (.trl) not drawn. Character cast animation + projectile flight path not combined. No in-game comparison (game must not be launched).
- Interactive viewer UI list of skills not added (CLI/bench only). Unreal import of effects not done.

### Resume (next)
1. Decode fxgraph colour/size params of render/update mats (Texture Remap curves, colour gradients) -> MeshPart tint/size; then a CPU particle sim reusing the SM2 EffectRenderer.
2. Real skill->effect link: GrantedEffects -> Metadata/Projectiles/*.ot (extract .ot/.otc; text) -> AOFiles.
3. Viewer UI: "PoE2" source in the content browser (prepared folder picker), skill list from skills.tsv, clip dropdown.
4. Unreal import under /Game/ForgeLens/POE/ (FBX already writes; needs editor lock rules).
