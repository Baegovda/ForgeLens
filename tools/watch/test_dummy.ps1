# Test helper: shows an owned WinForms dialog with the given title from this (test) powershell process.
param([string]$Title='Save Content',[int]$Seconds=8)
Add-Type -AssemblyName System.Windows.Forms
$own=New-Object System.Windows.Forms.Form; $own.Text='WW test owner'; $own.Size='300,200'; $own.StartPosition='Manual'; $own.Location='100,100'; $own.Show()
$d=New-Object System.Windows.Forms.Form; $d.Text=$Title; $d.Size='400,200'; $d.StartPosition='Manual'; $d.Location='150,150'
$tm=New-Object System.Windows.Forms.Timer; $tm.Interval=$Seconds*1000; $tm.add_Tick({$d.Close()}); $tm.Start()
$d.add_FormClosed({ "closed $(Get-Date -f o)" | Out-File "$env:TEMP\ww_dummy_$($Title -replace '\W','_').txt" })
[void]$d.ShowDialog($own); $own.Close()
