$ErrorActionPreference='Continue'
. C:\Users\Revaptor_FX\Desktop\SM2_Work\PoE2\fxg_cap.ps1
$log="$R\fxg_log2.txt"; function L($m){ "$(Get-Date -f HH:mm:ss) $m" | Add-Content $log }
Add-Content $log 'start2'
$out="$R\dev\docs\handoff\poe2_m2\viewer_fx"; New-Item -ItemType Directory -Force "$out\raw" | Out-Null
function Score($bmp){ $n=0; for($y=0;$y -lt $bmp.Height;$y+=3){ for($x=0;$x -lt $bmp.Width;$x+=3){ $c=$bmp.GetPixel($x,$y); $m=[Math]::Max($c.R,[Math]::Max($c.G,$c.B)); $mn=[Math]::Min($c.R,[Math]::Min($c.G,$c.B)); if($m -gt 150 -or ($m-$mn) -gt 45){$n++} } }; $n }
foreach($j in (Get-Content "$R\fxg_skills.txt" | ? { $_ -match '\S' })){ $f=$j.Split('|'); $name=$f[0]
  if (Get-Process | ? { $_.ProcessName -match 'PathOfExile' }) { L 'game started: stop'; break }
  $env:FL_OPEN_POE2='1'; $env:FL_POE2_AUTOOPEN='1'; $env:FL_POE2_SKILL=$f[1]; $env:FL_POE2_EFFECT=$f[2]; $env:FL_POE2_AUTOPLAY='1'; $env:FL_POE2_FX_LEGACYPLATE=$f[3]
  $p=Start-Process "$R\bin_g\ForgeLens.exe" -PassThru -WindowStyle Normal -WorkingDirectory "$R\bin_g"; Start-Sleep 28
  $h=$null; foreach($w in [W4]::Of($p.Id)){ $sb=New-Object Text.StringBuilder 256; [W4]::GetWindowText($w,$sb,256)|Out-Null; if($sb.ToString() -like 'Forge Lens*'){$h=$w} }
  if(-not $h){ L "no window $name"; Stop-Process -Id $p.Id -Force -EA 0; continue }
  [W4]::SetWindowPos($h,[IntPtr](-1),0,0,0,0,0x13)|Out-Null; Start-Sleep -m 300; $wr=New-Object W4+RECT; [W4]::GetWindowRect($h,[ref]$wr)|Out-Null
  $shots=@(); for($k=0;$k -lt 26;$k++){ $b=New-Object Drawing.Bitmap ($wr.R-$wr.L),($wr.B-$wr.T); $g=[Drawing.Graphics]::FromImage($b); $g.CopyFromScreen($wr.L,$wr.T,0,0,$b.Size); $g.Dispose(); $shots+=$b; Start-Sleep -m 130 }
  [W4]::SetWindowPos($h,[IntPtr](-2),0,0,0,0,0x13)|Out-Null
  $shots[0].Save("$out\raw\${name}_window.png")
  $sf=$shots[0].Width/1024.0; $vp=New-Object Drawing.Rectangle ([int](433*$sf)),([int](148*$sf)),([int](409*$sf)),([int](360*$sf))
  $sc=@(); for($k=0;$k -lt $shots.Count;$k++){ $c=$shots[$k].Clone($vp,$shots[$k].PixelFormat); $sc+=[pscustomobject]@{k=$k;s=(Score $c);c=$c} }
  $pick=@($sc | sort s -Desc | select -First 1); foreach($q in ($sc | sort s -Desc)){ if($pick.Count -ge 3){break}; if(($pick | ? { [Math]::Abs($_.k-$q.k) -lt 4 }).Count -eq 0){ $pick+=$q } }
  $pick = $pick | sort k; $i=0; foreach($q in $pick){ $i++; $q.c.Save("$out\raw\${name}_$i.png") }
  L "captured $name ($($f[1]) / $($f[2]) / legacy=$($f[3])) scores $(($sc|%{$_.s}) -join ',')"
  $shots|%{$_.Dispose()}; Stop-Process -Id $p.Id -Force -EA 0; Start-Sleep 2 }
L 'CAPTURE DONE'



