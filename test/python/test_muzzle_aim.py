"""R51 muzzle_aim helpers."""
import os, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "re"))
import muzzle_aim as M  # noqa: E402


class MuzzleAimTests(unittest.TestCase):
    def test_shoot_tip(self):
        t = 'prop_firearm_type_settings = { wpnType = 1.51 } prop_firearm_shoot_tip = { shootTip = { locator = "shootTip" } shootTipByBarrelOverrides = { melta = { locator = "tip_gl" } } }'
        self.assertEqual(M.shoot_tip(t), ("shootTip", {"melta": "tip_gl"}))
        self.assertEqual(M.wpn_type(t), "1.51")

    def test_codes(self):
        self.assertEqual(M.wpn_codes("1.51"), ["151"])
        self.assertEqual(M.wpn_codes("1"), ["100", "10"])
        self.assertEqual(M.wpn_codes("0"), ["000", "00"])


class GlbNodeTests(unittest.TestCase):
    def test_world(self):
        import json, struct, tempfile
        g = {"nodes": [{"name": "body", "translation": [0, 0, 1], "children": [1]}, {"name": "shootTip", "translation": [0, 0, 0.5]}]}
        j = json.dumps(g).encode(); j += b" " * (-len(j) % 4)
        data = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(j)) + struct.pack('<II', len(j), 0x4E4F534A) + j
        with tempfile.NamedTemporaryFile(suffix=".glb", delete=False) as f: f.write(data)
        self.assertEqual(M.glb_node_world(f.name, {"shootTip"})["shootTip"], (0, 0, 1.5))
