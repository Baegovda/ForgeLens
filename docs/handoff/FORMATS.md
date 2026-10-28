# FORMATS — SM2 file formats and where they are parsed

Paths are relative to `src/`. "A" means `LibSaber.SM2.Assets/`. RE evidence for each format is in `RE_NOTES.md`. Long-form notes are in HANDOFF.md, under the section named in the last column.

| Format | What it is | Parser / writer | HANDOFF section |
|---|---|---|---|
| `.pak` / `.pck` + `.pak.cache` / `.pck.cache` | Archives. The cache is the ZIP-like directory and the payload sits beside it. Deflate is supported; DEFLATE64 is skipped | `LibSaber.SpaceMarine2/Structures/Resources/fioZIP_FILE.cs`, `Serialization/Resources/fioZIP_CACHE_FILESerializer.cs`, `Files/SM2PckFileDevice.cs`; catalog `A/Catalog/GameCatalog.cs`, `ArchiveStore.cs`; CLI `LibSaber.SM2Extractor/ExtractorEngine.cs` | README |
| `.pct.resource` + `.pct_mip` (`.pct`) | Textures: YAML header plus mips; BC1-7 and cubemaps | `LibSaber.SpaceMarine2/Serialization/Resources/PctResourceSerializer.cs`, `pctPICTURESerializer.cs`; `A/Textures/TextureLoader.cs`, `TextureDecode.cs`, `ImageWriters.cs` | README |
| `.td` (`.td.resource`) | Texture/material definition: `format_descr` packing, glt layers, blendMode, tints, `preset "sfx"` | `A/Meshes/MaterialMaps.cs`, `SpecLayers.cs`; sidecar in `A/Export/AssetExporter.cs`; UE `A/Export/import_sm2_materials.py` | Item 2, Second specular, Material finishing |
| `.tpl` + `.tpl.resource` + `.tpl_data` | Templates: object tree, skin, LODs, shared vertex pools, affixes | `LibSaber.SpaceMarine2/Serialization/*` (objOBJ, objGEOM_*, tplSKIN, tplLOD_DEF, Geometry/*); `A/Meshes/TemplateMeshReader.cs`, `IndexMeshVisibility.cs`, `UnrealRoot.cs`, `FbxWriter.cs`, `GltfWriter.cs`, `ExportedMesh.cs` | Item 4, sani gaps part 2, Bulk |
| `.sani` + `.sani.resource` + `.sani_data` | ETNA animation clips (v1-v3; F2/F6 slot 0, F3/F7 additive, F4 root, block 245 events) | `A/Animation/SaniReader.cs`, `EtnaCurves.cs`, `SaniIndex.cs`, `AnimEvents.cs`, `AnimationClip.cs`, `ClipResolver.cs`; posing `A/Meshes/ClipPose.cs`, `ClipBinding.cs`; py reference `test/LibSaber.SM2.Assets.Tests/decode_sani.py`; validator `tools/sanival` | sani gaps, Animation finishing |
| `morpheme/<bundle>/<bundle>.mr.resource` | JSON `res_desc_morpheme`; `linksAnimation` gives a character's main clip set | `A/Animation/CharacterAnimations.cs` | SM2Browser v4 |
| `ssl/**/*.cls` (text SSL) | Character classes: `geom.nameTpl`, parent chain, `morphemeController`, `prop_cloth_sim`, `prop_phys_jiggle` | `A/Physics/PhysicsLocator.cs`, `PhysicsParser.cs`, `PhysicsSetup.cs`; `A/Animation/CharacterAnimations.cs` | Secondary physics |
| `{model}.physics.json` (ours) | Physics sidecar: source values, baked clips (`rate`), UE mapping | `A/Physics/PhysicsSidecar.cs`, `PhysicsBake.cs`; UE `tools/ue/ue_sm2_physics.py` | Secondary physics, UE verification |
| customization SSL (`ssl/body/customization/**`, `.sslbin`, dummy `.cls`) | Armor regions, presets, `meshesToHide` | `A/Customization/SslText.cs`, `ArmorVariant.cs`; `AssetExporter.ApplyVariant` → `{model}.variant.json` | Constructor tpls |
| `.sfx` (text SSL, `ssl/**`) | Effects: sfx_group/sfx_part/sfx_trail, emitters, unions | `A/Effects/SslText.cs`, `EffectLibrary.cs`, `EffectDefinition.cs`, `EffectSimulation.cs`, `EffectSprites.cs`, `EffectMeshParticles.cs`, `EffectTrails.cs`, `EffectCurves.cs`, `EffectHost.cs`; export digest `EffectSummary.cs` (`X.sfx.json`, `X.niagara.json`); render `LibSaber.SM2Browser/Gpu/EffectRenderer.cs`, `Effects/*` | Item 3, Browser VFX, VFX 2, vfx3 parts 1-3 |
| `part_aliases.ps` | Flipbook grids per texture | `A/Effects/PartAliases.cs` | Browser VFX |
| `.sso` (blueprint graphs) | Gameplay blueprints; anim event → VFX start/stop wiring | `A/Effects/AnimEventVfxMap.cs` | vfx3 part 1/2 |
| `.lods_base`, `.geom_dbg`, `.cdt` | Not the vertex payload (`.cdt` = collision) | – | README |

## Export outputs (ours)
- `fbx-unreal`: Z-up cm, facing +Y; ROOT is bone 0 and carries root motion; baked physics; helper bones `{SIM}_physNN`.
- `gltf`: Y-up m.
- `{model}.materials.json` slots carry `layers[]` (mtl1-27: Saber layer0..3, texture/subMaterial/tint/vcSet/uvSet/tiling/blending) and an optional hand-written `instanceTint [r,g,b]` override (sRGB 0..1).
- Sidecars next to the model: `{model}.materials.json`, `{model}.physics.json`, `{model}.variant.json`, `{fbx}.rebase.json` (cinematic placement).
- Scripts next to the model: `import_sm2_materials.py`, `ue_import_anim.py`, `ue_sm2_physics.py`.
- Clip files: `{model}__{clip}.fbx`, `__ADD.fbx` (additive).
