param([string]$act='shot',[int]$x=0,[int]$y=0,[string]$name='s',[int]$vk=0)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;
public class H{
[DllImport("user32.dll")]public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern bool GetCursorPos(out P p);
[DllImport("user32.dll")]public static extern void mouse_event(int f,int x,int y,int d,int e);
[DllImport("user32.dll")]public static extern void keybd_event(byte k,byte s,int f,int e);
[DllImport("user32.dll")]public static extern uint MapVirtualKey(uint c,uint t);
[DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);[DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]public static extern int GetWindowThreadProcessId(IntPtr h,IntPtr p);[DllImport("kernel32.dll")]public static extern int GetCurrentThreadId();[DllImport("user32.dll")]public static extern bool AttachThreadInput(int a,int b,bool c);[DllImport("user32.dll")]public static extern void SwitchToThisWindow(IntPtr h,bool a);public struct R{public int L,T,Ri,B;} public struct P{public int X,Y;}}
"@
[H]::SetProcessDpiAwarenessContext([IntPtr]-4)|Out-Null
$p=Get-Process | ?{$_.MainWindowTitle -like '*Space Marine 2 CLIENT*'} | select -First 1
$r=New-Object H+R; [H]::GetWindowRect($p.MainWindowHandle,[ref]$r)|Out-Null
[H]::SetWindowPos($p.MainWindowHandle,[IntPtr]-1,0,0,0,0,0x13)|Out-Null; $w=$r.Ri-$r.L; $h=$r.B-$r.T
if($act -ne 'shot'){ $ft=[H]::GetWindowThreadProcessId([H]::GetForegroundWindow(),[IntPtr]::Zero); $me=[H]::GetCurrentThreadId(); [H]::AttachThreadInput($me,$ft,$true)|Out-Null; [H]::SetForegroundWindow($p.MainWindowHandle)|Out-Null; [H]::SwitchToThisWindow($p.MainWindowHandle,$true); [H]::AttachThreadInput($me,$ft,$false)|Out-Null; Start-Sleep -m 400; "fg=$([H]::GetWindowThreadProcessId([H]::GetForegroundWindow(),[IntPtr]::Zero)) game=$([H]::GetWindowThreadProcessId($p.MainWindowHandle,[IntPtr]::Zero))" | Out-File "$PSScriptRoot\fg.txt" }
if($act -eq 'click'){ $o=New-Object H+P;[H]::GetCursorPos([ref]$o)|Out-Null; $ax=$r.L+[int]($x*$w/1000); $ay=$r.T+[int]($y*$h/1000); [H]::SetCursorPos($ax,$ay)|Out-Null; Start-Sleep -m 200; [H]::SetCursorPos($ax+1,$ay)|Out-Null; Start-Sleep -m 200; [H]::mouse_event(2,0,0,0,0); Start-Sleep -m 90; [H]::mouse_event(4,0,0,0,0); Start-Sleep -m 300; [H]::SetCursorPos($o.X,$o.Y)|Out-Null }
if($act -eq 'altenter'){ [H]::keybd_event(0x12,0x38,0,0); Start-Sleep -m 60; [H]::keybd_event(0x0D,0x1C,0,0); Start-Sleep -m 80; [H]::keybd_event(0x0D,0x1C,2,0); [H]::keybd_event(0x12,0x38,2,0); Start-Sleep 4; [H]::GetWindowRect($p.MainWindowHandle,[ref]$r)|Out-Null; $w=$r.Ri-$r.L; $h=$r.B-$r.T }
if($act -eq 'key'){ $sc=[byte][H]::MapVirtualKey($vk,0); [H]::keybd_event($vk,$sc,0,0); Start-Sleep -m 90; [H]::keybd_event($vk,$sc,2,0); Start-Sleep -m 200 }
Start-Sleep -m 600
$b=New-Object Drawing.Bitmap $w,$h; $g=[Drawing.Graphics]::FromImage($b); $g.CopyFromScreen($r.L,$r.T,0,0,(New-Object Drawing.Size $w,$h))
(New-Object Drawing.Bitmap $b,1280,([int](1280*$h/$w))).Save("$PSScriptRoot\$name.png")
"rect $($r.L),$($r.T),$w x $h pid $($p.Id)"


