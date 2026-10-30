"""Build per-function memory-access index: {func: [(addr, mnem, base, index, disp, is_store, size)]} for non-stack bases.
usage: dispscan.py <exe> <out.pkl>"""
import sys, struct, pefile, capstone, pickle
from capstone import x86
exe, out = sys.argv[1], sys.argv[2]
pe = pefile.PE(exe, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
idx = {}
for n,(fb, fe) in enumerate(funcs):
    if not (tb <= fb < tb + len(td)): continue
    acc = []
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        for i, op in enumerate(ins.operands):
            if op.type == x86.X86_OP_MEM:
                b = ins.reg_name(op.mem.base) if op.mem.base else ''
                if b in ('rsp', 'rip', ''): continue
                acc.append((ins.address, ins.mnemonic, b, op.mem.disp, i == 0, op.size, ins.op_str))
    if acc: idx[fb] = acc
    if n % 20000 == 0: print(n, len(funcs), flush=True)
pickle.dump(idx, open(out, 'wb'))
print('done', len(idx))
