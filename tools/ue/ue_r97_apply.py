# ue_r97_apply.py (R97a): give an existing imported character the R97 .td values without re-import.
# Backs up every MI to /Game/SM2_Work/_bak_r97/<char>/ (first run only), builds M_SM2_r97 next to the mesh,
# re-parents M_SM2* MIs to it and runs apply_td_scalars from the sidecar. Undo: copy the backups back (old masters are kept).
# uerun.ps1 ue_r97_apply.py <import_sm2_materials.py> <meshpath> <materials.json>
import unreal, sys, json
scr, mp, sc = sys.argv[1:4]; argv = sys.argv; sys.argv = [scr]; g = {"__name__": "ism_r97a"}
exec(compile(open(scr, encoding="utf-8").read().split("\nif __name__")[0], scr, "exec"), g); sys.argv = argv
EAL = unreal.EditorAssetLibrary; mel = unreal.MaterialEditingLibrary
mesh = unreal.load_asset(mp); folder = mp.rsplit("/", 1)[0]; char = mp.rsplit("/", 1)[1]
bak = "/Game/SM2_Work/_bak_r97/" + folder.strip("/").replace("/", "_")
master = g["make_master"](folder, "M_SM2_r97"); mel.recompile_material(master); EAL.save_loaded_asset(master)
slots = {s["slot"]: s for s in json.load(open(sc, encoding="utf-8"))["slots"]}
n = t = 0
for sm in mesh.get_editor_property("materials"):
    mi = sm.get_editor_property("material_interface"); name = str(sm.get_editor_property("material_slot_name"))
    if not isinstance(mi, unreal.MaterialInstanceConstant): continue
    par = mi.get_editor_property("parent")
    if not (par and par.get_name().startswith("M_SM2")): continue
    src = mi.get_path_name().split(".")[0]; b = bak + "/" + mi.get_name()
    if not EAL.does_asset_exist(b): EAL.duplicate_asset(src, b)
    mel.set_material_instance_parent(mi, master); n += 1
    if name in slots and slots[name].get("tdScalars"): g["apply_td_scalars"](mi, slots[name]); t += 1
    mel.update_material_instance(mi); EAL.save_loaded_asset(mi)
print("R97APPLY", mp, "reparented", n, "td", t, "backup", bak)
