# R86: idle memory at startup by last selection (settings.json SelectedFile/Folder rewritten; original restored after).
param([string]$Exe,[string]$Tag,[string]$Out,[int]$Wait=75,[switch]$GcDump,[string]$Env="")
$sj="$env:LOCALAPPDATA\LibSaber\SM2Browser\settings.json"; $bak="$Out\settings.orig.json"; if(-not (Test-Path $bak)){ Copy-Item $sj $bak }
$cases=[ordered]@{ none=@($null,$null,$null); model=@('tpl/cc_calgar.tpl','tpl/cc_calgar.tpl/cc_calgar.tpl',$null); level=@('scenes/hub_battle_barge.scn','scenes/hub_battle_barge.scn/hub_battle_barge.cd_list',$null) }
foreach($k in $cases.Keys){
  $j=Get-Content $bak -Raw | ConvertFrom-Json; $j.SelectedFolder=$cases[$k][0]; $j.SelectedFile=$cases[$k][1]; $j.SelectedFileArchive=$null
  $j | ConvertTo-Json -Depth 20 | Set-Content $sj -Encoding UTF8
  $psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
  foreach($kv in ($Env -split ';' | ? { $_ })){ $a,$b=$kv -split '=',2; $psi.EnvironmentVariables[$a]=$b }
  $p=[Diagnostics.Process]::Start($psi); [long]$peak=0
  for($s=0;$s -lt $Wait;$s++){ Start-Sleep 1; $p.Refresh(); $peak=[Math]::Max($peak,$p.PrivateMemorySize64) }
  $p.Refresh(); $line="r86idle $Tag $k private_mb $([int]($p.PrivateMemorySize64/1MB)) ws_mb $([int]($p.WorkingSet64/1MB)) peak_private_mb $([int]($peak/1MB)) title '$($p.MainWindowTitle)'"
  $ml=Get-Content "$env:LOCALAPPDATA\ForgeLens\memory.log" -Tail 1; $line += " | memlog: $ml"
  if($GcDump -and $k -eq 'none'){ & "$env:USERPROFILE\.dotnet\tools\dotnet-gcdump.exe" report -p $p.Id 2>&1 | select -first 45 | Set-Content "$Out\${Tag}_none_gcdump.txt" }
  $line | Add-Content "$Out\idle.txt"; $line
  $p.CloseMainWindow()|Out-Null; Start-Sleep 5; if(-not $p.HasExited){$p.Kill()}
}
Copy-Item $bak $sj -Force
