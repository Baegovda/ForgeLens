# mtl1-43: retarget-pose finger curl match (generic rule, Manny -> SM2 RTG). usage: sys.argv = [x, mode, ...]
#   Rule: per finger, bend fing2+fing3 (half each) about the finger plane normal by (Manny ref curl - SM2 bind curl),
#   curl = angle between the proximal (hand->fing2) and distal (fing3->eff) segment. Cadian idle index curl 40->52 (Manny 55).
#   HEURISTIC (Manny reference pose as target, not game data). Writes a NEW RTG (old kept as _bakN via keep_old).
#   probe <sm2 mesh>                         per-finger curl Manny ref vs SM2 bind (deg)
#   make  <src rtg> <dest folder> <name>     duplicate the RTG into dest (new asset) and apply the curl match
#   run   <dest folder> <name> <manny anim>...  batch + eval
import sys, math, unreal
sys.path.insert(0, r"C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts")
import importlib, ue_sm2_retarget as R
X = unreal.AnimPoseExtensions; WS = unreal.AnimPoseSpaces.WORLD; EAL = unreal.EditorAssetLibrary
FING = ((2, "index"), (3, "middle"), (4, "ring"), (5, "pinky"))
def P(pose, b): return X.get_bone_pose(pose, b, WS)
def sub(a, b): return unreal.Vector(a.x - b.x, a.y - b.y, a.z - b.z)
def ang(u, v):
    d = (u.x*v.x + u.y*v.y + u.z*v.z) / max(1e-9, u.length() * v.length()); return math.degrees(math.acos(max(-1, min(1, d))))
def curls(sm2_skel):
    m = X.get_reference_pose(unreal.load_asset(R.MANNY_MESH).skeleton); s = X.get_reference_pose(sm2_skel)
    out = []
    for side in "LR":
        ms = side.lower()
        for n, f in FING:
            mb = [f"hand_{ms}", f"{f}_01_{ms}", f"{f}_02_{ms}", f"{f}_03_{ms}"]
            sb = [f"HAND{side}", f"{side}{n}_fing2", f"{side}{n}_fing3", f"{side}{n}FINGeff"]
            mp = [P(m, b).translation for b in mb]; sp = [P(s, b).translation for b in sb]
            cm = ang(sub(mp[1], mp[0]), sub(mp[3], mp[2])); cs = ang(sub(sp[1], sp[0]), sub(sp[3], sp[2]))
            ax = sub(sp[1], sp[0]).cross(sub(sp[3], sp[2]))
            out.append((side, n, f, cm, cs, sb, ax, s))
    return out
def apply(c, skel, gain=1.0):
    T = R.enum("RetargetSourceOrTarget", "TARGET")
    for side, n, f, cm, cs, sb, ax, pose in curls(skel):
        g = (cm - cs) * gain
        if ax.length() < 1e-6: print("SKIP straight", sb[1]); continue
        ax = ax * (1.0 / ax.length())
        for b in sb[1:3]:
            la = P(pose, b).rotation.unrotate_vector(ax)  # bend axis in the bone's local frame (offset is local)
            h = math.radians(g / 2) / 2
            q = unreal.Quat(la.x * math.sin(h), la.y * math.sin(h), la.z * math.sin(h), math.cos(h))
            c.set_rotation_offset_for_retarget_pose_bone(b, q, T)
        print("CURL %s%d %s manny %.1f sm2 %.1f -> +%.1f deg over fing2/fing3" % (side, n, f, cm, cs, g))
mode = sys.argv[1]
if mode == "probe":
    for side, n, f, cm, cs, *_ in curls(unreal.load_asset(sys.argv[2]).skeleton): print("PROBE %s%d %-6s manny %.1f sm2 %.1f gap %.1f" % (side, n, f, cm, cs, cm - cs))
elif mode == "make":
    src, dest, name = sys.argv[2:5]; gain = float(sys.argv[5]) if len(sys.argv) > 5 else 1.0
    p = dest + "/RTG_Manny_to_" + name
    if EAL.does_asset_exist(p): R.keep_old(p)
    EAL.make_directory(dest); rtg = EAL.duplicate_asset(src, p)
    c = unreal.IKRetargeterController.get_controller(rtg)
    mesh = c.get_preview_mesh(R.enum("RetargetSourceOrTarget", "TARGET"))
    apply(c, mesh.skeleton, gain); EAL.save_loaded_asset(rtg); print("MADE", p)
elif mode == "run":
    dest, name = sys.argv[2:4]; anims = sys.argv[4:]
    paths = R.batch(dest, name, anims)
    for p in paths: EAL.save_asset(p, only_if_is_dirty=False)
    src = {a.split("/")[-1].split(".")[0]: a for a in anims}  # evaluate reads the original Manny clips (read-only)
    for p in paths:
        base = p.split("/")[-1].split(".")[0].rsplit("_" + name, 1)[0]
        R.evaluate(src.get(base, anims[0]), p)
