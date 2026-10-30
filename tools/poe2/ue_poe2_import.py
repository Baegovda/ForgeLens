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

MASTER = "/Game/ForgeLens/POE/_Shared/M_Poe2Lit"

def master():
    """Shared lit master: BaseColor = TextureSample(BaseColorTex), Normal = TextureSample(NormalTex, normal sampler)."""
    if unreal.EditorAssetLibrary.does_asset_exist(MASTER):
        return unreal.load_asset(MASTER)
    mel = unreal.MaterialEditingLibrary
    pkg, name = MASTER.rsplit("/", 1)
    m = unreal.AssetToolsHelpers.get_asset_tools().create_asset(name, pkg, unreal.Material, unreal.MaterialFactoryNew())
    bc = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, -100)
    bc.set_editor_property("parameter_name", "BaseColorTex")
    mel.connect_material_property(bc, "RGB", unreal.MaterialProperty.MP_BASE_COLOR)
    nm = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, 200)
    nm.set_editor_property("parameter_name", "NormalTex")
    nm.set_editor_property("sampler_type", unreal.MaterialSamplerType.SAMPLERTYPE_NORMAL)
    default_n = unreal.load_asset("/Engine/EngineMaterials/DefaultNormal")
    if default_n:
        nm.set_editor_property("texture", default_n)
    mel.connect_material_property(nm, "RGB", unreal.MaterialProperty.MP_NORMAL)
    rough = mel.create_material_expression(m, unreal.MaterialExpressionConstant, -300, 400)
    rough.set_editor_property("r", 0.8)
    mel.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    mel.recompile_material(m)
    unreal.EditorAssetLibrary.save_loaded_asset(m)
    return m

def part_maps(src):
    """part name -> (albedo png base, normal png base) from poe2export.txt 'part <n> tris .. albedo <path> normal <path>'."""
    maps = {}
    txt = os.path.join(src, "poe2export.txt")
    if not os.path.exists(txt):
        return maps
    for line in open(txt, encoding="utf-8", errors="replace"):
        if not line.startswith("part "):
            continue
        w = line.split()
        name = w[1]
        def after(k):
            return os.path.splitext(os.path.basename(w[w.index(k) + 1]))[0] if k in w and w.index(k) + 1 < len(w) and w[w.index(k) + 1] != "normal" else None
        maps[name.replace("|", "_")] = (after("albedo"), after("normal"))  # FBX slot names replace | with _
    return maps

def materials(src, dest, mesh_path):
    """M2 E: one MaterialInstanceConstant per mesh slot (MI_<slot>) on M_Poe2Lit with the part's albedo/normal textures."""
    sk = unreal.load_asset(mesh_path)
    if not isinstance(sk, unreal.SkeletalMesh):
        return 0
    parent = master()
    maps = part_maps(src)
    mel = unreal.MaterialEditingLibrary
    mats = list(sk.get_editor_property("materials"))
    done = 0
    for i, sm in enumerate(mats):
        slot = str(sm.get_editor_property("material_slot_name"))
        alb, nrm = maps.get(slot, (None, None))
        mi_name = "MI_" + slot
        mi_path = f"{dest}/Materials/{mi_name}"
        mi = unreal.load_asset(mi_path) if unreal.EditorAssetLibrary.does_asset_exist(mi_path) else \
            unreal.AssetToolsHelpers.get_asset_tools().create_asset(mi_name, f"{dest}/Materials", unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        mel.set_material_instance_parent(mi, parent)
        for pname, base in (("BaseColorTex", alb), ("NormalTex", nrm)):
            if base and unreal.EditorAssetLibrary.does_asset_exist(f"{dest}/Textures/{base}"):
                mel.set_material_instance_texture_parameter_value(mi, pname, unreal.load_asset(f"{dest}/Textures/{base}"))
        unreal.EditorAssetLibrary.save_loaded_asset(mi)
        sm.set_editor_property("material_interface", mi)
        mats[i] = sm
        done += 1
    sk.set_editor_property("materials", mats)
    unreal.EditorAssetLibrary.save_loaded_asset(sk)
    return done

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
        mesh = next((str(x).split(".")[0] for x in assets if str(x).split("/")[-1].split(".")[0] == name), f"{dest}/{name}")
        mats = materials(d, dest, mesh)
        listed = unreal.EditorAssetLibrary.list_assets(dest, recursive=True)
        classes = {}
        for a in listed:
            data = unreal.EditorAssetLibrary.find_asset_data(a)
            c = str(data.asset_class_path.asset_name)
            classes[c] = classes.get(c, 0) + 1
        report[name] = {"dest": dest, "textures": len(tex), "fbx_objects": [str(x) for x in assets], "material_slots": mats, "classes": classes}
        log(f"{name}: {classes}")
    unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
    with open(result, "w") as f:
        json.dump(report, f, indent=1)

if __name__ == "__main__":
    main(sys.argv[1:])
