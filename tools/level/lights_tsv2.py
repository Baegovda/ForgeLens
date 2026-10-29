# hub_battle_barge light dump: cd_list (name, world matrix, per-object overrides) merged over class_list light descs.
import re,sys,csv
from cdlist import parse as cdp
from classlist import parse as clp
KEYS=['lightType','attenCurveParam','useInverseSquaredFalloff','isConstAtten','isAdaptiveIntensity','isNoSpecular','spotAngle','spotInnerAngleFactor','fogAmount','coefIndirect','enableSM','isStatic','isSceneLight']
CTRL=['RadiusCtrl','ColorCtrl','Intensity']
def get(ps,k):
    if not ps: return None
    m=re.search(r'\b'+k+r' = \{value = ([^;]*);',ps) if k in CTRL else re.search(r'\b'+k+r' = ("?[^;{]*"?);',ps)
    return m.group(1).strip('"') if m else None
def soft(acp,inv,const,typ):
    if typ=='directional': return 0.0
    if inv: return 0.0025
    if const: return 8e22
    return 299*acp**4+1 if acp>0 else 256.0**acp
def main(scn,name,out):
    E=cdp(f'{scn}/{name}.cd_list'); C=clp(f'{scn}/{name}.class_list')
    w=csv.writer(open(out,'w'),delimiter='\t',lineterminator='\n')
    w.writerow(['idx','name','class','x','y','z']+CTRL+KEYS+['src','P0w_pred'])
    n=0
    for i,e in enumerate(E):
        ps=e.get('ps','')
        m=re.findall(r'__type = "([^"]*__actor)"',ps); cls=m[-1] if m else ''
        cps=C.get(cls,'')
        if 'sfx_light' not in ps and 'sfx_light' not in cps: continue
        if ps.count('sfx_light')>1: continue  # containers with several lights: skipped (no per-light transform)
        vals={}; src=[]
        for k in CTRL+KEYS:
            v=get(ps,k); s='cd'
            if v is None: v=get(cps,k); s='cls'
            if v is None: v=''; s='def'
            vals[k]=v; src.append(s[0])
        typ=vals['lightType'] or ('spot' if 'spot_light' in cls else 'point')
        acp=float(vals['attenCurveParam'] or 0); inv=vals['useInverseSquaredFalloff']=='true'; const=vals['isConstAtten']=='true'
        mm=e['m']
        w.writerow([i,e['name'][3:],cls,round(mm[12],4),round(mm[13],4),round(mm[14],4)]+[vals[k] for k in CTRL+KEYS]+[''.join(src),soft(acp,inv,const,typ)]); n+=1
    print(out,n)
main(sys.argv[1],sys.argv[2],sys.argv[3])
