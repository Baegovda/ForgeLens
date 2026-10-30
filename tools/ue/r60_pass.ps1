param([string]$Tag='phys',[string]$Shots='',[string]$Dir="$env:USERPROFILE\Desktop\SM2_Work\Shots\r60")
# R60: one Simulate pass (start -> oscillate 8 s + 3 s hold -> stop); screenshots only when -Shots is a name prefix
$s=$PSScriptRoot; function U($a){ & "$s\uerun.ps1" "$s\ue_pie_phys.py" $a 2>&1 | Select-String "^R60|Error:|Exception:" | % Line }
$t0=Get-Date; U sim; Start-Sleep 2; 
if($Shots){ U "cap,$Dir\${Shots}_1_sim_start.png" }
U "osc,8,80,1.0,$Tag"; Start-Sleep 3
if($Shots){ U "cap,$Dir\${Shots}_2_motion_a.png"; Start-Sleep 2; U "cap,$Dir\${Shots}_2_motion_b.png" }
$f="K:\Unreal Projects\UE_VFX_57\Saved\r60_osc_$Tag.json"; for($i=0;$i -lt 120 -and -not (Test-Path $f);$i++){Start-Sleep 1}
if($Shots){ U "cap,$Dir\${Shots}_3_settle.png" }
U stop; "R60 pass $Tag sim_s=" + [int]((Get-Date)-$t0).TotalSeconds

