import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.*;
import java.util.*;
import java.io.*;
// OffSet <out> b:0x6d d:0x54 ... : functions having all listed memory-operand displacements (b=byte, d=dword, q=any) ignoring RSP/RBP
public class OffSet extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    List<String> want = new ArrayList<>(Arrays.asList(a).subList(1, a.length));
    Listing l = currentProgram.getListing();
    for (Function f : l.getFunctions(true)) {
      if (monitor.isCancelled()) break;
      if (f.getBody().getNumAddresses() > 40000) continue;
      Set<String> have = new HashSet<>();
      for (Instruction in : l.getInstructions(f.getBody(), true)) {
        for (int o = 0; o < in.getNumOperands(); o++) {
          String rep = in.getDefaultOperandRepresentation(o);
          if (!rep.contains("[") || rep.contains("RSP") || rep.contains("RBP")) continue;
          for (Object x : in.getOpObjects(o)) if (x instanceof Scalar sc) {
            String h = "0x" + Long.toHexString(sc.getValue());
            have.add("q:" + h);
            if (rep.startsWith("byte ptr")) have.add("b:" + h);
            if (rep.startsWith("dword ptr")) have.add("d:" + h);
          }
        }
      }
      if (have.containsAll(want)) pw.println(f.getName() + "@" + f.getEntryPoint() + " size " + f.getBody().getNumAddresses());
    }
    pw.close();
  }
}
