param([string]$Tag='base',[string]$Dir="$env:USERPROFILE\Desktop\SM2_Work\Shots\r63")
# R63: one Simulate pass of the cloth sorcerer A/B map: rest -> shake 6 s (90 cm, 0.7 Hz) -> hold -> stop. Shots + plugin metrics.
$s=$PSScriptRoot; $o="$Dir\$Tag"; New-Item -ItemType Directory -Force $o | Out-Null
function U($a){ & "$s\uerun.ps1" "$s\ue_r63_cloth.py" $a 2>&1 | Select-String "^R63|Error:|Exception:" | % Line }
$log="$o\metrics.txt"; Remove-Item $log -EA 0
U "cap,$o,0_editor"; U sim; Start-Sleep 3
U "cap,$o,a_rest"; U "measure,a_rest" | Add-Content $log
U "osc,6,90"; Start-Sleep 1
U "cap,$o,b_motion1"; U "measure,b_motion1" | Add-Content $log; Start-Sleep 1
U "cap,$o,b_motion2"; U "measure,b_motion2" | Add-Content $log; Start-Sleep 8
U "cap,$o,c_settled"; U "measure,c_settled" | Add-Content $log
U stop; "R63 pass $Tag done"
