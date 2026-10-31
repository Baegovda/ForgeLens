param($tag,[string[]]$bloomArgs,$f=60)
$C="$env:USERPROFILE\ls_push\tools\poe2\ue_poe2_call.ps1"; $T="$env:USERPROFILE\ls_push\tools\ue"
$lk="C:\Users\Revaptor_FX\ue_editor_busy.txt"; $cur=(Get-Content $lk -Raw).Trim(); if($cur -notmatch '^editor-free' -and $cur -notmatch 'sm2left_executor'){ "LOCK BUSY: $cur"; exit 2 }
"editor-busy sm2left_executor r98 $(Get-Date -f s)" | Set-Content $lk
try{
 if($bloomArgs){ & $C "$T\ue_r98_orbbloom.py" $bloomArgs 2>&1 | Select-String 'R98A|Error|Exception' | % Line }
 $o="$env:USERPROFILE\Desktop\SM2_Work\Shots\r98_orb\$tag"; New-Item -ItemType Directory -Force $o|Out-Null; Remove-Item "$o\*.png" -EA 0
 & $C "C:\Users\Revaptor_FX\fl_r95fid\tools\ue\ue_r97_firemrq.py" @("$o,$f,$($f+1),0,,/Game/ForgeLens/SM2/VFX/vortex_orb/LS_orb_cam") 2>&1 | Select-String 'Exception' | % Line
 for($i=0;$i -lt 80 -and -not (Test-Path "$o\fire_00$f.png");$i++){Start-Sleep 3}; Start-Sleep 5
 & "$T\r98_orbcore.ps1" "$o\fire_00$f.png"
} finally { "editor-free $(Get-Date -f s)" | Set-Content $lk }
