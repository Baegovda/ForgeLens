# LibSaber

**Forge Lens** is the desktop asset browser/exporter built on this library (project `src/LibSaber.SM2Browser`, published as `ForgeLens.exe`; the CLI stays `LibSaber.SM2Extractor.exe`).

> Agents / contributors continuing the Space Marine 2 work: start with [`AGENTS.md`](AGENTS.md) and [`docs/handoff/`](docs/handoff/).

A work-in-progress library for parsing game files used by Saber Interactive's Saber3D engine.

[![wakatime](https://wakatime.com/badge/github/Wildenhaus/LibSaber.svg)](https://wakatime.com/badge/github/Wildenhaus/LibSaber)

## Space Marine 2 extractor

`LibSaber.SM2Extractor` lists and extracts Warhammer 40,000: Space Marine 2 archives. It reads the `.pak` / `.pck` payload together with the sibling cache the game ships next to it (`something.pak` and `something.pak.cache`). Point it at one archive or at a folder such as `client_pc`.

The retail game uses `.pak` files. `.pck` is accepted when it uses the same cache layout. `list` and `extract` write the archived bytes only. `export` converts one model the same way the browser's Export does: Index's default mesh visibility, the same UVs, and the textures that model names.

```
LibSaber.SM2Extractor list <archive-or-folder> [--filter <pattern>]
LibSaber.SM2Extractor extract <archive-or-folder> --output <folder> [--filter <pattern>]
LibSaber.SM2Extractor export <game-folder> --asset <path> --format fbx-unreal|gltf|both --lod 0 --output <folder>
```

`export` reads the game folder the way the browser does (every `.pak.cache` / `.pck.cache` under it) and writes the model plus the PNG, TGA, and DDS for the textures it uses. `--asset tpl/cc_titus_assault.tpl` is the path shown in the browser and resolves to `tpl/cc_titus_assault.tpl/cc_titus_assault.tpl`. `--format fbx-unreal` is one skinned mesh, Z-up and facing +Y, in centimeters, which is the direction a default Unreal import lines up with the mannequin. LOD and collision shells Index leaves unchecked are not written. `gltf` is Y-up meters and keeps the preview UVs. `both` writes the two of them. `--lod 0` keeps the meshes Index leaves checked. FBX gives each of those parts one material on that mesh. A part's diffuse is `shadingMtl_Tex`. The matching `.td` names the other maps, and Index's `{diffuse}_nm` and `{diffuse}_spec` siblings are included when those files are in any archive under the folder. `format_descr` is the channel packing (`Diff(rgb)`, `Normalmap(rgb)`, `Metalness(r)+Roughness(g)+AO(b)`, `MetGray(r)+Roughness(a)`, and the emissive, AO, detail, and height forms). The FBX links diffuse, normal, and specular by a path relative to the FBX. A `.materials.json` sidecar and `import_sm2_materials.py` sit next to it. Run the script in Unreal after the FBX import; it builds a master material and one instance per slot, imports normals as normal maps, and imports masks as linear. Unreal flips V on import, so the FBX stores `1 - v` of the preview coordinate and does not wrap it. Flat shared names such as `sc_grey_90` are a solid color when that texture is not in the folder.

Filters are globs or extensions, and can be repeated (`*.pct`, `.tpl`, `characters/**/*.lg`). A failure to open one archive, or to decompress one file, is logged and skipped. The process still prints a summary. Exit code `0` means nothing was skipped, `1` means the arguments or input path were unusable, and `2` means the run finished with at least one skip.

From a Steam library (the folder name matches the path already used by `test/Testbed`):

```
dotnet run --project src/LibSaber.SM2Extractor -- list "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --filter *.pct

dotnet run --project src/LibSaber.SM2Extractor -- extract "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" --output C:\sm2\extract --filter *.pct --filter *.pct.resource --filter *.tpl --filter *.lg
```

A Windows machine does not need a .NET install. Publish the self-contained single-file build with the `win-x64` profile (`PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`):

```
dotnet publish src/LibSaber.SM2Extractor -c Release -p:PublishProfile=win-x64
```

`publish-win-x64.ps1` runs that same command. The folder `src/LibSaber.SM2Extractor/bin/Release/net8.0/win-x64/publish` contains `LibSaber.SM2Extractor.exe` and `extract-all.bat`. The batch file extracts the default Steam `client_pc` folder into `extracted` next to the exe. Pass a different game folder as the first argument if the install is somewhere else. It pauses before the window closes.

```
LibSaber.SM2Extractor.exe list "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc"
LibSaber.SM2Extractor.exe extract "C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc" -o C:\sm2\extract --prefix-archive
extract-all.bat
extract-all.bat "D:\Games\Warhammer 40000 Space Marine 2\client_pc"
```

`--prefix-archive` keeps each archive in its own folder. Without it, archives are merged on their internal paths and a later archive overwrites the same path. `--no-overwrite` leaves files that are already present. `--no-recursive` searches only the top of a folder. `--quiet` prints the summary only.

Deflate entries are fully decompressed in memory before they are written. `DEFLATE64` entries are reported and skipped. Names that would leave the output folder, including `..` segments, are skipped.

The extractor is covered by synthetic `.pak` / `.pck` archives in `test/LibSaber.SM2Extractor.Tests`. `list` was run on a retail install at `D:\Steam\steamapps\common\Space Marine 2\client_pc`: 115 archives, 486564 files, nothing skipped. Some Steam libraries use that shorter folder name; the testbed and `extract-all.bat` still default to `Warhammer 40000 Space Marine 2`. Pass the real folder when it differs.

## Space Marine 2 browser

`LibSaber.SM2Browser` is the desktop browser on top of the same archive reader. Open the game folder or a `client_pc` folder. The app reads `.pak.cache` / `.pck.cache` files under that folder, builds one folder tree merged across those archives, and leaves the payloads on disk until a preview or export asks for one file. The last folder is stored under `%LocalAppData%\LibSaber\SM2Browser\settings.json`.

The folder tree starts expanded at the root you opened. Files stay in a virtualized list (path, type, size, archive) because a retail install is on the order of 486,000 entries, most of them `.pct_mip`. "Include subfolders" lists every file under the selected folder. Search matches the path. The type filter covers textures, models, animations, effects, and text. The same internal path in more than one archive keeps the last archive in path order, and the archive column is that archive's path relative to the folder you opened.

Preview, in priority order:

- `.pct.resource` plus the `.pct_mip` files named by its YAML. A bare `.pct` is uncommon. Block formats BC1, BC3, BC4, BC5, BC6, and BC7 decode for the image pane, with mip and cubemap face selection. DDS export keeps the original blocks. PNG and TGA are the decoded image.
- `.tpl` / `.tpl.resource` plus the sibling `.tpl_data`. Retail templates keep positions, normals, UVs, and indices in `.tpl_data`. A character packs many draw calls as slices of one shared vertex pool (`SubBufferOffset` is a byte offset). The preview and the glTF/FBX export use every slice of the largest pool as LOD0, and the LOD menu can switch to the smaller pools. Part names come from the material. Wireframe and the skeleton are overlays, off until toggled. The panes are splitters. `.lods_base`, `.geom_dbg`, and `.cdt` are not the vertex payload.
- `.sani` is an ETNA version-2 clip. The header stores bone names, duration, and frame rate. `0xF2` is the codec header plus every frame (the unarmed run). `0xF6` is the same header plus a few preview frames, and the sibling `.sani_data` is one `0xF2` block with the rest of the frames (idle, attack). The header is six counts — animated translation, rotation, and scalar, then the constant ones — six bone-index lists, and 24-byte range records (min xyz, scale xyz). Every code is unsigned: `value = code * scale + min`. A translation word is 11/11/10 bits. A rotation is three codes through a modified Rodrigues quaternion, and that quaternion replaces the bone's bind rotation. Constant tracks are written for every bone the clip lists. Scalar channels are not bone scale. `0xF4` is root motion; its translation is added to the skeleton root. Tracks bind by bone name. With a model loaded, the Anim list shows clips that share at least eight bone names, and Play or the slider poses the preview. Bones the clip does not name stay on the bind pose.
- Everything else falls back to text or hex. `.ps` and `.sfx` are not converted.

Export writes DDS, PNG, TGA, glTF (`.glb`), binary FBX 7.4, and a material JSON sidecar. Source units are meters and Y-up: a retail character is about 2.6 units on Y. glTF stays Y-up meters. Unreal FBX is Z-up centimeters (source times 100, Y moved onto Z). Blender FBX is Z-up meters. When a character has a skin block, inverse binds are paired to the first nodes by composing `MatrixModel` as child-before-parent, and the remaining nodes use the inverse of that world so the bind pose does not move. Vertex weights are written as glTF `JOINTS_0` / `WEIGHTS_0` and as FBX skin clusters. The fourth weight byte is dropped when the first three already sum to one. A folder export that would otherwise write `.tpl` and `.tpl.resource`, or `.sani`, `.sani.resource`, and `.sani_data`, onto the same file keeps the source file name so the siblings do not overwrite each other. Effects export the raw file plus any texture or model names found as plain text, and a note that particle systems are not converted. The model preview samples a part's diffuse texture when that name resolves to a `.pct` in the open folder; otherwise the part is a lit solid color. Exporting a model also writes those resolved textures (DDS, PNG, and TGA, following the same checkboxes) unless that option is turned off.

```
dotnet run --project src/LibSaber.SM2Browser
dotnet publish src/LibSaber.SM2Browser -c Release -p:PublishProfile=win-x64
```

The publish profile is a self-contained single-file `win-x64` build (`PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`), same idea as the extractor. `publish-win-x64.ps1` runs it. Skia is packed into the exe, so the Windows machine does not need a .NET install.

A small retail extract can be checked by setting `LIBSABER_SM2_SAMPLE` to that folder and running `LibSaber.SM2.Assets.Tests`. `LIBSABER_SM2_TITUS` points at a folder that contains `tpl/cc_titus_assault.tpl` (the `.tpl` and `.tpl_data` together) and checks the skinned character path. `LIBSABER_SM2_PICK` points at a folder of intercessor clips (`marine_idle_v01_intercessor`, `tacticus_unarmed_run_fwd_intercessor`, `npc_assault_attack_melee_thunder_hammer_light_1_intercessor`, each with its `.sani`, `.sani.resource`, and `.sani_data` when the resource names one). The game bytes are not in the repo. When a variable is unset that test returns without reading files, because this test runner reports a dynamic skip as a failure. The checked sample covers BC3 and BC5 textures (a normal map and a specular map that share the DXN format), `.tpl` plus `.tpl_data` for a helmet and a shield, and two ETNA `.sani` clips (a hormagaunt hit and a jump). Each of those clips writes a translation and a rotation for all 73 bones. The Titus check covers every LOD0 part, the triangle list, and the inverse-bind pairing. The pick check decodes idle (1214 frames), the thunder-hammer attack (157 frames), and the inline unarmed run (33 frames). Each intercessor clip writes 282 rotation tracks and 282 translation tracks, and the test compares every bone and the root track with `decode_sani.py`. 281 of the 286 clip bone names match `cc_titus_assault` (533 bones, root `Object00C9`); `CharacterWorldSpaceTM`, `length`, `footPos_L`, `footPos_R`, and `lookAt` do not. The run's `0xF4` track moves the root about 4.86 units on source Z. Still open: whether every other texture format matches the same path. `.lods_base`, `.geom_dbg`, and `.cdt` are not the vertex buffer.

`export --anim` writes the bind mesh and, beside it, `{model}__{clip}.fbx` and `.glb`. The clip FBX is the skeleton and the animation curves only, with the same bone names, hierarchy, bind pose, and axis conversion as the bind mesh, so it imports as an AnimSequence onto that skeleton. The glb keeps the skinned mesh for preview. `all` exports every `.sani` that shares at least eight bone names. A name matches the archive path.

```
LibSaber.SM2Extractor export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/cc_titus_assault.tpl --anim marine_idle_v01_intercessor --format fbx-unreal --lod 0 -o D:\export\tpl
LibSaber.SM2Extractor export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/cc_titus_assault.tpl --anim npc_assault_attack_melee_thunder_hammer_light_1_intercessor --format both --lod 0 -o D:\export\tpl
LibSaber.SM2Extractor export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/cc_titus_assault.tpl --anim tacticus_unarmed_run_fwd_intercessor --no-root-motion --format fbx-unreal -o D:\export\tpl
LibSaber.SM2Extractor list "D:\Steam\steamapps\common\Space Marine 2\client_pc" --filter *.sani.resource
```

Import the clip FBX as animation onto the existing skeleton `/Game/SM2/cc_titus_assault/cc_titus_assault_Skeleton`. That file has no mesh: it is the limb hierarchy plus the take. The armature node is a Null with a connected Null node attribute, and each bone is a LimbNode, matching an armature-only Blender FBX. UE 5.7.4's default Interchange importer can still report no data for a meshless animation (UE-307815, reproduced by Epic on their own file, unresolved as of late 2025 with a 5.8 target). The legacy FBX importer (`Interchange.FeatureFlags.Import.FBX` off, `FBXIT_ANIMATION`) reads the curves. It uses the same axis and units as the bind mesh: source `(x, y, z)` becomes `(-x, z, y)`, centimeters, Z-up, facing +Y. Bone names, hierarchy, and the bind pose on the nodes stay those of `cc_titus_assault`. A rotation key is the bone's local rotation, not an offset from the bind. `--no-root-motion` leaves the `0xF4` translation off the skeleton root; without that flag the run cycle travels forward. `tacticus_unarmed_run_fwd_intercessor` is inline in the `.sani` and is written.
