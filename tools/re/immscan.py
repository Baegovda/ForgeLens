"""Rank functions by co-occurring immediates (no analysis): .pdata bounds + capstone.
usage: immscan.py <exe> <imm,imm,...> [minhits]"""
import sys, re, struct, pefile, capstone, collections
exe = sys.argv[1]; want = [int(x, 0) for x in sys.argv[2].split(',')]; mn = int(sys.argv[3]) if len(sys.argv) > 3 else 3
pe = pefile.PE(exe, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
ws = set(want); res = []
pat = re.compile(r'0x[0-9a-f]+')
for fb, fe in funcs:
    if not (tb <= fb < tb + len(td)): continue
    seen = collections.Counter()
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        if ins.mnemonic in ('or', 'test', 'and', 'bts', 'bt', 'mov', 'cmp') and '[' not in ins.op_str.split(',')[-1]:
            for m in pat.findall(ins.op_str.split(',')[-1]):
                v = int(m, 16)
                if v in ws: seen[v] += 1
    if len(seen) >= mn: res.append((len(seen), hex(fb), fe - fb, {hex(k): c for k, c in seen.items()}))
for r in sorted(res, reverse=True)[:40]: print(r)
