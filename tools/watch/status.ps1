$root="$env:USERPROFILE\window_watch"
$run=Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | ?{ $_.CommandLine -like '*watch*watcher.ps1*' }
"running: " + ($(if($run){$run.ProcessId -join ','}else{'no'}))
"autostart: " + (Test-Path (Join-Path ([Environment]::GetFolderPath('Startup')) 'ForgeLensWindowWatch.lnk'))
"heartbeat: " + (Get-Content "$root\heartbeat.txt" -EA 0)
"events (last 5):"; Get-Content "$root\events.jsonl" -Tail 5 -EA 0

