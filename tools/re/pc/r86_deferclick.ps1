# R86: startup with the last file = a level: the level is NOT rebuilt; a real click (guarded: viewer must be foreground) on
# "Open last level" opens it. Private MB before / after. settings.json restored after.
param([string]$Exe,[string]$Out,[int]$BtnX=1103,[int]$BtnY=606)
$sj="$env:LOCALAPPDATA\LibSaber\SM2Browser\settings.json"; $bak="$Out\settings.orig_click.json"; Copy-Item $sj $bak -Force
$j=Get-Content $sj -Raw | ConvertFrom-Json; $j.SelectedFolder='scenes/hub_battle_barge.scn'; $j.SelectedFile='scenes/hub_battle_barge.scn/hub_battle_barge.cd_list'; $j.SelectedFileArchive=$null; $j | ConvertTo-Json -Depth 20 | Set-Content $sj -Encoding UTF8
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class N86b { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y); [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e); [DllImport("user32.dll")] public static extern void keybd_event(byte v,byte s,uint f,UIntPtr e); [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c); [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[N86b]::SetProcessDPIAware()|Out-Null
function Shot($n){ $b=New-Object Drawing.Bitmap 1600,900; $g=[Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(100,100,0,0,$b.Size); $b.Save("$Out\$n"); $g.Dispose(); $b.Dispose() }
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false; $psi.EnvironmentVariables["SM2_WINRECT"]="100,100,1600,900"
$p=[Diagnostics.Process]::Start($psi); Start-Sleep 35; $p.Refresh(); $before=[int]($p.PrivateMemorySize64/1MB)
$cur=[Windows.Forms.Cursor]::Position
[N86b]::keybd_event(0x87,0,0,[UIntPtr]::Zero); [N86b]::keybd_event(0x87,0,2,[UIntPtr]::Zero); [N86b]::ShowWindow($p.MainWindowHandle,9)|Out-Null; [N86b]::SetForegroundWindow($p.MainWindowHandle)|Out-Null; Start-Sleep -m 600
$fg=([N86b]::GetForegroundWindow() -eq $p.MainWindowHandle); Shot r86_level_deferred.png; $clicked=$false
if($fg){ [N86b]::SetCursorPos($BtnX,$BtnY); Start-Sleep -m 300; if([N86b]::GetForegroundWindow() -eq $p.MainWindowHandle){ [N86b]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -m 80; [N86b]::mouse_event(4,0,0,0,[UIntPtr]::Zero); $clicked=$true } }
[N86b]::SetCursorPos($cur.X,$cur.Y)
[long]$peak=0; for($i=0;$i -lt 60;$i++){ Start-Sleep 1; $p.Refresh(); $peak=[Math]::Max($peak,$p.PrivateMemorySize64) }
$fg2=$false; [N86b]::keybd_event(0x87,0,0,[UIntPtr]::Zero); [N86b]::keybd_event(0x87,0,2,[UIntPtr]::Zero); [N86b]::SetForegroundWindow($p.MainWindowHandle)|Out-Null; Start-Sleep -m 600; Shot r86_level_after_click.png
"fg $fg clicked $clicked before_click_private_mb $before peak_after_click_mb $([int]($peak/1MB)) end_mb $([int]($p.PrivateMemorySize64/1MB))" | Tee-Object -Append "$Out\click.txt"
$p.CloseMainWindow()|Out-Null; Start-Sleep 5; if(-not $p.HasExited){$p.Kill()}; Copy-Item $bak $sj -Force

