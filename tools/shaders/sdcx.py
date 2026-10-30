#!/usr/bin/env python3
"""Minimal SM2 .sdc shader-cache unpacker (recreated 2026-10-27; the original /workspace/mat/sdcx.py was lost).

HEURISTIC: the .sdc header/index is not decoded (HANDOFF "vfx3 part 4" only says "DXIL containers, zlib records").
This scans the file for zlib streams, inflates each record, and inside it carves every DXBC container by its own
header size (u32 at +24). Output: NNNNN_k.dxbc = k-th container of the N-th zlib record in file order. Record
numbering may differ from the lost original. Usage: sdcx.py X.sdc outdir
"""
import os, re, struct, sys, zlib

def records(d):
    pos = 0
    for m in re.finditer(rb'\x78[\x01\x5e\x9c\xda]', d):
        if m.start() < pos:
            continue
        z = zlib.decompressobj()
        try:
            blob = z.decompress(d[m.start():])
        except zlib.error:
            continue
        if not z.eof:
            continue
        pos = len(d) - len(z.unused_data)
        yield m.start(), blob

def main(src, out):
    os.makedirs(out, exist_ok=True)
    d = open(src, 'rb').read()
    nrec = ncon = 0
    for _, blob in records(d):
        k, i = 0, blob.find(b'DXBC')
        while i >= 0 and i + 28 <= len(blob):
            size = struct.unpack_from('<I', blob, i + 24)[0]
            if size < 32 or i + size > len(blob):
                i = blob.find(b'DXBC', i + 4); continue
            open(os.path.join(out, f'{nrec:05d}_{k}.dxbc'), 'wb').write(blob[i:i + size])
            k += 1; ncon += 1
            i = blob.find(b'DXBC', i + size)
        if k:
            nrec += 1
    print(f'{src}: {nrec} records, {ncon} DXBC containers -> {out}')

if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
