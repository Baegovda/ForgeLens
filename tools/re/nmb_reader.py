#!/usr/bin/env python3
"""nmb_reader.py - read-only dumper for SM2 Morpheme network (.nmb) switch/expression logic.

Evidence labels (see docs): layouts are DERIVED FROM DATA, not from vendor docs.
  - IDMappedStringTable header {u32 count, u32 dataLen, u64 offIds, u64 offOffsets, u64 offHashes, u64 offData}  [PROVEN: offsets resolve every name]
  - NodeDef header {u32 typeId, u32 flags, u16 nodeId, u16 parentId, u16 numChildren, u16 numInputCPs,
      +0x28 u64 rel->childIds(u16[]), +0x30 u64 rel->inputCPs({u16 node,u16 pin}[]), +0x48 u64 rel->attribHandles}  [INFERRED: ids/parents/children agree with names]
    Expression/operator nodes (flags 0x9000): numInputs = u8 @+0x10.  [INFERRED]
  - attrib handle {u64 rel, u32 size, u32 align}; attrib data header 16 bytes.  [INFERRED]
  - Switch (type 0x83): attr0 weights {u32 n, pad, u64 rel->f32[n]}; attr1 {u32 a, u32 b} (eval/selection mode, meaning HEURISTIC).
  - Expression (type 0xcb) bytecode: 4-byte instr (b0,b1,b2,op). b1>=0x80 -> constant id (b1-256); b2 -> variable index
      (inputs 0..n-1, output = n). op 04: var[b2]=const[b1]; op a4: if !(var[b2]==const[b1]) jump +b0;
      op a5/bd: if (var[b2]==const[b1]) jump +b0 (OR-chain); op c0: jump +b0. Jump target = pc+1+b0.  [INFERRED: every program
      in the aim/blind states decodes to consistent if/elif chains with these rules]
      ops 95/98/c4 not decoded (HEURISTIC: treated as unknown branch, both outcomes reported).
"""
import struct, re, sys, json, itertools

class Nmb:
    def __init__(s, path):
        s.d = open(path, 'rb').read()
        s._strings(); s._index()
    def u(s, o): return struct.unpack_from('<I', s.d, o)[0]
    def q(s, o): return struct.unpack_from('<Q', s.d, o)[0]
    def h(s, o): return struct.unpack_from('<H', s.d, o)[0]
    def _strings(s):
        d = s.d; best = None
        for m in re.finditer(rb'\x30\x00{7}', d):
            p = m.start() - 8
            if p < 0 or p % 4: continue
            n, ln = s.u(p), s.u(p + 4)
            if not (100 < n < 1_000_000): continue
            oo, oh, od = s.q(p + 16), s.q(p + 24), s.q(p + 32)
            if not (0x30 + 4 * n <= oo < oh < od and p + od + ln <= len(d)): continue
            if best is None or n > best[1]: best = (p, n, oo, od, ln)
        p, n, oo, od, ln = best
        s.names = {}
        for k in range(n):
            i = s.u(p + 0x30 + 4 * k); off = s.u(p + oo + 4 * k)
            a = p + od + off
            s.names.setdefault(i, s.d[a:s.d.index(b'\0', a)].decode('latin1'))
        s.strtab = p
    def _index(s):
        s.rec = {}; names = s.names
        for p in range(0, s.strtab, 4):
            fl = s.u(p + 4)
            if fl & 0xffff0000 or (fl & 0xf000) not in (0x8000, 0x9000): continue
            t = s.u(p)
            if t == 0 or t > 0x400: continue
            i = s.h(p + 8)
            if i not in names: continue
            co, cpo, ao = s.u(p + 0x28), s.u(p + 0x30), s.u(p + 0x48)
            if not ((co == 0 or 0x40 <= co < 0x400) and 0x40 <= cpo < 0x400 and 0x40 <= ao < 0x400): continue
            s.rec.setdefault(i, p)
    def short(s, i): return s.names.get(i, '#%d' % i).split('|')[-1]
    def node(s, i):
        p = s.rec.get(i)
        if p is None: return None
        t, fl = s.u(p), s.u(p + 4)
        nc, ncp = s.h(p + 12), s.h(p + 14)
        if fl & 0xf000 == 0x9000: nc, ncp = 0, s.d[p + 0x10]
        ch = [s.h(p + s.u(p + 0x28) + 2 * k) for k in range(nc)]
        cps = [(s.h(p + s.u(p + 0x30) + 4 * k), s.h(p + s.u(p + 0x30) + 4 * k + 2)) for k in range(ncp)]
        atts = []; e = p + s.u(p + 0x48)
        while True:
            off, sz, al = s.q(e), s.u(e + 8), s.u(e + 12)
            if not (0 < off < 0x4000 and 0 < sz < 0x8000 and al in (4, 8, 16)): break
            atts.append((e + off, sz)); e += 16
        return dict(id=i, p=p, type=t, flags=fl, parent=s.h(p + 10), ch=ch, cps=cps, atts=atts)
    def switch(s, n):
        a, _ = n['atts'][0]; cnt = s.u(a + 16)
        w = list(struct.unpack_from('<%df' % cnt, s.d, a + s.u(a + 0x18)))
        mode = (s.u(n['atts'][1][0] + 16), s.u(n['atts'][1][0] + 20)) if len(n['atts']) > 1 else None
        return w, mode
    def expr(s, n):
        a, sz = n['atts'][0]; nin = len(n['cps'])
        oV, oC = s.u(a + 0x18), s.u(a + 0x38)
        consts = {}; q = a + oV + 8 * (nin + 1)
        while q < a + oC:
            k = s.h(q + 4) - 0x10000
            consts[k] = round(struct.unpack_from('<f', s.d, q + 6)[0], 6) if s.u(q) == 4 else ('t%d' % s.u(q), s.d[q + 6:q + 10].hex())
            q += 24
        code = []; o = a + oC
        known = {0x04, 0xa4, 0xa5, 0xbd, 0xc0, 0x95, 0x98, 0xc4}
        while True:
            b0, b1, b2, op = s.d[o:o + 4]
            if op not in known or (op == 0 and b1 == 0): break
            if s.u(o) in (0xcb, 0x83, 0x68) or s.u(o + 4) in (0x8100, 0x9000, 0x8001): break
            code.append((b0, b1, b2, op)); o += 4
        return consts, code

def fmt_expr(nm, n, consts, code):
    inames = [nm.short(c) for c, _ in n['cps']] + ['OUT']
    def v(i): return inames[i] if i < len(inames) else 'v%d' % i
    def c(b): return consts.get(b - 256, 'c%d' % (b - 256)) if b >= 0x80 else 'r%d' % b
    out = []
    for k, (b0, b1, b2, op) in enumerate(code):
        t = k + 1 + b0
        if op == 0x04: s = '%s = %s' % (v(b2), c(b1))
        elif op == 0xa4: s = 'if !(%s == %s) goto %d' % (v(b2), c(b1), t)
        elif op in (0xa5, 0xbd): s = 'if (%s == %s) goto %d   [op %02x]' % (v(b2), c(b1), t, op)
        elif op == 0xc0: s = 'goto %d' % t
        else: s = 'op_%02x b0=%d b1=%d b2=%d   [UNDECODED]' % (op, b0, b1, b2)
        out.append('%3d: %s' % (k, s))
    return out

def run_expr(consts, code, inputs, nout):
    """Returns set of possible outputs (None = no assignment)."""
    f32 = lambda x: struct.unpack('<f', struct.pack('<f', x))[0]
    res = set()
    def go(pc, out, steps):
        while pc < len(code) and steps < 200:
            b0, b1, b2, op = code[pc]; steps += 1
            cv = consts.get(b1 - 256) if b1 >= 0x80 else (inputs[b1] if b1 < len(inputs) else None)
            if op == 0x04: out = cv; pc += 1
            elif op == 0xa4: pc = pc + 1 if f32(inputs[b2]) == f32(cv) else pc + 1 + b0
            elif op in (0xa5, 0xbd): pc = pc + 1 + b0 if f32(inputs[b2]) == f32(cv) else pc + 1
            elif op == 0xc0: pc = pc + 1 + b0
            elif b0: go(pc + 1 + b0, out, steps); pc += 1   # undecoded conditional: both ways
            else: pc += 1
        res.add(out)
    go(0, None, 0)
    return res

def walk(nm, i, depth=0, lines=None, seen=None):
    lines = [] if lines is None else lines; seen = set() if seen is None else seen
    ind = '  ' * depth; n = nm.node(i)
    if i in seen: lines.append(ind + '(seen) %d %s' % (i, nm.short(i))); return lines
    seen.add(i)
    if n is None: lines.append(ind + '%d %s' % (i, nm.short(i)) + ('  [clip/leaf]' if 'sht' in nm.short(i) or 'shoot' in nm.short(i) else '  [no record]')); return lines
    hdr = '%s%d %s type=0x%x' % (ind, i, nm.short(i), n['type'])
    if n['type'] == 0x83:
        w, mode = nm.switch(n); hdr += ' SWITCH weights=%s mode=%s input=%s' % (w, mode, [nm.short(c) for c, _ in n['cps']])
        lines.append(hdr)
        for k, c in enumerate(n['ch']): lines.append(ind + '  [w=%g] ->' % w[k]); walk(nm, c, depth + 2, lines, seen)
    elif n['type'] == 0xcb:
        consts, code = nm.expr(n)
        lines.append(hdr + ' EXPRESSION inputs=%s' % [nm.short(c) for c, _ in n['cps']])
        lines += [ind + '    ' + x for x in fmt_expr(nm, n, consts, code)]
    else:
        lines.append(hdr + ' inputs=%s' % [nm.short(c) for c, _ in n['cps']])
        for c in n['ch']: walk(nm, c, depth + 1, lines, seen)
    for c, _ in n['cps']:
        if c != i and nm.node(c): walk(nm, c, depth + 1, lines, seen)
    return lines

def cp_value(nm, i, env, notes):
    n = nm.node(i); nmn = nm.short(i)
    if n is None or n['type'] not in (0xcb, 0xcc):
        return env.get(nmn.split('|')[-1], env.get(nm.names.get(i, '').split('|')[-1]))
    if n['type'] == 0xcc:   # SimpleTypeConvert: pass-through of its input (INFERRED)
        return cp_value(nm, n['cps'][0][0], env, notes)
    consts, code = nm.expr(n)
    ins = [cp_value(nm, c, env, notes) for c, _ in n['cps']]
    outs = run_expr(consts, code, ins, len(ins))
    if len(outs) > 1: notes.append('%s ambiguous %s (undecoded op)' % (nmn, sorted(outs, key=str)))
    o = sorted(outs, key=str)[0]
    if o is None: notes.append('%s: no branch assigns OUT (default unknown; 0 assumed, HEURISTIC)' % nmn); o = 0.0
    return o

def select(nm, i, env, notes, depth=0):
    """Follow switches from node i to a leaf. Switch selection = floor (mode[1]==1) or closest (mode[1]==0); INFERRED."""
    n = nm.node(i)
    if n is None: return nm.short(i)
    if n['type'] == 0x83:
        w, mode = nm.switch(n)
        x = cp_value(nm, n['cps'][0][0], env, notes)
        if x is None: notes.append('no value for %s' % nm.short(n['cps'][0][0])); x = 0.0
        if mode and mode[1] == 1:
            k = max([j for j, v in enumerate(w) if v <= x + 1e-6] or [0])
        else:
            k = min(range(len(w)), key=lambda j: abs(w[j] - x))
        return select(nm, n['ch'][k], env, notes, depth + 1)
    if n['ch']: return select(nm, n['ch'][0], env, notes, depth + 1) + ' (via %s)' % nm.short(i)
    return nm.short(i)

if __name__ == '__main__':
    import argparse
    ap = argparse.ArgumentParser(); ap.add_argument('nmb'); ap.add_argument('--state', action='append', default=[])
    ap.add_argument('--json'); ap.add_argument('--map', help='TSV of name<TAB>wpnType')
    a = ap.parse_args()
    nm = Nmb(a.nmb)
    if a.map:
        roots = {s: [j for j, p in nm.rec.items() if nm.h(p + 10) == i][0] for i, s in nm.names.items()
                 if s in ('anim|LOCO|minizoom|shoot|shoot_state', 'anim|LOCO|loco2|blind_shoot|blind_whole_anim|shoot|shoot_state')}
        print('weapon\twpnType\tplasma_charged\tisInAuxVision\taim\tblind\tnotes')
        for line in open(a.map):
            name, w = line.rstrip('\n').split('\t'); w = float(w)
            for pc in (0.0, 1.0):
                for ax in (0.0, 1.0):
                    notes = []; env = dict(wpn_type=w, plasma_charged=pc, isInAuxVision=ax, character_type=0.0)
                    aim = select(nm, roots['anim|LOCO|minizoom|shoot|shoot_state'], env, notes)
                    bl = select(nm, roots['anim|LOCO|loco2|blind_shoot|blind_whole_anim|shoot|shoot_state'], env, notes)
                    print('%s\t%g\t%g\t%g\t%s\t%s\t%s' % (name, w, pc, ax, aim, bl, '; '.join(dict.fromkeys(notes))))
        sys.exit(0)
    pats = a.state or [r'minizoom\|shoot\|shoot_state(_1)?$', r'blind_shoot\|[^|]+\|shoot\|shoot_state$']
    for i, s in sorted(nm.names.items()):
        if any(re.search(p, s) for p in pats):
            roots = [j for j, p in nm.rec.items() if nm.h(p + 10) == i]
            print('=== STATE %d %s' % (i, s))
            for r in roots: print('\n'.join(walk(nm, r)))
