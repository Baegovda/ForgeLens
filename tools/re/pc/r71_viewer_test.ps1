# R71: open a model in the real window (no activation), winshot collapsed + expanded Collision/Helpers group, title.
param([string]$Select = "tpl/cc_calgar.tpl/cc_calgar.tpl", [string]$Name = "calgar",
      [string]$Exe = "$env:USERPROFILE\Desktop\SM2_Work\Bin\ForgeLens_r71\ForgeLens.exe",
      [string]$Out = "$env:USERPROFILE\Desktop\SM2_Work\Shots\r71")
New-Item -ItemType Directory -Force $Out | Out-Null
$shot = "$Out\win_$Name.png"; Remove-Item "$Out\win_$Name*" -EA 0
$psi = New-Object Diagnostics.ProcessStartInfo $Exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables["SM2_SELECT"] = $Select
$psi.EnvironmentVariables["SM2_WINSHOT"] = $shot
$psi.EnvironmentVariables["SM2_WINSHOT_CPU"] = "1"
$psi.EnvironmentVariables["SM2_MESHGROUP_SHOT"] = "1"
$t = [Diagnostics.Stopwatch]::StartNew()
$p = [Diagnostics.Process]::Start($psi); try { $p.PriorityClass = 'High' } catch {}
while (-not ((Get-Content "$shot.txt" -Raw -EA 0) -match "group_select|group container|winshot failed") -and $t.Elapsed.TotalSeconds -lt 400) { Start-Sleep 1; if ($p.HasExited) { break } }
Start-Sleep 2
$p.Refresh(); "title: $($p.MainWindowTitle)" | Add-Content "$shot.txt"
"wall $([math]::Round($t.Elapsed.TotalSeconds,1))s" | Add-Content "$shot.txt"
if (-not $p.HasExited) { $p.Kill() }
Get-Content "$shot.txt"
