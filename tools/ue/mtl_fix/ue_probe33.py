# probe: class + referencers of an asset path (read-only)
import sys, unreal
EAL = unreal.EditorAssetLibrary
for p in sys.argv[1:]:
    ex = EAL.does_asset_exist(p)
    cls = str(EAL.find_asset_data(p).asset_class_path.asset_name) if ex else "-"
    print("PROBE", p, ex, cls, list(EAL.find_package_referencers_for_asset(p, False)) if ex else [])
