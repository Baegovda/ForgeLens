# Import animation-only FBX written by `export --anim` onto an existing skeleton.
# usage (UEMCP ue_python): sys.argv = ['x', <fbx | folder>, <skeletal mesh or skeleton asset>, <dest folder>,
#                                      [yaw|auto], [rootmotion|noroot|autoroot]]
# A folder imports every {model}__{clip}.fbx under it (recursive) (anim-only files from `export --anim ... --anim-only`) in one batch:
# all clips go through one import_asset_tasks call.
# Yaw is auto by default (see sm2_anim_import): it reads skeleton bone 0's reference yaw instead of
# a hardcoded 180 (180 was right only for the old Original/cc_titus_assault skeleton; fresh Interchange meshes need 0).
# Root motion: fbx-unreal exports put ROOT at the top (Unreal bone 0) carrying the 0xF4 root motion.
#   autoroot (default): gameplay clips get EnableRootMotion + root lock on the first frame; cinematic shots
#   (name contains "cinematic") and additive clips keep it off, so Sequencer plays the authored travel from the
#   rebased origin ({clip}.rebase.json holds the level offset removed by --anim-rebase first-frame).
#   rootmotion / noroot force it on / off for gameplay clips.
# Additive: files ending in __ADD carry the 0xF3/0xF7 slot (clip+0x68 = slot 0 relative to its first frame). They get
#   AdditiveAnimType = local space, base pose = this clip's frame 0 (ABPT_LOCAL_ANIM_FRAME, index 0), which is the
#   same delta the game blends. Use them with Apply Additive (or a Layered Blend per Bone for face clips).
import sys, unreal
# --- sm2_anim_import (same block in ue_import_anim.py, ue_anim_survey.py, ue_sani_fix.py) ---
# Legacy FBX anim import onto an existing skeleton. yaw="auto": read skeleton bone 0's reference yaw and pick the
# 90-degree multiple that puts it at 180 (our export convention), in one import. Interchange meshes (incl. the old
# {mesh}_Skeleton Null, and ROOT-rooted exports) get 0; the older Original/cc_titus_assault skeleton gets 180. root_motion: bone 0 carries the 0xF4 root motion in
# ROOT-rooted exports, so enable it with the lock on the first frame.
def sm2_anim_import(fbx, skeleton, dest, yaw="auto", root_motion=True):
    import unreal
    files = fbx if isinstance(fbx, (list, tuple)) else [fbx]
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

import os, time
fbx, mesh_path, dest = sys.argv[1], sys.argv[2], sys.argv[3]
yaw = sys.argv[4] if len(sys.argv) > 4 else "auto"
mode = sys.argv[5] if len(sys.argv) > 5 else "autoroot"
rm = {"noroot": False, "rootmotion": "force"}.get(mode, True)
asset = unreal.load_asset(mesh_path)
skeleton = asset if isinstance(asset, unreal.Skeleton) else asset.skeleton
if os.path.isdir(fbx):
    # recursive: export -o <folder> writes {folder}/tpl/{model}.tpl/{model}__{clip}.fbx
    fbx = sorted(os.path.join(d, f) for d, _, fs in os.walk(fbx) for f in fs if f.lower().endswith(".fbx") and "__" in f)
start = time.time()
paths, used = sm2_anim_import(fbx, skeleton, dest, yaw, rm)
n = len(fbx) if isinstance(fbx, list) else 1
print("IMPORTED %d files -> %d assets in %.1f s (%.2f s/clip) yaw %g rootmotion %s" % (n, len(paths), time.time() - start, (time.time() - start) / max(n, 1), used, mode))
