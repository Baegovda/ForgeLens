#!/usr/bin/env python3
"""mtl1-40: check parsed textureTintControl / additionalContrast / contrastColorKey / blend flags against a capture's
PART_EMITTERS_SHARED_DATA buffer (FUN_1417e1e50 writes 6 float4 per emitter; read by particles PS 281 as t8).

  shared[0].z = textureTintControl, shared[1].rgb = contrastColorKey (0..255), shared[1].w = additionalContrast,
  shared[3].x (uint bits) = flags (0x20 = blendType add, 0x400 = alpha-multiply off).

usage: tint_check.py <part_cpu_data_shared.bin> <emitters.json from ForgeLens --vfxemitters> [out.json]
An emitter slot is "keyed" when its contrast key or additionalContrast is non-zero: those tuples are distinctive enough to
look up in the library; the check passes when every keyed slot has at least one library emitter with the same
(key, contrast, tint) and no library emitter with the same (key, contrast) disagrees on tint unanimously.
"""
import json, struct, sys
from collections import Counter, defaultdict

def slots(path):
    raw = open(path, "rb").read()
    n = len(raw) // 96
    out = []
    for i in range(n):
        v = struct.unpack_from("<24f", raw, i * 96)
        if not any(x != 0 and x == x for x in v):
            continue
        flags = struct.unpack_from("<I", raw, i * 96 + 48)[0]
        out.append({"slot": i, "tint": v[2], "key": [v[4], v[5], v[6]], "contrast": v[7], "flags": flags})
    return out

def kk(key, c):
    return (round(key[0]), round(key[1]), round(key[2]), round(c, 2))

def main(argv):
    cap = slots(argv[1])
    lib = json.load(open(argv[2]))
    by = defaultdict(list)
    for e in lib:
        by[kk(e["key"], e["contrast"])].append(e)
    res = {"slots": len(cap), "tintValues": Counter(round(s["tint"], 3) for s in cap).most_common(),
           "libTintValues": Counter(round(e["tint"], 3) for e in lib).most_common(12), "keyed": []}
    ok = bad = miss = 0
    for s in cap:
        if s["contrast"] == 0 and s["key"] == [0, 0, 0]:
            continue
        cands = by.get(kk(s["key"], s["contrast"]), [])
        tints = Counter(round(e["tint"], 3) for e in cands)
        hit = tints.get(round(s["tint"], 3), 0)
        st = "missing" if not cands else ("ok" if hit else "mismatch")
        ok += st == "ok"; bad += st == "mismatch"; miss += st == "missing"
        add = bool(s["flags"] & 0x20)
        blends = Counter(e["blend"] for e in cands if round(e["tint"], 3) == round(s["tint"], 3))
        # mtl1-41: predicted FUN_1417e1e50 bits (0x20 add && (!default || screenSpace || !alphaKill), 0x400) when the dump has them
        pred = Counter(e["flags"] & 0x420 for e in cands if "flags" in e and round(e["tint"], 3) == round(s["tint"], 3))
        res["keyed"].append({**s, "status": st, "libTints": dict(tints), "flagAdd": add, "libBlends": dict(blends),
                             "examples": [f'{e["effect"]}/{e["emitter"]}' for e in cands[:3]],
                             "predFlags": {hex(k): v for k, v in pred.items()}, "predOk": (s["flags"] & 0x420) in pred if pred else None})
    strict = [k for k in res["keyed"] if len(k["libTints"]) == 1]
    blend_ok = sum(1 for k in res["keyed"] if k["status"] == "ok" and ((k["flagAdd"] and "Add" in k["libBlends"]) or (not k["flagAdd"] and set(k["libBlends"]) - {"Add"})))
    res["summary"] = {"ok": ok, "mismatch": bad, "missing": miss, "unambiguous": len(strict),
                      "unambiguousTint0": sum(1 for k in strict if round(k["tint"], 3) == 0), "blendFlagConsistent": blend_ok,
                      "predFlagsOk": sum(1 for k in res["keyed"] if k["predOk"]), "predFlagsBad": [k["slot"] for k in res["keyed"] if k["predOk"] is False]}
    txt = json.dumps(res, indent=1)
    if len(argv) > 3:
        open(argv[3], "w").write(txt)
    print(json.dumps(res["summary"]), "tint values (capture):", res["tintValues"][:8])
    for k in res["keyed"]:
        if len(k["libTints"]) != 1: continue
        print(k["slot"], k["key"], k["contrast"], "tint", round(k["tint"], 3), k["status"], k["libTints"], "add" if k["flagAdd"] else "", k["libBlends"], k["examples"][:1])
    return 0 if bad == 0 else 1

if __name__ == "__main__":
    sys.exit(main(sys.argv))
