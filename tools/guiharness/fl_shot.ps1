# PC (Windows PowerShell 5) QA helper, repo copy of box /workspace/mtl_fix/m49/fl_shot.ps1: launch Forge Lens with SM2_OPEN/SM2_COLOUR env, wait, screenshot its window, close it. Run hidden/Idle; never while the user games.
param([string]$Exe, [string]$Open, [string]$Colour = "", [string]$Out, [int]$Wait = 30, [string]$Hover = "", [string]$Extra = "")
# Launch Forge Lens with SM2_OPEN / SM2_COLOUR, capture its window (PrintWindow, works when covered), close it.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
if (Get-Process ForgeLens -ErrorAction SilentlyContinue | ? { $_.Path -eq $Exe }) { throw "already running" }
$env:SM2_OPEN = $Open; $env:SM2_COLOUR = $Colour; $env:SM2_COLOUR_HOVER = $Hover
foreach ($kv in ($Extra -split ';' | ? { $_ })) { $p = $kv -split '=', 2; Set-Item "env:$($p[0])" $p[1] }
$p = Start-Process $Exe -PassThru
try {
  Start-Sleep $Wait
  $p.Refresh(); $h = $p.MainWindowHandle
  [W]::ShowWindow($h, 3) | Out-Null; [W]::SetForegroundWindow($h) | Out-Null; Start-Sleep 4
  $r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
  $g.Dispose(); $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  "saved $Out"
} finally { Stop-Process -Id $p.Id -ErrorAction SilentlyContinue }
