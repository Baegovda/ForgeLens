# PoE2 M2 F: private EngineWire runner. Scripts\uerun.ps1 writes the SHARED C:\Users\Revaptor_FX\ue_args.json, so two workers
# calling it at the same moment swap scripts (observed 07:xx: this worker's call executed ue_r97_orbhub7.py). This one uses
# its own args file per call.
param([string]$py, [string[]]$argv)
$a = ($argv | ForEach-Object { "'" + ($_ -replace '\\','/') + "'" }) -join ','
$code = "import sys, runpy`nsys.argv = ['" + ($py -replace '\\','/') + "'," + $a + "]`nrunpy.run_path(sys.argv[0], run_name='__main__')"
$f = Join-Path $env:TEMP ("poe2_ue_args_" + [guid]::NewGuid().ToString('N') + ".json")
@{code=$code} | ConvertTo-Json | Set-Content -Encoding utf8 $f
try { & python C:\Users\Revaptor_FX\enginewire_call.py ue_python $f; $rc = $LASTEXITCODE } finally { Remove-Item $f -ErrorAction SilentlyContinue }
exit $rc
