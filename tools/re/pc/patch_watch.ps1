# Daily patch check (silent): exe_check.py on the installed game exe; mismatch -> alerts.txt line [patch] + output. -v = always print.
param([switch]$v)
. "$PSScriptRoot\alerts.ps1"
$out = (python "$PSScriptRoot\..\exe_check.py" 2>&1 | Out-String).Trim(); $rc = $LASTEXITCODE
if ($rc -eq 0) { Set-Alert patch '' } else { Set-Alert patch ("game exe changed or missing: " + ((($out -split "`n") | ? { $_ -match 'sha256|MISMATCH|not found' }) -join ' ')) }
Add-Content "$env:USERPROFILE\Desktop\SM2_Work\Tools\re_env\patch_watch.log" "$(Get-Date -Format 'yyyy-MM-dd HH:mm') KST rc=$rc $($out -replace "`r?`n",' | ')"
if ($rc -ne 0 -or $v) { $out }
