# R64: readable lighting + captures for the R54 scratch level map (only /Game/ForgeLens/SM2/Tests/r54_level).
# Modes: stats | light | capture <out_dir> | export
import unreal, sys, builtins, collections
MAP = "/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge"
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
world = ues.get_editor_world()
assert "/Game/ForgeLens/SM2/Tests/r54_level/" in world.get_path_name(), world.get_path_name()
mode = sys.argv[1]
acts = [a for a in eas.get_all_level_actors() if a.actor_has_tag("R54")]
def q(v, f): v = sorted(v); return v[int((len(v) - 1) * f)]
def frame():
    L = [a.get_actor_location() for a in acts]
    cell = collections.Counter((int(v.x // 4000), int(v.y // 4000), int(v.z // 4000)) for v in L).most_common(1)[0][0]
    seed = unreal.Vector((cell[0] + .5) * 4000, (cell[1] + .5) * 4000, (cell[2] + .5) * 4000)
    near = [v for v in L if (v - seed).length() < 20000] or L
    xs = [v.x for v in near]; ys = [v.y for v in near]; zs = [v.z for v in near]
    c = unreal.Vector(q(xs, .5), q(ys, .5), q(zs, .5))
    size = max(q(xs, .9) - q(xs, .1), q(ys, .9) - q(ys, .1), 2000.0)
    return c, size, near
def pp(s, ev):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
if mode == "stats":
    tags = collections.Counter(t for a in eas.get_all_level_actors() for t in a.tags)
    print("R64 actors", len(acts), "tags", dict((str(k), v) for k, v in tags.items()))
    print("R64 mesh_types", len(set(a.static_mesh_component.static_mesh.get_name() for a in acts if a.static_mesh_component.static_mesh)))
    c, size, near = frame(); print("R64 frame", c, size, "near", len(near))
elif mode == "fixzero":  # re-import templates whose UE mesh came out with zero bounds (scratch Meshes folder only)
    import os
    d = sys.argv[2]; lim = int(sys.argv[3]) if len(sys.argv) > 3 else 9999
    bad = sorted(set(a.static_mesh_component.static_mesh for a in acts if a.static_mesh_component.static_mesh and (lambda b: (b.max - b.min).length() == 0)(a.static_mesh_component.static_mesh.get_bounding_box())), key=lambda m: m.get_name())[:lim]
    tasks = []
    for m in bad:
        f = os.path.join(d, "templates", m.get_name()[3:] + ".obj")
        if not os.path.exists(f): print("R64 noobj", m.get_name()); continue
        t = unreal.AssetImportTask(); t.filename = f; t.destination_path = m.get_path_name().rsplit("/", 1)[0]
        t.destination_name = m.get_name(); t.automated = True; t.replace_existing = True; t.save = True; tasks.append(t)
    if tasks: unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
    still = [m.get_name() for m in bad if (lambda b: (b.max - b.min).length() == 0)(unreal.load_asset(m.get_path_name()).get_bounding_box())]
    print("R64 fixzero tried", len(tasks), "still_zero", len(still), still[:5])
elif mode == "light":
    for a in eas.get_all_level_actors():
        if a.actor_has_tag("R54L") or a.actor_has_tag("R64L"): eas.destroy_actor(a)
    c, size, near = frame(); T = [unreal.Name("R64L")]
    d = eas.spawn_actor_from_class(unreal.DirectionalLight, c + unreal.Vector(0, 0, 5000), unreal.Rotator(roll=0, pitch=-55, yaw=35)); d.tags = T
    d.light_component.set_intensity(10.0); d.light_component.set_cast_shadows(False); d.light_component.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE)
    d2 = eas.spawn_actor_from_class(unreal.DirectionalLight, c, unreal.Rotator(roll=0, pitch=40, yaw=-145)); d2.tags = T; d2.light_component.set_intensity(4.0); d2.light_component.set_cast_shadows(False); d2.light_component.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE)
    k = eas.spawn_actor_from_class(unreal.SkyLight, c); k.tags = T
    kc = k.light_component; kc.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE); kc.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP); kc.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/SunsetAmbientCubemap")); kc.set_cast_shadows(False)
    kc.set_editor_property("lower_hemisphere_is_black", False); kc.set_intensity(4.0); kc.recapture_sky()
    np = 0
    for v in near[::max(1, len(near) // 24)]:  # ~24 fill point lights over the dense area
        p = eas.spawn_actor_from_class(unreal.PointLight, v + unreal.Vector(0, 0, 400)); p.tags = T
        p.light_component.set_intensity(3000.0); p.light_component.set_attenuation_radius(3000.0); p.light_component.set_cast_shadows(False); np += 1
    s = eas.spawn_actor_from_class(unreal.SpotLight, c + unreal.Vector(-size * .3, -size * .3, size * .3), unreal.Rotator(roll=0, pitch=-35, yaw=45)); s.tags = T
    s.light_component.set_intensity(50000.0); s.light_component.set_attenuation_radius(size * 2)
    v = eas.spawn_actor_from_class(unreal.PostProcessVolume, c); v.tags = T; v.set_editor_property("unbound", True)
    st = v.get_editor_property("settings"); pp(st, 0.0); v.set_editor_property("settings", st)
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
    print("R64 lights dir 2 sky 1 point", np, "spot 1 ppv 1 (manual exposure)")
elif mode == "capture":
    out = sys.argv[2]; ev = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0
    c, size, near = frame()
    # close-ups: the two densest 10 m cells
    cells = collections.Counter((int(v.x // 1000), int(v.y // 1000), int(v.z // 1000)) for v in near).most_common(2)
    views = [("overview", c + unreal.Vector(-size * .6, -size * .6, size * .5), unreal.Rotator(roll=0, pitch=-30, yaw=45), 60)]
    for i, (cl, n) in enumerate(cells):
        cc = unreal.Vector((cl[0] + .5) * 1000, (cl[1] + .5) * 1000, (cl[2] + .5) * 1000)
        views.append(("close%d" % (i + 1), cc + unreal.Vector(-1800, -1800, 1200), unreal.Rotator(roll=0, pitch=-25, yaw=45), 70))
    pend = []
    for tag, loc, rot, fov in views:
        rt = unreal.RenderingLibrary.create_render_target2d(world, 1600, 1000, unreal.TextureRenderTargetFormat.RTF_RGBA8)
        cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, loc, rot); cam.tags = [unreal.Name("R64Cap")]
        cc2 = cam.capture_component2d; cc2.set_editor_property("texture_target", rt)
        cc2.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc2.set_editor_property("fov_angle", fov)
        st = cc2.get_editor_property("post_process_settings"); pp(st, ev); cc2.set_editor_property("post_process_settings", st)
        cc2.set_editor_property("post_process_blend_weight", 1.0)
        cc2.capture_scene(); pend.append((rt, out, "ue_r64_" + tag + ".png", cam)); print("R64 view", tag, loc)
    builtins._r64_pending = pend; print("R64 captured", len(pend))
elif mode == "export":
    import os
    for rt, out, name, cam in builtins._r64_pending:
        f = os.path.join(out, name)
        if os.path.exists(f): os.remove(f)  # export_render_target does not overwrite
        unreal.RenderingLibrary.export_render_target(world, rt, out, name)
        try: eas.destroy_actor(cam)
        except Exception: pass
    builtins._r64_pending = []
    print("R64 exported", len(builtins._r64_pending))
