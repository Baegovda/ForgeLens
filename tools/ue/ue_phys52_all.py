# ue_phys52_all.py (R52): phys52 rules (jiggle Name key, motion damper, no ComplexAsSimple) on other characters.
# Usage (EngineWire): uerun.ps1 <this> <name>[,<name>...] [probe]
# Per character: mesh /Game/ForgeLens/SM2/Characters/<n>/<n>; sidecar Exports/chars_phys52/<n>/tpl/<n>.tpl/<n>.physics.json.
# Safety: PIE must be stopped; the mesh is first duplicated to <n>_bakN (next free N, never overwritten);
# SetupFromJson creates new PA/ABP/_SM2Damper and keeps the old ones (nothing deleted). Undo: re-assign the old
# PhysicsAsset / PostProcess ABP printed as BEFORE (or copy back from <n>_bakN).
import sys, json, os, unreal
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"
EAL = unreal.EditorAssetLibrary
args = [a for a in sys.argv[1:] if not a.startswith("-")]
toks = [t for a in args for t in a.split(",") if t]  # uerun.ps1 joins args with commas
probe = "probe" in toks
names = [t for t in toks if t != "probe"]
try:
    if unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).is_in_play_in_editor():
        print("R52 ABORT PIE running"); names = []
except Exception as ex:
    print("R52 PIE check err", ex)
lib = getattr(unreal, "SM2PhysicsSetupLibrary", None)
print("R52 plugin", lib is not None)
for n in names:
    folder = "/Game/ForgeLens/SM2/Characters/" + n
    pj = W + "/Exports/chars_phys52/%s/tpl/%s.tpl/%s.physics.json" % (n, n, n)
    mesh = unreal.load_asset(folder + "/" + n)
    if mesh is None or not os.path.exists(pj):
        print("R52 SKIP", n, "mesh", mesh is not None, "json", os.path.exists(pj)); continue
    pa0 = mesh.get_editor_property("physics_asset"); abp0 = mesh.get_editor_property("post_process_anim_blueprint")
    print("R52 BEFORE", n, "pa", pa0.get_path_name() if pa0 else None, "abp", abp0.get_path_name() if abp0 else None)
    if probe or lib is None:
        continue
    k = 1
    while EAL.does_asset_exist("%s/%s_bak%d" % (folder, n, k)):
        k += 1
    bak = "%s/%s_bak%d" % (folder, n, k)
    print("R52 BACKUP", bak, EAL.duplicate_asset(folder + "/" + n, bak) is not None, EAL.save_asset(bak, only_if_is_dirty=False))
    before = set(EAL.list_assets(folder, False, False))
    print("R52 SETUP", n, lib.setup_from_json(mesh, pj, folder))
    abpc = mesh.get_editor_property("post_process_anim_blueprint")
    if abpc:
        abp = unreal.load_asset(abpc.get_path_name().rsplit(".", 1)[0])
        print("R52 TUNE", lib.tune_rigid_body(abp, 2, "CENTRE", 0.0, 0.0, 600.0, 2000.0, 10.0, 100.0))
        EAL.save_loaded_asset(abp)
    new = [a for a in EAL.list_assets(folder, False, False) if a not in before]
    for a in new:
        EAL.save_asset(a, only_if_is_dirty=True)
    EAL.save_loaded_asset(mesh)
    pa = mesh.get_editor_property("physics_asset")
    d = json.load(open(pj, encoding="utf-8"))
    print("R52 AFTER", n, "pa", pa.get_path_name() if pa else None, "new", len(new), "jiggles", len(d.get("jiggles", [])), "cloths", len(d.get("cloths", [])))
