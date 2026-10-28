# ue_c28.py spawn|start|sample|sampleref|cap|exp <tag>|kick|stop|clean <name> <set>
# sampleskin (mtl1-32): free particles behind the skinned body surface (non-cloth sections), nearest vertex within 10 cm.
# sampleref (mtl1-31): penetration of every cloth vs a FIXED reference = the mesh's body PhysicsAsset (independent of the cloth colliders).: Chaos Cloth live check of the unsaved diag duplicate (ue_cdiag) vs the base mesh.
import unreal, sys, math
mode, name, SET = sys.argv[1], sys.argv[2], sys.argv[3]
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; TAG = "C28"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); ues = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
base = unreal.load_asset("/Game/SM2_Work/Tests/%s/%s/%s" % (SET, name, name))
diag = unreal.load_asset("/Game/SM2_Work/Tests/diag_unsaved/%s_diag" % name)
if diag is None:  # mtl1-32: no diag duplicate -> check the SAVED <name>_ChaosCloth
    diag = unreal.load_asset("/Game/SM2_Work/Tests/%s/%s/%s_ChaosCloth" % (SET, name, name))
O = unreal.Vector(0, 60000, 0)
def acts(world):
    return unreal.GameplayStatics.get_all_actors_with_tag(world, TAG)
if mode == "spawn":
    print("LEVEL", ues.get_editor_world().get_name())
    bb = base.get_bounds(); e = bb.box_extent; gap = max(e.x, e.y) * 2.2
    for lab, m, dx in (("base", base, -gap / 2), ("cloth", diag, gap / 2)):
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, O + unreal.Vector(dx, 0, 0), unreal.Rotator(0, 0, 0))
        a.skeletal_mesh_component.set_skeletal_mesh_asset(m); a.tags = [unreal.Name(TAG), unreal.Name(lab)]; a.set_actor_label("C28_" + lab)
    half = max(e.z, gap) * 0.75; fov = 30.0; dist = half / math.tan(math.radians(fov / 2)) + e.y
    c = O + bb.origin; pos = c + unreal.Vector(0, dist, 0)
    cam = eas.spawn_actor_from_class(unreal.SceneCapture2D, pos, unreal.MathLibrary.find_look_at_rotation(pos, c)); cam.tags = [unreal.Name(TAG), unreal.Name("cam")]
    cam.capture_component2d.set_editor_property("capture_every_frame", False); cam.capture_component2d.set_editor_property("fov_angle", fov)
    print("C28 spawned", diag is not None, len(diag.get_editor_property("mesh_clothing_assets")) if diag else -1)
elif mode == "start":
    les.editor_play_simulate(); print("C28 sim start")
elif mode == "stop":
    les.editor_request_end_play(); print("C28 sim stop")
elif mode == "clean":
    for a in acts(ues.get_editor_world()): eas.destroy_actor(a)
    print("C28 cleaned")
else:
    w = ues.get_game_world() or ues.get_editor_world(); print("C28 world", w.get_name())
    A = {[str(t) for t in a.tags if str(t) != TAG][0]: a for a in acts(w)}
    if mode == "sample":
        for lab in ("base", "cloth"):
            r = unreal.SM2PhysicsSetupLibrary.measure_cloth_penetration(A[lab].skeletal_mesh_component, 1.0)
            print("C28", lab, r)
    elif mode == "sampleref":
        ref = base.get_editor_property("physics_asset")
        r = unreal.SM2PhysicsSetupLibrary.measure_cloth_penetration(A["cloth"].skeletal_mesh_component, 1.0, ref)
        print("C28 clothREF", ref.get_name() if ref else None, r)
    elif mode == "sampleskin":  # mtl1-32: vs the CPU-skinned body surface of the same mesh (HEURISTIC)
        r = unreal.SM2PhysicsSetupLibrary.measure_cloth_skin_penetration(A["cloth"].skeletal_mesh_component, 1.0, 10.0)
        print("C28 clothSKIN", r)
    elif mode == "kick":
        for lab in ("base", "cloth"): A[lab].add_actor_world_offset(unreal.Vector(0, 0, 80), False, False)
        print("C28 kicked +80z")
    elif mode == "cap":
        cam = A["cam"].capture_component2d; rt = cam.get_editor_property("texture_target")
        if rt is None:
            rt = unreal.RenderingLibrary.create_render_target2d(w, 1024, 768, unreal.TextureRenderTargetFormat.RTF_RGBA8); cam.set_editor_property("texture_target", rt)
        cam.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_BASE_COLOR)
        cam.show_only_actor_components(A["base"]); cam.show_only_actor_components(A["cloth"])
        E = unreal.SceneCapturePrimitiveRenderMode; cam.set_editor_property("primitive_render_mode", next(getattr(E, k) for k in dir(E) if "SHOW_ONLY" in k))
        cam.capture_scene(); print("C28 captured")
    elif mode == "exp":
        tag = sys.argv[4]; rt = A["cam"].capture_component2d.get_editor_property("texture_target")
        unreal.RenderingLibrary.export_render_target(w, rt, W + "/Captures/mtl_fix/mtl28", "c28_%s.png" % tag); print("C28 exp", tag)
