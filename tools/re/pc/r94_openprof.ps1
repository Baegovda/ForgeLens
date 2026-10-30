param([string]$Exe,[string]$Tag,[int]$Rounds=2)
$d="C:\Users\Revaptor_FX\Desktop\SM2_Work\Shots\r94"
$steps="tpl/dstr_imp_ammo_box_01.tpl/dstr_imp_ammo_box_01.tpl;tpl/wpn_bolt_pistol_01.tpl/wpn_bolt_pistol_01.tpl;tpl/cc_calgar.tpl/cc_calgar.tpl;tpl/cc_sorcerer_exalted.tpl/cc_sorcerer_exalted.tpl;tpl/cc_vortex_beast.tpl/cc_vortex_beast.tpl"
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
$psi.EnvironmentVariables['SM2_MEMSOAK']=$steps; $psi.EnvironmentVariables['SM2_MEMSOAK_DWELL']='4'; $psi.EnvironmentVariables['SM2_MEMSOAK_ROUNDS']="$Rounds"
$psi.EnvironmentVariables['SM2_MEMSOAK_OUT']="$d\${Tag}_soak.tsv"; $psi.EnvironmentVariables['SM2_OPENPROF']="$d\${Tag}_openprof.tsv"
foreach($kv in ($env:R94_ENV -split ';' | ? { $_ })){ $a,$b=$kv -split '=',2; $psi.EnvironmentVariables[$a]=$b }
$p=[Diagnostics.Process]::Start($psi); if(-not $p.WaitForExit(400000)){ $p.CloseMainWindow()|Out-Null; Start-Sleep 5; if(-not $p.HasExited){$p.Kill()}; "TIMEOUT" }
Get-Content "$d\${Tag}_openprof.tsv"; Get-Content "$d\${Tag}_soak.tsv"
