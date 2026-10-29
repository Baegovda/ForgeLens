# Register the silent watcher tasks (mem/disk every 10 min, exe hash daily). REA / GhidraServe are NOT auto-started
# (owner decision 10/30: manual one-command start, start_rea.ps1 / gs.ps1 start). Watchers print nothing; they only write alerts.txt.
# Register Task Scheduler entries (current user, hidden via run_hidden.vbs). Re-run = update (/f). Remove: schtasks /delete /tn "SM2\<name>" /f
# Scripts run from the ls_push checkout so `git pull` updates them.
$T = "$env:USERPROFILE\ls_push\tools\re"; $V = "wscript.exe `"$T\pc\run_hidden.vbs`""
$jobs = @(
  @('SM2\mem_watch',   "$V `"$T\pc\mem_watch.ps1`" -Once",              '/sc minute /mo 10'),
  @('SM2\disk_watch',  "$V `"$T\pc\disk_watch.ps1`"",                   '/sc minute /mo 10'),
  @('SM2\patch_watch', "$V `"$T\pc\patch_watch.ps1`"",                  '/sc daily /st 09:00'))
foreach ($j in $jobs) { $a = "/create /f /tn `"$($j[0])`" /tr `"$($j[1] -replace '"','\"')`" $($j[2]) /rl limited"; cmd /c "schtasks $a" }
cmd /c 'schtasks /query /fo table /tn "SM2\\" 2>&1' 
