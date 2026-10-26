import sys, runpy, os, json, unreal
name, skeletal = sys.argv[1], sys.argv[2] == "1"
export_dir = sys.argv[3] if len(sys.argv) > 3 else name
root = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/" + export_dir + "/tpl/" + name + ".tpl/"
folder = "/Game/SM2_Work/Tests/typetest/" + export_dir
t = unreal.AssetImportTask(); t.filename = root + name + ".fbx"; t.destination_path = folder
t.automated = True; t.replace_existing = True; t.save = True
o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
o.import_as_skeletal = skeletal
o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH if skeletal else unreal.FBXImportType.FBXIT_STATIC_MESH
if not skeletal:
    o.static_mesh_import_data.combine_meshes = True
t.options = o
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
sys.argv = [root + "import_sm2_materials.py", root + name + ".materials.json", folder]
runpy.run_path(sys.argv[0], run_name="__main__")
mesh = unreal.load_asset(folder + "/" + name)
print("MESH", type(mesh).__name__ if mesh else None)
if mesh:
    if isinstance(mesh, unreal.SkeletalMesh):
        mats = [(str(s.material_slot_name), s.material_interface) for s in mesh.materials]
        print("BONES", mesh.skeleton.get_name() if mesh.skeleton else None, len(mesh.get_editor_property("ref_skeleton").get_editor_property("raw_ref_bone_info")) if hasattr(mesh,"ref_skeleton") else "")
    else:
        mats = [(str(s.material_slot_name), s.material_interface) for s in mesh.static_materials]
    bad = []
    mel = unreal.MaterialEditingLibrary
    for sn, mi in mats:
        if mi is None or not mi.get_name().startswith(("MI_", "M_SM2_ClothSim")):
            bad.append((sn, mi.get_name() if mi else None, "unassigned"))
        elif isinstance(mi, unreal.MaterialInstanceConstant):
            tex = mel.get_material_instance_texture_parameter_value(mi, "Diffuse")
            if tex is None or "WhiteSquare" in tex.get_name():
                bad.append((sn, mi.get_name(), "no diffuse"))
    print("SLOTS", len(mats), "BAD", bad)
