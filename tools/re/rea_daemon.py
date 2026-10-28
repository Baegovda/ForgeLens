#!/usr/bin/env python3
"""Persistent REA MCP session (one Ghidra import/analysis for many calls).
Drop /workspace/rea/rea_q/<id>.json = {"tool": name, "arguments": {...}} ; result -> <id>.out (json), then <id>.json is renamed .done"""
import json, os, subprocess, sys, time, threading, glob
Q = "/workspace/rea/rea_q"
env = dict(os.environ)
env.update({
 "PATH": "/home/box/tools/node24/bin:" + env.get("PATH", ""),
 "GHIDRA_INSTALL_DIR": "/home/box/tools/ghidra_12.1.4_PUBLIC", "JAVA_HOME": "/home/box/tools/jdk25",
 "GHIDRA_HEADLESS_MAXMEM": "9G", "REA_ANALYSIS_PROVIDER": "ghidra", "REA_GHIDRA_STARTUP_TIMEOUT_MS": "21600000",
 "REA_GHIDRA_DECOMPILE_TIMEOUT_SECONDS": "300",
 "REA_GHIDRA_DISABLED_ANALYZERS": "Decompiler Parameter ID,Decompiler Switch Analysis,Aggressive Instruction Finder,Stack,Non-Returning Functions - Discovered,Windows x86 PE Exception Handling,Call Convention ID,Embedded Media,Variadic Function Signature Override",
})
p = subprocess.Popen(["/home/box/tools/rea-cli/node_modules/.bin/rea", "mcp"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                     stderr=open(f"{Q}/../rea_mcp.err", "a"), env=env, text=True, bufsize=1)
pending = {}; lock = threading.Lock(); nid = [0]
def reader():
    for line in p.stdout:
        try: m = json.loads(line)
        except Exception: continue
        if "id" in m and m["id"] in pending:
            pending.pop(m["id"])(m)
threading.Thread(target=reader, daemon=True).start()
def send(method, params=None, cb=None):
    with lock:
        nid[0] += 1; i = nid[0]
        if cb: pending[i] = cb
        p.stdin.write(json.dumps({"jsonrpc": "2.0", "id": i, "method": method, "params": params or {}}) + "\n"); p.stdin.flush()
ev = threading.Event()
send("initialize", {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "sm2-rea-daemon", "version": "1"}}, lambda m: ev.set())
ev.wait(600)
p.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n"); p.stdin.flush()
def done(path):
    def cb(m):
        out = path[:-5] + ".out"
        with open(out + ".tmp", "w") as f: json.dump(m, f)
        os.replace(out + ".tmp", out)
    return cb
open(f"{Q}/../rea_daemon.ready", "w").write(str(os.getpid()))
while p.poll() is None:
    for path in sorted(glob.glob(f"{Q}/*.json")):
        try: req = json.load(open(path))
        except Exception: continue
        os.replace(path, path[:-5] + ".sent")
        if req.get("method"): send(req["method"], req.get("params"), done(path))
        else: send("tools/call", {"name": req["tool"], "arguments": req.get("arguments", {})}, done(path))
    time.sleep(0.02)
