"""R54 level placement helpers."""
import os, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "level"))
import placement as P  # noqa: E402


class PlacementTests(unittest.TestCase):
    def test_class_kind_tpl(self):
        ps = 'a = {geom = {nameTpl = "refrt_x_01";};};__type = "lvl__x__actor";'
        self.assertEqual(P.actor_class(ps), "lvl__x__actor")
        self.assertEqual(P.kind(ps, ""), "geom")
        self.assertEqual(P.kind("", "sfx_light"), "light")
        self.assertEqual(P.TPL.findall(ps), ["refrt_x_01"])
