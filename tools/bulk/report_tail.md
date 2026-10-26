## Root causes and fixes

1. **The glt.blendMode "none" slots were exported translucent (FIXED, commit "Materials: glt.blendMode none draws opaque").**
   - **Affected:**
     - chimera hull (28 slots);
     - the champions' `ch_undersuit_v1/v2`;
     - lictor legs;
     - tech-priest censers.
   - **Helmets:** the intercessor, ravenwing and lamenters helmets took a nested layer `blendMode = "tex_blend"`.
   - **REA, sm2 retail exe:** the 20-entry blend enum table is at `0x144cc9180`:
     - none, black, opaque, blend, add, add_no_alpha, add_smooth_no_alpha, premul_a, mul, mul_inv, lighten, darken, subtract, invert, blend_mul_src1, blend_alpha_src1, blend_only_dest, min, max, blend_factor.
     - Parser `0x14230f690` (`cmp ebx,0x14`); to-string `0x14230f710`.
   - **Fix:** `MaterialMaps.BlendMode` reads only the direct `glt` child, and `none`/`opaque` are opaque.
   - **Re-export:** all 32 non-constructor assets were re-exported.
2. **Tyranid monsters and walkers had no root motion in UE (FIXED, commit "FBX Unreal: single tpl root without ROOT is bone 0").**
   - **Affected:** carnifex, lictor, hive tyrant, warrior, zoanthrope, dreadnought, helbrute.
   - **Cause:** their hierarchy is `ObjectNNNN → ObjectNNNN → CENTRE` with no `ROOT`. `UnrealRoot.FindPromotable` declined, so the writer kept the `{mesh}_Skeleton` armature Null. Interchange made that Null bone 0, and the 0xF4 entity motion sat on bone 1.
   - **Fix:** a single parentless bone now also drops the Null.
   - **Re-test:** the 7 assets were re-exported into `Exports\bulk_fix` and imported into `/Game/SM2_Work/Tests/bulk_fix`.
   - **Root speeds after the fix:**
     - dreadnought `walk_fwd_v02_5_2ms` 526 cm/s. This matches the clip name's 5.2 m/s.
     - helbrute 520 cm/s.
     - carnifex 800 cm/s.
     - hive tyrant 480 cm/s.
     - warrior 593 cm/s.
     - lictor 2000 cm/s.
3. **Diagnostic (added):** the export log now names up to 8 unmatched clip tracks.
   - `CharacterWorldSpaceTM` never matches.
   - Marine/swarm partial matches are face/eye/tongue bones. Expected.
4. **Checker fixes:**
   - `ue_bulk.py` counted `M_SM2_ClothSimHidden` on collision/nav slots (`*_rb`, `glr`, `nav_silhouette`, `flame_area`) as a material mismatch. It is the intended hidden material, so it is now counted as hidden.
   - `ue_bulk.py` now takes an optional set name (`import <asset> bulk_fix`).
   - `bulk_cap.ps1`: `$c` clobbered `$C` (PowerShell variables are case-insensitive), which broke every capture after the first.
   - `make_report.py`: the "File bounds" regex no longer swallows the trailing period.

5. **Captures:**
   - The first capture pass caught 7 assets with grey placeholder textures, because textures were still compiling right after the batch import. The checker itself showed all texture params set.
   - Those 7 were recaptured into `Captures\bulk_re` (box: `caps_re/`).
   - Affected: tech_priest, servitor, servo_skull, bolt_carbine, sentry_gun, both real doors.
   - The contact sheet prefers `caps_re`. `imp_bur_door_03` uses its side view (the door face points along X).
   - A capture should wait for texture compilation; that check is not in `ue_cap_lit.py` yet.

## Open / known limitations (priority)

1. **Constructor tpls (3 FAIL):**
   - 375–670 s per export, 9–20 GB, every customization variant. The UE import crashes the editor.
   - Proposals: a `--variant` filter, and a png-only texture option for fbx-unreal (today every texture is written as dds+png+tga, about 3x disk).
2. **The rubric `rubric_fwd` and lesser-sorcerer `walk_fwd` clips have zero root translation, with ROOT as bone 0.**
   - Likely in-place clips with no 0xF4 track. Not verified in REA (WARN).
3. **Non-animated single-root assets were not re-exported with fix 2.**
   - Assets: chimera, servitor, servo skull, doors, bolt carbine, sentry gun.
   - Bone 0 there is still the `_Skeleton` Null, which is harmless without locomotion.
4. **File bounds:** the log's "File bounds" are whole-tpl bounds (markers and collision included), so UE bounds are compared only within a 0.4–1.6x ratio.
