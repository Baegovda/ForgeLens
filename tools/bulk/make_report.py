import json, glob, os, re, csv
M = "/workspace/bulk/meta"
man = {r["asset"]: r for r in csv.DictReader(open("/workspace/bulk/manifest.csv"))}
exp = {r["asset"]: r for r in json.load(open(f"{M}/results.json", encoding="utf-8-sig"))}
rows = []; fails = []
for a, m in man.items():
    e = exp.get(a, {}); log = open(f"{M}/{a}.log", encoding="utf-8", errors="replace").read() if os.path.exists(f"{M}/{a}.log") else ""
    u = json.load(open(f"{M}/{a}.ue.json", encoding="utf-8-sig")) if os.path.exists(f"{M}/{a}.ue.json") else None
    P = []; W = []; N = []
    F = "/workspace/bulk/meta_fix"  # single-root fix build (UnrealRoot.HasSingleRoot): Exports/bulk_fix -> /Game/SM2_Work/Tests/bulk_fix
    if os.path.exists(f"{F}/{a}.ue.json"):
        u = json.load(open(f"{F}/{a}.ue.json", encoding="utf-8-sig")); log = open(f"{F}/{a}.log", encoding="utf-8", errors="replace").read()
        N.append("re-exported with the single-root fix (bulk_fix set), bone0 %s" % u.get("bone0"))
    if e.get("exit") != 0: P.append("export exit %s after %ss" % (e.get("exit"), e.get("seconds")))
    elif e.get("seconds", 0) > 120: W.append("export slow %ss" % e["seconds"])
    tex = re.search(r"Resolved (\d+) of (\d+) diffuse", log)
    if tex and tex.group(1) != tex.group(2): P.append("diffuse resolved %s/%s" % tex.groups())
    fb = re.search(r"File bounds (-?[\d.]*\d),(-?[\d.]*\d),(-?[\d.]*\d) \.\. (-?[\d.]*\d),(-?[\d.]*\d),(-?[\d.]*\d)", log)
    fdim = sorted(round((float(fb.group(i + 4)) - float(fb.group(i + 1))) * 100, 1) for i in range(3)) if fb else None
    parts = re.search(r"(\d+) parts", log); nodes = re.search(r"of (\d+) nodes", log)
    tracks = re.findall(r"^\S+: (\d+) of (\d+) tracks match", log, re.M)
    want_clips = len([x for x in m["anims"].split(";") if x])
    if want_clips and len(tracks) < want_clips: P.append("clips exported %d/%d" % (len(tracks), want_clips))
    for t, n in tracks:
        if int(t) / int(n) < 0.5: W.append("clip tracks %s/%s" % (t, n))
    if e.get("exit") == 0 and u is None: P.append("not imported to UE")
    an = []
    if u:
        P += u.get("errors", [])
        if u.get("refNaN") or u.get("boundsNaN"): P.append("NaN in ref pose/bounds")
        if u.get("slots") is not None and u["slots"] != u["sidecarSlots"]: P.append("slots %s vs sidecar %s" % (u["slots"], u["sidecarSlots"]))
        if u.get("matMismatchN"): P.append("material mismatch %d: %s" % (u["matMismatchN"], u["matMismatch"][:2]))
        if u.get("texParamBadN"): W.append("texture param not set %d: %s" % (u["texParamBadN"], [x[0] + "." + x[1] for x in u["texParamBad"][:3]]))
        if u.get("filesMissing"): P.append("texture files missing %d" % len(u["filesMissing"]))
        if u.get("sidecarMissing"): W.append("sidecar missing maps %d" % len(u["sidecarMissing"]))
        if fdim and u.get("boundsCm"):
            ud = sorted(u["boundsCm"]); r = max(max(x, 1) / max(y, 1) for x, y in zip(ud, fdim)) if True else 1
            rr = [round(x / y, 2) if y else None for x, y in zip(ud, fdim)]
            if any(q and (q > 1.6 or q < 0.4) for q in rr): W.append("UE bounds %s vs file %s" % (ud, fdim))
        if want_clips and len(u.get("anims", [])) < want_clips: P.append("anims in UE %d/%d" % (len(u.get("anims", [])), want_clips))
        for c in u.get("anims", []):
            if c["nan"]: P.append("NaN in clip " + c["clip"])
            loco = re.search(r"(run|walk|fwd)", c["clip"].split("__")[-1]) is not None
            if loco and (not c["rootMotion"] or c["rootDispCm"] < 5):
                if u.get("bone0") == "ROOT": W.append("in-place loco clip (ROOT is bone 0, no root translation): " + c["clip"])
                else: P.append("no root motion in %s (bone0 %s)" % (c["clip"], u.get("bone0")))
            an.append("%s %.2fs RM %s %.0fcm/s" % (c["clip"].split("__")[-1], c["len"], "on" if c["rootMotion"] else "off", c["speedCmS"]))
    st = "FAIL" if P else ("WARN" if W else "PASS")
    rows.append(dict(asset=a, cat=m["category"], st=st, t=e.get("seconds"), nodes=nodes.group(1) if nodes else "", bones=(u or {}).get("bones", ""), parts=parts.group(1) if parts else "",
                     slots="%s/%s" % ((u or {}).get("slots", "-"), (u or {}).get("sidecarSlots", "-")), tex="%s/%s" % tex.groups() if tex else "", bounds="x".join("%.0f" % v for v in (u or {}).get("boundsCm", [])) , anims="; ".join(an), P=P, W=W, N=N))
json.dump(rows, open("/workspace/bulk/rows.json", "w"), indent=1)
n = {k: sum(r["st"] == k for r in rows) for k in ("PASS", "WARN", "FAIL")}
print(n)
for r in rows:
    if r["P"] or r["W"]: print(r["asset"], r["st"], r["P"], r["W"])

# ---- REPORT.md ----
import datetime
notes = json.load(open("/workspace/bulk/notes.json")) if os.path.exists("/workspace/bulk/notes.json") else {}
L = []
L.append("# SM2 bulk validation (step 4)\n")
L.append("Generated %s KST from `/workspace/bulk/meta` (exports with the b913484 CLI; blend-affected assets and the two new doors re-exported with the fix build, see Fixes).\n" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M"))
L.append("**Result:** %d assets — PASS %d · WARN %d · FAIL %d\n" % (len(rows), n["PASS"], n["WARN"], n["FAIL"]))
L.append("![contact sheet](contact_sheet.jpg)\n")
L.append("| Asset | Cat | Status | Export s | Nodes/Bones | Parts | Slots UE/sidecar | Diffuse | UE bounds cm | Anims |")
L.append("|---|---|---|---|---|---|---|---|---|---|")
for r in rows:
    L.append("| %s | %s | **%s** | %s | %s/%s | %s | %s | %s | %s | %s |" % (r["asset"], r["cat"], r["st"], r["t"], r["nodes"], r["bones"], r["parts"], r["slots"], r["tex"], r["bounds"], r["anims"] or "-"))
L.append("\n## Per-asset findings\n")
for r in rows:
    if r["P"] or r["W"] or r["N"] or r["asset"] in notes:
        L.append("- **%s** (%s): %s" % (r["asset"], r["st"], "; ".join(["FAIL: " + x for x in r["P"]] + ["warn: " + x for x in r["W"]] + ["note: " + x for x in r["N"]] + ([notes[r["asset"]]] if r["asset"] in notes else []))))
if os.path.exists("/workspace/bulk/report_tail.md"):
    L.append("\n" + open("/workspace/bulk/report_tail.md").read())
open("/workspace/bulk/REPORT.md", "w").write("\n".join(L) + "\n")
print("REPORT.md written")
