"""R49: colour scheme json -> UE master material instance values (import_sm2_materials)."""
import importlib.util, json, os, sys, tempfile, types, unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "..", "..", "src", "LibSaber.SM2.Assets", "Export", "import_sm2_materials.py")
sys.modules.setdefault("unreal", types.ModuleType("unreal"))
spec = importlib.util.spec_from_file_location("import_sm2_materials_cs", SCRIPT)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

PART = {"part": "chest_plate", "texture": "ch_titus_chest", "mask": "ch_titus_chest_cc", "albedo": "ch_titus_chest_a",
        "params": {"Layer0Enabled": 1, "Layer0Tint": [0.1, 0.2, 0.3, 1], "Layer0MetalTint": [0.5, 0.5, 0.5, 1],
                   "Layer0Metallic": 1, "Layer0MultiplyBase": 0, "Layer1Enabled": 0, "Layer2Enabled": 0, "Layer3Enabled": 0}}


class SchemeTests(unittest.TestCase):
    def test_values(self):
        vec, sca = m.scheme_values(PART)
        self.assertEqual(vec["Layer0Tint"], (0.1, 0.2, 0.3, 1.0))
        self.assertEqual(sca["Layer0Metallic"], 1.0)
        self.assertEqual(sca["Layer1Enabled"], 0.0)
        self.assertNotIn("Layer1Tint", vec)

    def test_match(self):
        s = {"parts": [PART]}
        self.assertIs(m.scheme_part_for(s, "CHEST_PLATE", None), PART)
        self.assertIs(m.scheme_part_for(s, "slot7", "ch_titus_chest"), PART)
        self.assertIsNone(m.scheme_part_for(s, "legs", "ch_titus_legs"))

    def test_find_only_when_single(self):
        with tempfile.TemporaryDirectory() as d:
            side = os.path.join(d, "x.materials.json")
            open(side, "w").write("{}")
            self.assertIsNone(m.find_scheme(side))
            open(os.path.join(d, "x.colour_scheme.a.json"), "w").write("{}")
            self.assertTrue(m.find_scheme(side).endswith("x.colour_scheme.a.json"))
            open(os.path.join(d, "x.colour_scheme.b.json"), "w").write("{}")
            self.assertIsNone(m.find_scheme(side))


if __name__ == "__main__":
    unittest.main()
