# ue_r96_mtltest.py (R96): build a test M_SM2 from <script> into <folder>, duplicate a mesh's MIs onto it (folder/MI_*), print map.
# uerun.ps1 ue_r96_mtltest.py <import_sm2_materials.py> <folder> <meshpath> [switch=val;...]
import unreal, sys, importlib.util
scr, folder, mp = sys.argv[1], sys.argv[2], sys.argv[3]
sw = dict(kv.split("=") for kv in sys.argv[4].split(";")) if len(sys.argv) > 4 and sys.argv[4] else {}
argv = sys.argv; sys.argv = [scr]
spec = importlib.util.spec_from_file_location("ism_r96", scr); m = importlib.util.module_from_spec(spec)
src = open(scr, encoding="utf-8").read().split("\nif __name__")[0]
exec(compile(src, scr, "exec"), m.__dict__); sys.argv = argv
master = m.make_master(folder, "M_SM2")
unreal.MaterialEditingLibrary.recompile_material(master)
mesh = unreal.load_asset(mp); EAL = unreal.EditorAssetLibrary; mel = unreal.MaterialEditingLibrary
for sm in mesh.get_editor_property("materials"):
    mi = sm.get_editor_property("material_interface")
    if not isinstance(mi, unreal.MaterialInstanceConstant): continue
    dst = folder + "/" + mi.get_name()
    if EAL.does_asset_exist(dst): EAL.delete_asset(dst)
    new = EAL.duplicate_asset(mi.get_path_name().split(".")[0], dst)
    p = mi.get_editor_property("parent")
    if p and p.get_name() == "M_SM2":
        mel.set_material_instance_parent(new, master)
        for k, v in sw.items(): mel.set_material_instance_static_switch_parameter_value(new, k, v == "1")
        mel.update_material_instance(new)
    EAL.save_loaded_asset(new)
print("R96MTL", folder, len(mesh.get_editor_property("materials")))
