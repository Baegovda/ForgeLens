#!/usr/bin/env python3
"""Build docs/SYMBOL_DB/rea_string_xrefs.tsv: asset-relevant functions in sm2_retail.exe found by REA string xrefs.

Inputs (produced with the REA daemon, see docs/handoff/WORKFLOW.md "REA"; default dir /workspace/rea):
  rea_procs.json          list_procedures inventory (paged via inspect_analysis_view)
  rea_strings.json        search_strings inventory [{address, value}]
  rea_domain_strings.json {domain: [string address]} keyword classification of the strings
  rea_q/x_<addr>.out      xrefs result for every classified string
Output: TSV (function, domains, in_symbol_db, string_count, strings) and a coverage summary on stdout.
Usage: rea_string_xrefs.py [rea_dir] [repo_root]
"""
import bisect, json, os, sys

rea = sys.argv[1] if len(sys.argv) > 1 else "/workspace/rea"
root = sys.argv[2] if len(sys.argv) > 2 else os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

procs = json.load(open(os.path.join(rea, "rea_procs.json")))
internal = sorted(int(p["record"]["address"], 16) for p in procs if p["record"]["address"].startswith("0x"))
strings = {s["address"]: s["value"] for s in json.load(open(os.path.join(rea, "rea_strings.json")))}
domains = json.load(open(os.path.join(rea, "rea_domain_strings.json")))

def func_of(a):
    i = bisect.bisect_right(internal, a)
    return internal[i - 1] if i else None

funcs = {}  # func -> {"domains": set, "strings": set}
for dom, addrs in domains.items():
    for sa in addrs:
        path = os.path.join(rea, "rea_q", f"x_{sa}.out")
        if not os.path.exists(path):
            continue
        res = json.load(open(path)).get("result", {}).get("structuredContent") or {}
        for ref in res.get("normalized_result") or []:
            f = func_of(int(ref, 16))
            if f is None:
                continue
            e = funcs.setdefault(f, {"domains": set(), "strings": set()})
            e["domains"].add(dom)
            e["strings"].add(strings.get(sa, sa))

covered = set()
with open(os.path.join(root, "docs/SYMBOL_DB/index.tsv"), encoding="utf-8") as fh:
    next(fh)
    for line in fh:
        a = line.split("\t")[1]
        if a.startswith("0x"):
            f = func_of(int(a, 16))
            covered.add(int(a, 16))
            if f is not None:
                covered.add(f)

out = os.path.join(root, "docs/SYMBOL_DB/rea_string_xrefs.tsv")
with open(out, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("function\tdomains\tin_symbol_db\tstring_count\tstrings\n")
    for f in sorted(funcs):
        e = funcs[f]
        s = sorted(e["strings"], key=len)[:6]
        txt = " | ".join(v.replace("\t", " ").replace("\n", "\\n")[:60] for v in s)
        fh.write(f"0x{f:x}\t{','.join(sorted(e['domains']))}\t{'Y' if f in covered else ''}\t{len(e['strings'])}\t{txt}\n")

print(f"procedures {len(procs)} (internal {len(internal)}); symbol_db symbols "
      f"{sum(1 for _ in open(os.path.join(root, 'docs/SYMBOL_DB/index.tsv'))) - 1}")
cov = sum(1 for f in funcs if f in covered)
print(f"asset functions {len(funcs)}, covered {cov} ({100 * cov / max(len(funcs), 1):.1f}%)")
for dom in domains:
    fs = [f for f, e in funcs.items() if dom in e["domains"]]
    c = sum(1 for f in fs if f in covered)
    print(f"  {dom}\t{len(fs)}\t{c}\t{100 * c / max(len(fs), 1):.1f}%")
print("wrote", out)
