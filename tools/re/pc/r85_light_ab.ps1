# R85: lightweight A/B in ONE build: each variant (env switches) x each model, window pinned (SM2_WINRECT), no UETEST.
# Per run: select_to_level_shown, GPU upload MB (vbo/ibo/tex, from the winshot txt), private MB, GPU dedicated MB, idle CPU
# (all via r83_viewer_ab.ps1) + the GPU frame read back in-process (win_m.png.gpu.raw, r85_pxdiff.py compares). Writes summary.tsv.
param([string]$Exe,[string]$Out,[string]$Rect="100,100,1600,900",[string[]]$Models=@("calgar=tpl/cc_calgar.tpl/cc_calgar.tpl","sorcerer=tpl/cc_sorcerer_exalted.tpl/cc_sorcerer_exalted.tpl","vortex=tpl/cc_vortex_beast.tpl/cc_vortex_beast.tpl"),
  [string[]]$Variants=@("base=SM2_VTX64=1;SM2_TEXCAP=0;SM2_IDX32=1","idx16=SM2_VTX64=1;SM2_TEXCAP=0","packed=SM2_TEXCAP=0","texcap=SM2_X=0"),[int]$Repeat=1)
$ab=Join-Path $PSScriptRoot "r83_viewer_ab.ps1"; New-Item -ItemType Directory -Force $Out | Out-Null
$rows=@("variant`tmodel`trun`topen_s`tvbo_mb`tibo_mb`ttex_mb`tprivate_mb`tgpu_dedicated_mb`tidle_cpu")
for($r=1;$r -le $Repeat;$r++){ foreach($v in $Variants){ $vn,$ve=$v -split '=',2; foreach($m in $Models){ $mn,$mp=$m -split '=',2
  $dir="$Out\$vn`_$mn`_$r"
  $txt=& $ab -Exe $Exe -Out $dir -Select $mp -Name m -Env $ve -Done 'r85 gpu_upload|winshot failed' -Rect $Rect | Out-String
  $g={ param($p) if($txt -match $p){ $Matches[1] } else { '' } }
  $rows+="$vn`t$mn`t$r`t$(& $g 'select_to_level_shown ([\d.]+)s')`t$(& $g 'vbo_mb ([\d.]+)')`t$(& $g 'ibo_mb ([\d.]+)')`t$(& $g 'tex_mb ([\d.]+)')`t$(& $g 'private_mb (\d+)')`t$(& $g 'gpu_dedicated_mb (\d+)')`t$(& $g 'idle_cpu_pct_one_core ([\d.]+)')"
  $rows | Set-Content "$Out\summary.tsv" } } }
Get-Content "$Out\summary.tsv"

