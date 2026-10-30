import os, sys, tempfile; sys.path.insert(0, os.path.dirname(__file__))
from placement_view import load, main
def test_roundtrip():
    d = tempfile.mkdtemp(); p = os.path.join(d, 'a.tsv')
    open(p, 'w').write('idx\tname\tclass\tkind\ttemplates\ttplsrc\tx\ty\tz\tmatrix16\n0\tn\tc\tlight\tt1;t2\tk\t1\t2\t3\t2 0 0 0 0 2 0 0 0 0 2 0 1 2 3 1\n')
    r = load(p); assert r == [['l', 1.0, 2.0, 3.0, 2.0, 't1']]
    main(p, os.path.join(d, 'o.html')); assert '"t1"' in open(os.path.join(d, 'o.html')).read()
