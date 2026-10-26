# Step 3 import: FBX into /Game/SM2_Work/Characters/<name> (Props/<name> when <name> is not cc_*), then the exported import_sm2_materials.py, then a switch report.
import sys, runpy, json, unreal
name = sys.argv[1]
root = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/step3_" + name + "/tpl/" + name + ".tpl/"
folder = "/Game/SM2_Work/" + ("Characters/" if name.startswith("cc_") else "Props/") + name
if sys.argv[2] == "import":
    t = unreal.AssetImportTask(); t.filename = root + name + ".fbx"; t.destination_path = folder
    t.automated = True; t.replace_existing = True; t.save = True
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_materials = False; o.import_textures = False; o.import_animations = False
    o.import_as_skeletal = True; o.mesh_type_to_import = unreal.FBXImportType.FBXIT_SKELETAL_MESH
    t.options = o
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
    sys.argv = [root + "import_sm2_materials.py", root + name + ".materials.json", folder]
    runpy.run_path(sys.argv[0], run_name="__main__")
    print("IMPORTED", name)
else:
    mel = unreal.MaterialEditingLibrary
    mesh = unreal.load_asset(folder + "/" + name)
    data = json.load(open(root + name + ".materials.json", encoding="utf-8"))
    want = {s["slot"]: s for s in data["slots"]}
    rows = []; bad = []
    for slot in mesh.materials:
        sn = str(slot.material_slot_name); mi = slot.material_interface
        if not isinstance(mi, unreal.MaterialInstanceConstant):
            rows.append((sn, mi.get_name() if mi else None)); continue
        got = tuple(mel.get_material_instance_static_switch_parameter_value(mi, k) for k in ("UseSecondSpec", "UseAnisoSpec", "UseAnisoDirMap"))
        s = want.get(sn, {}); a = s.get("anisotropicSpec") or {}
        exp = (bool(s.get("secondSpec")), bool(a), bool(a.get("dirMap")))
        if got != exp: bad.append((sn, got, exp))
        if any(got):
            vals = {k: round(mel.get_material_instance_scalar_parameter_value(mi, k), 3) for k in ("SecondMetalnessBias", "SecondRoughnessBias", "AnisoAnisotropy", "AnisoAngle", "AnisoIntensity")}
            tint = mel.get_material_instance_vector_parameter_value(mi, "SecondSpecTint")
            dtex = mel.get_material_instance_texture_parameter_value(mi, "AnisoDir")
            rows.append((sn, mi.get_editor_property("parent").get_name(), got, vals, (round(tint.r,3), round(tint.g,3), round(tint.b,3), round(tint.a,3)), dtex.get_name() if dtex else None))
    for r in rows: print("ROW", r)
    print("SLOTS", len(mesh.materials), "MISMATCH", bad)
    for m in ("M_SM2", "M_SM2_Translucent"):
        mat = unreal.load_asset(folder + "/" + m)
        st = mel.get_statistics(mat)
        print("STATS", m, "vs", st.num_vertex_shader_instructions, "ps", st.num_pixel_shader_instructions, "samplers", st.num_samplers)
