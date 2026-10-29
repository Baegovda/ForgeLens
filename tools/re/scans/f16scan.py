import sys, struct, pefile, capstone
from capstone import x86
pe = pefile.PE('/workspace/rea/sm2_retail.exe', fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
lo,hi=int(sys.argv[1],16),int(sys.argv[2],16)
for fb, fe in funcs:
    if not (lo <= fb < hi): continue
    cvt=0; s50=0; offs=set()
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        if 'cvtps2ph' in ins.mnemonic: cvt+=1
        for op in ins.operands:
            if op.type == x86.X86_OP_IMM and op.imm == 0x50 and ins.mnemonic in('imul','add','lea'): s50+=1
    if cvt: print(hex(fb), fe-fb, 'cvt',cvt,'x50',s50)
