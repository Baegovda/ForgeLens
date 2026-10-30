# ue_r92_pngimp.py (R92): import the 2 PNGs of the R72 sorcerer export that never became assets
# (ch_exalted_sorcerer_hair_01_ao: referenced only by material.json, not by the materials.json maps; __default_material__: 68 B placeholder)
import unreal
SRC = "C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/r72/cc_sorcerer_exalted/pct/"
DST = "/Game/ForgeLens/SM2/Textures/cc_sorcerer_exalted"
tasks = []
for n, srgb in (("ch_exalted_sorcerer_hair_01_ao", False), ("__default_material__", True)):
    t = unreal.AssetImportTask(); t.filename = SRC + n + ".png"; t.destination_path = DST; t.automated = True; t.replace_existing = True; t.save = False
    tasks.append((t, srgb))
unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t for t, _ in tasks])
for t, srgb in tasks:
    for p in t.imported_object_paths:
        tex = unreal.load_asset(p)
        tex.set_editor_property("srgb", srgb)
        if not srgb: tex.set_editor_property("compression_settings", unreal.TextureCompressionSettings.TC_GRAYSCALE)
        unreal.EditorAssetLibrary.save_loaded_asset(tex, False)
        print("R92 png", p, tex.blueprint_get_size_x(), tex.blueprint_get_size_y(), "srgb", tex.get_editor_property("srgb"))
