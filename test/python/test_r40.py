"""R40: nmb_verify (fire-clip table vs Morpheme), gs.py batch short form, push_lock.sh, rea_version guard."""
import importlib.util, json, os, re, subprocess, sys, tempfile, unittest
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.join(HERE, "..", "..")
sys.path.insert(0, os.path.join(ROOT, "tools", "re")); sys.path.insert(0, os.path.join(ROOT, "tools", "re", "ghidra_serve"))
import nmb_verify as V, gs, rea_version
NMB = os.environ.get("SM2_NMB", "/workspace/rea/b7/nmb/primaris.nmb")

class ClaimedMatchesCSharp(unittest.TestCase):
    def test_inline_rows(self):   # PlayerFireClipsTests.cs InlineData rows == python port
        src = open(os.path.join(ROOT, "test", "LibSaber.SM2.Assets.Tests", "PlayerFireClipsTests.cs"), encoding="utf-8").read()
        rows = re.findall(r'InlineData\(([\d.]+), "(\w+)", "(\w+)", "(\w+)"\)', src)
        self.assertGreater(len(rows), 5)
        for w, a, b, ev in rows: self.assertEqual(V.claimed(float(w)), (a, b, ev), w)
    def test_flags(self):
        self.assertEqual(V.claimed(0.2, pc=True)[0], "tacticus_sht7_shoot_aim")
        self.assertEqual(V.claimed(1.51, ax=True)[0], "tacticus_sht16_shoot_aim_melta")
        self.assertEqual(V.claimed(2.1, pc=True)[1], "tacticus_sht12_shoot_blind")

class Assumptions(unittest.TestCase):
    def test_labels(self):
        a = V.assumptions(["Expression_2: no branch assigns OUT (default unknown; 0 assumed, HEURISTIC)", "SIMPLECONV"], 2.0, "SubtractiveBlend")
        self.assertTrue(any("unassigned" in x for x in a)); self.assertTrue(any("heavy" in x for x in a))
        self.assertTrue(any("SubtractiveBlend" in x for x in a)); self.assertTrue(any("isInAuxVision" in x for x in a))
        self.assertEqual(V.assumptions([], 1.1, "tacticus_sht2_shoot_blind"), [])
    def test_clip(self):
        self.assertEqual(V.clip("tacticus_sht5_shoot_blind (via SubtractiveBlend)"), "tacticus_sht5_shoot_blind")

@unittest.skipUnless(os.path.exists(NMB), "primaris.nmb not present")
class AgainstGameData(unittest.TestCase):
    def test_no_mismatch_and_labels(self):
        from nmb_reader import Nmb
        ws = [(l.split("\t")[0], float(l.split("\t")[1])) for l in open(os.path.join(ROOT, "docs", "handoff", "fire_clip_weapons.tsv")) if l.strip()]
        rows = V.verify(Nmb(NMB), ws)
        self.assertEqual([r for r in rows if r["label"] == "MISMATCH"], [])
        self.assertEqual([r for r in rows if r["label"] != r["table_ev"]], [])   # table labels == verified labels

class GsBatch(unittest.TestCase):
    def test_parse(self):
        self.assertEqual(gs.parse_q("sum 0x1 5"), {"tool": "summary", "arguments": {"address": "0x1", "pseudo_lines": 5}})
        self.assertEqual(gs.parse_q("str ho_shoot")["arguments"], {"pattern": "ho_shoot"})
        self.assertEqual(gs.parse_q('{"tool":"ping"}'), {"tool": "ping", "arguments": {}})
    def test_fmt(self):
        self.assertIn("0x10 READ f@0x1", gs.fmt_any("xrefs", {"count": 1, "xrefs": [{"from": "0x10", "type": "READ", "function": "f@0x1"}]}))
        self.assertTrue(gs.fmt_any("decompile", {"error": "x"}).startswith("ERROR"))

class PushLock(unittest.TestCase):
    def run_(self, *a, env=None):
        return subprocess.run(["bash", os.path.join(ROOT, "tools", "re", "box", "push_lock.sh"), *a], capture_output=True, text=True, env=env)
    def test_lock(self):
        d = tempfile.mkdtemp(); env = dict(os.environ, PUSH_LOCK=d + "/l")
        self.assertEqual(self.run_("acquire", "a", env=env).returncode, 0)
        self.assertEqual(self.run_("acquire", "b", env=env).returncode, 1)
        self.assertEqual(self.run_("release", "b", env=env).returncode, 1)
        self.assertEqual(self.run_("release", "a", env=env).returncode, 0)
        self.assertEqual(self.run_("acquire", "b", env=env).returncode, 0)
        env["PUSH_LOCK_STALE"] = "-1"
        r = self.run_("acquire", "c", env=env); self.assertEqual(r.returncode, 0); self.assertIn("stale", r.stdout)

class ReaGuard(unittest.TestCase):
    def test_guard(self):
        d = tempfile.mkdtemp(); p = os.path.join(d, "node_modules", "rea-agents"); os.makedirs(p)
        json.dump({"version": "6.8.0"}, open(os.path.join(p, "package.json"), "w")); self.assertEqual(rea_version.main([d]), 0)
        json.dump({"version": "6.9.0"}, open(os.path.join(p, "package.json"), "w")); self.assertEqual(rea_version.main([d]), 1)
        self.assertEqual(rea_version.main([tempfile.mkdtemp()]), 1)

if __name__ == "__main__":
    unittest.main()
