param([string]$n,[string]$sfx="_ChaosCloth")
$d="$env:USERPROFILE\Desktop\SM2_Work\Scripts\mtl_fix"
function P($a){ & "$d\ue.ps1" "$d\physlive.py" $a 2>&1 | sls "PHYSV (sim|play)|Error" | % Line }
P @("stop",$n,"chars_cloth"); sleep 4; & "$d\ue.ps1" "$d\ue_phys_verify.py" @("spawn",$n,"chars_cloth",$sfx) | Out-Null; P @("start",$n,"chars_cloth"); sleep 8; P @("play",$n,"chars_cloth"); sleep 3
& "$d\ue.ps1" "$d\clothwind.py" @("probe") 2>&1 | sls PROBE | % Line
P @("stop","x","chars_cloth"); sleep 4; & "$d\ue.ps1" "$d\ue_phys_verify.py" @("clear",$n,"chars_cloth") | Out-Null; & "$d\ue.ps1" "$d\pie2.py" 2>&1 | sls 'PIE now'
