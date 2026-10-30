# ForgeLens window watcher: polls top-level windows ~1s, no image streaming.
# Message Log -> WM_CLOSE. Save Content / Crash Reporter / other UE modal -> one event + one monitor screenshot per appearance.
param([string]$Root="$env:USERPROFILE\window_watch",[string]$ProcNames="UnrealEditor,CrashReportClient,CrashReportClientEditor",[int]$PollMs=1000)
$ErrorActionPreference='Continue'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;using System.Collections.Generic;
public class WW{
 public delegate bool EP(IntPtr h,IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EP f,IntPtr l);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint c);
 [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential)] public struct R{public int L,T,Rt,B;}
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out R r);
 public static List<IntPtr> Of(HashSet<uint> pids){var l=new List<IntPtr>();EnumWindows((h,x)=>{uint p;GetWindowThreadProcessId(h,out p);if(pids.Contains(p)&&IsWindowVisible(h))l.Add(h);return true;},IntPtr.Zero);return l;}
 public static string T(IntPtr h){var s=new StringBuilder(512);GetWindowText(h,s,512);return s.ToString();}
 public static string C(IntPtr h){var s=new StringBuilder(256);GetClassName(h,s,256);return s.ToString();}
}
"@
$shots=Join-Path $Root shots; New-Item -ItemType Directory -Force $shots|Out-Null
$ev=Join-Path $Root events.jsonl; $log=Join-Path $Root watcher.log; $hb=Join-Path $Root heartbeat.txt
$names=$ProcNames.Split(',')|%{$_.Trim().ToLower()}
function Log($m){ if((Test-Path $log) -and (Get-Item $log).Length -gt 1MB){ Move-Item $log "$log.1" -Force }; "$(Get-Date -f s) $m" | Add-Content $log }
function Kind($t,$owned){
 if($t -match '^Message Log'){return 'message_log'}
 if($t -match 'Save Content'){return 'save_content'}
 if($t -match 'has crashed|Crash Reporter'){return 'crash_reporter'}
 if($owned){return 'modal_other'}; return $null }
$seen=@{}; $prev=@{}; $lastHb=[datetime]0
Log "start pid=$PID procs=$ProcNames"
while($true){
 try{
  $pend=@{}; $now=@{}; $procs=@{}; $set=New-Object 'System.Collections.Generic.HashSet[uint32]'
  foreach($pr in [System.Diagnostics.Process]::GetProcesses()){ if($names -contains $pr.ProcessName.ToLower()){ [void]$set.Add([uint32]$pr.Id); $procs[[uint32]$pr.Id]=$pr } }
  $list= if($set.Count){[WW]::Of($set)}else{@()}
  foreach($h in $list){
   $p=[uint32]0; [void][WW]::GetWindowThreadProcessId($h,[ref]$p)
   $pr=$procs[$p]; if(-not $pr){continue}; $pn=$pr.ProcessName.ToLower()
   $t=[WW]::T($h); $ow=[WW]::GetWindow($h,4); $owned=($ow -ne [IntPtr]::Zero -and -not [WW]::IsWindowEnabled($ow))  # modal = owner disabled
   $k=Kind $t $owned; if(-not $k){continue}
   if($pn -like 'crashreport*'){ $k='crash_reporter' }
   $key="$h|$t"; $now[$key]=1
   if($seen.ContainsKey($key)){ continue }
   if($k -eq 'modal_other'){ $pend["$key"]=1+[int]$prev["$key"]; if($pend["$key"] -lt 3){ continue } }  # debounce: modal for 3 polls
   $seen[$key]=1
   $r=New-Object WW+R; [void][WW]::GetWindowRect($h,[ref]$r)
   $scr=[System.Windows.Forms.Screen]::FromHandle($h); $b=$scr.Bounds
   $o=[ordered]@{time=(Get-Date -f o);kind=$k;title=$t;class=[WW]::C($h);process=$pr.ProcessName;pid=$p;hwnd=[int64]$h;rect=@($r.L,$r.T,$r.Rt,$r.B);monitor=$scr.DeviceName;monitor_bounds=@($b.X,$b.Y,$b.Width,$b.Height);action='none';shot=$null}
   if($k -eq 'message_log'){ [void][WW]::PostMessage($h,0x10,[IntPtr]::Zero,[IntPtr]::Zero); $o.action='wm_close' }
   else{
    try{ $f=Join-Path $shots ("{0}_{1}.png" -f (Get-Date -f yyyyMMdd_HHmmss),$k)
     $bmp=New-Object System.Drawing.Bitmap $b.Width,$b.Height; $g=[System.Drawing.Graphics]::FromImage($bmp)
     $g.CopyFromScreen($b.X,$b.Y,0,0,$bmp.Size); $bmp.Save($f,[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose();$bmp.Dispose(); $o.shot=$f }catch{ Log "shot fail $_" } }
   ($o|ConvertTo-Json -Compress -Depth 3)|Add-Content $ev -Encoding UTF8
   Log "event $k '$t' hwnd=$h"
  }
  $prev=$pend
  foreach($k in @($seen.Keys)){ if(-not $now.ContainsKey($k)){ $seen.Remove($k) } }
  if(((Get-Date)-$lastHb).TotalSeconds -ge 10){ "$(Get-Date -f o) pid=$PID" | Set-Content $hb; $lastHb=Get-Date }
 }catch{ Log "loop err $_" }
 Start-Sleep -Milliseconds $PollMs
}
