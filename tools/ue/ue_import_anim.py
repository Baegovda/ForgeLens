# Import animation-only FBX written by `export --anim` onto an existing skeleton.
# usage (UEMCP ue_python): sys.argv = ['x', <fbx>, <skeletal mesh asset>, <dest folder>, [yaw|auto], [rootmotion|noroot]]
# Yaw is auto by default (see sm2_anim_import): it compares skeleton bone 0 at t=0 with the reference pose instead of
# a hardcoded 180 (180 was right only for the old Original/cc_titus_assault skeleton; fresh Interchange meshes need 0).
# Root motion: fbx-unreal exports put ROOT at the top (Unreal bone 0) carrying the 0xF4 root motion, so the
# sequence gets EnableRootMotion + root lock on the first frame unless "noroot" is passed.
import sys, unreal
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

fbx, mesh_path, dest = sys.argv[1], sys.argv[2], sys.argv[3]
yaw = sys.argv[4] if len(sys.argv) > 4 else "auto"
rm = not (len(sys.argv) > 5 and sys.argv[5] == "noroot")
paths, used = sm2_anim_import(fbx, unreal.load_asset(mesh_path).skeleton, dest, yaw, rm)
print("IMPORTED", paths, "yaw", used, "rootmotion", rm)
