import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.*;
import java.io.*;
public class VtDump extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    Address base = toAddr(Long.decode(a[1])); int n = Integer.parseInt(a[2]); int max = Integer.parseInt(a[3]);
    DecompInterface di = new DecompInterface(); di.openProgram(currentProgram);
    for (int i = 0; i < n; i++) {
      long p = getLong(base.add(i * 8)); Address fa = toAddr(p);
      Function f = getFunctionAt(fa);
      pw.println("## slot " + i + " (+0x" + Integer.toHexString(i*8) + ") -> " + fa + " " + (f==null?"?":f.getName()+" size "+f.getBody().getNumAddresses()));
      if (f != null && f.getBody().getNumAddresses() <= max) { var r = di.decompileFunction(f, 60, monitor); if (r.decompileCompleted()) pw.println(r.getDecompiledFunction().getC()); }
    }
    pw.close();
  }
}
