param($dir,$out,$frames)
Add-Type -AssemblyName System.Drawing
$fs=$frames -split ','; $W=320;$H=240; $sheet=New-Object Drawing.Bitmap ($W*$fs.Count),$H; $g=[Drawing.Graphics]::FromImage($sheet)
for($i=0;$i -lt $fs.Count;$i++){ $im=[Drawing.Image]::FromFile("$dir\fire_00$($fs[$i]).png"); $g.DrawImage($im,(New-Object Drawing.Rectangle ($i*$W),0,$W,$H),(New-Object Drawing.Rectangle 425,170,$W,$H),[Drawing.GraphicsUnit]::Pixel); $g.DrawString($fs[$i],(New-Object Drawing.Font 'Arial',14),[Drawing.Brushes]::Red,$i*$W+4,4); $im.Dispose() }
$sheet.Save($out)
