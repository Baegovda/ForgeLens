# ue_r92_floor.py (R92): floor exposure EV 1.0 vs 1.2 on the 8 UE characters (/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor).
# R72 lighting (grey 0.18 floor, dir 2 + DaylightAmbient sky 1 + 1 point per actor), manual exposure.
# Captures turn the EyeAdaptation show flag ON: SceneCapture2D has it off by default, so R72/R90 capture EVs had no effect (R92).
# uerun.ps1 <this> scene | set,<ev> | cap,<dir>,<tag>   (cap in a separate call after set)
import sys, math, unreal
F = "/Game/ForgeLens/SM2/Tests/r92_floor"; EAL = unreal.EditorAssetLibrary
C = "/Game/SM2_Work/Characters/"; T2 = "/Game/ForgeLens/SM2/Tests/chars_cloth2/"
CH = {"vortex": C + "cc_vortex_beast/cc_vortex_beast", "sorcerer": C + "cc_sorcerer_exalted/cc_sorcerer_exalted", "calgar": C + "cc_calgar/cc_calgar",
      "hormagaunt": C + "cc_swarm_hormagaunt/cc_swarm_hormagaunt", "titus": C + "cc_titus_assault/cc_titus_assault",
      "chaplain": T2 + "cc_chaplain/cc_chaplain", "commissar": T2 + "cc_commissar/cc_commissar", "tech_priest": T2 + "cc_tech_priest/cc_tech_priest"}
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def world(): return unreal.EditorLevelLibrary.get_editor_world()
def pp(s, ev):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
def caps(): return {str(t)[4:]: x for x in E.get_all_level_actors() for t in x.tags if str(t).startswith("cap_")}
def scene(ev=9.0):  # R95: 1.2 overexposed (manual EV, physical camera off) -> near-white armour
    mp = F + "/Map_Floor"
    if EAL.does_asset_exist(mp): les.load_level(mp); [E.destroy_actor(x) for x in E.get_all_level_actors()]
    else: les.new_level(mp)
    fl = E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(400, 400, 1)); fl.static_mesh_component.set_material(0, unreal.load_asset("/Game/SM2_Work/Tests/r72_mats/M_r72_grey"))
    d = E.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(roll=0, pitch=-45, yaw=60)); d.light_component.set_intensity(2.0)
    s = E.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500)); s.light_component.set_intensity(1.0)
    s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    s.light_component.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/DaylightAmbientCubemap")); s.light_component.recapture_sky()
    v = E.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0)); v.set_editor_property("unbound", True)
    st = v.get_editor_property("settings"); pp(st, ev); v.set_editor_property("settings", st)
    AT = unreal.AssetToolsHelpers.get_asset_tools(); x = 0.0
    for k, p in CH.items():
        mesh = unreal.load_asset(p); ext = mesh.get_bounds().box_extent; w = max(ext.x, ext.y); x += w + 300
        a = E.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(x, 0, 0), unreal.Rotator(0, 0, 0))
        a.skeletal_mesh_component.set_skinned_asset_and_update(mesh); a.set_actor_label("R92_" + k); a.tags = ["R92_" + k]
        a.skeletal_mesh_component.set_editor_property("disable_cloth_simulation", True)
        pl = E.spawn_actor_from_class(unreal.PointLight, unreal.Vector(x, max(600, w * 2), ext.z * 1.5)); pl.point_light_component.set_intensity(3000); pl.point_light_component.set_attenuation_radius(max(3000, w * 6))
        fov = 35.0; dist = max(ext.z * 2 * 0.6, w * 0.75) / math.tan(math.radians(fov / 2)) + 100
        rp = F + "/RT_" + k
        rt = unreal.load_asset(rp) if EAL.does_asset_exist(rp) else AT.create_asset("RT_" + k, F, unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
        rt.set_editor_property("size_x", 900); rt.set_editor_property("size_y", 1000); rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB); EAL.save_loaded_asset(rt)
        cp = unreal.Vector(x + dist * 0.35, dist, ext.z * 1.3); tg = unreal.Vector(x, 0, ext.z * 0.8)
        cap = E.spawn_actor_from_class(unreal.SceneCapture2D, cp, unreal.MathLibrary.find_look_at_rotation(cp, tg)); cap.tags = ["cap_" + k]
        cc = cap.capture_component2d; cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", fov)
        f = unreal.EngineShowFlagsSetting(); f.set_editor_property("show_flag_name", "EyeAdaptation"); f.set_editor_property("enabled", True)
        cc.set_editor_property("show_flag_settings", [f])
        ps = cc.get_editor_property("post_process_settings"); pp(ps, ev); cc.set_editor_property("post_process_settings", ps); cc.set_editor_property("post_process_blend_weight", 1.0)
        x += w + 300
    les.save_current_level(); print("R92 floor scene", len(CH))
a = [t for x in sys.argv[1:] for t in x.split(",") if t]
if a[0] == "scene": scene()
else:
    if "Map_Floor" not in str(world().get_path_name()): les.load_level(F + "/Map_Floor")
    if a[0] == "set":
        for k, c in caps().items():
            cc = c.capture_component2d; ps = cc.get_editor_property("post_process_settings"); pp(ps, float(a[1])); cc.set_editor_property("post_process_settings", ps)
        print("R92 set ev", a[1])
    elif a[0] == "cap":
        for k, c in caps().items():
            cc = c.capture_component2d; cc.capture_scene(); cc.capture_scene()
            unreal.RenderingLibrary.export_render_target(world(), cc.get_editor_property("texture_target"), a[1], "%s_%s.png" % (a[2], k))
        print("R92 cap", a[2], len(caps()))
