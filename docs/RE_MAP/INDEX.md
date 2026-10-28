# RE map ("genome map") of sm2_retail.exe and the SM2 game data

One place to look up what is already reverse-engineered, so new work starts from the map instead of re-running Ghidra.

## How to use
1. `grep -i <word or address> docs/RE_MAP/index.tsv` (columns: symbol | address | domain | purpose | status | doc | dump | strings).
2. Open the domain file below for the overview (entry points, data links, code that implements it, open questions).
3. Only if the map has nothing: run new RE (Ghidra headless, see RE_NOTES.md header), write the result into RE_NOTES / HANDOFF with a status word, and regenerate the map:
   `python3 tools/re_map/build_re_map.py` (repo root; default dump dirs `/workspace/rea`, `/workspace/mtl_fix/m48` on the box).

## Domains
| Domain | File | What it covers |
|---|---|---|
| VFX | [vfx.md](vfx.md) | .sfx effects, emitters, trails, particle packer, anim-event → effect wiring |
| Shaders | [shaders.md](shaders.md) | .sdc bundles, permutations, define tables, PS/VS formulas |
| Rendering | [rendering.md](rendering.md) | lights, exposure, tonemap/LUT, instance buffers |
| Materials | [materials.md](materials.md) | .td, glt layers, blend/akill, lobes, layers, skin materials, colour schemes |
| Physics | [physics.md](physics.md) | jiggle, cloth config, CDT collision |
| Animation | [animation.md](animation.md) | .sani ETNA codec, root motion, additive, events, bundles |
| Customization | [customization.md](customization.md) | constructors, armor regions, weapons/uids, skins, attachments |
| Meshes | [meshes.md](meshes.md) | .tpl, LODs, markup locators, visibility |
| Containers | [containers.md](containers.md) | .pak/.pck, resources, catalog |
| Scripting / data links | [scripting.md](scripting.md) | .cls / .cls.resource / .sso / .sani links (class ↔ model ↔ clip ↔ weapon ↔ effect) |
| Misc | [misc.md](misc.md) | unclassified symbols |

## Status words
- **PROVEN**: read from exe code/data (or the doc line says proven / FOUND).
- **INFERRED**: follows from data with the exe giving only field names.
- **HEURISTIC** / **APPROXIMATION**: not proven; the implementation is a labelled guess or an approximate port.
- **NOTED**: mentioned in the docs as evidence without a status word (read the linked line).
- **DECOMPILED**: decompiled during some task (dump file listed) but its purpose was never written up; the `strings` column holds the string literals it uses as a hint.

## Coverage (generated 2026-10-29, mtl1-49)
727 symbols: 304 vfx, 115 physics, 97 shaders, 57 materials, 52 animation, 51 rendering, 27 customization, 10 meshes, 3 containers, 11 misc. 412 have a doc line; 315 are decompile-only (728 decompiled functions across 104 dump files).

This is not a full decompilation of the exe (tens of thousands of functions); it is the map of everything the project has touched, with the data-level links that make most new questions answerable without the exe. Extend it as you go.

## Sources merged
`HANDOFF.md`, `docs/handoff/{RE_NOTES,SHADERS,FORMATS,STATUS,ROADMAP,WORKFLOW}.md`, the Ghidra dumps in `/workspace/rea/*.c|*.txt` and `/workspace/mtl_fix/m48/*.c` (box), plus hand-written overviews. The dumps themselves stay on the box (7.6 MB); re-decompile any address with the RE_NOTES command.
