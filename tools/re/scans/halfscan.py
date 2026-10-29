import sys, struct, pefile, capstone
from capstone import x86
pe = pefile.PE('/workspace/rea/sm2_retail.exe', fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
lo,hi=int(sys.argv[1],16),int(sys.argv[2],16)
K={0x477fe000,0x38800000,0x7c00,0x3ff,0x387fc000,0xc7fff000,0x8000}
for fb, fe in funcs:
    if not (lo <= fb < hi) or fe-fb>30000: continue
    hits=set(); s50=0; calls=set()
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        for op in ins.operands:
            if op.type==x86.X86_OP_IMM:
                v=op.imm & 0xffffffff
                if v in K: hits.add(hex(v))
                if v==0x50 and ins.mnemonic in('add','imul','lea','sub'): s50+=1
    if len(hits)>=2 and s50: print(hex(fb),fe-fb,sorted(hits),'x50',s50)
