# Pair hub_battle_barge character_customization lights with armory capture 128197 eid 9799 and
# check the packer FUN_14224ecf0 / FUN_14224f010 formulas against COMMON_DYN_LIGHT_PARAMS.
import math,re,json
from cdlist import parse as cdp
from classlist import parse as clp
S='bb/scenes/hub_battle_barge.scn/hub_battle_barge'
E=cdp(S+'.cd_list'); C=clp(S+'.class_list')
CAP=[((-161.245682,609.240723,-93.39502,300.0),(7.931144,4.534479,1.334978,2e-05),(-0.511836,-0.209943,-0.833035,-29.535675),(30.577581,94.0,-0.001,1.0)),
((-159.75119,610.981689,-104.323242,300.0),(13.432114,35.0,32.629692,0.000201),(-0.706541,-0.532112,0.466537,-22.10849),(23.118666,93.0,-0.001,1.0)),
((-165.769852,610.320801,-102.910156,1.0),(121.148857,172.326538,176.908752,0.000772),(0.337698,-0.774558,0.534808,-16.631107),(17.698456,89.0,-0.001,1.0)),
((-158.335983,610.190796,-100.779785,300.0),(157.633255,269.219696,389.720123,0.000508),(-0.967364,-0.248827,-0.047873,-25.854927),(26.896866,95.0,-0.001,1.0))]
def g(ps,k,default=None):
    m=re.search(r'\b'+k+r' = (\{value = )?([^;]*);',ps); return m.group(2) if m else default
def p0w(acp,invsq=False,const=False,directional=False):
    if directional: return 0.0
    if const and not invsq: return 8e22
    if invsq: return 0.0025
    return 299*acp**4+1 if acp>0 else 256.0**acp
rows=[]
for li,(P0,P1,P2,P3) in enumerate(CAP):
    i,e=min(enumerate(E),key=lambda t:math.dist(P0[:3],t[1]['m'][12:15]))
    dist=math.dist(P0[:3],e['m'][12:15])
    typ=re.search(r'__type = "([^"]*)"',e['ps']).group(1); ps=C[typ]
    acp=float(g(ps,'attenCurveParam','0')); inv=g(ps,'useInverseSquaredFalloff','false')=='true'; const=g(ps,'isConstAtten','false')=='true'
    R=float(g(ps,'RadiusCtrl')); I=float(g(ps,'Intensity')); col=[float(x) for x in g(ps,'ColorCtrl').strip('[]').split(',')]
    sa=float(g(ps,'spotAngle')); sif=float(g(ps,'spotInnerAngleFactor'))
    co=math.cos(sa*math.pi/360); ci=math.cos(sa*sif*math.pi/360); p3x=1/max(ci-co,1e-6); p2w=-co*p3x
    pw=p0w(acp,inv,const)
    rgb=[(c/255)**2.2*I*(400 if inv else 1) for c in col[1:]]
    # model camera-space axis: forward = -row2 of the object matrix (unused for pairing)
    r=dict(L=li,cd_idx=i,name=e['name'][3:],pos_err_m=round(dist,5),cls_attenCurveParam=g(ps,'attenCurveParam','(default 0)'),useInvSq=inv,isConstAtten=const,
           P0w_cap=P0[3],P0w_pred=pw,radius=R,P1w_cap=P1[3],P1w_pred=round(1/R**4,6),spotAngle=sa,inner=sif,P3x_cap=P3[0],P3x_pred=round(p3x,4),P2w_cap=P2[3],P2w_pred=round(p2w,4),
           rgb_cap=[round(x,3) for x in P1[:3]],rgb_pred_gamma22=[round(x,3) for x in rgb],intensity=I,color=col)
    rows.append(r)
with open('armory_pairing.tsv','w') as f:
    f.write('\t'.join(rows[0])+'\n')
    for r in rows: f.write('\t'.join(str(v) for v in r.values())+'\n')
for r in rows: print(json.dumps(r))
