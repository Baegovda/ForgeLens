#!/usr/bin/env python3
"""Match exported mesh sections to RenderDoc draws by exact index count.

usage: map_sections_to_draws.py sections.tsv draws.json [--pool LOD1] [--min-eid N] [--max-eid N]

sections.tsv comes from tools/sections_dump (pool, part, texture, variant, indexCount, ...).
draws.json is the nightly RenderDoc dump ([{eid, ni, sh{ShaderStage.Pixel}, ...}]).
Only visible sections are matched unless --pool picks one. A section whose index count is unique in the
frame window gives an unambiguous draw; the pixel shader id then shows which material path the game used
(e.g. chameleon vs default), which is how the per-section variant is checked against the game.
"""
import argparse, csv, json

ap = argparse.ArgumentParser()
ap.add_argument("sections"); ap.add_argument("draws")
ap.add_argument("--pool"); ap.add_argument("--min-eid", type=int, default=0); ap.add_argument("--max-eid", type=int, default=1 << 30)
a = ap.parse_args()

rows = [r for r in csv.reader(open(a.sections), delimiter="\t")
        if (r[0] == a.pool if a.pool else r[6] == "True")]
draws = [d for d in json.load(open(a.draws)) if a.min_eid <= d["eid"] <= a.max_eid and d.get("ni")]
by = {}
for d in draws:
    by.setdefault(d["ni"], []).append(d)
hit = 0
for i, r in enumerate(rows):
    ds = by.get(int(r[4]), [])
    first = ds[0] if ds else None
    ps = first["sh"].get("ShaderStage.Pixel", "").split("::")[-1] if first else "-"
    hit += bool(ds)
    print(f"{i:3d}\t{r[2]}\t{r[3]}\t{r[4]}\t" + (f"eid {first['eid']}\tPS {ps}" + ("\t(ambiguous)" if len(ds) > 1 else "") if first else "no draw"))
print(f"# matched {hit}/{len(rows)}")
