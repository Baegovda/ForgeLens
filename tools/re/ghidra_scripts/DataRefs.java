import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.address.*;
import java.util.*;
import java.io.*;
public class DataRefs extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    int span = Integer.decode(a[1]);
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    Set<Function> fs = new LinkedHashSet<>();
    for (int i = 2; i < a.length; i++) {
      Address base = toAddr(Long.decode(a[i]));
      for (int k = 0; k < span; k += 1) {
        for (Reference r : getReferencesTo(base.add(k))) {
          Function c = getFunctionContaining(r.getFromAddress());
          pw.println("REF " + base.add(k) + " <- " + r.getFromAddress() + " " + r.getReferenceType() + " in " + (c == null ? "?" : c.getEntryPoint()));
          if (c != null) fs.add(c);
        }
      }
    }
    int n = 0;
    for (Function f : fs) {
      if (f.getBody().getNumAddresses() > 15000 || n++ > 30) continue;
      DecompileResults r = di.decompileFunction(f, 120, monitor);
      pw.println("---- FUNC " + f.getEntryPoint() + " size " + f.getBody().getNumAddresses());
      if (r.decompileCompleted()) pw.println(r.getDecompiledFunction().getC());
    }
    pw.close();
  }
}
