#!/usr/bin/env python3
"""Rebuild docs/handoff/RE_CACHE/INDEX.tsv (topic | addresses | conclusion | label | file) from
RE_NOTES.md sections, docs/handoff/RE_CACHE/notes/*.md sections, FIRE_CLIP.md and docs/SYMBOL_DB/index.tsv.
Run after every RE batch: python3 tools/re/re_cache_build.py"""
import csv, os, re, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "docs/handoff/RE_CACHE/INDEX.tsv")
ADDR = re.compile(r"\b(?:FUN_|DAT_|0x)(14[0-9a-fA-F]{7})\b")
LABELS = ("PROVEN", "INFERRED", "HEURISTIC")

def addrs(text):
    seen = []
    for m in ADDR.finditer(text):
        a = "0x" + m.group(1).lower()
        if a not in seen: seen.append(a)
    return seen

def label(text):
    c = {l: text.count(l) for l in LABELS}
    return max(c, key=c.get) if any(c.values()) else "NOTED"

def clean(s, n=220):
    s = re.sub(r"[`*]", "", re.sub(r"\s+", " ", s)).strip()
    return s if len(s) <= n else s[: n - 1] + "…"

def md_rows(rel):
    text = open(os.path.join(ROOT, rel), encoding="utf-8").read()
    parts = re.split(r"^(#{2,3} .+)$", text, flags=re.M)
    title0 = os.path.splitext(os.path.basename(rel))[0]
    rows = []
    if parts[0].strip() and addrs(parts[0]):
        parts = ["", "## " + title0, parts[0]] + parts[1:]
    for i in range(1, len(parts), 2):
        title = parts[i].lstrip("#").strip(); body = parts[i + 1]
        a = addrs(body)
        if not a and not any(l in body for l in LABELS): continue
        first = next((l for l in body.splitlines() if l.strip() and not l.startswith(">")), "")
        rows.append([clean(title, 100), " ".join(a[:12]), clean(first.lstrip("-0123456789. ")), label(body), f"{rel}#{title}"])
    return rows

def main():
    rows = []
    for rel in ["docs/handoff/RE_NOTES.md", "docs/handoff/FIRE_CLIP.md"] + sorted(
            "docs/handoff/RE_CACHE/notes/" + f for f in os.listdir(os.path.join(ROOT, "docs/handoff/RE_CACHE/notes")) if f.endswith(".md")):
        if os.path.exists(os.path.join(ROOT, rel)): rows += md_rows(rel)
    with open(os.path.join(ROOT, "docs/SYMBOL_DB/index.tsv"), encoding="utf-8") as f:
        for r in csv.DictReader(f, delimiter="\t"):
            rows.append([f"{r['domain']}: {r['symbol']}", r["address"].lower(), clean(r["purpose"]), r["status"], r["doc"] or "docs/SYMBOL_DB/index.tsv"])
    with open(OUT, "w", encoding="utf-8", newline="") as f:
        f.write("# addresses valid for sm2_retail.exe sha256 4c4fb3a009a803ea5c27e9ced0a36e7c401ef95eb6dd7d651a629f54ee0252fc (85,129,768 B); patched game -> tools/re/exe_check.py\n")
        f.write("topic\taddresses\tconclusion\tlabel\tfile\n")
        for r in rows: f.write("\t".join(x.replace("\t", " ") for x in r) + "\n")
    print(f"{len(rows)} rows -> {os.path.relpath(OUT, ROOT)}")

if __name__ == "__main__":
    main()
