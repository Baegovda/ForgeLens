param([string]$Subj,[string]$Clip,[string]$Tag='before',[string]$Dir="$env:USERPROFILE\Desktop\SM2_Work\Shots\r90_cloth")
# R90: prep cloth mesh with the INSTALLED plugin, play a fast clip, Simulate, capture + measure at ~1.5/3/5 s.
$s=$PSScriptRoot; $o="$Dir\$Subj`_$Clip\$Tag"; New-Item -ItemType Directory -Force $o | Out-Null
$ue="$env:USERPROFILE\Desktop\SM2_Work\Scripts\uerun.ps1"
function U($a){ & $ue "$s\ue_r90_cloth.py" $a 2>&1 | Select-String "^R90|Error:|Exception:|Traceback" | % Line }
$log="$o\metrics.txt"; Remove-Item $log -EA 0
U "prep,$Subj,$Tag" | Add-Content $log; U "scene,$Subj,$Tag,$Clip" | Add-Content $log; U "actor,$Subj,$Tag,$Clip" | Add-Content $log; U "cams" | Add-Content $log
U "cap,$o,0_editor"; U sim; Start-Sleep 1
foreach($t in 't1','t2','t3'){ U "cap,$o,$t"; U "measure,$t" | Add-Content $log; Start-Sleep 1 }
U stop; gc $log; "R90 pass $Subj $Clip $Tag done"

