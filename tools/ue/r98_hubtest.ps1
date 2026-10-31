param($tag,[string[]]$hl)
$C="$env:USERPROFILE\ls_push\tools\poe2\ue_poe2_call.ps1"; $T="$env:USERPROFILE\ls_push\tools\ue"
$lk="C:\Users\Revaptor_FX\ue_editor_busy.txt"; $cur=(Get-Content $lk -Raw).Trim(); if($cur -notmatch '^editor-free'){ "LOCK BUSY: $cur"; exit 2 }
"editor-busy sm2left_executor r98b $(Get-Date -f s)" | Set-Content $lk
try{
 if($hl){ & $C "$T\ue_r98_hublights.py" $hl 2>&1 | Select-String 'R98B|Error|Exception' | % Line }
 $o="$env:USERPROFILE\Desktop\SM2_Work\Shots\r98_hub\$tag"; New-Item -ItemType Directory -Force $o|Out-Null
 & $C "$T\ue_r97_levelcap.py" @($o) 2>&1 | Select-String 'Exception|viewpoint' | % Line
 $s=0;$m=@(0,0,0); foreach($y in '000','090','180','270'){ $l=& "$T\r97_imgstats.ps1" "$o\hub_y$y.png"; $l; if($l -match 'mean (\d+),(\d+),(\d+)'){ $m[0]+=[int]$matches[1];$m[1]+=[int]$matches[2];$m[2]+=[int]$matches[3] } }
 "AVG4 {0:0},{1:0},{2:0} (game 22,29,32)" -f ($m[0]/4),($m[1]/4),($m[2]/4)
} finally { "editor-free $(Get-Date -f s)" | Set-Content $lk }
