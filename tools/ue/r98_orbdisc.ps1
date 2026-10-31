param($p,$cx,$cy,$rad)
Add-Type -AssemblyName System.Drawing; $b=[Drawing.Bitmap]::FromFile($p); $r=0;$g=0;$bl=0;$n=0
for($y=$cy-$rad;$y -le $cy+$rad;$y++){for($x=$cx-$rad;$x -le $cx+$rad;$x++){ if(($x-$cx)*($x-$cx)+($y-$cy)*($y-$cy) -le $rad*$rad){$c=$b.GetPixel($x,$y);$r+=$c.R;$g+=$c.G;$bl+=$c.B;$n++}}}
"{0} disc({1},{2},r{3}) mean {4:0},{5:0},{6:0}" -f (Split-Path $p -Leaf),$cx,$cy,$rad,($r/$n),($g/$n),($bl/$n); $b.Dispose()
