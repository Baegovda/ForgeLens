#!/usr/bin/env python3
"""Bulk-extract and index SM2 .sdc shader caches.
usage: sdc_index.py <sdc_dir> <out_dir> [family ...]
Writes <out>/bin/<family>/<idx:06d>.<stage>.dxbc and <out>/index/<family>.jsonl (one line per binary),
plus <out>/index/families.json. Key names are crc32 of strings (proven: 'sdc_version', 'macro',
'filenamespreload', 'binarydictionarysize', 'shaderbinary%06d' from loader FUN_1420b4ce0)."""
import sys, os, json, struct, zlib, glob
sys.path.insert(0, os.path.dirname(__file__)); import sdc
K_VER, K_MACRO, K_PRELOAD, K_DICT, K_SRC = (zlib.crc32(s) for s in (b'sdc_version', b'macro', b'filenamespreload', b'binarydictionarysize', b'src'))
K_PDB, K_BINMETA, K_MD5, K_RECS = 0x97eb2089, 0xeb2b14e1, 0x1fff58e7, 0xec6393d0   # names not recovered
DXTYPE = {0:'ps',1:'vs',2:'gs',3:'hs',4:'ds',5:'cs',6:'lib',13:'ms',14:'as'}
STAGE = {1:'vs',2:'ps',3:'gs',4:'cs',5:'hs',6:'ds'}  # sdc stage byte (1/2/4 checked vs DXIL type)
def strlist(b):
    n = struct.unpack_from('<I', b, 0)[0]; o = 4; out = []
    for _ in range(n):
        l = struct.unpack_from('<I', b, o)[0]; o += 4; out.append(b[o:o+l].decode('latin1')); o += l
    return out
def recs(b):
    n = struct.unpack_from('<I', b, 0)[0]; o = 4; out = []
    for _ in range(n):
        l = struct.unpack_from('<I', b, o)[0]; o += 4; s = b[o:o+l]; o += l
        v = struct.unpack_from('<i', b, o)[0]; o += 4; out.append((s, v))
    return out
def dxinfo(b):
    n = struct.unpack_from('<I', b, 28)[0]; parts = []; t = None
    for i in range(n):
        off = struct.unpack_from('<I', b, 32+4*i)[0]; fcc = b[off:off+4].decode('latin1'); parts.append(fcc)
        if fcc in ('DXIL', 'SHEX', 'SHDR') and t is None:
            t = struct.unpack_from('<I', b, off+8)[0] >> 16
    return DXTYPE.get(t, str(t)), parts
def family(fn):
    b = os.path.basename(fn)[:-4]
    for p in ('common_pc_dx12_', 'pc_dx12_'):
        if b.startswith(p): return b[len(p):]
    return b
def run(path, out):
    fam = family(path); r = sdc.parse(path); items = r['items']
    K = {it['key']: it for it in items}
    macros = strlist(K[K_MACRO]['data'])
    pdb = strlist(K[K_PDB]['data']) if K_PDB in K else []
    # old /workspace/vfxsh naming (sdcx.py): NNNNN = n-th group that contains DXBC, k = k-th DXBC in it
    old = {}; g_seen = {}; 
    for it in items:
        if it['data'][:4] == b'DXBC':
            if it['group'] not in g_seen: g_seen[it['group']] = [len(g_seen), 0]
            gi = g_seen[it['group']]; old[it['key']] = f'{gi[0]:05d}_{gi[1]}'; gi[1] += 1
    bins = []; i = 0
    while True:
        k = zlib.crc32(b'shaderbinary%06d' % i)
        if k not in K: break
        bins.append(K[k]); i += 1
    perms = {}
    for s, v in recs(K[K_PRELOAD]['data']):
        perms.setdefault(v, []).append((STAGE.get(s[0], str(s[0])), sorted(macros[x-1] for x in s[1:])))
    os.makedirs(f'{out}/bin/{fam}', exist_ok=True); os.makedirs(f'{out}/index', exist_ok=True)
    with open(f'{out}/index/{fam}.jsonl', 'w') as jf:
        for i, it in enumerate(bins):
            d = it['data']; t, parts = dxinfo(d)
            ps = perms.get(i, [])
            # macros common to every permutation that maps to this binary, plus the union
            common = sorted(set.intersection(*[set(m) for _, m in ps])) if ps else []
            union = sorted(set().union(*[set(m) for _, m in ps])) if ps else []
            fn = f'{i:06d}.{t}.dxbc'
            open(f'{out}/bin/{fam}/{fn}', 'wb').write(d)
            jf.write(json.dumps(dict(family=fam, idx=i, file=f'bin/{fam}/{fn}', type=t, stage=sorted({s for s, _ in ps}),
                size=len(d), old=old.get(it['key']), group=it['group'], pdb=pdb[i] if i < len(pdb) else None,
                nperm=len(ps), macros_common=common, macros_any=union, parts=parts,
                perms=[m for _, m in ps][:64])) + '\n')
    return dict(family=fam, sdc=os.path.basename(path), size=os.path.getsize(path), groups=r['groups'], entries=r['entries'],
                version=struct.unpack('<I', K[K_VER]['data'])[0], binaries=len(bins), macros=len(macros), preload=sum(len(v) for v in perms.values()),
                src=[s for s in strlist_src(K[K_SRC]['data'])] if K_SRC in K else [])
def strlist_src(b):
    # src: u32 count, count x {u32 len, char name[len], u32 hash, u8 flags}
    n = struct.unpack_from('<I', b, 0)[0]; o = 4; out = []
    for _ in range(n):
        l = struct.unpack_from('<I', b, o)[0]; o += 4
        out.append(b[o:o+l].decode('latin1')); o += l + 5
    return out
if __name__ == '__main__':
    src, out = sys.argv[1], sys.argv[2]
    files = sorted(glob.glob(f'{src}/*.sdc'))
    if len(sys.argv) > 3: files = [f for f in files if family(f) in sys.argv[3:]]
    fams = []
    fp = f'{out}/index/families.json'
    if os.path.exists(fp): fams = [x for x in json.load(open(fp)) if x['family'] not in {family(f) for f in files}]
    for f in files:
        x = run(f, out); fams.append(x); print(x['family'], x['binaries'], x['preload'], flush=True)
    json.dump(sorted(fams, key=lambda x: x['family']), open(fp, 'w'), indent=1)
