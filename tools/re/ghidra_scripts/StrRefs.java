import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.util.*;
public class StrRefs extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    Set<String> s = new HashSet<>(Arrays.asList(a).subList(1, a.length));
    for (Data d : currentProgram.getListing().getDefinedData(true)) { if (!d.hasStringValue()) continue;
      Object v = d.getValue(); if (v == null || !s.contains(v.toString())) continue;
      pw.println("STR '" + v + "' @ " + d.getAddress());
      for (Reference r : getReferencesTo(d.getAddress())) { Function f = getFunctionContaining(r.getFromAddress());
        pw.println("  ref " + r.getFromAddress() + " in " + (f == null ? "?" : f.getEntryPoint() + " size " + f.getBody().getNumAddresses())); }
    }
    pw.close();
  }
}
