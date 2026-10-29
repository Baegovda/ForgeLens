# Disk watch (alerts.txt line [disk]): warn when C: < 100 GB or a used drive (D: game, K: UE/backup, J:, E:) < 50 GB free.
param([int]$MinC = 100, [int]$MinOther = 50, [string[]]$Drives = @('C','D','E','J','K'), [switch]$Verbose_)
. "$PSScriptRoot\alerts.ps1"
$low = @(); $all = @()
foreach ($d in $Drives) {
  $v = Get-PSDrive $d -EA SilentlyContinue; if (-not $v -or $null -eq $v.Free) { continue }
  $g = [int]($v.Free / 1GB); $all += "${d}:${g}G"; $min = if ($d -eq 'C') { $MinC } else { $MinOther }
  if ($g -lt $min) { $low += "${d}: ${g}G < ${min}G" }
}
Set-Alert disk ($low -join ', '); if ($low -or $Verbose_) { "free: $($all -join ' ')  LOW: $($low -join ', ')" }
