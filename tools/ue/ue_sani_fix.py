# .sani gap verification in the unified layout: mesh -> /Game/SM2_Work/{Characters|Props}/<name>,
# "after" clip -> /Game/SM2_Work/Animations/<name>, "before" clip -> /Game/SM2_Work/Tests/sani_before/<name>;
# then report per-anim length, frames and bone poses (world space) at a few times.
# usage: ue_sani_fix.py import <name> <clip> [after_tag] [before_tag]   |   ue_sani_fix.py check <name> <bone,bone,...>
# Exports are read from Desktop/SM2_Work/Exports/sani_fix_<tag>/<name>/tpl/<name>.tpl/ (tags default to after / before).
# A tag of "-" skips that side. The mesh is imported only when <folder>/<name> does not exist yet, so an existing
# mesh keeps its material instances (pass "remesh" as arg 6 to force a reimport).
import sys, os, unreal
# --- sm2_anim_import (same block in ue_import_anim.py, ue_anim_survey.py, ue_sani_fix.py) ---
# Legacy FBX anim import onto an existing skeleton. yaw="auto": read skeleton bone 0's reference yaw and pick the
# 90-degree multiple that puts it at 180 (our export convention), in one import. Interchange meshes (incl. the old
# {mesh}_Skeleton Null, and ROOT-rooted exports) get 0; the older Original/cc_titus_assault skeleton gets 180. root_motion: bone 0 carries the 0xF4 root motion in
# ROOT-rooted exports, so enable it with the lock on the first frame.
def sm2_anim_import(fbx, skeleton, dest, yaw="auto", root_motion=True):
    import unreal
    files = fbx if isinstance(fbx, (list, tuple)) else [fbx]
    import os as _os, json as _json, glob as _glob
    def baked_rate(path):
        # {model}.physics.json beside the clip lists the clips whose physics was baked (and at what rate).
        d, n = _os.path.split(path); clip = _os.path.splitext(n)[0].split("__", 1)[-1]
        for pj in _glob.glob(_os.path.join(d, "*.physics.json")):
            try: baked = _json.load(open(pj, encoding="utf-8-sig")).get("baked") or []
            except Exception: continue
            for b in baked:
                if str(b.get("clip", "")).lower() == clip.lower(): return int(b.get("rate") or 60)
        return 0
    def one(y, batch=None):
        unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 0")
        tasks = []
        try:
            for f in (batch or files):
                t = unreal.AssetImportTask(); t.filename = f; t.destination_path = dest
                t.automated = True; t.replace_existing = True; t.save = True
                o = unreal.FbxImportUI(); o.import_mesh = False; o.import_animations = True
                o.import_materials = False; o.import_textures = False; o.skeleton = skeleton
                o.mesh_type_to_import = unreal.FBXImportType.FBXIT_ANIMATION
                o.anim_sequence_import_data.set_editor_property("import_rotation", unreal.Rotator(roll=0, pitch=0, yaw=y))
                pr = baked_rate(f)
                if pr:
                    # Physics-baked clip: keep the 60 Hz jiggle/cloth keys instead of resampling to the FBX's 30 fps.
                    o.anim_sequence_import_data.set_editor_property("use_default_sample_rate", False)
                    o.anim_sequence_import_data.set_editor_property("custom_sample_rate", pr)
                t.options = o; tasks.append(t)
            unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
        finally:
            unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
        return [str(p) for t in tasks for p in t.imported_object_paths]
    import os, re
    def is_cinematic(path):
        # Level-space shots: a {clip}.rebase.json beside the FBX (written by --anim-rebase first-frame), or the name.
        n = os.path.basename(path).lower()
        return os.path.exists(os.path.splitext(path)[0] + ".rebase.json") or "cinematic" in n or re.search(r"_shot_\d", n) is not None
    cinematic_names = set(os.path.splitext(os.path.basename(f))[0].lower() for f in files if is_cinematic(f))
    y = 0.0 if yaw == "auto" else float(yaw)
    if yaw == "auto":
        # Our FBX convention puts skeleton bone 0 (ROOT, or the old {mesh}_Skeleton Null) at ref yaw 180 in Unreal
        # when the anims import at yaw 0. A skeleton whose bone 0 sits elsewhere (the old Original/cc_titus_assault
        # needed 180) gets the 90-degree multiple that brings it there. This reads only the skeleton: clip content
        # (turn clips, cinematics, a ROOT track that differs from the bind, e.g. vortex_beast) cannot fool it.
        X = unreal.AnimPoseExtensions; WS = unreal.AnimPoseSpaces.WORLD
        ref = X.get_reference_pose(skeleton); b0 = X.get_bone_names(ref)[0]
        rr = X.get_bone_pose(ref, b0, WS).rotation.rotator().yaw
        d = (180.0 - rr) % 360.0; snap = (round(d / 90.0) * 90) % 360
        off = abs(((d - snap) + 180.0) % 360.0 - 180.0)
        y = float(snap) if off < 10 else 0.0
        print("SM2YAW bone0 %s ref yaw %.1f -> import yaw %g%s" % (b0, rr, y, "" if off < 10 else " (ref not a 90 multiple, check)"))
    paths = one(y)
    seqs = [a for a in (unreal.load_asset(p) for p in paths) if isinstance(a, unreal.AnimSequence)]
    for s in seqs:
        name = s.get_name()
        if s.get_editor_property("skeleton") != skeleton:
            # Split skeleton (R35): the clip must share the mesh's skeleton, never get its own "{mesh}_Skeleton".
            print("SPLITSKEL %s uses %s, expected %s (run ue_fix_split_skeletons.py)" % (s.get_path_name(), s.get_editor_property("skeleton").get_path_name() if s.get_editor_property("skeleton") else None, skeleton.get_path_name()))
        additive = name.lower().endswith("__add")
        cinematic = name.lower() in cinematic_names or "cinematic" in name.lower()
        if additive:
            s.set_editor_property("additive_anim_type", unreal.AdditiveAnimationType.AAT_LOCAL_SPACE_BASE)
            s.set_editor_property("ref_pose_type", unreal.AdditiveBasePoseType.ABPT_LOCAL_ANIM_FRAME)
            s.set_editor_property("ref_frame_index", 0)
        on = False if (additive or root_motion is False) else (True if root_motion == "force" else not cinematic)
        s.set_editor_property("enable_root_motion", on)
        s.set_editor_property("root_motion_root_lock", unreal.RootMotionRootLock.ANIM_FIRST_FRAME if on else unreal.RootMotionRootLock.REF_POSE)
        unreal.EditorAssetLibrary.save_loaded_asset(s)
        print("SM2ANIM %s additive %s rootmotion %s len %.3f" % (name, additive, on, s.get_play_length()))
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
      # Split skeleton rule (R35): a re-import reuses the folder's existing skeleton, so anims already on it keep playing.
      sk = folder + "/" + name + "_Skeleton"
      if unreal.EditorAssetLibrary.does_asset_exist(sk): o.skeleton = unreal.load_asset(sk)
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
