# ue_r97_reskel.py (R97v): re-import our old chaplain test meshes (chars_cloth2, chars_phys) in place from their own FBX source onto OUR skeleton
# /Game/ForgeLens/SM2/Characters/cc_chaplain/cc_chaplain_Skeleton. PA kept, no materials/textures/PA created. Never touches/saves the user's skeleton.
import unreal
E = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools()
own = unreal.load_asset("/Game/ForgeLens/SM2/Characters/cc_chaplain/cc_chaplain_Skeleton")
for p in ["/Game/ForgeLens/SM2/Tests/chars_cloth2/cc_chaplain/cc_chaplain", "/Game/ForgeLens/SM2/Tests/chars_phys/cc_chaplain/cc_chaplain"]:
    m = unreal.load_asset(p); pa = m.get_editor_property("physics_asset"); src = m.get_editor_property("asset_import_data").get_first_filename()
    mats = list(m.get_editor_property("materials"))
    t = unreal.AssetImportTask(); t.filename = src; t.destination_path = p.rsplit("/", 1)[0]; t.destination_name = p.rsplit("/", 1)[1]
    t.automated = True; t.replace_existing = True; t.save = False
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    o.skeleton = own; o.create_physics_asset = False; o.physics_asset = pa; o.import_materials = False; o.import_textures = False; o.import_animations = False
    t.options = o; AT.import_asset_tasks([t])
    m = unreal.load_asset(p); m.set_editor_property("physics_asset", pa); m.set_editor_property("materials", mats)
    sk = m.get_editor_property("skeleton").get_path_name()
    if sk.startswith("/Game/ForgeLens/"): E.save_loaded_asset(m, False)
    print("R97V reskel", p, "src", src, "->", sk, "pa", pa.get_path_name() if pa else None)
d = [x.get_name() for x in unreal.EditorLoadingAndSavingUtils.get_dirty_content_packages()] if hasattr(unreal.EditorLoadingAndSavingUtils, "get_dirty_content_packages") else []
print("R97V dirty user pkgs", [x for x in d if x.startswith("/Game/Characters")])
