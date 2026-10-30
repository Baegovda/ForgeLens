# mtl1-40 UE frame sequences: render + export every *.vfxframe.json of a directory (one import beforehand:
#   uerun.ps1 <abs>\ue_vfx_frame.py import <dir> /Game/ForgeLens/SM2/VfxUe [fresh]).
# usage: vfx_sequence.ps1 -Dir <frames dir> -Out <png dir> [-Uerun <uerun.ps1>] [-Script <ue_vfx_frame.py>]
#        [-Res 512] [-Backdrop -1] [-Escale 1] [-Ground 0.06] [-Filter *]
# PNGs are named <json basename>.png (e.g. part_mg_frag_grenade_expl_150ms.png); tools/vfx/sequence_sheet.py builds the
# contact sheet / GIF / CPU-reference comparison on the box.
param([string]$Dir, [string]$Out, [string]$Uerun = "$PSScriptRoot\uerun.ps1", [string]$Script = "$PSScriptRoot\ue_vfx_frame.py",
      [string]$Res = '512', [string]$Backdrop = '-1', [string]$Escale = '1', [string]$Ground = '0.06', [string]$Filter = '*',
      [string]$Dest = '/Game/ForgeLens/SM2/VfxUe')
New-Item -ItemType Directory -Force $Out | Out-Null
$fail = 0
foreach ($j in Get-ChildItem $Dir -Filter "$Filter.vfxframe.json" | Sort-Object Name) {
  $png = Join-Path $Out ($j.Name -replace '\.vfxframe\.json$', '.png')
  & $Uerun $Script @('render', $j.FullName, $Dest, $Res, $Backdrop, $Escale, $Ground) | Select-String 'captured|MISSING|Error|Traceback' | ForEach-Object { $_.Line }
  & $Uerun $Script @('export', $png) | Select-String 'wrote|Error|Traceback' | ForEach-Object { $_.Line }
  if (-not (Test-Path $png)) { Write-Host "FAILED $($j.Name)"; $fail++ }
}
exit $fail
