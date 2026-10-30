# R74: scripted memory soak of the real viewer window (SM2_MEMSOAK hook). One row per step: open time, peak WS/private.
# -LevelCache n = SM2_LEVEL_MEM_CACHE (99 reproduces the pre-R74 keep-every-level behaviour).
param([string]$Exe = "$env:USERPROFILE\Desktop\SM2_Work\Build\r74\out\ForgeLens.exe",
      [string]$Out = "$env:USERPROFILE\Desktop\SM2_Work\Shots\r74\soak.tsv",
      [string]$LevelCache = "", [int]$Dwell = 15, [int]$Rounds = 1,
      [string]$Steps = "tpl/cc_calgar.tpl/cc_calgar.tpl;tpl/cc_sorcerer_exalted.tpl/cc_sorcerer_exalted.tpl;tpl/cc_vortex_beast.tpl/cc_vortex_beast.tpl;scenes/hub_battle_barge.scn/hub_battle_barge.cd_list;scenes/story_intro.scn/story_intro.cd_list;scenes/pvp_city.scn/pvp_city.cd_list;tpl/cc_calgar.tpl/cc_calgar.tpl;scenes/hub_battle_barge.scn/hub_battle_barge.cd_list")
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null; Remove-Item $Out -EA 0
$psi = New-Object Diagnostics.ProcessStartInfo $Exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables["SM2_MEMSOAK"] = $Steps
$psi.EnvironmentVariables["SM2_MEMSOAK_OUT"] = $Out
$psi.EnvironmentVariables["SM2_MEMSOAK_DWELL"] = "$Dwell"
$psi.EnvironmentVariables["SM2_MEMSOAK_ROUNDS"] = "$Rounds"
if ($LevelCache) { $psi.EnvironmentVariables["SM2_LEVEL_MEM_CACHE"] = $LevelCache }
$t = [Diagnostics.Stopwatch]::StartNew()
$p = [Diagnostics.Process]::Start($psi); try { $p.PriorityClass = 'High' } catch {}
[long]$peak = 0
while (-not $p.HasExited -and $t.Elapsed.TotalMinutes -lt 40) { Start-Sleep 2; $p.Refresh(); if (-not $p.HasExited) { $peak = [Math]::Max($peak, $p.PrivateMemorySize64) } ; if ((Get-Content $Out -Raw -EA 0) -match "# done") { break } }
Start-Sleep 3; if (-not $p.HasExited) { $p.Kill() }
"pid $($p.Id) wall $([math]::Round($t.Elapsed.TotalSeconds,1))s outer_peak_private_mb $([int]($peak/1MB))" | Add-Content $Out
Get-Content $Out
