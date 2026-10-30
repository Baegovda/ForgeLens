# Start watcher hidden (single instance) and register a user-level logon task. Usage: start.ps1 [-ProcNames a,b]
param([string]$ProcNames="UnrealEditor,CrashReportClient,CrashReportClientEditor",[switch]$NoTask)
$w=Join-Path $PSScriptRoot watcher.ps1; $root="$env:USERPROFILE\window_watch"; New-Item -ItemType Directory -Force $root|Out-Null
$arg="-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$w`" -ProcNames `"$ProcNames`""
if(-not $NoTask){  # user-level autostart: Startup folder shortcut (Scheduled Task registration did not persist from the agent shell)
 $lnk=Join-Path ([Environment]::GetFolderPath('Startup')) 'ForgeLensWindowWatch.lnk'
 $sh=New-Object -ComObject WScript.Shell; $l=$sh.CreateShortcut($lnk); $l.TargetPath="$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"; $l.Arguments=$arg; $l.WindowStyle=7; $l.Save() }
$run=Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | ?{ $_.CommandLine -like '*watch\watcher.ps1*' }
if($run){ "already running pid=$($run.ProcessId -join ',')"; return }
$p=Start-Process "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $arg -WindowStyle Hidden -PassThru; "started pid=$($p.Id)"
