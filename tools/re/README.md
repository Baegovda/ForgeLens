# SSLB scripts (datapars)
- `sslb.py <file.sslbin> [x]`: classes / members / constants (x = also disassemble every method); `pb.py <file>`: raw protobuf dump. Layout: docs/SYMBOL_DB/scripting.md.

# Light exe RE (no Ghidra auto-analysis) — mtl1-39
- `xref.py <exe> <out.json> --str S | --va 0xHEX ...`: .pdata function bounds + capstone RIP-relative refs.
- `callers.py <exe> 0xTARGET...`: direct call/jmp callers (also via one E9 ILT thunk, e.g. Convert.exe).
- `immscan.py <exe> imm1,imm2,... <min>`: functions using at least <min> of the immediates.
- `patscan.py` / `flagscan.py`: one-off structural scans (array+0x150 stride 0x28 readers; emitter +0xe4/+0xe8 with 0x400). Edit constants.
- `DecompFn.java <out.c> addr...` (Ghidra headless `-noanalysis`: creates functions at addresses and decompiles),
  `DecompAt.java <out.c> addr...` (already-analysed project, `-readOnly -noanalysis`).
Needs pefile + capstone (box: /tmp/cs/bin/python), JAVA_HOME=jdk25, GHIDRA_HEADLESS_MAXMEM≤8G, run under nice.
