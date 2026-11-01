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

    def test_p0w_proven(self):
        self.assertAlmostEqual(L.p0w(0.0), 1.0, places=6)
        self.assertAlmostEqual(L.p0w(1.0), 300.0, places=4)
        self.assertAlmostEqual(L.p0w(1.0, use_inverse_squared_falloff=True), 0.0025, places=7)
        self.assertEqual(L.p0w(1.0, is_const_atten=True), 8e22)
        self.assertEqual(L.p0w(1.0, directional=True), 0.0)

    def test_spot_stays_spot_no_fit_k(self):
        row = {
            "lightType": "spot",
            "x": 1,
            "y": 2,
            "z": 3,
            "Intensity": 8,
            "ColorCtrl": "[255,165,207,226]",
            "RadiusCtrl": 10,
            "attenCurveParam": 1,
            "spotAngle": 110,
            "spotInnerAngleFactor": 0.04,
        }
        p = L.light_row_to_ue(row, spot_scale=1.0)
        self.assertEqual(p["lightType"], "spot")
        self.assertEqual(p["authoredType"], "spot")
        self.assertAlmostEqual(p["intensity"], 8.0, places=6)  # no K=10
        self.assertAlmostEqual(p["outer_cone_degrees"], 110.0, places=5)
        self.assertAlmostEqual(p["inner_cone_degrees"], 110.0 * 0.04, places=5)
        self.assertAlmostEqual(p["p0w"], 300.0, places=4)
        self.assertIn("PROVEN", p["evidence"])
        self.assertNotIn("HEURISTIC FIT K=10", p["evidence"])
        self.assertIn("PROVEN", L.COLOR_ORDER_EVIDENCE)

    def test_inv_sq_scales_intensity_x400(self):
        row = {
            "lightType": "point",
            "x": 0,
            "y": 0,
            "z": 0,
            "Intensity": 2,
            "ColorCtrl": "[255,255,255,255]",
            "RadiusCtrl": 15,
            "useInverseSquaredFalloff": "true",
        }
        p = L.light_row_to_ue(row)
        self.assertAlmostEqual(p["intensity"], 800.0, places=5)
        self.assertTrue(p["use_inverse_squared"])
        self.assertAlmostEqual(p["p0w"], 0.0025, places=7)


if __name__ == "__main__":
    unittest.main()
