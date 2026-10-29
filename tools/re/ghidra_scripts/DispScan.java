import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.*;
import java.util.*;
import java.io.*;
public class DispScan extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    long lo = Long.decode(a[1]), hi = Long.decode(a[2]);
    Set<Long> want = new HashSet<>();
    for (int i = 3; i < a.length; i++) want.add(Long.decode(a[i]));
    Map<Function, Set<String>> hits = new LinkedHashMap<>();
    InstructionIterator it = currentProgram.getListing().getInstructions(toAddr(lo), true);
    while (it.hasNext() && !monitor.isCancelled()) {
      Instruction ins = it.next();
      if (ins.getAddress().getOffset() > hi) break;
      String s = ins.toString();
      if (!s.contains("[")) continue;
      for (int o = 0; o < ins.getNumOperands(); o++)
        for (Object x : ins.getOpObjects(o))
          if (x instanceof Scalar sc && want.contains(sc.getValue()) && ins.getDefaultOperandRepresentation(o).contains("[")) {
            Function f = getFunctionContaining(ins.getAddress());
            pw.println(ins.getAddress() + " " + s + " in " + (f == null ? "?" : f.getEntryPoint()));
            if (f != null) hits.computeIfAbsent(f, k -> new TreeSet<>()).add(Long.toHexString(sc.getValue()));
          }
    }
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    int n = 0;
    for (var e : hits.entrySet()) {
      Function f = e.getKey();
      if (f.getBody().getNumAddresses() > 20000 || n++ > 120) continue;
      DecompileResults r = di.decompileFunction(f, 120, monitor);
      pw.println("---- FUNC " + f.getEntryPoint() + " offs " + e.getValue() + " size " + f.getBody().getNumAddresses());
      if (r.decompileCompleted()) pw.println(r.getDecompiledFunction().getC());
    }
    pw.close();
  }
}
