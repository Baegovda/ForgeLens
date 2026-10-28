"""mtl1-26: chameleon helpers of import_sm2_materials.py against the frame-68141 capture (FORMULA.md)."""
import importlib.util
import os
import sys
import types
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "..", "..", "src", "LibSaber.SM2.Assets", "Export", "import_sm2_materials.py")
sys.modules.setdefault("unreal", types.ModuleType("unreal"))
spec = importlib.util.spec_from_file_location("import_sm2_materials", SCRIPT)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

HELLBRUTE = {  # sidecar of cc_hellbrute slot 6 (blue), = CB_STATIC_GLT rows 10..15 of draws 2945/3430
    "colors": [[0, 0, 0, 0], [0, 1, 1, 1], [0.13725491, 0.6313726, 0.74509805, 1], [0.07058824, 0.9098039, 0.76862746, 1]],
    "offset1": 0.5, "offset2": 0.9, "fresnelPwr": 0.7,
}


class ChameleonTests(unittest.TestCase):
    def test_ramp_rows_match_capture_cb(self):
        p = m.chameleon_params(HELLBRUTE)
        # Shader steps: sat((t-0)*2), sat((t-0.5)*2.5), sat((t-0.9)*10); fresnel power 0.7.
        r0, r1 = p["ChameleonR0"], p["ChameleonR1"]
        for got, want in zip(r0, (0.0, 2.0, 0.5, 2.5)):
            self.assertAlmostEqual(got, want, places=4)
        for got, want in zip(r1[:3], (0.9, 10.0, 0.7)):
            self.assertAlmostEqual(got, want, places=3)

    def test_colours_stay_srgb(self):
        p = m.chameleon_params(HELLBRUTE)
        self.assertAlmostEqual(p["ChameleonC2"][1], 0.6313726, places=6)  # not linearised
        self.assertEqual(p["ChameleonC1"], (0.0, 1.0, 1.0, 1.0))

    def test_missing_fields_use_capture_defaults(self):
        a, b = m.chameleon_params({}), m.chameleon_params(HELLBRUTE)
        for key in a:
            for x, y in zip(a[key], b[key]):
                self.assertAlmostEqual(x, y, places=5)
        partial = m.chameleon_params({"offset1": 0.3})
        self.assertAlmostEqual(partial["ChameleonR0"][2], 0.3, places=6)
        self.assertAlmostEqual(partial["ChameleonR1"][2], 0.7, places=6)

    def test_explicit_zero_is_kept(self):
        # The exe parser defaults to 0; an exported 0 must not be replaced by the capture default.
        p = m.chameleon_params({"colors": HELLBRUTE["colors"], "offset1": 0.0, "offset2": 0.0, "fresnelPwr": 0.0})
        self.assertAlmostEqual(p["ChameleonR1"][2], 0.0)

    def test_poly_matches_shader(self):
        self.assertAlmostEqual(m.srgb_to_linear_poly(1.0), 0.305306 + 0.682171 + 0.0125229, places=6)
        self.assertAlmostEqual(m.srgb_to_linear_poly(0.5), 0.5 * (0.5 * (0.305306 * 0.5 + 0.682171) + 0.0125229), places=7)

    def test_multiply_in_srgb_then_linearise(self):
        # Ramp at t = pow(N.V, 0.7) = 0.75: c = lerp(C1, C2, 0.625); white texture.
        ndotv = 0.75 ** (1 / 0.7)
        got = m.chameleon_albedo_reference((1.0, 1.0, 1.0), ndotv, HELLBRUTE)
        c = [1 + (b - 1) * 0.625 if k else 0.137255 * 0.625 for k, b in enumerate((0.137255, 0.631373, 0.745098))]
        for g, ck in zip(got, c):
            self.assertAlmostEqual(g, m.srgb_to_linear_poly(ck), places=4)

    def test_capture_gbuffer_albedo_in_range(self):
        # GBUFFER_0 albedo of draw 2956 (arm_02 blue) median sRGB (1, 89, 120): red ~0, blue > green, both in the
        # cyan zone. With a mid-grey sRGB texture and N.V ~ 0.6 the reference gives the same hue order.
        lin = m.chameleon_albedo_reference((0.5, 0.5, 0.5), 0.6, HELLBRUTE)
        self.assertLess(lin[0], 0.01)
        self.assertGreater(lin[2], lin[1] * 0.9)

    def test_metal_whitens(self):
        a = m.chameleon_albedo_reference((0.5, 0.5, 0.5), 0.6, HELLBRUTE, metal=1.0)
        for v in a:
            self.assertAlmostEqual(v, m.srgb_to_linear_poly(0.5), places=6)


if __name__ == "__main__":
    unittest.main()
