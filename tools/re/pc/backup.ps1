# Backup of big non-git artefacts (docs/handoff/BACKUPS.md) to K:\SM2_Backup. Never deletes (robocopy /E, no /MIR, no /PURGE).
# Usage: backup.ps1 copy [-Src name=path ...]   -> copies + writes <dest>\<name>.sha256 manifest (relpath<TAB>size<TAB>sha256)
#        backup.ps1 verify                     -> re-hashes every manifest entry in the backup, prints OK/BAD counts
#        backup.ps1 record <name> <path>       -> manifest only, no copy (proj_sm2 while GhidraServe runs)
param([Parameter(Mandatory)][ValidateSet('copy','verify','record')]$Cmd, [string]$Name, [string]$Path, [string]$Dest = 'K:\SM2_Backup')
$W = "$env:USERPROFILE\Desktop\SM2_Work"
$Default = [ordered]@{ Raw = "$W\Raw"; ghidra_proj3 = "$W\Tools\re_env\ghidra_proj3"; sm2_retail_exe = "$W\Tools\re_env\work\sm2_retail.exe" }
[System.Diagnostics.Process]::GetCurrentProcess().PriorityClass = 'High'
$Sha = [System.Security.Cryptography.SHA256]::Create()
function Sha256Of($f) { $st = [IO.File]::OpenRead($f); try { -join ($Sha.ComputeHash($st) | % { $_.ToString('x2') }) } finally { $st.Close() } }   # no Get-FileHash dependency (missing in some PS 5 hosts)
function Manifest($root, $out) {
  $base = if ((Get-Item $root).PSIsContainer) { (Resolve-Path $root).Path.TrimEnd('\') } else { Split-Path (Resolve-Path $root).Path }
  $files = if ((Get-Item $root).PSIsContainer) { Get-ChildItem $root -Recurse -File } else { Get-Item $root }
  $files | % { "{0}`t{1}`t{2}" -f $_.FullName.Substring($base.Length + 1), $_.Length, (Sha256Of $_.FullName) } | Set-Content $out -Encoding UTF8
  $s = ($files | measure Length -Sum); "$out : $($s.Count) files, $('{0:N0}' -f $s.Sum) B"
}
New-Item -ItemType Directory -Force $Dest | Out-Null
switch ($Cmd) {
 'copy' {
   $set = if ($Name) { @{ $Name = $Path } } else { $Default }
   foreach ($k in $set.Keys) { $src = $set[$k]; if (-not (Test-Path $src)) { "skip $k (missing $src)"; continue }
     $t0 = Get-Date
     if ((Get-Item $src).PSIsContainer) { robocopy $src "$Dest\$k" /E /COPY:DAT /R:1 /W:1 /NFL /NDL /NP /MT:8 | Out-Null } else { New-Item -ItemType Directory -Force "$Dest\$k" | Out-Null; Copy-Item $src "$Dest\$k\" -Force }
     Manifest "$Dest\$k" "$Dest\$k.sha256"; "copied $k in $([int]((Get-Date)-$t0).TotalSeconds) s" }
 }
 'record' { $t0 = Get-Date; Manifest $Path "$Dest\$Name.record.sha256"; "recorded (no copy) $Name in $([int]((Get-Date)-$t0).TotalSeconds) s" }
 'verify' {
   foreach ($m in Get-ChildItem "$Dest\*.sha256" | ? { $_.Name -notlike '*.record.sha256' }) {
     $k = $m.BaseName; $ok = 0; $bad = @()
     foreach ($l in Get-Content $m.FullName) { $p, $sz, $h = $l -split "`t"; $f = "$Dest\$k\$p"
       if ((Test-Path $f) -and (Sha256Of $f) -eq $h) { $ok++ } else { $bad += $p } }
     "$k : OK $ok, BAD $($bad.Count) $($bad | select -First 5)" }
 }
}
