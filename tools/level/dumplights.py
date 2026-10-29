import json,sys
def find(o):
    if isinstance(o,dict):
        if o.get('n','').startswith('COMMON_DYN_LIGHT_PARAMS') or o.get('n','')=='COMMON_DYN_LIGHT_COUNT' or 'LIGHT_COUNT' in o.get('n',''): yield o
        for v in o.values(): yield from find(v)
    elif isinstance(o,list):
        for v in o: yield from find(v)
for f in sys.argv[1:]:
    d=json.load(open(f)); seen=set()
    for e in d:
        for o in find(e):
            s=json.dumps(o)
            if s in seen: continue
            seen.add(s); print(f, e['eid'], s[:3000])
