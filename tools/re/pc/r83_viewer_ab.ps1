# R83: real-window A/B of one viewer build on one model, window pinned (SM2_WINRECT, same DPI + size every run).
# SM2_UETEST shots + log, then private MB, GPU dedicated MB (perf counter), nvidia-smi per-process MB, idle CPU.
# viewport_r79.json is copied aside before and copied back after (the test changes view settings / layout).
param([string]$Exe,[string]$Select="tpl/cc_calgar.tpl/cc_calgar.tpl",[string]$Name="calgar",[string]$Out,[string]$Env="SM2_UETEST=1",[string]$Done="r83 cpu view modes done|winshot failed",[string]$Rect="0,0,1600,900",[int]$Timeout=500)
New-Item -ItemType Directory -Force $Out | Out-Null
$vp="$env:LOCALAPPDATA\ForgeLens\viewport_r79.json"; $keep="$Out\viewport_r79.before.json"; if(Test-Path $vp){ Copy-Item $vp $keep -Force }
$shot="$Out\win_$Name.png"; Get-ChildItem "$Out\win_$Name*" -EA 0 | Remove-Item -EA 0
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
$psi.EnvironmentVariables["SM2_SELECT"]=$Select; $psi.EnvironmentVariables["SM2_WINSHOT"]=$shot; $psi.EnvironmentVariables["SM2_WINRECT"]=$Rect
foreach($kv in ($Env -split ';' | ? { $_ })){ $k,$v=$kv -split '=',2; $psi.EnvironmentVariables[$k]=$v }
$t=[Diagnostics.Stopwatch]::StartNew(); $p=[Diagnostics.Process]::Start($psi); try{$p.PriorityClass='High'}catch{}
while(-not ((Get-Content "$shot.txt" -Raw -EA 0) -match $Done) -and $t.Elapsed.TotalSeconds -lt $Timeout){ Start-Sleep 1; if($p.HasExited){break} }
Start-Sleep 5; $p.Refresh()
$gpu=(Get-Counter "\GPU Process Memory(pid_$($p.Id)*)\Dedicated Usage" -EA 0).CounterSamples | Measure-Object CookedValue -Sum
$smi=(& nvidia-smi --query-compute-apps=pid,used_memory --format=csv,noheader,nounits 2>$null) + (& nvidia-smi --query-graphics-apps=pid,used_memory --format=csv,noheader,nounits 2>$null) | ? { $_ -match "^\s*$($p.Id)\s*," } | select -first 1
$c0=$p.TotalProcessorTime.TotalSeconds; Start-Sleep 20; $p.Refresh(); $cpu=($p.TotalProcessorTime.TotalSeconds-$c0)/20*100
"r83ab exe $Exe private_mb $([int]($p.PrivateMemorySize64/1MB)) gpu_dedicated_mb $([int]($gpu.Sum/1MB)) nvsmi '$smi' idle_cpu_pct_one_core $('{0:0.0}' -f $cpu) wall $([math]::Round($t.Elapsed.TotalSeconds,1))s title $($p.MainWindowTitle)" | Add-Content "$shot.txt"
if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null; Start-Sleep 4; if(-not $p.HasExited){$p.Kill()} }
if(Test-Path $keep){ Copy-Item $keep $vp -Force }
Get-Content "$shot.txt"
