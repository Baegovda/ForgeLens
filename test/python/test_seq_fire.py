"""R59 ue_seq_fire: aim-clip column lookup (no UE needed)."""
import os, sys, unittest
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
sys.path.insert(0, os.path.join(R, "tools", "ue"))
import ue_seq_fire as S  # noqa: E402
TSV = os.path.join(R, "docs", "handoff", "MUZZLE_AIM.tsv")


class SeqFireTests(unittest.TestCase):
    def test_bolt_rifle(self):
        self.assertEqual(S.aim_clip(TSV, "arifle_bolt_rifle_client.cls"), "tacticus_wpn10_aim_hold_intercessor")

    def test_first_of_many(self):
        self.assertEqual(S.aim_clip(TSV, "arifle_bolt_rifle_combimelta_client.cls"), "tacticus_wpn151_aim_hold_intercessor")

    def test_missing(self):
        with self.assertRaises(SystemExit):
            S.aim_clip(TSV, "arifle_heavy_bolt_rifle_heroic_01_client.cls")


if __name__ == "__main__":
    unittest.main()
