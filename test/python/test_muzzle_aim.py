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
