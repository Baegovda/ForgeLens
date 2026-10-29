#!/bin/bash
# rea_dec.sh <addr>... -> /workspace/rea/rea_dec/<addr>.c via the REA daemon
for a in "$@"; do
  REA_MAX=99999999 python3 $(dirname "$0")/../rea_call.py procedure_pseudo_code "{\"procedure\":\"$a\"}" 900 > /tmp/rd_$$_$a.json
  python3 -c "
import json,sys;d=json.load(open('/tmp/rd_$$_$a.json'));r=d.get('normalized_result');
t=r if isinstance(r,str) else (r.get('pseudo_code') or r.get('text') or r.get('code') or json.dumps(r)[:200000] if isinstance(r,dict) else json.dumps(d)[:3000])
open('/workspace/rea/rea_dec/$a.c','w').write(t)"
  echo "$a $(wc -l < /workspace/rea/rea_dec/$a.c) lines"
done
