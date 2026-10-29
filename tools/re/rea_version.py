#!/usr/bin/env python3
"""REA version guard: installed rea-agents must be the pinned 6.8.0 (arg names in docs/tools depend on it). Exit 1 + loud message otherwise.
Usage: rea_version.py [path-to-rea-cli]   (default PC re_env\\rea-cli). Called by pc/start_rea.ps1 and docs_check.py --pc."""
import json, os, sys
PIN = "6.8.0"
DEF = os.path.expandvars(r"%USERPROFILE%\Desktop\SM2_Work\Tools\re_env\rea-cli")
def installed(root=DEF):
    p = os.path.join(root, "node_modules", "rea-agents", "package.json")
    return json.load(open(p, encoding="utf-8"))["version"] if os.path.exists(p) else None
def main(a):
    v = installed(a[0] if a else DEF)
    if v == PIN: print(f"REA version OK {v}"); return 0
    print(f"!!! REA VERSION GUARD FAILED: installed {v or 'NOT FOUND'} != pinned {PIN}. Do not use until docs_check + arg names are re-verified (README RE toolbox).", file=sys.stderr)
    return 1
if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
