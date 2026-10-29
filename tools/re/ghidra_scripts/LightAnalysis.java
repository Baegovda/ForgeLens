import ghidra.app.script.GhidraScript;
public class LightAnalysis extends GhidraScript {
  public void run() throws Exception {
    String[] off = {"Decompiler Parameter ID","Decompiler Switch Analysis","Aggressive Instruction Finder","Stack",
      "Non-Returning Functions - Discovered","Windows x86 PE Exception Handling","Call Convention ID","DWARF","Embedded Media","Variadic Function Signature Override"};
    java.util.Map<String,String> o = getCurrentAnalysisOptionsAndValues(currentProgram);
    for (String k : off) if (o.containsKey(k)) { setAnalysisOption(currentProgram, k, "false"); println("disabled " + k); } else println("missing " + k);
  }
}
