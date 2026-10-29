"""RE toolbox: cache index builder, recall lookup, GhidraServe client formatting (no Ghidra needed)."""
import io, os, subprocess, sys, unittest
from contextlib import redirect_stdout
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "tools", "re")); sys.path.insert(0, os.path.join(ROOT, "tools", "re", "ghidra_serve"))
import re_cache_build, recall, gs

class ReTools(unittest.TestCase):
    def test_addrs_normalised(self):
        self.assertEqual(re_cache_build.addrs("FUN_141AB9B60 and 0x141ab9b60, DAT_144f9cee0"), ["0x141ab9b60", "0x144f9cee0"])
        self.assertEqual(recall.norm("FUN_14224ecf0"), "0x14224ecf0")
        self.assertEqual(recall.norm("Trail"), "trail")

    def test_label(self):
        self.assertEqual(re_cache_build.label("PROVEN x PROVEN y INFERRED"), "PROVEN")
        self.assertEqual(re_cache_build.label("nothing"), "NOTED")

    def test_index_has_known_conclusions(self):
        subprocess.check_call([sys.executable, os.path.join(ROOT, "tools/re/re_cache_build.py")], stdout=subprocess.DEVNULL)
        lines = open(os.path.join(ROOT, "docs/handoff/RE_CACHE/INDEX.tsv"), encoding="utf-8").read().splitlines()
        self.assertIn("4c4fb3a0", lines[0])
        self.assertEqual(lines[1].split("\t"), ["topic", "addresses", "conclusion", "label", "file"])
        self.assertTrue(all(len(l.split("\t")) == 5 for l in lines[1:]))
        buf = io.StringIO()
        with redirect_stdout(buf): recall.main(["FUN_141ab9b60"])
        self.assertIn("[PROVEN]", buf.getvalue())

    def test_summary_format(self):
        r = {"name": "FUN_1", "entry": "0x1", "size": 10, "callers": 2, "ms": 3.0, "signature": "void f(void)",
             "calls": ["memcpy@0x2"], "strings": ['0x3 "Trail"'], "globals": ["DAT@0x4"], "globals_total": 1, "constants": ["0x50"], "code": "a\nb\n"}
        out = gs.fmt_summary(r)
        for s in ("FUN_1 @0x1", "calls: memcpy@0x2", 'str: 0x3 "Trail"', "const: 0x50", "    b"): self.assertIn(s, out)
        self.assertTrue(gs.fmt_summary({"error": "x"}).startswith("ERROR"))

if __name__ == "__main__":
    unittest.main()
