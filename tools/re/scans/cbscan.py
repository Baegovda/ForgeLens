import sys, struct, pefile, capstone
from capstone import x86
pe = pefile.PE('/workspace/rea/sm2_retail.exe', fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
lo,hi=int(sys.argv[1],16),int(sys.argv[2],16)
for fb, fe in funcs:
    if not (lo <= fb < hi) or fe-fb>20000: continue
    rd=set(); wr=set(); imm12=0
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        ops=ins.operands
        if ins.mnemonic in('movss','movups','movaps','mov','movsd','movd','vmovss','vmovups') and len(ops)==2:
            if ops[0].type==x86.X86_OP_MEM: wr.add(ops[0].mem.disp)
            if ops[1].type==x86.X86_OP_MEM: rd.add(ops[1].mem.disp)
        for op in ops:
            if op.type==x86.X86_OP_IMM and op.imm==0x12: imm12+=1
    a = {0xa8,0xe8}<=rd
    b = {0x60}<=wr and ({0x64,0x6c}&wr or {0x50,0x40,0x70,0x80}<=wr)
    if a and b: print(hex(fb), fe-fb, 'imm12',imm12, sorted(x for x in wr if 0x30<=x<=0x140))
