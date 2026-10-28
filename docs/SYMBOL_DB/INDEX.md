# symbol DB ("genome map") of sm2_retail.exe and the SM2 game data

One place to look up what is already reverse-engineered, so new work starts from the map instead of re-running RE. **REA is the primary RE path** (WORKFLOW.md "REA"); plain Ghidra headless is the fallback.

## How to use
1. `grep -i <word or address> docs/SYMBOL_DB/index.tsv` (columns: symbol | address | domain | purpose | status | doc | dump | strings).
2. Open the domain file below for the overview (entry points, data links, code that implements it, open questions).
3. Unknown function but asset-related? `grep <address or string> docs/SYMBOL_DB/rea_string_xrefs.tsv` (REA string-xref inventory, below).
4. Only if both have nothing: run new RE with **REA first** (the persistent `rea mcp` daemon, `tools/re/rea_call.py`; WORKFLOW.md "REA"), plain Ghidra headless only as a fallback. Write the result into RE_NOTES / HANDOFF with a status word, and regenerate the map:
   `python3 tools/symbol_db/build_symbol_db.py` (repo root; default dump dirs `/workspace/rea`, `/workspace/mtl_fix/m48` on the box).

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

## Coverage (generated 2026-10-29, mtl1-49; 743 after mtl1-50)
727 symbols at mtl1-49: 304 vfx, 115 physics, 97 shaders, 57 materials, 52 animation, 51 rendering, 27 customization, 10 meshes, 3 containers, 11 misc. 412 have a doc line; 315 are decompile-only (728 decompiled functions across 104 dump files).

This is not a full decompilation of the exe (156,646 functions, see below); it is the map of everything the project has touched, with the data-level links that make most new questions answerable without the exe. Extend it as you go.

## Whole-exe coverage (REA, 2026-10-29)
REA (`list_procedures`, full Ghidra 12.1.4 analysis) counts **156,646 procedures** in sm2_retail.exe (155,469 internal, 1,177 external, 385 thunks). The 743 symbols here (after mtl1-50) are **0.47 %** of them.
Asset-relevant functions = functions that reference a string classified into one of 9 asset domains (REA `search_strings` 56,142 strings → keyword classification → `xrefs` per string → containing procedure). Inventory: [rea_string_xrefs.tsv](rea_string_xrefs.tsv) (function | domains | in_symbol_db | string_count | strings), generator `tools/symbol_db/rea_string_xrefs.py` (inputs on the box in `/workspace/rea`).

| Domain | Functions | In symbol DB | % |
|---|---|---|---|
| animation | 682 | 37 | 5.4 |
| shaders | 791 | 40 | 5.1 |
| physics | 1,845 | 47 | 2.5 |
| vfx | 523 | 62 | 11.9 |
| meshes | 631 | 18 | 2.9 |
| rendering | 437 | 49 | 11.2 |
| customization | 214 | 20 | 9.3 |
| materials | 847 | 78 | 9.2 |
| containers | 339 | 4 | 1.2 |
| **all (union)** | **5,062** | **193** | **3.8** |

Most of the uncovered rows are ECS component/registration boilerplate; the tsv strings column tells you which ones matter. Functions with no string reference (most of the exe) are not counted.

## Sources merged
`HANDOFF.md`, `docs/handoff/{RE_NOTES,SHADERS,FORMATS,STATUS,ROADMAP,WORKFLOW}.md`, the Ghidra dumps in `/workspace/rea/*.c|*.txt` and `/workspace/mtl_fix/m48/*.c` (box), plus hand-written overviews. The dumps themselves stay on the box (7.6 MB); re-decompile any address with the RE_NOTES command.
