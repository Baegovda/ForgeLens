// Decompile functions at given entry addresses without auto-analysis: disassemble + create the function if missing.
// args: <out.c> 0xADDR...   (optional "callees" as last arg: also decompile direct callees one level deep)
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.app.cmd.disassemble.DisassembleCommand;
import ghidra.app.cmd.function.CreateFunctionCmd;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.*;
import java.util.*;
import java.io.*;
public class DecompFn extends GhidraScript {
  Function ensure(Address ad) {
    Function f = getFunctionAt(ad);
    if (f != null && f.getBody().getNumAddresses() > 16) return f;
    if (f != null) removeFunction(f);
    new DisassembleCommand(ad, null, true).applyTo(currentProgram, monitor);
    new CreateFunctionCmd(ad).applyTo(currentProgram, monitor);
    return getFunctionAt(ad);
  }
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    boolean callees = a[a.length - 1].equals("callees");
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    List<Function> todo = new ArrayList<>();
    for (int i = 1; i < a.length; i++) if (a[i].startsWith("0x")) { Function f = ensure(toAddr(a[i].substring(2))); if (f != null) todo.add(f); else pw.println("### no function at " + a[i]); }
    Set<Address> done = new HashSet<>();
    int n0 = todo.size();
    for (int k = 0; k < todo.size(); k++) {
      Function f = todo.get(k);
      if (!done.add(f.getEntryPoint())) continue;
      DecompileResults res = di.decompileFunction(f, 240, monitor);
      pw.println("---- FUNC " + f.getEntryPoint() + " size " + f.getBody().getNumAddresses() + (k >= n0 ? " (callee)" : ""));
      if (res.decompileCompleted()) pw.println(res.getDecompiledFunction().getC()); else pw.println("decompile failed: " + res.getErrorMessage());
      if (callees && k < n0)
        for (Instruction ins : currentProgram.getListing().getInstructions(f.getBody(), true))
          if (ins.getFlowType().isCall()) for (Address t : ins.getFlows()) { Function c = ensure(t); if (c != null) todo.add(c); }
      pw.flush();
    }
    pw.close();
  }
}
