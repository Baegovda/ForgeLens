"""mtl1-27: DYNAMIC_GLT per-instance tint helpers (exe FUN_1423bb1e0)."""
import importlib.util
import os
import sys
import types
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "..", "..", "src", "LibSaber.SM2.Assets", "Export", "import_sm2_materials.py")
sys.modules.setdefault("unreal", types.ModuleType("unreal"))
spec = importlib.util.spec_from_file_location("import_sm2_materials_it", SCRIPT)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class InstanceTintTests(unittest.TestCase):
    def test_no_override_keeps_default_path(self):
        self.assertIsNone(m.instance_tint_value({}))
        self.assertIsNone(m.instance_tint_value({"instanceTint": [1, 1, 1]}))

    def test_override(self):
        self.assertEqual(m.instance_tint_value({"instanceTint": [0.5, 1, 0.25]}), (0.5, 1.0, 0.25, 1.0))

    def test_decode_packed_color(self):
        r, g, b = m.decode_instance_color(0x00FF8000)
        self.assertAlmostEqual(r, 1.0)
        self.assertAlmostEqual(g, 128 / 255.0)
        self.assertAlmostEqual(b, 0.0)
        r, g, b = m.decode_instance_color(0x00FFFFFF, scale_byte=51, boost=True)
        self.assertAlmostEqual(r, 2.0)


if __name__ == "__main__":
    unittest.main()
