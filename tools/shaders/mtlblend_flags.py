"""Which glt PS permutations read OBJ_MTLBLEND_LAYER_CONST[15].x (spec-map flags) and which bits they test.
Data-flow on dxc -dumpbin text: CB handle -> cbufferLoadLegacy reg 21 (offset 96+15*16=336) -> extractvalue 0 -> bitcast -> and/lshr."""
import re,glob,json,collections,sys
idx={json.loads(l)['idx']:json.loads(l) for l in open('out/index/glt.jsonl')}
res={}
for f in sorted(glob.glob('out/dis/glt/*.ps.txt')):
    t=open(f).read(); i=int(f.split('/')[-1][:6])
    if 'OBJ_MTLBLEND_LAYER_CONST' not in t: continue
    # cbuffer containing it
    blocks=re.split(r'\n; cbuffer ',t)
    cbname=None
    for b in blocks[1:]:
        if 'OBJ_MTLBLEND_LAYER_CONST[' in b.split('\n;\n')[0] or 'OBJ_MTLBLEND_LAYER_CONST[' in b[:20000].split('; cbuffer')[0]:
            cbname=b.split('\n')[0].strip(); break
    m=re.search(r'^; %s +cbuffer +NA +NA +CB(\d+) '%re.escape(cbname or '?'),t,re.M)
    if not m: res[i]=('nocb',); continue
    rid=m.group(1); off=re.search(r'OBJ_MTLBLEND_LAYER_CONST\[64\]; +; Offset: +(\d+)',t)
    reg=(int(off.group(1))+15*16)//16
    hs=re.findall(r'(%\d+) = call %dx.types.Handle @dx.op.createHandle\(i32 57, i8 2, i32 '+rid+r', ',t)
    bits=set(); loaded=False
    for h in hs:
        for L in re.findall(r'(%\d+) = call %dx.types.CBufRet.(?:f32|i32) @dx.op.cbufferLoadLegacy.(?:f32|i32)\(i32 59, %dx.types.Handle '+h+r', i32 '+str(reg)+r'\)',t):
            loaded=True
            for e in re.findall(r'(%\d+) = extractvalue %dx.types.CBufRet.(?:f32|i32) '+L+r', 0',t):
                vs=[e]+re.findall(r'(%\d+) = bitcast float '+e+' to i32',t)
                for v in vs:
                    for c in re.findall(r'= and i32 '+v+r', (-?\d+)',t)+re.findall(r'= and i32 (-?\d+), '+v+r'\b',t): bits.add(int(c))
                    for c in re.findall(r'= lshr i32 '+v+r', (\d+)',t): bits.add(('shr',int(c)))
                    if re.search(r'fptoui float '+e+r'\b|fptosi float '+e+r'\b',t): bits.add('fpto')
    res[i]=(loaded,sorted(bits,key=str))
c=collections.Counter(str(v) for v in res.values())
print(len(res)); [print(n,k) for k,n in c.most_common()]
json.dump({str(k):v for k,v in res.items()},open('out/index/glt_mtlblend_flags.json','w'))
