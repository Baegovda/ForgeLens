#!/usr/bin/env python3
"""DXIL HASH part of every sdc binary -> out/index/hash.tsv; match RenderDoc capture disasm ('; shader hash:')
under /workspace/rdc -> out/index/capture_map.tsv (capture file, capture shader id, family, idx, type, macros_common)."""
import os, re, json, glob, struct
O = '/workspace/shaders/out'
H = {}
for jf in sorted(glob.glob(f'{O}/index/*.jsonl')):
    for line in open(jf):
        x = json.loads(line); b = open(f"{O}/{x['file']}", 'rb').read()
        n = struct.unpack_from('<I', b, 28)[0]
        for i in range(n):
            off = struct.unpack_from('<I', b, 32+4*i)[0]
            if b[off:off+4] == b'HASH':
                h = b[off+12:off+28].hex(); H.setdefault(h, []).append(x)
with open(f'{O}/index/hash.tsv', 'w') as f:
    for h, xs in H.items():
        for x in xs: f.write(f"{h}\t{x['family']}\t{x['idx']}\t{x['type']}\t{' '.join(x['macros_common'])}\n")
hits = 0; miss = 0
with open(f'{O}/index/capture_map.tsv', 'w') as f:
    for p in sorted(glob.glob('/workspace/rdc/**/*.txt', recursive=True)):
        try: t = open(p, errors='replace').read(4096)
        except Exception: continue
        m = re.search(r'; shader hash: ([0-9a-f]{32})', t)
        if not m: continue
        xs = H.get(m.group(1))
        if not xs: miss += 1; f.write(f"{p}\t{m.group(1)}\t-\n"); continue
        hits += 1
        for x in xs: f.write(f"{p}\t{m.group(1)}\t{x['family']}\t{x['idx']}\t{x['type']}\t{' '.join(x['macros_common'])}\n")
print('hashes', len(H), 'capture hits', hits, 'miss', miss)
