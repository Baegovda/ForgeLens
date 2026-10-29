# phys52_live.py spawn|start|play|status|stop|clean : before/after for the Titus box (phys52).
# "anim" actor = post-process ABP disabled (== the pre-phys52 box: PA_5 had Belt_jiggle kinematic and no box_* bodies),
# "phys" actor = mesh post-process ABP_6 (34 jiggle bodies + SM2Damper node). Tick callback records bone data and captures PNGs.
import unreal, sys, json, math, os
mode = sys.argv[1]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; OUT = W + "/Captures/phys52"
MESH = "/Game/SM2_Work/Tests/chars_phys/cc_titus_assault/cc_titus_assault"
ANIM = "/Game/SM2_Work/Sequences/TitusWalkFire/Anims/cc_titus_assault__tacticus_wpn1_walk_fwd_intercessor"
BONES = ["CENTRE", "Belt_jiggle", "box_jiggle_01", "box_jiggle_02", "box_jiggle_end"]
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
if mode == "spawn":
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2P52")]: eas.destroy_actor(a)
    m = unreal.load_asset(MESH)
    for lab, y in (("phys", 0.0), ("anim", 400.0)):
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(0, y, 0), unreal.Rotator(0, 0, 0))
        a.skeletal_mesh_component.set_skeletal_mesh_asset(m); a.tags = [unreal.Name("SM2P52"), unreal.Name(lab)]
        for view in ("side", "front"):
            cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(0, y, 150), unreal.Rotator(0, 0, 0))
            cam.tags = [unreal.Name("SM2P52"), unreal.Name("cam"), unreal.Name(lab + "_" + view)]
            c = cam.capture_component2d; c.set_editor_property("capture_every_frame", False); c.set_editor_property("capture_on_movement", False); c.set_editor_property("fov_angle", 35.0)
    print("P52 spawned")
elif mode == "start":
    les.editor_play_simulate(); print("P52 sim start")
elif mode == "stop":
    les.editor_request_end_play(); print("P52 sim stop")
elif mode == "clean":
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2P52")]: eas.destroy_actor(a)
    print("P52 cleaned")
elif mode == "status":
    print("P52 status", open(OUT + "/status.txt").read() if os.path.exists(OUT + "/status.txt") else "none")
elif mode == "play":
    os.makedirs(OUT, exist_ok=True)
    w = ues.get_game_world(); acts = unreal.GameplayStatics.get_all_actors_with_tag(w, "SM2P52")
    meshes = {("phys" if a.actor_has_tag("phys") else "anim"): a for a in acts if isinstance(a, unreal.SkeletalMeshActor)}
    cams = {}
    for a in acts:
        if a.actor_has_tag("cam"):
            cams[[str(t) for t in a.tags if "_" in str(t)][0]] = a
    anim = unreal.load_asset(ANIM)
    ca = meshes["anim"].skeletal_mesh_component; cp = meshes["phys"].skeletal_mesh_component
    ca.set_editor_property("disable_post_process_blueprint", True); mm = ca.get_skeletal_mesh_asset(); ca.set_skeletal_mesh_asset(None); ca.set_skeletal_mesh_asset(mm)
    for c in (ca, cp):
        c.set_editor_property("visibility_based_anim_tick_option", unreal.VisibilityBasedAnimTickOption.ALWAYS_TICK_POSE_AND_REFRESH_BONES); c.play_animation(anim, True)
    print("P52 pp", cp.get_post_process_instance(), ca.get_post_process_instance())
    rts = {}
    for k, cam in cams.items():
        rt = unreal.RenderingLibrary.create_render_target2d(w, 480, 480, unreal.TextureRenderTargetFormat.RTF_RGBA8)
        c = cam.capture_component2d; c.set_editor_property("texture_target", rt); c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR)
        c.show_only_actor_components(meshes[k.split("_")[0]]); E = unreal.SceneCapturePrimitiveRenderMode
        c.set_editor_property("primitive_render_mode", next(getattr(E, k2) for k2 in dir(E) if "SHOW_ONLY" in k2)); rts[k] = rt
    st = {"t": 0.0, "n": 0, "rec": [], "cap": 0, "h": None}
    T_WARM, T_REC_END, T_CAP0, T_CAP1, CAP_EVERY = 1.0, 7.0, 4.6, 5.8, 1
    RTS = unreal.RelativeTransformSpace.RTS_COMPONENT
    def tick(dt):
        try:
            st["t"] += dt; st["n"] += 1; t = st["t"]
            if t < T_WARM: return
            row = {"t": round(t, 4), "dt": round(dt, 4)}
            for lab, c in (("phys", cp), ("anim", ca)):
                row[lab] = {b: [round(v, 2) for v in (lambda p: (p.x, p.y, p.z))(c.get_socket_transform(b, RTS).translation)] for b in BONES}
                row[lab + "_pos"] = round(c.get_position(), 4)
            st["rec"].append(row)
            if T_CAP0 <= t <= T_CAP1 and st["n"] % CAP_EVERY == 0:
                for k, cam in cams.items():
                    c = cp if k.startswith("phys") else ca
                    tgt = (c.get_socket_location("box_jiggle_02") + c.get_socket_location("box_jiggle_end")) * 0.5; fwd = meshes[k.split("_")[0]].get_actor_forward_vector(); rgt = meshes[k.split("_")[0]].get_actor_right_vector()
                    dv = rgt if k.endswith("side") else fwd
                    pos = tgt + dv * 170.0; cam.set_actor_location_and_rotation(pos, unreal.MathLibrary.find_look_at_rotation(pos, tgt), False, False)
                    cam.capture_component2d.capture_scene()
                    unreal.RenderingLibrary.export_render_target(w, rts[k], OUT, "%s_%03d.png" % (k, st["cap"]))
                st["cap"] += 1
            if t > T_REC_END:
                unreal.unregister_slate_post_tick_callback(st["h"])
                json.dump(st["rec"], open(OUT + "/rec.json", "w"))
                open(OUT + "/status.txt", "w").write("done ticks=%d rows=%d caps=%d" % (st["n"], len(st["rec"]), st["cap"]))
        except Exception as ex:
            unreal.unregister_slate_post_tick_callback(st["h"]); open(OUT + "/status.txt", "w").write("error %r" % ex)
    if os.path.exists(OUT + "/status.txt"): os.remove(OUT + "/status.txt")
    st["h"] = unreal.register_slate_post_tick_callback(tick)
    print("P52 play registered", anim.get_name(), list(cams.keys()))
