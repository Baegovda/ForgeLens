// GhidraServe: persistent loopback query server over a SAVED Ghidra project (own code, read-only).
// Run as -postScript from analyzeHeadless <proj_dir> <proj_name> -process <exe> -readOnly -noanalysis.
// Writes <state_dir>/serve.json {port, token, pid}; one JSON request per line, one JSON reply per line.
// Request: {"token":T, "tool":NAME, "arguments":{...}}  or  {"token":T, "batch":[{"tool":..,"arguments":..},...]}
// Tools: ping, decompile{address,max_lines?}, xrefs{address,limit?}, search_strings{pattern,limit?},
//        function_at{address}, list_functions{start,end,limit?}, summary{address,pseudo_lines?}, shutdown.
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.data.StringDataInstance;
import ghidra.program.model.scalar.Scalar;
import ghidra.util.task.TaskMonitor;
import com.google.gson.*;
import java.io.*;
import java.net.*;
import java.nio.file.*;
import java.security.SecureRandom;
import java.util.*;
import java.util.regex.*;

public class GhidraServe extends GhidraScript {
    private DecompInterface dec;
    private List<long[]> strAddr = new ArrayList<>();
    private List<String> strVal = new ArrayList<>();
    private final Gson gson = new GsonBuilder().disableHtmlEscaping().create();

    @Override
    public void run() throws Exception {
        String[] a = getScriptArgs();
        String stateDir = a.length > 0 ? a[0] : System.getProperty("java.io.tmpdir");
        int port = a.length > 1 ? Integer.parseInt(a[1]) : 0;
        dec = new DecompInterface();
        dec.setOptions(new DecompileOptions());
        dec.toggleCCode(true); dec.toggleSyntaxTree(false);
        dec.openProgram(currentProgram);
        long t0 = System.currentTimeMillis();
        DataIterator it = currentProgram.getListing().getDefinedData(true);
        while (it.hasNext()) {
            Data d = it.next();
            if (!d.hasStringValue()) continue;
            Object v = d.getValue();
            if (v == null) continue;
            strAddr.add(new long[]{d.getAddress().getOffset()}); strVal.add(v.toString());
        }
        println("GhidraServe: strings indexed " + strVal.size() + " in " + (System.currentTimeMillis() - t0) + " ms");
        byte[] tb = new byte[16]; new SecureRandom().nextBytes(tb);
        StringBuilder tok = new StringBuilder(); for (byte b : tb) tok.append(String.format("%02x", b));
        String token = tok.toString();
        try (ServerSocket ss = new ServerSocket(port, 8, InetAddress.getByName("127.0.0.1"))) {
            JsonObject st = new JsonObject();
            st.addProperty("port", ss.getLocalPort()); st.addProperty("token", token);
            st.addProperty("pid", ProcessHandle.current().pid());
            st.addProperty("program", currentProgram.getName());
            Path sp = Paths.get(stateDir, "serve.json"), tmp = Paths.get(stateDir, "serve.json.tmp");
            Files.writeString(tmp, gson.toJson(st)); Files.move(tmp, sp, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
            println("GhidraServe: listening 127.0.0.1:" + ss.getLocalPort());
            boolean stop = false;
            while (!stop && !monitor.isCancelled()) {
                try (Socket s = ss.accept();
                     BufferedReader in = new BufferedReader(new InputStreamReader(s.getInputStream(), "UTF-8"));
                     Writer out = new OutputStreamWriter(s.getOutputStream(), "UTF-8")) {
                    String line;
                    while ((line = in.readLine()) != null) {
                        JsonObject resp;
                        JsonObject req = null;
                        try { req = JsonParser.parseString(line).getAsJsonObject(); } catch (Exception e) { }
                        if (req == null || !req.has("token") || !token.equals(req.get("token").getAsString())) {
                            resp = err("bad request or token");
                        } else if (req.has("batch")) {
                            JsonArray res = new JsonArray();
                            for (JsonElement e : req.getAsJsonArray("batch")) {
                                JsonObject q = e.getAsJsonObject();
                                if ("shutdown".equals(str(q, "tool", ""))) stop = true;
                                res.add(timed(q));
                            }
                            resp = new JsonObject(); resp.add("batch", res);
                        } else {
                            if ("shutdown".equals(str(req, "tool", ""))) stop = true;
                            resp = timed(req);
                        }
                        out.write(gson.toJson(resp)); out.write("\n"); out.flush();
                        if (stop) break;
                    }
                } catch (IOException e) { println("conn: " + e); }
            }
        } finally {
            dec.dispose();
            try { Files.deleteIfExists(Paths.get(stateDir, "serve.json")); } catch (Exception e) { }
        }
    }

    private JsonObject timed(JsonObject q) {
        long t = System.nanoTime();
        JsonObject r;
        try { r = handle(str(q, "tool", ""), q.has("arguments") ? q.getAsJsonObject("arguments") : new JsonObject()); }
        catch (Exception e) { r = err(e.toString()); }
        r.addProperty("ms", (System.nanoTime() - t) / 1e6);
        return r;
    }

    private static JsonObject err(String m) { JsonObject o = new JsonObject(); o.addProperty("error", m); return o; }
    private static String str(JsonObject o, String k, String d) { return o.has(k) ? o.get(k).getAsString() : d; }
    private static int num(JsonObject o, String k, int d) { return o.has(k) ? o.get(k).getAsInt() : d; }
    private Address addr(String s) {
        s = s.trim(); if (s.startsWith("FUN_")) s = s.substring(4);
        return currentProgram.getAddressFactory().getDefaultAddressSpace().getAddress(Long.parseUnsignedLong(s.replaceFirst("^0x", ""), 16));
    }
    private Function fn(String s) {
        Address a = addr(s);
        Function f = getFunctionAt(a); if (f == null) f = getFunctionContaining(a);
        if (f == null) throw new IllegalArgumentException("no function at " + s);
        return f;
    }
    private static String hex(Address a) { return "0x" + Long.toHexString(a.getOffset()); }

    private JsonObject handle(String tool, JsonObject args) throws Exception {
        JsonObject r = new JsonObject();
        switch (tool) {
        case "ping": r.addProperty("ok", currentProgram.getName()); return r;
        case "shutdown": r.addProperty("ok", "stopping"); return r;
        case "function_at": {
            Function f = fn(str(args, "address", str(args, "procedure", "")));
            r.addProperty("name", f.getName()); r.addProperty("entry", hex(f.getEntryPoint()));
            r.addProperty("signature", f.getSignature().getPrototypeString());
            r.addProperty("size", f.getBody().getNumAddresses()); return r;
        }
        case "decompile": case "procedure_pseudo_code": {
            Function f = fn(str(args, "address", str(args, "procedure", "")));
            String c = decomp(f);
            int ml = num(args, "max_lines", 0);
            if (ml > 0) c = trim(c, ml);
            r.addProperty("name", f.getName()); r.addProperty("entry", hex(f.getEntryPoint())); r.addProperty("code", c); return r;
        }
        case "xrefs": {
            Address a = addr(str(args, "address", ""));
            int lim = num(args, "limit", 200);
            JsonArray arr = new JsonArray(); int n = 0;
            for (Reference ref : getReferencesTo(a)) {
                if (n++ >= lim) break;
                JsonObject o = new JsonObject();
                o.addProperty("from", hex(ref.getFromAddress())); o.addProperty("type", ref.getReferenceType().getName());
                Function f = getFunctionContaining(ref.getFromAddress());
                if (f != null) o.addProperty("function", f.getName() + "@" + hex(f.getEntryPoint()));
                arr.add(o);
            }
            r.add("xrefs", arr); r.addProperty("count", n); return r;
        }
        case "search_strings": {
            Pattern p = Pattern.compile(str(args, "pattern", str(args, "query", "")));
            int lim = num(args, "limit", 100);
            JsonArray arr = new JsonArray();
            for (int i = 0; i < strVal.size() && arr.size() < lim; i++)
                if (p.matcher(strVal.get(i)).find()) {
                    JsonObject o = new JsonObject();
                    o.addProperty("address", "0x" + Long.toHexString(strAddr.get(i)[0])); o.addProperty("value", strVal.get(i));
                    arr.add(o);
                }
            r.add("strings", arr); return r;
        }
        case "list_functions": {
            Address s = addr(str(args, "start", "0")), e = addr(str(args, "end", "ffffffffffff"));
            int lim = num(args, "limit", 500);
            JsonArray arr = new JsonArray();
            FunctionIterator fi = currentProgram.getFunctionManager().getFunctions(s, true);
            while (fi.hasNext() && arr.size() < lim) {
                Function f = fi.next(); if (f.getEntryPoint().compareTo(e) > 0) break;
                arr.add(f.getName() + "@" + hex(f.getEntryPoint()) + " size=" + f.getBody().getNumAddresses());
            }
            r.add("functions", arr); return r;
        }
        case "summary": return summary(fn(str(args, "address", str(args, "procedure", ""))), num(args, "pseudo_lines", 0));
        default: return err("unknown tool " + tool);
        }
    }

    private String decomp(Function f) {
        DecompileResults res = dec.decompileFunction(f, 120, TaskMonitor.DUMMY);
        if (res == null || !res.decompileCompleted() || res.getDecompiledFunction() == null)
            throw new RuntimeException("decompile failed: " + (res == null ? "null" : res.getErrorMessage()));
        return res.getDecompiledFunction().getC();
    }

    private static String trim(String c, int ml) {
        String[] ls = c.split("\n"); StringBuilder b = new StringBuilder(); int n = 0;
        for (String l : ls) { if (l.isBlank()) continue; if (n++ >= ml) { b.append("... (").append(ls.length).append(" lines total)\n"); break; } b.append(l).append('\n'); }
        return b.toString();
    }

    // Compact view: signature, callees, callers count, referenced strings/globals, notable constants.
    private JsonObject summary(Function f, int pseudo) {
        JsonObject r = new JsonObject();
        r.addProperty("name", f.getName()); r.addProperty("entry", hex(f.getEntryPoint()));
        r.addProperty("signature", f.getSignature().getPrototypeString());
        r.addProperty("size", f.getBody().getNumAddresses());
        Set<String> callees = new LinkedHashSet<>();
        for (Function c : f.getCalledFunctions(TaskMonitor.DUMMY)) callees.add(c.getName() + "@" + hex(c.getEntryPoint()));
        JsonArray ca = new JsonArray(); callees.forEach(ca::add); r.add("calls", ca);
        int callers = 0; for (Function c : f.getCallingFunctions(TaskMonitor.DUMMY)) callers++;
        r.addProperty("callers", callers);
        Set<String> strs = new LinkedHashSet<>(), globs = new LinkedHashSet<>(); Set<Long> consts = new TreeSet<>();
        Listing L = currentProgram.getListing();
        InstructionIterator ii = L.getInstructions(f.getBody(), true);
        while (ii.hasNext()) {
            Instruction ins = ii.next();
            for (Reference ref : ins.getReferencesFrom()) {
                if (ref.getReferenceType().isFlow()) continue;
                Address to = ref.getToAddress(); if (!to.isMemoryAddress()) continue;
                Data d = L.getDataAt(to);
                if (d != null && d.hasStringValue() && d.getValue() != null) { strs.add(hex(to) + " \"" + d.getValue() + "\""); continue; }
                if (getFunctionAt(to) != null) continue;
                Symbol s = getSymbolAt(to);
                globs.add(s != null ? s.getName() + "@" + hex(to) : hex(to));
            }
            for (int op = 0; op < ins.getNumOperands(); op++)
                for (Object o : ins.getOpObjects(op))
                    if (o instanceof Scalar) {
                        long v = ((Scalar) o).getUnsignedValue();
                        if (v > 0x10 && v < 0x100000000L && !(v >= 0x140000000L)) consts.add(v);
                    }
        }
        JsonArray sa = new JsonArray(); strs.forEach(sa::add); r.add("strings", sa);
        JsonArray ga = new JsonArray(); int gn = 0; for (String g : globs) { if (gn++ >= 40) break; ga.add(g); } r.add("globals", ga);
        r.addProperty("globals_total", globs.size());
        JsonArray ka = new JsonArray(); int kn = 0; for (Long v : consts) { if (kn++ >= 40) break; ka.add("0x" + Long.toHexString(v)); } r.add("constants", ka);
        if (pseudo > 0) r.addProperty("code", trim(decomp(f), pseudo));
        return r;
    }
}
