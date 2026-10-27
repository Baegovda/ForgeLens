# mtl1-15 live Chaos cloth run (SIE): spawn, optional WindDirectionalSource (env SM2WINDSRC="x y z strength", cm/s), optional
# interactor wind (env SM2WIND=scale), 4 pen samples, capture, stop + clear. env SM2SET = export set (default chars_cloth).
param([string]$n,[string]$tag="t",[string]$sfx="")
$d="$env:USERPROFILE\Desktop\SM2_Work\Scripts\mtl_fix"; $L="$d\liveS.log"; $S=$(if($env:SM2SET){$env:SM2SET}else{"chars_cloth"}); Remove-Item $L -EA 0
function P($a){ & "$d\ue.ps1" "$d\physlive.py" $a 2>&1 | sls "PHYSV (c|p|sim|pen)|Error:" | %{$_.Line} | Add-Content $L }
function V($a){ & "$d\ue.ps1" "$d\ue_phys_verify.py" $a 2>&1 | sls "PHYSV" | %{$_.Line} | Add-Content $L }
P @("stop",$n,$S); Start-Sleep 4; V @("spawn",$n,$S,$sfx); P @("cams",$n,$S); if($env:SM2WINDSRC){ & "$d\ue.ps1" "$d\clothwind.py" (@("src")+($env:SM2WINDSRC -split " ")) 2>&1 | sls "WIND" | %{$_.Line} | Add-Content $L }; P @("start",$n,$S); Start-Sleep 8; P @("play",$n,$S); Start-Sleep 3; if($env:SM2WIND){ & "$d\ue.ps1" "$d\clothwind.py" @("wind",$n,$S,$env:SM2WIND) 2>&1 | sls "WIND" | %{$_.Line} | Add-Content $L }
foreach($i in 1..4){ P @("sample",$n,$S); Start-Sleep 1 }; P @("cap",$n,$S,$tag)
P @("stop","x",$S); Start-Sleep 4; V @("clear",$n,$S); & "$d\ue.ps1" "$d\physlive.py" @("camclear","x",$S) 2>&1 | Out-Null
& "$d\ue.ps1" "$d\clothwind.py" @("srcclear") 2>&1 | sls "WIND" | %{$_.Line} | Add-Content $L; gc $L | sls "pen|WIND"
