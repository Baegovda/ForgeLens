import struct,sys
def parse(path):
    d=open(path,'rb').read(); p=len("1SERcd_list")+0x35
    cnt,unk=struct.unpack_from('<ii',d,p); p+=8
    ents=[{} for _ in range(cnt)]
    def s32():
        nonlocal p
        n=struct.unpack_from('<i',d,p)[0]; p+=4; s=d[p:p+n].decode('utf8','replace'); p+=n; return s
    if d[p]: 
        p+=1
        for e in ents: e['name']=s32()
    else: p+=1
    assert d[p]==0; p+=1
    if d[p]:
        p+=1
        for e in ents: e['m']=struct.unpack_from('<16f',d,p); p+=64
    else: p+=1
    if d[p]:
        p+=1
        for e in ents: e['aff']=s32()
    else: p+=1
    if d[p]:
        p+=1; nb=(cnt+7)//8; bits=d[p:p+nb]; p+=nb
        for i,e in enumerate(ents):
            if not (bits[i>>3]>>(i&7))&1: continue
            while True:
                sid,end=struct.unpack_from('<HI',d,p); p+=6
                if sid==0xFFFF: break
                if sid==0: e['ps']=s32()
                else: raise Exception(f'sentinel {sid:x} at {p}')
    return ents
if __name__=='__main__':
    ents=parse(sys.argv[1]); print(len(ents))
    for i,e in enumerate(ents):
        if 'sfx_light' in e.get('ps',''): print(i,e['name'][:90],[round(x,2) for x in e['m'][12:15]])
