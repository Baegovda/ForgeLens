# GhidraServe launcher (PC). Usage: gs.ps1 import | start | stop | status
#   import : one-time headless import+analysis of the exe into a SAVED project (re_env\proj_sm2). ~10-15 min.
#   start  : open the saved project read-only and serve queries on 127.0.0.1 (High priority, hidden). Restart = minutes.
#   stop   : send shutdown (closes the project cleanly).
# Rule: one Ghidra process per project folder. Do not run another analyzeHeadless on proj_sm2 while the server is up.
param([Parameter(Mandatory)][ValidateSet('import','start','stop','status')]$cmd)
$R = "$env:USERPROFILE\Desktop\SM2_Work\Tools\re_env"
$G = "$R\ghidra_12.1.4_PUBLIC\support\analyzeHeadless.bat"
$env:JAVA_HOME = (Get-ChildItem "$R\jdk-25*" -Directory | Select-Object -First 1).FullName
$P = "$R\proj_sm2"; $S = "$R\gs_state"; $Exe = "$R\work\sm2_retail.exe"
$Scr = Split-Path -Parent $MyInvocation.MyCommand.Path
New-Item -ItemType Directory -Force $P, $S | Out-Null
if (-not $env:MAXMEM) { $env:MAXMEM = "24G" }
switch ($cmd) {
 'import' {
   $p = Start-Process -FilePath $G -ArgumentList "`"$P`" sm2 -import `"$Exe`" -overwrite" -RedirectStandardOutput "$S\import.log" -RedirectStandardError "$S\import.err" -WindowStyle Hidden -PassThru
   Start-Sleep 5; Get-CimInstance Win32_Process -Filter "Name='java.exe'" | ? { $_.CommandLine -like "*proj_sm2*" } | % { (Get-Process -Id $_.ProcessId).PriorityClass = 'High' }
   "import started pid $($p.Id); log $S\import.log"
 }
 'start' {
   if ((Test-Path "$S\serve.json") -and ((python "$Scr\gs.py" ping 2>$null) -match '"ok"')) { "GhidraServe already running: no-op"; return }   # idempotent
   Remove-Item "$S\serve.json" -ErrorAction SilentlyContinue
   $t0 = Get-Date
   $p = Start-Process -FilePath $G -ArgumentList "`"$P`" sm2 -process sm2_retail.exe -readOnly -noanalysis -scriptPath `"$Scr`" -postScript GhidraServe.java `"$S`"" -RedirectStandardOutput "$S\serve.log" -RedirectStandardError "$S\serve.err" -WindowStyle Hidden -PassThru
   Start-Sleep 5; Get-CimInstance Win32_Process -Filter "Name='java.exe'" | ? { $_.CommandLine -like "*proj_sm2*" } | % { (Get-Process -Id $_.ProcessId).PriorityClass = 'High' }
   while (-not (Test-Path "$S\serve.json")) { if ((Get-Date) - $t0 -gt [TimeSpan]::FromMinutes(30)) { throw "not ready; see $S\serve.log" }; Start-Sleep 2 }
   "ready in $([int]((Get-Date) - $t0).TotalSeconds) s: $(Get-Content $S\serve.json)"
 }
 'stop'   { python "$Scr\gs.py" shutdown }
 'status' { if (Test-Path "$S\serve.json") { python "$Scr\gs.py" ping } else { "not running" } }
}
