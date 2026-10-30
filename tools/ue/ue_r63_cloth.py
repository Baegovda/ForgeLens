# ue_r63_cloth.py (R63): readable cloth-physics check of the R53 cloth sorcerer (cc_sorcerer_exalted, ChaosCloth).
# Scratch map /Game/SM2_Work/Tests/r63_cloth/Map_Cloth: grey floor, directional + sky + 2 fill lights, fixed exposure,
# two actors of the SAME cloth mesh: R63_on (cloth simulating) and R63_off (cloth disabled = skinned pose) for an A/B.
# One SceneCapture per actor (close 3/4 back view on cloak/skirt/tabard), viewport-independent.
# Modes (uerun.ps1 <this> <mode>[,args]):
#   prep                duplicate /Game/SM2_Work/Characters/cc_sorcerer_exalted (materials kept) into Tests/r63_cloth and run
#                       create_chaos_cloth with the chars_cloth physics.json (falls back to the R53 FBX import if 0 cloths)
#   scene[,ev]          new scratch map + lights + floor + actors + captures (+ unbound PPV, manual exposure bias ev)
#   anim,<asset>        looping clip on both actors
#   cap,<dir>,<tag>     write <dir>/<tag>_on.png and <tag>_off.png
#   sim | stop          Simulate start / end
#   osc,<sec>,<amp>     move both actors (sin x, hz 0.7) for sec, then hold
#   measure,<tag>       plugin cloth metrics for both actors (penetration / skin / wrong side), NaN check of bounds
#   tune,<k=v;k=v>      set_cloth_config_props on the r63 mesh
import sys, os, math, json, unreal
W = r"C:/Users/Revaptor_FX/Desktop/SM2_Work"; N = "cc_sorcerer_exalted"
SRC = "/Game/SM2_Work/Characters/%s/%s" % (N, N); FOLD = "/Game/SM2_Work/Tests/r63_cloth"; MESH = FOLD + "/" + N + "_cloth"
MAP = FOLD + "/Map_Cloth"; PJ = W + "/Exports/chars_cloth/%s/tpl/%s.tpl/%s.physics.json" % (N, N, N)
EAL = unreal.EditorAssetLibrary; L = unreal.SM2PhysicsSetupLibrary
def les(): return unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
def eas(): return unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def world():
    u = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem); return u.get_game_world() or u.get_editor_world()
def acts(cls=unreal.SkeletalMeshActor, pre="R63_"):
    return {a.get_actor_label(): a for a in unreal.GameplayStatics.get_all_actors_of_class(world(), cls) if a.get_actor_label().startswith(pre)}
def acts_sim(cls=unreal.SkeletalMeshActor, pre="R63_"):  # sim copies keep tags, not always labels
    out = {}
    for a in unreal.GameplayStatics.get_all_actors_of_class(world(), cls):
        t = [str(x) for x in a.tags if str(x).startswith(pre)]
        if t: out[t[0]] = a
    return out

def prep():
    if EAL.does_asset_exist(MESH): EAL.delete_asset(MESH)
    m = EAL.duplicate_asset(SRC, MESH); print("R63 dup", m is not None)
    r = L.create_chaos_cloth(m, PJ); conv, rep = (r if isinstance(r, tuple) else ([], ""))[-2:]
    print("R63 converted", [str(c) for c in conv])
    for l in str(rep).split(" | ")[:40]: print("R63LOG", l[:240])
    print("R63 clothing assets", len(m.get_editor_property("mesh_clothing_assets")))
    EAL.save_asset(MESH, only_if_is_dirty=False)

def grey_mat():
    p = FOLD + "/M_r63_grey"
    if EAL.does_asset_exist(p): return unreal.load_asset(p)
    mat = unreal.AssetToolsHelpers.get_asset_tools().create_asset("M_r63_grey", FOLD, unreal.Material, unreal.MaterialFactoryNew())
    MEL = unreal.MaterialEditingLibrary
    c = MEL.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -300, 0)
    c.set_editor_property("constant", unreal.LinearColor(0.18, 0.18, 0.18, 1))
    MEL.connect_material_property(c, "", unreal.MaterialProperty.MP_BASE_COLOR)
    r = MEL.create_material_expression(mat, unreal.MaterialExpressionConstant, -300, 200); r.set_editor_property("r", 0.8)
    MEL.connect_material_property(r, "", unreal.MaterialProperty.MP_ROUGHNESS)
    MEL.recompile_material(mat); EAL.save_loaded_asset(mat); return mat

POS = {"R63_on": (-400, 0), "R63_off": (400, 0)}
def pp(s, ev):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True)
    s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)

def scene(ev=0.0):
    les().new_level(MAP); E = eas()
    fl = E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"), unreal.Vector(0, 0, 0))
    fl.set_actor_scale3d(unreal.Vector(30, 30, 1)); fl.set_actor_label("R63floor")
    fl.static_mesh_component.set_material(0, grey_mat())
    d = E.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 800), unreal.Rotator(roll=0, pitch=-45, yaw=60))
    d.light_component.set_intensity(4.0)
    s = E.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 500)); s.light_component.set_intensity(1.5)
    s.light_component.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP)
    s.light_component.set_editor_property("cubemap", unreal.load_asset("/Engine/MapTemplates/Sky/SunsetAmbientCubemap"))
    s.light_component.recapture_sky()
    for x, y, z in [(0, 300, 250), (0, -300, 250)]:
        p = E.spawn_actor_from_class(unreal.PointLight, unreal.Vector(x, y, z)); p.point_light_component.set_intensity(8000)
        p.point_light_component.set_attenuation_radius(1500)
    sp = E.spawn_actor_from_class(unreal.SpotLight, unreal.Vector(0, 450, 350), unreal.Rotator(roll=0, pitch=-30, yaw=-90))
    sp.spot_light_component.set_intensity(15000); sp.spot_light_component.set_outer_cone_angle(50)
    v = E.spawn_actor_from_class(unreal.PostProcessVolume, unreal.Vector(0, 0, 0)); v.set_editor_property("unbound", True)
    st = v.get_editor_property("settings"); pp(st, ev); v.set_editor_property("settings", st)
    mesh = unreal.load_asset(MESH)
    rt_tools = unreal.AssetToolsHelpers.get_asset_tools()
    for k, (x, y) in POS.items():
        a = E.spawn_actor_from_class(unreal.SkeletalMeshActor, unreal.Vector(x, y, 0), unreal.Rotator(0, 0, 90))
        c = a.skeletal_mesh_component; c.set_skinned_asset_and_update(mesh)
        c.set_editor_property("disable_post_process_blueprint", True)
        if k == "R63_off": c.set_editor_property("disable_cloth_simulation", True)
        a.set_actor_label(k); a.tags = [k]
        rtp = FOLD + "/RT_" + k
        rt = unreal.load_asset(rtp) if EAL.does_asset_exist(rtp) else rt_tools.create_asset("RT_" + k, FOLD, unreal.TextureRenderTarget2D, unreal.TextureRenderTargetFactoryNew())
        rt.set_editor_property("size_x", 1000); rt.set_editor_property("size_y", 1200)
        rt.set_editor_property("render_target_format", unreal.TextureRenderTargetFormat.RTF_RGBA8); EAL.save_loaded_asset(rt)
        # character faces +Y (yaw 90 mesh rotation) -> back is -Y; camera behind-left, chest height
        cap = E.spawn_actor_from_class(unreal.SceneCapture2D, unreal.Vector(x + 120, y - 260, 170), unreal.Rotator(roll=0, pitch=-8, yaw=65))
        cap.set_actor_label("R63cap_" + k); cap.tags = ["R63cap_" + k]; cc = cap.capture_component2d
        cc.set_editor_property("texture_target", rt); cc.set_editor_property("capture_every_frame", False)
        cc.set_editor_property("capture_source", unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR); cc.set_editor_property("fov_angle", 50)
        ps = cc.get_editor_property("post_process_settings"); pp(ps, ev); cc.set_editor_property("post_process_settings", ps)
        cc.set_editor_property("post_process_blend_weight", 1.0)
    les().save_current_level(); print("R63 scene", MAP, list(acts()))

def setcam(k, x, y, z, pitch, yaw, fov):
    c = acts_sim(unreal.SceneCapture2D, "R63cap_")["R63cap_" + k]
    c.set_actor_location_and_rotation(unreal.Vector(x, y, z), unreal.Rotator(roll=0, pitch=pitch, yaw=yaw), False, False)
    c.capture_component2d.set_editor_property("fov_angle", fov)
    if not unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world(): les().save_current_level()

def cap(d, tag):
    for k, c in acts_sim(unreal.SceneCapture2D, "R63cap_").items():
        cc = c.capture_component2d; cc.capture_scene()
        unreal.RenderingLibrary.export_render_target(world(), cc.get_editor_property("texture_target"), d.replace("\\", "/"), "%s_%s.png" % (tag, k[11:]))
    print("R63 cap", d, tag)

def anim(p):
    for k, a in acts().items():
        c = a.skeletal_mesh_component; c.set_editor_property("animation_mode", unreal.AnimationMode.ANIMATION_SINGLE_NODE)
        dd = unreal.SingleAnimationPlayData(); dd.set_editor_property("anim_to_play", unreal.load_asset(p) if p != "none" else None)
        dd.set_editor_property("saved_looping", True); dd.set_editor_property("saved_playing", p != "none")
        c.set_editor_property("animation_data", dd)
    les().save_current_level(); print("R63 anim", p)

def osc(sec, amp, hz=0.7):
    st = {"t": 0.0, "a": [(a, a.get_actor_location()) for a in acts_sim().values()]}
    def tick(dt):
        st["t"] += dt; t = st["t"]
        for a, p0 in st["a"]:
            off = math.sin(2 * math.pi * hz * t) * amp if t < sec else 0.0
            a.set_actor_location(p0 + unreal.Vector(off, 0, 0), False, False)
        if t > sec: unreal.unregister_slate_post_tick_callback(st["h"]); unreal.log("R63 osc done")
    st["h"] = unreal.register_slate_post_tick_callback(tick); print("R63 osc", sec, amp, len(st["a"]))

def measure(tag):
    for k, a in acts_sim().items():
        c = a.skeletal_mesh_component; out = {"tag": tag, "actor": k}
        b = c.get_editor_property("bounds") if False else None
        o, ext = a.get_actor_bounds(False); out["bounds"] = [o.x, o.y, o.z, ext.x, ext.y, ext.z]
        out["nan"] = any(math.isnan(v) or math.isinf(v) for v in out["bounds"])
        for f, args in (("measure_cloth_penetration", (1.0,)), ("measure_cloth_skin_penetration", (1.0, 15.0)), ("measure_cloth_wrong_side", (2.0, 30.0))):
            try:
                r = getattr(L, f)(c, *args); out[f] = [r[0], str(r[1])[:900]] if isinstance(r, tuple) else [r, ""]
            except Exception as ex: out[f] = ["ERR", str(ex)[:200]]
        print("R63M", json.dumps(out))

def tune(kv):
    m = unreal.load_asset(MESH); r = L.set_cloth_config_props(m, kv); print("R63 tune", r); EAL.save_asset(MESH, only_if_is_dirty=False)

if __name__ == "__main__":
    a = sys.argv[1].split(",") if len(sys.argv) == 2 else sys.argv[1:]; m = a[0]
    if m == "prep": prep()
    elif m == "scene": scene(float(a[1]) if len(a) > 1 else 0.0)
    elif m == "anim": anim(a[1])
    elif m == "setcam": setcam(a[1], *[float(x) for x in a[2:8]])
    elif m == "cap": cap(a[1], a[2])
    elif m == "sim": les().editor_play_simulate(); print("R63 sim")
    elif m == "stop": les().editor_request_end_play(); print("R63 stop")
    elif m == "osc": osc(float(a[1]), float(a[2]))
    elif m == "measure": measure(a[1])
    elif m == "tune": tune(a[1])
    elif m == "ev":
        for c in acts_sim(unreal.SceneCapture2D, "R63cap_").values():
            cc = c.capture_component2d; ps = cc.get_editor_property("post_process_settings"); pp(ps, float(a[1])); cc.set_editor_property("post_process_settings", ps)
        for v in unreal.GameplayStatics.get_all_actors_of_class(world(), unreal.PostProcessVolume):
            st = v.get_editor_property("settings"); pp(st, float(a[1])); v.set_editor_property("settings", st)
        print("R63 ev", a[1])
    elif m == "place":  # re-apply POS + standard back cameras (fov 32) in the editor world
        for k, (x, y) in POS.items():
            acts()[k].set_actor_location(unreal.Vector(x, y, 0), False, True)
            setcam(k, x, 760, 290, -7, -90, 32)
        les().save_current_level(); print("R63 placed")
    elif m == "save_r54": les().save_current_level(); print("R63 saved", world().get_path_name())
    elif m == "open": les().load_level(a[1]); print("R63 open", a[1])
