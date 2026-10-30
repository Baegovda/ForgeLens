# resetup.py <asset> <set>: rerun SetupFromJson (new PA/ABP, old ones kept) + RigidBody tune on the existing imported mesh. Saves.
import sys, unreal
name, SET = sys.argv[1], sys.argv[2]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/ForgeLens/SM2/Tests/%s/%s" % (SET, name)
pj = W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name)
lib = unreal.SM2PhysicsSetupLibrary; mesh = unreal.load_asset(folder + "/" + name)
r = lib.setup_from_json(mesh, pj, folder); print("RS setup", str(r)[:400])
abpc = mesh.get_editor_property("post_process_anim_blueprint")
abp = unreal.load_asset(abpc.get_path_name().rsplit(".", 1)[0])
print("RS tune", lib.tune_rigid_body(abp, 2, "CENTRE", 0.0, 0.0, 600.0, 2000.0, 10.0, 100.0))
unreal.EditorAssetLibrary.save_loaded_asset(abp)
for a in unreal.EditorAssetLibrary.list_assets(folder, False, False):
    if "_SM2" in a.split("/")[-1]: unreal.EditorAssetLibrary.save_asset(a, only_if_is_dirty=True)
unreal.EditorAssetLibrary.save_loaded_asset(mesh)
