# tune_rb.py <asset> <set> <space 0/1/2> <base> <worldA> <dampA> <maxLinVel> <maxLinAcc> <maxAngVel> <maxAngAcc>
import sys, unreal
n, SET = sys.argv[1], sys.argv[2]; sp = int(sys.argv[3]); base = sys.argv[4]; v = [float(x) for x in sys.argv[5:11]]
m = unreal.load_asset("/Game/ForgeLens/SM2/Tests/%s/%s/%s" % (SET, n, n))
cls = m.get_editor_property("post_process_anim_blueprint")
abp = unreal.load_asset(cls.get_path_name().rsplit(".", 1)[0]) if cls else None
print("TUNE abp", abp.get_path_name() if abp else None)
r = unreal.SM2PhysicsSetupLibrary.tune_rigid_body(abp, sp, base, *v)
print("TUNE", r)
unreal.EditorAssetLibrary.save_loaded_asset(abp)
