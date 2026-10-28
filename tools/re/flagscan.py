"""Functions that touch [reg+0xe4] and [reg+0xe8] and use imm 0x400 and 0x20. usage: flagscan.py exe"""
import sys, struct, pefile, capstone
from capstone import x86
pe = pefile.PE(sys.argv[1], fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
for fb, fe in funcs:
    if not (tb <= fb < tb + len(td)) or fe - fb > 20000: continue
    s = set()
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        for op in ins.operands:
            if op.type == x86.X86_OP_MEM and op.mem.disp in (0xe4, 0xe8) and op.mem.base not in (x86.X86_REG_RSP, x86.X86_REG_RBP, x86.X86_REG_RIP): s.add(op.mem.disp)
            if op.type == x86.X86_OP_IMM and op.imm in (0x400, 0x20, 0x200) and ins.mnemonic in ('or', 'bts', 'and', 'test', 'mov', 'cmovne', 'cmove'): s.add(op.imm)
    if {0xe4, 0xe8, 0x400}.issubset(s): print(hex(fb), fe - fb, sorted(hex(x) for x in s))
