# R83 (g): drive the real ForgeLens window with real Win32 SendInput (mouse + keyboard) and check each effect through the
# SM2_INPUTPROBE line (camera, hover, selection, visible parts, undo depth). Brings the viewer to the foreground (input
# needs focus): run only while the PC is idle. Screen grabs (GPU viewport included) via CopyFromScreen.
param([string]$Exe,[string]$Select="tpl/cc_calgar.tpl/cc_calgar.tpl",[string]$Out,[string]$Rect="0,0,1600,900")
New-Item -ItemType Directory -Force $Out | Out-Null
$vp="$env:LOCALAPPDATA\ForgeLens\viewport_r79.json"; $keep="$Out\viewport_r79.before.json"; if(Test-Path $vp){ Copy-Item $vp $keep -Force }
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class SI {
  [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct KI { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] public struct IN { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MI mi; [FieldOffset(8)] public KI ki; }
  [DllImport("user32.dll")] static extern uint SendInput(uint n, IN[] i, int size);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  static uint S(IN x){ return SendInput(1, new[]{x}, Marshal.SizeOf(typeof(IN))); }
  public static uint Move(int x,int y){ int vx=GetSystemMetrics(76), vy=GetSystemMetrics(77), vw=GetSystemMetrics(78), vh=GetSystemMetrics(79);
    var i=new IN{type=0}; i.mi.dx=(int)Math.Round((x-vx)*65535.0/(vw-1)); i.mi.dy=(int)Math.Round((y-vy)*65535.0/(vh-1)); i.mi.flags=0x0001|0x8000|0x4000; return S(i); }
  public static uint Btn(uint f){ var i=new IN{type=0}; i.mi.flags=f; return S(i); }
  public static uint Wheel(int d){ var i=new IN{type=0}; i.mi.flags=0x0800; i.mi.data=(uint)d; return S(i); }
  [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
  // real keyboards always send a scan code with the virtual key (IME / TSF need it)
  public static uint Key(ushort vk,bool up){ var i=new IN{type=1}; i.ki.vk=vk; i.ki.scan=(ushort)MapVirtualKey(vk,0); i.ki.flags=up?2u:0u; return S(i); }
}
"@
[SI]::SetProcessDPIAware() | Out-Null
$probe="$Out\probe.txt"; Remove-Item $probe -EA 0; $log="$Out\sendinput_log.txt"; Remove-Item $log -EA 0
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
$psi.EnvironmentVariables["SM2_SELECT"]=$Select; $psi.EnvironmentVariables["SM2_WINSHOT"]="$Out\start.png"; $psi.EnvironmentVariables["SM2_WINRECT"]=$Rect; $psi.EnvironmentVariables["SM2_INPUTPROBE"]=$probe
$p=[Diagnostics.Process]::Start($psi)
$t=[Diagnostics.Stopwatch]::StartNew(); while(-not ((Get-Content "$Out\start.png.txt" -Raw -EA 0) -match 'settings file|note_runs') -and $t.Elapsed.TotalSeconds -lt 240){ Start-Sleep 1 }
Start-Sleep 3
function P { $l=(Get-Content $probe -Raw -EA 0); $h=@{}; $a=$l.Trim() -split ' '; for($i=0;$i+1 -lt $a.Count;$i+=2){ $h[$a[$i]]=$a[$i+1] }; $h }
function L([string]$s){ $s | Tee-Object -FilePath $log -Append }
# A harmless F24 tap lets SetForegroundWindow through (the caller sent the last input).
function Focus { $p.Refresh(); [SI]::Key(0x87,$false)|Out-Null; [SI]::Key(0x87,$true)|Out-Null; [SI]::ShowWindow($p.MainWindowHandle,9)|Out-Null; [SI]::SetForegroundWindow($p.MainWindowHandle)|Out-Null; Start-Sleep -Milliseconds 400 }
# Safety (R83 incident 18:12 KST: another worker's BLTest editor took the foreground mid-run and the remaining keys /
# clicks went to it): before every step the viewer must still be the foreground window, else the run stops at once.
function Guard { $p.Refresh(); if([SI]::GetForegroundWindow() -ne $p.MainWindowHandle){ L "ABORT: viewer lost the foreground (another window took focus); no more input sent"; if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null }; if(Test-Path $keep){ Copy-Item $keep $vp -Force }; exit 2 } }
function XY([string]$s){ $a=$s -split ','; ,@([int]$a[0],[int]$a[1]) }
function Grab([string]$name){ $p.Refresh(); $r=(XY (P).vpc); $w=760;$h=520; $bmp=New-Object Drawing.Bitmap $w,$h; $g=[Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r[0]-$w/2,$r[1]-$h/2,0,0,$bmp.Size); $bmp.Save("$Out\si_$name.png"); $g.Dispose(); $bmp.Dispose() }
function Line([string]$tag){ Guard; $s=P; L ("{0,-22} yaw {1} pitch {2} dist {3} pivot {4} hover {5} sel {6} visible {7} undo {8} redo {9} help {10} focus {11}" -f $tag,$s.yaw,$s.pitch,$s.dist,$s.pivot,$s.hover,$s.sel,$s.visible,$s.undo,$s.redo,$s.help,$s.focus); $s }
$VK=@{ALT=0x12;CTRL=0x11;SHIFT=0x10;F=0x46;Z=0x5A;Y=0x59;D1=0x31;F1=0x70}
Focus
$s0=Line "start"; L "  hud $($s0.hud)"; $c=XY $s0.vpc; L "viewport centre screen px $($s0.vpc)  fg-is-viewer $([SI]::GetForegroundWindow() -eq $p.MainWindowHandle)"
Grab "0_start"
# hover
[SI]::Move($c[0]-40,$c[1]-60)|Out-Null; Start-Sleep -Milliseconds 150; [SI]::Move($c[0],$c[1]-30)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "hover centre"; Grab "1_hover"
# click select
[SI]::Btn(2)|Out-Null; Start-Sleep -Milliseconds 60; [SI]::Btn(4)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "LMB click"; Grab "2_selected"
# undo / redo of selection: click empty corner (deselect), Ctrl+Z (select back), Ctrl+Y (deselect again)
$em=XY (P).vpempty; [SI]::Move($em[0],$em[1])|Out-Null; Start-Sleep -Milliseconds 300; [SI]::Btn(2)|Out-Null; Start-Sleep -Milliseconds 60; [SI]::Btn(4)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "LMB empty (deselect)"
[SI]::Key($VK.CTRL,$false)|Out-Null; [SI]::Key($VK.Z,$false)|Out-Null; [SI]::Key($VK.Z,$true)|Out-Null; [SI]::Key($VK.CTRL,$true)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "Ctrl+Z"
[SI]::Key($VK.CTRL,$false)|Out-Null; [SI]::Key($VK.Y,$false)|Out-Null; [SI]::Key($VK.Y,$true)|Out-Null; [SI]::Key($VK.CTRL,$true)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "Ctrl+Y"
[SI]::Key($VK.CTRL,$false)|Out-Null; [SI]::Key($VK.Z,$false)|Out-Null; [SI]::Key($VK.Z,$true)|Out-Null; [SI]::Key($VK.CTRL,$true)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "Ctrl+Z (reselect)"
# F frame selection
[SI]::Key($VK.F,$false)|Out-Null; [SI]::Key($VK.F,$true)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "F"; L "  lastkey $($s.lastkey)"; Grab "3_frame"
# wheel 3 notches toward the cursor
for($i=0;$i -lt 3;$i++){ [SI]::Wheel(120)|Out-Null; Start-Sleep -Milliseconds 120 }; Start-Sleep -Milliseconds 600; $s=Line "wheel +3"; Grab "4_wheel"
# Alt+LMB orbit drag 120 px right (start at the centre)
[SI]::Move($c[0],$c[1]-30)|Out-Null; Start-Sleep -Milliseconds 150
[SI]::Key($VK.ALT,$false)|Out-Null; [SI]::Btn(2)|Out-Null; for($i=1;$i -le 12;$i++){ [SI]::Move($c[0]+10*$i,$c[1]-30)|Out-Null; Start-Sleep -Milliseconds 25 }; [SI]::Btn(4)|Out-Null; [SI]::Key($VK.ALT,$true)|Out-Null; Start-Sleep -Milliseconds 900; $s=Line "Alt+LMB drag +120px"; Grab "5_orbit"
# Alt+RMB dolly drag 80 px right
[SI]::Move($c[0],$c[1])|Out-Null; Start-Sleep -Milliseconds 100; [SI]::Key($VK.ALT,$false)|Out-Null; [SI]::Btn(8)|Out-Null; for($i=1;$i -le 8;$i++){ [SI]::Move($c[0]+10*$i,$c[1])|Out-Null; Start-Sleep -Milliseconds 25 }; [SI]::Btn(16)|Out-Null; [SI]::Key($VK.ALT,$true)|Out-Null; Start-Sleep -Milliseconds 800; $s=Line "Alt+RMB drag +80px"; Grab "6_dolly"
# bookmark: Ctrl+1, wheel away, 1 recalls
[SI]::Key($VK.CTRL,$false)|Out-Null; [SI]::Key($VK.D1,$false)|Out-Null; [SI]::Key($VK.D1,$true)|Out-Null; [SI]::Key($VK.CTRL,$true)|Out-Null; Start-Sleep -Milliseconds 400; $bm=Line "Ctrl+1 saved"
for($i=0;$i -lt 4;$i++){ [SI]::Wheel(-120)|Out-Null; Start-Sleep -Milliseconds 120 }; Start-Sleep -Milliseconds 500; $s=Line "wheel -4 (away)"
[SI]::Key($VK.D1,$false)|Out-Null; [SI]::Key($VK.D1,$true)|Out-Null; Start-Sleep -Milliseconds 700; $s=Line "1 recalled"; Grab "7_bookmark"
L "bookmark match: dist $($bm.dist) vs $($s.dist), yaw $($bm.yaw) vs $($s.yaw), pivot $($bm.pivot) vs $($s.pivot)"
# eye icon click in the outliner hides the row, Ctrl+Z shows it again
$s=P; L "eye0 $($s.eye0) $($s.eye0val)"
if($s.eye0 -ne '-'){ $e=XY $s.eye0; [SI]::Move($e[0],$e[1])|Out-Null; Start-Sleep -Milliseconds 300; [SI]::Btn(2)|Out-Null; Start-Sleep -Milliseconds 60; [SI]::Btn(4)|Out-Null; Start-Sleep -Milliseconds 800; $s=Line "eye click"; L "eye0val now $((P).eye0val)"; Grab "8_eye_hidden"
  [SI]::Move($c[0],$c[1])|Out-Null; Start-Sleep -Milliseconds 200
  Focus; [SI]::Key($VK.CTRL,$false)|Out-Null; [SI]::Key($VK.Z,$false)|Out-Null; [SI]::Key($VK.Z,$true)|Out-Null; [SI]::Key($VK.CTRL,$true)|Out-Null; Start-Sleep -Milliseconds 800; $s=Line "Ctrl+Z after eye"; L "eye0val now $((P).eye0val)" }
# H toggles the HUD
[SI]::Key(0x48,$false)|Out-Null; [SI]::Key(0x48,$true)|Out-Null; Start-Sleep -Milliseconds 600; $s=Line "H"; L "  lastkey $($s.lastkey) hud $($s.hud)"; Grab "8b_hud_off"; [SI]::Key(0x48,$false)|Out-Null; [SI]::Key(0x48,$true)|Out-Null; Start-Sleep -Milliseconds 400
# F1 help
[SI]::Key($VK.F1,$false)|Out-Null; [SI]::Key($VK.F1,$true)|Out-Null; Start-Sleep -Milliseconds 600; $s=Line "F1"; Grab "9_help"; [SI]::Key($VK.F1,$false)|Out-Null; [SI]::Key($VK.F1,$true)|Out-Null; Start-Sleep -Milliseconds 400; $s=Line "F1 again"
$p.Refresh(); if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null; Start-Sleep 4; if(-not $p.HasExited){$p.Kill()} }
if(Test-Path $keep){ Copy-Item $keep $vp -Force }

