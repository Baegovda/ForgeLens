# Checks the anim_finish imports: root motion, CENTRE start/end (rebase), additive settings.
import sys, unreal
X = unreal.AnimPoseExtensions; WS = unreal.AnimPoseSpaces.WORLD; LS = unreal.AnimPoseSpaces.LOCAL
root = sys.argv[1] if len(sys.argv) > 1 else "/Game/SM2_Work/Tests/anim_finish"
opt = unreal.AnimPoseEvaluationOptions()
for p in unreal.EditorAssetLibrary.list_assets(root, recursive=True):
    a = unreal.load_asset(p)
    if not isinstance(a, unreal.AnimSequence):
        continue
    L = a.get_play_length(); n = a.get_editor_property("number_of_sampled_frames") if hasattr(a, "number_of_sampled_frames") else -1
    try:
        n = unreal.AnimationLibrary.get_num_frames(a)
    except Exception:
        pass
    line = "SEQ %s len %.3f frames %s add %s base %s rm %s lock %s" % (a.get_name(), L, n,
        a.get_editor_property("additive_anim_type"), a.get_editor_property("ref_pose_type"),
        a.get_editor_property("enable_root_motion"), a.get_editor_property("root_motion_root_lock"))
    print(line)
    p0 = X.get_anim_pose_at_time(a, 0.0, opt); p1 = X.get_anim_pose_at_time(a, L, opt)
    names = [str(b) for b in X.get_bone_names(p0)]
    for b in ("ROOT", "CENTRE", "HEAD"):
        if b in names:
            t0 = X.get_bone_pose(p0, b, WS); t1 = X.get_bone_pose(p1, b, WS)
            v0 = t0.translation; v1 = t1.translation
            print("  %s t0 (%.0f, %.0f, %.0f) yaw %.1f   end (%.0f, %.0f, %.0f) yaw %.1f" % (b, v0.x, v0.y, v0.z, t0.rotation.rotator().yaw, v1.x, v1.y, v1.z, t1.rotation.rotator().yaw))
    try:
        rt = unreal.AnimationLibrary.extract_root_track_transform(a, L)
        v = rt.translation
        print("  ROOTTRACK end (%.1f, %.1f, %.1f) dist %.1f yaw %.1f" % (v.x, v.y, v.z, (v.x**2 + v.y**2 + v.z**2) ** 0.5, rt.rotation.rotator().yaw))
    except Exception as e:
        print("  ROOTTRACK err", e)
    if str(a.get_editor_property("additive_anim_type")).endswith("LOCAL_SPACE_BASE") or "__ADD" in a.get_name():
        # Local-space change per bone between frame 0 (the additive base) and mid clip: lids should move, body not.
        pm = X.get_anim_pose_at_time(a, L * 0.5, opt); moved = []
        for b in names:
            q0 = X.get_bone_pose(p0, b, LS); q1 = X.get_bone_pose(pm, b, LS)
            d = (q0.translation - q1.translation).length(); r = q0.rotation.angular_distance(q1.rotation) * 57.2958
            if d > 0.05 or r > 0.5:
                moved.append((r, d, b))
        moved.sort(reverse=True)
        print("  ADDMOVE %d bones move vs frame 0 at mid: %s" % (len(moved), ", ".join("%s %.1fdeg %.2fcm" % (b, r, d) for r, d, b in moved[:8])))
