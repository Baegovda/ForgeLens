# R91: close any visible "Message Log" window of UnrealEditor (owner t222u). PostMessage WM_CLOSE, no focus change.
Add-Type @"
using System;using System.Text;using System.Collections.Generic;using System.Runtime.InteropServices;
public class ML{public delegate bool EP(IntPtr h,IntPtr l);
[DllImport("user32.dll")]public static extern bool EnumWindows(EP f,IntPtr l);
[DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
[DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
[DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
public static List<IntPtr> Find(HashSet<uint> pids){var r=new List<IntPtr>();EnumWindows((h,x)=>{uint p;GetWindowThreadProcessId(h,out p);if(pids.Contains(p)&&IsWindowVisible(h)){var s=new StringBuilder(256);GetWindowText(h,s,256);if(s.ToString()=="Message Log")r.Add(h);}return true;},IntPtr.Zero);return r;}}
"@
$ids=New-Object 'System.Collections.Generic.HashSet[uint32]'; Get-Process UnrealEditor -EA 0 | ? MainWindowTitle -like 'UE_VFX_57*' | % { [void]$ids.Add([uint32]$_.Id) }
$n=0; foreach($h in [ML]::Find($ids)){ [ML]::PostMessage($h,0x10,[IntPtr]::Zero,[IntPtr]::Zero)|Out-Null; $n++ }; "MSGLOG closed=$n"
