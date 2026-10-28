#!/usr/bin/env python3
"""SM2 .sdc (shader cache) reader. Layout (proven by parse = file size on all 83 files, see FORMAT.md):
 header: u32 groupCount, u32 entryCount, u32 dataSize, u32 version(=1)
 groupCount x { u32 zSize, u32 n, n x { u32 key, u32 size, u32 strLen, char[strLen] } }
 dataSize bytes: groups' zlib streams back to back; each inflates to concat(entry payloads).
"""
import struct, zlib, sys
def parse(path, inflate=True):
    d = open(path,'rb').read()
    gc, ec, ds, ver = struct.unpack_from('<4I', d, 0); o = 16
    groups = []
    for g in range(gc):
        zs, n = struct.unpack_from('<2I', d, o); o += 8
        ents = []
        for _ in range(n):
            k, sz, sl = struct.unpack_from('<3I', d, o); o += 12
            s = d[o:o+sl].decode('latin1'); o += sl
            ents.append((k, sz, s))
        groups.append((zs, ents))
    assert o + ds == len(d), (path, o, ds, len(d))
    out = []
    p = o
    for gi,(zs, ents) in enumerate(groups):
        blob = zlib.decompress(d[p:p+zs]) if inflate else None; p += zs
        q = 0
        for k, sz, s in ents:
            out.append(dict(group=gi, key=k, size=sz, name=s, data=blob[q:q+sz] if inflate else None)); q += sz
        if inflate: assert q == len(blob), (path, gi, q, len(blob))
    return dict(groups=gc, entries=ec, version=ver, items=out)
if __name__ == '__main__':
    r = parse(sys.argv[1])
    print(r['groups'], r['entries'], r['version'])
    for it in r['items'][:40]:
        print(it['group'], hex(it['key']), it['size'], repr(it['name']), it['data'][:48])
