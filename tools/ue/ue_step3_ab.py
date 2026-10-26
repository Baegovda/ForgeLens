# Step 3 A/B helper: duplicates each step3 instance that has UseSecondSpec/UseAnisoSpec on into
# /Game/SM2_Work/Tests/step3_ab_off/<asset>/<MI>_off with all three switches off (originals untouched).
import unreal, sys
asset = sys.argv[1]
mesh = unreal.load_asset(f"/Game/SM2_Work/Characters/{asset}/{asset}")
dst = f"/Game/SM2_Work/Tests/step3_ab_off/{asset}"
mel = unreal.MaterialEditingLibrary; eal = unreal.EditorAssetLibrary
n = 0
for s in mesh.materials:
    mi = s.material_interface
    if not isinstance(mi, unreal.MaterialInstanceConstant): continue
    on = [mel.get_material_instance_static_switch_parameter_value(mi, p) for p in ("UseSecondSpec", "UseAnisoSpec")]
    if not any(on): continue
    path = f"{dst}/{mi.get_name()}_off"
    off = unreal.load_asset(path) if eal.does_asset_exist(path) else eal.duplicate_asset(mi.get_path_name().split(".")[0], path)
    for p in ("UseSecondSpec", "UseAnisoSpec", "UseAnisoDirMap"):
        mel.set_material_instance_static_switch_parameter_value(off, p, False)
    mel.update_material_instance(off); eal.save_loaded_asset(off); n += 1
    print("OFF", s.material_slot_name, path)
print("AB_OFF", asset, n)
