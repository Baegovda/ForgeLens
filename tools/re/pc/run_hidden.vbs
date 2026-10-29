' Hidden launcher for Task Scheduler (no window, no focus steal): wscript run_hidden.vbs script.ps1 [args]
Set a = WScript.Arguments: cmd = "powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & a(0) & """"
For i = 1 To a.Count - 1: cmd = cmd & " " & a(i): Next
CreateObject("WScript.Shell").Run cmd, 0, False
