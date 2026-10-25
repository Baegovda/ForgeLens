# Import a mesh (optional) and anim FBX, then numeric sanity over the clip in component space.
import sys, unreal, math, runpy
mode = sys.argv[1]
if mode == "mesh":
    fbx, dest = sys.argv[2], sys.argv[3]
    unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
    t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = dest; t.automated = True; t.replace_existing = True; t.save = True
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    o.import_materials = False; o.import_textures = False; o.import_animations = False
    t.options = o
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t]); print("MESH", list(t.imported_object_paths))
elif mode == "check":
    an = unreal.load_asset(sys.argv[2])
    X = unreal.AnimPoseExtensions; op = unreal.AnimPoseEvaluationOptions()
    ln = an.get_play_length(); n = 24
    names = [str(b) for b in X.get_bone_names(X.get_anim_pose_at_time(an, 0, op))]
    root = names[0]; worst = 0; nan = 0; jump = 0; prev = None
    for k in range(n + 1):
        p = X.get_anim_pose_at_time(an, ln * k / n, op)
        r = X.get_bone_pose(p, root, unreal.AnimPoseSpaces.WORLD).translation
        pos = []
        for b in names:
            v = X.get_bone_pose(p, b, unreal.AnimPoseSpaces.WORLD).translation
            if any(math.isnan(c) for c in (v.x, v.y, v.z)): nan += 1
            d = math.dist((v.x, v.y, v.z), (r.x, r.y, r.z)); worst = max(worst, d); pos.append((v.x - r.x, v.y - r.y, v.z - r.z))
        if prev:
            jump = max(jump, max(math.dist(a, b) for a, b in zip(pos, prev)))
        prev = pos
    print("CHECK", sys.argv[2].split("/")[-1], "len %.2fs bones %d nan %d maxBoneFromRoot %.1fcm maxStep(1/24 clip) %.1fcm" % (ln, len(names), nan, worst, jump))
