# oneclick QA: run Forge Lens with QA env (SM2_OPEN / SM2_SEQ / SM2_CLIP / SM2_EXPORT_QA / SM2_DIRECT_*), keep its windows
# off-screen, PrintWindow every Forge Lens window every few seconds until the Direct-to-Unreal result json appears.
param([string]$exe,[string]$out,[string]$exportDir,[int]$timeout=900)
Add-Type -ReferencedAssemblies System.Drawing @"
using System;using System.Text;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Drawing;
public class FW{
 public delegate bool EP(IntPtr h,IntPtr l);
 [DllImport("user32.dll")]public static extern bool EnumWindows(EP f,IntPtr l);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int ht,uint f);
 [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint f);
 [DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
 public struct RECT{public int L,T,R,B;}
 public static List<IntPtr> Of(uint pid){var l=new List<IntPtr>();EnumWindows((h,x)=>{uint p;GetWindowThreadProcessId(h,out p);if(p==pid&&IsWindowVisible(h))l.Add(h);return true;},IntPtr.Zero);return l;}
 public static string Title(IntPtr h){var s=new StringBuilder(256);GetWindowText(h,s,256);return s.ToString();}
 public static void Cap(IntPtr h,string o){RECT r;GetWindowRect(h,out r);int w=Math.Max(64,r.R-r.L),ht=Math.Max(64,r.B-r.T);var b=new Bitmap(w,ht);using(var g=Graphics.FromImage(b)){var dc=g.GetHdc();PrintWindow(h,dc,2);g.ReleaseHdc(dc);}b.Save(o);}
}
"@
[FW]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force $out | Out-Null
$p=Start-Process $exe -WindowStyle Minimized -PassThru; try{$p.PriorityClass='BelowNormal'}catch{}
$t0=Get-Date; $moved=@{}; $n=0; $done=$null
while(((Get-Date)-$t0).TotalSeconds -lt $timeout -and !$p.HasExited){
  Start-Sleep 3; $n++
  foreach($h in [FW]::Of([uint32]$p.Id)){
    $title=[FW]::Title($h); if($title -eq ""){continue}
    if(-not $moved.ContainsKey($h)){
      $r=New-Object FW+RECT; [FW]::GetWindowRect($h,[ref]$r)|Out-Null
      $w=$r.R-$r.L; $ht=$r.B-$r.T
      if($title -like "*Space Marine 2 assets*"){ [FW]::SetWindowPos($h,[IntPtr]1,-3000,0,1440,860,0x10)|Out-Null } else { [FW]::SetWindowPos($h,[IntPtr]0,-2900,40,[Math]::Max($w,200),[Math]::Max($ht,200),0x10)|Out-Null }
      $moved[$h]=1; "{0,5:n0}s window: {1}" -f ((Get-Date)-$t0).TotalSeconds,$title
    }
    $tag=($title -replace '[^A-Za-z]','')
    if($n % 2 -eq 0 -or $title -notlike "*assets*"){ [FW]::Cap($h,(Join-Path $out ("{0:d3}_{1}.png" -f [int]((Get-Date)-$t0).TotalSeconds,$tag))) }
  }
  $res=Get-ChildItem (Join-Path $exportDir "_forge_direct") -Filter "result_*.json" -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt $t0 } | Sort LastWriteTime | Select -Last 1
  if($res -and -not $done){ $done=Get-Date; "{0,5:n0}s result: {1}" -f ((Get-Date)-$t0).TotalSeconds,$res.Name }
  if($done -and ((Get-Date)-$done).TotalSeconds -gt 10){ break }
}
foreach($h in [FW]::Of([uint32]$p.Id)){ $title=[FW]::Title($h); if($title){ [FW]::Cap($h,(Join-Path $out ("final_{0}.png" -f ($title -replace '[^A-Za-z]',''))))} }
if(!$p.HasExited){ $p.CloseMainWindow()|Out-Null; Start-Sleep 3; if(!$p.HasExited){$p.Kill()} }
"elapsed {0:n0}s" -f ((Get-Date)-$t0).TotalSeconds
