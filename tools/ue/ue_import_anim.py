# Import animation-only FBX written by `export --anim` onto an existing skeleton.
# usage (UEMCP ue_python): sys.argv = ['x', <fbx>, <skeletal mesh asset>, <dest folder>]
# The skeletal mesh is imported by Interchange, which puts a 180 deg yaw on the root
# (ref pose Object00C9/ROOT yaw 180). The legacy FBX importer (needed for anim-only FBX)
# does not, so without import_rotation yaw=180 the animated body faces -Y and root motion
# runs along -Y while the mesh faces +Y. With it: body faces +Y and the run root moves +Y.
import sys, unreal
fbx, mesh_path, dest = sys.argv[1], sys.argv[2], sys.argv[3]
unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 0")
sk = unreal.load_asset(mesh_path).skeleton
t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = dest
t.automated = True; t.replace_existing = True; t.save = True
o = unreal.FbxImportUI(); o.import_mesh = False; o.import_animations = True
o.import_materials = False; o.import_textures = False; o.skeleton = sk
o.mesh_type_to_import = unreal.FBXImportType.FBXIT_ANIMATION
o.anim_sequence_import_data.set_editor_property("import_rotation", unreal.Rotator(roll=0, pitch=0, yaw=180))
t.options = o
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
unreal.SystemLibrary.execute_console_command(None, "Interchange.FeatureFlags.Import.FBX 1")
print("IMPORTED", list(t.imported_object_paths))
