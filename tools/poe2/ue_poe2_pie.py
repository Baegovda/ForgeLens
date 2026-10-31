"""PoE2 M3.4: Simulate-in-editor swing check of the PoE2 phys_ bones (editor only, never the game). Scratch map
/Game/ForgeLens/POE/Tests/pie/Map_PhysCheck. Reuses ue_pie_phys.py (osc/result/snap/stop: actors tagged R60_*).
steps: setup | anims | sim | osc,<sec>,<amp>[,hz,tag] | cap,<png> | stop | result | tune,<world>,<dampAlpha>,<stiff>,<damp>,<swing>[,<mesh>] | restore,<map>
Refuses setup when the open map has unsaved changes (other workers' maps are never saved or switched away from dirty)."""
import sys, os, json, unreal
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "ue"))
import ue_pie_phys as P

MAP = "/Game/ForgeLens/POE/Tests/pie/Map_PhysCheck"
MESHES = {"bellcrow": "/Game/ForgeLens/POE/bellcrow/bellcrow", "lichboss": "/Game/ForgeLens/POE/lichboss/lichboss",
          "cartrhoa": "/Game/ForgeLens/POE/cartrhoa/cartrhoa"}
EAL = unreal.EditorAssetLibrary

def idle(name):
    for a in EAL.list_assets(f"/Game/ForgeLens/POE/{name}/Anims", recursive=False):
        if "idle" in a.lower(): return a.split(".")[0]
    return None

def setup():
    dirty = [str(p.get_name()) for p in unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages()]
    if [d for d in dirty if not d.startswith(("/Game/ForgeLens/POE", "/Temp/"))]:
        print("POE3 refuse: dirty maps", dirty); return
    w = P.world(); prev = w.get_path_name().split(".")[0] if w else ""
    if prev and not prev.startswith(("/Game/ForgeLens/POE", "/Temp/")):
        json.dump({"prev": prev}, open(os.path.join(P.SAVED, "poe3_prev.json"), "w"))
    if EAL.does_asset_exist(MAP): P.les().load_level(MAP)
    else: P.les().new_level(MAP)
    eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
    for a in eas.get_all_level_actors():
        if a.get_actor_label().startswith(("R60_", "POE3_")): eas.destroy_actor(a)
    fl = eas.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(60, 30, 1)); fl.set_actor_label("POE3_floor")
    eas.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(roll=0, pitch=-40, yaw=-120)).set_actor_label("POE3_sun")
    eas.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500)).set_actor_label("POE3_sky")
    for k, x in zip(MESHES, (-700, 0, 700)):
        if not EAL.does_asset_exist(MESHES[k]): print("POE3 missing", MESHES[k]); continue
        a = eas.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(x, 0, 0), unreal.Rotator(0, 0, 0))
        a.skeletal_mesh_component.set_skinned_asset_and_update(unreal.load_asset(MESHES[k]))
        a.set_actor_label("R60_" + k); a.tags = ["R60_" + k]
    rt_path = MAP.rsplit("/", 1)[0] + "/RT_poe3cap"
    rt = unreal.load_asset(rt_path) if EAL.does_asset_exist(rt_path) else unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "RT_poe3cap", MAP.rsplit("/", 1)[0], unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
    rt.set_editor_property("size_x", 1600); rt.set_editor_property("size_y", 800)
    rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB)
    rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8)  # non-sRGB target
    EAL.save_loaded_asset(rt)
    cap = eas.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(-450, 1150, 260), unreal.Rotator(roll=0, pitch=-6, yaw=-68))
    cap.set_actor_label("POE3_cap"); c = cap.capture_component2d
    c.set_editor_property("texture_target", rt); c.set_editor_property("capture_every_frame", False)
    c.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
    P.les().save_current_level()
    print("POE3 setup", MAP, [a.get_actor_label() for a in P.actors()], "prev", prev)

def anims():
    for x in P.actors():
        k = str(x.tags[0])[4:] if x.tags else ""
        clip = idle(k)
        if not clip: print("POE3 no idle", k); continue
        c = x.skeletal_mesh_component; c.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
        d = unreal.SingleAnimationPlayData(); d.set_editor_property("anim_to_play", unreal.load_asset(clip))
        d.set_editor_property("saved_looping", True); d.set_editor_property("saved_playing", True)
        c.set_editor_property("animation_data", d); print("POE3 anim", k, clip)
        mesh = c.get_skinned_asset()
        print("POE3 ppabp", k, mesh.get_editor_property("post_process_anim_blueprint"), "pa", mesh.get_editor_property("physics_asset"))
    P.les().save_current_level()

def cap(png):
    caps = [a for a in unreal.GameplayStatics.get_all_actors_of_class(P.world(), unreal.SceneCapture2D)]
    c = caps[0].capture_component2d; c.capture_scene()
    d, f = os.path.split(png.replace("\\", "/"))
    unreal.RenderingLibrary.export_render_target(P.world(), c.get_editor_property("texture_target"), d, f)
    print("POE3 cap", png, os.path.exists(png))

def tune(world_alpha, damp_alpha, stiff, damp, swing=1.0, only=""):
    """Feed the actor's world motion into the RigidBody node (WorldAlpha/DampingAlpha) and give every phys_ joint a SLERP spring
    (acceleration drive). Values are HEURISTIC (no game spring data). Saves only the POE PA/ABP assets."""
    out = {}
    for k, mp in MESHES.items():
        if only and k != only: continue
        if not EAL.does_asset_exist(mp): continue
        mesh = unreal.load_asset(mp); pa = mesh.get_editor_property("physics_asset")
        abp_cls = mesh.get_editor_property("post_process_anim_blueprint")
        abp = unreal.load_asset(abp_cls.get_path_name().rsplit(".", 1)[0]) if abp_cls else None
        X = unreal.AnimPoseExtensions; names = X.get_bone_names(X.get_reference_pose(mesh.get_editor_property("skeleton")))
        base = names[1] if len(names) > 1 else names[0]
        r1 = unreal.SM2PhysicsSetupLibrary.tune_rigid_body(abp, 2, base, world_alpha, damp_alpha, 600.0, 2000.0, 10.0, 100.0) if abp else None
        r2 = unreal.SM2PhysicsSetupLibrary.tune_constraint_drive(pa, stiff, damp, swing) if pa else None
        for a in (abp, pa):
            if a: EAL.save_loaded_asset(a, False)
        out[k] = {"abp": str(r1), "pa": str(r2), "base": str(base)}
        print("POE3 tune", k, out[k])
    json.dump(out, open(os.path.join(P.SAVED, "poe3_tune_%s.json" % (only or "all")), "w"))

def grav(on, only):
    pa = unreal.load_asset(MESHES[only]).get_editor_property("physics_asset")
    n = unreal.SM2PhysicsSetupLibrary.set_sim_gravity(pa, bool(int(on))); EAL.save_loaded_asset(pa, False)
    print("POE3 grav", only, on, "bodies", n)

def desc(only, pattern):
    mesh = unreal.load_asset(MESHES[only]); pa = mesh.get_editor_property("physics_asset")
    X = unreal.AnimPoseExtensions
    for b in X.get_bone_names(X.get_reference_pose(mesh.get_editor_property("skeleton"))):
        if pattern in str(b): print("POE3 desc", unreal.SM2PhysicsSetupLibrary.describe_body(pa, mesh, b))

def kin(only, pattern):
    """Make the matching bodies kinematic (follow animation). Used for chain roots that hang far from their kinematic parent."""
    pa = unreal.load_asset(MESHES[only]).get_editor_property("physics_asset")
    try: bs = pa.get_editor_property("skeletal_body_setups")
    except Exception as e: print("POE3 kin no access", e); return
    n = 0
    for b in bs:
        if pattern in str(b.get_editor_property("bone_name")):
            b.set_editor_property("physics_type", unreal.PhysicsType.PHYS_TYPE_KINEMATIC); n += 1
            print("POE3 kin", only, b.get_editor_property("bone_name"))
    EAL.save_loaded_asset(pa, False); print("POE3 kin count", n)

def restore():
    prev = json.load(open(os.path.join(P.SAVED, "poe3_prev.json"))).get("prev", "")
    if P.world() and P.world().get_path_name().startswith("/Game/ForgeLens/POE"): P.les().save_current_level()
    if prev and not prev.startswith("/Game/ForgeLens/POE") and EAL.does_asset_exist(prev):
        P.les().load_level(prev); print("POE3 restored", prev)
    else: print("POE3 no restore", prev)

def blank():
    """Leave a foreign map WITHOUT saving it (its unsaved edits are discarded; the saved file is untouched), remember it for restore."""
    w = P.world(); prev = w.get_path_name().split(".")[0] if w else ""
    dirty = [str(p.get_name()) for p in unreal.EditorLoadingAndSavingUtils.get_dirty_map_packages()]
    json.dump({"prev": prev, "discarded_dirty": dirty}, open(os.path.join(P.SAVED, "poe3_prev.json"), "w"))
    unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
    print("POE3 blank: left", prev, "unsaved (dirty was", dirty, ")")

if __name__ == "__main__":
    a = sys.argv[1].split(",") if len(sys.argv) == 2 else sys.argv[1:]
    m = a[0]
    if m == "setup": setup()
    elif m == "anims": anims()
    elif m == "cap": cap(a[1])
    elif m == "restore": restore()
    elif m == "blank": blank()
    elif m == "tune": tune(*[float(x) for x in a[1:6]], only=a[6] if len(a) > 6 else "")
    elif m == "sim": P.les().editor_play_simulate(); print("POE3 sim started")
    elif m == "stop": P.les().editor_request_end_play(); print("POE3 stopped")
    elif m == "osc": P.osc(float(a[1]), float(a[2]), float(a[3]) if len(a) > 3 else 1.0, a[4] if len(a) > 4 else "phys")
    elif m == "result": P.result()
    elif m == "grav": grav(a[1], a[2])
    elif m == "kin": kin(a[1], a[2])
    elif m == "desc": desc(a[1], a[2])
    elif m == "list": print("POE3 world", P.world().get_path_name(), [x.get_actor_label() for x in P.actors()])
