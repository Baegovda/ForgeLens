"""mtl1-40: distortion gate + bilinear resample (vfxframe_scene.distort_resample) and tint_check matching."""
import json
import os
import subprocess
import sys
import tempfile
import unittest

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.join(HERE, "..", "..", "tools", "vfx")
sys.path.insert(0, TOOLS)
import vfxframe_scene as S  # noqa: E402


class DistortResampleTests(unittest.TestCase):
    def setUp(self):
        n = 32
        self.n = n
        self.hdr = np.tile(np.arange(n, dtype=np.float32)[None, :, None], (n, 1, 3))  # value = x

    def test_fractional_shift_is_bilinear_inside_footprint(self):
        n = self.n; sh = np.zeros((n, n, 2), np.float32); cov = np.zeros((n, n), bool)
        cov[:, 4:28] = True; sh[:, 8:16, 0] = 2.5 / n
        out, k = S.distort_resample(self.hdr, sh, cov)
        self.assertEqual(k, 8 * n)
        np.testing.assert_allclose(out[5, 8:16, 0], np.arange(8, 16) + 2.5, atol=1e-4)
        np.testing.assert_allclose(out[5, 20, 0], 20.0)

    def test_gate_rejects_samples_outside_footprint(self):
        n = self.n; sh = np.zeros((n, n, 2), np.float32); cov = np.zeros((n, n), bool)
        cov[:, 4:12] = True; sh[:, 4:12, 0] = 3.0 / n
        out, k = S.distort_resample(self.hdr, sh, cov)
        # x + 3 must be < 12: x in 4..8 shift, 9..11 keep
        np.testing.assert_allclose(out[0, 4:12, 0], [7, 8, 9, 10, 11, 9, 10, 11], atol=1e-4)
        self.assertEqual(k, 5 * n)

    def test_zero_shift_untouched(self):
        n = self.n; sh = np.zeros((n, n, 2), np.float32); cov = np.ones((n, n), bool)
        out, k = S.distort_resample(self.hdr, sh, cov)
        self.assertEqual(k, 0); np.testing.assert_array_equal(out, self.hdr)


class TintCheckTests(unittest.TestCase):
    def test_tint_check_matches_by_key(self):
        # 6 float4 per emitter: [0].z tint, [1].rgb key (0..255) .w contrast, [3].x flags (0x20 add)
        rec = np.zeros((2, 6, 4), np.float32)
        rec[0, 0, 2] = 0.0; rec[0, 1] = [237, 230, 24, 30]; rec[0, 3, 0] = np.array([0x20], np.uint32).view(np.float32)[0]  # flags are uint bits
        rec[1, 0, 2] = 0.25; rec[1, 1] = [10, 20, 30, 5]; rec[1, 3, 0] = 0
        em = [{"effect": "a", "emitter": "e1", "tint": 0.0, "contrast": 30, "key": [237, 230, 24], "blend": "Add"},
              {"effect": "b", "emitter": "e2", "tint": 0.25, "contrast": 5, "key": [10, 20, 30], "blend": "Blend"}]
        with tempfile.TemporaryDirectory() as d:
            b = os.path.join(d, "shared.bin"); e = os.path.join(d, "em.json"); o = os.path.join(d, "out.json")
            rec.tofile(b)
            with open(e, "w") as f: json.dump(em, f)
            r = subprocess.run([sys.executable, os.path.join(TOOLS, "tint_check.py"), b, e, o], capture_output=True, text=True)
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            with open(o) as f: res = json.load(f)["summary"]
            self.assertEqual(res["mismatch"], 0); self.assertEqual(res["ok"], 2); self.assertEqual(res["blendFlagConsistent"], 2)
            em[0]["tint"] = 1.0
            with open(e, "w") as f: json.dump(em, f)
            r = subprocess.run([sys.executable, os.path.join(TOOLS, "tint_check.py"), b, e], capture_output=True, text=True)
            self.assertEqual(r.returncode, 1)


if __name__ == "__main__":
    unittest.main()
