# Sequential UE import+check of every exported bulk asset (results.json exit 0), BULKJSON lines -> Exports\bulk\ue_results.jsonl
param([string]$Only = "", [string]$Mode = "import")
$S = "$env:USERPROFILE\Desktop\SM2_Work\Scripts"; $O = "$env:USERPROFILE\Desktop\SM2_Work\Exports\bulk"
function U($py, $a) { $r = & "$S\uerun.ps1" "$S\$py" @($a) | Out-String; try { $j = ($r.Substring($r.IndexOf('{')) | ConvertFrom-Json); $j.output; if (-not $j.success) { "PYERR " + $j.result } } catch { $r } }
$ok = @(Get-Content "$O\results.json" | ConvertFrom-Json) | ? { $_.exit -eq 0 } | % asset
if ($Only) { $ok = $Only -split ',' }
foreach ($a in $ok) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $o = U ue_bulk.py @($Mode, $a) | Out-String
  $line = ($o -split "`n" | ? { $_ -match "^BULKJSON " } | select -First 1)
  if (-not $line) { $line = "BULKJSON " + (@{ asset = $a; errors = @("ue call failed: " + (($o -split "`n" | ? { $_ -match "PYERR|Error|Traceback" }) -join ' | ').Substring(0, [Math]::Min(800, (($o -split "`n" | ? { $_ -match "PYERR|Error|Traceback" }) -join ' | ').Length))) } | ConvertTo-Json -Compress) }
  Add-Content "$O\ue_results.jsonl" ($line.Trim() -replace '^BULKJSON ', '')
  "$a $([math]::Round($sw.Elapsed.TotalSeconds,1))s"
}
