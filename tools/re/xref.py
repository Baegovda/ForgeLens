"""Light xref scanner (no full analysis): .pdata function bounds + capstone, RIP-relative refs to target VAs.
usage: xref.py <exe> <out.json> (--str S | --va 0xHEX)..."""
import sys, json, pefile, capstone, re
exe, out = sys.argv[1], sys.argv[2]
pe = pefile.PE(exe, fast_load=True); 
base = pe.OPTIONAL_HEADER.ImageBase; data = open(exe, 'rb').read()
def off2va(o):
    for s in pe.sections:
        if s.PointerToRawData <= o < s.PointerToRawData + s.SizeOfRawData: return base + s.VirtualAddress + o - s.PointerToRawData
targets = {}
a = sys.argv[3:]
for i in range(0, len(a), 2):
    if a[i] == '--str':
        for m in re.finditer(re.escape(a[i+1].encode()), data):
            st = m.start()
            while st > 0 and 32 <= data[st-1] < 127: st -= 1
            targets[off2va(st)] = data[st:data.index(b'\0', st)].decode('latin1')[:120]
    else: targets[int(a[i+1], 16)] = a[i+1]
print('targets', {hex(k): v for k, v in targets.items()}, flush=True)
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
text = [s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text'][0]
tb = base + text.VirtualAddress; tdata = text.get_data()
import struct
d = pe.OPTIONAL_HEADER.DATA_DIRECTORY[3]; pd = pe.get_data(d.VirtualAddress, d.Size)
funcs = sorted({(base + b, base + e) for b, e, _ in struct.iter_unpack('<III', pd[:len(pd) // 12 * 12]) if e > b})
hits = []; print('funcs', len(funcs), flush=True)
rip = re.compile(r'\[rip ([+-]) 0x([0-9a-f]+)\]')
for fb, fe in funcs:
    if not (tb <= fb < tb + len(tdata)): continue
    for ins in md.disasm(tdata[fb - tb: fe - tb], fb):
        m = rip.search(ins.op_str)
        if not m: continue
        t = ins.address + ins.size + (int(m.group(2), 16) * (1 if m.group(1) == '+' else -1))
        if t in targets: hits.append({'func': hex(fb), 'ins': hex(ins.address), 'target': hex(t), 'str': targets[t], 'asm': ins.mnemonic + ' ' + ins.op_str})
json.dump(hits, open(out, 'w'), indent=1)
for h in hits: print(h['func'], h['ins'], h['asm'][:50], '|', h['str'][:70])
