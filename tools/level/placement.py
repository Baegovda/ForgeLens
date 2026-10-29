# R54 level / prop placement: every cd_list object -> name, actor class, template (geom.nameTpl), world matrix.
# Read-only parse of <scene>.cd_list + <scene>.class_list (tools/level/cdlist.py, classlist.py; extracted game data).
# Template: the cd_list per-object override wins, else the class_list actor desc (src column c/k/-).
# Matrix: the 16 floats as stored (row-major, translation in m[12..14], game metres, Y-up; same convention as lights_tsv2).
# Usage: python3 placement.py <scene_dir> <scene_name> <out.tsv>
import re, sys, csv
from cdlist import parse as cdp
from classlist import parse as clp
TPL = re.compile(r'\bnameTpl = "([^"]*)"')
def actor_class(ps):
    m = re.findall(r'__type = "([^"]*__actor)"', ps)
    return m[-1] if m else ''
def kind(ps, cps):
    s = ps + cps
    for k in ("sfx_light", "prop_camera", "geom"):
        if k in s: return {"sfx_light": "light", "prop_camera": "camera", "geom": "geom"}[k]
    return "other"
def rows(scn, name):
    E = cdp(f'{scn}/{name}.cd_list'); C = clp(f'{scn}/{name}.class_list')
    for i, e in enumerate(E):
        ps = e.get('ps', ''); cls = actor_class(ps); cps = C.get(cls, '')
        t = TPL.findall(ps); src = 'c'
        if not t: t = TPL.findall(cps); src = 'k' if t else '-'
        m = e['m']
        yield [i, e['name'][3:] if e['name'].startswith('|x|') else e['name'], cls, kind(ps, cps), ';'.join(dict.fromkeys(t)), src,
               round(m[12], 4), round(m[13], 4), round(m[14], 4), ' '.join('%.6g' % x for x in m)]
def main(scn, name, out):
    w = csv.writer(open(out, 'w'), delimiter='\t', lineterminator='\n')
    w.writerow(['idx', 'name', 'class', 'kind', 'templates', 'tplsrc', 'x', 'y', 'z', 'matrix16'])
    n = 0
    for r in rows(scn, name): w.writerow(r); n += 1
    print(out, n)
if __name__ == '__main__':
    main(*sys.argv[1:4])
