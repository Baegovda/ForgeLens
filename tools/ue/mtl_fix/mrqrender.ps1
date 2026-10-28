# mtl1-22 MRQ CLI render: mrqrender.ps1 <map> <LevelSequence> <MRQ config> [outDir]
# Asset args may be package paths (/Game/X/LS_a); they are expanded to full object paths (/Game/X/LS_a.LS_a). With a bare
# package path the CLI executor resolves the sequence soft path with an empty asset name, so {sequence_name} in
# file_name_format rendered as "" (Captures\mrq16\.0000.png). Runs hidden at Idle priority; prints the new output files.
param([string]$map, [string]$seq, [string]$cfg, [string]$outDir = "")
function Full([string]$p) { if ($p -match '\.[^/]+$') { $p } else { "$p." + ($p -split '/')[-1] } }
$ue = "C:\Program Files\Epic Games\UE_5.7\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
$proj = "K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject"
$t0 = Get-Date
$a = @("`"$proj`"", $map, "-game", "-LevelSequence=`"$(Full $seq)`"", "-MoviePipelineConfig=`"$(Full $cfg)`"", "-RenderOffscreen", "-windowed", "-Unattended", "-NoSplash", "-NoSound", "-log=mrqrender.log")
"MRQR args: " + ($a -join ' ')
$p = Start-Process $ue -ArgumentList $a -WindowStyle Hidden -PassThru
try { $p.PriorityClass = 'Idle' } catch {}
$p.WaitForExit()
"MRQR exit $($p.ExitCode) in $([int]((Get-Date) - $t0).TotalSeconds)s"
if ($outDir) { Get-ChildItem $outDir -File | ? LastWriteTime -gt $t0 | sort Name | select -First 3 -ExpandProperty Name; "MRQR new files: " + @(Get-ChildItem $outDir -File | ? LastWriteTime -gt $t0).Count }
