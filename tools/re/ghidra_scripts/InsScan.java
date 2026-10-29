import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import java.util.*;
import java.util.regex.*;
import java.io.*;
public class InsScan extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    List<List<Pattern>> sets = new ArrayList<>(); List<Pattern> cur = new ArrayList<>();
    for (int i = 1; i < a.length; i++) { if (a[i].equals("--")) { sets.add(cur); cur = new ArrayList<>(); } else cur.add(Pattern.compile(a[i])); }
    sets.add(cur);
    Listing l = currentProgram.getListing();
    for (Function f : l.getFunctions(true)) {
      if (f.getBody().getNumAddresses() > 20000) continue;
      List<String> ins = new ArrayList<>();
      for (Instruction in : l.getInstructions(f.getBody(), true)) ins.add(in.toString());
      for (int s = 0; s < sets.size(); s++) {
        boolean all = true; StringBuilder sb = new StringBuilder();
        for (Pattern p : sets.get(s)) { boolean hit = false; for (String x : ins) if (p.matcher(x).find()) { hit = true; sb.append(" | ").append(x); break; } if (!hit) { all = false; break; } }
        if (all) pw.println("set" + s + " " + f.getName() + "@" + f.getEntryPoint() + " size " + f.getBody().getNumAddresses() + sb);
      }
    }
    pw.close();
  }
}
