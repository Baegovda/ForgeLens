# mtl1-13 Manny visual check in Simulate-In-Editor (the editor world does not tick anims).
# modes: prep | start | pose <anim> <t> | cap <tag> | stop | clear. Ortho 300 cm base-color captures centred on the pelvis xy, z=100:
# ground z=0 is row 640 of 768 (front = looking from +Y toward -Y, side = from +X).
import sys, unreal
mode = sys.argv[1]
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
OUT = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Captures/mtl_fix"
if mode == "prep":
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Manny")]: eas.destroy_actor(a)
    a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(0, 3000, 0), unreal.Rotator(0, 0, 0)); a.tags = [unreal.Name("SM2Manny"), unreal.Name("mesh")]
    c = a.skeletal_mesh_component; c.set_skinned_asset_and_update(unreal.load_asset("/Game/Characters/Mannequins/Meshes/SKM_Manny"), True)
    c.set_editor_property("visibility_based_anim_tick_option", unreal.VisibilityBasedAnimTickOption.ALWAYS_TICK_POSE_AND_REFRESH_BONES)
    for v in ("front", "side"):
        cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(0, 0, 0), unreal.Rotator(0, 0, 0)); cam.tags = [unreal.Name("SM2Manny"), unreal.Name(v)]
        cam.capture_component2d.set_editor_property("capture_every_frame", False)
    print("MCAP prepped")
elif mode == "clear":
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Manny")]: eas.destroy_actor(a)
    print("MCAP cleared")
elif mode == "start": les.editor_play_simulate(); print("MCAP started")
elif mode == "stop": les.editor_request_end_play(); print("MCAP stopped")
else:
    w = ues.get_game_world(); acts = unreal.GameplayStatics.get_all_actors_with_tag(w, "SM2Manny")
    a = next(x for x in acts if x.actor_has_tag("mesh")); c = a.skeletal_mesh_component
    if mode == "pose":
        c.play_animation(unreal.load_asset(sys.argv[2]), False); c.set_position(float(sys.argv[3]), False); c.set_play_rate(0.0); print("MCAP pose", sys.argv[3])
    elif mode == "cap":
        tag = sys.argv[2]; L = lambda b: c.get_socket_location(b) - a.get_actor_location()
        rl, pv, hd, fl, fr = [L(b) for b in ("root", "pelvis", "head", "foot_l", "foot_r")]
        print("MCAP %s root=(%.0f %.0f %.0f) pelvis=(%.0f %.0f %.0f) head=(%.0f %.0f %.0f) footz=%.0f/%.0f" % (tag, rl.x, rl.y, rl.z, pv.x, pv.y, pv.z, hd.x, hd.y, hd.z, fl.z, fr.z))
        P = c.get_socket_location("pelvis"); base = a.get_actor_location()
        for cam in [x for x in acts if not x.actor_has_tag("mesh")]:
            view = "front" if cam.actor_has_tag("front") else "side"
            o = unreal.Vector(P.x, P.y, base.z + 100); pos = o + (unreal.Vector(0, 400, 0) if view == "front" else unreal.Vector(400, 0, 0))
            cam.set_actor_location_and_rotation(pos, unreal.MathLibrary.find_look_at_rotation(pos, o), False, True)
            cc = cam.capture_component2d; cc.set_editor_property("projection_type", unreal.CameraProjectionMode.ORTHOGRAPHIC); cc.set_editor_property("ortho_width", 300.0)
            rt = unreal.RenderingLibrary.create_render_target2d(w, 768, 768, unreal.TextureRenderTargetFormat.RTF_RGBA8)
            cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR)
            cc.show_only_actor_components(a); E = unreal.SceneCapturePrimitiveRenderMode; cc.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
            cc.capture_scene(); unreal.RenderingLibrary.export_render_target(w, rt, OUT, "manny_%s_%s.png" % (tag, view))
        print("MCAP capped", tag)
