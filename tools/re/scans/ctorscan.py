import sys, struct, pefile, capstone, collections
from capstone import x86
pe = pefile.PE('/workspace/rea/sm2_retail.exe', fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = True
lo,hi=int(sys.argv[1],16),int(sys.argv[2],16)
for fb, fe in funcs:
    if not (lo <= fb < hi) or fe-fb>6000: continue
    st=[]
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        o=ins.operands
        if ins.mnemonic=='mov' and len(o)==2 and o[0].type==x86.X86_OP_MEM and o[1].type==x86.X86_OP_IMM and o[0].size==4:
            st.append((o[0].mem.disp, struct.unpack('<f',struct.pack('<I',o[1].imm&0xffffffff))[0]))
    ds={x for x,_ in st}
    if {0x10,0x14,0x18}<=ds and (0x28 in ds or 0x20 in ds): print(hex(fb),fe-fb,[(hex(a),round(v,4)) for a,v in st if 0x8<=a<=0x40][:20])
