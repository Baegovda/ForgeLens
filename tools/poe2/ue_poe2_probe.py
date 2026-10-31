"""PoE2 M3: read-only editor state (open map, dirty maps, asset counts under /Game/ForgeLens/POE/<names>)."""
import sys, json, unreal
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
out = {"map": w.get_path_name() if w else None, "dirty": [str(p.get_name()) for p in unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages()]}
for n in sys.argv[2:]:
    cl = {}
    for a in unreal.EditorAssetLibrary.list_assets("/Game/ForgeLens/POE/" + n, recursive=True):
        c = str(unreal.EditorAssetLibrary.find_asset_data(a).asset_class_path.asset_name); cl[c] = cl.get(c, 0) + 1
    out[n] = cl
open(sys.argv[1], "w").write(json.dumps(out, indent=1))
