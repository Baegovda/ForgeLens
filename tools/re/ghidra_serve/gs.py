#!/usr/bin/env python3
"""Client for GhidraServe (persistent decompile server over a saved Ghidra project).
Usage:
  gs.py <tool> '<json args>'              one query  (same names as rea_call.py where practical:
                                          procedure_pseudo_code/decompile, xrefs, search_strings, function_at,
                                          list_functions, summary, ping, shutdown)
  gs.py batch <file.jsonl | ->            many queries in one round trip: one {"tool":..,"arguments":..} per line
  gs.py sum 0xADDR [0xADDR...] [-c N]     compact summary per function (signature, calls, strings, globals,
                                          constants; -c N adds first N pseudo-code lines)
State file: $GS_STATE/serve.json (default: dir of this script/../../.. not used; set GS_STATE)."""
import json, os, socket, sys

def state_dir():
    d = os.environ.get("GS_STATE")
    if d: return d
    return os.path.join(os.environ.get("USERPROFILE", os.path.expanduser("~")), "Desktop", "SM2_Work", "Tools", "re_env", "gs_state") if os.name == "nt" else "/workspace/rea/gs_state"

def send(payload):
    st = json.load(open(os.path.join(state_dir(), "serve.json")))
    payload["token"] = st["token"]
    with socket.create_connection(("127.0.0.1", st["port"]), timeout=float(os.environ.get("GS_TIMEOUT", "600"))) as s:
        s.sendall((json.dumps(payload) + "\n").encode())
        buf = b""
        while not buf.endswith(b"\n"):
            c = s.recv(1 << 20)
            if not c: break
            buf += c
    return json.loads(buf)

def fmt_summary(r):
    if "error" in r: return f"ERROR {r['error']}"
    L = [f"{r['name']} @{r['entry']} size={r['size']} callers={r['callers']} ({r['ms']:.0f} ms)", f"  sig: {r['signature']}"]
    if r["calls"]: L.append("  calls: " + ", ".join(r["calls"]))
    for s in r["strings"]: L.append("  str: " + s[:160])
    if r["globals"]: L.append(f"  globals({r['globals_total']}): " + ", ".join(r["globals"]))
    if r["constants"]: L.append("  const: " + " ".join(r["constants"]))
    if r.get("code"): L.append("  code:\n    " + r["code"].replace("\r", "").rstrip().replace("\n", "\n    "))
    return "\n".join(L)

def main(a):
    if not a: sys.exit(__doc__)
    if a[0] == "sum":
        n = 0; addrs = []
        i = 1
        while i < len(a):
            if a[i] == "-c": n = int(a[i + 1]); i += 2
            else: addrs.append(a[i]); i += 1
        res = send({"batch": [{"tool": "summary", "arguments": {"address": x, "pseudo_lines": n}} for x in addrs]})
        print("\n".join(fmt_summary(r) for r in res["batch"]))
        return
    if a[0] == "batch":
        f = sys.stdin if a[1] == "-" else open(a[1])
        qs = [json.loads(l) for l in f if l.strip()]
        print(json.dumps(send({"batch": qs}), indent=1, ensure_ascii=False))
        return
    tool = a[0]; args = json.loads(a[1]) if len(a) > 1 else {}
    r = send({"tool": tool, "arguments": args})
    if "code" in r and os.environ.get("GS_JSON") != "1":
        print(f"// {r['name']} @{r['entry']} ({r['ms']:.0f} ms)\n" + r["code"].replace("\r", ""))
    else:
        print(json.dumps(r, indent=1, ensure_ascii=False))

if __name__ == "__main__":
    main(sys.argv[1:])
