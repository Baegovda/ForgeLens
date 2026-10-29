# Shared helper: Set-Alert <tag> <text|''>. Keeps one line per tag in SM2_Work\alerts.txt (empty file = no alerts) and
# appends every change to SM2_Work\Tools\re_env\alerts.log. Read by the sm2-20 routine / the bot.
$Global:AlertFile = "$env:USERPROFILE\Desktop\SM2_Work\alerts.txt"
$Global:AlertLog = "$env:USERPROFILE\Desktop\SM2_Work\Tools\re_env\alerts.log"
function Set-Alert([string]$Tag, [string]$Text) {
  $lines = @(); if (Test-Path $AlertFile) { $lines = @(Get-Content $AlertFile | ? { $_ -and -not $_.StartsWith("[$Tag]") }) }
  $ts = Get-Date -Format 'yyyy-MM-dd HH:mm'
  if ($Text) { $lines += "[$Tag] $ts KST $Text"; Add-Content $AlertLog "[$Tag] $ts KST $Text" }
  $old = if (Test-Path $AlertFile) { (Get-Content $AlertFile) -join "`n" } else { $null }
  if ($old -ne ($lines -join "`n")) { Set-Content $AlertFile $lines -Encoding UTF8 }   # write only on change
}
