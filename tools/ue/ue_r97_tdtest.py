# ue_r97_tdtest.py (R97 F43): build M_SM2 from <script> in <folder>, duplicate <mesh> MIs onto it, then apply the sidecar's tdScalars
# (apply_td_scalars: NormalScale <- nmScale, EmissiveStrength <- emissive.intensity). uerun.ps1 ue_r97_tdtest.py <script> <folder> <mesh> <materials.json>
import unreal, sys, json
scr, folder, mp, sc = sys.argv[1:5]; argv = sys.argv; sys.argv = [scr]; g = {"__name__": "ism_r97"}
exec(compile(open(scr, encoding="utf-8").read().split("\nif __name__")[0], scr, "exec"), g); sys.argv = argv
EAL = unreal.EditorAssetLibrary; mel = unreal.MaterialEditingLibrary
master = g["make_master"](folder, "M_SM2"); mel.recompile_material(master)
slots = {s["slot"]: s for s in json.load(open(sc, encoding="utf-8"))["slots"]}
mesh = unreal.load_asset(mp); n = 0
for sm in mesh.get_editor_property("materials"):
    mi = sm.get_editor_property("material_interface"); name = str(sm.get_editor_property("material_slot_name"))
    if not isinstance(mi, unreal.MaterialInstanceConstant): continue
    dst = folder + "/" + mi.get_name()
    if EAL.does_asset_exist(dst): EAL.delete_asset(dst)
    new = EAL.duplicate_asset(mi.get_path_name().split(".")[0], dst)
    if mi.get_editor_property("parent").get_name().startswith("M_SM2"): mel.set_material_instance_parent(new, master)
    if name in slots and slots[name].get("tdScalars"):
        g["apply_td_scalars"](new, slots[name]); n += 1
        print("TD", name, slots[name]["tdScalars"], mel.get_material_instance_scalar_parameter_value(new, "NormalScale"), mel.get_material_instance_scalar_parameter_value(new, "EmissiveStrength"))
    mel.update_material_instance(new); EAL.save_loaded_asset(new)
print("R97TD", folder, n)
