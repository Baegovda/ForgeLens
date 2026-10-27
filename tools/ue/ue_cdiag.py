# cdiag.py <asset> <set>: run CreateChaosCloth on an unsaved duplicate and print the per-cloth DIAG lines (nothing saved).
import sys, unreal
name, SET = sys.argv[1], sys.argv[2]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/SM2_Work/Tests/%s/%s" % (SET, name)
pj = W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name)
print("HASDRIVE", hasattr(unreal.SM2PhysicsSetupLibrary, "tune_constraint_drive"))
EAL = unreal.EditorAssetLibrary; dst = "/Game/SM2_Work/Tests/diag_unsaved/" + name + "_diag"
m = unreal.load_asset(dst) if EAL.does_asset_exist(dst) else EAL.duplicate_asset(folder + "/" + name, dst)
r = unreal.SM2PhysicsSetupLibrary.create_chaos_cloth(m, pj)
rep = r[-1] if isinstance(r, tuple) else ""
for l in str(rep).split(" | "): print("CD", l)
