"""PoE2 M3: import a --poe2level folder (static FBX + PNGs) under /Game/ForgeLens/POE/Levels/<name>, build a scratch map
with the room, lights and a SceneCapture2D, and write viewport captures (RTF_RGBA8 non-sRGB target, FINAL_COLOR_LDR).
usage: ue_poe2_level.py <result.json> <export dir> <png out dir> [import|map|cap|all]
Only touches /Game/ForgeLens/POE/Levels. Refuses when the open map has unsaved changes; reloads the previously open map at the end."""
import json, os, sys, math
import unreal

ROOT = "/Game/ForgeLens/POE/Levels"
EAL = unreal.EditorAssetLibrary
MEL = unreal.MaterialEditingLibrary
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ue_poe2_import as imp  # master(), import_tasks()

def les(): return unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
def eas(): return unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def world(): return unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()

def part_lines(src):
    out = {}
    for line in open(os.path.join(src, "poe2level.txt"), encoding="utf-8", errors="replace"):
        if not line.startswith("part "): continue
        w = line.split()
        def after(k):
            if k in w and w.index(k) + 1 < len(w) and w[w.index(k) + 1] not in ("normal", "mat"):
                return os.path.splitext(os.path.basename(w[w.index(k) + 1]))[0]
            return None
        out[w[1].replace("|", "_")] = (after("albedo"), after("normal"))
    return out

CEL = "/Game/ForgeLens/POE/_Shared/M_Poe2Celestial"

def celestial_master():
    """CelestialMaterial approximation (void_hideoutc.mat): unlit-looking dark base, emissive = SpaceTex.R * ColorR + SpaceTex.B * ColorB
    (the mat's 'Space Tex R/B Channel_1 Color'), UV * Tile. Scroll, the second layer and muddle distortion are not modelled (INFERRED)."""
    if EAL.does_asset_exist(CEL): return unreal.load_asset(CEL)
    pkg, name = CEL.rsplit("/", 1)
    m = unreal.AssetToolsHelpers.get_asset_tools().create_asset(name, pkg, unreal.Material, unreal.MaterialFactoryNew())
    tc = MEL.create_material_expression(m, unreal.MaterialExpressionTextureCoordinate, -900, 0)
    tile = MEL.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -900, 150); tile.set_editor_property("parameter_name", "Tile"); tile.set_editor_property("default_value", 0.3)
    uvm = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -700, 50)
    MEL.connect_material_expressions(tc, "", uvm, "A"); MEL.connect_material_expressions(tile, "", uvm, "B")
    t = MEL.create_material_expression(m, unreal.MaterialExpressionTextureSampleParameter2D, -500, 0); t.set_editor_property("parameter_name", "SpaceTex")
    MEL.connect_material_expressions(uvm, "", t, "UVs")
    cr = MEL.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -500, 300); cr.set_editor_property("parameter_name", "ColorR")
    cb = MEL.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -500, 500); cb.set_editor_property("parameter_name", "ColorB")
    k = MEL.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -500, 700); k.set_editor_property("parameter_name", "Intensity"); k.set_editor_property("default_value", 0.1)
    mr = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -250, 200); MEL.connect_material_expressions(t, "R", mr, "A"); MEL.connect_material_expressions(cr, "", mr, "B")
    mb = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -250, 400); MEL.connect_material_expressions(t, "B", mb, "A"); MEL.connect_material_expressions(cb, "", mb, "B")
    ad = MEL.create_material_expression(m, unreal.MaterialExpressionAdd, -100, 300); MEL.connect_material_expressions(mr, "", ad, "A"); MEL.connect_material_expressions(mb, "", ad, "B")
    mk = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, 50, 300); MEL.connect_material_expressions(ad, "", mk, "A"); MEL.connect_material_expressions(k, "", mk, "B")
    MEL.connect_material_property(mk, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    bc = MEL.create_material_expression(m, unreal.MaterialExpressionConstant3Vector, 50, 0); bc.set_editor_property("constant", unreal.LinearColor(0.004, 0.005, 0.012, 1))
    MEL.connect_material_property(bc, "", unreal.MaterialProperty.MP_BASE_COLOR)
    r = MEL.create_material_expression(m, unreal.MaterialExpressionConstant, 50, 150); r.set_editor_property("r", 0.9)
    MEL.connect_material_property(r, "", unreal.MaterialProperty.MP_ROUGHNESS)
    MEL.recompile_material(m); EAL.save_loaded_asset(m)
    return m

def do_import(src, name, rep):
    dest = f"{ROOT}/{name}"
    tex = imp.textures(src, dest)
    for p in tex:
        t = unreal.load_asset(p)
        if isinstance(t, unreal.Texture2D) and not t.get_name().lower().endswith(("ns", "normal_dxt5", "_normal")):
            pass
    ui = unreal.FbxImportUI()
    ui.set_editor_property("import_mesh", True)
    ui.set_editor_property("import_as_skeletal", False)
    ui.set_editor_property("mesh_type_to_import", unreal.FBXImportType.FBXIT_STATIC_MESH)
    ui.set_editor_property("import_materials", False)
    ui.set_editor_property("import_textures", False)
    ui.set_editor_property("import_animations", False)
    sd = ui.static_mesh_import_data
    sd.set_editor_property("combine_meshes", True)
    sd.set_editor_property("auto_generate_collision", False)
    for k, v in (("generate_lightmap_u_vs", False), ("build_nanite", False)):
        try: sd.set_editor_property(k, v)
        except Exception: pass
    t = unreal.AssetImportTask()
    t.set_editor_property("filename", os.path.join(src, name + ".fbx"))
    t.set_editor_property("destination_path", dest)
    t.set_editor_property("destination_name", "SM_" + name)
    t.set_editor_property("automated", True); t.set_editor_property("replace_existing", True); t.set_editor_property("save", True)
    t.set_editor_property("options", ui)
    objs = imp.import_tasks([t])
    sm = unreal.load_asset(f"{dest}/SM_{name}")
    maps = part_lines(src)
    mats = list(sm.get_editor_property("static_materials"))
    done = []
    for i, m in enumerate(mats):
        slot = str(m.get_editor_property("material_slot_name"))
        key = slot if slot in maps else next((k for k in maps if k.startswith(slot) or slot.startswith(k)), None)
        alb, nrm = maps.get(key, (None, None)) if key else (None, None)
        mi_name = "MI_" + slot[:60]
        mi_path = f"{dest}/Materials/{mi_name}"
        mi = unreal.load_asset(mi_path) if EAL.does_asset_exist(mi_path) else unreal.AssetToolsHelpers.get_asset_tools().create_asset(
            mi_name, f"{dest}/Materials", unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
        MEL.set_material_instance_parent(mi, celestial_master() if slot.startswith("terrain_") else imp.master())
        for pname, base in (("BaseColorTex", alb), ("NormalTex", nrm)):
            if base and EAL.does_asset_exist(f"{dest}/Textures/{base}"):
                MEL.set_material_instance_texture_parameter_value(mi, pname, unreal.load_asset(f"{dest}/Textures/{base}"))
        if slot.startswith("terrain_") and EAL.does_asset_exist(f"{dest}/Textures/space_pack01_DXT5"):
            # void_hideoutc.mat values: 'Space Tex R Channel_1 Color' (1.15, 3.5, 4.0), 'B Channel_1' (1.5, 8, 12), 'Space Texture Tile_1' 0.3
            MEL.set_material_instance_texture_parameter_value(mi, "SpaceTex", unreal.load_asset(f"{dest}/Textures/space_pack01_DXT5"))
            MEL.set_material_instance_vector_parameter_value(mi, "ColorR", unreal.LinearColor(1.15, 3.5, 4.0, 1))
            MEL.set_material_instance_vector_parameter_value(mi, "ColorB", unreal.LinearColor(1.5, 8.0, 12.0, 1))
        EAL.save_loaded_asset(mi)
        m.set_editor_property("material_interface", mi); mats[i] = m
        done.append([slot, key, alb, nrm])
    sm.set_editor_property("static_materials", mats)
    EAL.save_loaded_asset(sm)
    b = sm.get_bounding_box()
    rep["import"] = {"textures": len(tex), "objects": [str(o) for o in objs], "slots": done,
                     "bounds_min": [b.min.x, b.min.y, b.min.z], "bounds_max": [b.max.x, b.max.y, b.max.z],
                     "tris": sm.get_num_triangles(0) if hasattr(sm, "get_num_triangles") else None}
    return sm

def do_map(name, rep):
    dest = f"{ROOT}/{name}"
    mp = f"{dest}/L_{name}"
    if EAL.does_asset_exist(mp): les().load_level(mp)
    else: les().new_level(mp)
    for a in eas().get_all_level_actors():
        if a.get_actor_label().startswith("POE3_"): eas().destroy_actor(a)
    sm = unreal.load_asset(f"{dest}/SM_{name}")
    a = eas().spawn_actor_from_object(sm, unreal.Vector(0, 0, 0)); a.set_actor_label("POE3_room")
    l = eas().spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 2000), unreal.Rotator(roll=0, pitch=-55, yaw=-30)); l.set_actor_label("POE3_sun")
    l.light_component.set_editor_property("intensity", 6.0)
    s = eas().spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 1000)); s.set_actor_label("POE3_sky")
    s.light_component.set_editor_property("intensity", 1.0)
    s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    cube = unreal.load_asset("/Engine/MapTemplates/Sky/SunsetAmbientCubemap")
    if cube: s.light_component.set_editor_property("cubemap", cube)
    les().save_current_level()
    rep["map"] = mp

def do_cap(name, out_dir, rep):
    dest = f"{ROOT}/{name}"
    sm = unreal.load_asset(f"{dest}/SM_{name}")
    b = sm.get_bounding_box(); c = (b.min + b.max) * 0.5; ext = (b.max - b.min) * 0.5
    rt_path = f"{dest}/RT_cap"
    rt = unreal.load_asset(rt_path) if EAL.does_asset_exist(rt_path) else unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "RT_cap", dest, unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
    rt.set_editor_property("size_x", 1600); rt.set_editor_property("size_y", 900)
    rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB)
    rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8)  # non-sRGB (toggle re-inits)
    EAL.save_loaded_asset(rt)
    # doodads sit near the room centre (waypoint at 23.8 m, 23.8 m in PoE space); views: overview, centre, objects close
    cx, cy = c.x, c.y
    views = {"overview": (unreal.Vector(cx - ext.x * 1.1, cy - ext.y * 1.1, ext.x * 1.3), -40, 45, 60),
             "room": (unreal.Vector(cx - 2600, cy - 2600, 2400), -35, 45, 60),
             "objects": (unreal.Vector(cx - 900, cy + 900, 650), -25, -45, 50),
             "top": (unreal.Vector(cx, cy, ext.x * 2.2), -89.9, 0, 60)}
    shots = []
    for k, (pos, pitch, yaw, fov) in views.items():
        ca = eas().spawn_actor_from_class(unreal.SceneCapture2D, pos, unreal.Rotator(roll=0, pitch=pitch, yaw=yaw)); ca.set_actor_label("POE3_cap")
        cc = ca.capture_component2d
        cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
        cc.set_editor_property("fov_angle", fov)
        pps = cc.get_editor_property("post_process_settings")
        for kk, vv in (("override_auto_exposure_min_brightness", True), ("auto_exposure_min_brightness", 1.0),
                       ("override_auto_exposure_max_brightness", True), ("auto_exposure_max_brightness", 1.0)):
            try: pps.set_editor_property(kk, vv)
            except Exception: pass
        cc.set_editor_property("post_process_settings", pps)
        cc.capture_scene()
        f = f"ue_level_{k}.png"
        unreal.RenderingLibrary.export_render_target(world(), rt, out_dir.replace("\\", "/"), f)
        shots.append([k, os.path.join(out_dir, f), os.path.exists(os.path.join(out_dir, f))])
        eas().destroy_actor(ca)
    rep["caps"] = shots
    rep["bounds"] = [[b.min.x, b.min.y, b.min.z], [b.max.x, b.max.y, b.max.z]]

def main(a):
    result, src, out_dir = a[0], a[1], a[2]
    step = a[3] if len(a) > 3 else "all"
    name = os.path.splitext([f for f in os.listdir(src) if f.lower().endswith(".fbx")][0])[0]
    rep = {"name": name}
    prev = world().get_path_name().split(".")[0] if world() else None
    rep["prev_map"] = prev
    dirty = [str(p.get_name()) for p in unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages()]
    rep["dirty_maps_before"] = dirty
    try:
        if step in ("import", "all"): do_import(src, name, rep)
        if step in ("map", "cap", "all"):
            if [d for d in dirty if not d.startswith(("/Game/ForgeLens/POE", "/Temp/"))]:
                rep["refused"] = "open map has unsaved changes"; return
            do_map(name, rep)
            if step in ("cap", "all"): do_cap(name, out_dir, rep)
            les().save_current_level()
            if prev and not prev.startswith((ROOT, "/Temp/")) and EAL.does_asset_exist(prev):
                les().load_level(prev); rep["restored"] = prev
    except Exception as e:
        import traceback; rep["error"] = traceback.format_exc()
    finally:
        json.dump(rep, open(result, "w"), indent=1)

if __name__ == "__main__":
    main(sys.argv[1:])
