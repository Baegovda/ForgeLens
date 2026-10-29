"""level-lights: vfxframe_scene.falloff = window * P0.w / (d^2 + P0.w) (CaptureLightUnits.SurfaceAttenuation)."""
import os
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "..", "tools", "vfx"))
import vfxframe_scene as S  # noqa: E402


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


if __name__ == "__main__":
    unittest.main()
