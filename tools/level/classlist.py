import struct
def parse(path):
    d=open(path,'rb').read(); p=0x40
    cnt,pc=struct.unpack_from('<ii',d,p); p+=8
    names=['']*cnt; ps=['']*cnt
    def s32():
        nonlocal p
        n=struct.unpack_from('<i',d,p)[0]; p+=4; s=d[p:p+n].decode('utf8','replace'); p+=n; return s
    if pc>0:
        f=d[p]; p+=1
        if f: names=[s32() for _ in range(cnt)]
    if pc>1:
        f=d[p]; p+=1
        if f: ps=[s32() for _ in range(cnt)]
    return dict(zip(names,ps))
