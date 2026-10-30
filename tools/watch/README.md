# Window watcher (tools/watch)
Polls visible top-level windows every 1 s (EnumWindows, no screen streaming) for processes in -ProcNames (default UnrealEditor, CrashReportClient*).
- `Message Log*` title -> PostMessage WM_CLOSE (never kills), event action=wm_close.
- `Save Content`, crash reporter (`has crashed` / CrashReportClient process), other modal windows (owner window disabled; floating tabs like Content Browser are ignored) -> NOT clicked; one JSON line in `%USERPROFILE%\window_watch\events.jsonl` + one PNG of that monitor in `window_watch\shots\` per appearance.
- `heartbeat.txt` every 10 s, `watcher.log` rotated at 1 MB.
start.ps1 (hidden, single instance, Startup-folder shortcut for logon), stop.ps1 [-Unregister], status.ps1. Test: start with -ProcNames powershell -NoTask and run test_dummy.ps1.
Parent agent use: read events.jsonl tail (text, cheap); only CopyToBox the `shot` PNG when a new event appears. Check heartbeat age < 30 s to know the watcher is alive.
