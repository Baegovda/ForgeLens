param($p)
Add-Type -AssemblyName System.Drawing; $b=[Drawing.Bitmap]::FromFile($p); $r=0;$g=0;$bl=0;$n=0;$clip=0
for($y=0;$y -lt $b.Height;$y+=4){ for($x=0;$x -lt $b.Width;$x+=4){ $c=$b.GetPixel($x,$y); $r+=$c.R;$g+=$c.G;$bl+=$c.B;$n++; if($c.R -ge 254 -and $c.G -ge 254 -and $c.B -ge 254){$clip++} } }
"{0} mean {1:0},{2:0},{3:0} clip {4:0.0}%" -f (Split-Path $p -Leaf),($r/$n),($g/$n),($bl/$n),(100*$clip/$n)
