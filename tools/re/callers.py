"""Direct callers of a target (also via one ILT jmp thunk). usage: callers.py <exe> 0xTARGET..."""
import sys, struct, pefile, capstone
exe = sys.argv[1]; T = {int(x, 16) for x in sys.argv[2:]}
pe = pefile.PE(exe, fast_load=True); base = pe.OPTIONAL_HEADER.ImageBase
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]; tb = base + text.VirtualAddress; td = text.get_data()
# thunks: E9 rel32 anywhere 5-byte aligned in ILT region (start of .text) that jump to targets
thunks = {}
for t in T:
    for o in range(0, min(len(td), 0x200000)):
        if td[o] == 0xE9 and tb + o + 5 + struct.unpack_from('<i', td, o + 1)[0] == t: thunks[tb + o] = t
print('thunks', {hex(k): hex(v) for k, v in thunks.items()})
want = T | set(thunks)
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
for fb, fe in funcs:
    if not (tb <= fb < tb + len(td)): continue
    for ins in md.disasm(td[fb - tb:fe - tb], fb):
        if ins.mnemonic in ('call', 'jmp') and ins.op_str.startswith('0x') and int(ins.op_str, 16) in want:
            print(hex(fb), hex(ins.address), ins.mnemonic, ins.op_str)
