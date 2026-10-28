"""Decode the 'psos' section (crc32 key 0x37a5fe67) of an .sdc: prebuilt PSO descs.
Header: u32 recSize(213), 11, 108, 11, 4x u32 0, u32 count; then count x 213-byte records.
rec[0:4]=VS binary idx, rec[4:8]=PS binary idx (same family). Per-RT blend block of 8 bytes x8 at +72:
 bytes 0..5 = flags (?), byte6 = colour blend preset, byte7 = alpha blend preset (engine enum, calibrated vs captures)."""
import sys,struct,os
sys.path.insert(0,os.path.dirname(__file__))
import sdc
KEY=0x37a5fe67
def load(path):
    r=sdc.parse(path)
    it=[i for i in r['items'] if i['key']==KEY]
    if not it: return []
    d=it[0]['data']; rs,_,_,_=struct.unpack_from('<4I',d,0); n=struct.unpack_from('<I',d,32)[0]
    out=[]
    for i in range(n):
        x=d[36+i*rs:36+(i+1)*rs]
        vs,ps=struct.unpack_from('<ii',x,0)
        p=x.find(b'\x00\x00\x80\x3f')+4
        rts=[tuple(x[p+3+8*k:p+3+8*k+8]) for k in range(8)]
        out.append(dict(i=i,vs=vs,ps=ps,pre=x[p:p+3],rts=rts,tail=x[p+3+64:],raw=x))
    return out

# Blend preset enum, calibrated on 16 capture draws (particles/sfx/distort; colour and alpha share the enum):
PRESET={0:('Zero','One'),1:('Zero','Zero'),2:('One','Zero'),3:('SrcAlpha','InvSrcAlpha'),5:('One','One'),
        6:('One','InvSrcAlpha'),7:('One','InvSrcAlpha'),18:('One','One','Max')}
# RT block byte5 == 1: capture shows the RT copying RT0's blend (SURFBUF_HEIGHT in sfx). Bytes 0..4 not decoded (write mask/enable?).

if __name__=='__main__':
    import json,glob
    for p in sorted(glob.glob('/workspace/shaders/raw/sdc/*.sdc')):
        if os.path.getsize(p)>100e6: continue
        recs=load(p)
        if not recs: continue
        fam=os.path.basename(p)[:-4].replace('common_pc_dx12_','').replace('pc_dx12_','')
        with open(f'/workspace/shaders/out/index/psos_{fam}.jsonl','w') as f:
            for r in recs:
                f.write(json.dumps(dict(i=r['i'],vs=r['vs'],ps=r['ps'],pre=list(r['pre']),
                  rt=[dict(flags=list(b[:6]),c=b[6],a=b[7],cs=PRESET.get(b[6]),as_=PRESET.get(b[7])) for b in r['rts']],
                  tail=list(r['tail'])))+'\n')
        print(fam,len(recs))
