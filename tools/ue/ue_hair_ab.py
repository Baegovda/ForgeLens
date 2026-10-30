# Hair A/B helper (material finishing 2): duplicates the cc_calgar-style hair instances (slots with UseAnisoSpec on)
# into /Game/ForgeLens/SM2/Tests/mat2_hair/<asset>/<mode>/<MI>_off with some lobes switched off (originals untouched),
# for ue_cap_lit.py off=<folder>. Modes: nosecond (UseSecondSpec off), noaniso (UseAnisoSpec/UseAnisoDirMap off), nospec (all three off).
import unreal, sys
asset, mode = sys.argv[1], sys.argv[2]
mesh = unreal.load_asset(f"/Game/ForgeLens/SM2/Characters/{asset}/{asset}")
dst = f"/Game/ForgeLens/SM2/Tests/mat2_hair/{asset}/{mode}"
mel = unreal.MaterialEditingLibrary; eal = unreal.EditorAssetLibrary
offs = {"nosecond": ("UseSecondSpec",), "noaniso": ("UseAnisoSpec", "UseAnisoDirMap"), "nospec": ("UseSecondSpec", "UseAnisoSpec", "UseAnisoDirMap")}[mode]
n = 0
for s in mesh.materials:
    mi = s.material_interface
    if not isinstance(mi, unreal.MaterialInstanceConstant) or "hair" not in str(mel.get_material_instance_texture_parameter_value(mi, "Diffuse").get_name()): continue
    path = f"{dst}/{mi.get_name()}_off"
    if eal.does_asset_exist(path):
        off = unreal.load_asset(path)
    else:
        off = eal.duplicate_asset(mi.get_path_name().split(".")[0], path)
    for p in offs:
        mel.set_material_instance_static_switch_parameter_value(off, p, False)
    mel.update_material_instance(off); eal.save_loaded_asset(off); n += 1
    print("OFF", s.material_slot_name, path)
print("HAIR_AB", asset, mode, n)
