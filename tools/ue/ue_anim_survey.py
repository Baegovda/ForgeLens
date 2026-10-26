# Animation survey (read-mostly). Everything goes to /Game/SM2_Work/Tests/anim_survey/<asset>/.
# usage (ue_python argv):
#   import <export dir>/tpl/<asset>.tpl <asset> [mesh|noyaw|yaw180|noroot|sub=<Name>|root=<Name>]
#        mesh import only when missing (Interchange) into <root or asset>/; anims via sm2_anim_import (auto yaw, root motion on)
#        into <root or asset>/<sub> (default Anims_auto; noyaw -> Anims_noyaw yaw 0, yaw180 -> Anims yaw 180)
#   check <AnimSequence> <SkeletalMesh>                  numeric pose sanity (component/world space via AnimPose)
#   bones <SkeletalMesh>                                 index, name, parent, ref world position
#   pose <SkeletalMesh> <AnimSequence>  then  capture <out dir> <name>  then  export    (contact sheet, 5 poses in a row)
#   mannequin                                            look for Manny/Quinn/IK Rig/retargeter assets
import sys, os, math, builtins, unreal
# --- sm2_anim_import (same block in ue_import_anim.py, ue_anim_survey.py, ue_sani_fix.py) ---
# Legacy FBX anim import onto an existing skeleton. yaw="auto": import at yaw 0, then compare skeleton bone 0
# (component space) at t=0 with the skeleton's reference pose; when they differ by a multiple of 90 deg, reimport
# with that yaw. Interchange meshes (incl. the old {mesh}_Skeleton Null, and ROOT-rooted exports) need 0; the
# older Original/cc_titus_assault skeleton needs 180. root_motion: bone 0 carries the 0xF4 root motion in
# ROOT-rooted exports, so enable it with the lock on the first frame.
def sm2_anim_import(fbx, skeleton, dest, yaw="auto", root_motion=True):
    import unreal
    def one(y):
        unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 0")
        try:
            t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = dest
            t.automated = True; t.replace_existing = True; t.save = True
            o = unreal.FbxImportUI(); o.import_mesh = False; o.import_animations = True
            o.import_materials = False; o.import_textures = False; o.skeleton = skeleton
            o.mesh_type_to_import = unreal.FBXImportType.FBXIT_ANIMATION
            o.anim_sequence_import_data.set_editor_property("import_rotation", unreal.Rotator(roll=0, pitch=0, yaw=y))
            t.options = o
            unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
        finally:
            unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
        return [str(p) for p in t.imported_object_paths]
    y = 0.0 if yaw == "auto" else float(yaw)
    paths = one(y)
    seqs = [a for a in (unreal.load_asset(p) for p in paths) if isinstance(a, unreal.AnimSequence)]
    if yaw == "auto" and seqs:
        X = unreal.AnimPoseExtensions; WS = unreal.AnimPoseSpaces.WORLD
        ref = X.get_reference_pose(skeleton); b0 = X.get_bone_names(ref)[0]
        rr = X.get_bone_pose(ref, b0, WS).rotation.rotator().yaw
        ar = X.get_bone_pose(X.get_anim_pose_at_time(seqs[0], 0.0, unreal.AnimPoseEvaluationOptions()), b0, WS).rotation.rotator().yaw
        d = (rr - ar) % 360.0; snap = (round(d / 90.0) * 90) % 360
        off = abs(((d - snap) + 180.0) % 360.0 - 180.0)
        print("SM2YAW bone0 %s ref %.1f anim@0 %.1f diff %.1f -> %s" % (b0, rr, ar, d, ("reimport yaw %d" % snap) if snap and off < 10 else ("keep %g" % y if off < 10 else "keep %g (not a 90 multiple, check)" % y)))
        if snap and off < 10:
            y = float(snap); paths = one(y)
            seqs = [a for a in (unreal.load_asset(p) for p in paths) if isinstance(a, unreal.AnimSequence)]
    if root_motion:
        for s in seqs:
            s.set_editor_property("enable_root_motion", True)
            s.set_editor_property("root_motion_root_lock", unreal.RootMotionRootLock.ANIM_FIRST_FRAME)
            unreal.EditorAssetLibrary.save_loaded_asset(s)
    return paths, y
# --- end sm2_anim_import ---
W = "/Game/SM2_Work/Tests/anim_survey/"
mode = sys.argv[1]
X = unreal.AnimPoseExtensions
SP = unreal.AnimPoseSpaces
tools = unreal.AssetToolsHelpers.get_asset_tools()
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def world(): return unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
HIDE = ("cdt", "_glr", "rb_", "_lod", "nav_silhouette")
if mode == "import":
    d, asset = sys.argv[2], sys.argv[3]
    flags = sys.argv[4:]
    kv = dict(f.split("=", 1) for f in flags if "=" in f)
    dest = W + kv.get("root", asset); mesh_path = dest + "/" + asset
    if not unreal.EditorAssetLibrary.does_asset_exist(mesh_path):
        unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
        t = unreal.AssetImportTask(); t.filename = d + "/" + asset + ".fbx"; t.destination_path = dest
        t.automated = True; t.replace_existing = False; t.save = True
        o = unreal.FbxImportUI(); o.import_mesh = True; o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
        o.import_materials = False; o.import_textures = False; o.import_animations = False; t.options = o
        tools.import_asset_tasks([t]); print("MESH", list(t.imported_object_paths))
    else:
        print("MESHKEEP", mesh_path)
    if "mesh" in flags: raise SystemExit
    yaw = "0" if "noyaw" in flags else ("180" if "yaw180" in flags else "auto")
    sub = "/" + kv.get("sub", {"0": "Anims_noyaw", "180": "Anims"}.get(yaw, "Anims_auto"))
    sk = unreal.load_asset(mesh_path).skeleton
    print("SKEL bone0", unreal.AnimPoseExtensions.get_bone_names(unreal.AnimPoseExtensions.get_reference_pose(sk))[0])
    for f in sorted(os.listdir(d)):
        if not (f.startswith(asset + "__") and f.endswith(".fbx")): continue
        paths, used = sm2_anim_import(d + "/" + f, sk, dest + sub, yaw, "noroot" not in flags)
        print("ANIM", f, paths, "yaw", used)
elif mode == "check":
    seq = unreal.load_asset(sys.argv[2]); mesh = unreal.load_asset(sys.argv[3])
    AL = unreal.AnimationLibrary
    L = seq.get_play_length()
    try: nf = AL.get_num_frames(seq)
    except Exception as e: nf = "?"
    try: fr = AL.get_frame_rate(seq); fr = "%g" % (fr.numerator / fr.denominator)
    except Exception: fr = "?"
    try: tracks = len(AL.get_animation_track_names(seq))
    except Exception: tracks = "?"
    gp = lambda k: (lambda: seq.get_editor_property(k))
    props = {}
    for k in ("enable_root_motion", "root_motion_root_lock", "force_root_lock", "additive_anim_type", "ref_pose_type", "interpolation"):
        try: props[k] = str(seq.get_editor_property(k)).replace("RootMotionRootLock.", "").replace("AdditiveAnimationType.", "")
        except Exception: props[k] = "?"
    skel = seq.get_editor_property("skeleton")
    print("SEQ %s len %.3f frames %s fps %s tracks %s skeleton %s meshSkeleton %s" % (seq.get_name(), L, nf, fr, tracks, skel.get_path_name(), mesh.skeleton.get_path_name()))
    print("PROPS", props)
    op = unreal.AnimPoseEvaluationOptions()
    try: op.set_editor_property("evaluation_type", unreal.AnimPoseEvaluationType.SOURCE)
    except Exception: pass
    ref = X.get_reference_pose(skel)
    names = [str(b) for b in X.get_bone_names(ref)]
    reflen = {b: X.get_bone_pose(ref, b, SP.LOCAL).translation.length() for b in names}
    n = max(2, int(round(L * 30)))
    feet = [b for b in names if any(k in b.lower() for k in ("foot", "toe", "ankle", "ball"))][:8]
    head = next((b for b in names if "head" in b.lower()), None)
    nan = 0; dev = {}; step = (0, "", 0); prevq = None; fz = [1e9, -1e9]; root = names[0]
    rpos = []; rows = []
    for k in range(n + 1):
        t = min(L * k / n, L * 0.9999)
        p = X.get_anim_pose_at_time(seq, t, op)
        q = {}
        for b in names:
            lt = X.get_bone_pose(p, b, SP.LOCAL)
            v = lt.translation; r = lt.rotation
            if any(math.isnan(c) for c in (v.x, v.y, v.z, r.x, r.y, r.z, r.w)): nan += 1; continue
            if b != root: dev[b] = max(dev.get(b, 0), abs(v.length() - reflen[b]))
            q[b] = r
        if prevq:
            for b, r in q.items():
                if b in prevq:
                    a = prevq[b]; dot = abs(a.x*r.x + a.y*r.y + a.z*r.z + a.w*r.w)
                    ang = math.degrees(2 * math.acos(min(1, dot)))
                    if ang > step[0]: step = (ang, b, k)
        prevq = q
        rw = X.get_bone_pose(p, root, SP.WORLD).translation; rpos.append(rw)
        for b in feet:
            z = X.get_bone_pose(p, b, SP.WORLD).translation.z; fz[0] = min(fz[0], z); fz[1] = max(fz[1], z)
        if k in [round(n * f) for f in (0, .25, .5, .75, 1)]:
            hd = X.get_bone_pose(p, head, SP.WORLD).translation if head else None
            fts = [X.get_bone_pose(p, b, SP.WORLD).translation for b in feet[:2]]
            rows.append("  t=%.2f root(%.0f,%.0f,%.0f) head(%s) feet[%s]" % (t, rw.x, rw.y, rw.z, "%.0f,%.0f,%.0f" % (hd.x, hd.y, hd.z) if hd else "-", " ".join("%.0f,%.0f,%.0f" % (f.x, f.y, f.z) for f in fts)))
    bad = sorted(((v, b) for b, v in dev.items() if v > 1.0), reverse=True)
    d = math.dist((rpos[0].x, rpos[0].y, rpos[0].z), (rpos[-1].x, rpos[-1].y, rpos[-1].z))
    print("POSE samples %d nanBones %d bonesLenDev>1cm %d max %s maxRotStep %.1fdeg(%s@%d/%d) feet%s z[%.1f..%.1f] rootDisp %.1fcm (%.0f,%.0f,%.0f)->(%.0f,%.0f,%.0f)" % (
        n + 1, nan, len(bad), ("%.1f %s" % bad[0]) if bad else "-", step[0], step[1], step[2], n, feet[:4], fz[0], fz[1], d,
        rpos[0].x, rpos[0].y, rpos[0].z, rpos[-1].x, rpos[-1].y, rpos[-1].z))
    if bad: print("LENDEV", ", ".join("%s %.1f" % (b, v) for v, b in bad[:8]))
    print("\n".join(rows))
    # Root motion as UE sees it: bone 0's root track (AnimationLibrary.extract_root_track_transform), not a child bone.
    try:
        a = AL.extract_root_track_transform(seq, 0.0)
        b = AL.extract_root_track_transform(seq, L)
        dv = b.translation - a.translation
        print("ROOTMOTION bone0 %s enabled %s lock %s  t0 (%.1f,%.1f,%.1f) yaw %.1f  tEnd (%.1f,%.1f,%.1f) yaw %.1f  dist %.1fcm over %.3fs  dyaw %.1f" % (
            root, AL.is_root_motion_enabled(seq), AL.get_root_motion_lock_type(seq), a.translation.x, a.translation.y, a.translation.z, a.rotation.rotator().yaw,
            b.translation.x, b.translation.y, b.translation.z, b.rotation.rotator().yaw, dv.length(), L, b.rotation.rotator().yaw - a.rotation.rotator().yaw))
    except Exception as e:
        print("ROOTMOTION n/a", e)
    # With root motion extracted (what a character with root motion plays), the body should stay in place.
    try:
        op2 = unreal.AnimPoseEvaluationOptions(); op2.set_editor_property("extract_root_motion", True)
        hb = head or names[min(4, len(names) - 1)]
        h0 = X.get_bone_pose(X.get_anim_pose_at_time(seq, 0.0, op2), hb, SP.WORLD).translation
        h1 = X.get_bone_pose(X.get_anim_pose_at_time(seq, L * 0.9999, op2), hb, SP.WORLD).translation
        print("INPLACE %s with extract_root_motion: t0 (%.0f,%.0f,%.0f) tEnd (%.0f,%.0f,%.0f)" % (hb, h0.x, h0.y, h0.z, h1.x, h1.y, h1.z))
    except Exception as e:
        print("INPLACE n/a", e)
elif mode == "bones":
    m = unreal.load_asset(sys.argv[2])
    a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(0, 0, -50000)); c = a.skeletal_mesh_component; c.set_skeletal_mesh_asset(m)
    base = a.get_actor_location()
    print("NBONES", c.get_num_bones())
    for i in range(c.get_num_bones()):
        b = c.get_bone_name(i); p = c.get_socket_location(b) - base
        print("B %d %s %s %.1f %.1f %.1f" % (i, b, c.get_parent_bone(b), p.x, p.y, p.z))
    eas.destroy_actor(a)
elif mode == "mannequin":
    reg = unreal.AssetRegistryHelpers.get_asset_registry()
    for cls in ("IKRigDefinition", "IKRetargeter", "SkeletalMesh"):
        f = unreal.ARFilter(class_paths=[unreal.TopLevelAssetPath("/Script/IKRig" if cls.startswith("IK") else "/Script/Engine", cls)], recursive_paths=True)
        hits = [str(a.package_name) for a in reg.get_assets(f)]
        if cls == "SkeletalMesh": hits = [h for h in hits if any(k in h.lower() for k in ("manny", "quinn", "mannequin"))]
        print("CLS", cls, len(hits), hits[:15])
    for pth in ("/Game/Characters/Mannequins", "/Game/Characters", "/Game/ThirdPerson", "/IKRig"):
        try: print("DIR", pth, len(unreal.EditorAssetLibrary.list_assets(pth, True, False)))
        except Exception as e: print("DIR", pth, "err", e)
    print("ENGINE", unreal.SystemLibrary.get_engine_version())
    print("HAS_AUTOCHAR", hasattr(unreal, "IKRigController"), [n for n in dir(unreal.IKRigController) if "auto" in n.lower() or "template" in n.lower()] if hasattr(unreal, "IKRigController") else [])
elif mode == "pose":
    m = unreal.load_asset(sys.argv[2]); seq = unreal.load_asset(sys.argv[3])
    for old in eas.get_all_level_actors():
        if old.actor_has_tag("SM2Sheet"): eas.destroy_actor(old)
    acts = []
    for i, f in enumerate((0, .25, .5, .75, 1.0)):
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(400000 + i * 5000, 400000, 50000)); a.tags = [unreal.Name("SM2Sheet")]
        c = a.skeletal_mesh_component; c.set_skeletal_mesh_asset(m); c.set_update_animation_in_editor(True)
        c.override_animation_data(seq, False, False, f * seq.get_play_length() * 0.999, 0.0)
        try: c.tick_animation(0.0, False); c.refresh_bone_transforms()
        except Exception: pass
        acts.append(a)
    builtins._sm2_sheet = (m, acts); print("posed", len(acts))
elif mode == "capture":
    out, name = sys.argv[2], sys.argv[3]
    m, acts = builtins._sm2_sheet
    hidden = unreal.load_asset("/Game/SM2_Work/Materials/M_Hide")
    boxes = []
    for a in acts:
        c = a.skeletal_mesh_component
        for i, slot in enumerate(m.materials):
            if hidden and any(k in str(slot.material_slot_name).lower() for k in HIDE): c.set_material(i, hidden)
        base = a.get_actor_location()
        SKIP = HIDE + ("cam", "targ", "hold", "gun", "_ik", "ik_", "weapon", "_skeleton", "object00", "pose", "aim", "look", "attach", "prop")
        pts = [c.get_socket_location(c.get_bone_name(i)) - base for i in range(c.get_num_bones()) if not any(k in str(c.get_bone_name(i)).lower() for k in SKIP)]
        if not pts: pts = [c.get_socket_location(c.get_bone_name(i)) - base for i in range(c.get_num_bones())]
        cx = sorted(p.x for p in pts); cy = sorted(p.y for p in pts); cz = sorted(p.z for p in pts); k = len(pts)
        med = unreal.Vector(cx[k // 2], cy[k // 2], cz[k // 2])
        dd = sorted((p - med).length() for p in pts); lim = max(dd[int(k * 0.9)] * 2.0, 30)
        pts = [p for p in pts if (p - med).length() <= lim]
        boxes.append((min(p.x for p in pts), max(p.x for p in pts), min(p.y for p in pts), max(p.y for p in pts), min(p.z for p in pts), max(p.z for p in pts)))
    w = max(b[1] - b[0] for b in boxes); w = max(w, max(b[3] - b[2] for b in boxes) * 0.6, 50)
    gap = w * 0.35; x = 400000.0
    allp = []
    for a, b in zip(acts, boxes):
        a.set_actor_location(unreal.Vector(x - b[0], 400000, 50000), False, False)
        allp += [(x, 400000 + b[2], 50000 + b[4]), (x + (b[1] - b[0]), 400000 + b[3], 50000 + b[5])]
        x += (b[1] - b[0]) + gap
    lo = [min(p[i] for p in allp) for i in range(3)]; hi = [max(p[i] for p in allp) for i in range(3)]
    o = unreal.Vector(*[(l + h) / 2 for l, h in zip(lo, hi)])
    W_, H_ = 2560, 768; fov = 50.0
    halfw = (hi[0] - lo[0]) / 2 * 1.08; halfh = (hi[2] - lo[2]) / 2 * 1.15
    tw = math.tan(math.radians(fov / 2)); dist = max(halfw / tw, halfh / (tw * H_ / W_)) + (hi[1] - lo[1]) / 2
    rot = unreal.Rotator(roll=0, pitch=-8, yaw=-90 + 12)
    fwd = rot.get_forward_vector()
    lit = []
    for off, mul in ((unreal.Vector(-0.5, 1.0, 0.6), 1.0), (unreal.Vector(0.8, 0.6, 0.2), 0.4), (unreal.Vector(0.3, -1.0, 0.8), 0.7)):
        Lt = eas.spawn_actor_from_class(unreal.PointLight, o + off * dist); Lt.tags = [unreal.Name("SM2Sheet")]
        lc = Lt.point_light_component; lc.set_mobility(unreal.ComponentMobility.MOVABLE)
        lc.set_editor_property("intensity_units", unreal.LightUnits.CANDELAS)
        lc.set_intensity(float(next((x[10:] for x in sys.argv if x.startswith('intensity=')), 20)) * mul * (dist / 100.0) ** 2); lc.set_attenuation_radius(dist * 6); lit.append(Lt)
    rt = unreal.RenderingLibrary.create_render_target2d(world(), W_, H_, unreal.TextureRenderTargetFormat.RTF_RGBA8)
    cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, o - fwd * dist, rot); cam.tags = [unreal.Name("SM2Sheet")]
    cc = cam.capture_component2d
    cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
    cc.set_editor_property("fov_angle", fov)
    pps = cc.get_editor_property("post_process_settings")
    pps.set_editor_property("override_auto_exposure_method", True); pps.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    pps.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); pps.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
    pps.set_editor_property("override_depth_of_field_focal_distance", True); pps.set_editor_property("depth_of_field_focal_distance", 0.0)
    cc.set_editor_property("post_process_settings", pps)
    cc.capture_scene()
    builtins._sm2_sheet_pending = (rt, out, name)
    print("captured w %.0f dist %.0f" % (hi[0] - lo[0], dist))
elif mode == "export":
    rt, out, name = builtins._sm2_sheet_pending
    unreal.RenderingLibrary.export_render_target(world(), rt, out, name)
    for old in eas.get_all_level_actors():
        if old.actor_has_tag("SM2Sheet"): eas.destroy_actor(old)
    print("exported", name)
