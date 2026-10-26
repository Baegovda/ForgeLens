# Probe: does the imported clip move the SM2 physics bones? usage: ue_phys_probe.py <physics.json> <anim asset> [samples=60]
# Prints, per group (jiggle / cloth helpers per sim mesh), the max local-rotation swing (deg) and max local-translation
# swing (cm) over the clip, relative to frame 0, plus the sequence frame rate and key count.
import sys, json, math, unreal
data = json.load(open(sys.argv[1], encoding="utf-8"))
seq = unreal.load_asset(sys.argv[2]); N = int(sys.argv[3]) if len(sys.argv) > 3 else 60
X = unreal.AnimPoseExtensions; LS = unreal.AnimPoseSpaces.LOCAL
L = seq.get_play_length()
try:
    ctrl = seq.controller if hasattr(seq, "controller") else None
except Exception: ctrl = None
fr = None
for attr in ("get_sampling_frame_rate",):
    try: fr = getattr(unreal.AnimationLibrary, attr)(seq)
    except Exception: pass
try: nk = unreal.AnimationLibrary.get_num_keys(seq)
except Exception: nk = None
try: nf = unreal.AnimationLibrary.get_num_frames(seq)
except Exception: nf = None
print("PROBE seq %s len %.3f frames %s keys %s rate %s" % (seq.get_name(), L, nf, nk, fr and (fr.numerator, fr.denominator)))
groups = {"jiggle": [j.get("Name") or j.get("name") for j in data["jiggles"] if j.get("found")]}
for c in data["cloths"]: groups[c["SimMeshName"]] = [h["bone"] for h in c.get("helperBones") or []]
opts = unreal.AnimPoseEvaluationOptions(); opts.set_editor_property("evaluation_type", unreal.AnimDataEvalType.RAW)
poses = []
for i in range(N + 1):
    t = L * i / N
    poses.append(X.get_anim_pose_at_time(seq, t, opts))
def qang(a, b):
    d = abs(a.x*b.x + a.y*b.y + a.z*b.z + a.w*b.w); d = min(1.0, d); return math.degrees(2*math.acos(d))
for g, bones in groups.items():
    rmax = 0.0; tmax = 0.0; moving = 0; worst = ""
    for b in bones:
        p0 = X.get_bone_pose(poses[0], b, LS); br = 0.0; bt = 0.0
        for p in poses[1:]:
            q = X.get_bone_pose(p, b, LS)
            br = max(br, qang(p0.rotation, q.rotation)); bt = max(bt, (q.translation - p0.translation).length())
        if br > 0.5 or bt > 0.2: moving += 1
        if br > rmax: rmax = br; worst = b
        tmax = max(tmax, bt)
    print("PROBE %-24s bones %4d moving %4d maxRot %6.1f deg (%s) maxTrans %6.1f cm" % (g, len(bones), moving, rmax, worst, tmax))
