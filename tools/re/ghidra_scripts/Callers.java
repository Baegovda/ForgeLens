import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.util.*;
import java.io.*;
public class Callers extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    int depth = Integer.parseInt(a[1]);
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    Set<Function> done = new HashSet<>();
    List<Function> cur = new ArrayList<>();
    for (int i = 2; i < a.length; i++) cur.add(getFunctionContaining(toAddr(Long.decode(a[i]))));
    for (int d = 0; d < depth; d++) {
      List<Function> next = new ArrayList<>();
      for (Function f : cur) {
        if (f == null) continue;
        for (Reference r : getReferencesTo(f.getEntryPoint())) {
          Function c = getFunctionContaining(r.getFromAddress());
          pw.println("XREF " + f.getEntryPoint() + " <- " + r.getFromAddress() + " " + r.getReferenceType() + " in " + (c == null ? "?" : c.getEntryPoint()));
          if (c != null && done.add(c)) next.add(c);
        }
      }
      cur = next;
    }
    for (Function f : done) {
      if (f.getBody().getNumAddresses() > 15000) { pw.println("---- SKIP big " + f.getEntryPoint()); continue; }
      DecompileResults r = di.decompileFunction(f, 120, monitor);
      pw.println("---- FUNC " + f.getEntryPoint() + " size " + f.getBody().getNumAddresses());
      if (r.decompileCompleted()) pw.println(r.getDecompiledFunction().getC());
    }
    pw.close();
  }
}
