import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import java.io.*;
public class MemDump extends GhidraScript {
  public void run() throws Exception {
    String[] a = getScriptArgs();
    PrintWriter pw = new PrintWriter(new FileWriter(a[0]));
    Address b = toAddr(Long.decode(a[1])); int n = Integer.parseInt(a[2]);
    for (int i = 0; i < n; i++) { Address x = b.add(i*8); long v = getLong(x); String s = "";
      try { if (v > 0x140000000L && v < 0x146000000L) { Address p = toAddr(v); byte[] bs = getBytes(p, 40); StringBuilder sb = new StringBuilder();
        for (byte c : bs) { if (c == 0) break; if (c < 32 || c > 126) { sb.setLength(0); break; } sb.append((char)c); } s = sb.toString(); } } catch (Exception e) {}
      pw.println(x + ": " + Long.toHexString(v) + (s.isEmpty() ? "" : "  \"" + s + "\""));
    }
    pw.close();
  }
}
