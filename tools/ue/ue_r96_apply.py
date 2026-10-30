# ue_r96_apply.py (R96): build M_SM2_r96 (merged second-spec master) next to a character's M_SM2 and re-parent its M_SM2 MIs.
# Undo: re-parent back to M_SM2 (kept). uerun.ps1 ue_r96_apply.py <import_sm2_materials.py> <meshpath>
import unreal, sys
scr, mp = sys.argv[1], sys.argv[2]; argv = sys.argv; sys.argv = [scr]; g = {"__name__": "ism_r96"}
exec(compile(open(scr, encoding="utf-8").read().split("\nif __name__")[0], scr, "exec"), g); sys.argv = argv
mesh = unreal.load_asset(mp); folder = mp.rsplit("/", 1)[0]; mel = unreal.MaterialEditingLibrary
master = g["make_master"](folder, "M_SM2_r96"); mel.recompile_material(master); unreal.EditorAssetLibrary.save_loaded_asset(master)
n = 0
for sm in mesh.get_editor_property("materials"):
    mi = sm.get_editor_property("material_interface")
    if isinstance(mi, unreal.MaterialInstanceConstant) and mi.get_editor_property("parent") and mi.get_editor_property("parent").get_name() == "M_SM2":
        mel.set_material_instance_parent(mi, master); mel.update_material_instance(mi); unreal.EditorAssetLibrary.save_loaded_asset(mi); n += 1
print("R96APPLY", mp, n)
