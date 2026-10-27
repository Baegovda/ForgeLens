import json,struct,sys,numpy as np
from PIL import Image,ImageDraw
def load(p):
    b=open(p,'rb').read(); jl=struct.unpack_from('<I',b,12)[0]; j=json.loads(b[20:20+jl]); bin0=20+jl+8
    def acc(i):
        a=j['accessors'][i]; v=j['bufferViews'][a['bufferView']]; n={'SCALAR':1,'VEC3':3}[a['type']]
        dt={5126:np.float32,5125:np.uint32,5123:np.uint16}[a['componentType']]
        o=bin0+v.get('byteOffset',0)+a.get('byteOffset',0)
        return np.frombuffer(b,dt,a['count']*n,o).reshape(-1,n) if n>1 else np.frombuffer(b,dt,a['count'],o)
    P=[];T=[];base=0
    for m in j['meshes']:
        for pr in m['primitives']:
            v=acc(pr['attributes']['POSITION']); i=acc(pr['indices']).astype(np.int64).reshape(-1,3)
            P.append(v);T.append(i+base);base+=len(v)
    return np.vstack(P),np.vstack(T)
def render(p,W=700,H=700,view='side'):
    V,F=load(p)
    if view=='front': V=V[:,[0,1,2]]
    else: V=V[:,[2,1,0]]*np.array([-1,1,1])
    lo,hi=V.min(0),V.max(0); s=0.92*min(W/(hi[0]-lo[0]),H/(hi[1]-lo[1])); c=(lo+hi)/2
    x=(V[:,0]-c[0])*s+W/2; y=H/2-(V[:,1]-c[1])*s; z=V[:,2]
    tri=V[F]; n=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]); n/=np.linalg.norm(n,axis=1,keepdims=True)+1e-12
    sh=np.abs(n@np.array([0.3,0.5,0.81]))*0.8+0.2
    zb=np.full((H,W),np.inf); img=np.full((H,W),40.0)
    for k,(a,bb,cc) in enumerate(F):
        xs=x[[a,bb,cc]];ys=y[[a,bb,cc]]
        x0,x1=int(max(0,xs.min())),int(min(W-1,xs.max()));y0,y1=int(max(0,ys.min())),int(min(H-1,ys.max()))
        if x1<x0 or y1<y0: continue
        X,Y=np.meshgrid(np.arange(x0,x1+1)+.5,np.arange(y0,y1+1)+.5)
        d=(ys[1]-ys[2])*(xs[0]-xs[2])+(xs[2]-xs[1])*(ys[0]-ys[2])
        if abs(d)<1e-9: continue
        l0=((ys[1]-ys[2])*(X-xs[2])+(xs[2]-xs[1])*(Y-ys[2]))/d; l1=((ys[2]-ys[0])*(X-xs[2])+(xs[0]-xs[2])*(Y-ys[2]))/d; l2=1-l0-l1
        m=(l0>=0)&(l1>=0)&(l2>=0)
        if not m.any(): continue
        zz=-(l0*z[a]+l1*z[bb]+l2*z[cc]); sub=zb[y0:y1+1,x0:x1+1]; w=m&(zz<sub)
        sub[w]=zz[w]; img[y0:y1+1,x0:x1+1][w]=255*sh[k]
    return Image.fromarray(img.astype(np.uint8)),len(F)
pairs=[(sys.argv[1],sys.argv[2]),]
out=sys.argv[3]; view=sys.argv[4]
ims=[]
for p in sys.argv[1:3]:
    im,nf=render(p,view=view); im=im.convert('RGB'); ImageDraw.Draw(im).text((8,8),f"{p} tris={nf}",fill=(255,255,0)); ims.append(im)
o=Image.new('RGB',(1400,700)); o.paste(ims[0],(0,0)); o.paste(ims[1],(700,0)); o.save(out); print(out)
