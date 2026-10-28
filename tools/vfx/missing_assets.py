"""mtl1-36: generic missing VFX asset fetch. Collects missingTextures / missingTemplates from *.vfxframe.json files and
writes a PowerShell script that runs the extractor hidden at Idle priority (WORKFLOW "Missing textures"), zips the
output and lists the files. Then CopyToBox the zip and `cp -rn` the texture files into <raw>/texall/tex and the tpl
dirs into <raw>/mesh1 (both are searched recursively by the exporter).

usage: missing_assets.py <out.ps1> <outName> <frame.json|dir> [...]
  outName: folder under SM2_Work\\Raw\\vfx (e.g. tex36)
"""
import glob, json, os, re, sys

CLIENT = r'"D:\Steam\steamapps\common\Space Marine 2\client_pc"'
EXE = r'C:\Users\Revaptor_FX\SM2Extractor\LibSaber.SM2Extractor.exe'
RAW = r'C:\Users\Revaptor_FX\Desktop\SM2_Work\Raw\vfx'


def texture_filters(name):
    return [f"pct/{name}.pct.resource", f"pct/{name}_?.pct_mip", f"pct/{name}_??.pct_mip", f"td/{name}.td", f"td/{name}.td.resource"]


def template_filters(name):
    return [f"tpl/{name}.tpl/*"]


def collect(paths):
    tex, tpl = set(), set()
    for p in paths:
        files = glob.glob(os.path.join(p, "*.vfxframe.json")) if os.path.isdir(p) else [p]
        for f in files:
            fr = json.load(open(f))
            for t in fr.get("missingTextures", []):
                t = re.sub(r"\s*\(.*\)$", "", t)  # "name (error)" -> name
                if t: tex.add(t)
            for t in fr.get("missingTemplates", []):
                if t: tpl.add(t)
    return sorted(tex), sorted(tpl)


def script(tex, tpl, out_name):
    filters = [f for t in tex for f in texture_filters(t)] + [f for t in tpl for f in template_filters(t)]
    args = ",".join(["'extract'", f"'{CLIENT}'", "'--output'", "$out", "'-q'"] + [f"'--filter','{f}'" for f in filters])
    return "\n".join([
        f"$out='{RAW}\\{out_name}'",
        f"$a = @({args})",
        f"$p = Start-Process -FilePath '{EXE}' -ArgumentList $a -WindowStyle Hidden -PassThru",
        "$p.PriorityClass = 'Idle'",
        "$p.WaitForExit()",
        '"exit $($p.ExitCode)"',
        f"if (Test-Path $out) {{ Compress-Archive -Path \"$out\\*\" -DestinationPath '{RAW}\\{out_name}.zip' -Force; Get-ChildItem -Recurse -File $out | Select-Object -ExpandProperty FullName }}",
        ""])


if __name__ == "__main__":
    tex, tpl = collect(sys.argv[3:])
    print("textures", tex); print("templates", tpl)
    if tex or tpl:
        open(sys.argv[1], "w", newline="\r\n").write(script(tex, tpl, sys.argv[2]))
        print("wrote", sys.argv[1])
