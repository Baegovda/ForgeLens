"""level-lights: vfxframe falloff + shared UE hub-light core (LevelLightUe / ue_level_lights_core)."""
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "tools", "vfx"))
sys.path.insert(0, os.path.join(HERE, "..", "..", "tools", "ue"))
import vfxframe_scene as S  # noqa: E402
import ue_level_lights_core as L  # noqa: E402


class FalloffTests(unittest.TestCase):
    def test_default_soft_is_one(self):
        w = S.attenuation(2.5, 5.0)
        self.assertAlmostEqual(S.falloff(2.5, 5.0), w / (2.5 ** 2 + 1.0), places=6)
        self.assertAlmostEqual(S.falloff(2.5, 5.0, None), w / (2.5 ** 2 + 1.0), places=6)

    def test_attencurve_one_is_300(self):
        self.assertAlmostEqual(S.falloff(2.0, 10.0, 300.0), S.attenuation(2.0, 10.0) * 300.0 / 304.0, places=6)

    def test_const_sphere(self):
        self.assertEqual(S.falloff(4.9, 5.0, -1), 1.0)
        self.assertEqual(S.falloff(5.1, 5.0, -1), 0.0)


class HubLightCoreTests(unittest.TestCase):
    def test_argb_order_and_gamma(self):
        r, g, b = L.color_ctrl_to_linear_rgb("[255,165,207,226]")
        self.assertAlmostEqual(r, (165 / 255.0) ** 2.2, places=6)
        self.assertAlmostEqual(g, (207 / 255.0) ** 2.2, places=6)
        self.assertAlmostEqual(b, (226 / 255.0) ** 2.2, places=6)

    def test_metres_to_ue_cm(self):
        self.assertEqual(L.metres_to_ue_cm(1, 2, 3), (100.0, 300.0, 200.0))

    def test_spot_becomes_point_and_fit_k(self):
        row = {
            "lightType": "spot",
            "x": 1,
            "y": 2,
            "z": 3,
            "Intensity": 8,
            "ColorCtrl": "[255,165,207,226]",
            "RadiusCtrl": 10,
        }
        p = L.light_row_to_ue(row, k=10, spot_scale=1.0)
        self.assertEqual(p["lightType"], "point")
        self.assertEqual(p["authoredType"], "spot")
        self.assertAlmostEqual(p["intensity"], 80.0, places=6)
        self.assertIn("HEURISTIC", p["evidence"])
        self.assertIn("PROVEN", L.COLOR_ORDER_EVIDENCE)


if __name__ == "__main__":
    unittest.main()
