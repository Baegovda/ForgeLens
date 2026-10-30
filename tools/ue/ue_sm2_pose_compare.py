# mtl1-45: old/new bake comparison in UE (uemcp ue_python). Modes: setup | sie_start | sie_pose <t> | sie_cap <outdir> <tag> | sie_export <outdir> <tag> | sie_stop | clean
# Working recipe: spawn tagged actors in the editor world -> SIE -> play_animation + rate 0 + set_position -> wait ~1 s -> capture -> export -> stop SIE -> clean.
# Editor-world components never re-evaluate from the bridge, EditorAssetLibrary refuses in play mode, and ShowOnly lists must point at the PIE copy.
# SETS / BASE below are the mtl1-44 test imports; edit them for other assets. The legacy pose/cap/export modes are kept for reference (they do not animate).
import sys, builtins, unreal
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem); w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
SETS = {"old": "/Game/ForgeLens/SM2/Tests/mtl44/old/cc_calgar/", "new": "/Game/ForgeLens/SM2/Tests/mtl44/new/cc_calgar/"}
BASE = {"old": unreal.Vector(300000, 310000, 50000), "new": unreal.Vector(310000, 310000, 50000)}
TAG = unreal.Name("SM2Cap44")
def tagged(): return [x for x in eas.get_all_level_actors() if x.actor_has_tag(TAG)]
def find_asset(folder, cls, needle=""):
    for p in unreal.EditorAssetLibrary.list_assets(folder, True, False):
        d = unreal.EditorAssetLibrary.find_asset_data(p)
        if str(d.asset_class_path.asset_name) == cls and needle in p: return unreal.load_asset(p)
mode = sys.argv[1]
def npp(c):
    for k in ("disable_post_process_blueprint", "b_disable_post_process_blueprint"):
        try: c.set_editor_property(k, True); print("NPP", k); return
        except Exception as e: pass
    print("NPP none", [m for m in dir(c) if "post" in m.lower()])
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
if mode == "rt_on":
    builtins._m44_rt = les.editor_get_viewport_realtime() if hasattr(les, "editor_get_viewport_realtime") else None
    les.editor_set_viewport_realtime(True); print("RT on, was", builtins._m44_rt)
elif mode == "rt_off":
    was = getattr(builtins, "_m44_rt", None)
    if was is not None: les.editor_set_viewport_realtime(was)
    print("RT restored", was)
elif mode == "pose":
    # Editor-world SkeletalMeshComponents never re-evaluated the single-node anim from the bridge (pose stuck at t=0),
    # so evaluate the AnimSequence with AnimPoseExtensions and drive a PoseableMeshComponent instead.
    t = float(sys.argv[2])
    if not getattr(builtins, "_m44_actors", None) or not all(a and unreal.SystemLibrary.is_valid(a) for a in builtins._m44_actors.values()):
        for x in tagged(): eas.destroy_actor(x)
        builtins._m44_actors = {}; builtins._m44_comp = {}
        sds = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem); L = unreal.SubobjectDataBlueprintFunctionLibrary
        for s, f in SETS.items():
            a = eas.spawn_actor_from_class(unreal.Actor, BASE[s]); a.tags = [TAG]
            hs = sds.k2_gather_subobject_data_for_instance(a)
            if not a.root_component:
                h, _ = sds.add_new_subobject(unreal.AddNewSubobjectParams(parent_handle=hs[0], new_class=unreal.SceneComponent, blueprint_context=None))
                hs = sds.k2_gather_subobject_data_for_instance(a)
            h, _ = sds.add_new_subobject(unreal.AddNewSubobjectParams(parent_handle=hs[-1] if len(hs) > 1 else hs[0], new_class=unreal.PoseableMeshComponent, blueprint_context=None))
            c = a.get_component_by_class(unreal.PoseableMeshComponent)
            c.set_skinned_asset_and_update(find_asset(f, "SkeletalMesh")) if hasattr(c, "set_skinned_asset_and_update") else c.set_skeletal_mesh(find_asset(f, "SkeletalMesh"))
            c.set_component_tick_enabled(True); a.set_actor_tick_enabled(True)
            print("TICKEN", c.is_component_tick_enabled(), c.get_editor_property("primary_component_tick").get_editor_property("start_with_tick_enabled") if hasattr(c, "primary_component_tick") else "?")
            c.set_editor_property("bounds_scale", 50.0)  # pose comes from root motion; ref-pose bounds would cull it
            a.set_actor_location(BASE[s], False, False)
            builtins._m44_actors[s] = a; builtins._m44_comp[s] = c
    o = unreal.AnimPoseEvaluationOptions(); res = []
    for s, a in builtins._m44_actors.items():
        anim = find_asset(SETS[s], "AnimSequence", "shield_run"); c = builtins._m44_comp[s]
        p = unreal.AnimPoseExtensions.get_anim_pose_at_time(anim, t, o)
        for n in unreal.AnimPoseExtensions.get_bone_names(p):
            c.set_bone_transform_by_name(n, unreal.AnimPoseExtensions.get_bone_pose(p, n, unreal.AnimPoseSpaces.WORLD), unreal.BoneSpaces.COMPONENT_SPACE)
        res.append(c.get_bone_location_by_name("FOOT1L", unreal.BoneSpaces.WORLD_SPACE))
    print("POSED", t, [(round(v.x, 1), round(v.y, 1), round(v.z, 1)) for v in res])
elif mode == "setup":
    # SIE path: editor-world components never tick from the bridge, so pose/capture inside a Simulate world.
    for x in tagged(): eas.destroy_actor(x)
    sds = unreal.get_engine_subsystem(unreal.SubobjectDataSubsystem)
    for s, f in SETS.items():
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, BASE[s]); a.tags = [TAG, unreal.Name("m44_" + s)]
        c = a.skeletal_mesh_component; c.set_skeletal_mesh_asset(find_asset(f, "SkeletalMesh"))
        c.set_editor_property("bounds_scale", 50.0)
        c.set_editor_property("visibility_based_anim_tick_option", unreal.VisibilityBasedAnimTickOption.ALWAYS_TICK_POSE_AND_REFRESH_BONES)
        npp(c)  # ABP_*_SM2Phys runtime sim would mask the baked cloth bones
        c.set_animation_mode(unreal.AnimationMode.ANIMATION_SINGLE_NODE)
        for view in ("back", "side"):
            rt = unreal.RenderingLibrary.create_render_target2d(w, 768, 768, unreal.TextureRenderTargetFormat.RTF_RGBA8)
            cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, BASE[s] + unreal.Vector(0, -500, 100)); cam.tags = [TAG, unreal.Name("m44cam_%s_%s" % (s, view))]
            cc = cam.capture_component2d; cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_NORMAL)
            cc.set_editor_property("fov_angle", 45); cc.set_editor_property("capture_every_frame", False); cc.set_editor_property("capture_on_movement", False)
            cc.show_only_actor_components(a)
            E = unreal.SceneCapturePrimitiveRenderMode; cc.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
    # EditorAssetLibrary refuses to work during play mode, so resolve the anims now.
    builtins._m44_anims = {s: find_asset(f, "AnimSequence", "shield_run") for s, f in SETS.items()}
    print("SETUP", len(tagged()), {k: v.get_name() for k, v in builtins._m44_anims.items()})
elif mode == "sie_start":
    builtins._m44_sie_play = {}; les.editor_play_simulate(); print("SIE start requested")
elif mode == "sie_stop":
    les.editor_request_end_play(); print("SIE stop requested")
elif mode in ("sie_pose", "sie_cap", "sie_export"):
    gw = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
    if gw is None: raise SystemExit("NO GAME WORLD")
    def g(n):
        r = unreal.GameplayStatics.get_all_actors_with_tag(gw, unreal.Name(n)); return r[0] if r else None
    if mode == "sie_pose":
        t = float(sys.argv[2]); o = unreal.AnimPoseEvaluationOptions(); res = []
        for s in SETS:
            c = g("m44_" + s).skeletal_mesh_component
            if c.get_editor_property("animation_data").get_editor_property("anim_to_play") is None or not getattr(builtins, "_m44_sie_play", {}).get(s):
                c.set_simulate_physics(False); c.set_all_bodies_simulate_physics(False)
                c.play_animation(builtins._m44_anims[s], False)
                builtins.__dict__.setdefault("_m44_sie_play", {})[s] = True
            c.set_play_rate(0.0); c.set_position(t, False)
            res.append(c.get_socket_location("FOOT1L")); res.append(unreal.Vector(c.get_position(), 0, 0))
        print("SPOSED", t, [(round(v.x, 1), round(v.y, 1), round(v.z, 1)) for v in res])
    elif mode == "sie_cap":
        for s in SETS:
            a = g("m44_" + s); c = a.skeletal_mesh_component
            o = (c.get_socket_location("FOOT1L") + c.get_socket_location("FOOT1R")) * 0.5
            o = unreal.Vector(o.x, o.y, BASE[s].z + 110)
            for view, d in (("back", unreal.Vector(0.6, -1.0, 0.25)), ("side", unreal.Vector(1.0, 0.1, 0.1))):
                d = d / d.length(); pos = o + d * 480
                cam = g("m44cam_%s_%s" % (s, view)); cam.set_actor_location_and_rotation(pos, unreal.MathLibrary.find_look_at_rotation(pos, o), False, False)
                cc = cam.capture_component2d; cc.clear_show_only_components(); cc.show_only_actor_components(a)  # PIE copy, not the editor actor
                cc.capture_scene()
        print("SCAP", sys.argv[2], sys.argv[3])
    else:
        out, tag = sys.argv[2], sys.argv[3]
        for s in SETS:
            for view in ("back", "side"):
                cc = g("m44cam_%s_%s" % (s, view)).capture_component2d
                unreal.RenderingLibrary.export_render_target(gw, cc.get_editor_property("texture_target"), out, "%s_%s_%s.png" % (tag, s, view))
        print("SEXP", tag)
elif mode == "cap":
    out, tag = sys.argv[2], sys.argv[3]
    pend = getattr(builtins, "_m44_pend", [])
    for s, a in builtins._m44_actors.items():
        c = builtins._m44_comp[s]
        B = unreal.BoneSpaces.WORLD_SPACE  # hips ~ midpoint of the feet + 95 cm (this rig has no PELVIS bone)
        o = (c.get_bone_location_by_name("FOOT1L", B) + c.get_bone_location_by_name("FOOT1R", B)) * 0.5
        o = unreal.Vector(o.x, o.y, a.get_actor_location().z + 110)
        o = o + unreal.Vector(0, 0, -25)
        for view, d in (("back", unreal.Vector(0.6, -1.0, 0.25)), ("side", unreal.Vector(1.0, 0.1, 0.1))):
            d = d / d.length(); pos = o + d * 480
            rt = unreal.RenderingLibrary.create_render_target2d(w, 768, 768, unreal.TextureRenderTargetFormat.RTF_RGBA8)
            cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, pos, unreal.MathLibrary.find_look_at_rotation(pos, o)); cam.tags = [TAG]
            cc = cam.capture_component2d; cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_NORMAL)
            cc.set_editor_property("fov_angle", 45); cc.show_only_actor_components(a)
            E = unreal.SceneCapturePrimitiveRenderMode; cc.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
            cc.capture_scene(); pend.append((rt, out, "%s_%s_%s.png" % (tag, s, view)))
    builtins._m44_pend = pend; print("CAP", tag, len(pend))
elif mode == "export":
    for rt, out, name in builtins._m44_pend: unreal.RenderingLibrary.export_render_target(w, rt, out, name)
    print("EXPORTED", len(builtins._m44_pend)); builtins._m44_pend = []
else:
    for x in tagged(): eas.destroy_actor(x)
    builtins._m44_actors = {}; builtins._m44_pend = []; print("CLEAN")
