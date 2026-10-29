#!/usr/bin/env python3
"""Patch check: compare the game exe against the analysed build (all RE addresses are for this build).
Usage: exe_check.py [path-to-exe]   (default: installed game exe on the PC, else re_env\\work copy)
Same hash -> addresses valid. Different -> game patched: addresses in SYMBOL_DB/RE_CACHE are stale; relocate via strings
(docs/SYMBOL_DB/index.tsv 'strings' + rea_string_xrefs.tsv: search_strings -> xrefs -> function), re-import Ghidra/REA.
Data-file tools (pck/sani/tpl/sfx readers) do not depend on the exe."""
import hashlib, os, sys
KNOWN = {"4c4fb3a009a803ea5c27e9ced0a36e7c401ef95eb6dd7d651a629f54ee0252fc": "analysed build (2026-10, 85,129,768 B)"}
CAND = [r"D:\Steam\steamapps\common\Space Marine 2\client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe",
        os.path.expandvars(r"%USERPROFILE%\Desktop\SM2_Work\Tools\re_env\work\sm2_retail.exe"), "/workspace/rea/sm2_retail.exe"]
def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""): h.update(c)
    return h.hexdigest()
def main(a):
    p = a[0] if a else next((c for c in CAND if os.path.exists(c)), None)
    if not p: sys.exit("exe not found")
    h = sha(p); ok = h in KNOWN
    print(f"{p}\n{os.path.getsize(p)} B sha256 {h}\n" + (f"MATCH: {KNOWN[h]}" if ok else "MISMATCH: game patched -> relocate addresses by strings (see docstring)"))
    return 0 if ok else 1
if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
