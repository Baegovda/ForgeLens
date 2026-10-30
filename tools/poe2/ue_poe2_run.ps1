# PoE2 M2 E: Unreal import runner. Waits (bounded) for C:\Users\Revaptor_FX\ue_editor_busy.txt == editor-free, refuses when
# NEEDS_ACTION.txt exists or the game runs, takes the lock as "poe2m2 <ts>", runs ue_poe2_import.py through Scripts\uerun.ps1
# (EngineWire into the open UE_VFX_57 editor), always writes editor-free back. Never launches the editor or the game.
param([Parameter(Mandatory)][string[]]$Dirs, [string]$Dest = '/Game/ForgeLens/POE', [int]$WaitMin = 60, [string]$Result = "$env:TEMP\poe2_ue_result.json")
$lock = "$env:USERPROFILE\ue_editor_busy.txt"; $na = "$env:USERPROFILE\window_watch\NEEDS_ACTION.txt"
$py = Join-Path $PSScriptRoot 'ue_poe2_import.py'; $uerun = "$env:USERPROFILE\Desktop\SM2_Work\Scripts\uerun.ps1"
$end = (Get-Date).AddMinutes($WaitMin)
while ((Get-Content $lock -ErrorAction SilentlyContinue) -notmatch '^editor-free') {
  if ((Get-Date) -gt $end) { "LOCK HELD: $(Get-Content $lock) after $WaitMin min"; exit 3 }
  Start-Sleep 30
}
if (Test-Path $na) { "NEEDS_ACTION present: $(Get-Content $na -Raw)"; exit 4 }
if (Get-Process | Where-Object { $_.Name -match 'PathOfExile|Warhammer40000' }) { 'GAME RUNNING'; exit 5 }
# the -unattended editor can have an empty MainWindowTitle: match the command line instead
if (-not (Get-CimInstance Win32_Process -Filter "Name='UnrealEditor.exe'" | Where-Object CommandLine -match 'UE_VFX_57')) { 'UE_VFX_57 editor not running (not launching it)'; exit 6 }
Set-Content $lock "poe2m2 $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
try {
  & $uerun $py (@($Result, $Dest) + $Dirs)
  if (Test-Path $na) { "NEEDS_ACTION appeared during import: $(Get-Content $na -Raw)" }
  if (Test-Path $Result) { Get-Content $Result -Raw } else { 'no result json' }
} finally { Set-Content $lock 'editor-free' }
