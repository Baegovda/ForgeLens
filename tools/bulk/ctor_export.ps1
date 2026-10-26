# Constructor re-export (exe10): default armor variant + png textures -> Exports\bulk_fix\<asset>, ctor_results.json
param([string]$Variant = "", [string]$Tex = "png", [int]$Par = 3, [switch]$InPlace)
$W = "$env:USERPROFILE\Desktop\SM2_Work"; $X = "$W\Bin\SM2Extractor10\LibSaber.SM2Extractor.exe"
$G = "D:\Steam\steamapps\common\Space Marine 2\client_pc"; $O = "$W\Exports\bulk_fix"
$rows = Import-Csv "$W\Scripts\bulk\manifest.csv" | ? { $_.asset -match 'constructor' }
$jobs = @()
foreach ($r in $rows) {
  while (@($jobs | ? State -eq 'Running').Count -ge $Par) { Start-Sleep -Milliseconds 500 }
  $jobs += Start-Job -ArgumentList $X, $G, $O, $r.asset, $r.anims, $Variant, $Tex, $InPlace.IsPresent {
    param($X, $G, $O, $a, $anims, $variant, $tex, $inPlace)
    $d = "$O\$a"; if (-not $inPlace -and (Test-Path $d)) { $n = 1; while (Test-Path "$d.bak$n") { $n++ }; Rename-Item $d "$a.bak$n" }
    $args2 = @("export", $G, "--asset", "tpl/$a.tpl", "--format", "fbx-unreal", "--output", $d, "--tex-formats", $tex)
    if ($variant) { $args2 += @("--variant", $variant) }
    if ($anims) { $args2 += @("--anim", $anims) }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $X @args2 *> "$O\$a.log"; $code = $LASTEXITCODE
    $files = Get-ChildItem $d -Recurse -File -ErrorAction SilentlyContinue
    [pscustomobject]@{ asset = $a; exit = $code; seconds = [math]::Round($sw.Elapsed.TotalSeconds, 1); gb = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1GB, 3); files = $files.Count; png = @($files | ? Extension -eq '.png').Count; fbxMB = [math]::Round((($files | ? Name -eq "$a.fbx" | Measure-Object Length -Sum).Sum) / 1MB, 1) }
  }
}
$res = $jobs | Wait-Job | Receive-Job | select asset, exit, seconds, gb, files, png, fbxMB
$res | ConvertTo-Json | Set-Content -Encoding utf8 "$O\ctor_results.json"
$res | ft -auto | Out-String -Width 200
