import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.*;
import java.io.*;
public class MkDecomp extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    for (int i = 1; i < a.length; i++) {
      Address ad = toAddr(Long.decode(a[i]));
      Function f = getFunctionAt(ad);
      if (f == null) { disassemble(ad); f = createFunction(ad, null); }
      pw.println("---- FUNC " + a[i] + " " + (f==null?"FAILED":"size "+f.getBody().getNumAddresses()));
      if (f == null) continue;
      var r = di.decompileFunction(f, 120, monitor); if (r.decompileCompleted()) pw.println(r.getDecompiledFunction().getC());
    }
    pw.close();
  }
}
