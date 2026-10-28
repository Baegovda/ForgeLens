# ue_cdiag.py <asset> <set> [physics.json]: run CreateChaosCloth on an unsaved duplicate and print the per-cloth DIAG lines (nothing saved).
# mtl1-31: optional 3rd arg = a physics.json path (A/B variants of the sidecar on the same mesh).
import sys, unreal
name, SET = sys.argv[1], sys.argv[2]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/SM2_Work/Tests/%s/%s" % (SET, name)
pj = sys.argv[3] if len(sys.argv) > 3 else W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name)
print("PJ", pj)
EAL = unreal.EditorAssetLibrary; dst = "/Game/SM2_Work/Tests/diag_unsaved/" + name + "_diag"
m = unreal.load_asset(dst) if EAL.does_asset_exist(dst) else EAL.duplicate_asset(folder + "/" + name, dst)
r = unreal.SM2PhysicsSetupLibrary.create_chaos_cloth(m, pj)
rep = r[-1] if isinstance(r, tuple) else ""
for l in str(rep).split(" | "): print("CD", l)
