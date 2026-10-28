import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.*;
import ghidra.program.model.symbol.*;
import java.util.*;
import java.io.*;
public class DecompAt extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    Set<Function> done = new HashSet<>();
    List<Function> todo = new ArrayList<>();
    Set<String> strs = new HashSet<>();
    for (int i = 1; i < a.length; i++) {
      if (a[i].startsWith("0x")) {
        Address ad = toAddr(a[i].substring(2));
        Function f = getFunctionContaining(ad);
        pw.println("### ADDR " + a[i] + " in " + (f == null ? "?" : f.getName() + "@" + f.getEntryPoint()));
        if (f != null) todo.add(f);
      } else strs.add(a[i]);
    }
    if (!strs.isEmpty())
      for (Data d : currentProgram.getListing().getDefinedData(true)) { if (!d.hasStringValue()) continue;
        Object v = d.getValue(); if (v == null || !strs.contains(v.toString())) continue;
        pw.println("### STRING '" + v + "' @ " + d.getAddress());
        for (Reference r : getReferencesTo(d.getAddress())) {
          Function f = getFunctionContaining(r.getFromAddress());
          pw.println("  ref " + r.getFromAddress() + " in " + (f == null ? "?" : f.getName() + "@" + f.getEntryPoint()));
          if (f != null) todo.add(f);
        }
      }
    for (Function f : todo) {
      if (done.contains(f) || f.getBody().getNumAddresses() > 60000) continue;
      done.add(f);
      DecompileResults res = di.decompileFunction(f, 180, monitor);
      pw.println("---- FUNC " + f.getEntryPoint() + " size " + f.getBody().getNumAddresses());
      if (res.decompileCompleted()) pw.println(res.getDecompiledFunction().getC());
    }
    pw.close();
  }
}
