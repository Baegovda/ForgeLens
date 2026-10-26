# Manny -> SM2 humanoid retarget prototype (UE 5.7, Python). Builds NEW assets only; never edits the owner's
# IK_titus_deathwatch / IK_cadian_* / Mannequin assets.
# usage (UEMCP ue_python): sys.argv = ['x', mode, ...]
#   rig   <SM2 skeletal mesh> <dest folder> <name>          -> <dest>/IK_SM2_<name>  (chains named like IK_Mannequin)
#   rtg   <dest folder> <name> [source rig]                  -> <dest>/RTG_Manny_to_<name> (source default IK_Mannequin)
#   batch <dest folder> <name> <anim>...                     -> <dest>/Anims/<anim>_<name> (duplicate_and_retarget)
#   all   <SM2 skeletal mesh> <dest folder> <name> <anim>... rig + rtg + batch
#   mesh  <fbx> <dest folder>                               Interchange skeletal mesh import
#   eval  <manny anim> <sm2 anim> [...]                     hands/feet error, spine twist, finger curl, foot sliding
#   info  <rig or rtg>                                       dump chains / ops
#   config <rtg>                                             re-run op setup (fixes lost root motion), print op settings
#   unalign <rtg>                                            reset finger bones in the retarget pose (finger curl fix)
#   fingers <rtg> <FKChainRotationMode>                      finger chain FK rotation mode (experiments)
#   fbx   <anim folder> <out dir>                            export retargeted sequences to FBX
#   redirectors <folder>                                     list ObjectRedirectors (check nothing leaked into Mannequins)
# SM2 humanoids (Titus, Cadian, ... share bone names): retarget root CENTRE (pelvis), A-pose bind, Z up, faces +Y.
import os, sys, unreal

MANNY_RIG = "/Game/Characters/Mannequins/Rigs/IK_Mannequin"
MANNY_MESH = "/Game/Characters/Mannequins/Meshes/SKM_Manny"
# (Manny chain name, SM2 start, SM2 end, IK goal bone or None). L/R are expanded below.
CHAINS = [
    ("Root", "ROOT", "ROOT", None),
    ("Spine", "BACKdown", "BACKup", None),
    ("Neck", "NECK", "NECK", None),
    ("Head", "HEAD", "HEAD", None),
]
SIDE = [
    ("{S}Clavicle", "clavicle{s}", "clavicle{s}", None),
    ("{S}Arm", "ARM1{s}", "HAND{s}", "HAND{s}"),
    ("{S}Leg", "LEG1{s}", "FOOT2{s}", "FOOT2{s}"),
    ("{S}Thumb", "{f}1_fing1", "{f}1FINGeff", None),
    ("{S}IndexMetacarpal", "{f}2_fing1", "{f}2_fing1", None),
    ("{S}MiddleMetacarpal", "{f}3_fing1", "{f}3_fing1", None),
    ("{S}RingMetacarpal", "{f}4_fing1", "{f}4_fing1", None),
    ("{S}PinkyMetacarpal", "{f}5_fing1", "{f}5_fing1", None),
    ("{S}Index", "{f}2_fing2", "{f}2FINGeff", None),
    ("{S}Middle", "{f}3_fing2", "{f}3FINGeff", None),
    ("{S}Ring", "{f}4_fing2", "{f}4FINGeff", None),
    ("{S}Pinky", "{f}5_fing2", "{f}5FINGeff", None),
]
for S, s, f in (("Left", "L", "L"), ("Right", "R", "R")):
    for n, a, b, g in SIDE:
        CHAINS.append((n.format(S=S), a.format(s=s, f=f), b.format(s=s, f=f), g.format(s=s, f=f) if g else None))
GOAL_NAME = {"HANDL": "LeftHandIK", "HANDR": "RightHandIK", "FOOT2L": "LeftFootIK", "FOOT2R": "RightFootIK"}

tools = unreal.AssetToolsHelpers.get_asset_tools()
EAL = unreal.EditorAssetLibrary

def new_asset(dest, name, cls, factory):
    path = dest + "/" + name
    if EAL.does_asset_exist(path):
        # never delete: keep the old one as <name>_bakN and build fresh
        n = 1
        while EAL.does_asset_exist(path + "_bak%d" % n): n += 1
        EAL.rename_asset(path, path + "_bak%d" % n); print("KEEP old as", name + "_bak%d" % n)
    return tools.create_asset(name, dest, cls, factory)

def bone_names(mesh):
    X = unreal.AnimPoseExtensions
    return [str(b) for b in X.get_bone_names(X.get_reference_pose(mesh.skeleton))]

def add_fbik(c):
    # UE 5.6+: AddSolver takes the solver struct path as a string
    for t in ("/Script/IKRig.IKRigFullBodyIKSolver", "IKRigFullBodyIKSolver", "/Script/IKRig.IKRigFBIKSolver"):
        try:
            i = c.add_solver(t)
            if i is not None and i >= 0: print("SOLVER", t, i); return i
        except Exception as e:
            print("add_solver", t, str(e).splitlines()[0])
    return -1

def build_rig(mesh_path, dest, name):
    mesh = unreal.load_asset(mesh_path)
    names = set(bone_names(mesh))
    rig = new_asset(dest, "IK_SM2_" + name, unreal.IKRigDefinition, unreal.IKRigDefinitionFactory())
    c = unreal.IKRigController.get_controller(rig)
    c.set_skeletal_mesh(mesh)
    c.set_retarget_root("CENTRE")
    si = add_fbik(c)
    if si >= 0:
        ok = False
        for f in (lambda: c.set_start_bone("CENTRE", si), lambda: c.set_root_bone("CENTRE", si)):
            try: ok = f() is not False; break
            except Exception as e: print("solver root", str(e).splitlines()[0])
    missing = []
    for chain, a, b, g in CHAINS:
        if a not in names or b not in names:
            missing.append(chain); continue
        goal = ""
        if g:
            goal = c.add_new_goal(GOAL_NAME[g], g) or GOAL_NAME[g]
            if si >= 0: c.connect_goal_to_solver(goal, si)
        c.add_retarget_chain(chain, a, b, goal)
    unreal.EditorAssetLibrary.save_loaded_asset(rig)
    print("RIG", rig.get_path_name(), "chains", len(CHAINS) - len(missing), "missing", missing, "solver", si,
          "goals", [str(x.goal_name) + ":" + str(x.bone_name) for x in c.get_all_goals()])
    return rig

def enum(name, *vals):
    e = getattr(unreal, name)
    for v in vals:
        if hasattr(e, v): return getattr(e, v)
    raise AttributeError(name + str(vals))

def build_rtg(dest, name, source_rig=MANNY_RIG):
    tgt = unreal.load_asset(dest + "/IK_SM2_" + name)
    src = unreal.load_asset(source_rig)
    rtg = new_asset(dest, "RTG_Manny_to_" + name, unreal.IKRetargeter, unreal.IKRetargetFactory())
    c = unreal.IKRetargeterController.get_controller(rtg)
    SRC = enum("RetargetSourceOrTarget", "SOURCE"); TGT = enum("RetargetSourceOrTarget", "TARGET")
    c.set_ik_rig(SRC, src); c.set_ik_rig(TGT, tgt)
    c.set_preview_mesh(SRC, unreal.load_asset(MANNY_MESH))
    c.set_preview_mesh(TGT, unreal.IKRigController.get_controller(tgt).get_skeletal_mesh())
    if c.get_num_retarget_ops() == 0:  # the factory already adds the default stack in 5.7
        c.add_default_ops()
    try: c.assign_ik_rig_to_all_ops(TGT, tgt); c.assign_ik_rig_to_all_ops(SRC, src)
    except Exception as e: print("assign_ik_rig_to_all_ops", e)
    try: c.auto_map_chains(enum("AutoMapChainType", "EXACT"), True)
    except Exception as e: print("auto_map_chains", e)
    # Retarget pose: both are A-poses with different arm angles (Manny ~45 deg down, SM2 ~33 deg) and SM2 forearms
    # bent forward. Make a target retarget pose and auto-align every mapped bone to the source directions.
    try:
        c.create_retarget_pose("SM2_AutoAlign", TGT)
        c.set_current_retarget_pose("SM2_AutoAlign", TGT)
        c.auto_align_all_bones(TGT)
        unalign_fingers(c, tgt_mesh_for_fingers(c))
        print("POSE auto-aligned SM2_AutoAlign")
    except Exception as e: print("auto_align", e)
    configure_ops(c)
    unreal.EditorAssetLibrary.save_loaded_asset(rtg)
    info_rtg(rtg)
    return rtg

def tgt_mesh_for_fingers(c):
    return c.get_preview_mesh(enum("RetargetSourceOrTarget", "TARGET"))

def unalign_fingers(c, mesh):
    # Auto-align straightens SM2 fingers onto Manny's bone directions (index curl in the retarget pose drops to
    # ~18 deg vs Manny ref 42), and retargeting copies deltas from that pose, so every clip ended up ~24 deg
    # less curled. Keep the SM2 bind-pose fingers instead (ref curl 30 deg).
    T = enum("RetargetSourceOrTarget", "TARGET")
    pose = unreal.AnimPoseExtensions.get_reference_pose(mesh.skeleton)
    bones = [b for b in unreal.AnimPoseExtensions.get_bone_names(pose) if "fing" in str(b).lower()]
    c.reset_retarget_pose(c.get_current_retarget_pose_name(T), bones, T)
    print("UNALIGN fingers", len(bones), "bones")

def op_settings(c, cls):
    for i in range(c.get_num_retarget_ops()):
        oc = c.get_op_controller(i)
        if type(oc).__name__ == cls: return oc, oc.get_settings()
    return None, None

def dump(obj, pre=""):
    for k in dir(obj):
        if k.startswith("_") or k in ("static_struct", "to_tuple", "export_text", "import_text", "assign"): continue
        try: v = obj.get_editor_property(k)
        except Exception: continue
        print(pre + k, str(v)[:90])

def configure_ops(c):
    # Root motion: Manny 'root' -> SM2 'ROOT' (bone 0 of ROOT-rooted exports), pelvis CENTRE. Without this the
    # retargeted clips lose Manny's root motion (walk 450 cm -> 0).
    import warnings
    # Ops created by the factory before the IK rigs were assigned keep unresolved bones; re-run each op's setup.
    for i in range(c.get_num_retarget_ops()):
        try: c.run_op_initial_setup(i)
        except Exception as e: print("setup", i, str(e).splitlines()[0])
    oc, st = op_settings(c, "IKRetargetRootMotionController")
    if oc:
        # 5.7: root bones come from each skeleton's bone 0 (Manny root, SM2 ROOT); the old *_root_bone fields are deprecated.
        print("OP Root Motion", {k: str(st.get_editor_property(k)) for k in ("root_motion_source", "root_height_source", "maintain_offset_from_pelvis")})
    oc, st = op_settings(c, "IKRetargetPelvisMotionController")
    if oc:
        print("OP Pelvis Motion", {k: str(st.get_editor_property(k)) for k in ("scale_horizontal", "scale_vertical", "affect_ik_horizontal", "affect_ik_vertical", "translation_alpha")})
    oc, st = op_settings(c, "IKRetargetIKChainsController")
    if oc:
        ch = st.get_editor_property("chains_to_retarget")
        print("OP IK Goals", [str(x.get_editor_property("target_chain_name")) for x in ch] if ch else ch)
        if ch: dump(ch[0], "   IK ")
    oc, st = op_settings(c, "IKRetargetFKChainsController")
    if oc:
        ch = st.get_editor_property("chains_to_retarget")
        if ch:
            print("   FK chains n", len(ch))
            x = [y for y in ch if "Index" in str(y.get_editor_property("target_chain_name"))]
            if x:
                dump(x[0], "   FK ")

def info_rtg(rtg):
    c = unreal.IKRetargeterController.get_controller(rtg)
    n = c.get_num_retarget_ops()
    print("RTG", rtg.get_path_name(), "ops", n)
    for i in range(n):
        nm = c.get_op_name(i)
        oc = None
        try: oc = c.get_op_controller(i)
        except Exception: pass
        print("  OP", i, nm, c.get_retarget_op_enabled(i), type(oc).__name__ if oc else "-")

def keep_old(path):
    if EAL.does_asset_exist(path):
        n = 1
        while EAL.does_asset_exist(path + "_bak%d" % n): n += 1
        EAL.rename_asset(path, path + "_bak%d" % n); print("KEEP old as", path.split("/")[-1] + "_bak%d" % n)

def batch(dest, name, anims):
    # duplicate_and_retarget writes next to its inputs, so first copy the Manny clips into <dest>/../MannySrc
    # (the owner's Mannequin folder is never written to), then retarget the copies into <dest>/Anims.
    rtg = unreal.load_asset(dest + "/RTG_Manny_to_" + name)
    c = unreal.IKRetargeterController.get_controller(rtg)
    tgt_mesh = c.get_preview_mesh(enum("RetargetSourceOrTarget", "TARGET"))
    src_mesh = unreal.load_asset(MANNY_MESH)
    srcdir = dest.rsplit("/", 1)[0] + "/MannySrc"; out = dest + "/Anims"
    EAL.make_directory(srcdir); EAL.make_directory(out)
    data = []
    for a in anims:
        nm = a.split("/")[-1].split(".")[0]; cp = srcdir + "/" + nm
        if not EAL.does_asset_exist(cp): EAL.duplicate_asset(a, cp)
        data.append(EAL.find_asset_data(cp))
    for d in data: keep_old(out + "/" + str(d.asset_name) + "_" + name)
    res = unreal.IKRetargetBatchOperation.duplicate_and_retarget(data, src_mesh, tgt_mesh, rtg, "", "", "", "_" + name, False, True)
    paths = []
    for r in res:
        p = str(r.package_name)
        nm = p.split("/")[-1]
        if not p.startswith(out):
            EAL.rename_asset(p, out + "/" + nm); p = out + "/" + nm
        paths.append(p)
    print("RETARGETED", paths)
    return paths

# --- eval: numeric Manny vs SM2 comparison (no actors, AnimPose only) ---
PAIRS = {"pelvis": ("pelvis", "CENTRE"), "head": ("head", "HEAD"), "hand_l": ("hand_l", "HANDL"), "hand_r": ("hand_r", "HANDR"),
         "foot_l": ("ball_l", "FOOT2L"), "foot_r": ("ball_r", "FOOT2R"), "sh_l": ("upperarm_l", "ARM1L"), "sh_r": ("upperarm_r", "ARM1R"),
         "hip_l": ("thigh_l", "LEG1L"), "hip_r": ("thigh_r", "LEG1R"), "root": ("root", "ROOT"),
         "i0": ("hand_l", "HANDL"), "i1": ("index_01_l", "L2_fing2"), "i2": ("index_02_l", "L2_fing3"), "i3": ("index_03_l", "L2FINGeff")}
def evaluate(manny_seq, sm2_seq, n=60):
    import math
    X = unreal.AnimPoseExtensions; WS = unreal.AnimPoseSpaces.WORLD
    op = unreal.AnimPoseEvaluationOptions()
    def track(seq, side):
        L = seq.get_play_length(); out = []
        for k in range(n + 1):
            p = X.get_anim_pose_at_time(seq, min(L * k / n, L * 0.9999), op)
            out.append({key: X.get_bone_pose(p, v[side], WS).translation for key, v in PAIRS.items()})
        return L, out
    Lm, M = track(unreal.load_asset(manny_seq), 0); Ls, T = track(unreal.load_asset(sm2_seq), 1)
    X_ = unreal.AnimPoseExtensions
    hm = X_.get_bone_pose(X_.get_reference_pose(unreal.load_asset(MANNY_MESH).skeleton), "pelvis", WS).translation.z
    sk = unreal.load_asset(sm2_seq).get_editor_property("skeleton")
    hs = X_.get_bone_pose(X_.get_reference_pose(sk), "CENTRE", WS).translation.z
    r = hs / hm
    sub = lambda a, b: unreal.Vector(a.x - b.x, a.y - b.y, a.z - b.z)
    yaw = lambda a, b: math.degrees(math.atan2(b.y - a.y, b.x - a.x))
    def ang(u, v):
        d = (u.x*v.x + u.y*v.y + u.z*v.z) / max(1e-6, u.length() * v.length()); return math.degrees(math.acos(max(-1, min(1, d))))
    def wrap(a): return (a + 180) % 360 - 180
    res = {"ratio": r, "len": (Lm, Ls)}
    # hands/feet/head relative to pelvis, normalized by pelvis height: error in % of pelvis height
    for key in ("hand_l", "hand_r", "foot_l", "foot_r", "head"):
        e = [sub(sub(t[key], t["pelvis"]) * (1 / hs), sub(m[key], m["pelvis"]) * (1 / hm)).length() * 100 for m, t in zip(M, T)]
        res["err_" + key] = (sum(e) / len(e), max(e))
    # spine twist: shoulder line yaw minus hip line yaw, relative to frame 0
    def twist(F, i): return wrap(yaw(F[i]["sh_l"], F[i]["sh_r"]) - yaw(F[i]["hip_l"], F[i]["hip_r"]) - (yaw(F[0]["sh_l"], F[0]["sh_r"]) - yaw(F[0]["hip_l"], F[0]["hip_r"])))
    tw = [abs(wrap(twist(T, i) - twist(M, i))) for i in range(n + 1)]
    res["twist_range"] = (max(twist(M, i) for i in range(n+1)) - min(twist(M, i) for i in range(n+1)), max(twist(T, i) for i in range(n+1)) - min(twist(T, i) for i in range(n+1)))
    res["twist_err"] = (sum(tw) / len(tw), max(tw))
    # finger curl (index): angle between hand->knuckle and middle->tip segments
    def curl(F, i): return ang(sub(F[i]["i1"], F[i]["i0"]), sub(F[i]["i3"], F[i]["i2"]))
    cm = [curl(M, i) for i in range(n + 1)]; cs = [curl(T, i) for i in range(n + 1)]
    res["curl"] = (sum(cm) / len(cm), sum(cs) / len(cs), max(abs(a - b) for a, b in zip(cm, cs)))
    # foot sliding: horizontal foot speed while planted (z within 3% of pelvis height of the clip minimum)
    def slide(F, key, h, L):
        zs = [f[key].z for f in F]; lo = min(zs); dt = L / n; v = []
        for i in range(1, n + 1):
            if F[i][key].z < lo + 0.03 * h and F[i - 1][key].z < lo + 0.03 * h:
                d = sub(F[i][key], F[i - 1][key]); v.append(math.hypot(d.x, d.y) / dt)
        return (sum(v) / len(v) if v else 0.0, len(v), lo)
    for key in ("foot_l", "foot_r"):
        sm, ss = slide(M, key, hm, Lm), slide(T, key, hs, Ls)
        res["slide_" + key] = (sm[0], ss[0], sm[1], ss[1], sm[2], ss[2])
    rm = sub(M[-1]["root"], M[0]["root"]).length(); rs = sub(T[-1]["root"], T[0]["root"]).length()
    res["rootdisp"] = (rm, rs)
    print("EVAL %s vs %s ratio %.3f len %.2f/%.2f" % (manny_seq.split("/")[-1], sm2_seq.split("/")[-1], r, Lm, Ls))
    for k in ("err_hand_l", "err_hand_r", "err_foot_l", "err_foot_r", "err_head"):
        print("  %-10s mean %.1f%%  max %.1f%% of pelvis height" % (k, res[k][0], res[k][1]))
    print("  spine twist range manny %.1f sm2 %.1f deg, |diff| mean %.1f max %.1f" % (res["twist_range"] + res["twist_err"]))
    print("  index curl mean manny %.1f sm2 %.1f deg, max |diff| %.1f" % res["curl"])
    for key in ("foot_l", "foot_r"):
        a = res["slide_" + key]
        print("  %s planted speed manny %.1f cm/s (%d fr) sm2 %.1f cm/s (%d fr), expected sm2 = manny*ratio %.1f; min z manny %.1f sm2 %.1f" % (key, a[0], a[2], a[1], a[3], a[0] * r, a[4], a[5]))
    print("  root disp manny %.1f sm2 %.1f (expected %.1f)" % (rm, rs, rm * r))
    return res

if __name__ == "__main__":
    mode = sys.argv[1]
    if mode == "rig": build_rig(sys.argv[2], sys.argv[3], sys.argv[4])
    elif mode == "rtg": build_rtg(sys.argv[2], sys.argv[3], *(sys.argv[4:5]))
    elif mode == "batch": batch(sys.argv[2], sys.argv[3], sys.argv[4:])
    elif mode == "all":
        build_rig(sys.argv[2], sys.argv[3], sys.argv[4]); build_rtg(sys.argv[3], sys.argv[4]); batch(sys.argv[3], sys.argv[4], sys.argv[5:])
    elif mode == "mesh":
        # mesh <fbx> <dest>: Interchange skeletal mesh import (ROOT-rooted fbx-unreal export), no materials/anims
        fbx, dest = sys.argv[2], sys.argv[3]
        unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
        t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = dest
        t.automated = True; t.replace_existing = False; t.save = True
        o = unreal.FbxImportUI(); o.import_mesh = True; o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
        o.import_materials = False; o.import_textures = False; o.import_animations = False; t.options = o
        tools.import_asset_tasks([t]); print("MESH", list(t.imported_object_paths))
    elif mode == "eval":
        # eval <manny anim> <sm2 anim> [...pairs]
        a = sys.argv[2:]
        for i in range(0, len(a) - 1, 2): evaluate(a[i], a[i + 1])
    elif mode == "fbx":
        # fbx <anim folder> <out dir>: export every AnimSequence (no _bakN) in the folder to FBX
        out = sys.argv[3]; os.makedirs(out, exist_ok=True)
        for p in EAL.list_assets(sys.argv[2], recursive=False):
            a = unreal.load_asset(p)
            if not isinstance(a, unreal.AnimSequence) or "_bak" in a.get_name(): continue
            t = unreal.AssetExportTask(); t.object = a; t.filename = os.path.join(out, a.get_name() + ".fbx")
            t.automated = True; t.prompt = False; t.replace_identical = True
            o = unreal.FbxExportOption(); o.export_morph_targets = False; t.options = o
            ok = unreal.Exporter.run_asset_export_task(t)
            print("FBX", a.get_name(), ok, os.path.getsize(t.filename) if os.path.exists(t.filename) else -1)
    elif mode == "unalign":
        a = unreal.load_asset(sys.argv[2]); c = unreal.IKRetargeterController.get_controller(a)
        unalign_fingers(c, tgt_mesh_for_fingers(c)); EAL.save_loaded_asset(a)
    elif mode == "fingers":
        # fingers <rtg> <FKChainRotationMode name>: switch only the finger/thumb FK chains (curl tuning)
        a = unreal.load_asset(sys.argv[2]); c = unreal.IKRetargeterController.get_controller(a)
        oc, st = op_settings(c, "IKRetargetFKChainsController")
        ch = st.get_editor_property("chains_to_retarget"); m = getattr(unreal.FKChainRotationMode, sys.argv[3]); n = 0
        for i in range(len(ch)):
            x = ch[i]  # struct elements come back by value: write them back
            t = str(x.get_editor_property("target_chain_name"))
            if any(k in t for k in ("Thumb", "Index", "Middle", "Ring", "Pinky")) and "Metacarpal" not in t:
                x.set_editor_property("rotation_mode", m); ch[i] = x; n += 1
        st.set_editor_property("chains_to_retarget", ch); oc.set_settings(st); EAL.save_loaded_asset(a)
        print("FINGERS", sys.argv[3], n, "chains")
    elif mode == "redirectors":
        reg = unreal.AssetRegistryHelpers.get_asset_registry()
        f = unreal.ARFilter(package_paths=[sys.argv[2]], class_paths=[unreal.TopLevelAssetPath("/Script/CoreUObject", "ObjectRedirector")], recursive_paths=True)
        for a in reg.get_assets(f): print("REDIRECTOR", a.package_name)
    elif mode == "info":
        a = unreal.load_asset(sys.argv[2])
        if isinstance(a, unreal.IKRetargeter): info_rtg(a)
    elif mode == "config":
        # config <rtg>: re-apply configure_ops to an existing retargeter we built
        a = unreal.load_asset(sys.argv[2]); configure_ops(unreal.IKRetargeterController.get_controller(a)); EAL.save_loaded_asset(a)
    elif mode == "manny":
        reg = unreal.AssetRegistryHelpers.get_asset_registry()
        for a in EAL.list_assets("/Game/Characters/Mannequins/Anims", True, False):
            print("A", a)
