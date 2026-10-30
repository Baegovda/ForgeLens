"""Rebuild the REA procedure inventory (/workspace/rea/rea_procs.json, ~156,646 procs) by paging
REA `inspect_analysis_view` native-inventory over a retained-evidence id (box original: /workspace/rea/rea_page_procs.py).

usage: python3 tools/re/rea_page_procs.py <evidence_id> [out.json] [page_size]
  <evidence_id> = the `ev_...` id REA returns for the analysed sm2_retail.exe (from any analysis response of the
  running daemon; the old box used ev_232234d6...190b, valid only for that daemon session).
Needs the REA daemon running (tools/re/rea_daemon.py) and tools/re/rea_call.py next to this file.
Strings inventory (rea_strings.json) comes from paging `search_strings` the same way.
"""
import json, os, subprocess, sys

def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    ev = sys.argv[1]
    out_path = sys.argv[2] if len(sys.argv) > 2 else "/workspace/rea/rea_procs.json"
    limit = int(sys.argv[3]) if len(sys.argv) > 3 else 15000
    rea_call = os.path.join(os.path.dirname(os.path.abspath(__file__)), "rea_call.py")
    off, out = 0, []
    while True:
        args = {"source": {"kind": "retained-evidence", "evidence_id": ev},
                "view": {"kind": "native-inventory", "offset": off, "limit": limit}}
        r = subprocess.run(["python3", rea_call, "inspect_analysis_view", json.dumps(args), "600"],
                           capture_output=True, text=True,
                           env=dict(os.environ, REA_FULL="1", REA_MAX="999999999"))
        d = json.loads(r.stdout)["normalized_result"]
        items = [v for k, v in d.items() if isinstance(v, list) and k != "limitations"]
        if off == 0:
            print("keys", list(d.keys()), file=sys.stderr)
        page = max(items, key=len) if items else []
        out += page
        c = d["coverage"]
        print(off, len(page), c, file=sys.stderr)
        if c.get("exhausted") or not c.get("next_offset"):
            break
        off = c["next_offset"]
    json.dump(out, open(out_path, "w"))
    print(len(out))
    print(json.dumps(out[:2]))

if __name__ == "__main__":
    main()
