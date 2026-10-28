"""Functions that load [reg+0x150] and index with stride 0x28 (imul 0x28 or lea x5 then *8). usage: patscan.py exe"""
import sys, struct, pefile, capstone
from capstone import x86
pe = pefile.PE(sys.argv[1], fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
for fb, fe in funcs:
    if not (0x142000000 <= fb < 0x142900000) or fe - fb > 8000: continue
    a = b = c = 0
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        for op in ins.operands:
            if op.type == x86.X86_OP_MEM:
                if op.mem.disp == 0x150 and ins.mnemonic == 'mov': a = 1
                if op.mem.index and op.mem.scale == 4 and op.mem.base == op.mem.index: b = 1
            if op.type == x86.X86_OP_IMM and op.imm == 0x28 and ins.mnemonic == 'imul': b = 1
        if ins.mnemonic in ('cmp',) and len(ins.operands) == 2 and ins.operands[1].type == x86.X86_OP_IMM and ins.operands[1].imm == 3: c += 1
    if a and b: print(hex(fb), fe - fb, c)
