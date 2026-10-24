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

The extractor is covered by synthetic `.pak` / `.pck` archives in `test/LibSaber.SM2Extractor.Tests`. A retail Space Marine 2 install was not available here, so real `client_pc` caches have not been read with this tool. The cache layout matches the reader already in `LibSaber.SpaceMarine2` (legacy entries, the version 7 header, and the short trailer).
