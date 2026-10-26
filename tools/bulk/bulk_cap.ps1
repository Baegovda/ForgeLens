# Offscreen lit captures (ue_cap_lit.py, intensity=80) of every bulk asset with a .ue.json -> Captures\bulk\<asset>\{front,side,back}.png
param([string]$Only = "")
$S = "$env:USERPROFILE\Desktop\SM2_Work\Scripts"; $O = "$env:USERPROFILE\Desktop\SM2_Work\Exports\bulk"; $C = "$env:USERPROFILE\Desktop\SM2_Work\Captures\bulk"
function U($py, $a) { $r = & "$S\uerun.ps1" "$S\$py" @($a) | Out-String; try { $j = ($r.Substring($r.IndexOf('{')) | ConvertFrom-Json); $j.output; if (-not $j.success) { "PYERR " + $j.result } } catch { $r } }
$list = if ($Only) { $Only -split ',' } else { dir $O -Filter *.ue.json | % { $_.Name -replace '\.ue\.json$', '' } }
foreach ($a in $list) {
  $cap = U ue_cap_lit.py @("capture", "/Game/SM2_Work/Tests/bulk/$a/$a", "$C\$a", "bulk", "intensity=80") | Out-String
  $e = U ue_cap_lit.py @("export") | Out-String
  "$a " + (($cap + $e) -split "`n" | ? { $_ -match "captured|exported|PYERR|Error" } | % { $_.Trim() }) -join ' '
}
