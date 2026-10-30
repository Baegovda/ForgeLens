# drive.py <asset> <set> <stiffness> <damping> <swingScale>: back up PA_<asset>_SM2 (_bakN), TuneConstraintDrive, save. Bone-mode sag fix.
import sys, unreal
name, SET, k, dmp, sw = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
EAL = unreal.EditorAssetLibrary; folder = "/Game/ForgeLens/SM2/Tests/%s/%s" % (SET, name)
pas = [a.split(".")[0] for a in EAL.list_assets(folder, False, False) if "/PA_" in a and "_bak" not in a and "ChaosCloth" not in a]
for p in pas:
    n = 1
    while EAL.does_asset_exist(p + "_bak%d" % n): n += 1
    EAL.duplicate_asset(p, p + "_bak%d" % n); EAL.save_asset(p + "_bak%d" % n, only_if_is_dirty=False)
    r = unreal.SM2PhysicsSetupLibrary.tune_constraint_drive(unreal.load_asset(p), k, dmp, sw)
    EAL.save_asset(p, only_if_is_dirty=False); print("DRIVE", p, "bak%d" % n, r)
