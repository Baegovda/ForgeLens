# R84 (t201u): picking an animation plays it at once (no Space). Real SendInput into the real ForgeLens window; the
# SM2_INPUTPROBE line (atime / playing / clip) proves it. Run only while the PC is idle (takes mouse + keyboard).
param([string]$Exe,[string]$Select="tpl/cc_calgar.tpl/cc_calgar.tpl",[string]$Out,[string]$Rect="0,0,3200,1900",[int]$Down=2,[int]$RowPx=90)
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
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public static uint FgPid(){ uint p; GetWindowThreadProcessId(GetForegroundWindow(), out p); return p; }
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
function L([string]$s){ $s | Tee-Object -FilePath $log -Append }
function P { $l=""; for($r=0;$r -lt 20 -and $l.Trim().Length -eq 0;$r++){ $l="$(Get-Content $probe -Raw -EA 0)"; if($l.Trim().Length -eq 0){ Start-Sleep -Milliseconds 30 } }; $h=@{}; $a=$l.Trim() -split ' '; for($i=0;$i+1 -lt $a.Count;$i+=2){ $h[$a[$i]]=$a[$i+1] }; $h }
function XY([string]$s){ $a=$s -split ','; ,@([int][double]$a[0],[int][double]$a[1]) }
function Focus { $p.Refresh(); [SI]::Key(0x87,$false)|Out-Null; [SI]::Key(0x87,$true)|Out-Null; [SI]::ShowWindow($p.MainWindowHandle,9)|Out-Null; [SI]::SetForegroundWindow($p.MainWindowHandle)|Out-Null; Start-Sleep -Milliseconds 400 }
function Guard { $p.Refresh(); if([SI]::FgPid() -ne $p.Id){ L "  foreground pid $([SI]::FgPid()) hwnd $([SI]::GetForegroundWindow())"; L "ABORT: viewer lost the foreground (another window took focus); no more input sent"; if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null }; if(Test-Path $keep){ Copy-Item $keep $vp -Force }; exit 2 } }
function Grab([string]$name){ $r=(XY (P).vpc); $w=1000;$h=900; $bmp=New-Object Drawing.Bitmap $w,$h; $g=[Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r[0]-$w/2,$r[1]-$h/2,0,0,$bmp.Size); $bmp.Save("$Out\$name.png"); $g.Dispose(); $bmp.Dispose() }
function St([string]$tag){ Guard; $s=P; L ("{0,-28} clip {1} atime {2}/{8} playing {3} btn {4} animsel {5}/{6} phys {7}" -f $tag,$s.clip,$s.atime,$s.playing,$s.playbtn,$s.animsel,$s.animn,$s.phys,$s.adur); $s }
function K([int]$vk){ [SI]::Key($vk,$false)|Out-Null; Start-Sleep -Milliseconds 40; [SI]::Key($vk,$true)|Out-Null }
Add-Type -AssemblyName System.Windows.Forms
$cur0=[Windows.Forms.Cursor]::Position
$probe="$Out\probe.txt"; Remove-Item $probe,"$Out\start.png","$Out\start.png.txt" -EA 0; $log="$Out\anim_log.txt"; Remove-Item $log -EA 0
$psi=New-Object Diagnostics.ProcessStartInfo $Exe; $psi.UseShellExecute=$false
$psi.EnvironmentVariables["SM2_SELECT"]=$Select; $psi.EnvironmentVariables["SM2_WINSHOT"]="$Out\start.png"; $psi.EnvironmentVariables["SM2_WINRECT"]=$Rect; $psi.EnvironmentVariables["SM2_INPUTPROBE"]=$probe
$p=[Diagnostics.Process]::Start($psi)
$t=[Diagnostics.Stopwatch]::StartNew(); while(-not ((Get-Content "$Out\start.png.txt" -Raw -EA 0) -match 'settings file|note_runs') -and $t.Elapsed.TotalSeconds -lt 240){ Start-Sleep 1 }
# wait for the clip list
$t.Restart(); while($t.Elapsed.TotalSeconds -lt 120){ $s=P; if([int]$s.animn -gt 2){ break }; Start-Sleep 1 }
Start-Sleep 2
try {
Focus
$s=St "loaded (no pick)"; Start-Sleep 1.5; $s2=St "loaded +1.5s"; L "  auto-play on model load: $(if($s2.atime -ne $s.atime -or $s2.playing -eq '1'){'YES (bad)'}else{'no (ok)'})"
# 1) mouse: click the ANIM box (opens the drop-down), then click the 3rd row below it
$a=XY $s.animbox; [SI]::Move($a[0],$a[1])|Out-Null; Start-Sleep -Milliseconds 200; [SI]::Btn(2)|Out-Null; Start-Sleep -Milliseconds 60; [SI]::Btn(4)|Out-Null; Start-Sleep -Milliseconds 800
# keyboard inside the open drop-down: Down x2 + Enter (= a keyboard pick)
for($k=0;$k -lt $Down;$k++){ K 0x28; Start-Sleep -Milliseconds 120 }; K 0x0D
$t.Restart(); while($t.Elapsed.TotalSeconds -lt 30){ $s=P; if($s.clip -ne '-'){ break }; Start-Sleep -Milliseconds 200 }
Start-Sleep -Milliseconds 400; $a1=St "picked (kbd in dropdown)"; Grab "pose_a"; Start-Sleep -Milliseconds 900; $a2=St "+0.9s, no Space"; Grab "pose_b"
L ("  advance without Space: {0:0.000}s" -f ([double]$a2.atime-[double]$a1.atime))
K 0x20; Start-Sleep -Milliseconds 500; $b1=St "Space (pause)"; Start-Sleep 1; $b2=St "+1s paused"; L ("  paused delta {0:0.000}s" -f ([double]$b2.atime-[double]$b1.atime))
K 0x20; Start-Sleep 1; $c1=St "Space (resume) +1s"; L ("  resumed delta {0:0.000}s" -f ([double]$c1.atime-[double]$b2.atime))
# 2) switch while playing, keyboard: Escape closes nothing harmful; the box keeps focus after the pick; Down = next clip
$prev=(P).clip; K 0x28
$t.Restart(); while($t.Elapsed.TotalSeconds -lt 30){ $s=P; if($s.clip -ne $prev -and $s.clip -ne '-'){ break }; Start-Sleep -Milliseconds 100 }
$d1=St "Down while playing"; Start-Sleep 1; $d2=St "+1s"; L ("  new clip {0} started at {1}, advance {2:0.000}s" -f $d1.clip,$d1.atime,([double]$d2.atime-[double]$d1.atime)); Grab "pose_c"
# 3) mouse pick while playing: open the box, click the row ~3 rows below
$a=XY (P).animbox; $prev=(P).clip
[SI]::Move($a[0],$a[1])|Out-Null; Start-Sleep -Milliseconds 200; [SI]::Btn(2)|Out-Null; Start-Sleep -Milliseconds 60; [SI]::Btn(4)|Out-Null; Start-Sleep -Milliseconds 900
[SI]::Move($a[0],$a[1]+$RowPx)|Out-Null; Start-Sleep -Milliseconds 300; [SI]::Btn(2)|Out-Null; Start-Sleep -Milliseconds 60; [SI]::Btn(4)|Out-Null
$t.Restart(); while($t.Elapsed.TotalSeconds -lt 30){ $s=P; if($s.clip -ne $prev){ break }; Start-Sleep -Milliseconds 100 }
Start-Sleep -Milliseconds 300; $e1=St "mouse row pick while playing"; Start-Sleep 1; $e2=St "+1s"; L ("  new clip {0} at {1}, advance {2:0.000}s" -f $e1.clip,$e1.atime,([double]$e2.atime-[double]$e1.atime)); Grab "pose_d"
# tick rate (wall vs anim time) over 3 s
$w0=P; Start-Sleep 3; $w1=P; L ("  3 s wall: anim advance {0:0.000}s (loops at {1}s; per-tick dt is clamped to 0.1 s)" -f ([double]$w1.atime-[double]$w0.atime),$w1.adur)} finally {
 if(-not $p.HasExited){ $p.CloseMainWindow()|Out-Null }
 if(Test-Path $keep){ Copy-Item $keep $vp -Force }
 [SI]::Move($cur0.X,$cur0.Y)|Out-Null; L "cursor restored to $($cur0.X),$($cur0.Y)"
}
