# SSLB (SM2 compiled ssl script, protobuf body after 16-byte header) reader / disassembler. Field meanings INFERRED from structure.
import sys
from pb import parse
def msg(v): return parse(v)
def get(m,f): return [v for ff,w,v in m if ff==f]
def first(m,f,d=None):
    r=get(m,f); return r[0] if r else d
def load(path):
    b=open(path,'rb').read(); assert b[:4]==b'SSLB'
    top=parse(b[16:])
    strs=[bytes(v).decode('utf8','replace') for f,w,v in top if f==6]
    return top,strs
def ops(code):
    for f,w,v in parse(code):
        if f==1: yield v&0xff, (v>>8)-0x800000
def const(c,strs):
    m=msg(c); k=first(m,1)
    if k==4: return repr(strs[first(m,5)])
    if k==1: return f'int {first(m,2,0)}'
    return f'k{k}:{m}'
def summary(path):
    top,strs=load(path)
    print('module',strs[0])
    for f,w,v in top:
        if f==8:
            cls=msg(v); print('class',strs[first(cls,1)])
            for mem in get(cls,4):
                mm=msg(mem); name=strs[first(mm,1)]; slot=first(mm,2); kind=first(mm,3)
                c=first(mm,6); extra=f' = {const(c,strs)}' if c is not None else ''
                code=first(mm,5)
                print(f'  [{slot}] kind{kind} {name}{extra}' + (f' code#{code}' if code is not None else ''))
    return top,strs
def disasm(path):
    top,strs=load(path)
    for f,w,v in top:
        if f==9:
            m=msg(v); sig=msg(first(m,2)); nm=strs[first(sig,1)]
            args=[strs[first(msg(a),1)] for a in get(sig,2)]
            print(f'code#{first(m,1)} {nm}({", ".join(args)})')
            for op,a in ops(first(m,5)): print(f'    {op:02x} {a}')
if __name__=='__main__':
    summary(sys.argv[1])
    if len(sys.argv)>2: disasm(sys.argv[1])
