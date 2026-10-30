# Start watcher hidden (single instance) and register a user-level logon task. Usage: start.ps1 [-ProcNames a,b]
param([string]$ProcNames="UnrealEditor,CrashReportClient,CrashReportClientEditor",[switch]$NoTask)
$root="$env:USERPROFILE\window_watch"; $bin=Join-Path $root bin; New-Item -ItemType Directory -Force $bin|Out-Null
# Deploy: repo copy is the source of truth; the watcher always runs from the fixed bin copy so push.ps1 rewriting ls_push cannot break it.
if((Resolve-Path $PSScriptRoot).Path.TrimEnd('\') -ne (Resolve-Path $bin).Path.TrimEnd('\')){
 $running=Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | ?{ $_.CommandLine -like '*watch*watcher.ps1*' }
 if($running){ "watcher running (pid=$($running.ProcessId -join ',')); run stop.ps1 first to redeploy"; return }
 Copy-Item (Join-Path $PSScriptRoot '*.ps1'),(Join-Path $PSScriptRoot 'README.md') $bin -Force; "deployed to $bin" }
$w=Join-Path $bin watcher.ps1
$arg="-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$w`" -ProcNames `"$ProcNames`""
if(-not $NoTask){  # user-level autostart: Startup folder shortcut (Scheduled Task registration did not persist from the agent shell)
 $lnk=Join-Path ([Environment]::GetFolderPath('Startup')) 'ForgeLensWindowWatch.lnk'
 $sh=New-Object -ComObject WScript.Shell; $l=$sh.CreateShortcut($lnk); $l.TargetPath="$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"; $l.Arguments=$arg; $l.WindowStyle=7; $l.Save() }
$run=Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | ?{ $_.CommandLine -like '*watch*watcher.ps1*' }
if($run){ "already running pid=$($run.ProcessId -join ',')"; return }
$p=Start-Process "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $arg -WindowStyle Hidden -PassThru; "started pid=$($p.Id)"

