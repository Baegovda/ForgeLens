# rules_check (PC): <=10 lines. Run at session start / after context summary. Read-only, no windows.
$W = "$env:USERPROFILE\Desktop\SM2_Work"; $R = "$W\RULES_CORE.md"
if (Test-Path $R) { $h = (Get-FileHash $R -Algorithm SHA256).Hash.Substring(0,12); "rules: $h $((Get-Item $R).LastWriteTime.ToString('MM-dd HH:mm'))" } else { "rules: MISSING $R" }
$all = Get-CimInstance Win32_Process -Filter "Name='UnrealEditor.exe' OR Name='UnrealEditor-Cmd.exe'"
$ue = $all | ? { $_.CommandLine -match 'UE_VFX_57' -and $_.CommandLine -notmatch '-run=' }; $oth = $all | ? { $_.CommandLine -notmatch 'UE_VFX_57' }
$cmd = $all | ? { $_.CommandLine -match 'UE_VFX_57' -and $_.CommandLine -match '-run=' }
# netstat, not Get-NetTCPConnection: the latter returned nothing for a live listener on this PC (R47)
$port = [bool](netstat -ano -p tcp | Select-String '127\.0\.0\.1:3000\s+0\.0\.0\.0:0\s+LISTENING')
if (-not $ue) { "UE: editor off -> ROUTE headless" + $(if ($cmd) { " (headless job running pid $($cmd.ProcessId -join ','))" } else { '' }) }
elseif ($port) { "UE: editor pid $($ue.ProcessId -join ',') + port3000 open -> ROUTE EngineWire (uerun.ps1)" }
else { "UE: editor pid $($ue.ProcessId -join ',') but port3000 CLOSED -> bridge DOWN -> UEMCP or fix bridge (Saved\Logs LogMcp)" }
if ($oth) { "UE other projects (not ours, ignore): pid $($oth.ProcessId -join ',')" }
$p = Get-CimInstance Win32_Process -Filter "Name='python.exe' OR Name='java.exe'" | Select-Object ProcessId, CommandLine
$rea = $p | ? { $_.CommandLine -like '*rea_daemon.py*' }; $gs = $p | ? { $_.CommandLine -match 'GhidraServe|proj_sm2' }; $rg = $p | ? { $_.CommandLine -match 'ReaGhidraBridge' }
"REA: $(if ($rea) { 'up pid ' + ($rea.ProcessId -join ',') } else { 'down' }) | GhidraServe: $(if ($gs) { 'up pid ' + ($gs.ProcessId -join ',') } else { 'down' })"
"REA-ghidra child (part of REA, not GhidraServe): $(if ($rg) { $rg.ProcessId -join ',' } else { 'none' })"
$a = "$W\alerts.txt"; $t = if (Test-Path $a) { (Get-Content $a | ? { $_.Trim() }) } else { @() }
if ($t) { "alerts:"; $t | Select-Object -First 4 } else { "alerts: none" }
