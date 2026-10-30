param($Out,$Pid2)
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public static class T{[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);[DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);public struct R{public int L,T,Ri,B;}
[DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern void mouse_event(uint f,int x,int y,uint d,UIntPtr e);[DllImport("user32.dll")]public static extern IntPtr FindWindow(string c,string t);
[DllImport("user32.dll")]public static extern bool SetProcessDPIAware();[DllImport("user32.dll")]public static extern void keybd_event(byte k,byte s,uint f,UIntPtr e);}
"@
function Shot($h,$name){ $r=New-Object T+R; [T]::GetWindowRect($h,[ref]$r)|out-null; $b=New-Object Drawing.Bitmap(($r.Ri-$r.L),($r.B-$r.T)); $g=[Drawing.Graphics]::FromImage($b); $dc=$g.GetHdc(); [T]::PrintWindow($h,$dc,2)|out-null; $g.ReleaseHdc($dc); $g.Dispose(); $b.Save("$Out\$name"); $r }
function Click($x,$y){ [T]::SetCursorPos($x,$y)|out-null; Start-Sleep -m 120; [T]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -m 60; [T]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Start-Sleep -m 400 }
[T]::SetProcessDPIAware()|out-null; $log="$Out\gui_log.txt"; $prevFg=[T]::GetForegroundWindow(); $cur=[Windows.Forms.Cursor]::Position
$h=(Get-Process -Id $Pid2).MainWindowHandle; $TOP=[IntPtr](-1); $NOTOP=[IntPtr](-2)
try {
 InputBlockOn
 [T]::SetWindowPos($h,$TOP,0,0,0,0,3)|out-null; [T]::SetForegroundWindow($h)|out-null; Start-Sleep 1
 $r=New-Object T+R; [T]::GetWindowRect($h,[ref]$r)|out-null; "main $($r.L) $($r.T)" | Add-Content $log
 Click ($r.L+1260) ($r.T+410); Shot $h "gui_0_after_click.png" | out-null
 $s=[IntPtr]::Zero; for($k=0;$k -lt 25 -and $s -eq [IntPtr]::Zero;$k++){ Start-Sleep -m 200; $s=[T]::FindWindow($null,"Settings") }; "settings hwnd $s" | Add-Content $log
 if($s -ne [IntPtr]::Zero){
  [T]::SetWindowPos($s,$TOP,0,0,0,0,3)|out-null; $sr=Shot $s 'gui_1_opened.png'; "settings rect $($sr.L) $($sr.T) $($sr.Ri) $($sr.B)" | Add-Content $log
  Click ($sr.L+229) ($sr.T+121); Shot $s 'gui_2_auto_off.png' | out-null
  Click ($sr.L+345) ($sr.T+121); [Windows.Forms.SendKeys]::SendWait('^a'); [Windows.Forms.SendKeys]::SendWait('12'); [Windows.Forms.SendKeys]::SendWait('{TAB}'); Start-Sleep 1
  Shot $s 'gui_3_typed12.png' | out-null
  "settings.json: " + ((gc "$env:LOCALAPPDATA\LibSaber\SM2Browser\settings.json" -Raw | ConvertFrom-Json) | select WorkerThreads,MaxMemoryMB | ConvertTo-Json -Compress) | Add-Content $log
  Click ($sr.L+229) ($sr.T+121); Start-Sleep 1; Shot $s 'gui_4_auto_back.png' | out-null
  "settings.json after: " + ((gc "$env:LOCALAPPDATA\LibSaber\SM2Browser\settings.json" -Raw | ConvertFrom-Json) | select WorkerThreads,MaxMemoryMB | ConvertTo-Json -Compress) | Add-Content $log
  Click ($sr.Ri-90) ($sr.B-45); Start-Sleep 1; "settings closed: $([T]::FindWindow($null,'Settings') -eq [IntPtr]::Zero)" | Add-Content $log
 }
} finally {
 [T]::SetWindowPos($h,$NOTOP,0,0,0,0,3)|out-null; if($prevFg -ne [IntPtr]::Zero){ [T]::SetForegroundWindow($prevFg)|out-null }
 [Windows.Forms.Cursor]::Position=$cur; InputBlockOff; "restored fg $prevFg cursor $cur" | Add-Content $log
}


