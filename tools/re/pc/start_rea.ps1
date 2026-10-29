# Start the REA daemon (rea-agents 6.8.0, pinned) at High priority, open the exe, wait until analysis is ready.
# Copy to / run from re_env\work (rea_daemon.py + rea_call.py live there). Skips start if a daemon is already running.
# Usage: powershell -File start_rea.ps1      (first ready ~11 min on the PC; REA deletes its temp project on exit)
$W = "$env:USERPROFILE\Desktop\SM2_Work\Tools\re_env\work"; Set-Location $W
python "$PSScriptRoot\..\rea_version.py"; if ($LASTEXITCODE) { throw "REA version guard failed (pinned 6.8.0)" }
$run = Get-CimInstance Win32_Process | ? { $_.CommandLine -like "*rea_daemon.py*" }
if ($run) { "REA daemon already running (pid $($run.ProcessId -join ',')): no-op"; exit 0 }   # idempotent (Task Scheduler logon)
if (-not $run) {
  New-Item -ItemType Directory -Force "$W\rea_q" | Out-Null; Remove-Item "$W\rea_daemon.ready" -ErrorAction SilentlyContinue
  Start-Process python -ArgumentList "-u rea_daemon.py" -WorkingDirectory $W -WindowStyle Hidden -RedirectStandardOutput "$W\rea_daemon.log" -RedirectStandardError "$W\rea_daemon.err.log"
  while (-not (Test-Path "$W\rea_daemon.ready")) { Start-Sleep 2 }
  python rea_call.py open_binary ('{"path":"' + ("$W\sm2_retail.exe" -replace '\\','\\') + '","provider_id":"ghidra"}') 600
}
function High { Get-CimInstance Win32_Process | ? { $_.Name -in 'java.exe','node.exe','python.exe' -and $_.CommandLine -match 'rea' } | % { try { (Get-Process -Id $_.ProcessId).PriorityClass = 'High' } catch {} } }
$t0 = Get-Date
do { High; $r = python rea_call.py search_strings '{"pattern":"attenCurveParam"}' 3600 } until ($r -match '0x143684c18' -or ((Get-Date) - $t0).TotalHours -gt 2)
High; "REA ready after $([int]((Get-Date)-$t0).TotalMinutes) min"
