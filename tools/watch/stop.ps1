param([switch]$Unregister)
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | ?{ $_.CommandLine -like '*watch\watcher.ps1*' } | %{ Stop-Process -Id $_.ProcessId -Force; "stopped $($_.ProcessId)" }
if($Unregister){ Remove-Item (Join-Path ([Environment]::GetFolderPath('Startup')) 'ForgeLensWindowWatch.lnk') -EA 0; "autostart removed" }
