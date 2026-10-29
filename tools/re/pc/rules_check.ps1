# rules_check (PC): <=10 lines. Run at session start / after context summary. Read-only, no windows.
$W = "$env:USERPROFILE\Desktop\SM2_Work"; $R = "$W\RULES_CORE.md"
if (Test-Path $R) { $h = (Get-FileHash $R -Algorithm SHA256).Hash.Substring(0,12); "rules: $h $((Get-Item $R).LastWriteTime.ToString('MM-dd HH:mm'))" } else { "rules: MISSING $R" }
$ue = Get-Process UnrealEditor -ErrorAction SilentlyContinue
$port = [bool](Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue)
if ($ue) { "UE: running pid $($ue.Id -join ',') port3000=$port -> ROUTE EngineWire (uerun.ps1)" } else { "UE: not running -> ROUTE headless" }
$p = Get-CimInstance Win32_Process -Filter "Name='python.exe' OR Name='java.exe'" | Select-Object ProcessId, CommandLine
$rea = $p | ? { $_.CommandLine -like '*rea_daemon.py*' }; $gs = $p | ? { $_.CommandLine -match 'GhidraServe|proj_sm2' }
"REA: $(if ($rea) { 'up pid ' + ($rea.ProcessId -join ',') } else { 'down' }) | GhidraServe: $(if ($gs) { 'up pid ' + ($gs.ProcessId -join ',') } else { 'down' })"
$a = "$W\alerts.txt"; $t = if (Test-Path $a) { (Get-Content $a | ? { $_.Trim() }) } else { @() }
if ($t) { "alerts:"; $t | Select-Object -First 4 } else { "alerts: none" }
