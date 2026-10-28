#!/bin/bash
# dxil-spirv --glsl for chosen families: out/bin/<fam>/*.dxbc -> out/glsl/<fam>/*.glsl (skips existing)
cd /workspace/shaders/out
for fam in "$@"; do find bin/$fam -name '*.dxbc'; done | sort | xargs -P 6 -n 40 bash -c 'for f; do o=glsl/${f#bin/}; o=${o%.dxbc}.glsl; [ -s "$o" ] && continue; mkdir -p $(dirname $o); timeout 60 /home/box/tools/dxil-spirv/build/dxil-spirv "$f" --glsl --output "$o" >/dev/null 2>"$o.err" || echo "FAIL $f"; [ -s "$o.err" ] || rm -f "$o.err"; done' _
