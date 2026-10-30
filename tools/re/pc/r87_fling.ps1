# R87: real-input orbit FLING test (inertia after release) + wheel zoom, real SendInput into the real window, SM2_INPUTPROBE readout.
# Header = r83_sendinput.ps1 (foreground guard, viewport json restored, cursor restored).
param([string]$Exe,[string]$Select="tpl/cc_calgar.tpl/cc_calgar.tpl",[string]$Out,[string]$Rect="0,0,1600,900")
Add-Type -AssemblyName System.Windows.Forms
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
function Focus { if(Get-Command InputBlockOn -EA 0){ InputBlockOn }; $p.Refresh(); [SI]::Key(0x87,$false)|Out-Null; [SI]::Key(0x87,$true)|Out-Null; [SI]::ShowWindow($p.MainWindowHandle,9)|Out-Null; [SI]::SetForegroundWindow($p.MainWindowHandle)|Out-Null; Start-Sleep -Milliseconds 400 }
# Safety (R83 incident 18:12 KST: another worker's BLTest editor took the foreground mid-run and the remaining keys /
# clicks went to it): before every step the viewer must still be the foreground window, else the run stops at once.
function Guard { $p.Refresh(); for($gi=0;$gi -lt 4 -and [SI]::GetForegroundWindow() -ne $p.MainWindowHandle;$gi++){ if($gi -ge 1 -and (Get-Command IBMinimize -EA 0)){ IBMinimize ([SI]::GetForegroundWindow()) }; Focus; $p.Refresh() }; if([SI]::GetForegroundWindow() -ne $p.MainWindowHandle){ L "ABORT: viewer lost the foreground (another window took focus); no more input sent"; if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null }; if(Test-Path $keep){ Copy-Item $keep $vp -Force }; exit 2 } }
# t220u: a covering (topmost) window over the viewport eats real clicks; log it and minimise it (never UnrealEditor).
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public static class HW{public struct PT{public int x,y;}[DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(PT p);[DllImport("user32.dll")]public static extern IntPtr GetAncestor(IntPtr h,uint f);[DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);[DllImport("user32.dll")]public static extern int GetClassName(IntPtr h,StringBuilder s,int n);}
"@
function HitCheck([int]$x,[int]$y){ for($k=0;$k -lt 3;$k++){ $pt=New-Object HW+PT; $pt.x=$x;$pt.y=$y; $h=[HW]::GetAncestor([HW]::WindowFromPoint($pt),2); $id=0;[HW]::GetWindowThreadProcessId($h,[ref]$id)|Out-Null; if($id -eq $p.Id){ L "  hit-test $x,$y = viewer"; return }
  $sb=New-Object Text.StringBuilder 256;[HW]::GetClassName($h,$sb,256)|Out-Null; $pn=(Get-Process -Id $id -EA 0).Name; L "  hit-test $x,$y covered by pid $id $pn class $sb hwnd $h"
  if($pn -ne 'UnrealEditor' -and (Get-Command IBMinimize -EA 0)){ IBMinimize $h; Start-Sleep -Milliseconds 300 } else { Focus } } }
function XY([string]$s){ $a=$s -split ','; ,@([int]$a[0],[int]$a[1]) }
function Grab([string]$name){ $p.Refresh(); $r=(XY (P).vpc); $w=760;$h=520; $bmp=New-Object Drawing.Bitmap $w,$h; $g=[Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r[0]-$w/2,$r[1]-$h/2,0,0,$bmp.Size); $bmp.Save("$Out\si_$name.png"); $g.Dispose(); $bmp.Dispose() }
function Line([string]$tag){ Guard; $s=P; L ("{0,-22} yaw {1} pitch {2} dist {3} pivot {4} hover {5} sel {6} visible {7} undo {8} redo {9} help {10} focus {11}" -f $tag,$s.yaw,$s.pitch,$s.dist,$s.pivot,$s.hover,$s.sel,$s.visible,$s.undo,$s.redo,$s.help,$s.focus); $s }
$VK=@{ALT=0x12;CTRL=0x11;SHIFT=0x10;F=0x46;Z=0x5A;Y=0x59;D1=0x31;F1=0x70}
Focus
$cur=[System.Windows.Forms.Cursor]::Position
function Samp([string]$tag){ $s=P; L ("{0,-26} t {1} yaw {2} pitch {3} dist {4}" -f $tag,$s.t,$s.yaw,$s.pitch,$s.dist); $s }
$s0=Line "start"; $c=XY $s0.vpc; HitCheck $c[0] $c[1]; [SI]::Btn(4)|Out-Null; [SI]::Btn(0x10)|Out-Null; [SI]::Key(0x1B,$false)|Out-Null; [SI]::Key(0x1B,$true)|Out-Null; Start-Sleep -Milliseconds 300; $s0=Line "after button reset"
foreach($mod in @('none','alt')){
  Guard; [SI]::Move($c[0]-150,$c[1]-30)|Out-Null; Start-Sleep -Milliseconds 300
  if($mod -eq 'alt'){ [SI]::Key($VK.ALT,$false)|Out-Null }
  $a=Samp "$mod before"
  [SI]::Btn(2)|Out-Null; for($i=1;$i -le 10;$i++){ [SI]::Move($c[0]-150+30*$i,$c[1]-30)|Out-Null; Start-Sleep -Milliseconds 8 }; [SI]::Btn(4)|Out-Null
  if($mod -eq 'alt'){ [SI]::Key($VK.ALT,$true)|Out-Null }
  $r=Samp "$mod at release (+300px/80ms)"; Start-Sleep -Milliseconds 150; $b1=Samp "$mod +150ms"; Start-Sleep -Milliseconds 500; $b2=Samp "$mod +650ms"; Start-Sleep -Milliseconds 1500; $b3=Samp "$mod +2150ms"; Start-Sleep -Milliseconds 1000; $b4=Samp "$mod +3150ms"
  L ("  {0}: yaw at release {1:0.000}, coast after release {2:0.000} rad (0-650ms {3:0.000}, 650-2150ms {4:0.000}, 2150-3150ms {5:0.000})" -f $mod,([double]$r.yaw-[double]$a.yaw),([double]$b4.yaw-[double]$r.yaw),([double]$b2.yaw-[double]$r.yaw),([double]$b3.yaw-[double]$b2.yaw),([double]$b4.yaw-[double]$b3.yaw))
  Grab "fling_$mod"
}
Guard; [SI]::Move($c[0],$c[1])|Out-Null; Start-Sleep -Milliseconds 200; $w0=Samp "wheel before"
for($i=0;$i -lt 3;$i++){ [SI]::Wheel(120)|Out-Null; Start-Sleep -Milliseconds 120 }; Start-Sleep -Milliseconds 600; $w1=Samp "wheel +3"
for($i=0;$i -lt 3;$i++){ [SI]::Wheel(-120)|Out-Null; Start-Sleep -Milliseconds 120 }; Start-Sleep -Milliseconds 600; $w2=Samp "wheel -3"
L ("  wheel in dist {0} -> {1}, out -> {2}" -f $w0.dist,$w1.dist,$w2.dist)
$p.CloseMainWindow()|Out-Null; Start-Sleep 3; if(-not $p.HasExited){ L "viewer still open after CloseMainWindow" }
if(Test-Path $keep){ Copy-Item $keep $vp -Force }
[System.Windows.Forms.Cursor]::Position=$cur; L "cursor restored to $($cur.X),$($cur.Y)"
