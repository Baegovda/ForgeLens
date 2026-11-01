# Black Myth: Wukong — M0 (format survey)

Game code: `BMW` (Unreal import root `/Game/ForgeLens/BMW/`). Same SM2 feature set as every game
(models, skeletons, animations, textures, materials, physics/cloth, levels/props, viewer, Unreal import);
only the reader differs. The game was NOT launched for this survey (rule t265u).

## Install (measured on Revaptor-FX, 2026-10-31 KST)
- Path: `D:\Steam\steamapps\common\BlackMythWukong` (Steam app 2358720, buildid 21393610, 149,864,365,377 B on disk).
- Exe: `b1\Binaries\Win64\b1-Win64-Shipping.exe` (project name `b1`). Content root in paks: `/b1/Content/` (`00Main`, `00MainHZ`, ...).
- Paks: `b1\Content\Paks\pakchunk{0-17,20,23,26}-Windows*.pak` + `.sig` each. Biggest: chunk14 37.8 GB, chunk15 27.0 GB, chunk16 20.9 GB, chunk0 16.6 GB, chunk10 11.2 GB.
- **No IoStore** (`.utoc/.ucas` absent): classic UE pak containers only.

## Container format (measured from the pakchunk17 footer)
- Footer: encryption key GUID = all zeros (single default key), `bEncryptedIndex = 1`, magic `0x5A6F12E1`,
  version 11, followed by an extra 16-bit field (`0x0010`) that stock UE 5.0 does not have, then index offset/size/hash,
  compression method names: `Oodle`.
- So: custom-modified UE 5.0 pak v11 (one extra int16 after version; per gildor.org also one extra byte in legacy
  StructSize), AES-256 encrypted index (and likely encrypted entries), Oodle compression.
- Engine: UE 5.0 (game-specific fork). Unversioned property serialization -> a `.usmap` mapping is needed for
  property-level parsing.

## Encryption key
- Public community AES-256 key (not verified by us yet, not decrypted on the PC):
  `0xA896068444F496956900542A215367688B49B19C2537FCD2743D8585BA1EB128`
  (sources: nexusmods.com/blackmythwukong/mods/943, cnblogs paw5zx/p/18372743, blog.ixiaocai.net).
- `.usmap`: community `Wukong_Release.usmap` exists; generating our own needs injecting a dumper into the running game,
  which we will NOT do (no game launch). Prefer the published usmap.
- Textures: mostly virtual textures (gildor note) -> texture reader must handle VT tiles.

## What can be extracted (community-reported chunk map, to verify)
- pak1-9 maps/levels, pak10 audio (Wwise .wem), pak11 animation, pak12 effects (Niagara), pak13 blueprints,
  pak14 environment meshes/textures, pak15 characters (meshes, textures), pak16 movies (.bk2) + data tables
  (`00Main/PBTable`, protobuf).
- Reference tools: CUE4Parse/FModel has `GAME_BlackMythWukong` (handles the pak quirk + cooked UE5.0 assets);
  UModel game override; quickbms script.

## First reader plan (M1)
1. Pak reader: footer with the extra int16, AES-256-ECB index decrypt with the key, path-hash/full-directory index (v11),
   Oodle (reuse SM2/PoE2 oo2core path) -> list all files to `bmw_index.tsv`. Verify: decrypted index parses, file count, a known path
   (`/b1/Content/00MainHZ/Characters/Sunwukong_weiba/SK_weiba.uasset`).
2. Asset reader: UE 5.0 cooked `.uasset/.uexp` with usmap -> USkeletalMesh, USkeleton, UAnimSequence (ACL compressed likely),
   UTexture2D (+VT), UMaterialInstance params. Use CUE4Parse (MIT-licensed... check licence before copying) as reference.
3. glTF export -> viewer -> Unreal import to `/Game/ForgeLens/BMW/` (Destined One + one boss first), then physics
   (UE PhysicsAsset/ClothingAsset are native here, so they map directly), levels from pak1-9.

## Not verified
- Key not tested; index not decrypted; no asset parsed; chunk map is from community posts.

## M1 draft status (2026-11-02, commit `1461bbf`)
- **In repo:** `BmwGlb` + `BmwWindow` + Wukong toolbar button + `tools/bmw/ue_bmw_import.py` (C# Release build OK). Draft only — no retail assets parsed in-tree.
- **Not committed (on purpose):** pak decrypt readers that embed AES key literals (`tools/bmw/Program.cs`, `bmw_pak.py` left untracked on `fl_bmw`).
- **Key handling:** pass the community key via env **`BMW_AES_KEY`** (hex). The public community value is listed above for humans; **do not hard-code it into committed tools**. Key was **not** re-tested in the 2026-11-02 pass (still: index not decrypted in CI; no asset parsed from a committed reader).
- Unreal target root: `/Game/ForgeLens/BMW/`. Never launch the game.
