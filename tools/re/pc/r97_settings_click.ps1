param($Out,$ProcId,$Phase,$Pts)
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Runtime.InteropServices;
public static class W{[DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
[DllImport("user32.dll")]public static extern void mouse_event(uint f,int x,int y,uint d,UIntPtr e);[DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);[DllImport("user32.dll")]public static extern IntPtr FindWindow(string c,string t);
[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);[DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);public struct R{public int L,T,Ri,B;}}
"@
[W]::SetProcessDPIAware()|Out-Null; $log="$Out\gui_log.txt"; function L($m){ "$(Get-Date -f HH:mm:ss.fff) $m" | Add-Content $log }
$ProcId=[int]$ProcId; $main=(Get-Process -Id $ProcId).MainWindowHandle
function FgOk(){ $h=[W]::GetForegroundWindow(); $p=0; [W]::GetWindowThreadProcessId($h,[ref]$p)|Out-Null; return $p -eq $ProcId }
function Fg($hw){ for($k=0;$k -lt 5 -and -not (FgOk);$k++){ [W]::SetWindowPos($hw,[IntPtr](-1),0,0,0,0,3)|Out-Null; [W]::SetForegroundWindow($hw)|Out-Null; Start-Sleep -m 300 } }
function Click($hw,$x,$y,$what){ Fg $hw; if(-not (FgOk)){ L "ABORT fg not ForgeLens before $what"; throw 'fg' }
  L "fg ok (pid $ProcId) -> click $what at $x,$y"; [W]::SetCursorPos($x,$y)|Out-Null; Start-Sleep -m 120
  if(-not (FgOk)){ L "ABORT fg changed before $what"; throw 'fg' }
  [W]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -m 60; [W]::mouse_event(4,0,0,0,[UIntPtr]::Zero); Start-Sleep -m 500 }
function Shot($h,$name){ $r=New-Object W+R; [W]::GetWindowRect($h,[ref]$r)|Out-Null; $b=New-Object Drawing.Bitmap(($r.Ri-$r.L),($r.B-$r.T)); $g=[Drawing.Graphics]::FromImage($b); $dc=$g.GetHdc(); [W]::PrintWindow($h,$dc,2)|Out-Null; $g.ReleaseHdc($dc); $g.Dispose(); $b.Save("$Out\$name"); $b.Dispose(); $r }
function Keys($k){ if(-not (FgOk)){ L "ABORT fg before keys $k"; throw 'fg' }; [Windows.Forms.SendKeys]::SendWait($k) }
$sj="$env:LOCALAPPDATA\LibSaber\SM2Browser\settings.json"; function SJ(){ (gc $sj -Raw | ConvertFrom-Json) | select WorkerThreads,MaxMemoryMB | ConvertTo-Json -Compress }
$cur=[Windows.Forms.Cursor]::Position; $prev=[W]::GetForegroundWindow()
try{ InputBlockOn
 if($Phase -eq 'open'){ $r=New-Object W+R; [W]::GetWindowRect($main,[ref]$r)|Out-Null; Click $main ($r.L+1009) ($r.T+330) 'toolbar Settings'
   $s=[IntPtr]::Zero; for($i=0;$i -lt 20 -and $s -eq [IntPtr]::Zero;$i++){ Start-Sleep -m 250; $s=[W]::FindWindow([NullString]::Value,'Settings') }; L "settings hwnd $s"
   if($s -ne 0){ $sr=Shot $s 's0_open.png'; L "settings rect $($sr.L) $($sr.T) $($sr.Ri) $($sr.B)" } }
 else { $s=[W]::FindWindow([NullString]::Value,'Settings'); $sr=New-Object W+R; [W]::GetWindowRect($s,[ref]$sr)|Out-Null; $q=$Pts -split ','
   Click $s ($sr.L+$q[0]) ($sr.T+$q[1]) 'threads Auto'; Click $s ($sr.L+$q[2]) ($sr.T+$q[3]) 'threads box'; Keys '^a'; Keys '6'; Keys '{TAB}'; Start-Sleep 1
   Click $s ($sr.L+$q[4]) ($sr.T+$q[5]) 'memory Auto'; Click $s ($sr.L+$q[6]) ($sr.T+$q[7]) 'memory box'; Keys '^a'; Keys '8'; Keys '{TAB}'; Start-Sleep 1
   Shot $s 's1_manual.png' | Out-Null; L "settings.json after: $(SJ)" }
} catch { L "ERR $_" } finally { InputBlockOff; [Windows.Forms.Cursor]::Position=$cur; if($prev -ne 0){[W]::SetForegroundWindow($prev)|Out-Null}; L 'restored' }

