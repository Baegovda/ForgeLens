#!/usr/bin/env python3
"""Docs check: verify that paths and commands the handoff docs rely on still exist / match. Reports mismatches.
Usage: docs_check.py [--pc]   (--pc: also check PC paths; run on the PC)"""
import os, re, subprocess, sys
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REPO = ["tools/re/README.md", "tools/re/rea_call.py", "tools/re/recall.py", "tools/re/re_cache_build.py", "tools/re/exe_check.py",
        "tools/re/ghidra_serve/GhidraServe.java", "tools/re/ghidra_serve/gs.py", "tools/re/ghidra_serve/gs.ps1",
        "tools/re/pc/start_rea.ps1", "tools/re/pc/mem_watch.ps1", "tools/re/pc/disk_watch.ps1", "tools/re/pc/patch_watch.ps1", "tools/re/pc/install_tasks.ps1", "tools/re/pc/backup.ps1", "tools/re/rea_version.py", "tools/re/box/push_lock.sh", "tools/re/nmb_verify.py", "docs/handoff/CONCERNS.md", "tools/re/box/refresh_origin.sh",
        "docs/handoff/RE_CACHE/INDEX.tsv", "docs/handoff/UNVERIFIED.md", "docs/handoff/VISUAL_CHECKS.md", "docs/handoff/BACKUPS.md",
        "docs/BOT_TRANSFER.md", "HANDOFF.md", "docs/SYMBOL_DB/index.tsv"]
PC = [r"%USERPROFILE%\Desktop\SM2_Work\Tools\re_env\work\sm2_retail.exe", r"%USERPROFILE%\Desktop\SM2_Work\Tools\re_env\ghidra_12.1.4_PUBLIC",
      r"%USERPROFILE%\Desktop\SM2_Work\Tools\re_env\rea-cli\node_modules\rea-agents", r"%USERPROFILE%\ls_push",
      r"%USERPROFILE%\Desktop\SM2_Work\Scripts\mtl_fix\push.ps1"]
# Known REA 6.8.0 argument names (docs must use these).
REA_ARGS = {"search_strings": "pattern", "procedure_pseudo_code": "procedure"}
def main(a):
    bad = []
    for p in REPO:
        if not os.path.exists(os.path.join(ROOT, p)): bad.append(f"missing repo path {p}")
    if "--pc" in a:
        for p in PC:
            if not os.path.exists(os.path.expandvars(p)): bad.append(f"missing PC path {p}")
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import rea_version
        v = rea_version.installed()
        if v != rea_version.PIN: bad.append(f"REA version guard: installed {v} != pinned {rea_version.PIN}")
    for doc in ["tools/re/README.md", "docs/BOT_TRANSFER.md", "docs/handoff/WORKFLOW.md", "tools/re/rea_call.py"]:
        f = os.path.join(ROOT, doc)
        if not os.path.exists(f): continue
        for n, line in enumerate(open(f, encoding="utf-8"), 1):
            for tool, arg in REA_ARGS.items():
                m = re.search(tool + r"""\s+'\{"(\w+)\"""", line)
                if m and m.group(1) != arg: bad.append(f"{doc}:{n} {tool} uses '{m.group(1)}', REA 6.8.0 wants '{arg}'")
    print("\n".join(bad) if bad else "docs check OK")
    return 1 if bad else 0
if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
