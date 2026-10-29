# RAM watch (log + alerts.txt line [mem]). Never kills anything.
# Usage: powershell -File mem_watch.ps1 [-Once] [-Minutes 0 = forever, every 60 s] [-MinFreeGB 20]   (scheduled: -Once every 10 min)
param([switch]$Once, [int]$Minutes = 0, [int]$MinFreeGB = 20)
. "$PSScriptRoot\alerts.ps1"
$L = "$env:USERPROFILE\Desktop\SM2_Work\Tools\re_env\mem_watch.log"; $t0 = Get-Date
do {
  $free = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
  if ($free -lt $MinFreeGB) {
    $top = (Get-Process | sort WS -Descending | select -First 3 | % { "$($_.Name)=$([int]($_.WS/1GB))G" }) -join ' '
    $msg = "free RAM ${free}G < ${MinFreeGB}G top: $top"; Add-Content $L "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') KST $msg"; Set-Alert mem $msg
  } else { Set-Alert mem '' }
  if (-not $Once) { "free RAM ${free} GB" }
  if ($Once) { break }; Start-Sleep 60
} while ($Minutes -eq 0 -or ((Get-Date) - $t0).TotalMinutes -lt $Minutes)
