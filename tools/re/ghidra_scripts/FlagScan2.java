import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.Scalar;
import java.util.*;
import java.io.*;
public class FlagScan2 extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    long[] want = {0x20,0x40,0x80,0x100,0x200,0x400,0x800,0x1000,0x2000,0x4000,0x8000};
    Listing l = currentProgram.getListing();
    for (Function f : l.getFunctions(true)) {
      Set<Long> got = new HashSet<>();
      for (Instruction ins : l.getInstructions(f.getBody(), true)) {
        String m = ins.getMnemonicString();
        if (!(m.equals("OR") || m.equals("BTS"))) continue;
        for (int i = 0; i < ins.getNumOperands(); i++) {
          Scalar s = ins.getScalar(i);
          if (s != null) { long v = s.getUnsignedValue(); if (m.equals("BTS")) v = 1L << v; if (!m.equals("MOV") || m.equals("MOV")) got.add(v); }
        }
      }
      int n = 0; for (long w : want) if (got.contains(w)) n++;
      if (n >= 6) pw.println(f.getEntryPoint() + " " + n + " size " + f.getBody().getNumAddresses());
    }
    pw.close();
  }
}
