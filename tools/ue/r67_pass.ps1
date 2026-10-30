param([string]$Subj,[string]$Tag='base',[string]$Dir="$env:USERPROFILE\Desktop\SM2_Work\Shots\r67")
$s=$PSScriptRoot; $o="$Dir\$Subj\$Tag"; New-Item -ItemType Directory -Force $o | Out-Null
$ue="$env:USERPROFILE\Desktop\SM2_Work\Scripts\uerun.ps1"
function U($a){ & $ue "$s\ue_r67_phys.py" $a 2>&1 | Select-String "^R67|Error:|Exception:" | % Line }
$log="$o\metrics.txt"; Remove-Item $log -EA 0
U "cap,$Subj,$o,0_editor"; U sim; Start-Sleep 3
U "cap,$Subj,$o,a_rest"; U "measure,$Subj,a_rest" | Add-Content $log
U "osc,6,90"; Start-Sleep 1
U "cap,$Subj,$o,b_motion1"; U "measure,$Subj,b_motion1" | Add-Content $log; Start-Sleep 1
U "cap,$Subj,$o,b_motion2"; U "measure,$Subj,b_motion2" | Add-Content $log; Start-Sleep 8
U "cap,$Subj,$o,c_settled"; U "measure,$Subj,c_settled" | Add-Content $log
U stop; gc $log; "R67 pass $Subj $Tag done"
