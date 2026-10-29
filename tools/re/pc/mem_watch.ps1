# Log-only RAM watch: appends a line to re_env\mem_watch.log when free RAM < 20 GB. Never kills anything.
# Usage: powershell -File mem_watch.ps1 [-Minutes 0 = run forever, check every 60 s]
param([int]$Minutes = 0, [int]$MinFreeGB = 20)
$L = "$env:USERPROFILE\Desktop\SM2_Work\Tools\re_env\mem_watch.log"; $t0 = Get-Date
while ($Minutes -eq 0 -or ((Get-Date) - $t0).TotalMinutes -lt $Minutes) {
  $free = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
  if ($free -lt $MinFreeGB) {
    $top = (Get-Process | sort WS -Descending | select -First 3 | % { "$($_.Name)=$([int]($_.WS/1GB))G" }) -join ' '
    Add-Content $L "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') KST free=${free}G < ${MinFreeGB}G top: $top"
  }
  Start-Sleep 60
}
