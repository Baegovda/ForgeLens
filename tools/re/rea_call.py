#!/usr/bin/env python3
"""Queue one call to the persistent REA session started by rea_daemon.py and print the result.
Usage: rea_call.py <tool> '<json arguments>' [timeout_s]
  e.g. rea_call.py procedure_pseudo_code '{"procedure":"0x1417e1e50"}'
The first call after open_binary waits for Ghidra import + analysis (~20-40 min for sm2_retail.exe); later calls take seconds."""
import json, os, sys, time, uuid
Q = "/workspace/rea/rea_q"
tool, args = sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
timeout = float(sys.argv[3]) if len(sys.argv) > 3 else 7200
rid = time.strftime("%H%M%S_") + uuid.uuid4().hex[:6]
tmp = f"{Q}/{rid}.tmp"
json.dump({"tool": tool, "arguments": args}, open(tmp, "w")); os.replace(tmp, f"{Q}/{rid}.json")
out = f"{Q}/{rid}.out"; t0 = time.time()
while not os.path.exists(out):
    if time.time() - t0 > timeout: sys.exit(f"timeout; result will land in {out}")
    time.sleep(0.05)
m = json.load(open(out))
r = m.get("result", {})
sc = r.get("structuredContent")
if sc is not None and os.environ.get("REA_FULL") != "1":
    sc = {k: sc[k] for k in ("normalized_result", "result", "limitations", "error") if k in sc} or sc
if sc is not None: print(json.dumps(sc, indent=1)[:int(os.environ.get("REA_MAX", "200000"))])
else: print(json.dumps(m)[:20000])
