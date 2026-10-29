#!/usr/bin/env python3
"""Look up what is already known BEFORE decompiling again.
Usage: recall.py <term> [term...]   terms = address (0x14..., FUN_14...) or keywords (all must match, case-insensitive)
       recall.py -n 50 ...           max rows (default 20)
Searches docs/handoff/RE_CACHE/INDEX.tsv (rebuild: re_cache_build.py). Prints label, topic, conclusion, file."""
import os, re, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
IDX = os.path.join(ROOT, "docs/handoff/RE_CACHE/INDEX.tsv")

def norm(t):
    m = re.fullmatch(r"(?:FUN_|DAT_|0x)?(14[0-9a-fA-F]{7})", t)
    return ("0x" + m.group(1).lower()) if m else t.lower()

def main(a):
    n = 20
    if a[:1] == ["-n"]: n, a = int(a[1]), a[2:]
    if not a: sys.exit(__doc__)
    terms = [norm(t) for t in a]
    hits = []
    for line in open(IDX, encoding="utf-8").read().splitlines()[2:]:
        low = line.lower()
        if all(t in low for t in terms): hits.append(line.split("\t"))
    hits.sort(key=lambda r: ("RE_NOTES" not in r[4] and "RE_CACHE" not in r[4] and "FIRE_CLIP" not in r[4], r[3] != "PROVEN"))
    for r in hits[:n]:
        print(f"[{r[3]}] {r[0]}\n    {r[2]}\n    -> {r[4]}  ({r[1][:80]})")
    print(f"{len(hits)} hit(s)" + (f", showing {n}" if len(hits) > n else "") + ("" if hits else " - not in cache; decompile, then record it"))

if __name__ == "__main__":
    main(sys.argv[1:])
