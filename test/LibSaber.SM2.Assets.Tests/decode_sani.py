#!/usr/bin/env python3
"""Decoder for Space Marine 2 (Swarm engine) ETNA .sani clips, recovered from sm2_retail.exe.
See SANI_FORMAT.md. Usage: decode_sani.py clip.sani [clip.sani_data] [--json out.json]
"""
import struct, sys, json, math, os

class R:
    def __init__(s, b, o=0): s.b, s.o = b, o
    def u16(s): v = struct.unpack_from('<H', s.b, s.o)[0]; s.o += 2; return v
    def i32(s): v = struct.unpack_from('<i', s.b, s.o)[0]; s.o += 4; return v
    def arr(s, fmt, n):
        sz = struct.calcsize('<' + fmt); v = struct.unpack_from('<' + fmt * n, s.b, s.o); s.o += sz * n; return list(v)

def mrp_quat(x, y, z):
    """FUN_142229bb0: modified-Rodrigues -> quaternion (w, x, y, z)."""
    s = x * x + y * y + z * z
    k = 1.0 / (s + 1.0)
    return ((1.0 - s) * k, 2 * k * x, 2 * k * y, 2 * k * z)

def blocks(b, start):
    c = start
    while c + 6 <= len(b):
        tag, end = struct.unpack_from('<Hi', b, c)
        if end <= c or end > len(b): break
        yield tag, b[c + 6:end]
        c = end

class Codec:
    """Class with vtable 0x1437e73e0 (file version <3, codec subversion 2)."""
    def __init__(s, payload, external):
        r = R(payload)
        s.sub = r.u16()
        s.n = r.arr('i', 6)  # animT, animR, animS, constT, constR, constS
        s.inline_frames = r.i32() if external else None
        n0, n1, n2, n3, n4, n5 = s.n
        s.idx = [r.arr('H', k) for k in s.n]
        s.cT_rng = r.arr('f', 6); s.cR_rng = r.arr('f', 6); s.cS_rng = r.arr('f', 2)
        s.aT_rng = [r.arr('f', 6) for _ in range(n0)]
        s.aR_rng = [r.arr('f', 6) for _ in range(n1)]
        s.aS_rng = [r.arr('f', 2) for _ in range(n2)]
        s.cT = [r.arr('H', 3) for _ in range(n3)]
        s.cR = [r.arr('H', 3) for _ in range(n4)]
        s.cS = r.arr('H', n5)
        s.header_end = r.o
        s.payload = payload
    def frame_size(s):
        n0, n1, n2 = s.n[:3]; return n0 * 4 + n1 * 6 + n2 * 2
    def attach(s, data, frames):
        n0, n1, n2 = s.n[:3]
        assert len(data) >= frames * s.frame_size(), (len(data), frames * s.frame_size())
        s.frames = frames
        s.T = data[0:frames * n0 * 4]
        s.Rk = data[frames * n0 * 4: frames * (n0 * 4 + n1 * 6)]
        s.S = data[frames * (n0 * 4 + n1 * 6): frames * s.frame_size()]
    def sample(s, f):
        """Returns {bone: (quat wxyz)}, {bone: (txyz)}, {scalar_index: value} at integer frame f."""
        n0, n1, n2, n3, n4, n5 = s.n
        rot, tr, sc = {}, {}, {}
        g = s.cT_rng
        for i, b in enumerate(s.idx[3]):
            u = s.cT[i]; tr[b] = tuple(u[k] * g[3 + k] + g[k] for k in range(3))
        g = s.cR_rng
        for i, b in enumerate(s.idx[4]):
            u = s.cR[i]; rot[b] = mrp_quat(*(u[k] * g[3 + k] + g[k] for k in range(3)))
        for i, b in enumerate(s.idx[5]):
            sc[b] = s.cS[i] * s.cS_rng[1] + s.cS_rng[0]
        for i, b in enumerate(s.idx[0]):
            w = struct.unpack_from('<I', s.T, (f * n0 + i) * 4)[0]
            q = (w >> 21, (w >> 10) & 0x7FF, w & 0x3FF); g = s.aT_rng[i]
            tr[b] = tuple(q[k] * g[3 + k] + g[k] for k in range(3))
        for i, b in enumerate(s.idx[1]):
            u = struct.unpack_from('<3H', s.Rk, (f * n1 + i) * 6); g = s.aR_rng[i]
            rot[b] = mrp_quat(*(u[k] * g[3 + k] + g[k] for k in range(3)))
        for i, b in enumerate(s.idx[2]):
            u = struct.unpack_from('<H', s.S, (f * n2 + i) * 2)[0]; g = s.aS_rng[i]
            sc[b] = u * g[1] + g[0]
        return rot, tr, sc

def root_motion(payload, frames):
    """0xF4, class vtable 0x1437e7800 (FUN_14222f1b0)."""
    r = R(payload); sub = r.u16()
    tr_rng = r.arr('f', 6); rot_rng = r.arr('f', 6)
    T = [r.arr('H', 3) for _ in range(frames)]; Q = [r.arr('H', 3) for _ in range(frames)]
    out = []
    for f in range(frames):
        t = tuple(T[f][k] * tr_rng[3 + k] + tr_rng[k] for k in range(3))
        q = mrp_quat(*(Q[f][k] * rot_rng[3 + k] + rot_rng[k] for k in range(3)))
        out.append((t, q))
    return out

def load(sani_path, data_path=None):
    b = open(sani_path, 'rb').read()
    assert b[:4] == b'ETNA'
    version = struct.unpack_from('<H', b, 4)[0]
    str_end, dur, rate, frames, nb = struct.unpack_from('<iffii', b, 8)
    o, bones = 32, []
    while o + 4 <= str_end and len(bones) < nb:
        L = struct.unpack_from('<i', b, o)[0]; o += 4
        bones.append(b[o:o + L].decode('ascii', 'replace')); o += L
    clip = dict(version=version, duration=dur, rate=rate, frames=frames, bones=bones, codec=None, root=None, tags=[])
    for tag, p in blocks(b, str_end):
        clip['tags'].append((hex(tag), len(p)))
        if tag == 0xF2:     # inline codec: header + all frames
            c = Codec(p, False); c.attach(p[c.header_end:], frames); clip['codec'] = c; clip['storage'] = 'inline'
        elif tag == 0xF6:   # header + a few preview frames; full frames in .sani_data (one 0xF2 block)
            c = Codec(p, True); clip['codec'] = c; clip['storage'] = 'external'
            if data_path is None and os.path.exists(sani_path + '_data'): data_path = sani_path + '_data'
            if data_path:
                d = open(data_path, 'rb').read()
                tag2, end = struct.unpack_from('<Hi', d, 0); assert tag2 == 0xF2 and end == len(d)
                c.attach(d[6:], frames)
            else:
                c.attach(p[c.header_end:], c.inline_frames); clip['frames_available'] = c.inline_frames
        elif tag == 0xF4:
            clip['root'] = root_motion(p, frames)
    return clip

def sample_all(clip):
    c = clip['codec']; return [c.sample(f) for f in range(c.frames)]

if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    clip = load(args[0], args[1] if len(args) > 1 else None)
    c = clip['codec']
    print(f"{os.path.basename(args[0])}: v{clip['version']} {clip['frames']}f @{clip['rate']}fps storage={clip['storage']} counts={c.n} blocks={clip['tags']}")
    if '--json' in sys.argv:
        out = sys.argv[sys.argv.index('--json') + 1]
        fr = sample_all(clip); names = clip['bones']
        js = dict(rate=clip['rate'], frames=len(fr), bones={})
        for bi, n in enumerate(names):
            js['bones'][n] = dict(rot=[f[0].get(bi) for f in fr], trans=[f[1].get(bi) for f in fr])
        js['root'] = clip['root']
        json.dump(js, open(out, 'w')); print('wrote', out)
