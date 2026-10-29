import sys, struct, pefile, capstone, collections
from capstone import x86
pe = pefile.PE('/workspace/rea/sm2_retail.exe', fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
lo,hi=int(sys.argv[1],16),int(sys.argv[2],16); want=set(int(x,16) for x in sys.argv[3].split(',')); need=sys.argv[4] if len(sys.argv)>4 else ''
for fb, fe in funcs:
    if not (lo <= fb < hi) or fe-fb>40000: continue
    by=collections.defaultdict(set); mn=set()
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        mn.add(ins.mnemonic)
        if ins.mnemonic in('movss','vmovss','mulss','vmulss','addss','subss','divss','comiss','ucomiss','maxss','minss'):
            for op in ins.operands:
                if op.type==x86.X86_OP_MEM and op.mem.base: by[op.mem.base].add(op.mem.disp)
    if need and not any(need in m for m in mn): continue
    for r,s in by.items():
        if want<=s: print(hex(fb),fe-fb,ins.reg_name(r) if False else r,sorted(hex(x) for x in s if x<0x40)); break
