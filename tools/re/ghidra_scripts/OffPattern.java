import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.*;
import java.util.*;
import java.io.*;
// OffPattern <out> : functions where for some base B memory operands hit byte[B+0x19], dword[B+0x1c], byte[B+0x20], dword[B+0x24] (light desc layout)
public class OffPattern extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    Listing l = currentProgram.getListing();
    for (Function f : l.getFunctions(true)) {
      if (monitor.isCancelled()) break;
      if (f.getBody().getNumAddresses() > 40000) continue;
      Set<Long> bytes = new HashSet<>(), dws = new HashSet<>();
      for (Instruction in : l.getInstructions(f.getBody(), true)) {
        for (int o = 0; o < in.getNumOperands(); o++) {
          String rep = in.getDefaultOperandRepresentation(o);
          if (!rep.contains("[") || rep.contains("RSP") || rep.contains("RBP")) continue;
          long d = 0; boolean has = false;
          for (Object x : in.getOpObjects(o)) if (x instanceof Scalar sc) { d = sc.getValue(); has = true; }
          if (!has) continue;
          if (rep.startsWith("byte ptr")) bytes.add(d);
          else if (rep.startsWith("dword ptr")) dws.add(d);
        }
      }
      for (long b : dws) {
        long B = b - 0x1c;
        if (B < 0) continue;
        if (bytes.contains(B + 0x19) && bytes.contains(B + 0x20) && dws.contains(B + 0x24))
          pw.println(f.getName() + "@" + f.getEntryPoint() + " B=0x" + Long.toHexString(B) + " size " + f.getBody().getNumAddresses());
      }
    }
    pw.close();
  }
}
