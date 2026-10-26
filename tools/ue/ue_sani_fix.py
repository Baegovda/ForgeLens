# .sani gap verification in the unified layout: mesh -> /Game/SM2_Work/{Characters|Props}/<name>,
# "after" clip -> /Game/SM2_Work/Animations/<name>, "before" clip -> /Game/SM2_Work/Tests/sani_before/<name>;
# then report per-anim length, frames and bone poses (world space) at a few times.
# usage: ue_sani_fix.py import <name> <clip> [after_tag] [before_tag]   |   ue_sani_fix.py check <name> <bone,bone,...>
# Exports are read from Desktop/SM2_Work/Exports/sani_fix_<tag>/<name>/tpl/<name>.tpl/ (tags default to after / before).
# A tag of "-" skips that side. The mesh is imported only when <folder>/<name> does not exist yet, so an existing
# mesh keeps its material instances (pass "remesh" as arg 6 to force a reimport).
import sys, os, unreal
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
mode, name = sys.argv[1], sys.argv[2]
base = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/sani_fix_{}/" + name + "/tpl/" + name + ".tpl/"
W = "/Game/SM2_Work/"
folder = W + ("Characters/" if name.startswith("cc_") else "Props/") + name
anim_dirs = {"after": W + "Animations/" + name, "before": W + "Tests/sani_before/" + name}
tags = {"after": sys.argv[4] if len(sys.argv) > 4 else "after", "before": sys.argv[5] if len(sys.argv) > 5 else "before"}
tools = unreal.AssetToolsHelpers.get_asset_tools()
if mode == "import":
    clip = sys.argv[3]
    remesh = len(sys.argv) > 6 and sys.argv[6] == "remesh"
    if unreal.EditorAssetLibrary.does_asset_exist(folder + "/" + name) and not remesh:
        print("MESHKEEP", folder + "/" + name)
    else:
      t = unreal.AssetImportTask(); t.filename = base.format(tags["after"]) + name + ".fbx"; t.destination_path = folder
      t.automated = True; t.replace_existing = True; t.save = True
      o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
      o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
      t.options = o
      tools.import_asset_tasks([t])
      print("MESH", list(t.imported_object_paths))
    mesh = unreal.load_asset(folder + "/" + name)
    for v in ("before", "after"):
        if tags[v] == "-":
            continue
        fbx = base.format(tags[v]) + name + "__" + clip + ".fbx"
        if not os.path.exists(fbx):
            print("NOFBX", v, fbx); continue
        paths, used = sm2_anim_import(fbx, mesh.skeleton, anim_dirs[v])
        print("ANIM", v, paths, "yaw", used)
else:
    bones = sys.argv[3].split(",")
    reg = unreal.AssetRegistryHelpers.get_asset_registry()
    for v in ("before", "after"):
        for ad in reg.get_assets_by_path(anim_dirs[v], recursive=False):
            seq = unreal.load_asset(str(ad.package_name))
            if not isinstance(seq, unreal.AnimSequence): continue
            n = seq.get_editor_property("number_of_sampled_frames") if hasattr(seq, "number_of_sampled_frames") else None
            L = seq.get_play_length()
            opt = unreal.AnimPoseEvaluationOptions()
            print("SEQ", v, seq.get_name(), "len %.3f" % L, "frames", n)
            for b in bones:
                row = []
                for f in (0.0, 0.25, 0.5, 0.75, 1.0):
                    pose = unreal.AnimPoseExtensions.get_anim_pose_at_time(seq, f * L * 0.999, opt)
                    tr = unreal.AnimPoseExtensions.get_bone_pose(pose, unreal.Name(b), unreal.AnimPoseSpaces.WORLD)
                    p = tr.translation; r = tr.rotation.rotator(); s = tr.scale3d
                    row.append("(%.1f,%.1f,%.1f|y%.0f p%.0f r%.0f|s%.2f)" % (p.x, p.y, p.z, r.yaw, r.pitch, r.roll, s.x))
                print("  ", b, " ".join(row))
