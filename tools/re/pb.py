import sys
def varint(b,i):
    r=0;s=0
    while True:
        c=b[i];i+=1;r|=(c&0x7f)<<s;s+=7
        if c<0x80:return r,i
def parse(b):
    i=0;out=[]
    while i<len(b):
        k,i=varint(b,i);f=k>>3;w=k&7
        if w==0:v,i=varint(b,i)
        elif w==2:
            l,i=varint(b,i);v=b[i:i+l];i+=l
        elif w==5:v=b[i:i+4];i+=4
        elif w==1:v=b[i:i+8];i+=8
        else: raise ValueError(w)
        out.append((f,w,v))
    return out
def looks_msg(v):
    try:
        p=parse(v); return len(v)>0 and all(f>0 and f<64 for f,_,_ in p)
    except Exception: return False
def dump(b,ind=0,strs=None):
    for f,w,v in parse(b):
        if w==2:
            if looks_msg(v) and not (len(v)>2 and all(32<=c<127 for c in v)):
                print(' '*ind+f'{f}:{{'); dump(v,ind+2,strs); print(' '*ind+'}')
            else: print(' '*ind+f'{f}: {bytes(v)!r}')
        else:
            s=''
            if strs and w==0 and v<len(strs): s=f'  ;{strs[v]}'
            print(' '*ind+f'{f}= {v} (0x{v:x}){s}' if w==0 else f'{" "*ind}{f}= {bytes(v).hex()}')
if __name__=='__main__':
    b=open(sys.argv[1],'rb').read()
    print(b[:16].hex())
    dump(b[16:])
