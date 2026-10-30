param($in,$out)
Add-Type -AssemblyName System.Drawing
$b=[Drawing.Bitmap]::FromFile($in); $r=New-Object Drawing.Rectangle 0,0,$b.Width,$b.Height
$d=$b.LockBits($r,'ReadWrite','Format32bppArgb'); $n=$d.Stride*$b.Height; $a=New-Object byte[] $n
[Runtime.InteropServices.Marshal]::Copy($d.Scan0,$a,0,$n); for($i=3;$i -lt $n;$i+=4){$a[$i]=255}
[Runtime.InteropServices.Marshal]::Copy($a,0,$d.Scan0,$n); $b.UnlockBits($d); $b.Save($out,[Drawing.Imaging.ImageFormat]::Jpeg); $b.Dispose()
