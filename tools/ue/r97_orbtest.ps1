param($ns,$tag,$f=45)
$U="$env:USERPROFILE\Desktop\SM2_Work\Build\r73\tools\ue\uerun.ps1"; $T="C:\Users\Revaptor_FX\fl_r95fid\tools\ue"
& $U "$T\ue_r97_orbdark.py" $ns 2>&1 | Select-String 'R97T|Exception' | % Line
$o="$env:USERPROFILE\Desktop\SM2_Work\Shots\r97_orb\$tag"; New-Item -ItemType Directory -Force $o|Out-Null
& $U "$T\ue_r97_firemrq.py" "$o,$f,$($f+1),0,,/Game/ForgeLens/SM2/VFX/vortex_orb/LS_orb_dark" 2>&1 | Select-String 'Exception' | % Line
for($i=0;$i -lt 60 -and -not (Test-Path "$o\fire_00$f.png");$i++){Start-Sleep 3}; Start-Sleep 4
Add-Type -AssemblyName System.Drawing; $b=[Drawing.Bitmap]::FromFile("$o\fire_00$f.png"); $n=0;$r=0;$g=0;$bb=0; for($y=0;$y -lt $b.Height;$y+=4){for($x=0;$x -lt $b.Width;$x+=4){$c=$b.GetPixel($x,$y); if($c.R+$c.G+$c.B -gt 30){$n++;$r+=$c.R;$g+=$c.G;$bb+=$c.B}}}; if($n){"lit px $n mean $([int]($r/$n)),$([int]($g/$n)),$([int]($bb/$n))"}else{"lit px 0"}
$bg=New-Object Drawing.Bitmap $b.Width,$b.Height; $gr=[Drawing.Graphics]::FromImage($bg); $gr.Clear([Drawing.Color]::Black); $gr.DrawImage($b,0,0); $bg.Save("$o\view.jpg",[Drawing.Imaging.ImageFormat]::Jpeg); $b.Dispose()

