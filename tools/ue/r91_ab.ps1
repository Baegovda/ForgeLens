# R91 A/B driver: variants x subjects x 3 runs with r90_pass.ps1; closes the Message Log after every pass (owner t222u).
param([string]$Out="$env:USERPROFILE\Desktop\SM2_Work\Shots\r91_cloth",[int]$Runs=3,[string[]]$Only)
$s=$PSScriptRoot; $ue="$env:USERPROFILE\Desktop\SM2_Work\Scripts\uerun.ps1"
$VAR=[ordered]@{legacy=@('0.25','0');cur=@('0.9','0.5');f100=@('1.0','0.5')}
$JOBS=@('sorcerer:death','titus:run','titus:melee','calgar:run','vortex:melee','tech_priest:death','chaplain:turn','commissar:death','commissar:run','commissar:melee')
if($Only){$JOBS=$Only}
foreach($r in 1..$Runs){ foreach($v in $VAR.Keys){ if($v -eq 'f100' -and $Only -eq $null){ $jj=$JOBS | ? { $_ -like 'titus:*' -or $_ -like 'sorcerer:*' } } else { $jj=$JOBS }
  & $ue "$s\r91_cvar.py" -argv $VAR[$v][0],$VAR[$v][1] 2>&1 | Select-String R91CVAR | % Line
  foreach($job in $jj){ $a,$c=$job.Split(':'); $t0=Get-Date
    & "$s\r90_pass.ps1" -Subj $a -Clip $c -Tag $v -Dir "$Out\run$r" *>&1 | Out-Null; & "$s\close_msglog.ps1" | Out-Null
    $m="$Out\run$r\$a`_$c\$v\metrics.txt"; $pen=0;$sk=0; Get-Content $m -EA 0 | Select-String '^R90M' | % { $o=($_.Line.Substring(5)|ConvertFrom-Json); $pen+=$o.measure_cloth_penetration[0]; $sk+=$o.measure_cloth_skin_penetration[0] }
    $rs=(Get-Content $m -EA 0 | Select-String 'minRadiusScale=([\d.]+)' -AllMatches | % { $_.Matches.Groups[1].Value }) -join ','
    "$r|$v|$a|$c|pen=$pen|skin=$sk|rs=$rs|$([int]((Get-Date)-$t0).TotalSeconds)s" | Tee-Object -Append "$Out\r91_summary.txt" } } }
"R91 ALLDONE"
