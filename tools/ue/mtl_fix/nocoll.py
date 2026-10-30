# mtl1-13b baseline: <name> [set]: duplicate <name>_ChaosCloth -> <name>_ChaosCloth_NoColl with the cloth collision cleared.
# Writes the collision PA path to Captures/mtl_fix/<name>_collpa.txt so the penetration metric can test the NoColl twin against it.
import sys, unreal
name = sys.argv[1]; SET = sys.argv[2] if len(sys.argv) > 2 else "chars_cloth"
EAL = unreal.EditorAssetLibrary; lib = unreal.SM2PhysicsSetupLibrary
src = "/Game/ForgeLens/SM2/Tests/%s/%s/%s_ChaosCloth" % (SET, name, name); dst = src + "_NoColl"
if EAL.does_asset_exist(dst):
    n = 1
    while EAL.does_asset_exist(dst + "_bak%d" % n): n += 1
    EAL.rename_asset(dst, dst + "_bak%d" % n); EAL.save_asset(dst + "_bak%d" % n, only_if_is_dirty=False)
m = EAL.duplicate_asset(src, dst); pa = lib.set_cloth_collision(m, None); EAL.save_asset(dst, only_if_is_dirty=False)
p = pa.get_path_name() if pa else ""
open(r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Captures/mtl_fix/%s_collpa.txt" % name, "w").write(p); print("NOCOLL", dst, "collPA", p)
