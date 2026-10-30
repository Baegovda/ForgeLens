# input_block.ps1 (owner rule t220u, 10/30): run a real-input test script while the owner's PHYSICAL mouse/keyboard is
# blocked (user32 BlockInput). Injected SendInput from this process still works. Safety: per-run watchdog kills this
# process after -MaxBlockSec (process exit = Windows unblocks), finally{} always unblocks, Ctrl+Alt+Del also unblocks.
# Self-elevates (UAC ConsentPromptBehaviorAdmin=0 on Revaptor-FX = no prompt). Covering windows that steal focus are
# minimised by the test's Focus/Guard and restored at the end; nothing is closed. Cursor position restored.
# Usage: tools\input_block.ps1 -Script <test.ps1> -ArgsJson '{"Exe":"...","Out":"..."}' [-MaxBlockSec 60]
# Test scripts opt in by calling InputBlockOn (idempotent) right before their first input (e.g. inside Focus) and
# IBMinimize <hwnd> for a covering window. Block log: <Out>\input_block.txt (seconds blocked).
param([string]$Script,[string]$ArgsJson='{}',[int]$MaxBlockSec=60,[switch]$Elevated,[ValidateSet('hook','blockinput')][string]$Mode='hook')
# Mode hook (default, no elevation): WH_MOUSE_LL + WH_KEYBOARD_LL drop every event without the INJECTED flag (= the
# physical devices) and pass SendInput through. Mode blockinput: user32 BlockInput (elevated). R88 finding: with
# BlockInput the viewer's 3D viewport ignored injected LMB/hover (panels, wheel, keys still worked), hook mode did not.
$me=$MyInvocation.MyCommand.Path; $exe=(Get-Process -Id $PID).Path
$isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if($Mode -eq 'blockinput' -and -not $isAdmin){
  $j=[IO.Path]::GetTempFileName(); Set-Content $j $ArgsJson -Encoding UTF8
  $p=Start-Process $exe -Verb RunAs -Wait -PassThru -WindowStyle Hidden -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$me`"",'-Script',"`"$Script`"",'-ArgsJson',"@$j",'-MaxBlockSec',$MaxBlockSec,'-Elevated','-Mode','blockinput')
  Remove-Item $j -EA 0; exit $p.ExitCode
}
if($ArgsJson.StartsWith('@')){ $ArgsJson=Get-Content $ArgsJson.Substring(1) -Raw }
$params=@{}; ($ArgsJson|ConvertFrom-Json).PSObject.Properties|%{ $params[$_.Name]=$_.Value }
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class IB { [DllImport("user32.dll", SetLastError=true)] public static extern bool BlockInput(bool b);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int c);
  delegate IntPtr HP(int c, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HP f, IntPtr m, uint t);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
  [DllImport("user32.dll")] static extern bool PostThreadMessage(uint t, uint m, IntPtr w, IntPtr l);
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
  [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
  [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr h; public uint m; public IntPtr w, l; public uint t; public int x, y; }
  static HP km, mm; static uint tid; static System.Threading.Thread th; public static int Dropped;
  public static void HookOn(){ if(th!=null) return; var ready=new System.Threading.ManualResetEvent(false);
    th=new System.Threading.Thread(()=>{ tid=GetCurrentThreadId();
      // KBDLLHOOKSTRUCT.flags @8 bit4 = injected; MSLLHOOKSTRUCT.flags @12 bit0 = injected
      km=(c,w,l)=>{ if(c>=0 && (Marshal.ReadInt32(l,8)&0x10)==0){ Dropped++; return (IntPtr)1; } return CallNextHookEx(IntPtr.Zero,c,w,l); };
      mm=(c,w,l)=>{ if(c>=0 && (Marshal.ReadInt32(l,12)&1)==0){ Dropped++; return (IntPtr)1; } return CallNextHookEx(IntPtr.Zero,c,w,l); };
      var hm=GetModuleHandle(null); var a=SetWindowsHookEx(13,km,hm,0); var b=SetWindowsHookEx(14,mm,hm,0); ready.Set();
      MSG msg; while(GetMessage(out msg,IntPtr.Zero,0,0)>0){} UnhookWindowsHookEx(a); UnhookWindowsHookEx(b); });
    th.IsBackground=true; th.Start(); ready.WaitOne(); }
  public static void HookOff(){ if(th==null) return; PostThreadMessage(tid,0x12,IntPtr.Zero,IntPtr.Zero); th.Join(2000); th=null; } }
"@
Add-Type -AssemblyName System.Windows.Forms
$global:IBCur=[Windows.Forms.Cursor]::Position; $global:IBT=$null; $global:IBDog=$null; $global:IBMin=@()
$global:IBLog= if($params.Out){ Join-Path $params.Out 'input_block.txt' } else { "$env:TEMP\input_block.txt" }
function global:InputBlockOn { if($global:IBT){ return }
  $global:IBDog=Start-Process $exe -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-Command',"Start-Sleep $MaxBlockSec; Stop-Process -Id $PID -Force")
  $ok= if($Mode -eq 'hook'){ [IB]::HookOn(); 'hook' } else { [IB]::BlockInput($true) }; $global:IBT=[Diagnostics.Stopwatch]::StartNew(); "$(Get-Date -f s) block on ok=$ok (watchdog ${MaxBlockSec}s pid $($global:IBDog.Id))" | Add-Content $global:IBLog }
function global:InputBlockOff { if(-not $global:IBT){ return }; [IB]::HookOff(); [IB]::BlockInput($false)|Out-Null; $global:IBT.Stop()
  "$(Get-Date -f s) block off after {0:0.0} s (physical events dropped {1})" -f $global:IBT.Elapsed.TotalSeconds,[IB]::Dropped | Add-Content $global:IBLog; $global:IBT=$null
  if($global:IBDog -and -not $global:IBDog.HasExited){ Stop-Process -Id $global:IBDog.Id -Force -EA 0 } }
function global:IBMinimize([IntPtr]$h){ if($h -ne [IntPtr]::Zero){ [IB]::ShowWindow($h,6)|Out-Null; $global:IBMin+=$h; "minimised hwnd $h" | Add-Content $global:IBLog } }
$code=0
try { & $Script @params; $code=$LASTEXITCODE } finally {
  InputBlockOff; [IB]::HookOff(); [IB]::BlockInput($false)|Out-Null
  foreach($h in $global:IBMin){ [IB]::ShowWindow($h,4)|Out-Null }   # SW_SHOWNOACTIVATE: restore without stealing focus
  [Windows.Forms.Cursor]::Position=$global:IBCur
}
exit $code

