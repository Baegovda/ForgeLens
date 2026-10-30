"""oneclick: destination rules and backup naming of tools/ue/ue_forge_direct.py (same rule as UnrealContentPath)."""
import importlib.util
import os
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "..", "..", "tools", "ue", "ue_forge_direct.py")
spec = importlib.util.spec_from_file_location("ue_forge_direct", SCRIPT)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class ForgeDirectTests(unittest.TestCase):
    def test_dest_rules(self):
        ok = ["/Game/SM2_Work/Tests/oneclick/wpn", "/Game/SM2_Work/Props/wpn", "/Game/Workshop/a", "/Game/ForgeLens/SM2/Characters/cc_x", "/Game/ForgeLens/POE/Props/y"]
        bad = ["/Game/SM2_Work/Original/x", "/game/sm2_work/ORIGINAL", "/Game/Work", "/Game/Work/x", "/Game/SM2_Work/Props",
               "/Game/SM2_Work/Tests/", "/Game/X", "/Engine/a/b", "/Game/SM2_Work/Props/a b", "/Game/SM2_Work/Props/../Original", "/Game/ForgeLens", "/Game/ForgeLens/SM2", "/Game/ForgeLens/SM2/Props"]
        for p in ok:
            self.assertIsNone(m.dest_error(p), p)
        for p in bad:
            self.assertIsNotNone(m.dest_error(p), p)

    def test_next_backup_skips_existing(self):
        have = {"/Game/a/p_bak1", "/Game/a/p_bak2"}
        self.assertEqual(m.next_backup(lambda f: f in have, "/Game/a/p"), "/Game/a/p_bak3")
        self.assertEqual(m.next_backup(lambda f: False, "/Game/a/p"), "/Game/a/p_bak1")


if __name__ == "__main__":
    unittest.main()
