# .sani gap verification in the unified layout: mesh -> /Game/SM2_Work/{Characters|Props}/<name>,
# "after" clip -> /Game/SM2_Work/Animations/<name>, "before" clip -> /Game/SM2_Work/Tests/sani_before/<name>;
# then report per-anim length, frames and bone poses (world space) at a few times.
# usage: ue_sani_fix.py import <name> <clip> [after_tag] [before_tag]   |   ue_sani_fix.py check <name> <bone,bone,...>
# Exports are read from Desktop/SM2_Work/Exports/sani_fix_<tag>/<name>/tpl/<name>.tpl/ (tags default to after / before).
import sys, os, unreal
mode, name = sys.argv[1], sys.argv[2]
base = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/sani_fix_{}/" + name + "/tpl/" + name + ".tpl/"
W = "/Game/SM2_Work/"
folder = W + ("Characters/" if name.startswith("cc_") else "Props/") + name
anim_dirs = {"after": W + "Animations/" + name, "before": W + "Tests/sani_before/" + name}
tags = {"after": sys.argv[4] if len(sys.argv) > 4 else "after", "before": sys.argv[5] if len(sys.argv) > 5 else "before"}
tools = unreal.AssetToolsHelpers.get_asset_tools()
if mode == "import":
    clip = sys.argv[3]
    t = unreal.AssetImportTask(); t.filename = base.format(tags["after"]) + name + ".fbx"; t.destination_path = folder
    t.automated = True; t.replace_existing = True; t.save = True
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
    o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    t.options = o
    tools.import_asset_tasks([t])
    print("MESH", list(t.imported_object_paths))
    mesh = unreal.load_asset(folder + "/" + name)
    for v in ("before", "after"):
        fbx = base.format(tags[v]) + name + "__" + clip + ".fbx"
        if not os.path.exists(fbx):
            print("NOFBX", v, fbx); continue
        unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 0")
        t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = anim_dirs[v]
        t.automated = True; t.replace_existing = True; t.save = True
        o = unreal.FbxImportUI(); o.import_mesh = False; o.import_animations = True
        o.import_materials = False; o.import_textures = False; o.skeleton = mesh.skeleton
        o.mesh_type_to_import = unreal.FBXImportType.FBXIT_ANIMATION
        o.anim_sequence_import_data.set_editor_property("import_rotation", unreal.Rotator(roll=0, pitch=0, yaw=180))
        t.options = o
        tools.import_asset_tasks([t])
        unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
        print("ANIM", v, list(t.imported_object_paths))
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
