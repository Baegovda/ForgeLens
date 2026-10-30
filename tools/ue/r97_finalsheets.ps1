# r97_finalsheets.ps1 (R97w): game crop (left third of the R96 sheet) | UE lit final (Shots/r96/r97w_<tag>_lit.png) + all-character contact sheet.
Add-Type -AssemblyName System.Drawing
$S="$env:USERPROFILE\Desktop\SM2_Work\Shots\r96"; $D="C:\Users\Revaptor_FX\fl_r95fid\docs\handoff"; $f=New-Object Drawing.Font "Arial",11
foreach($p in @(@("lesser_sorc","lesser_sorc"),@("hormagaunt","hormagaunt"),@("vortex","vortex"),@("term_occult","term_occult"))){
 $old=[Drawing.Image]::FromFile("$D\r96\sheet_$($p[0]).jpg"); $ue=[Drawing.Image]::FromFile("$S\r97w_$($p[1])_lit.png")
 $gw=[int]($old.Width/3); $hh=$old.Height-14; $uw=[int]($ue.Width*$hh/$ue.Height)
 $o=New-Object Drawing.Bitmap ($gw+$uw),($hh+18); $g=[Drawing.Graphics]::FromImage($o); $g.Clear([Drawing.Color]::Black)
 $g.DrawImage($old,(New-Object Drawing.Rectangle 0,0,$gw,$hh),(New-Object Drawing.Rectangle 0,0,$gw,$hh),[Drawing.GraphicsUnit]::Pixel); $g.DrawImage($ue,$gw,0,$uw,$hh)
 $g.DrawString("$($p[0]): game (user F12 crop) | UE final R97w (neutral room dir20 sky6 EV0, RTF_RGBA8 non-sRGB)",$f,[Drawing.Brushes]::White,2,$hh+2)
 $o.Save("$D\r97\final_sheet_$($p[0]).jpg",[Drawing.Imaging.ImageFormat]::Jpeg); $old.Dispose(); $ue.Dispose(); "sheet $($p[0])" }
$T=@("vortex","exalted_sorc","lesser_sorc","hormagaunt","titus","titus_cloth","calgar","chaplain","term_occult"); $o=New-Object Drawing.Bitmap 1500,1000; $g=[Drawing.Graphics]::FromImage($o)
for($i=0;$i -lt $T.Count;$i++){ $im=[Drawing.Image]::FromFile("$S\r97w_$($T[$i])_lit.png"); $x=($i%3)*500; $y=[Math]::Floor($i/3)*333; $g.DrawImage($im,$x,$y,500,333); $g.DrawString($T[$i],$f,[Drawing.Brushes]::Yellow,$x+4,$y+4); $im.Dispose() }
$o.Save("$D\r97\final_all_characters.jpg",[Drawing.Imaging.ImageFormat]::Jpeg); "contact ok"
# R97y: vortex sheet with the final orb (hub MRQ, NS_vortex_orb_v7 + orb light on Vortex lighting channel + bloom)
$old=[Drawing.Image]::FromFile("$D\r96\sheet_vortex.jpg"); $ue=[Drawing.Image]::FromFile("$S\r97w_vortex_lit.png"); $hb=[Drawing.Image]::FromFile("$env:USERPROFILE\Desktop\SM2_Work\Shots\r97_orb\hub7e\view.jpg")
$gw=[int]($old.Width/3); $hh=$old.Height-14; $uw=[int]($ue.Width*$hh/$ue.Height); $hw=[int](560*$hh/560)
$bmp=New-Object Drawing.Bitmap ($gw+$uw+$hw),($hh+18); $g=[Drawing.Graphics]::FromImage($bmp); $g.Clear([Drawing.Color]::Black)
$g.DrawImage($old,(New-Object Drawing.Rectangle 0,0,$gw,$hh),(New-Object Drawing.Rectangle 0,0,$gw,$hh),[Drawing.GraphicsUnit]::Pixel); $g.DrawImage($ue,$gw,0,$uw,$hh)
$g.DrawImage($hb,(New-Object Drawing.Rectangle ($gw+$uw),0,$hw,$hh),(New-Object Drawing.Rectangle 360,60,560,560),[Drawing.GraphicsUnit]::Pixel)
$g.DrawString("vortex: game | UE neutral room (no Niagara in SceneCapture) | UE hub MRQ final orb: core 163,123,173 vs game 194,177,217",$f,[Drawing.Brushes]::White,2,$hh+2)
$bmp.Save("$D\r97\final_sheet_vortex.jpg",[Drawing.Imaging.ImageFormat]::Jpeg); $old.Dispose(); $ue.Dispose(); $hb.Dispose(); "vortex orb sheet"
