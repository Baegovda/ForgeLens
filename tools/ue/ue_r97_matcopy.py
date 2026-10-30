# ue_r97_matcopy.py (R97w): give the own-skeleton re-imports their textured MIs by slot name (from our textured meshes / MI_<slot> in our folders). Saves only our meshes.
import unreal
E = unreal.EditorAssetLibrary
JOBS = [("/Game/ForgeLens/SM2/Characters/cc_titus_cloth/cc_titus_assault", "/Game/ForgeLens/SM2/Characters/cc_titus_assault/cc_titus_assault", "/Game/ForgeLens/SM2/Characters/cc_titus_assault"),
        ("/Game/ForgeLens/SM2/Characters/cc_chaplain/cc_chaplain", "/Game/ForgeLens/SM2/Tests/chars_phys/cc_chaplain/cc_chaplain", "/Game/ForgeLens/SM2/Tests/chars_phys/cc_chaplain")]
for dst, src, fold in JOBS:
    d = unreal.load_asset(dst); s = unreal.load_asset(src)
    sm = {str(x.get_editor_property("material_slot_name")): x.get_editor_property("material_interface") for x in s.get_editor_property("materials")}
    ms = list(d.get_editor_property("materials")); n = 0; miss = []
    for x in ms:
        k = str(x.get_editor_property("material_slot_name")); mi = sm.get(k)
        if (mi is None or "WorldGrid" in mi.get_path_name()) and E.does_asset_exist(fold + "/MI_" + k): mi = unreal.load_asset(fold + "/MI_" + k)
        if mi and "WorldGrid" not in mi.get_path_name() and mi.get_path_name().startswith("/Game/ForgeLens/"): x.set_editor_property("material_interface", mi); n += 1
        else: miss.append(k)
    d.set_editor_property("materials", ms); E.save_loaded_asset(d, False); print("R97W mats", dst, n, "/", len(ms), "miss", miss[:10])
