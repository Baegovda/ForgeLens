# ue_r72_mats.py (R72): why vortex/sorcerer show no textures in UE, and the fix.
# Cause (PROVEN 10/30): Characters/cc_vortex_beast and Characters/cc_sorcerer_exalted had every slot = WorldGridMaterial
# and no MIs/textures in their folder (mesh imported with import_materials=False, the sidecar material step never ran;
# and their chars_phys52 export is DDS-only, which UE 5.7 cannot import for BC7 -> textures fail even when it runs);
# the R67 test duplicates (Tests/r67_phys/cc_vortex_beast_cloth, Tests/r67_cloth/*) inherited that.
# Modes (uerun.ps1 <this> <mode>[,args]):
#   state                      slot -> material per mesh (WorldGrid count)
#   apply,<name>[,<exp>]       run the exported import_sm2_materials.py (Exports/<exp>, default r72 = png export) into Characters/<name>
#   copy                       copy Characters MIs onto the test duplicates by slot name
#   scene[,ev]                 Tests/r72_mats/Map_Mats: grey floor, lights, manual exposure, 4 actors + 1 capture each
#   cap,<dir>,<tag>            <dir>/<tag>_<actor>.png
import sys, math, os, runpy, unreal
W = "C:/Users/Revaptor_FX/Desktop/SM2_Work"; F = "/Game/SM2_Work/Tests/r72_mats"; C = "/Game/SM2_Work/Characters/"
EAL = unreal.EditorAssetLibrary
MESHES = {"vortex": C + "cc_vortex_beast/cc_vortex_beast", "vortex_cloth": "/Game/SM2_Work/Tests/r67_phys/cc_vortex_beast_cloth",
          "sorcerer": C + "cc_sorcerer_exalted/cc_sorcerer_exalted", "sorcerer_ref": "/Game/SM2_Work/Tests/r67_cloth/cc_sorcerer_exalted_ref"}
TESTS = {"cc_vortex_beast": ["/Game/SM2_Work/Tests/r67_phys/cc_vortex_beast_cloth"],
         "cc_sorcerer_exalted": ["/Game/SM2_Work/Tests/r67_cloth/cc_sorcerer_exalted_" + s for s in ("ref", "b0", "r13", "r13b0", "skin0")]}
def les(): return unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
def eas(): return unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def world():
    u = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem); return u.get_game_world() or u.get_editor_world()
def slots(m): return [(str(s.material_slot_name), s.material_interface) for s in m.get_editor_property("materials")]
def state():
    for k, p in list(MESHES.items()) + [(t.rsplit("/", 1)[-1], t) for v in TESTS.values() for t in v]:
        m = unreal.load_asset(p)
        if m is None: print("R72 STATE", k, "missing"); continue
        s = slots(m); wg = sum(1 for _, mi in s if mi is None or "WorldGrid" in mi.get_path_name())
        mi_tex = 0
        for _, mi in s:
            if isinstance(mi, unreal.MaterialInstanceConstant):
                if any(tp.parameter_value for tp in mi.get_editor_property("texture_parameter_values")): mi_tex += 1
        print("R72 STATE", k, "slots", len(s), "worldgrid", wg, "mi_with_texture", mi_tex)
def apply(n, exp="r72"):
    # exp = Exports subfolder. chars_phys52 is DDS-only (BC7 -> UE 5.7 InterchangeDDSTranslator "DXGIFormat not
    # supported : 98"), so R72 re-exports with --tex-formats png into Exports/r72/<n>.
    root = W + "/Exports/%s/%s/tpl/%s.tpl/" % (exp, n, n)
    sys.argv = [root + "import_sm2_materials.py", root + n + ".materials.json", C + n]
    runpy.run_path(sys.argv[0], run_name="__main__")
    print("R72 APPLY", n, "textures", len(EAL.list_assets(C + n + "/Textures", False, False)) if EAL.does_directory_exist(C + n + "/Textures") else 0)
def copy():
    for n, tests in TESTS.items():
        src = dict(slots(unreal.load_asset(C + n + "/" + n)))
        for t in tests:
            m = unreal.load_asset(t)
            if m is None: continue
            ss = list(m.get_editor_property("materials")); hit = 0
            for i, s in enumerate(ss):
                mi = src.get(str(s.material_slot_name))
                if mi is not None and "WorldGrid" not in mi.get_path_name():
                    s.set_editor_property("material_interface", mi); ss[i] = s; hit += 1
            m.set_editor_property("materials", ss); EAL.save_loaded_asset(m)
            print("R72 COPY", t.rsplit("/", 1)[-1], hit, "/", len(ss))
def pp(s, ev):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True)
    s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
def grey_mat():
    p = F + "/M_r72_grey"
    if EAL.does_asset_exist(p): return unreal.load_asset(p)
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset("M_r72_grey", F, unreal.Material, unreal.MaterialFactoryNew())
    MEL = unreal.MaterialEditingLibrary
    c = MEL.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -300, 0); c.set_editor_property("constant", unreal.LinearColor(0.18, 0.18, 0.18, 1))
    MEL.connect_material_property(c, "", unreal.MaterialProperty.MP_BASE_COLOR)
    MEL.recompile_material(mat); EAL.save_loaded_asset(mat); return mat
def tagged(cls, pre):
    return {str(t): a for a in unreal.GameplayStatics.get_all_actors_of_class(world(), cls) for t in a.tags if str(t).startswith(pre)}
def scene(ev=0.0):
    mp = F + "/Map_Mats"; E = eas()
    if EAL.does_asset_exist(mp): les().load_level(mp); [E.destroy_actor(x) for x in E.get_all_level_actors()]
    else: les().new_level(mp)
    fl = E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(200, 200, 1)); fl.static_mesh_component.set_material(0, grey_mat())
    d = E.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(roll=0, pitch=-45, yaw=60)); d.light_component.set_intensity(2.0)
    s = E.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500)); s.light_component.set_intensity(1.0)
    s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    s.light_component.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/DaylightAmbientCubemap")); s.light_component.recapture_sky()
    v = E.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0)); v.set_editor_property("unbound", True)
    st = v.get_editor_property("settings"); pp(st, ev); v.set_editor_property("settings", st)
    T = unreal.AssetToolsHelpers.get_asset_tools(); x = 0.0
    for k, p in MESHES.items():
        mesh = unreal.load_asset(p); b = mesh.get_bounds(); ext = b.box_extent
        w = max(ext.x, ext.y); x += w + 200
        a = E.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(x, 0, 0), unreal.Rotator(0, 0, 90))
        a.skeletal_mesh_component.set_skinned_asset_and_update(mesh); a.set_actor_label("R72_" + k); a.tags = ["R72_" + k]
        a.skeletal_mesh_component.set_editor_property("disable_cloth_simulation", True)
        pl = E.spawn_actor_from_class(unreal.PointLight, unreal.Vector(x, max(600, w * 2), ext.z * 1.5)); pl.point_light_component.set_intensity(3000); pl.point_light_component.set_attenuation_radius(max(3000, w * 6))
        fov = 35.0; dist = max(ext.z * 2 * 0.6, w * 0.75) / math.tan(math.radians(fov / 2)) + 100
        rp = F + "/RT_" + k
        rt = unreal.load_asset(rp) if EAL.does_asset_exist(rp) else T.create_asset("RT_" + k, F, unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
        rt.set_editor_property("size_x", 900); rt.set_editor_property("size_y", 1000); rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB); EAL.save_loaded_asset(rt)
        cp = unreal.Vector(x + dist * 0.35, dist, ext.z * 1.3); tg = unreal.Vector(x, 0, ext.z)
        cap = E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.MathLibrary.find_look_at_rotation(cp, tg)); cap.tags = ["cap_" + k]
        cc = cap.capture_component2d; cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", fov)
        ps = cc.get_editor_property("post_process_settings"); pp(ps, ev); cc.set_editor_property("post_process_settings", ps); cc.set_editor_property("post_process_blend_weight", 1.0)
        x += w + 200
    les().save_current_level(); print("R72 scene actors", len(MESHES))
def cap(d, tag):
    for k, c in tagged(unreal.SceneCapture2D, "cap_").items():
        cc = c.capture_component2d; cc.capture_scene()
        unreal.RenderingLibrary.export_render_target(world(), cc.get_editor_property("texture_target"), d.replace("\\", "/"), "%s_%s.png" % (tag, k[4:]))
    print("R72 cap", tag)
a = [t for x in sys.argv[1:] if not x.startswith("-") for t in x.split(",") if t]
m = a[0] if a else "state"
if m == "state": state()
elif m == "apply": apply(a[1], a[2] if len(a) > 2 else "r72")
elif m == "copy": copy()
elif m == "scene": scene(float(a[1]) if len(a) > 1 else 0.0)
elif m == "cap": cap(a[1], a[2])
elif m == "open": les().load_level(F + "/Map_Mats")
