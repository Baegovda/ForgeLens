# Bulk export: manifest.csv (asset,category,anims ';'-separated globs) -> Exports\bulk\<asset>, log <asset>.log, results.json
param([string]$Only = "", [int]$Par = 4, [string]$Exe = "")
$W = "$env:USERPROFILE\Desktop\SM2_Work"; $X = if ($Exe) { $Exe } else { "$env:USERPROFILE\SM2Extractor6\LibSaber.SM2Extractor.exe" }
$G = "D:\Steam\steamapps\common\Space Marine 2\client_pc"; $O = "$W\Exports\bulk"
New-Item -ItemType Directory -Force $O | Out-Null
$rows = Import-Csv "$W\Scripts\bulk\manifest.csv" | ? { -not $Only -or ($Only -split ',') -contains $_.asset }
$jobs = @()
foreach ($r in $rows) {
  while (@($jobs | ? State -eq 'Running').Count -ge $Par) { Start-Sleep -Milliseconds 500 }
  $jobs += Start-Job -ArgumentList $X, $G, $O, $r.asset, $r.anims {
    param($X, $G, $O, $a, $anims)
    $d = "$O\$a"; if (Test-Path $d) { $n = 1; while (Test-Path "$d.bak$n") { $n++ }; Rename-Item $d "$a.bak$n" }
    $args2 = @("export", $G, "--asset", "tpl/$a.tpl", "--format", "fbx-unreal", "--output", $d)
    if ($anims) { $args2 += @("--anim", $anims) }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $X @args2 *> "$O\$a.log"; $code = $LASTEXITCODE
    [pscustomobject]@{ asset = $a; exit = $code; seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1) }
  }
}
$res = $jobs | Wait-Job | Receive-Job | select asset, exit, seconds
$prev = if (Test-Path "$O\results.json") { @(Get-Content "$O\results.json" | ConvertFrom-Json) | ? { $res.asset -notcontains $_.asset } } else { @() }
@($prev) + @($res) | ConvertTo-Json | Set-Content -Encoding utf8 "$O\results.json"
$res | ft -auto | Out-String -Width 200
