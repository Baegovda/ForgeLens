# mtl1-16 solver variants on one ChaosCloth mesh (chars_cloth set): pen16.ps1 <asset> ; writes Captures\mtl_fix\pen16_<asset>.txt
param([string]$n="cc_sorcerer_exalted")
$W="$env:USERPROFILE\Desktop\SM2_Work"; $d="$W\Scripts\mtl_fix"; $out="$W\Captures\mtl_fix\pen16_$n.txt"; Remove-Item $out -ea 0
$mesh="/Game/SM2_Work/Tests/chars_cloth/$n/${n}_ChaosCloth"
$vars=[ordered]@{ base="SubdivisionCount=1;IterationCount=1;MaxIterationCount=10"; sub2="SubdivisionCount=2;IterationCount=1"; it4="SubdivisionCount=1;IterationCount=4"; sub2it4="SubdivisionCount=2;IterationCount=4"; sub4it4="SubdivisionCount=4;IterationCount=4"; sub2t3="SubdivisionCount=2;IterationCount=1;CollisionThickness=3" }
if($env:PEN16VARS){ $sel=$env:PEN16VARS -split ","; $v2=[ordered]@{}; foreach($k in $sel){ $v2[$k]=$vars[$k] }; $vars=$v2 }
$orig = & "$d\ue.ps1" "$d\cfgtune.py" @($mesh,"") 2>&1 | Out-String
foreach($k in $vars.Keys){
  "$k cfg: " + ((& "$d\ue.ps1" "$d\cfgtune.py" @($mesh,$vars[$k]) 2>&1 | sls "CFGT" | % Line) -replace "\|.*","") | Add-Content $out
  Remove-Item env:SM2WIND -ea 0; & "$d\liveC.ps1" $n "p16$k" "_ChaosCloth" | Out-Null
  & "$d\ue.ps1" "$d\pie2.py" | Out-Null; sleep 4
  "$n ${k}: " + ((gc "$d\liveC.log" | sls 'pen' | % { $_.Line -replace '.*particles=(\d+) inside>1.0cm=(\d+).*worstFreeCm=([\d.]+).*','$1:$2/$3' }) -join ' ') | Add-Content $out }
& "$d\ue.ps1" "$d\cfgtune.py" @($mesh,$vars["base"]) | Out-Null
"PIE: " + (& "$d\ue.ps1" "$d\pie2.py" 2>&1 | select -Last 1) | Add-Content $out; "DONE" | Add-Content $out
