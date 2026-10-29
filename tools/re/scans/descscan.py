import sys, struct, pefile, capstone
from capstone import x86
pe = pefile.PE('/workspace/rea/sm2_retail.exe', fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
lo,hi=int(sys.argv[1],16),int(sys.argv[2],16); dsp=int(sys.argv[3],16); want=[int(x,16) for x in sys.argv[4:]]
for fb, fe in funcs:
    if not (lo <= fb < hi) or fe-fb>40000: continue
    descregs={}; hits=[]; others=set()
    for k,ins in enumerate(md.disasm(td[fb - tb:fe - tb], fb)):
        ops=ins.operands
        if ins.mnemonic=='mov' and len(ops)==2 and ops[0].type==x86.X86_OP_REG and ops[1].type==x86.X86_OP_MEM and ops[1].mem.disp==dsp:
            descregs[ops[0].reg]=k; continue
        for op in ops:
            if op.type==x86.X86_OP_MEM and op.mem.base in descregs and k-descregs[op.mem.base]<60: others.add(op.mem.disp)
        if ops and ops[0].type==x86.X86_OP_REG and ops[0].reg in descregs and ins.mnemonic.startswith(('mov','lea','add','sub','xor')): descregs.pop(ops[0].reg,None)
    if all(w in others for w in want): print(hex(fb),fe-fb,sorted(hex(x) for x in others)[:40])
