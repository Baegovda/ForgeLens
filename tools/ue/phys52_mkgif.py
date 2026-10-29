from PIL import Image, ImageDraw
import glob, json, math
n=len(glob.glob('phys_side_*.png')); frames=[]
for i in range(n):
    tiles=[Image.open(f'{k}_{i:03d}.png').convert('RGB') for k in ('anim_side','phys_side','anim_front','phys_front')]
    W,H=tiles[0].size; im=Image.new('RGB',(W*2,H*2+30),(20,20,20)); d=ImageDraw.Draw(im)
    for j,t in enumerate(tiles): im.paste(t,((j%2)*W,(j//2)*H+30))
    d.text((10,8),"BEFORE: box chain anim-only (= PA_5, 0/34 jiggles)  top=side bottom=front",fill=(255,255,255)); d.text((W+10,8),"AFTER phys52 ABP_7 (34 jiggles + SM2Damper)  %d/%d"%(i+1,n),fill=(255,255,0))
    frames.append(im)
frames[0].save('../titus_box_walk_before_after.gif',save_all=True,append_images=frames[1:],duration=100,loop=0)
frames[n//2].save('../titus_box_walk_before_after_mid.png')
# chart: box02->end angle from its mean direction, in component space
R=json.load(open('rec.json')); rows=[r for r in R if r['t']>2.0]
def sub(a,b): return [a[i]-b[i] for i in range(3)]
def ang(u,v):
    d=sum(x*y for x,y in zip(u,v))/(math.hypot(*u)*math.hypot(*v)); return math.degrees(math.acos(max(-1,min(1,d))))
C=Image.new('RGB',(900,360),(255,255,255)); d=ImageDraw.Draw(C)
t0,t1=rows[0]['t'],rows[-1]['t']; X=lambda t:50+(t-t0)/(t1-t0)*830; Y=lambda a:320-a*5
for a in range(0,61,10): d.line([(50,Y(a)),(880,Y(a))],fill=(225,225,225)); d.text((10,Y(a)-6),"%d"%a,fill=(0,0,0))
for lab,col in (("anim",(40,40,220)),("phys",(220,40,40))):
    v=[sub(r[lab]['box_jiggle_end'],r[lab]['box_jiggle_02']) for r in rows]; m=[sum(x[i] for x in v)/len(v) for i in range(3)]
    pts=[(X(r['t']),Y(ang(x,m))) for r,x in zip(rows,v)]; d.line(pts,fill=col,width=2)
d.text((60,10),"Titus box (box_jiggle_02->end) swing from its mean direction [deg] vs time [s], walk_fwd loop. blue=before(anim) red=after(phys52)",fill=(0,0,0))
d.text((60,335),"t=%.1f .. %.1f s (SIE, PhysTestMap)"%(t0,t1),fill=(0,0,0)); C.save('../titus_box_swing_chart.png'); print(n)
