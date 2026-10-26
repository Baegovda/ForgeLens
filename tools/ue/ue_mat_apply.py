# Material-finishing pass: rerun the new import_sm2_materials (Substrate two-lobe graph) on an existing step3 export
# into /Game/SM2_Work/Characters/<asset>, then report switches/params vs the sidecar and the master stats.
import sys, runpy, json, unreal
name = sys.argv[1]; mode = sys.argv[2] if len(sys.argv) > 2 else "apply"
root = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/step3_" + name + "/tpl/" + name + ".tpl/"
folder = "/Game/SM2_Work/Characters/" + name
script = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Scripts/import_sm2_materials_mat.py"
if mode == "apply":
    sys.argv = [script, root + name + ".materials.json", folder]
    runpy.run_path(script, run_name="__main__")
mel = unreal.MaterialEditingLibrary
mesh = unreal.load_asset(folder + "/" + name)
data = json.load(open(root + name + ".materials.json", encoding="utf-8"))
want = {s["slot"]: s for s in data["slots"]}
bad = []; rows = 0; on = 0
def sc(mi, k): return round(mel.get_material_instance_scalar_parameter_value(mi, k), 4)
for slot in mesh.materials:
    sn = str(slot.material_slot_name); mi = slot.material_interface
    if not isinstance(mi, unreal.MaterialInstanceConstant): continue
    rows += 1
    s = want.get(sn, {}); a = s.get("anisotropicSpec") or {}; ss = s.get("secondSpec") or {}
    got = tuple(mel.get_material_instance_static_switch_parameter_value(mi, k) for k in ("UseSecondSpec", "UseAnisoSpec", "UseAnisoDirMap"))
    exp = (bool(ss), bool(a), bool(a.get("dirMap")))
    if got != exp: bad.append((sn, "switch", got, exp))
    if ss:
        on += 1
        chk = {"SecondMetalnessScale": ss["metalness"]["scale"], "SecondMetalnessBias": ss["metalness"]["bias"], "SecondRoughnessScale": ss["roughness"]["scale"], "SecondRoughnessBias": ss["roughness"]["bias"], "SecondSpecGain": 1.0 if a else 4.0}
        for k, v in chk.items():
            if abs(sc(mi, k) - v) > 1e-3: bad.append((sn, k, sc(mi, k), v))
        t = mel.get_material_instance_vector_parameter_value(mi, "SecondSpecTint")
        lin = [pow(c, 2.2) for c in ss["metallicTint"][:3]]
        if max(abs(t.r - lin[0]), abs(t.g - lin[1]), abs(t.b - lin[2])) > 1e-3: bad.append((sn, "tint", (t.r, t.g, t.b), lin))
    if a:
        e = a.get("extra") or {}
        chk = {"AnisoAnisotropy": a["anisotropy"], "AnisoAngle": a["angle"], "AnisoIntensity": a["intensity"], "AnisoExtraAnisotropy": e.get("anisotropy", 0.5), "AnisoExtraIntensity": e.get("intensity", 1.0), "AnisoExtraShift": e.get("shift", 0.2)}
        for k, v in chk.items():
            if abs(sc(mi, k) - v) > 1e-3: bad.append((sn, k, sc(mi, k), v))
        print("ANISO", sn, {k: sc(mi, k) for k in chk}, "second" if ss else "")
print("SLOTS", len(mesh.materials), "MIs", rows, "secondSpec", on, "MISMATCH", bad)
for m in ("M_SM2", "M_SM2_Translucent"):
    mat = unreal.load_asset(folder + "/" + m)
    st = mel.get_statistics(mat)
    print("STATS", m, "vs", st.num_vertex_shader_instructions, "ps", st.num_pixel_shader_instructions, "samplers", st.num_samplers, "front", mel.get_material_property_input_node(mat, unreal.MaterialProperty.MP_FRONT_MATERIAL))
