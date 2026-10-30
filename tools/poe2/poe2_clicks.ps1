# M2 real-input test (run via tools\input_block.ps1): Files tab -> search -> open first result; Skills -> firebolt effect.
param([string]$Out)
New-Item -ItemType Directory -Force $Out | Out-Null
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;
public static class UX{[DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);[DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")]public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);[DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out R r);
[DllImport("user32.dll")]public static extern void mouse_event(uint f,int x,int y,uint d,IntPtr e);[DllImport("user32.dll")]public static extern bool SetCursorPos(int x,int y);
public struct R{public int L,T,Rr,B;}
public delegate bool EP(IntPtr h,IntPtr l);[DllImport("user32.dll")]public static extern bool EnumWindows(EP e,IntPtr l);[DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
public static IntPtr Find(string t){IntPtr f=IntPtr.Zero;EnumWindows((h,l)=>{var s=new StringBuilder(256);GetWindowText(h,s,256);if(IsWindowVisible(h)&&s.ToString()==t){f=h;return false;}return true;},IntPtr.Zero);return f;}
public static string Title(IntPtr h){var s=new StringBuilder(256);GetWindowText(h,s,256);return s.ToString();}}
"@
$log = Join-Path $Out 'clicks.txt'
function L($m){ "$(Get-Date -f HH:mm:ss) $m" | Add-Content $log }
$fg=[UX]::GetForegroundWindow(); L "foreground before: '$([UX]::Title($fg))'"
if (Get-Process | ? { $_.ProcessName -match 'PathOfExile' }) { L 'game running: abort'; exit 2 }
$w=[UX]::Find('Path of Exile 2'); if ($w -eq [IntPtr]::Zero) { L 'no PoE2 window'; exit 3 }
function Click($dx,$dy){ $r=New-Object UX+R; [UX]::GetWindowRect($w,[ref]$r)|Out-Null; [UX]::SetCursorPos($r.L+$dx,$r.T+$dy)|Out-Null; Start-Sleep -m 120; [UX]::mouse_event(2,0,0,0,[IntPtr]::Zero); Start-Sleep -m 60; [UX]::mouse_event(4,0,0,0,[IntPtr]::Zero); L "click $dx,$dy"; Start-Sleep -m 400 }
function Shot($name){ $b=[Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp=New-Object Drawing.Bitmap $b.Width,$b.Height; [Drawing.Graphics]::FromImage($bmp).CopyFromScreen(0,0,0,0,$bmp.Size); $bmp.Save((Join-Path $Out "$name.png")); $bmp.Dispose() }
InputBlockOn
[UX]::SetForegroundWindow($w)|Out-Null; Start-Sleep -m 500
L "foreground now: '$([UX]::Title([UX]::GetForegroundWindow()))'"
Click 54 107                      # Files tab
Click 300 147                     # search box
[Windows.Forms.SendKeys]::SendWait('^a'); [Windows.Forms.SendKeys]::SendWait('mp_halfsphere_lod1_ff'); Start-Sleep 2
Shot '1_files_search'
Click 300 183                     # first result
Start-Sleep 12
Shot '2_file_opened'
Click 125 107                     # Skills tab
Click 300 147
[Windows.Forms.SendKeys]::SendWait('^a'); [Windows.Forms.SendKeys]::SendWait('firebolt'); Start-Sleep 1
Click 300 183                     # first skill
Click 300 431                     # first effect (projectile)
Start-Sleep 10
Shot '3_skill_effect'
InputBlockOff
L 'done'
