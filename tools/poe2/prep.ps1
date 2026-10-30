# PoE2 M1 "prepared folder" builder (read-only on the game). Usage:
#   tools\poe2\prep.ps1 -Out <folder> -Patterns 'art/models/monsters/basicskeleton/basicskeleton1*','art/models/monsters/basicskeleton/rig.ast','art/textures/monsters/katarinaskeleton/*'
# Writes <Out>\raw (verbatim files), <Out>\json (poe_data_tools translate JSON), and <ast>.anim.bin (decompressed
# animation bundle of every .ast v8+). Tools (not committed, see docs/POE2_M1.md "Build"): $Bin\poe_data_tools.exe, $Bin\ast_bundle.exe.
param(
  [Parameter(Mandatory)][string]$Out,
  [Parameter(Mandatory)][string[]]$Patterns,
  [string]$Ggpk = 'D:\Daum Games\Path of Exile2\Content.ggpk',
  [string]$Bin = "$env:USERPROFILE\Desktop\SM2_Work\PoE2\tools"
)
$ErrorActionPreference = 'Stop'
$pdt = Join-Path $Bin 'poe_data_tools.exe'; $ab = Join-Path $Bin 'ast_bundle.exe'
& $pdt -p 2 --ggpk $Ggpk extract (Join-Path $Out 'raw') @Patterns 2>&1 | Out-Null
& $pdt -p 2 --ggpk $Ggpk translate (Join-Path $Out 'json') @Patterns 2>&1 | Select-String 'ERROR' | ForEach-Object { $_.Line }
Get-ChildItem -Recurse (Join-Path $Out 'raw') -Filter *.ast | ForEach-Object {
  $bin = $_.FullName + '.anim.bin'
  if (-not (Test-Path $bin)) { & $ab $_.FullName $bin 2>&1 | ForEach-Object { "$($_.ToString()) $($_.FullName)" } }
}
"prepared $Out"
