# mtl1-15m: import an mtl15 re-export (BC5 spec R fix) into /Game/ForgeLens/SM2/Tests/mtl15/<asset> and build its materials.
import sys, runpy, unreal
name = sys.argv[1]; exp = sys.argv[2] if len(sys.argv) > 2 else "mtl15"
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/ForgeLens/SM2/Tests/%s/%s" % (exp, name)
root = W + "/Exports/%s/%s/tpl/%s.tpl/" % (exp, name, name)
t = unreal.AssetImportTask(); t.filename = root + name + ".fbx"; t.destination_path = folder
t.automated = True; t.replace_existing = True; t.save = False
o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH; t.options = o
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
script = root + "import_sm2_materials.py"
sys.argv = [script, root + name + ".materials.json", folder]
runpy.run_path(script, run_name="__main__")
unreal.EditorAssetLibrary.save_directory(folder, only_if_is_dirty=True, recursive=True)
print("MTL15 done", folder)
