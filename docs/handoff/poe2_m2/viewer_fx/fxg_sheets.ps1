Add-Type -AssemblyName System.Drawing
$out='C:\Users\Revaptor_FX\Desktop\SM2_Work\PoE2\dev\docs\handoff\poe2_m2\viewer_fx'; $raw="$out\raw"
$font=New-Object Drawing.Font 'Segoe UI',13,([Drawing.FontStyle]::Bold); $wb=[Drawing.Brushes]::White; $bg=[Drawing.Color]::FromArgb(24,24,24)
$cw=410;$ch=361;$lh=26
function Grid($cells,$cols,$file,$title){ $rows=[Math]::Ceiling($cells.Count/$cols); $W=$cols*$cw; $H=40+$rows*($ch+$lh)
  $b=New-Object Drawing.Bitmap $W,$H; $g=[Drawing.Graphics]::FromImage($b); $g.Clear($bg); $g.DrawString($title,$font,$wb,8,8)
  for($i=0;$i -lt $cells.Count;$i++){ $x=($i % $cols)*$cw; $y=40+[Math]::Floor($i/$cols)*($ch+$lh); $g.DrawString($cells[$i].L,$font,$wb,$x+6,$y+2); if(Test-Path $cells[$i].P){ $im=[Drawing.Image]::FromFile($cells[$i].P); $g.DrawImage($im,$x,$y+$lh,$cw,$ch); $im.Dispose() } }
  $g.Dispose(); $b.Save($file); $b.Dispose() }
$names=@{}; Get-Content 'C:\Users\Revaptor_FX\Desktop\SM2_Work\PoE2\dev\docs\handoff\poe2_m2\skills.tsv' | select -Skip 1 | % { $f=$_.Split("`t"); if(-not $names[$f[0]]){ $names[$f[0]]=$f[1] } }
$ba=@(); foreach($n in 'arc_stormblast_BEFORE','arc_stormblast_AFTER','fireball_impact_BEFORE','fireball_impact_AFTER','flame_dash_BEFORE','flame_dash','frost_bomb_BEFORE','frost_bomb'){ 1..3 | % { $ba+=[pscustomobject]@{L="$n  #$_";P="$raw\${n}_$_.png"} } }
Grid $ba 3 "$out\before_after_plate.png" 'Plate fix in the real ForgeLens viewer: *_BEFORE = old cards, next row = after (3 time points each)'
$sk=Get-Content 'C:\Users\Revaptor_FX\Desktop\SM2_Work\PoE2\fxg_skills.txt' | ? { $_ -match '\S' -and $_ -notmatch '_BEFORE|_AFTER' } | % { $_.Split('|')[0..2] -join '|' }
$cells=@(); foreach($l in $sk){ $f=$l.Split('|'); $best=Get-ChildItem "$raw\$($f[0])_[123].png" -EA 0 | sort Length -Desc | select -First 1; if(-not $best){continue}; $nm=$names[$f[1]]; if(-not $nm){$nm=$f[1]}; $cells+=[pscustomobject]@{L="$nm [$($f[1])] $($f[2])";P=$best.FullName} }
for($p=0;$p*12 -lt $cells.Count;$p++){ Grid @($cells[($p*12)..([Math]::Min($cells.Count,$p*12+12)-1)]) 4 "$out\skills_sheet_$($p+1).png" "PoE2 skills in the ForgeLens viewer (real window captures, sheet $($p+1))" }
if($false){ Copy-Item "C:\Users\Revaptor_FX\Desktop\SM2_Work\PoE2\fxg_cmp\$n.png" "$out\bench_${n}_before_top_after_bottom.png" -Force }
"cells $($cells.Count)"


