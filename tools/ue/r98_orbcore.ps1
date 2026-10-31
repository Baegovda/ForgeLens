param($p,$h=7)
Add-Type -AssemblyName System.Drawing; $b=[Drawing.Bitmap]::FromFile($p); $best=0;$bx=0;$by=0
for($y=0;$y -lt $b.Height;$y+=2){for($x=0;$x -lt $b.Width;$x+=2){$c=$b.GetPixel($x,$y);$l=$c.R+$c.G+$c.B;if($l -gt $best){$best=$l;$bx=$x;$by=$y}}}
$r=0;$g=0;$bl=0;$n=0; for($y=[Math]::Max(0,$by-$h);$y -le [Math]::Min($b.Height-1,$by+$h);$y++){for($x=[Math]::Max(0,$bx-$h);$x -le [Math]::Min($b.Width-1,$bx+$h);$x++){$c=$b.GetPixel($x,$y);$r+=$c.R;$g+=$c.G;$bl+=$c.B;$n++}}
# bloom: count px within 120 px radius with R>120 and B>120
$bp=0; for($y=[Math]::Max(0,$by-150);$y -le [Math]::Min($b.Height-1,$by+150);$y+=3){for($x=[Math]::Max(0,$bx-150);$x -le [Math]::Min($b.Width-1,$bx+150);$x+=3){$c=$b.GetPixel($x,$y);if($c.R -gt 110 -and $c.B -gt 110){$bp++}}}
"{0} peak@{1},{2} core15 {3:0},{4:0},{5:0} pinkpx {6}" -f (Split-Path $p -Leaf),$bx,$by,($r/$n),($g/$n),($bl/$n),$bp; $b.Dispose()
