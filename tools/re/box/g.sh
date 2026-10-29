#!/bin/bash
# usage: g.sh Script.java args...
s=$1; shift
JAVA_HOME=/home/box/tools/jdk25 /home/box/tools/ghidra_12.1.4_PUBLIC/support/analyzeHeadless /workspace/rea/ghidra_proj3 sm2 -process sm2_retail.exe -readOnly -noanalysis -scriptPath /workspace/rea/scripts -postScript $s "$@" > /tmp/g_last.log 2>&1
grep -E "ERROR|Exception" /tmp/g_last.log | head -5
