# ue_r97_orbmats.py (R97s): import vortex VFX textures + build additive Niagara sprite materials (Emissive = tex.rgb*tex.a*ParticleColor.rgb*Intensity, opacity n/a).
import unreal
D = "/Game/ForgeLens/SM2/VFX/vortex_orb"; SRC = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Exports/r97/vortex_vfx/export/pct/"
E = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools(); M = unreal.MaterialEditingLibrary
tex = {}
for n in ["spark_01", "part_chaos_rad_runes_blur", "fx_warp_in"]:
    p = D + "/T_" + n
    if not E.does_asset_exist(p):
        t = unreal.AssetImportTask(); t.filename = SRC + n + ".tga"; t.destination_path = D; t.destination_name = "T_" + n; t.automated = True; t.save = True; t.replace_existing = True
        AT.import_asset_tasks([t])
    tex[n] = unreal.load_asset(p); print("R97S tex", p, tex[n] is not None)
def mat(name, t, inten, tint):
    p = D + "/" + name
    if E.does_asset_exist(p): E.delete_asset(p)
    m = AT.create_asset(name, D, unreal.Material, unreal.MaterialFactoryNew())
    m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_ADDITIVE); m.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)
    m.set_editor_property("used_with_niagara_sprites", True); m.set_editor_property("two_sided", True)
    ts = M.create_material_expression(m, unreal.MaterialExpressionTextureSample, -600, 0); ts.set_editor_property("texture", t)
    pc = M.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -600, 250); pc.set_editor_property("parameter_name", "Tint"); pc.set_editor_property("default_value", unreal.LinearColor(*tint))  # R97t: fixed tint (ParticleColor unused)
    k = M.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -600, 400); k.set_editor_property("parameter_name", "Intensity"); k.set_editor_property("default_value", inten)
    m1 = M.create_material_expression(m, unreal.MaterialExpressionMultiply, -350, 0); M.connect_material_expressions(ts, "RGB", m1, "A"); M.connect_material_expressions(ts, "A", m1, "B")
    m2 = M.create_material_expression(m, unreal.MaterialExpressionMultiply, -200, 100); M.connect_material_expressions(m1, "", m2, "A"); M.connect_material_expressions(pc, "RGB", m2, "B")
    m3 = M.create_material_expression(m, unreal.MaterialExpressionMultiply, -100, 200); M.connect_material_expressions(m2, "", m3, "A"); M.connect_material_expressions(ts, "A", m3, "B")
    m4 = M.create_material_expression(m, unreal.MaterialExpressionMultiply, 0, 300); M.connect_material_expressions(m3, "", m4, "A"); M.connect_material_expressions(k, "", m4, "B")
    M.connect_material_property(m4, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR); M.recompile_material(m); E.save_loaded_asset(m); print("R97S mat", p)
mat("M_orb_glow", tex["spark_01"], 0.7, (1.0, 0.35, 0.9, 1)); mat("M_orb_runes", tex["part_chaos_rad_runes_blur"], 1.5, (0.55, 0.3, 1.0, 1)); mat("M_orb_sparks", tex["spark_01"], 6.0, (0.4, 0.55, 1.0, 1)); mat("M_orb_warp", tex["fx_warp_in"], 2.0, (0.6, 0.3, 1.0, 1))



