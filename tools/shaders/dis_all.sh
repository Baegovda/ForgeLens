#!/bin/bash
# bulk dxc -dumpbin: out/bin/<fam>/<n>.dxbc -> out/dis/<fam>/<n>.txt (skips existing)
cd /workspace/shaders/out
export LD_LIBRARY_PATH=/home/box/tools/dxc/lib
find bin -name '*.dxbc' | sort | xargs -P 6 -n 50 bash -c 'for f; do o=dis/${f#bin/}; o=${o%.dxbc}.txt; [ -s "$o" ] && continue; mkdir -p $(dirname $o); /home/box/tools/dxc/bin/dxc -dumpbin "$f" > "$o" 2>"$o.err" || echo "FAIL $f"; [ -s "$o.err" ] || rm -f "$o.err"; done' _
