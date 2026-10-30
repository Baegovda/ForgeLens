# vfx4-2: Unreal export TEST for the compared VFX (no Niagara). Imports only the textures + meshes those effects use
# (grenade part_mg_frag_grenade_expl, bolter muzzle/trail/smoke, finisher blood) and builds simple preview materials.
# Usage (each mode a separate uerun.ps1 call):
#   import <export_root> [dest]   textures/meshes from the SM2Extractor exports -> dest (default /Game/ForgeLens/SM2/VfxExport)
#   capture <dest>                spawns an SM2Cap-tagged board (one plane per texture material + both meshes) far away
#   export <out_dir>              writes the render target PNG and removes the SM2Cap actors
# Never deletes assets: an existing dest folder is renamed to <dest>_bakN first. /Game/SM2_Work/Original is not touched.
# Blend per texture = what the browser bench resolved (SPRDBG "blend"), Add -> additive, else translucent. Preview only:
# flipbooks show the whole sheet, no curves/tint (HEURISTIC preview, not the game's particle shader).
# mtl1-23 capture blend rules (LibSaber.SM2.Assets.Effects.VfxBlendRules, RenderDoc nightly 1.47) -> UE blend mode:
#   SFX meshes One/One (alpha Zero/One, plasma 101917)        -> BLEND_ADDITIVE (sfx_geom_muzzle uses the additive MI)
#   CPU particles premultiplied One/InvSrcAlpha (PS 101818)   -> BLEND_TRANSLUCENT (UE has no premultiplied mode;
#                                                                 BLEND_ALPHACOMPOSITE is the exact match, not wired yet)
#   trails SrcAlpha/InvSrcAlpha (CB_PASS_TRAIL)               -> BLEND_TRANSLUCENT (trail_smoke_hard)
#   sparks opaque, no blend (eid 2086 (NOTES 12:10 blend list))              -> BLEND_MASKED (the q_sparks textures here are sprite sheets,
#                                                                 kept translucent: name-based spark mapping is HEURISTIC)
import unreal, sys, os, glob, builtins
EAL = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools(); MEL = unreal.MaterialEditingLibrary
ADD = ["part_sparks_seq_b", "part_ashes_01", "part_radius_01", "candle_top", "part_sparks_basic", "part_muzzle_front_01",
       "part_muzzle_side_01", "phantom_hands"]
BLEND = ["part_debris_04", "q_sparks_a_01", "q_sparks_b_01", "q_sparks_b_02", "seq_fin_expl_g", "seq_fin_expl_d",
         "part_q_comet_seq_c", "part_mg_smoke_seq_1a", "part_seq_plasma_fluid_big_8x8", "trail_smoke_hard",
         "part_splash_central_01", "part_splash_mid_03"]
MESHES = ["sfx_geom_muzzle", "splashes_vtxa_lwi"]
mode = sys.argv[1]

def master(dest, name, additive):
    p = f"{dest}/Materials/{name}"
    if EAL.does_asset_exist(p): return unreal.load_asset(p)
    m = AT.create_asset(name, f"{dest}/Materials", unreal.Material, unreal.MaterialFactoryNew())
    m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_ADDITIVE if additive else unreal.BlendMode.BLEND_TRANSLUCENT)
    m.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)
    m.set_editor_property("two_sided", True)
    t = MEL.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -600, 0)
    t.set_editor_property("parameter_name", "Tex")
    k = MEL.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -600, 300)
    k.set_editor_property("parameter_name", "Tint"); k.set_editor_property("default_value", unreal.LinearColor(1, 1, 1, 1))
    mul = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -300, 0)
    MEL.connect_material_expressions(t, "RGB", mul, "A"); MEL.connect_material_expressions(k, "RGB", mul, "B")
    if additive:
        mul2 = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -150, 0)
        MEL.connect_material_expressions(mul, "", mul2, "A"); MEL.connect_material_expressions(t, "A", mul2, "B")
        MEL.connect_material_property(mul2, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    else:
        MEL.connect_material_property(mul, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
        MEL.connect_material_property(t, "A", unreal.MaterialProperty.MP_OPACITY)
    MEL.recompile_material(m); EAL.save_loaded_asset(m)
    return m

if mode == "import":
    root = sys.argv[2]; dest = sys.argv[3] if len(sys.argv) > 3 else "/Game/ForgeLens/SM2/VfxExport"
    assert dest.startswith("/Game/ForgeLens/SM2/") and not dest.startswith("/Game/SM2_Work/Original"), dest
    if EAL.does_directory_exist(dest):
        n = 1
        while EAL.does_directory_exist(f"{dest}_bak{n}"): n += 1
        EAL.rename_directory(dest, f"{dest}_bak{n}"); print("backup", f"{dest}_bak{n}")
    pngs = {}
    for f in glob.glob(os.path.join(root, "**", "*.png"), recursive=True):
        pngs.setdefault(os.path.splitext(os.path.basename(f))[0], f)
    tasks = []
    for name in ADD + BLEND:
        if name not in pngs: print("MISSING tex", name); continue
        t = unreal.AssetImportTask(); t.filename = pngs[name]; t.destination_path = f"{dest}/Textures"
        t.automated = True; t.save = True; t.replace_existing = False; tasks.append(t)
    for name in MESHES:
        f = glob.glob(os.path.join(root, name, "**", name + ".fbx"), recursive=True)
        if not f: print("MISSING mesh", name); continue
        t = unreal.AssetImportTask(); t.filename = f[0]; t.destination_path = f"{dest}/Meshes/{name}"
        t.automated = True; t.save = True; t.replace_existing = False
        o = unreal.FbxImportUI(); o.import_mesh = True; o.import_as_skeletal = False; o.import_materials = True
        o.import_textures = True; o.import_animations = False
        o.static_mesh_import_data.combine_meshes = True
        t.options = o; tasks.append(t)
    AT.import_asset_tasks(tasks)
    for t in tasks: print("imported", os.path.basename(t.filename), len(t.imported_object_paths))
    madd, mbl = master(dest, "M_SM2Vfx_Add", True), master(dest, "M_SM2Vfx_Blend", False)
    for name in ADD + BLEND:
        tex = unreal.load_asset(f"{dest}/Textures/{name}")
        if not tex: continue
        mi = AT.create_asset("MI_" + name, f"{dest}/Materials", unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        MEL.set_material_instance_parent(mi, madd if name in ADD else mbl)
        MEL.set_material_instance_texture_parameter_value(mi, "Tex", tex)
        EAL.save_loaded_asset(mi)
    # Mesh preview: muzzle rings use phantom_hands (mzl_geom td block, vfx3x) additive orange (mzl_geom tint 255,237,72).
    mi = unreal.load_asset(f"{dest}/Materials/MI_phantom_hands")
    if mi:
        MEL.set_material_instance_vector_parameter_value(mi, "Tint", unreal.LinearColor(1.0, 0.85, 0.07, 1)); EAL.save_loaded_asset(mi)
    print("done", dest)

elif mode == "capture":
    dest = sys.argv[2]
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for old in eas.get_all_level_actors():
        if old.actor_has_tag("SM2Cap"): eas.destroy_actor(old)
    base = unreal.Vector(300000, -300000, 50000); tag = [unreal.Name("SM2Cap")]
    plane = unreal.load_asset("/Engine/BasicShapes/Plane")
    names = ADD + BLEND; cols = 5
    # black backdrop so additive textures read correctly
    bg = eas.spawn_actor_from_object(plane, base + unreal.Vector(0, 0, -50)); bg.tags = tag
    bg.set_actor_scale3d(unreal.Vector(40, 40, 1))
    bg.static_mesh_component.set_material(0, unreal.load_asset("/Engine/EngineMaterials/BlackUnlitMaterial") or unreal.load_asset("/Engine/BasicShapes/BasicShapeMaterial"))
    for i, n in enumerate(names):
        mi = unreal.load_asset(f"{dest}/Materials/MI_{n}")
        if not mi: continue
        x, y = (i % cols - 2) * 120, (i // cols - 2) * 120
        a = eas.spawn_actor_from_object(plane, base + unreal.Vector(x, y, 0)); a.tags = tag
        a.static_mesh_component.set_material(0, mi)
    for j, n in enumerate(MESHES):
        sm = unreal.load_asset(f"{dest}/Meshes/{n}/{n}")
        if not sm: print("no mesh", n); continue
        a = eas.spawn_actor_from_object(sm, base + unreal.Vector(-420 + j * 840, 360, 0)); a.tags = tag
        b = sm.get_bounding_box(); s = 300.0 / max(1e-3, (b.max - b.min).length()); a.set_actor_scale3d(unreal.Vector(s, s, s))
        if n == "sfx_geom_muzzle":
            mi = unreal.load_asset(f"{dest}/Materials/MI_phantom_hands")
            for k in range(a.static_mesh_component.get_num_materials()): a.static_mesh_component.set_material(k, mi)
        print("mesh", n, "scale", round(s, 3), "mats", a.static_mesh_component.get_num_materials())
    rot = unreal.Rotator(0, -90, 0)
    cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, base + unreal.Vector(0, 0, 1150), rot); cam.tags = tag
    c = cam.capture_component2d
    rt = unreal.RenderingLibrary.create_render_target2d(w, 1600, 1200, unreal.TextureRenderTargetFormat.RTF_RGBA8)
    c.set_editor_property("texture_target", rt); c.set_editor_property("fov_angle", 70.0)
    c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
    # manual exposure (same recipe as ue_cap_lit.py) so eye adaptation does not darken the unlit board
    pps = c.get_editor_property("post_process_settings")
    pps.set_editor_property("override_auto_exposure_method", True); pps.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    pps.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); pps.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
    pps.set_editor_property("override_auto_exposure_bias", True); pps.set_editor_property("auto_exposure_bias", float(sys.argv[3]) if len(sys.argv) > 3 else 0.0)
    c.set_editor_property("post_process_settings", pps)
    c.capture_scene(); builtins._sm2_vfx_rt = rt; print("captured")

elif mode == "export":
    out = sys.argv[2]; os.makedirs(out, exist_ok=True)
    w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    rt = getattr(builtins, "_sm2_vfx_rt", None)
    unreal.RenderingLibrary.export_render_target(w, rt, out, "ue_export_board.png"); print("wrote", out)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for old in eas.get_all_level_actors():
        if old.actor_has_tag("SM2Cap"): eas.destroy_actor(old)
