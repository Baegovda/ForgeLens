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
