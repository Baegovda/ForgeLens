# oneclick: run a UE python snippet (file) through EngineWire; extra args become sys.argv[1:]
param([string]$py,[string[]]$rest)
$w="C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts\mtl_fix\oneclick"
$argv=@($py)+$rest | ForEach-Object { ($_ -replace '\\','/') }
$code="import sys`nsys.argv=" + (ConvertTo-Json -Compress @($argv)) + "`nexec(compile(open(sys.argv[0],encoding='utf-8').read(),sys.argv[0],'exec'),{'__name__':'__main__'})"
@{code=$code} | ConvertTo-Json | Set-Content -Encoding utf8 "$w\_call.json"
python C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts\enginewire_call.py ue_python "$w\_call.json" 2>&1 | Select-Object -Last 25
