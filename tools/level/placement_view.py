# R54 follow-up: placement.tsv -> self-contained HTML viewer (no deps, Canvas2D).
# Plots every object at its world translation (m[12..14], game metres, Y-up), coloured by kind:
# geom=grey, light=yellow, camera=red, other=blue. Top view (X/Z) + side view (X/Y) + orbit 3D point view.
# Meshes are markers sized by matrix scale (no model geometry; multi-part bundles drawn at bundle origin).
# New standalone output only; does not change any existing viewer output.
# Usage: python3 placement_view.py <in.placement.tsv> <out.html>
import csv, json, sys
def load(p):
    out = []
    for r in csv.DictReader(open(p), delimiter='\t'):
        m = [float(v) for v in r['matrix16'].split()]
        s = max((m[0]**2+m[1]**2+m[2]**2)**.5, 1e-3)
        out.append([r['kind'][0], round(m[12], 3), round(m[13], 3), round(m[14], 3), round(s, 3), r['templates'].split(';')[0]])
    return out
HTML = r'''<!doctype html><meta charset=utf-8><title>loading</title><style>body{margin:0;background:#111;color:#ddd;font:12px sans-serif}canvas{display:block}</style>
<div id=info></div><canvas id=c></canvas><script>
const T0=performance.now();const D=__DATA__;const C={g:'#999',l:'#fd3',c:'#f44',o:'#49f'};
const cv=document.getElementById('c'),W=cv.width=innerWidth,H=cv.height=innerHeight-20,x=cv.getContext('2d');
let yaw=0.6,pitch=0.5;const cnt={};D.forEach(d=>cnt[d[0]]=(cnt[d[0]]||0)+1);
const ext=(i)=>{let a=D.map(d=>d[i]).sort((p,q)=>p-q);return [a[Math.floor(a.length*.01)],a[Math.floor(a.length*.99)]]};
const X=ext(1),Y=ext(2),Z=ext(3);
function panel(ox,oy,w,h,f,label){x.strokeStyle='#444';x.strokeRect(ox,oy,w,h);x.fillStyle='#ddd';x.fillText(label,ox+4,oy+12);
 for(const k of ['g','o','l','c'])for(const d of D){if(d[0]!=k)continue;const p=f(d);if(!p)continue;const u=ox+w*p[0],v=oy+h*(1-p[1]);
 if(u<ox||u>ox+w||v<oy||v>oy+h)continue;x.fillStyle=C[k];const r=k=='g'?1.5:2.5;x.fillRect(u-r,v-r,2*r,2*r)}}
const n=(v,e)=>(v-e[0])/(e[1]-e[0]||1);
function draw(){x.fillStyle='#111';x.fillRect(0,0,W,H);const w=W/3;
 panel(0,0,w,H,d=>[n(d[1],X),1-n(d[3],Z)],'TOP X/Z');panel(w,0,w,H,d=>[n(d[1],X),n(d[2],Y)],'SIDE X/Y (Y-up)');
 const cx=(X[0]+X[1])/2,cy=(Y[0]+Y[1])/2,cz=(Z[0]+Z[1])/2,R=Math.max(X[1]-X[0],Z[1]-Z[0],Y[1]-Y[0]);
 panel(2*w,0,w,H,d=>{let a=d[1]-cx,b=d[2]-cy,c=d[3]-cz;let a2=a*Math.cos(yaw)-c*Math.sin(yaw),c2=a*Math.sin(yaw)+c*Math.cos(yaw);
 let b2=b*Math.cos(pitch)-c2*Math.sin(pitch);return [.5+a2/R,.5+b2/R]},'ORBIT (drag)');}
draw();cv.onmousemove=e=>{if(e.buttons){yaw+=e.movementX*.01;pitch+=e.movementY*.01;draw()}};
const ms=(performance.now()-T0).toFixed(1);
document.getElementById('info').textContent=`objects ${D.length} geom ${cnt.g||0} light ${cnt.l||0} camera ${cnt.c||0} other ${cnt.o||0} | draw ${ms} ms | X ${X.map(v=>v.toFixed(1))} Y ${Y.map(v=>v.toFixed(1))} Z ${Z.map(v=>v.toFixed(1))}`;
document.title='ready '+ms;</script>'''
def main(i, o):
    d = load(i); open(o, 'w').write(HTML.replace('__DATA__', json.dumps(d, separators=(',', ':')))); print(len(d), 'objects ->', o)
if __name__ == '__main__': main(*sys.argv[1:3])
