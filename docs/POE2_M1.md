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
