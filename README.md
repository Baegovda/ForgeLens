# LibSaber

A work-in-progress library for parsing game files used by Saber Interactive's Saber3D engine.

[![wakatime](https://wakatime.com/badge/github/Wildenhaus/LibSaber.svg)](https://wakatime.com/badge/github/Wildenhaus/LibSaber)

## Space Marine 2 extractor

`LibSaber.SM2Extractor` lists and extracts Warhammer 40,000: Space Marine 2 archives. It reads the `.pak` / `.pck` payload together with the sibling cache the game ships next to it (`something.pak` and `something.pak.cache`). Point it at one archive or at a folder such as `client_pc`.

The retail game uses `.pak` files. `.pck` is accepted when it uses the same cache layout. This step writes the archived bytes only. It does not convert textures, models, or animations.

```
LibSaber.SM2Extractor list <archive-or-folder> [--filter <pattern>]
LibSaber.SM2Extractor extract <archive-or-folder> --output <folder> [--filter <pattern>]
```

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

`LibSaber.SM2Browser` is the desktop browser on top of the same archive reader. Open a `client_pc` folder. The app reads `.pak.cache` / `.pck.cache` files only, builds a folder tree, and leaves the payloads on disk until a preview or export asks for one file. The last folder is stored under `%LocalAppData%\LibSaber\SM2Browser\settings.json`.

The tree is folders. Files stay in a virtualized list (name, type, size, archive) because a retail install is on the order of 486,000 entries, most of them `.pct_mip`. Search matches the path. The type filter covers textures, models, animations, effects, and text.

Preview, in priority order:

- `.pct.resource` plus the `.pct_mip` files named by its YAML. A bare `.pct` is uncommon. Block formats BC1, BC3, BC4, BC5, BC6, and BC7 decode for the image pane, with mip and cubemap face selection. DDS export keeps the original blocks. PNG and TGA are the decoded image.
- `.tpl` / `.tpl.resource` for a skeleton and, when the vertex buffer is inside the `.tpl`, a mesh. Orbit and zoom are in the preview pane. `.tpl_data`, `.lods_base`, and `.geom_dbg` are the external geometry buffers; they are not decoded on their own.
- `.sani` / `.sani_data` for sequence names and best-effort curves. Play poses the skeleton of the model you opened before the clip.
- Everything else falls back to text or hex. `.ps` and `.sfx` are not converted.

Export writes DDS, PNG, TGA, glTF (`.glb`), FBX, and a material JSON sidecar. glTF is Y-up meters. Unreal FBX is Z-up centimeters. Blender FBX is Z-up meters (scale 0.01). Those units assume the game is centimeters and Z-up. Effects export the raw file plus any texture or model names found as plain text, and a note that particle systems are not converted.

```
dotnet run --project src/LibSaber.SM2Browser
dotnet publish src/LibSaber.SM2Browser -c Release -p:PublishProfile=win-x64
```

The publish profile is a self-contained single-file `win-x64` build (`PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`), same idea as the extractor. `publish-win-x64.ps1` runs it. Skia is packed into the exe, so the Windows machine does not need a .NET install.

What is still unverified against the retail files, because this environment has no `client_pc` payloads: the exact mip packing inside `.pct_mip`, whether `.tpl_data` / `.lods_base` hold the vertex bytes the `.tpl` describes, skin weights and inverse binds, `.sani` curve layout, and the centimeter Z-up assumption. The browser still opens those files, shows the bytes, and says when a decoder does not recognize them.
