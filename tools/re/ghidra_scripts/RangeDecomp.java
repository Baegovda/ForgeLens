import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import java.io.*;
public class RangeDecomp extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    var it = currentProgram.getFunctionManager().getFunctions(toAddr(a[1]), true);
    long end = Long.decode(a[2]);
    while (it.hasNext()) { Function f = it.next(); if (f.getEntryPoint().getOffset() > end) break;
      DecompileResults r = di.decompileFunction(f, 120, monitor);
      pw.println("---- FUNC " + f.getEntryPoint() + " size " + f.getBody().getNumAddresses());
      if (r.decompileCompleted()) pw.println(r.getDecompiledFunction().getC()); }
    pw.close();
  }
}
