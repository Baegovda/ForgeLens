param([string]$Ev='0',[switch]$NoLight)
# R64: relight the r54 scratch level and capture overview + 2 close-ups into Shots\r64\level (EngineWire via uerun.ps1).
$s=$PSScriptRoot; $o="$env:USERPROFILE\Desktop\SM2_Work\Shots\r64\level"; New-Item -ItemType Directory -Force $o | Out-Null
function U($a){ & "$s\uerun.ps1" "$s\ue_r64_level.py" $a 2>&1 | Select-String "R64|Error|Exception" | % Line }
if(-not $NoLight){ U light; Start-Sleep 5 }
U @("capture",$o,$Ev); Start-Sleep 3; U export
