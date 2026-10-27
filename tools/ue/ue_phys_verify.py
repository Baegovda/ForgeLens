# SM2 live-physics verify: twin actors (post-process ABP on/off), manual ticks, helper-bone deviation per step.
import sys, json, math, unreal
mode, name = sys.argv[1], sys.argv[2]; SET = sys.argv[3] if len(sys.argv) > 3 else "mtl_phys"
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; folder = "/Game/SM2_Work/Tests/%s/%s" % (SET, name)
pj = json.load(open(W + "/Exports/%s/%s/tpl/%s.tpl/%s.physics.json" % (SET, name, name, name), encoding="utf-8-sig"))
helpers = []
for c in pj.get("cloths", []):
    md = c.get("maxDistanceMeters", [])
    for i, h in enumerate(c.get("helperBones", [])):
        if not (i < len(md) and md[i] == 0): helpers.append(h["bone"])
helpers = helpers[::max(1, len(helpers) // 12)][:12]
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
mine = [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Phys")]
if mode == "spawn":
    for a in mine: eas.destroy_actor(a)
    m = unreal.load_asset(folder + "/" + name + (sys.argv[4] if len(sys.argv) > 4 else ""))  # optional mesh suffix, e.g. _ChaosCloth
    an = unreal.EditorAssetLibrary.list_assets(folder + "/Anims", False, False); anim = unreal.load_asset(an[0]) if an else None
    for tag, pp_off in (("on", False), ("off", True)):
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(0, 20000, 0)); a.tags = [unreal.Name("SM2Phys"), unreal.Name(tag)]
        c = a.skeletal_mesh_component; c.set_skeletal_mesh_asset(m); c.set_collision_enabled(unreal.CollisionEnabled.NO_COLLISION)  # test twins: no scene collision (no ComplexAsSimple warnings)
        c.set_editor_property("disable_post_process_blueprint", pp_off); c.set_update_animation_in_editor(True)
        if anim: c.play_animation(anim, True)
    print("PHYSV spawned", name, len(helpers))
elif mode == "sample":
    on = next(a for a in mine if a.actor_has_tag("on")).skeletal_mesh_component
    off = next(a for a in mine if a.actor_has_tag("off")).skeletal_mesh_component
    d = [(on.get_socket_location(b) - off.get_socket_location(b)).length() for b in helpers]
    z = [round(on.get_socket_location(b).z, 1) for b in helpers[:4]]
    print("PHYSV", name, "t=%.2f" % on.get_position(), "mean=%.2f max=%.2f nan=%s" % (sum(d) / len(d), max(d), any(math.isnan(x) for x in d)), "z", z)
elif mode == "clear":
    for a in mine: eas.destroy_actor(a)
    print("PHYSV cleared")
elif mode == "cap":   # cap <name> <set> <label>: render 'on' (left) and 'off' (offset right) into kept RTs; export next call
    tag = sys.argv[4]; pend = getattr(unreal, "_sm2phys_pend", []); 
    for a in mine:
        if a.actor_has_tag("on") or a.actor_has_tag("off"):
            o, e = a.get_actor_bounds(False); r = e.length()
            for view, dirv in (("front", unreal.Vector(1, 0, 0)), ("side", unreal.Vector(0, 1, 0))):
                pos = o + dirv * (r * 2.2); rot = unreal.MathLibrary.find_look_at_rotation(pos, o)
                cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, pos, rot); cam.tags = [unreal.Name("SM2Phys")]
                c = cam.capture_component2d; rt = unreal.RenderingLibrary.create_render_target2d(cam, 768, 768, unreal.TextureRenderTargetFormat.RTF_RGBA8)
                c.set_editor_property("texture_target", rt); c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR)
                c.show_only_actor_components(a); E = unreal.SceneCapturePrimitiveRenderMode; c.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
                c.capture_scene(); pend.append((rt, "phys_%s_%s_%s_%s" % (name, tag, "physON" if a.actor_has_tag("on") else "animOnly", view)))
    unreal._sm2phys_pend = pend; print("PHYSV captured", len(pend))
elif mode == "export":
    out = W + "/Captures/mtl_fix"; pend = getattr(unreal, "_sm2phys_pend", [])
    for rt, n in pend: unreal.RenderingLibrary.export_render_target(unreal.EditorLevelLibrary.get_editor_world(), rt, out, n + ".png")
    unreal._sm2phys_pend = []
    for a in [a for a in eas.get_all_level_actors() if a.actor_has_tag("SM2Phys") and isinstance(a, unreal.SceneCapture2D)]: eas.destroy_actor(a)
    print("PHYSV exported", len(pend))



