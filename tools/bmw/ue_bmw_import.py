"""BMW M1: import a tools/bmw export (CUE4Parse .glb + PNG + material .json) into /Game/ForgeLens/BMW/<name>.
usage (in the editor): ue_bmw_import.py <result.json> <export root> <glb relative path> <dest>
Mesh through the glTF importer (automated). Per material slot: PM_Diffuse / PM_Normals textures from the material .json
are imported to <dest>/Textures and an MI_<slot> on the shared lit master (POE M_Poe2Lit_v2; same pipeline) is assigned."""
import json, os, sys
import unreal

res_path, root, rel, dest = sys.argv[1:5]
MASTER = "/Game/ForgeLens/POE/_Shared/M_Poe2Lit_v2"
out = {"dest": dest, "log": []}
def log(m): out["log"].append(m); unreal.log("[bmw] " + m)

def task(fn, d):
    t = unreal.AssetImportTask()
    t.set_editor_property("filename", fn); t.set_editor_property("destination_path", d)
    t.set_editor_property("automated", True); t.set_editor_property("replace_existing", True); t.set_editor_property("save", True)
    return t

def run(tasks):
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
    r = []
    for t in tasks: r += [str(x) for x in (t.get_editor_property("imported_object_paths") or [])]
    return r

glb = os.path.join(root, rel)
paths = run([task(glb, dest)])
out["mesh_import"] = paths
sk = next((unreal.load_asset(p) for p in paths if isinstance(unreal.load_asset(p), unreal.SkeletalMesh)), None)
if sk is None:
    for a in unreal.EditorAssetLibrary.list_assets(dest, recursive=True):
        o = unreal.load_asset(a)
        if isinstance(o, unreal.SkeletalMesh): sk = o; break
if sk is None:
    log("no skeletal mesh imported")
else:
    jsons = {}
    for d, _, fs in os.walk(root):
        for f in fs:
            if f.endswith(".json"): jsons.setdefault(f[:-5].lower(), os.path.join(d, f))
    def png(mat, key):
        jf = jsons.get(mat.lower())
        if not jf: return None
        t = json.load(open(jf, encoding="utf-8")).get("Textures", {}).get(key)
        if not t: return None
        p = os.path.join(root, t.rsplit(".", 1)[0].replace("/", os.sep) + ".png")
        return p if os.path.exists(p) else None
    texcache = {}
    def tex(p, normal):
        if p in texcache: return texcache[p]
        r = run([task(p, dest + "/Textures")])
        t = unreal.load_asset(r[0]) if r else None
        if t and normal:
            t.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_NORMALMAP)
            t.set_editor_property("srgb", False); unreal.EditorAssetLibrary.save_loaded_asset(t)
        texcache[p] = t; return t
    parent = unreal.load_asset(MASTER)
    mel = unreal.MaterialEditingLibrary
    mats = list(sk.get_editor_property("materials"))
    slots = []
    for i, sm in enumerate(mats):
        slot = str(sm.get_editor_property("material_slot_name"))
        a, n = png(slot, "PM_Diffuse"), png(slot, "PM_Normals")
        mi = unreal.AssetToolsHelpers.get_asset_tools().create_asset("MI_" + slot, dest + "/Materials", unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew()) \
            if not unreal.EditorAssetLibrary.does_asset_exist(dest + "/Materials/MI_" + slot) else unreal.load_asset(dest + "/Materials/MI_" + slot)
        mel.set_material_instance_parent(mi, parent)
        if a: mel.set_material_instance_texture_parameter_value(mi, "BaseColorTex", tex(a, False))
        if n: mel.set_material_instance_texture_parameter_value(mi, "NormalTex", tex(n, True))
        unreal.EditorAssetLibrary.save_loaded_asset(mi)
        sm.set_editor_property("material_interface", mi); mats[i] = sm
        slots.append({"slot": slot, "albedo": bool(a), "normal": bool(n)})
    sk.set_editor_property("materials", mats)
    unreal.EditorAssetLibrary.save_loaded_asset(sk)
    out["mesh"] = sk.get_path_name(); out["slots"] = slots
    out["bones"] = len(sk.get_editor_property("skeleton").get_editor_property("bone_tree"))
    out["bounds_cm"] = str(sk.get_bounds().box_extent)
json.dump(out, open(res_path, "w"), indent=1)
