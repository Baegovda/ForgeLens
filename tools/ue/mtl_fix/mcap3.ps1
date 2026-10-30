$d="$env:USERPROFILE\Desktop\SM2_Work\Scripts\mtl_fix"; $b="/Game/ForgeLens/SM2/Anims_Mannequin/cc_titus_assault__tacticus_"
function M($a){ & "$d\ue.ps1" "$d\mcap3.py" $a 2>&1 | sls "MCAP|Error" | %{$_.Line} }
M @("prep"); M @("start"); Start-Sleep 5; try {
$clips=@(@("run","run_fwd_sound_intercessor_Manny","0,0.23,0.47,0.7"),@("idle","wpn0_blind_idle_intercessor_Manny","0,1.6"),@("attack","attack_melee_axe_light_1_intercessor_Manny","0,0.7,1.35,2.0"))
foreach($c in $clips){ foreach($t in $c[2].Split(",")){ M @("pose","$b$($c[1])",$t); Start-Sleep 1; M @("cap","p_$($c[0])_t$t","120","500") } }
} finally { M @("stop") }; Start-Sleep 3; M @("clear")

