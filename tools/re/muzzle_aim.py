# muzzle_aim.py (R51): per firearm client cls -> shoot-tip locator (+ barrel overrides), muzzle sfx slot, wpnType,
# and the player aim-hold clip for that wpnType (tacticus_wpn<N>_aim_hold*.sani). Read-only parse of extracted game data.
# Usage: python muzzle_aim.py <cls_root> <sani_root> <out.tsv>
import os, re, sys, csv
def block(t, key):
    m = re.search(r'\b' + key + r'\s*=\s*\{', t)
    if not m: return None
    i = m.end(); d = 1
    while d and i < len(t):
        d += {'{': 1, '}': -1}.get(t[i], 0); i += 1
    return t[m.end():i - 1]
def shoot_tip(t):
    b = block(t, 'prop_firearm_shoot_tip')
    if b is None: return None, {}
    st = block(b, 'shootTip') or ''
    loc = re.search(r'locator\s*=\s*"([^"]*)"', st)
    ov = {}
    o = block(b, 'shootTipByBarrelOverrides')
    if o:
        for k, l in re.findall(r'(\w+)\s*=\s*\{\s*locator\s*=\s*"([^"]*)"', o): ov[k] = l
    return (loc.group(1) if loc else None), ov
def wpn_type(t):
    b = block(t, 'prop_firearm_type_settings') or ''
    m = re.search(r'wpnType\s*=\s*([0-9.]+)', b); return m.group(1) if m else ''
def muzzle_sfx(t):
    return ';'.join(dict.fromkeys(re.findall(r'muzzleSfx\s*=\s*"([^"]*)"', t)))
def wpn_codes(wt):
    # wpnType a.bc -> clip code a + bc (two digits), then the same without a trailing 0: 1.51->151, 1.1->110, 0->000,
    # 1->100|10, 1.7->170|17. INFERRED from the 22 tacticus_wpn<code>_aim_hold clips (no exe/data link read).
    if not wt: return []
    a, _, b = wt.partition('.'); c = a + (b + '00')[:2]
    return [c] + ([c[:-1]] if c.endswith('0') else [])
def main(cls_root, sani_root, out):
    clips = {}
    for dp, _, fs in os.walk(sani_root):
        for f in fs:
            m = re.match(r'tacticus_wpn(\d+)_aim_hold(_[a-z_]+)?_intercessor\.sani$', f)
            if m: clips.setdefault(m.group(1), []).append(f)
    w = csv.writer(open(out, 'w', newline=''), delimiter='\t', lineterminator='\n')
    w.writerow(['cls', 'wpnType', 'shootTip', 'barrelOverrides', 'muzzleSfx', 'aimHoldClips'])
    n = 0
    for dp, _, fs in os.walk(cls_root):
        for f in fs:
            if not f.endswith('_client.cls'): continue
            t = open(os.path.join(dp, f), encoding='utf-8', errors='replace').read()
            loc, ov = shoot_tip(t)
            wt = wpn_type(t)
            if loc is None and not wt: continue
            loc = loc or '(inherited)'
            w.writerow([f, wt, loc, ';'.join('%s=%s' % kv for kv in sorted(ov.items())), muzzle_sfx(t), ';'.join(sorted(next((clips[c] for c in wpn_codes(wt) if c in clips), [])))]); n += 1
    print(out, n, 'aim-hold codes', len(clips))
if __name__ == '__main__':
    main(*sys.argv[1:4])


# R51: world position (metres, glTF Y-up) of named nodes in an exported .glb, e.g. shootTip / tip.
def _mat(x):
    if 'matrix' in x:
        m = x['matrix']; return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    tx, ty, tz = x.get('translation', [0, 0, 0]); qx, qy, qz, qw = x.get('rotation', [0, 0, 0, 1]); sx, sy, sz = x.get('scale', [1, 1, 1])
    R = [[1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
         [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
         [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)]]
    s = (sx, sy, sz)
    return [[R[r][0] * s[0], R[r][1] * s[1], R[r][2] * s[2], (tx, ty, tz)[r]] for r in range(3)] + [[0, 0, 0, 1]]
def _mul(a, b): return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
def glb_node_world(path, names):
    import json, struct
    d = open(path, 'rb').read(); n = struct.unpack_from('<I', d, 12)[0]; nodes = json.loads(d[20:20 + n])['nodes']
    par = {c: i for i, x in enumerate(nodes) for c in x.get('children', [])}
    out = {}
    for i, x in enumerate(nodes):
        if x.get('name') in names:
            m = _mat(x); j = i
            while j in par: j = par[j]; m = _mul(_mat(nodes[j]), m)
            out[x['name']] = (m[0][3], m[1][3], m[2][3])
    return out
