"""Close the editor for a plugin rebuild only when no other worker has unsaved work: dirty packages outside /Game/ForgeLens/POE
-> refuse (nothing saved, nothing closed). Own dirty POE packages are saved first. Arg 'check' only reports."""
import sys, unreal
L = unreal.EditorLoadingAndSavingUtils
d = [str(p.get_name()) for p in list(L.get_dirty_map_packages()) + list(L.get_dirty_content_packages())]
foreign = [x for x in d if not x.startswith(("/Game/ForgeLens/POE", "/Temp/"))]
print("POE3 quit dirty", d, "foreign", foreign)
if foreign or (len(sys.argv) > 1 and sys.argv[1] == "check"):
    print("POE3 quit refused" if foreign else "POE3 quit check only")
else:
    own = [p for p in list(L.get_dirty_content_packages()) if str(p.get_name()).startswith("/Game/ForgeLens/POE")]
    if own: L.save_packages(own, True)
    print("POE3 quit requested"); unreal.SystemLibrary.quit_editor()
