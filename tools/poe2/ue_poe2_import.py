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

MASTER = "/Game/ForgeLens/POE/_Shared/M_Poe2Lit_v2"  # v2: + Tint, GlowTex x GlowColor emissive

def master(masked=False):
    """Shared lit master: BaseColor = TextureSample(BaseColorTex), Normal = TextureSample(NormalTex, normal sampler)."""
    path = MASTER + ("_Masked" if masked else "")
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        return unreal.load_asset(path)
    mel = unreal.MaterialEditingLibrary
    pkg, name = path.rsplit("/", 1)
    m = unreal.AssetToolsHelpers.get_asset_tools().create_asset(name, pkg, unreal.Material, unreal.MaterialFactoryNew())
    if masked:
        # ForceAlphaTest graphs: albedo alpha is the cut-out mask (wolf fur cards)
        m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_MASKED)
        m.set_editor_property("two_sided", True)
    bc = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, -100)
    bc.set_editor_property("parameter_name", "BaseColorTex")
    tint = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -600, -300)
    tint.set_editor_property("parameter_name", "Tint")
    tint.set_editor_property("default_value", unreal.LinearColor(1, 1, 1, 1))
    mul = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -300, -150)
    mel.connect_material_expressions(bc, "RGB", mul, "A")
    mel.connect_material_expressions(tint, "", mul, "B")
    mel.connect_material_property(mul, "", unreal.MaterialProperty.MP_BASE_COLOR)
    if masked:
        mel.connect_material_property(bc, "A", unreal.MaterialProperty.MP_OPACITY_MASK)
    gt = mel.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, 600)
    gt.set_editor_property("parameter_name", "GlowTex")
    black = unreal.load_asset("/Engine/EngineResources/Black")
    if black:
        gt.set_editor_property("texture", black)
    gc = mel.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -600, 850)
    gc.set_editor_property("parameter_name", "GlowColor")
    gc.set_editor_property("default_value", unreal.LinearColor(0, 0, 0, 1))
    gm = mel.create_material_expression(m, unreal.MaterialExpressionMultiply, -300, 700)
    mel.connect_material_expressions(gt, "RGB", gm, "A")
    mel.connect_material_expressions(gc, "", gm, "B")
    mel.connect_material_property(gm, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
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
        def num(k, d):
            try: return w[w.index(k) + 1]
            except (ValueError, IndexError): return d
        glow = after("glow")
        gm = [float(x) for x in num("glowmult", "1,1,1").split(",")] if glow else None
        maps[name.replace("|", "_")] = (after("albedo"), after("normal"), glow, gm, float(num("tint", "1")), num("blend", "opaque") == "masked")  # FBX slot names replace | with _
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
        alb, nrm, glow, gm, tint, masked = maps.get(slot, (None, None, None, None, 1.0, False))
        mi_name = "MI_" + slot
        mi_path = f"{dest}/Materials/{mi_name}"
        mi = unreal.load_asset(mi_path) if unreal.EditorAssetLibrary.does_asset_exist(mi_path) else \
            unreal.AssetToolsHelpers.get_asset_tools().create_asset(mi_name, f"{dest}/Materials", unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        mel.set_material_instance_parent(mi, master(True) if masked else parent)
        if tint != 1.0:
            # TWEAK_Albedo multiplier is linear; BaseColor is linear in UE, so the factor applies as is (clamped by shading).
            mel.set_material_instance_vector_parameter_value(mi, "Tint", unreal.LinearColor(tint, tint, tint, 1))
        if glow and gm:
            mel.set_material_instance_vector_parameter_value(mi, "GlowColor", unreal.LinearColor(gm[0], gm[1], gm[2], 1))
        for pname, base in (("BaseColorTex", alb), ("NormalTex", nrm), ("GlowTex", glow)):
            if base and unreal.EditorAssetLibrary.does_asset_exist(f"{dest}/Textures/{base}"):
                mel.set_material_instance_texture_parameter_value(mi, pname, unreal.load_asset(f"{dest}/Textures/{base}"))
        unreal.EditorAssetLibrary.save_loaded_asset(mi)
        sm.set_editor_property("material_interface", mi)
        mats[i] = sm
        done += 1
    sk.set_editor_property("materials", mats)
    unreal.EditorAssetLibrary.save_loaded_asset(sk)
    return done

def anims(src, dest, mesh_path):
    """M2 F: every anims/<clip>.fbx onto the imported mesh's skeleton (legacy FBX, animation only) into <dest>/Anims."""
    adir = os.path.join(src, "anims")
    sk = unreal.load_asset(mesh_path)
    if not os.path.isdir(adir) or not isinstance(sk, unreal.SkeletalMesh):
        return 0
    skel = sk.get_editor_property("skeleton")
    tasks = []
    for f in sorted(os.listdir(adir)):
        if not f.lower().endswith(".fbx"):
            continue
        ui = unreal.FbxImportUI()
        ui.set_editor_property("import_mesh", False)
        ui.set_editor_property("import_as_skeletal", True)
        ui.set_editor_property("mesh_type_to_import", unreal.FBXImportType.FBXIT_ANIMATION)
        ui.set_editor_property("skeleton", skel)
        ui.set_editor_property("import_materials", False)
        ui.set_editor_property("import_textures", False)
        ui.set_editor_property("import_animations", True)
        ui.set_editor_property("create_physics_asset", False)
        ui.anim_sequence_import_data.set_editor_property("import_bone_tracks", True)
        # walk_01 (0.533 s) failed at the detected 12 fps ("not frame-border aligned"): snap to the closest frame
        for k, v in (("snap_to_closest_frame_boundary", True), ("use_default_sample_rate", False), ("custom_sample_rate", 30)):
            try: ui.anim_sequence_import_data.set_editor_property(k, v)
            except Exception: pass
        t = unreal.AssetImportTask()
        t.set_editor_property("filename", os.path.join(adir, f))
        t.set_editor_property("destination_path", dest + "/Anims")
        t.set_editor_property("destination_name", os.path.splitext(f)[0])
        t.set_editor_property("automated", True)
        t.set_editor_property("replace_existing", True)
        t.set_editor_property("save", True)
        t.set_editor_property("options", ui)
        tasks.append(t)
    n = 0
    for i in range(0, len(tasks), 20):
        n += len([p for p in import_tasks(tasks[i:i + 20]) if "AnimSequence" in str(unreal.EditorAssetLibrary.find_asset_data(p).asset_class_path.asset_name)])
    return n

def cleanup(dest):
    """Delete only our own unused importer-created FBXLegacyPhong MICs in <dest> (no referencers)."""
    gone = []
    for a in unreal.EditorAssetLibrary.list_assets(dest, recursive=False):
        o = unreal.load_asset(a)
        if isinstance(o, unreal.MaterialInstanceConstant) and o.get_editor_property("parent") and \
                o.get_editor_property("parent").get_name() == "FBXLegacyPhongSurfaceMaterial":
            if not unreal.EditorAssetLibrary.find_package_referencers_for_asset(a, False):
                if unreal.EditorAssetLibrary.delete_asset(a):
                    gone.append(a)
    return gone

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
        n_anims = anims(d, dest, mesh) if os.environ.get("POE2_UE_ANIMS", "1") == "1" else 0
        removed = cleanup(dest)
        listed = unreal.EditorAssetLibrary.list_assets(dest, recursive=True)
        classes = {}
        for a in listed:
            data = unreal.EditorAssetLibrary.find_asset_data(a)
            c = str(data.asset_class_path.asset_name)
            classes[c] = classes.get(c, 0) + 1
        report[name] = {"dest": dest, "textures": len(tex), "fbx_objects": [str(x) for x in assets], "material_slots": mats, "anims_imported": n_anims, "removed": removed, "classes": classes}
        log(f"{name}: {classes}")
    unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
    with open(result, "w") as f:
        json.dump(report, f, indent=1)

if __name__ == "__main__":
    main(sys.argv[1:])

# (v1 master cleanup is done by ue_poe2_check.py runs; see docs/POE2_M2.md step F)
