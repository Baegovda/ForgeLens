"""R35 split skeleton: sibling-mesh rule, protected paths and backup naming of tools/ue/ue_fix_split_skeletons.py."""
import importlib.util
import os
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("fss", os.path.join(HERE, "..", "..", "tools", "ue", "ue_fix_split_skeletons.py"))
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
P = "/Game/SM2_Work/"


class SplitSkeletonTests(unittest.TestCase):
    def test_hologram_localbake_is_split(self):
        lb = P + "Tests/sani_before/hologram_planet/localbake/"
        anims = {lb + "hologram_planet__anim": lb + "hologram_planet_Skeleton",
                 P + "Animations/hologram_planet/hologram_planet__anim": P + "Props/hologram_planet/hologram_planet_Skeleton"}
        meshes = {lb + "hologram_planet": P + "Props/hologram_planet/hologram_planet_Skeleton",
                  P + "Props/hologram_planet/hologram_planet": P + "Props/hologram_planet/hologram_planet_Skeleton"}
        s = m.find_splits(anims, meshes)
        self.assertEqual([x[0] for x in s], [lb + "hologram_planet__anim"])
        self.assertEqual(s[0][3], P + "Props/hologram_planet/hologram_planet_Skeleton")

    def test_protected_and_no_sibling(self):
        anims = {P + "Original/cc_titus_assault/a__x": "S1", P + "Animations/x/x__y": "S1"}
        meshes = {P + "Original/cc_titus_assault/cc_titus_assault": "S2"}
        self.assertEqual(m.find_splits(anims, meshes), [])
        self.assertTrue(m.is_protected("/Game/Work/02/Lightning_01"))

    def test_prefix_mesh_wins(self):
        f = P + "Tests/x/"
        meshes = {f + "a": "SA", f + "b": "SB"}
        self.assertEqual(m.owner_mesh(f + "b__run", list(meshes)), f + "b")
        self.assertIsNone(m.owner_mesh(f + "c__run", list(meshes)))

    def test_backup_next_free(self):
        have = {"/G/a_bak1", "/G/a_bak2"}
        self.assertEqual(m.next_backup(lambda p: p in have, "/G/a"), "/G/a_bak3")


if __name__ == "__main__":
    unittest.main()
