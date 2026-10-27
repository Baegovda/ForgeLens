# probe Manny anim: world root/pelvis/foot/head at several times. args: <asset path>
import sys, unreal
seq = unreal.load_asset(sys.argv[1]); L = unreal.AnimPoseExtensions
o = unreal.AnimPoseEvaluationOptions(); o.set_editor_property("evaluation_type", unreal.AnimDataEvalType.RAW)
d = seq.get_play_length()
for f in (0, .25, .5, .75):
    p = L.get_anim_pose_at_time(seq, f * d, o); out = []
    for b in (sys.argv[4].split(",") if len(sys.argv) > 4 else ("root", "pelvis", "spine_05", "head", "foot_l", "foot_r")):
        t = L.get_bone_pose(p, b, unreal.AnimPoseSpaces.WORLD)
        v = t.translation; r = t.rotation.rotator()
        out.append("%s=(%.0f %.0f %.0f|%.0f %.0f %.0f)" % (b, v.x, v.y, v.z, r.roll, r.pitch, r.yaw))
    print("MPROBE t=%.2f %s" % (f * d, " ".join(out)))
