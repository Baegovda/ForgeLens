# ue_dirty.py [save]: list dirty packages; with "save", save all except Lightning_01, /Game/SM2_Work/Original/*, /Game/Work/*, diag_unsaved.
import sys, unreal
save = len(sys.argv) > 1 and sys.argv[1] == "save"
def skip(n):
    return ("Lightning_01" in n or n.startswith("/Game/SM2_Work/Original/") or n.startswith("/Game/Work/")
            or "/diag_unsaved/" in n)
pk = list(unreal.EditorLoadingAndSavingUtils.get_dirty_content_packages()) + list(unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages())
ok = []
for p in pk:
    n = p.get_name(); s = skip(n)
    print("DIRTY", n, "SKIP" if s else "")
    if not s: ok.append(p)
if save and ok:
    print("SAVED", unreal.EditorLoadingAndSavingUtils.save_packages(ok, True))
print("N", len(pk), len(ok))
