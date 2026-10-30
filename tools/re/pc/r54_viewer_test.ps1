# R54b: open the real ForgeLens window (no activation), select a level .cd_list via SM2_SELECT (same path as a file-list
# click), save the in-app window capture (SM2_WINSHOT) and a PrintWindow capture of the OS window. Hidden, High priority.
param([string]$Level = "pvp_city", [string]$Out = "$env:USERPROFILE\Desktop\SM2_Work\Shots\r54b")
$exe = "$env:USERPROFILE\Desktop\SM2_Work\Bin\ForgeLens_r54\ForgeLens.exe"
New-Item -ItemType Directory -Force $Out | Out-Null
$shot = "$Out\viewer_$Level.png"; Remove-Item "$shot*" -EA 0
$psi = New-Object Diagnostics.ProcessStartInfo $exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables["SM2_SELECT"] = "scenes/$Level.scn/$Level.cd_list"
$psi.EnvironmentVariables["SM2_WINSHOT"] = $shot
$psi.WindowStyle = 'Minimized'
$t = [Diagnostics.Stopwatch]::StartNew()
$p = [Diagnostics.Process]::Start($psi); try { $p.PriorityClass = 'High' } catch {}
while (-not (Test-Path "$shot.txt") -and $t.Elapsed.TotalSeconds -lt 600) { Start-Sleep 1 }
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class PW { [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; } }
"@
Add-Type -AssemblyName System.Drawing
$p.Refresh(); $h = $p.MainWindowHandle
if ($h -ne [IntPtr]::Zero) {
  [PW]::ShowWindow($h, 4) | Out-Null  # SW_SHOWNOACTIVATE (restore without focus) so PrintWindow has pixels
  Start-Sleep 3
  $r = New-Object PW+RECT; [PW]::GetWindowRect($h, [ref]$r) | Out-Null
  $bmp = New-Object Drawing.Bitmap ([Math]::Max(1, $r.R - $r.L)), ([Math]::Max(1, $r.B - $r.T))
  $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc(); [PW]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
  $bmp.Save("$Out\viewer_${Level}_printwindow.png"); $g.Dispose(); $bmp.Dispose()
}
"wall $([math]::Round($t.Elapsed.TotalSeconds,1))s" | Add-Content "$shot.txt"
$p.Kill()
