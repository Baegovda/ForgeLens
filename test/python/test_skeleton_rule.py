"""R43: one skeleton per mesh + its anims (tools/ue/sm2_skeleton_rule.py, uasset_skel_scan.py, importer wiring)."""
import importlib.util, os, re, tempfile, unittest

HERE = os.path.dirname(os.path.abspath(__file__)); UE = os.path.join(HERE, "..", "..", "tools", "ue")


def load(n):
    spec = importlib.util.spec_from_file_location(n, os.path.join(UE, n + ".py")); m = importlib.util.module_from_spec(spec)
    import sys; sys.path.insert(0, UE); spec.loader.exec_module(m); return m


R = load("sm2_skeleton_rule")
P = "/Game/SM2_Work/"; LB = P + "Tests/sani_before/hologram_planet/localbake/"; PR = P + "Props/hologram_planet/"


class Rule(unittest.TestCase):
    def test_reuse_same_folder_first(self):
        sk = [LB + "hologram_planet_Skeleton", PR + "hologram_planet_Skeleton"]
        self.assertEqual(R.pick_mesh_skeleton(PR.rstrip("/"), "hologram_planet", sk), PR + "hologram_planet_Skeleton")

    def test_second_copy_unifies_onto_existing(self):
        # importing the mesh into a new folder must reuse the one existing skeleton, not make a same-named second one
        sk = [PR + "hologram_planet_Skeleton", PR + "hologram_planet_Skeleton_bak1"]
        self.assertEqual(R.pick_mesh_skeleton(LB.rstrip("/"), "hologram_planet", sk), PR + "hologram_planet_Skeleton")

    def test_collision_fails_loudly(self):
        sk = [LB + "hologram_planet_Skeleton", PR + "hologram_planet_Skeleton"]
        with self.assertRaises(R.SkeletonRuleError) as c:
            R.pick_mesh_skeleton(P + "Tests/new", "hologram_planet", sk)
        self.assertIn("SKELCOLLISION", str(c.exception))
        self.assertEqual(list(R.name_collisions(sk)), ["hologram_planet_skeleton"])

    def test_fresh_mesh_gets_none(self):
        self.assertIsNone(R.pick_mesh_skeleton(P + "Props/x", "x", [PR + "hologram_planet_Skeleton"]))

    def test_check_anims_raises_on_split(self):
        R.check_anims([(LB + "a", PR + "S")], PR + "S")
        with self.assertRaises(R.SkeletonRuleError):
            R.check_anims([(LB + "hologram_planet__anim", LB + "hologram_planet_Skeleton")], PR + "hologram_planet_Skeleton")

    def test_audit_reported_case(self):
        anims = {LB + "hologram_planet__anim": LB + "hologram_planet_Skeleton",
                 "/Game/Characters/Humanoids/Sci_Fi_Cyborg_03/Demo/Aim_Idle": "/Game/Characters/Mesh/SK_Sci_Fi_Cyborg_03_Skeleton"}
        meshes = {LB + "hologram_planet": PR + "hologram_planet_Skeleton", PR + "hologram_planet": PR + "hologram_planet_Skeleton"}
        skels = [LB + "hologram_planet_Skeleton", PR + "hologram_planet_Skeleton"]
        a = R.audit(anims, meshes, skels)
        self.assertEqual([x[0] for x in a["splits"]], [LB + "hologram_planet__anim"])
        self.assertIn("hologram_planet_skeleton", a["collisions"])
        self.assertEqual(a["dangling"], [("/Game/Characters/Humanoids/Sci_Fi_Cyborg_03/Demo/Aim_Idle", "/Game/Characters/Mesh/SK_Sci_Fi_Cyborg_03_Skeleton")])
        self.assertIn("/Game/Characters/Mesh/SK_Sci_Fi_Cyborg_03_Skeleton", a["orphan_skeletons"])

    def test_anims_subfolder_owner(self):
        self.assertEqual(R.owner_mesh(P + "Props/x/Anims/x__run", [P + "Props/x/x"]), P + "Props/x/x")


class Wiring(unittest.TestCase):
    def test_importers_use_rule(self):
        for f in ("ue_bulk.py", "ue_forge_direct.py", "ue_anim_survey.py", "ue_sani_fix.py"):
            s = open(os.path.join(UE, f)).read()
            self.assertIn("sm2_mesh_skeleton(", s, f); self.assertIn("o.skeleton = _sk", s, f)

    def test_anim_block_raises_and_is_identical(self):
        blocks = []
        for f in ("ue_import_anim.py", "ue_anim_survey.py", "ue_sani_fix.py"):
            s = open(os.path.join(UE, f)).read()
            b = re.search(r"# --- sm2_anim_import.*?# --- end sm2_anim_import ---", s, re.S).group(0)
            self.assertIn('raise RuntimeError("SPLITSKEL', b, f); blocks.append(b)
        self.assertEqual(len(set(blocks)), 1)


if __name__ == "__main__":
    unittest.main()
