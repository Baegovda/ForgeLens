# physlive.py start|sample|cap|stop <name> <set> : Simulate-In-Editor so the twin actors (spawned by ue_phys_verify spawn) really tick.
import unreal, sys, math, json
mode, name, SET = sys.argv[1], sys.argv[2], sys.argv[3]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
if mode == "start":
    les.editor_play_simulate(); print("PHYSV sim start")
elif mode == "cams":
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2PhysCam")]: eas.destroy_actor(a)
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Phys") and isinstance(a, unreal.SkeletalMeshActor)]:
        lab = "on" if a.actor_has_tag("on") else "off"; o, e = a.get_actor_bounds(False); r = e.length()
        for view, dv in (("front", unreal.Vector(1, 0, 0)), ("side", unreal.Vector(0, 1, 0))):
            pos = o + dv * (r * 2.2); rot = unreal.MathLibrary.find_look_at_rotation(pos, o)
            cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, pos, rot); cam.tags = [unreal.Name("SM2PhysCam"), unreal.Name(lab + "_" + view)]
            cam.capture_component2d.set_editor_property("capture_every_frame", False)
    print("PHYSV cams placed")
elif mode == "camclear":
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2PhysCam")]: eas.destroy_actor(a)
    print("PHYSV cams cleared")
elif mode == "stop":
    les.editor_request_end_play(); print("PHYSV sim stop")
else:
    w = ues.get_game_world(); print("PHYSV world", w.get_name() if w else None)
    acts = unreal.GameplayStatics.get_all_actors_with_tag(w, "SM2Phys")
    on = next(a for a in acts if a.actor_has_tag("on")); off = next(a for a in acts if a.actor_has_tag("off"))
    pj = json.load(open(W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name), encoding="utf-8-sig"))
    hb = []
    for c in pj.get("cloths", []):
        md = c.get("maxDistanceMeters", [])
        hb += [h["bone"] for i, h in enumerate(c.get("helperBones", [])) if not (i < len(md) and md[i] == 0)]
    hb += [j.get("bone") for j in pj.get("jiggles", pj.get("jiggleBones", [])) if isinstance(j, dict) and j.get("bone")]
    co, cf = on.skeletal_mesh_component, off.skeletal_mesh_component
    if mode == "play":
        import os; fd = r"K:/Unreal Projects/UE_VFX_57/Content/SM2_Work/Tests/%s/%s/Anims" % (SET, name); f = sorted(x for x in os.listdir(fd) if x.endswith(".uasset"))[0][:-7]; anim = unreal.load_asset("/Game/SM2_Work/Tests/%s/%s/Anims/%s" % (SET, name, f))
        cf.set_editor_property("disable_post_process_blueprint", True); mm = cf.get_skeletal_mesh_asset(); cf.set_skeletal_mesh_asset(None); cf.set_skeletal_mesh_asset(mm); print("PHYSV offpp", cf.get_post_process_instance())
        for c in (co, cf): c.set_editor_property("visibility_based_anim_tick_option", unreal.VisibilityBasedAnimTickOption.ALWAYS_TICK_POSE_AND_REFRESH_BONES); c.play_animation(anim, True)
        print("PHYSV play", anim.get_name())
    if mode == "sample":
        d = [(co.get_socket_location(b) - cf.get_socket_location(b)).length() for b in hb]
        nan = any(math.isnan(x) or math.isinf(x) for x in d)
        print("PHYSV", name, "t=%.2f/%.2f" % (co.get_position(), cf.get_position()), "h0on", [round(x,1) for x in (co.get_socket_location(hb[0]).x, co.get_socket_location(hb[0]).z)], "h0off", [round(x,1) for x in (cf.get_socket_location(hb[0]).x, cf.get_socket_location(hb[0]).z)], "bones=%d mean=%.2f max=%.2f nan=%s" % (len(d), sum(d) / max(1, len(d)), max(d or [0]), nan))
    elif mode == "cap":
        tag = sys.argv[4]; n = 0
        for cam in unreal.GameplayStatics.get_all_actors_with_tag(w, "SM2PhysCam"):
            key = [str(t) for t in cam.tags if str(t) != "SM2PhysCam"][0]; lab, view = key.split("_")
            a = on if lab == "on" else off
            c = cam.capture_component2d; rt = unreal.RenderingLibrary.create_render_target2d(w, 768, 768, unreal.TextureRenderTargetFormat.RTF_RGBA8)
            c.set_editor_property("texture_target", rt); c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR)
            c.show_only_actor_components(a); E = unreal.SceneCapturePrimitiveRenderMode; c.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
            c.capture_scene(); unreal.RenderingLibrary.export_render_target(w, rt, W + "/Captures/mtl_fix", "phys_%s_%s_%s_%s.png" % (name, tag, "physON" if lab == "on" else "animOnly", view)); n += 1
        print("PHYSV captured", n)

