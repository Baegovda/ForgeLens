import re,sys,math
from cdlist import parse
F=['lightType','attenCurveParam','useInverseSquaredFalloff','isConstAtten','isAdaptiveIntensity','isNoSpecular','spotAngle','spotInnerAngleFactor','fogAmount','coefIndirect','enableSM','isStatic','isSceneLight']
def grab(ps,key):
    m=re.search(r'\b'+key+r' = \{value = ([^;]*);',ps)
    return m.group(1) if m else ''
def field(ps,k):
    m=re.search(r'\b'+k+r' = ("?[^;]*"?);',ps); return m.group(1).strip('"') if m else ''
def rows(path,scene):
    for i,e in enumerate(parse(path)):
        ps=e.get('ps','')
        if 'sfx_light' not in ps: continue
        m=e['m']
        r={'scene':scene,'idx':i,'name':e['name'],'x':m[12],'y':m[13],'z':m[14],
           'dx':-m[8],'dy':-m[9],'dz':-m[10],'fx':m[4],'fy':m[5],'fz':m[6],
           'radius':grab(ps,'RadiusCtrl'),'color':grab(ps,'ColorCtrl'),'intensity':grab(ps,'Intensity'),
           'ctrl_type':field(ps,'__type').split('"')[0] if False else '', 'n_lights':ps.count('sfx_light')}
        for k in F: r[k]=field(ps,k)
        r['ps_len']=len(ps)
        yield r
if __name__=='__main__':
    out=open(sys.argv[1],'w'); first=True
    for a in sys.argv[2:]:
        path,scene=a.split('=')
        for r in rows(path,scene):
            if first: out.write('\t'.join(r)+'\n'); first=False
            out.write('\t'.join(str(v) for v in r.values())+'\n')
