param([string]$py, [string[]]$argv)
# Runs a tools/ue/*.py script inside Unreal Editor.
# Primary: EngineWire / MCP Automation Bridge native MCP (C:\Users\Revaptor_FX\enginewire_call.py, http://127.0.0.1:3000/mcp).
# Fallback: UEMCP (C:\Users\Revaptor_FX\uemcp_call.py) when port 3000 is not listening, or when $env:UE_MCP = 'uemcp'.
$a = ($argv | ForEach-Object { "'" + ($_ -replace '\\','/') + "'" }) -join ','
$code = "import sys, runpy`nsys.argv = ['" + ($py -replace '\\','/') + "'," + $a + "]`nrunpy.run_path(sys.argv[0], run_name='__main__')"
@{code=$code} | ConvertTo-Json | Set-Content -Encoding utf8 C:\Users\Revaptor_FX\ue_args.json
$ew = $env:UE_MCP -ne 'uemcp' -and (Test-NetConnection 127.0.0.1 -Port 3000 -WarningAction SilentlyContinue -InformationLevel Quiet)
if ($ew) { & python C:\Users\Revaptor_FX\enginewire_call.py ue_python C:\Users\Revaptor_FX\ue_args.json }
else { Write-Host '[uerun] EngineWire not reachable -> UEMCP fallback'; & python C:\Users\Revaptor_FX\uemcp_call.py ue_python C:\Users\Revaptor_FX\ue_args.json }
exit $LASTEXITCODE
