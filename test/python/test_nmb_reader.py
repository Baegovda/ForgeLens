"""R38: tools/re/nmb_reader.py expression bytecode interpreter (no game data needed)."""
import importlib.util
import os
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("nmb", os.path.join(HERE, "..", "..", "tools", "re", "nmb_reader.py"))
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

# Expression_4 of minizoom|shoot|shoot_state, bytes as stored (b0, b1, b2, op): character_type == 13 ? 1 : 0
EXPR4_CONSTS = {-1: 13.0, -2: 1.0, -3: 0.0}
EXPR4_CODE = [(2, 0xff, 0, 0xa4), (0, 0xfe, 1, 0x04), (1, 0, 0, 0xc0), (0, 0xfd, 1, 0x04)]
# Expression_1 head: wpn_type == 0.1 | (wpn_type == 0.2 & plasma_charged == 0) -> 0, else wpn_type == 0 -> 1
EXPR1_CONSTS = {-1: 0.1, -2: 0.2, -3: 0.0, -4: 1.0}
EXPR1_CODE = [(2, 0xff, 0, 0xa5), (3, 0xfe, 0, 0xa4), (2, 0xfd, 1, 0xa4), (0, 0xfd, 2, 0x04), (3, 0, 0, 0xc0),
              (1, 0xfd, 0, 0xa4), (0, 0xfc, 2, 0x04)]


class NmbExprTests(unittest.TestCase):
    def test_equality_branch(self):
        self.assertEqual(m.run_expr(EXPR4_CONSTS, EXPR4_CODE, [13.0], 1), {1.0})
        self.assertEqual(m.run_expr(EXPR4_CONSTS, EXPR4_CODE, [0.0], 1), {0.0})

    def test_or_and_chain(self):
        self.assertEqual(m.run_expr(EXPR1_CONSTS, EXPR1_CODE, [0.1, 1.0], 2), {0.0})
        self.assertEqual(m.run_expr(EXPR1_CONSTS, EXPR1_CODE, [0.2, 0.0], 2), {0.0})
        self.assertEqual(m.run_expr(EXPR1_CONSTS, EXPR1_CODE, [0.2, 1.0], 2), {None})
        self.assertEqual(m.run_expr(EXPR1_CONSTS, EXPR1_CODE, [0.0, 0.0], 2), {1.0})

    def test_undecoded_op_reports_both_paths(self):
        code = [(1, 0x04, 2, 0x98), (0, 0xff, 1, 0x04)]
        self.assertEqual(m.run_expr({-1: 4.0}, code, [0.0, 0.0], 1), {None, 4.0})


if __name__ == "__main__":
    unittest.main()
