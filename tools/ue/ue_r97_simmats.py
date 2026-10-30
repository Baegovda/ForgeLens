# ue_r97_simmats.py (R97x): give *_SIM proxy slots the MI of the most similar textured slot on the same mesh (name tokens). Saves only our meshes.
import unreal, re
E = unreal.EditorAssetLibrary
def toks(s): return set(t for t in re.split(r"[_\d]+", s.lower()) if t and t not in ("sim", "cloth", "01", "frnt", "bck"))
for p in ["/Game/ForgeLens/SM2/Characters/cc_titus_cloth/cc_titus_assault", "/Game/ForgeLens/SM2/Characters/cc_chaplain/cc_chaplain"]:
    m = unreal.load_asset(p); ms = list(m.get_editor_property("materials"))
    good = [(str(x.get_editor_property("material_slot_name")), x.get_editor_property("material_interface")) for x in ms if x.get_editor_property("material_interface") and "WorldGrid" not in x.get_editor_property("material_interface").get_path_name()]
    for x in ms:
        mi = x.get_editor_property("material_interface")
        if mi and "WorldGrid" not in mi.get_path_name(): continue
        k = str(x.get_editor_property("material_slot_name")); tk = toks(k)
        best = max(good, key=lambda g: (len(tk & toks(g[0])), "cloth" in g[0].lower() or "tbrd" in g[0].lower() or "skirt" in g[0].lower() or "scarf" in g[0].lower()))
        x.set_editor_property("material_interface", best[1]); print("R97X", k, "->", best[0])
    m.set_editor_property("materials", ms); E.save_loaded_asset(m, False)
