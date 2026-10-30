# PoE2 prepared-folder builder with texture follow-up: extract+translate patterns, then every .dds named by the extracted .mat files,
# then decompress .ast animation bundles. Usage: mesh_prep.ps1 -Out <folder> -Patterns <glob>...
param([Parameter(Mandatory)][string]$Out, [Parameter(Mandatory)][string[]]$Patterns,
  [string]$Ggpk = 'D:\Daum Games\Path of Exile2\Content.ggpk', [string]$Bin = "$env:USERPROFILE\Desktop\SM2_Work\PoE2\tools")
$pdt = Join-Path $Bin 'poe_data_tools.exe'; $ab = Join-Path $Bin 'ast_bundle.exe'
& $pdt -p 2 --ggpk $Ggpk extract (Join-Path $Out 'raw') @Patterns 2>&1 | Out-Null
& $pdt -p 2 --ggpk $Ggpk translate (Join-Path $Out 'json') @Patterns 2>&1 | Out-Null
# .sm -> its .mat files may live elsewhere: translate referenced mats too
$mats = Get-ChildItem -Recurse (Join-Path $Out 'json') -Filter *.sm.json | ForEach-Object { [regex]::Matches((Get-Content $_.FullName -Raw), '"mat_file":"([^"]+)"') | ForEach-Object { $_.Groups[1].Value.ToLower() } } | Sort-Object -Unique
if ($mats) { & $pdt -p 2 --ggpk $Ggpk translate (Join-Path $Out 'json') @mats 2>&1 | Out-Null }
$dds = Get-ChildItem -Recurse (Join-Path $Out 'json') -Filter *.mat.json | ForEach-Object { [regex]::Matches((Get-Content $_.FullName -Raw), '"path":"([^"]+\.dds)"') | ForEach-Object { $_.Groups[1].Value.ToLower() } } | Sort-Object -Unique
if ($dds) { & $pdt -p 2 --ggpk $Ggpk extract (Join-Path $Out 'raw') @dds 2>&1 | Out-Null }
Get-ChildItem -Recurse (Join-Path $Out 'raw') -Filter *.ast | ForEach-Object { $b = $_.FullName + '.anim.bin'; if (-not (Test-Path $b)) { & $ab $_.FullName $b 2>&1 | Out-Null } }
"prepared $Out mats $(@($mats).Count) dds $(@($dds).Count)"
