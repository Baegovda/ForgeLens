# R82: real-window A/B of one viewer build on one model: SM2_UETEST shots + log, then private MB, GPU dedicated MB (perf counter), idle CPU.
param([string]$Exe,[string]$Select="tpl/cc_calgar.tpl/cc_calgar.tpl",[string]$Name="calgar",[string]$Out,[string]$Env="SM2_UETEST=1",[string]$Done="settings file|winshot failed")
New-Item -ItemType Directory -Force $Out | Out-Null
$shot="$Out\win_$Name.png"; Remove-Item "$Out\win_$Name*" -EA 0
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
$psi.EnvironmentVariables["SM2_SELECT"]=$Select; $psi.EnvironmentVariables["SM2_WINSHOT"]=$shot
foreach($kv in ($Env -split ';' | ? { $_ })){ $k,$v=$kv -split '=',2; $psi.EnvironmentVariables[$k]=$v }
$t=[Diagnostics.Stopwatch]::StartNew(); $p=[Diagnostics.Process]::Start($psi); try{$p.PriorityClass='High'}catch{}
while(-not ((Get-Content "$shot.txt" -Raw -EA 0) -match $Done) -and $t.Elapsed.TotalSeconds -lt 400){ Start-Sleep 1; if($p.HasExited){break} }
Start-Sleep 5; $p.Refresh()
$gpu=(Get-Counter "\GPU Process Memory(pid_$($p.Id)*)\Dedicated Usage" -EA 0).CounterSamples | Measure-Object CookedValue -Sum
$c0=$p.TotalProcessorTime.TotalSeconds; Start-Sleep 20; $p.Refresh(); $cpu=($p.TotalProcessorTime.TotalSeconds-$c0)/20*100
"r82ab exe $Exe private_mb $([int]($p.PrivateMemorySize64/1MB)) gpu_dedicated_mb $([int]($gpu.Sum/1MB)) idle_cpu_pct_one_core $('{0:0.0}' -f $cpu) wall $([math]::Round($t.Elapsed.TotalSeconds,1))s title $($p.MainWindowTitle)" | Add-Content "$shot.txt"
if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null; Start-Sleep 3; if(-not $p.HasExited){$p.Kill()} }
Get-Content "$shot.txt"