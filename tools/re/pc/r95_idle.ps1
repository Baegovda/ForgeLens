# R94 idle: start the viewer (last selection restored as the owner left it), wait, then sample 30 s of idle.
param([string]$Exe,[string]$Tag,[int]$Warm=45,[string]$Env="")
$d="C:\Users\Revaptor_FX\Desktop\SM2_Work\Shots\r95"
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
foreach($kv in ($Env -split ';' | ? { $_ })){ $a,$b=$kv -split '=',2; $psi.EnvironmentVariables[$a]=$b }
$p=[Diagnostics.Process]::Start($psi); Start-Sleep $Warm; $p.Refresh(); $c0=$p.TotalProcessorTime.TotalMilliseconds; $t0=Get-Date
Start-Sleep 30; $p.Refresh(); $cpu=($p.TotalProcessorTime.TotalMilliseconds-$c0)/((Get-Date)-$t0).TotalMilliseconds*100
$line="r95idle $Tag ws_mb $([int]($p.WorkingSet64/1MB)) private_mb $([int]($p.PrivateMemorySize64/1MB)) threads $($p.Threads.Count) idle_cpu_pct_of_1core $([math]::Round($cpu,2)) title '$($p.MainWindowTitle)'"
$line | Add-Content "$d\idle.txt"; $line
$p.CloseMainWindow()|Out-Null; Start-Sleep 5; if(-not $p.HasExited){$p.Kill()}

