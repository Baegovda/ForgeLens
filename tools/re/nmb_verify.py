#!/usr/bin/env python3
"""nmb_verify.py - check the shipped fire-clip table (PlayerFireClips.cs / FIRE_CLIP.md) against primaris.nmb (R40).

Usage: nmb_verify.py <primaris.nmb> [--weapons docs/handoff/fire_clip_weapons.tsv] [--out report.tsv] [--all-states]
For every weapon x plasma_charged x isInAuxVision it evaluates the network with nmb_reader.select() (aim =
minizoom shoot_state, blind = loco2 blind_whole_anim shoot_state), compares with claimed(), a line-by-line port of
PlayerFireClips.For, lists the assumptions the evaluation needed and re-labels the row:
  INFERRED  = network decodes to one clip with no assumption, matches the table (layout itself is INFERRED, U8)
  HEURISTIC = a value was assumed (unassigned OUT -> 0, undecoded op, missing control param, heavy floor into rifle branch,
              unindexed SubtractiveBlend)
  MISMATCH  = table and network disagree  (exit code 1)
--all-states also evaluates the variants (shoot_state_1, wham, whole_anim_1, shield...) and reports divergences.
Nothing here is PROVEN in-game; a visual check (VISUAL_CHECKS V3) is still needed."""
import os, re, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nmb_reader import Nmb, select

AIM = 'anim|LOCO|minizoom|shoot|shoot_state'
BLIND = 'anim|LOCO|loco2|blind_shoot|blind_whole_anim|shoot|shoot_state'
VAR_RX = r'^anim\|LOCO\|(minizoom|loco2\|blind_shoot\|[^|]+|shield\|blind_shoot\|[^|]+)\|shoot\|shoot_state(_1)?$'
f32 = lambda x: struct.unpack('<f', struct.pack('<f', x))[0]
eq = lambda a, b: f32(a) == f32(b)

def claimed(w, pc=False, ax=False):
    """Port of PlayerFireClips.For -> (aim, blind, evidence). Keep in sync with the C# (test_nmb_verify pins rows)."""
    A = lambda n: 'tacticus_sht%d_shoot_aim' % n; B = lambda n: 'tacticus_sht%d_shoot_blind' % n
    if w < 1:
        if eq(w, .1) or (eq(w, .2) and not pc): n = 6
        elif eq(w, 0) or (eq(w, .2) and pc): n = 7
        elif eq(w, .3) or eq(w, .4): n = 14
        else: return A(6), B(6), 'HEURISTIC'
        return A(n), B(n), 'INFERRED'
    if w < 2:
        g = None
        if any(eq(w, v) for v in (1.03, 1.11, 1.2, 1.21)) or (eq(w, 1.3) and not pc): g = 1
        elif any(eq(w, v) for v in (1.1, 1.13, 1.6)) or (eq(w, 1.3) and pc): g = 2
        elif eq(w, 1.12): g = 3
        if g: return A(g), B(g), 'INFERRED'
        if eq(w, 1.4): return A(5), B(5), 'HEURISTIC'
        if eq(w, 1.41): return A(1), B(5), 'HEURISTIC'
        if eq(w, 1.25): return A(15), B(15), 'INFERRED'
        if eq(w, 1.51): return ('tacticus_sht16_shoot_aim_melta' if ax else A(16)), B(16), 'HEURISTIC'
        if eq(w, 1.7): return A(15), B(16), 'HEURISTIC'
        if eq(w, 1.5): return A(2), B(16), 'HEURISTIC'
        return A(1), B(16), 'HEURISTIC'
    h = 13 if w >= 2.3 - 1e-6 else 10 if w >= 2.2 - 1e-6 else (12 if pc else 11) if w >= 2.1 - 1e-6 else 9
    return A(1), B(h), 'HEURISTIC'

def clip(s):
    m = re.search(r'tacticus_sht\d+_shoot_(aim|blind)(_melta)?', s)
    return m.group(0) if m else s.split(' ')[0]

def roots(nm):
    r = {}
    for i, s in nm.names.items():
        if re.match(VAR_RX, s):
            k = [j for j, p in nm.rec.items() if nm.h(p + 10) == i]
            if k: r[s] = k[0]
    return r

def assumptions(notes, w, blind_raw):
    a = []
    for n in notes:
        if 'no branch assigns OUT' in n: a.append('unassigned Expression OUT -> 0 (' + n.split(':')[0] + ')')
        elif 'ambiguous' in n: a.append('undecoded op (95/98/c4): first outcome taken (' + n.split(' ')[0] + ')')
        elif n == 'SIMPLECONV': a.append('SimpleTypeConvert not indexed: its input assumed = isInAuxVision')
        elif n.startswith('no value for'): a.append('control param missing, 0 assumed (' + n[13:] + ')')
        else: a.append(n)
    if w >= 2: a.append('aim: no heavy branch, wpn_type floors into rifle branch (1)')
    if 'SubtractiveBlend' in blind_raw: a.append('blind: SubtractiveBlend not indexed, child assumed sht5')
    return list(dict.fromkeys(a))

def evaluate(nm, rt, state, w, pc, ax):
    notes = []
    env = dict(wpn_type=w, plasma_charged=float(pc), isInAuxVision=float(ax), character_type=0.0)
    r = select(nm, rt[state], env, notes)
    if 'no value for SimpleTypeConvert' in notes:   # record not indexed: feed isInAuxVision through it (name-based guess)
        notes = [n for n in notes if n != 'no value for SimpleTypeConvert'] + ['SIMPLECONV']
        env['SimpleTypeConvert'] = float(ax); r = select(nm, rt[state], env, [])
    return r, notes

def verify(nm, weapons, all_states=False):
    rt = roots(nm); rows = []
    for name, w in weapons:
        for pc in (0, 1):
            for ax in (0, 1):
                aim_raw, n1 = evaluate(nm, rt, AIM, w, pc, ax)
                bl_raw, n2 = evaluate(nm, rt, BLIND, w, pc, ax)
                aim, bl = clip(aim_raw), clip(bl_raw)
                ca, cb, cev = claimed(w, bool(pc), bool(ax))
                asm = assumptions(n1 + n2, w, bl_raw)
                bl_cmp = 'tacticus_sht5_shoot_blind' if bl == 'SubtractiveBlend' else bl
                ok = (aim == ca and bl_cmp == cb)
                label = 'MISMATCH' if not ok else ('HEURISTIC' if asm else 'INFERRED')
                div = []
                if all_states:
                    for s in sorted(rt):
                        if s in (AIM, BLIND): continue
                        v = clip(evaluate(nm, rt, s, w, pc, ax)[0]); ref = aim if 'minizoom' in s else bl
                        if v != ref: div.append('%s=%s' % (s.split('|', 2)[2].replace('|shoot|', ':'), v))
                rows.append(dict(weapon=name, wpnType=w, pc=pc, ax=ax, aim=aim, blind=bl, table_aim=ca, table_blind=cb,
                                 table_ev=cev, label=label, assumptions=asm, variants=div))
    return rows

def main(a):
    import argparse
    ap = argparse.ArgumentParser(); ap.add_argument('nmb')
    ap.add_argument('--weapons', default=os.path.join(os.path.dirname(__file__), '..', '..', 'docs', 'handoff', 'fire_clip_weapons.tsv'))
    ap.add_argument('--out'); ap.add_argument('--all-states', action='store_true')
    o = ap.parse_args(a)
    weapons = [(l.split('\t')[0], float(l.split('\t')[1])) for l in open(o.weapons) if l.strip()]
    rows = verify(Nmb(o.nmb), weapons, o.all_states)
    hdr = 'weapon\twpnType\tplasma\taux\taim\tblind\tlabel\ttable_label\tassumptions\tvariant_divergence'
    lines = [hdr] + ['%s\t%g\t%d\t%d\t%s\t%s\t%s\t%s\t%s\t%s' % (r['weapon'], r['wpnType'], r['pc'], r['ax'], r['aim'], r['blind'],
             r['label'], r['table_ev'], '; '.join(r['assumptions']), '; '.join(r['variants'])) for r in rows]
    txt = '\n'.join(lines) + '\n'
    (open(o.out, 'w').write(txt) if o.out else sys.stdout.write(txt))
    c = {k: sum(r['label'] == k for r in rows) for k in ('INFERRED', 'HEURISTIC', 'MISMATCH')}
    lab = sum(r['label'] != r['table_ev'] and r['label'] != 'MISMATCH' for r in rows)
    print('rows=%d INFERRED=%d HEURISTIC=%d MISMATCH=%d relabelled_vs_table=%d' % (len(rows), c['INFERRED'], c['HEURISTIC'], c['MISMATCH'], lab), file=sys.stderr)
    return 1 if c['MISMATCH'] else 0

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
