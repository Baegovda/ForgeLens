#!/usr/bin/env python3
"""Build docs/SYMBOL_DB/index.tsv + per-domain symbol tables (the "genome map" of sm2_retail.exe RE so far).

Sources (any missing path is skipped):
  * repo docs: HANDOFF.md, docs/handoff/*.md  -> every FUN_/DAT_/0x14xxxxxxx mention with its line as purpose
  * Ghidra dump files (*.c with '---- FUNC <addr> size N' / '### STRING' headers, *.txt REF lists)
    from --dumps dirs (default /workspace/rea, /workspace/mtl_fix/m48)
Domain = keyword rules over the mention line + its section heading (docs) or over the dump's task section
title + the string literals in the function (dumps). Status = PROVEN / HEURISTIC / INFERRED / APPROXIMATION
from the mention text, else NOTED (docs) / DECOMPILED (dump only).
Usage: python3 tools/symbol_db/build_symbol_db.py [--dumps DIR ...]   (run from the repo root)
"""
import argparse, collections, csv, glob, os, re, sys

ADDR = re.compile(r"\b(?:FUN|DAT|PTR|LAB)_(14[0-9a-fA-F]{7})\b|\b0x(14[0-9a-fA-F]{7})\b|\b(?:vtable|@)\s*(14[0-9a-fA-F]{7})\b")
DOMAINS = [  # first match wins; order = specificity
    ("vfx", r"\bsfx\b|vfx|particle|emitter|trail|spark|distortion|genonfly|covering|explosion|muzzle|sfx_|SFX_"),
    ("shaders", r"\bsdc\b|dxil|\bPS \d|\bVS \d|shader|pso|permutation|define table|psos"),
    ("rendering", r"light|exposure|tonemap|\blut\b|grading|hdr|bloom|lightng|render|gbuffer|instance_?tint|inst_obj|LWI_"),
    ("materials", r"material|\btd\b|\.td|glt|chameleon|texture|spec|albedo|nm\b|layer|decal|mtlblend|usage"),
    ("physics", r"cloth|phys|jiggle|cdt|collision|simremap|\bsim\b|wind|backstop"),
    ("animation", r"anim|sani|morpheme|clip|bone|skeleton|retarget|pose|curve"),
    ("customization", r"custom|skin|armor|armour|weapon|firearm|melee|uid|kitbash|constructor|inventory|equipment|gauntlet|colou?r ?scheme"),
    ("meshes", r"\btpl\b|mesh|lod|markup|locator|geom|submesh|vertex"),
    ("scripting", r"\bssl\b|\.cls|blueprint|\bsso\b|native|reflection|serializ|registered|actor|prop_"),
    ("containers", r"pck|archive|resource|zlib|oodle|catalog|stream|file"),
]
STATUS = [("PROVEN", r"PROVEN|\bproven\b|FOUND"), ("HEURISTIC", r"HEURISTIC"), ("INFERRED", r"INFERRED"), ("APPROXIMATION", r"APPROXIMATION")]


def domain_of(*texts):
    t = " ".join(x for x in texts if x)
    for name, rx in DOMAINS:
        if re.search(rx, t, re.I):
            return name
    return "misc"


def status_of(text, default):
    for name, rx in STATUS:
        if re.search(rx, text):
            return name
    return default


def clean(s, n=220):
    s = re.sub(r"\s+", " ", s.replace("|", "/").replace("\t", " ")).strip(" -*#")
    return s if len(s) <= n else s[: n - 1] + "…"


def dom_doc(rel, line, section):
    # Curated evidence docs (RE_NOTES / SHADERS / FORMATS) are sectioned by domain: the heading wins there.
    if rel.startswith("docs/handoff/") and not rel.endswith("STATUS.md"):
        hits = [(m.start(), name) for name, rx in DOMAINS for m in [re.search(rx, section, re.I)] if m]
        if hits:
            return min(hits)[1]  # the first domain word of the heading ("Materials / shaders" -> materials)
    return domain_of(line, section)


def doc_mentions(root):
    files = [os.path.join(root, "HANDOFF.md")] + sorted(glob.glob(os.path.join(root, "docs/handoff/*.md")))
    out = []
    for f in files:
        if not os.path.exists(f):
            continue
        section = ""
        rel = os.path.relpath(f, root)
        for line in open(f, encoding="utf-8", errors="replace"):
            if line.startswith("#"):
                section = line.strip("# \n")
                continue
            for m in ADDR.finditer(line):
                addr = (m.group(1) or m.group(2) or m.group(3)).lower()
                sym = m.group(0).split()[-1].lstrip("@")
                if sym.startswith("0x"):
                    sym = "0x" + addr
                elif not re.match(r"(FUN|DAT|PTR|LAB)_", sym):
                    sym = "0x" + addr
                lo, hi = max(0, m.start() - 110), min(len(line), m.end() + 110)
                snippet = ("…" if lo else "") + line[lo:hi] + ("…" if hi < len(line) else "")
                out.append(dict(symbol=sym, address="0x" + addr, purpose=clean(snippet), section=section, doc=f"{rel}#{section[:60]}",
                                domain=dom_doc(rel, line, section), status=status_of(line, "NOTED")))
    return out


def task_titles(root):
    """mtl1-NN -> HANDOFF/STATUS section title (dump files are named mNN_*)."""
    titles = {}
    for f in [os.path.join(root, "HANDOFF.md"), os.path.join(root, "docs/handoff/STATUS.md")]:
        if not os.path.exists(f):
            continue
        for line in open(f, encoding="utf-8", errors="replace"):
            m = re.match(r"#+ .*?mtl1-(\d+)", line)
            if m:
                titles.setdefault(int(m.group(1)), line.strip("# \n"))
    return titles


def dump_functions(dirs, titles):
    funcs = {}
    strings = collections.defaultdict(set)  # func addr -> anchor strings
    for d in dirs:
        for f in sorted(glob.glob(os.path.join(d, "*.c")) + glob.glob(os.path.join(d, "*.txt"))):
            base = os.path.basename(f)
            tm = re.match(r"m(\d+)", base)
            title = titles.get(int(tm.group(1)), "") if tm else ""
            text = open(f, encoding="utf-8", errors="replace").read()
            for m in re.finditer(r"### STRING '([^']*)' @ ([0-9a-f]+)\n((?:  ref .*\n)*)", text):
                for fn in re.findall(r"in FUN_([0-9a-f]+)", m.group(3)):
                    strings[fn].add(m.group(1))
            parts = re.split(r"^---- FUNC ([0-9a-f]+) size (\d+)\n", text, flags=re.M)
            for i in range(1, len(parts) - 1, 3):
                addr, size, body = parts[i], int(parts[i + 1]), parts[i + 2]
                lits = [s for s in re.findall(r'"([^"\\]{4,60})"', body) if not s.startswith("%")][:6]
                e = funcs.setdefault(addr, dict(size=size, files=set(), lits=[], title=title))
                e["files"].add(os.path.relpath(f, d) if len(dirs) == 1 else f)
                for s in lits:
                    if s not in e["lits"]:
                        e["lits"].append(s)
                if title and not e["title"]:
                    e["title"] = title
    return funcs, strings


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=".")
    ap.add_argument("--dumps", nargs="*", default=["/workspace/rea", "/workspace/mtl_fix/m48"])
    a = ap.parse_args()
    root = a.root
    rows = {}
    for r in doc_mentions(root):
        key = r["address"]
        if key in rows:  # keep the most informative: PROVEN first, then longest purpose
            old = rows[key]
            rank = lambda x: (x["status"] == "PROVEN", x["status"] != "NOTED", len(x["purpose"]))
            if rank(r) > rank(old):
                r["also"] = old.get("also", 0) + 1
                rows[key] = r
            else:
                old["also"] = old.get("also", 0) + 1
            continue
        rows[key] = r
    funcs, strings = dump_functions([d for d in a.dumps if os.path.isdir(d)], task_titles(root))
    for addr, e in funcs.items():
        key = "0x" + addr
        hints = sorted(strings.get(addr, set())) + e["lits"]
        dump = ";".join(sorted(e["files"]))[:200]
        if key in rows:
            rows[key]["dump"] = dump
            if hints:
                rows[key]["strings"] = clean(" / ".join(dict.fromkeys(hints)), 160)
            continue
        purpose = ("strings: " + " / ".join(dict.fromkeys(hints))) if hints else f"decompiled ({e['size']} B), purpose not documented"
        rows[key] = dict(symbol="FUN_" + addr, address=key, purpose=clean(purpose), section=e["title"], doc="",
                         domain=domain_of(" ".join(hints)) if hints else "misc", status="DECOMPILED", dump=dump,
                         strings=clean(" / ".join(dict.fromkeys(hints)), 160))
    # Undocumented dump-only functions inherit the majority domain of their dump file (documented + string-hinted ones),
    # then the file's task title (mNN_ -> HANDOFF mtl1-NN).
    per_file = collections.defaultdict(collections.Counter)
    for r in rows.values():
        if r["domain"] != "misc":
            for f in r.get("dump", "").split(";"):
                if f:
                    per_file[f][r["domain"]] += 1
    for r in rows.values():
        if r["domain"] == "misc" and r["status"] == "DECOMPILED":
            votes = collections.Counter()
            for f in r.get("dump", "").split(";"):
                votes.update(per_file.get(f, {}))
            if votes:
                r["domain"] = votes.most_common(1)[0][0]
                r["purpose"] += " (domain from its dump file)"
            elif r.get("section"):
                r["domain"] = domain_of(r["section"])
    out_dir = os.path.join(root, "docs/SYMBOL_DB")
    os.makedirs(out_dir, exist_ok=True)
    order = sorted(rows.values(), key=lambda r: (r["domain"], r["address"]))
    with open(os.path.join(out_dir, "index.tsv"), "w", encoding="utf-8", newline="") as fh:
        w = csv.writer(fh, delimiter="\t", lineterminator="\n")
        w.writerow(["symbol", "address", "domain", "purpose", "status", "doc", "dump", "strings"])
        for r in order:
            w.writerow([r["symbol"], r["address"], r["domain"], r["purpose"], r["status"], r.get("doc", ""), r.get("dump", ""), r.get("strings", "")])
    by = collections.defaultdict(list)
    for r in order:
        by[r["domain"]].append(r)
    for dom, items in by.items():
        path = os.path.join(out_dir, f"{dom}.md")
        head = ""
        if os.path.exists(path):  # keep the hand-written overview above the marker
            txt = open(path, encoding="utf-8").read()
            head = txt.split("<!-- symbols (generated) -->")[0].rstrip() + "\n\n"
        if not head:
            head = f"# symbol DB — {dom}\n\n(overview to be written)\n\n"
        lines = [head + "<!-- symbols (generated) -->", f"## Symbols ({len(items)}; generated by tools/symbol_db/build_symbol_db.py)", "",
                 "| symbol | address | status | purpose | doc |", "|---|---|---|---|---|"]
        for r in sorted(items, key=lambda r: ({"PROVEN": 0, "INFERRED": 1, "HEURISTIC": 2, "APPROXIMATION": 3, "NOTED": 4, "DECOMPILED": 5}[r["status"]], r["address"])):
            lines.append(f"| {r['symbol']} | {r['address']} | {r['status']} | {r['purpose'].replace('|', '/')} | {r.get('doc', '') or r.get('dump', '')} |")
        open(path, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    stats = collections.Counter((r["domain"], r["status"]) for r in order)
    print(len(order), "symbols")
    for dom in sorted(by):
        print(f"  {dom:14} {len(by[dom]):4}  " + " ".join(f"{s}={stats[(dom, s)]}" for s in ["PROVEN", "INFERRED", "HEURISTIC", "NOTED", "DECOMPILED"] if stats[(dom, s)]))


if __name__ == "__main__":
    main()
