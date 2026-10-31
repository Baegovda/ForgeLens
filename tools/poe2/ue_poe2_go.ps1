# PoE2 M3: generic locked runner for one EngineWire script call (same checks as ue_poe2_run.ps1): waits for editor-free,
# refuses on NEEDS_ACTION / game running / editor not running, takes the lock with a named owner, always releases it.
param([Parameter(Mandatory)][string]$Py, [string[]]$Argv = @(), [string]$Owner = 'poe2m3', [int]$WaitMin = 30)
$lock = "$env:USERPROFILE\ue_editor_busy.txt"; $na = "$env:USERPROFILE\window_watch\NEEDS_ACTION.txt"
$uerun = Join-Path $PSScriptRoot 'ue_poe2_call.ps1'
$end = (Get-Date).AddMinutes($WaitMin)
while ((Get-Content $lock -ErrorAction SilentlyContinue) -notmatch '^editor-free') {
  if ((Get-Date) -gt $end) { "LOCK HELD: $(Get-Content $lock) after $WaitMin min"; exit 3 }
  Start-Sleep 20
}
if ((Test-Path $na) -and (Get-Content $na -Raw) -match '\S') { "NEEDS_ACTION present: $(Get-Content $na -Raw)"; exit 4 }
if (Get-Process | Where-Object { $_.Name -match 'PathOfExile|Warhammer40000' }) { 'GAME RUNNING'; exit 5 }
if (-not (Get-CimInstance Win32_Process -Filter "Name='UnrealEditor.exe'" | Where-Object CommandLine -match 'UE_VFX_57')) { 'UE_VFX_57 editor not running (not launching it)'; exit 6 }
Set-Content $lock "$Owner $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')"
try { & $uerun $Py $Argv; $rc = $LASTEXITCODE } finally { Set-Content $lock "editor-free $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')" }
exit $rc
