param([string]$Subj,[string]$Clip,[string]$Tag='cur',[string]$Dir="$env:USERPROFILE\Desktop\SM2_Work\Shots\r93_cloth",[string]$Floor='0.9',[string]$Deep='0.5',[string]$Shallow='1',[string]$Frames='30;60;90',[string]$Step='0.0333333')
# R93: like r90_pass but sampling at fixed frame numbers with a frame-driven clip time (ue_r93_fixstep.py).
$s=$PSScriptRoot; $o="$Dir\$Subj`_$Clip\$Tag"; New-Item -ItemType Directory -Force $o | Out-Null
$ue="$env:USERPROFILE\Desktop\SM2_Work\Scripts\uerun.ps1"
function U($f,$a){ & $ue "$s\$f" $a 2>&1 | Select-String "^R9|Error:|Exception:|Traceback" | % Line }
U r93_cvar.py "$Floor,$Deep,$Shallow" | Out-Null
$log="$o\metrics.txt"; Remove-Item $log -EA 0
foreach($m in "prep,$Subj,$Tag","scene,$Subj,$Tag,$Clip","actor,$Subj,$Tag,$Clip","cams"){ U ue_r90_cloth.py $m | Add-Content $log }
U ue_r93_fixstep.py "start,$o,$Step,$Frames" | Add-Content $log
$d="$o\fix_done.json"; $t0=Get-Date; while(-not (Test-Path $d) -and ((Get-Date)-$t0).TotalSeconds -lt 90){ Start-Sleep -Milliseconds 500 }
Start-Sleep 1; & "$s\close_msglog.ps1" | Out-Null
if(Test-Path $d){ $j=gc $d -Raw | ConvertFrom-Json; if($j.err){"R93ERR $($j.err)"}; $pen=0;$sk=0; foreach($r in $j.res){$pen+=$r.measure_cloth_penetration[0];$sk+=$r.measure_cloth_skin_penetration[0]}
 $ct=($j.res|%{"$($_.frame):$($_.clip_t)"}) -join ' '; "R93SUM $Subj $Clip $Tag pen=$pen skin=$sk clip=[$ct] dt=$($j.dt_min)-$($j.dt_max)" | Tee-Object -Append $log } else { "R93 TIMEOUT $Subj $Clip" }

