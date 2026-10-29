import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import java.util.*;
import java.util.regex.*;
import java.io.*;
public class InsScanR extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    long lo = Long.decode(a[1]), hi = Long.decode(a[2]); int maxSize = Integer.parseInt(a[3]);
    List<Pattern> ps = new ArrayList<>(); for (int i = 4; i < a.length; i++) ps.add(Pattern.compile(a[i]));
    Listing l = currentProgram.getListing();
    var it = currentProgram.getFunctionManager().getFunctions(toAddr(lo), true);
    while (it.hasNext()) { Function f = it.next(); if (f.getEntryPoint().getOffset() > hi) break;
      if (f.getBody().getNumAddresses() > maxSize) continue;
      List<String> ins = new ArrayList<>(); for (Instruction in : l.getInstructions(f.getBody(), true)) ins.add(in.toString());
      StringBuilder sb = new StringBuilder(); boolean all = true;
      for (Pattern p : ps) { boolean hit = false; for (String x : ins) if (p.matcher(x).find()) { hit = true; sb.append(" | ").append(x); break; } if (!hit) { all = false; break; } }
      if (all) pw.println(f.getName() + "@" + f.getEntryPoint() + " size " + f.getBody().getNumAddresses() + sb);
    }
    pw.close();
  }
}
