"""PoE2 M2 E: import Forge Lens --poe2export folders into Unreal under /Game/ForgeLens/POE/<name>.

usage (inside the editor, via Scripts/uerun.ps1):  ue_poe2_import.py <result.json> <dest root> <export dir> [<export dir> ...]
Each export dir holds <name>.fbx (skinned mesh + skeleton + one clip, Unreal space) and the PNG textures it references.
Textures are imported first (uncompressed sRGB for colour, normal maps as TC_NORMALMAP), then the FBX through the legacy
FBX pipeline (automated, no dialogs) with skeleton, physics asset off, animations on; materials are created by the importer
from the FBX texture references. Results (asset paths and classes per folder) go to <result.json>.
"""
import json, os, sys
import unreal

def log(m):
    unreal.log("[poe2] " + m)

def import_tasks(tasks):
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
    out = []
    for t in tasks:
        out += list(t.get_editor_property("imported_object_paths") or [])
    return out

def textures(src, dest):
    tasks = []
    for f in sorted(os.listdir(src)):
        if f.lower().endswith(".png"):
            t = unreal.AssetImportTask()
            t.set_editor_property("filename", os.path.join(src, f))
            t.set_editor_property("destination_path", dest + "/Textures")
            t.set_editor_property("automated", True)
            t.set_editor_property("replace_existing", True)
            t.set_editor_property("save", True)
            tasks.append(t)
    paths = import_tasks(tasks)
    for p in paths:
        tex = unreal.load_asset(p)
        if isinstance(tex, unreal.Texture2D) and "normal" in p.lower():
            tex.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP)
            tex.set_editor_property("srgb", False)
            unreal.EditorAssetLibrary.save_loaded_asset(tex)
    return paths

def fbx(src, dest):
    files = [f for f in os.listdir(src) if f.lower().endswith(".fbx")]
    if not files:
        return []
    ui = unreal.FbxImportUI()
    ui.set_editor_property("import_mesh", True)
    ui.set_editor_property("import_as_skeletal", True)
    ui.set_editor_property("mesh_type_to_import", unreal.FBXImportType.FBXIT_SKELETAL_MESH)
    ui.set_editor_property("import_materials", True)
    ui.set_editor_property("import_textures", False)
    ui.set_editor_property("import_animations", True)
    ui.set_editor_property("create_physics_asset", False)
    ui.skeletal_mesh_import_data.set_editor_property("import_morph_targets", False)
    ui.anim_sequence_import_data.set_editor_property("import_bone_tracks", True)
    t = unreal.AssetImportTask()
    t.set_editor_property("filename", os.path.join(src, files[0]))
    t.set_editor_property("destination_path", dest)
    t.set_editor_property("automated", True)
    t.set_editor_property("replace_existing", True)
    t.set_editor_property("save", True)
    t.set_editor_property("options", ui)
    return import_tasks([t])

def main(argv):
    result, root, dirs = argv[0], argv[1].rstrip("/"), argv[2:]
    if not root.startswith("/Game/ForgeLens/POE"):
        raise SystemExit("dest must be under /Game/ForgeLens/POE")
    report = {}
    for d in dirs:
        name = os.path.splitext([f for f in os.listdir(d) if f.lower().endswith(".fbx")][0])[0]
        dest = f"{root}/{name}"
        tex = textures(d, dest)
        assets = fbx(d, dest)
        listed = unreal.EditorAssetLibrary.list_assets(dest, recursive=True)
        classes = {}
        for a in listed:
            data = unreal.EditorAssetLibrary.find_asset_data(a)
            c = str(data.asset_class_path.asset_name)
            classes[c] = classes.get(c, 0) + 1
        report[name] = {"dest": dest, "textures": len(tex), "fbx_objects": [str(x) for x in assets], "classes": classes}
        log(f"{name}: {classes}")
    unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
    with open(result, "w") as f:
        json.dump(report, f, indent=1)

if __name__ == "__main__":
    main(sys.argv[1:])
