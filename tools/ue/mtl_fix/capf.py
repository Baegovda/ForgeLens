# mtl1-15m before/after framing: same camera for any import of a mesh. capture <asset> <out> <bone> <dist> | export
# env: spawn at (0,3000,0) in the level, level lighting only (sky/sun, auto exposure) like the user viewport. Otherwise lit like ue_cap_lit.py (3 movable point lights, manual exposure), FINAL colour, front/side/front3q views around <bone>.
import sys, builtins, unreal
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
if sys.argv[1] == "capture":
    asset, out, bone, r = sys.argv[2], sys.argv[3], sys.argv[4], float(sys.argv[5])
    for old in eas.get_all_level_actors():
        if old.actor_has_tag("SM2CapF"): eas.destroy_actor(old)
    env = len(sys.argv) > 6 and sys.argv[6] == "env"
    base = unreal.Vector(0, 3000, 0) if env else unreal.Vector(300000, 310000, 50000)
    a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, base); a.tags = [unreal.Name("SM2CapF")]
    comp = a.skeletal_mesh_component; comp.set_skeletal_mesh_asset(unreal.load_asset(asset))
    names = [str(b) for b in comp.get_all_socket_names()]
    hb = next((b for b in names if b.lower() == bone.lower()), None) or next((b for b in names if bone.lower() in b.lower()), None)
    o = comp.get_socket_location(hb) if hb else base + unreal.Vector(0, 0, 150)
    print("CAPF bone", hb, o)
    acts = [a]
    for off, mul in (() if env else ((unreal.Vector(-1.0, -0.6, 0.5), 1.0), (unreal.Vector(-0.6, 1.0, 0.1), 0.35), (unreal.Vector(1.0, 0.3, 0.6), 0.8))):
        L = eas.spawn_actor_from_class(unreal.PointLight, o + off * r * 1.2); L.tags = [unreal.Name("SM2CapF")]
        lc = L.point_light_component; lc.set_mobility(unreal.ComponentMobility.MOVABLE)
        lc.set_editor_property("intensity_units", unreal.LightUnits.CANDELAS); lc.set_intensity(40 * mul * (r / 100.0) ** 2); lc.set_attenuation_radius(r * 6)
    pend = []
    for tag, d in (("front", unreal.Vector(-1, 0, 0)), ("q3", unreal.Vector(-0.7, -0.7, 0.1)), ("side", unreal.Vector(0, -1, 0)), ("face", unreal.Vector(0.25, 1, 0.15))):
        d = d / d.length(); pos = o + d * r
        rt = unreal.RenderingLibrary.create_render_target2d(w, 1024, 1024, unreal.TextureRenderTargetFormat.RTF_RGBA8)
        cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, pos, unreal.MathLibrary.find_look_at_rotation(pos, o)); cam.tags = [unreal.Name("SM2CapF")]
        c = cam.capture_component2d; c.set_editor_property("texture_target", rt); c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
        c.set_editor_property("fov_angle", 45)
        c.show_only_actor_components(a); E = unreal.SceneCapturePrimitiveRenderMode; c.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
        if env: c.capture_scene(); pend.append((rt, out, tag + ".png")); continue
        pps = c.get_editor_property("post_process_settings")
        pps.set_editor_property("override_auto_exposure_method", True); pps.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
        pps.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); pps.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
        pps.set_editor_property("override_auto_exposure_bias", True); pps.set_editor_property("auto_exposure_bias", 0.0)
        c.set_editor_property("post_process_settings", pps); c.capture_scene(); pend.append((rt, out, tag + ".png"))
    builtins._sm2_capf = pend; print("CAPF captured", len(pend))
else:
    for rt, out, name in builtins._sm2_capf: unreal.RenderingLibrary.export_render_target(w, rt, out, name)
    for x in eas.get_all_level_actors():
        if x.actor_has_tag("SM2CapF"): eas.destroy_actor(x)
    print("CAPF exported", len(builtins._sm2_capf))
