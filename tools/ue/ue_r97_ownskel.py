# ue_r97_ownskel.py (R97p): import <fbx> as a skeletal mesh into <folder> on a NEW skeleton (never reuse the user's
# /Game/Characters skeleton), copy material slots from <old mesh>, import <anim fbx;...> onto the new skeleton.
# usage: uerun ue_r97_ownskel.py <fbx>,<folder>,<old mesh path>,<anim1.fbx;anim2.fbx>
import sys, unreal
a = sys.argv[1:]; a = a[0].split(",") if len(a) == 1 else a
fbx, folder, old, anims = a[0], a[1], a[2], [x for x in a[3].split(";") if x]
reuse = unreal.load_asset(a[4]) if len(a) > 4 and a[4] else None  # optional: OUR skeleton to merge into (never /Game/Characters)
if reuse and not reuse.get_path_name().startswith("/Game/ForgeLens/"): raise SystemExit("R97P refuse non-ForgeLens skeleton")
E = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools()
if E.does_directory_exist(folder) and E.list_assets(folder, True, False): raise SystemExit("R97P folder not empty: " + folder)
def task(f, dest, skel_mesh, sk=None):
    t = unreal.AssetImportTask(); t.filename = f; t.destination_path = dest; t.automated = True; t.replace_existing = True; t.save = True
    o = unreal.FbxImportUI(); o.import_materials = False; o.import_textures = False
    if skel_mesh:
        o.import_mesh = True; o.import_animations = False; o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
        if reuse: o.skeleton = reuse
    else:
        o.import_mesh = False; o.import_animations = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_ANIMATION; o.skeleton = sk
    t.options = o; AT.import_asset_tasks([t]); return list(t.imported_object_paths)
p = task(fbx, folder, True); print("R97P mesh", p)
m = [unreal.load_asset(x) for x in p if isinstance(unreal.load_asset(x), unreal.SkeletalMesh)][0]
sk = m.get_editor_property("skeleton"); print("R97P skeleton", sk.get_path_name())
om = unreal.load_asset(old)
if om:
    oms = om.get_editor_property("materials"); ms = m.get_editor_property("materials"); byname = {str(x.material_slot_name): x.material_interface for x in oms}; n = 0
    for s in ms:
        mi = byname.get(str(s.material_slot_name))
        if mi: s.material_interface = mi; n += 1
    m.set_editor_property("materials", ms); print("R97P materials copied", n, "/", len(ms))
E.save_loaded_asset(m)
if reuse: E.save_loaded_asset(sk); print("R97P merged+saved skeleton", sk.get_path_name(), len(unreal.AnimPoseExtensions.get_bone_names(unreal.AnimPoseExtensions.get_reference_pose(sk))))
for f in anims:
    print("R97P anim", task(f, folder + "/Anims", False, sk))

