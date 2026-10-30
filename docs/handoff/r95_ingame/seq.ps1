param($Out,$Seq)
InputBlockOn
Add-Type 'using System;using System.Runtime.InteropServices;public class E{[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);[DllImport("user32.dll")]public static extern void mouse_event(int f,int x,int y,int d,int e);[DllImport("user32.dll")]public static extern void keybd_event(byte k,byte s,int f,int e);[DllImport("user32.dll")]public static extern uint MapVirtualKey(uint c,uint t);}'
Add-Type 'using System;using System.Runtime.InteropServices;public class FG{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);[DllImport("user32.dll")]public static extern void SwitchToThisWindow(IntPtr h,bool a);[DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]public static extern int GetWindowThreadProcessId(IntPtr h,IntPtr p);[DllImport("kernel32.dll")]public static extern int GetCurrentThreadId();[DllImport("user32.dll")]public static extern bool AttachThreadInput(int a,int b,bool c);}'; $gp=Get-Process | ?{$_.MainWindowTitle -like '*Space Marine 2*'}|select -First 1; [E]::keybd_event(0x12,0x38,0,0); [E]::keybd_event(0x12,0x38,2,0); $ft=[FG]::GetWindowThreadProcessId([FG]::GetForegroundWindow(),[IntPtr]::Zero); $me=[FG]::GetCurrentThreadId(); [FG]::AttachThreadInput($me,$ft,$true)|Out-Null; [FG]::SetForegroundWindow($gp.MainWindowHandle)|Out-Null; [FG]::SwitchToThisWindow($gp.MainWindowHandle,$true); [FG]::AttachThreadInput($me,$ft,$false)|Out-Null; Start-Sleep -m 500; [E]::SetCursorPos(1000,15)|Out-Null; Start-Sleep -m 200; [E]::mouse_event(2,0,0,0,0); Start-Sleep -m 80; [E]::mouse_event(4,0,0,0,0); Start-Sleep -m 300; [E]::SetCursorPos(1000,600)|Out-Null; Start-Sleep -m 200
function L($dx,$dy){for($i=0;$i -lt 40;$i++){[E]::mouse_event(1,[int]($dx/40),[int]($dy/40),0,0);Start-Sleep -m 20}}
"fg=$([FG]::GetWindowThreadProcessId([FG]::GetForegroundWindow(),[IntPtr]::Zero)) game=$([FG]::GetWindowThreadProcessId($gp.MainWindowHandle,[IntPtr]::Zero))" | Out-File "$Out\fg.txt"
foreach($st in ($Seq -split ';')){ $a=$st -split ':'
 if($a[0] -eq 'rot'){ $dx=[int]$a[1]; $dy=[int]$a[2]; [E]::mouse_event(8,0,0,0,0); for($i=0;$i -lt 40;$i++){[E]::mouse_event(1,[int]($dx/40),[int]($dy/40),0,0);Start-Sleep -m 20}; [E]::mouse_event(16,0,0,0,0); Start-Sleep -m 300 }
 if($a[0] -eq 'look'){ L ([int]$a[1]) ([int]$a[2]); Start-Sleep -m 300 }
 if($a[0] -eq 'sc'){ $sc=[byte][int]$a[1]; [E]::keybd_event(0,$sc,8,0); Start-Sleep -m ([int]$a[2]); [E]::keybd_event(0,$sc,10,0); Start-Sleep -m 300 }
 if($a[0] -eq 'hold'){ $vk=[byte][int]$a[1]; $sc=[byte][E]::MapVirtualKey($vk,0); [E]::keybd_event($vk,$sc,0,0); Start-Sleep -m ([int]$a[2]); [E]::keybd_event($vk,$sc,2,0); Start-Sleep -m 300 }
}






