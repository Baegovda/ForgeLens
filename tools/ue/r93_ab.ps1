# R93 A/B driver (deterministic r93_pass): variants x jobs x runs; summary line per pass.
param([string]$Out="$env:USERPROFILE\Desktop\SM2_Work\Shots\r93_cloth\ab",[int]$Runs=1,[string[]]$Var=@('cur=0.9,0.5,1','sf02=0.9,0.5,0.02'),[string[]]$Only)
$JOBS=@('sorcerer:death','titus:run','titus:melee','calgar:run','vortex:melee','tech_priest:death','chaplain:turn','commissar:death','commissar:run','commissar:melee'); if($Only){$JOBS=$Only}
foreach($r in 1..$Runs){ foreach($v in $Var){ $n,$cv=$v.Split('='); $f,$d,$sh=$cv.Split(',')
 foreach($job in $JOBS){ $a,$c=$job.Split(':'); $line=& "$PSScriptRoot\r93_pass.ps1" -Subj $a -Clip $c -Tag $n -Dir "$Out\run$r" -Floor $f -Deep $d -Shallow $sh 2>&1 | Select-String 'R93SUM|R93ERR|TIMEOUT' | select -last 1
  $m="$Out\run$r\$a`_$c\$n\metrics.txt"; $rs=(gc $m -EA 0 | Select-String 'minRadiusScale=([\d.]+)' -AllMatches | % { $_.Matches.Groups[1].Value }) -join ','; $fs=(gc $m -EA 0 | Select-String 'floorSkipped=(\d+)' -AllMatches | % { $_.Matches.Groups[1].Value }) -join ','
  "$r|$n|$line|rs=$rs|fskip=$fs" | Tee-Object -Append "$Out\r93_summary.txt" } } }
"R93 ALLDONE"
