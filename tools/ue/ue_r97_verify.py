# ue_r97_verify.py (R97b): after the migration, every skeletal mesh under /Game/ForgeLens/SM2/Characters loads with skeleton,
# physics asset and non-null materials whose parent chain resolves; prints one line per mesh.
import unreal
AR = unreal.AssetRegistryHelpers.get_asset_registry()
for a in AR.get_assets_by_path("/Game/ForgeLens/SM2/Characters", recursive=True):
    if str(a.asset_class_path.asset_name) != "SkeletalMesh": continue
    m = a.get_asset(); sk = m.get_editor_property("skeleton"); pa = m.get_editor_property("physics_asset")
    mats = [s.get_editor_property("material_interface") for s in m.get_editor_property("materials")]
    bad = sum(1 for x in mats if x is None or (isinstance(x, unreal.MaterialInstance) and x.get_base_material() is None))
    print("R97VER", a.package_name, "skel", sk.get_path_name() if sk else None, "pa", pa.get_path_name() if pa else None, "mats", len(mats), "bad", bad)
