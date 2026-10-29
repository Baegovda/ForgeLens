#!/bin/bash
# gd.sh <out.c> <addrs|strings...>  headless DecompAt on ghidra_proj3 (readOnly, noanalysis), 3.5G heap
out=$1; shift
export JAVA_HOME=/home/box/tools/jdk25
GHIDRA_HEADLESS_MAXMEM=${GMEM:-3500M} nice -n 10 /home/box/ghidra/support/analyzeHeadless /workspace/rea/ghidra_proj3 sm2 -process sm2_retail.exe -readOnly -noanalysis -scriptPath /workspace/rea/scripts -postScript ${GSCRIPT:-DecompAt.java} $out "$@" > ${out%.c}.log 2>&1
grep -E "ERROR|Exception|OutOfMemory" ${out%.c}.log | head -5
