import json, math, sys
R=json.load(open(sys.argv[1]))
def sub(a,b): return [a[i]-b[i] for i in range(3)]
def ang(u,v):
    d=sum(x*y for x,y in zip(u,v))/(math.hypot(*u)*math.hypot(*v)); return math.degrees(math.acos(max(-1,min(1,d))))
rows=[r for r in R if r['t']>2.0]
out={}
for seg,(a,b) in {"box02->end":("box_jiggle_02","box_jiggle_end"),"box01->02":("box_jiggle_01","box_jiggle_02"),"CENTRE->Belt":("CENTRE","Belt_jiggle"),"Belt->box01":("Belt_jiggle","box_jiggle_01")}.items():
    A=[ang(sub(r['phys'][b],r['phys'][a]),sub(r['anim'][b],r['anim'][a])) for r in rows]
    out[seg]=(round(sum(A)/len(A),2),round(max(A),2))
D=[math.dist(sub(r['phys']['box_jiggle_end'],r['phys']['CENTRE']),sub(r['anim']['box_jiggle_end'],r['anim']['CENTRE'])) for r in rows]
out["box_end_offset_cm(mean,max)"]=(round(sum(D)/len(D),2),round(max(D),2))
for lab in ("anim","phys"):
    v=[sub(r[lab]['box_jiggle_end'],r[lab]['box_jiggle_02']) for r in rows]
    # swing amplitude: angle relative to the mean direction
    m=[sum(x[i] for x in v)/len(v) for i in range(3)]
    A=[ang(x,m) for x in v]; out[lab+"_box02->end swing vs mean (mean,max deg)"]=(round(sum(A)/len(A),2),round(max(A),2))
dts=[r['dt'] for r in rows]; out["dt(mean,max)"]=(round(sum(dts)/len(dts),4),max(dts)); out["rows"]=len(rows)
out["nan"]=any(any(math.isnan(c) for b in r['phys'].values() for c in b) for r in rows)
print(json.dumps(out,indent=1))
