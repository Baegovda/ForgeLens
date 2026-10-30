# R81: idle cost of a viewer build (no model opened): minimized start, warm-up, then CPU % of one core, WS/private MB, threads.
param([string]$Exe,[int]$Warm=40,[int]$Sample=30)
$p=Start-Process $Exe -PassThru -WindowStyle Minimized
Start-Sleep $Warm; $p.Refresh()
$c0=$p.TotalProcessorTime.TotalSeconds; $t0=Get-Date
Start-Sleep $Sample; $p.Refresh()
$cpu=($p.TotalProcessorTime.TotalSeconds-$c0)/((Get-Date)-$t0).TotalSeconds*100
"{0} ws_mb {1} private_mb {2} threads {3} cpu_pct_of_one_core {4:0.0}" -f (Split-Path $Exe -Leaf), [int]($p.WorkingSet64/1MB), [int]($p.PrivateMemorySize64/1MB), $p.Threads.Count, $cpu
$p.CloseMainWindow()|Out-Null; Start-Sleep 3; if(!$p.HasExited){$p.Kill()}

