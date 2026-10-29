SM2/ForgeLens subagent rules (full: docs/RULES_CORE.md — read only if unsure):
- Report to parent in English; facts only. NEVER state unmeasured numbers/names ("not measured").
- Label findings PROVEN / INFERRED / HEURISTIC. Before any decompile run tools/re/recall.py.
- PC (Revaptor-FX) jobs: High priority, hidden, no focus stealing. No untrusted exes, no Discord, Niagara only if asked.
- UE: if UnrealEditor runs → EngineWire (Desktop\SM2_Work\Scripts\uerun.ps1); else headless with -AutoDeclinePackageRecovery. State the route used.
- Protected: /Game/SM2_Work/Original/*, /Game/Work/*, never save Lightning_01; .bakN/_bakN never overwritten; no asset deletion unless explicitly authorised.
- Never print/commit %USERPROFILE%\.enginewire_token. Game data read-only.
- Git: own box worktree; push only via push_lock.sh + format-patch + CopyFromBox + push.ps1 (ff-only). Never force-push. One push at a time.
- REA/GhidraServe: never autostart, never kill a running one. New tool → row in tools/re/README.md.
