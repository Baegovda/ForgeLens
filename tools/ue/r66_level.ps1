param([string]$Tag='b',[string]$Ev='1.5',[string]$Fog='',[switch]$NoSky)
# R66L: sky + capture overview/close1-3 + viewer-matched vpersp/vclose of the r54 scratch level into Shots\r66_level\<Tag> (EngineWire via uerun.ps1), zip for the box.
$s=$PSScriptRoot; $d="$env:USERPROFILE\Desktop\SM2_Work\Exports\level\ue_r66_hub"; $o="$env:USERPROFILE\Desktop\SM2_Work\Shots\r66_level\$Tag"; New-Item -ItemType Directory -Force $o | Out-Null
function U{ & "$s\uerun.ps1" "$s\ue_r66_level.py" $args 2>&1 | Select-String "R66|Error|Exception|Traceback|line \d" | % Line }
if(-not $NoSky){ if($Fog){ U sky $d $Fog } else { U sky $d } }
U capture $d $o $Ev; Start-Sleep 6; U export; U capture $d $o $Ev; Start-Sleep 4; U export
Compress-Archive -Force "$o\*.png" "$o.zip"
